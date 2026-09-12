using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>固定边集的有界方向搜索；每工作项最多一个方向状态或一次坐标迭代，候选经独立验收。</summary>
    public sealed class MapGraphOrthogonalLayoutSolver
    {
        private readonly MapGraphLayoutDraft _reference;
        private readonly MapGraphGenerationSettings _settings;
        private readonly MapGraphEdgeDefinition[] _edges;
        private readonly int[] _from, _to, _directions, _order;
        private readonly int[][] _choices;
        private readonly bool[,] _ports;
        private readonly int _maximumStates, _maximumSolutions, _maximumCoordinateIterations;
        private readonly Dictionary<string, int> _failures = new Dictionary<string, int>(StringComparer.Ordinal);
        private IEnumerator<int> _search;
        private MapGraphLayoutDraft _best;
        private double _bestScore = double.PositiveInfinity;
        public MapGraphLayoutDraft Result => IsComplete && !IsCancelled ? _best : null;
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool BudgetExhausted { get; private set; }
        public int SearchStates { get; private set; }
        public int FeasibleLayouts { get; private set; }
        public int CoordinateIterations { get; private set; }
        public double ElapsedMilliseconds { get; private set; }
        public double BestScore => _bestScore;
        public IReadOnlyDictionary<string, int> FailureCounts { get; }

        public MapGraphOrthogonalLayoutSolver(MapGraphLayoutDraft reference, IEnumerable<MapGraphEdgeDefinition> fixedEdges,
            MapGraphGenerationSettings settings, int maximumStates = 0, int maximumSolutions = 8, int maximumCoordinateIterations = 0)
        {
            _reference = reference ?? throw new ArgumentNullException(nameof(reference));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _maximumStates = maximumStates > 0 ? maximumStates : settings.MaximumSearchStates;
            _maximumSolutions = Math.Max(1, maximumSolutions);
            _maximumCoordinateIterations = maximumCoordinateIterations > 0 ? maximumCoordinateIterations : int.MaxValue;
            var edges = new List<MapGraphEdgeDefinition>(fixedEdges ?? throw new ArgumentNullException(nameof(fixedEdges)));
            edges.Sort((a, b) => string.CompareOrdinal(a?.EdgeId, b?.EdgeId)); _edges = edges.ToArray();
            _from = new int[edges.Count]; _to = new int[edges.Count]; _directions = new int[edges.Count];
            _order = new int[edges.Count]; _choices = new int[edges.Count][];
            _ports = new bool[reference.Nodes.Count, 4];
            FailureCounts = new ReadOnlyDictionary<string, int>(_failures);
            var draft = new MapGraphLayoutDraft(reference.Zones, reference.Nodes, edges, reference.Constraints, reference.StartNodeId);
            if (!draft.Graph.IsValid) { Reject("InvalidFixedTopology"); IsComplete = true; return; }
            foreach (var node in reference.Nodes)
                if (!MapGraphGeometry.Finite(reference.Graph.GetNodePosition(node.NodeId)) || !MapGraphGeometry.Positive(node.Footprint))
                { Reject("InvalidReferenceNode:" + node.NodeId); IsComplete = true; return; }
            foreach (var zone in reference.Zones)
                if (!MapGraphGeometry.Finite(zone.Bounds) || !MapGraphGeometry.Positive(zone.Bounds.size) || !MapGraphGeometry.Positive(zone.NameSafeSize))
                { Reject("InvalidReferenceZone:" + zone.ZoneId); IsComplete = true; return; }
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < reference.Nodes.Count; i++) indices.Add(reference.Nodes[i].NodeId, i);
            var degree = new int[reference.Nodes.Count];
            for (int e = 0; e < _edges.Length; e++)
            {
                var edge = _edges[e];
                _from[e] = indices[edge.FromNodeId]; _to[e] = indices[edge.ToNodeId]; _order[e] = e;
                degree[_from[e]]++; degree[_to[e]]++;
                _choices[e] = DirectionChoices(edge);
            }
            for (int i = 0; i < degree.Length; i++)
                if (degree[i] > 4) { Reject("MoreThanFourPorts:" + reference.Nodes[i].NodeId); IsComplete = true; return; }
            Array.Sort(_order, (a, b) =>
            {
                int comparison = (degree[_from[b]] + degree[_to[b]]).CompareTo(degree[_from[a]] + degree[_to[a]]);
                return comparison == 0 ? string.CompareOrdinal(_edges[a].EdgeId, _edges[b].EdgeId) : comparison;
            });
            _search = Explore(0).GetEnumerator();
        }

        public int Advance(int maximumWorkItems)
        {
            if (IsComplete || IsCancelled || maximumWorkItems <= 0) return 0;
            long started = Stopwatch.GetTimestamp(); int work = 0;
            while (work < maximumWorkItems)
            {
                if (!_search.MoveNext()) { IsComplete = true; _search.Dispose(); _search = null; break; }
                work++;
            }
            ElapsedMilliseconds += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            return work;
        }

        public void Cancel()
        {
            if (IsComplete || IsCancelled) return;
            IsCancelled = true; _search?.Dispose(); _search = null; _best = null; _bestScore = double.PositiveInfinity;
        }

        private IEnumerable<int> Explore(int depth)
        {
            if (FeasibleLayouts >= _maximumSolutions) yield break;
            if (depth == _edges.Length)
            {
                if (!ReserveState()) yield break;
                yield return 1;
                var coordinates = new MapGraphLayoutCoordinates(_reference, _edges, _from, _to, _directions, _settings,
                    _maximumCoordinateIterations - CoordinateIterations);
                try
                {
                    while (!coordinates.IsComplete)
                    {
                        int before = coordinates.Iterations; coordinates.Advance(1);
                        CoordinateIterations += coordinates.Iterations - before; yield return 1;
                    }
                    var candidate = coordinates.Result;
                    if (candidate != null)
                    {
                        var validation = MapGraphValidation.Validate(candidate, _reference);
                        var score = MapGraphLayoutScore.Evaluate(candidate, _reference, _settings);
                        if (validation.IsValid && score.IsFinite)
                        {
                            FeasibleLayouts++;
                            if (score.Total < _bestScore) { _bestScore = score.Total; _best = candidate; }
                        }
                        else Reject(validation.IsValid ? "NonFiniteScore" : validation.Issues[0].ToString());
                    }
                    else Reject(coordinates.Failure ?? "CoordinateSolveFailed");
                }
                finally { if (!coordinates.IsComplete) coordinates.Cancel(); }
                yield return 1; yield break;
            }
            int edge = _order[depth], a = _from[edge], b = _to[edge];
            foreach (int direction in _choices[edge])
            {
                if (FeasibleLayouts >= _maximumSolutions || !ReserveState()) yield break;
                yield return 1;
                if (_ports[a, direction] || _ports[b, direction ^ 1]) { Reject("PortAlreadyUsed"); continue; }
                _ports[a, direction] = true; _ports[b, direction ^ 1] = true; _directions[edge] = direction;
                foreach (int step in Explore(depth + 1)) yield return step;
                _ports[a, direction] = false; _ports[b, direction ^ 1] = false;
                if (BudgetExhausted) yield break;
            }
        }

        private int[] DirectionChoices(MapGraphEdgeDefinition edge)
        {
            Vector2 delta = _reference.Graph.GetNodePosition(edge.ToNodeId) - _reference.Graph.GetNodePosition(edge.FromNodeId);
            var values = new List<int>();
            for (int direction = 0; direction < 4; direction++)
            {
                bool horizontal = direction < 2;
                if (MapGraphLayoutIntentValidation.IsPinnedEdge(edge) && edge.Axis != MapGraphAxis.Unspecified &&
                    horizontal != (edge.Axis == MapGraphAxis.Horizontal)) continue;
                values.Add(direction);
            }
            values.Sort((a, b) =>
            {
                int c = DirectionCost(a, delta).CompareTo(DirectionCost(b, delta));
                return c == 0 ? a.CompareTo(b) : c;
            });
            return values.ToArray();
        }

        private static double DirectionCost(int direction, Vector2 delta)
        {
            double along = direction < 2 ? delta.x : delta.y, perpendicular = direction < 2 ? delta.y : delta.x;
            if ((direction & 1) != 0) along = -along;
            return Math.Abs(perpendicular) + (along < 0 ? Math.Abs(along) * 4 + 1 : 0);
        }
        private bool ReserveState()
        {
            if (CoordinateIterations >= _maximumCoordinateIterations)
            { BudgetExhausted = true; Reject("TotalCoordinateBudgetExhausted"); return false; }
            if (SearchStates >= _maximumStates) { BudgetExhausted = true; return false; }
            SearchStates++; return true;
        }
        private void Reject(string reason)
        { _failures.TryGetValue(reason, out int count); _failures[reason] = count + 1; }
    }
}

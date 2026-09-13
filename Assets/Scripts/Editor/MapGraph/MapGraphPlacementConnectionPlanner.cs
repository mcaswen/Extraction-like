using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>冻结位置连线和有限格点建议。每次边验收可让出执行，不执行 NavMesh 查询。</summary>
    public sealed class MapGraphPlacementConnectionPlanner
    {
        private sealed class CandidateLayout
        {
            public MapGraphLayoutDraft Layout;
            public List<MapGraphValidationIssue> Issues;
            public int Errors => Issues.Count(i => i.IsError);
            public double Displacement;
        }
        private readonly MapGraphLayoutDraft _original;
        private readonly MapGraphConnectionCandidates _candidates;
        private readonly float _spacing;
        private readonly int _cells, _budget, _extraEdges;
        private readonly List<MapGraphValidationIssue> _diagnostics = new List<MapGraphValidationIssue>();
        private readonly IEnumerator<int> _work;
        public IReadOnlyList<MapGraphValidationIssue> Diagnostics => _diagnostics;
        public MapGraphLayoutDraft Result { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public int SearchStates { get; private set; }
        public int WorkItems { get; private set; }

        public MapGraphPlacementConnectionPlanner(MapGraphLayoutDraft original, IEnumerable<MapGraphScannedConnection> measurements,
            IEnumerable<string> profiles, MapGraphGenerationSettings settings, float spacing, int adjustmentCells = 0)
        {
            _original = original ?? throw new ArgumentNullException(nameof(original));
            _spacing = MapGraphGridPlacement.ValidSpacing(spacing); _cells = Mathf.Clamp(adjustmentCells, 0, 4);
            // 交互建议使用有限宽度和最多 512 个布局，原配置仍可进一步降低预算。
            _budget = Math.Min(512, settings.MaximumSearchStates);
            _extraEdges = Mathf.CeilToInt(original.Nodes.Count * settings.ExtraConnectionRatio);
            _candidates = new MapGraphConnectionCandidates(original, measurements, profiles);
            _work = Search();
        }
        public int Advance(int maximumWorkItems = 1)
        {
            int count = 0;
            while (!IsComplete && !IsCancelled && count < maximumWorkItems)
            {
                if (!_work.MoveNext()) { IsComplete = true; break; }
                count++; WorkItems++;
            }
            return count;
        }
        public void Cancel() { IsCancelled = true; Result = null; _work.Dispose(); }

        private IEnumerator<int> Search()
        {
            if (!_candidates.Validation.IsValid) { _diagnostics.AddRange(_candidates.Validation.Issues); yield break; }
            CandidateLayout first = null;
            foreach (int step in Evaluate(_original, value => first = value)) yield return step;
            if (Accept(first)) yield break;
            if (_cells == 0)
            {
                _diagnostics.AddRange(first.Issues);
                _diagnostics.Add(new MapGraphValidationIssue("PlacementConnectionsIncomplete", "graph", detail: "保持位置无法连通上列群，请对齐行列或使用微调建议。"));
                yield break;
            }
            var frontier = new List<CandidateLayout> { first };
            var visited = new HashSet<string>(StringComparer.Ordinal) { PositionKey(_original) };
            CandidateLayout best = first;
            while (frontier.Count > 0 && SearchStates < _budget)
            {
                frontier.Sort(Compare); var current = frontier[0]; frontier.RemoveAt(0);
                foreach (var node in _original.Nodes.OrderBy(n => n.NodeId, StringComparer.Ordinal))
                {
                    _original.Graph.TryGetZone(node.ZoneId, out var zone);
                    if (node.PositionLocked || zone.LayoutLocked) continue;
                    Vector2 initial = _original.Graph.GetNodePosition(node.NodeId);
                    bool rowLocked = _original.Constraints.Alignments.Any(a => a.Id == node.RowId && a.Locked);
                    bool columnLocked = _original.Constraints.Alignments.Any(a => a.Id == node.ColumnId && a.Locked);
                    foreach (var point in AdjustmentPoints(initial, rowLocked, columnLocked))
                            {
                                if (Mathf.Abs(point.x - initial.x) > _spacing * _cells + MapGraphGeometry.Epsilon ||
                                    Mathf.Abs(point.y - initial.y) > _spacing * _cells + MapGraphGeometry.Epsilon ||
                                    MapGraphGeometry.Near(point, current.Layout.Graph.GetNodePosition(node.NodeId))) continue;
                                var basis = new MapGraphLayoutDraft(current.Layout.Zones, current.Layout.Nodes, _original.Edges, current.Layout.Constraints, _original.StartNodeId);
                                var next = MapGraphGridPlacement.WithPositions(basis, new Dictionary<string, Vector2> { [node.NodeId] = point });
                                if (!visited.Add(PositionKey(next))) continue;
                                CandidateLayout evaluated = null;
                                foreach (int step in Evaluate(next, value => evaluated = value)) yield return step;
                                if (Accept(evaluated)) yield break;
                                if (Compare(evaluated, best) < 0) best = evaluated;
                                frontier.Add(evaluated); frontier.Sort(Compare);
                                if (frontier.Count > 8) frontier.RemoveRange(8, frontier.Count - 8);
                                if (SearchStates >= _budget) goto Finished;
                            }
                }
            }
            Finished:
            _diagnostics.AddRange(best.Issues);
            _diagnostics.Add(new MapGraphValidationIssue("PlacementAdjustmentNotFound", "graph",
                detail: $"已检查 {SearchStates} 个布局，位移上限 {_cells} 格，预算 {_budget}；预算内未找到合法建议。请调整摆放或约束。"));
        }

        private IEnumerable<Vector2> AdjustmentPoints(Vector2 initial, bool rowLocked, bool columnLocked)
        {
            var snapped = MapGraphGridPlacement.Snap(initial, _spacing); var points = new HashSet<Vector2>();
            for (int x = -_cells; x <= _cells; x++) for (int y = -_cells; y <= _cells; y++)
            {
                var point = snapped + new Vector2(x, y) * _spacing;
                if (columnLocked) point.x = initial.x;
                if (rowLocked) point.y = initial.y;
                points.Add(point);
            }
            return points.OrderBy(p => (p - initial).sqrMagnitude).ThenBy(p => p.x).ThenBy(p => p.y);
        }

        private IEnumerable<int> Evaluate(MapGraphLayoutDraft placement, Action<CandidateLayout> complete)
        {
            SearchStates++;
            var normalized = MapGraphGridPlacement.WithPositions(placement, null);
            var edges = _original.Edges.Where(MapGraphLayoutIntentValidation.IsPinnedEdge).ToList();
            MapGraphLayoutDraft Draft() => new MapGraphLayoutDraft(normalized.Zones, normalized.Nodes, edges, normalized.Constraints, normalized.StartNodeId);
            var draft = Draft(); var geometry = MapGraphValidation.Validate(draft, _original);
            yield return 1;
            if (geometry.IsValid)
            {
                var parent = draft.Nodes.ToDictionary(n => n.NodeId, n => n.NodeId);
                string Find(string id) { while (parent[id] != id) id = parent[id]; return id; }
                foreach (var edge in edges) parent[Find(edge.ToNodeId)] = Find(edge.FromNodeId);
                int extras = 0;
                foreach (var candidate in _candidates.Ordered.OrderBy(c => c.NavigationLength).ThenBy(c => c.Edge.EdgeId, StringComparer.Ordinal))
                {
                    var saved = candidate.Edge;
                    if (edges.Any(e => MapGraphGeometry.PairKey(e.FromNodeId, e.ToNodeId) == MapGraphGeometry.PairKey(saved.FromNodeId, saved.ToNodeId))) continue;
                    var a = draft.Graph.GetNodePosition(saved.FromNodeId); var b = draft.Graph.GetNodePosition(saved.ToNodeId);
                    var axis = MapGraphGeometry.Near(a.y, b.y) ? MapGraphAxis.Horizontal : MapGraphGeometry.Near(a.x, b.x) ? MapGraphAxis.Vertical : MapGraphAxis.Unspecified;
                    if (axis == MapGraphAxis.Unspecified) continue;
                    bool joins = Find(saved.FromNodeId) != Find(saved.ToNodeId);
                    if (!joins && extras >= _extraEdges) continue;
                    var edge = new MapGraphEdgeDefinition(saved.EdgeId, saved.FromNodeId, saved.ToNodeId, (float)candidate.NavigationLength, axis, MapGraphEdgeOrigin.Generated);
                    edges.Add(edge); var trial = Draft();
                    if (MapGraphValidation.Validate(trial).IsValid)
                    { draft = trial; if (joins) parent[Find(saved.ToNodeId)] = Find(saved.FromNodeId); else extras++; }
                    else edges.RemoveAt(edges.Count - 1);
                    yield return 1;
                }
            }
            var issues = MapGraphValidation.Validate(draft, _original).Issues.ToList();
            issues.AddRange(_candidates.Validate(draft).Issues);
            double displacement = draft.Nodes.Sum(n => (double)(draft.Graph.GetNodePosition(n.NodeId) - _original.Graph.GetNodePosition(n.NodeId)).sqrMagnitude);
            complete(new CandidateLayout { Layout = draft, Issues = issues, Displacement = displacement });
            yield return 1;
        }
        private bool Accept(CandidateLayout candidate)
        {
            if (candidate.Errors != 0) return false;
            Result = candidate.Layout; _diagnostics.AddRange(candidate.Issues); return true;
        }
        private static int Compare(CandidateLayout a, CandidateLayout b)
        {
            int comparison = a.Errors.CompareTo(b.Errors);
            if (comparison == 0) comparison = a.Displacement.CompareTo(b.Displacement);
            return comparison != 0 ? comparison : string.CompareOrdinal(PositionKey(a.Layout), PositionKey(b.Layout));
        }
        private static string PositionKey(MapGraphLayoutDraft draft)
            => string.Join("|", draft.Graph.OrderedNodeIds.Select(id => id + ":" + draft.Graph.GetNodePosition(id).x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + draft.Graph.GetNodePosition(id).y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
    }
}

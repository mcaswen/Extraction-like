using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    public sealed class MapGraphShortcutAttempt
    {
        public string EdgeId { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public double DetourRatio { get; }
        public string Outcome { get; }
        public bool Relayout { get; }
        public IReadOnlyList<string> Details { get; }
        internal MapGraphShortcutAttempt(MapGraphEdgeDefinition edge, double ratio, string outcome, bool relayout, IEnumerable<string> details = null)
        {
            EdgeId = edge?.EdgeId ?? "graph"; FromNodeId = edge?.FromNodeId ?? ""; ToNodeId = edge?.ToNodeId ?? "";
            DetourRatio = ratio; Outcome = outcome; Relayout = relayout; Details = (details ?? Array.Empty<string>()).ToList().AsReadOnly();
        }
    }

    /// <summary>在有效骨架上增加有实测绕行收益的可选边，失败或预算耗尽保留已验收结果。</summary>
    internal sealed class MapGraphShortcutGenerator
    {
        private const double MinimumDetourRatio = 1.5;
        private readonly MapGraphLayoutDraft _reference;
        private readonly MapGraphConnectionCandidates _candidates;
        private readonly MapGraphGenerationSettings _settings;
        private readonly int _maximumSearchStates, _maximumCoordinateIterations, _maximumAddedEdges;
        private readonly double _maximumScore;
        private readonly List<MapGraphShortcutAttempt> _attempts = new List<MapGraphShortcutAttempt>();
        private readonly Dictionary<string, MapGraphPathfindingService> _paths = new Dictionary<string, MapGraphPathfindingService>(StringComparer.Ordinal);
        private MapGraphLayoutDraft _current;
        private IEnumerator<int> _search;
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public bool BudgetExhausted { get; private set; }
        public int SearchStates { get; private set; }
        public int CoordinateIterations { get; private set; }
        public int AddedConnections { get; private set; }
        public int RelayoutAttempts { get; private set; }
        public double ElapsedMilliseconds { get; private set; }
        public MapGraphLayoutDraft Result => IsComplete && !IsCancelled ? _current : null;
        public IReadOnlyList<MapGraphShortcutAttempt> Attempts { get; }

        internal MapGraphShortcutGenerator(MapGraphLayoutDraft reference, MapGraphLayoutDraft current,
            MapGraphConnectionCandidates candidates, MapGraphGenerationSettings settings, int maximumSearchStates)
        {
            _reference = reference ?? throw new ArgumentNullException(nameof(reference));
            _current = current ?? throw new ArgumentNullException(nameof(current));
            _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _maximumSearchStates = Math.Max(0, maximumSearchStates);
            _maximumCoordinateIterations = Math.Max(settings.MaximumLayoutIterations, current.Nodes.Count * 32);
            _maximumAddedEdges = Mathf.CeilToInt(current.Edges.Count * settings.ExtraConnectionRatio);
            _maximumScore = MapGraphLayoutScore.Evaluate(current, reference, settings).Total * 1.15 + 0.1;
            Attempts = _attempts.AsReadOnly(); _search = Generate().GetEnumerator();
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
            IsCancelled = true; _search?.Dispose(); _search = null; _current = null;
        }

        private IEnumerable<int> Generate()
        {
            var initial = MapGraphValidation.Validate(_current, _reference);
            if (!_candidates.Validation.IsValid || !initial.IsValid || !_candidates.Validate(_current).IsValid)
            {
                _attempts.Add(new MapGraphShortcutAttempt(null, 0, "INVALID_BASE", false, initial.Issues.Select(i => i.ToString())));
                _current = null; yield break;
            }
            if (_maximumAddedEdges == 0) yield break;
            RefreshPaths();
            var ranked = new List<(MapGraphEdgeDefinition edge, double ratio, bool aligned)>();
            foreach (var candidate in _candidates.Ordered)
            {
                var edge = candidate.Edge;
                if (_current.Graph.TryGetEdgeBetween(edge.FromNodeId, edge.ToNodeId, out _)) continue;
                double ratio = MeasureDetour(edge);
                if (ratio >= MinimumDetourRatio)
                {
                    var a = _current.Graph.GetNodePosition(edge.FromNodeId); var b = _current.Graph.GetNodePosition(edge.ToNodeId);
                    ranked.Add((edge, ratio, MapGraphGeometry.Near(a.x, b.x) || MapGraphGeometry.Near(a.y, b.y)));
                }
                yield return 1;
            }
            foreach (var proposal in ranked.OrderByDescending(p => p.aligned).ThenByDescending(p => p.ratio).ThenBy(p => p.edge.EdgeId, StringComparer.Ordinal))
            {
                if (AddedConnections >= _maximumAddedEdges) yield break;
                var edge = proposal.edge; double ratio = MeasureDetour(edge);
                if (ratio < MinimumDetourRatio)
                { Record(edge, ratio, "BENEFIT_DISAPPEARED", false); yield return 1; continue; }
                var direct = MapGraphLayoutGenerator.CreateAlignedConnectionDraft(_current, edge);
                if (Acceptable(direct, out var details))
                { Accept(direct, edge, ratio, false); yield return 1; continue; }
                var directDetails = details;
                if (_current.Graph.GetConnectedEdges(edge.FromNodeId).Count >= 4 || _current.Graph.GetConnectedEdges(edge.ToNodeId).Count >= 4)
                { Record(edge, ratio, "PORT_LIMIT", false, details); yield return 1; continue; }
                int states = _maximumSearchStates - SearchStates, iterations = _maximumCoordinateIterations - CoordinateIterations;
                if (RelayoutAttempts >= _settings.CandidateNeighbors || states == 0 || iterations == 0)
                { BudgetExhausted = true; Record(edge, ratio, "RELAYOUT_BUDGET", false, details); yield return 1; continue; }
                RelayoutAttempts++;
                var solver = new MapGraphOrthogonalLayoutSolver(_current, _current.Edges.Concat(new[] { edge }), _settings,
                    Math.Max(1, states / Math.Max(1, _settings.CandidateNeighbors - RelayoutAttempts + 1)), maximumSolutions: 4,
                    maximumCoordinateIterations: Math.Max(1, iterations / Math.Max(1, _settings.CandidateNeighbors - RelayoutAttempts + 1)));
                yield return 1;
                try
                {
                    while (!solver.IsComplete)
                    {
                        int previousStates = solver.SearchStates, previousIterations = solver.CoordinateIterations;
                        solver.Advance(1); SearchStates += solver.SearchStates - previousStates;
                        CoordinateIterations += solver.CoordinateIterations - previousIterations; yield return 1;
                    }
                    BudgetExhausted |= solver.BudgetExhausted;
                    if (Acceptable(solver.Result, out details)) Accept(solver.Result, edge, ratio, true);
                    else Record(edge, ratio, "RELAYOUT_REJECTED", true, directDetails.Concat(details).Concat(solver.FailureCounts.Keys).Distinct());
                }
                finally { if (!solver.IsComplete) solver.Cancel(); }
                yield return 1;
            }
        }

        private bool Acceptable(MapGraphLayoutDraft draft, out IReadOnlyList<string> details)
        {
            var errors = new List<string>(); details = errors;
            if (draft == null) { errors.Add("NoAlignedLayout"); return false; }
            errors.AddRange(MapGraphValidation.Validate(draft, _reference).Issues.Where(i => i.IsError).Select(i => i.ToString()));
            errors.AddRange(_candidates.Validate(draft).Issues.Where(i => i.IsError).Select(i => i.ToString()));
            var score = MapGraphLayoutScore.Evaluate(draft, _reference, _settings);
            if (!score.IsFinite || score.Total > _maximumScore) errors.Add("LayoutDistortionLimit");
            return errors.Count == 0;
        }

        private void RefreshPaths()
        {
            _paths.Clear();
            foreach (string profile in _candidates.ProfileIds)
                _paths.Add(profile, new MapGraphPathfindingService(_current.Graph, _candidates.CreateCostSnapshot(_current, profile)));
        }

        private double MeasureDetour(MapGraphEdgeDefinition edge)
        {
            double maximum = 0;
            foreach (var profile in _paths)
            {
                maximum = Math.Max(maximum, Direction(edge.FromNodeId, edge.ToNodeId));
                maximum = Math.Max(maximum, Direction(edge.ToNodeId, edge.FromNodeId));
                double Direction(string from, string to)
                {
                    if (!_candidates.TryGetLength(profile.Key, from, to, out float length)) return 0;
                    var path = profile.Value.ResolveFromNode(from, to);
                    return path.IsValid ? Math.Min(1000000, path.TotalEstimatedLengthUnits / Math.Max(0.000001, length)) : 0;
                }
            }
            return maximum;
        }

        private void Accept(MapGraphLayoutDraft draft, MapGraphEdgeDefinition edge, double ratio, bool relayout)
        { _current = draft; AddedConnections++; RefreshPaths(); Record(edge, ratio, "ADDED", relayout); }
        private void Record(MapGraphEdgeDefinition edge, double ratio, string outcome, bool relayout, IEnumerable<string> details = null)
            => _attempts.Add(new MapGraphShortcutAttempt(edge, ratio, outcome, relayout, details));
    }
}

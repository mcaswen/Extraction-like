using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    public sealed class MapGraphConnectionAttempt
    {
        public string Strategy { get; }
        public IReadOnlyList<string> EdgeIds { get; }
        public string Outcome { get; }
        public int SearchStates { get; }
        public int FeasibleLayouts { get; }
        public int CoordinateIterations { get; }
        public bool BudgetExhausted { get; }
        public double Score { get; }
        public IReadOnlyList<string> Failures { get; }
        internal MapGraphConnectionAttempt(string strategy, IEnumerable<MapGraphEdgeDefinition> edges, string outcome,
            MapGraphOrthogonalLayoutSolver solver = null, IEnumerable<string> failures = null)
        {
            Strategy = strategy; EdgeIds = (edges ?? Array.Empty<MapGraphEdgeDefinition>()).Select(e => e.EdgeId).ToList().AsReadOnly();
            Outcome = outcome; SearchStates = solver?.SearchStates ?? 0; FeasibleLayouts = solver?.FeasibleLayouts ?? 0;
            CoordinateIterations = solver?.CoordinateIterations ?? 0;
            BudgetExhausted = solver?.BudgetExhausted ?? false; Score = solver?.BestScore ?? double.PositiveInfinity;
            Failures = (failures ?? Array.Empty<string>()).ToList().AsReadOnly();
        }
    }

    /// <summary>联合选边和横竖求解的有界编排；输入是测量快照，结果只在完成后发布。</summary>
    public sealed class MapGraphConnectionGenerator
    {
        private readonly MapGraphLayoutDraft _reference;
        private readonly MapGraphGenerationSettings _settings;
        private readonly MapGraphScannedConnection[] _measurements;
        private readonly string[] _profileIds;
        private readonly List<MapGraphConnectionAttempt> _attempts = new List<MapGraphConnectionAttempt>();
        private readonly List<MapGraphValidationIssue> _diagnostics = new List<MapGraphValidationIssue>();
        private readonly int _maximumStates;
        private MapGraphConnectionCandidates _candidates;
        private MapGraphShortcutGenerator _shortcuts;
        private IEnumerator<int> _search;
        private MapGraphLayoutDraft _best;
        private double _score = double.PositiveInfinity;
        public MapGraphLayoutDraft Result => IsComplete && !IsCancelled ? _best : null;
        public string SelectedStrategy { get; private set; }
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public int SearchStates { get; private set; }
        public int CoordinateIterations { get; private set; }
        public int WorkItems { get; private set; }
        public double ElapsedMilliseconds { get; private set; }
        public double BestScore => _score;
        public int AddedConnections => _shortcuts?.AddedConnections ?? 0;
        public bool ShortcutBudgetExhausted => _shortcuts?.BudgetExhausted ?? false;
        public IReadOnlyList<MapGraphShortcutAttempt> ShortcutAttempts => _shortcuts?.Attempts ?? Array.Empty<MapGraphShortcutAttempt>();
        public IReadOnlyList<MapGraphConnectionAttempt> Attempts { get; }
        public IReadOnlyList<MapGraphValidationIssue> Diagnostics { get; }

        public MapGraphConnectionGenerator(MapGraphLayoutDraft reference, IEnumerable<MapGraphScannedConnection> measurements,
            IEnumerable<string> profileIds, MapGraphGenerationSettings settings)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            _reference = new MapGraphLayoutDraft(reference.Zones.OrderBy(z => z.ZoneId, StringComparer.Ordinal),
                reference.Nodes.OrderBy(n => n.NodeId, StringComparer.Ordinal), reference.Edges.OrderBy(e => e.EdgeId, StringComparer.Ordinal),
                reference.Constraints, reference.StartNodeId);
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _measurements = (measurements ?? throw new ArgumentNullException(nameof(measurements))).ToArray();
            _profileIds = (profileIds ?? throw new ArgumentNullException(nameof(profileIds))).ToArray();
            _maximumStates = settings.MaximumSearchStates;
            Attempts = _attempts.AsReadOnly(); Diagnostics = _diagnostics.AsReadOnly();
            _search = Generate().GetEnumerator();
        }

        public int Advance(int maximumWorkItems)
        {
            if (IsComplete || IsCancelled || maximumWorkItems <= 0) return 0;
            long started = Stopwatch.GetTimestamp(); int work = 0;
            while (work < maximumWorkItems)
            {
                if (!_search.MoveNext()) { IsComplete = true; _search.Dispose(); _search = null; break; }
                work++; WorkItems++;
            }
            ElapsedMilliseconds += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
            return work;
        }

        public void Cancel()
        {
            if (IsComplete || IsCancelled) return;
            IsCancelled = true; _search?.Dispose(); _search = null; _best = null; _score = double.PositiveInfinity; SelectedStrategy = null;
        }

        private IEnumerable<int> Generate()
        {
            _candidates = new MapGraphConnectionCandidates(_reference, _measurements, _profileIds);
            _diagnostics.AddRange(_candidates.Validation.Issues); yield return 1;
            if (!_candidates.Validation.IsValid) yield break;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int strategy = 0; strategy < MapGraphConnectionSeedBuilder.StrategyCount; strategy++)
            {
                string name = MapGraphConnectionSeedBuilder.StrategyName(strategy);
                var edges = MapGraphConnectionSeedBuilder.Build(_reference, _candidates, strategy, out string failure);
                if (edges == null)
                { _attempts.Add(new MapGraphConnectionAttempt(name, null, "REJECTED", failures: new[] { failure })); yield return 1; continue; }
                var topology = new MapGraphLayoutDraft(_reference.Zones, _reference.Nodes, edges, _reference.Constraints, _reference.StartNodeId);
                var navigation = _candidates.Validate(topology);
                if (!navigation.IsValid)
                {
                    _attempts.Add(new MapGraphConnectionAttempt(name, edges, "CONNECTIVITY_REJECTED", failures: navigation.Issues.Where(i => i.IsError).Select(i => i.ToString())));
                    yield return 1; continue;
                }
                string key = string.Concat(edges.Select(e => e.EdgeId.Length + ":" + e.EdgeId));
                if (!visited.Add(key)) { _attempts.Add(new MapGraphConnectionAttempt(name, edges, "DUPLICATE")); yield return 1; continue; }
                int remaining = _maximumStates - SearchStates;
                if (remaining == 0) { _attempts.Add(new MapGraphConnectionAttempt(name, edges, "BUDGET_EXHAUSTED")); yield return 1; continue; }
                int budget = Math.Max(1, remaining / (MapGraphConnectionSeedBuilder.StrategyCount - strategy));
                int coordinateBudget = Math.Max(_settings.MaximumLayoutIterations, _reference.Nodes.Count * 32);
                var solver = new MapGraphOrthogonalLayoutSolver(_reference, edges, _settings, budget,
                    maximumCoordinateIterations: coordinateBudget);
                yield return 1;
                try
                {
                    while (!solver.IsComplete)
                    {
                        int before = solver.SearchStates, iterations = solver.CoordinateIterations;
                        solver.Advance(1); SearchStates += solver.SearchStates - before;
                        CoordinateIterations += solver.CoordinateIterations - iterations; yield return 1;
                    }
                    var candidate = solver.Result;
                    var failures = solver.FailureCounts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value).ToList();
                    bool valid = candidate != null;
                    if (valid)
                    {
                        var geometry = MapGraphValidation.Validate(candidate, _reference);
                        var measured = _candidates.Validate(candidate);
                        valid = geometry.IsValid && measured.IsValid;
                        failures.AddRange(geometry.Issues.Concat(measured.Issues).Where(i => i.IsError).Select(i => i.ToString()));
                    }
                    _attempts.Add(new MapGraphConnectionAttempt(name, edges, valid ? "FEASIBLE" : "UNRESOLVED", solver, failures));
                    if (valid && solver.BestScore < _score)
                    { _best = candidate; _score = solver.BestScore; SelectedStrategy = name; }
                }
                finally { if (!solver.IsComplete) solver.Cancel(); }
                yield return 1;
            }
            if (_best == null) _diagnostics.Add(new MapGraphValidationIssue("NoFeasibleConnectionLayout", "graph",
                detail: "有界候选未找到可发布布局，请检查各候选冲突或调整预算；不表示数学无解。"));
            else
            {
                _shortcuts = new MapGraphShortcutGenerator(_reference, _best, _candidates, _settings, _maximumStates - SearchStates);
                try
                {
                    while (!_shortcuts.IsComplete)
                    {
                        int states = _shortcuts.SearchStates, iterations = _shortcuts.CoordinateIterations;
                        _shortcuts.Advance(1); SearchStates += _shortcuts.SearchStates - states;
                        CoordinateIterations += _shortcuts.CoordinateIterations - iterations; yield return 1;
                    }
                    if (_shortcuts.Result == null)
                    { _best = null; _score = double.PositiveInfinity; _diagnostics.Add(new MapGraphValidationIssue("ShortcutBaseValidationFailed", "graph")); }
                    else
                    {
                        _best = _shortcuts.Result; _score = MapGraphLayoutScore.Evaluate(_best, _reference, _settings).Total;
                        if (_shortcuts.BudgetExhausted) _diagnostics.Add(new MapGraphValidationIssue("OptionalShortcutBudgetExhausted", "graph",
                            detail: "可选补边的预算耗尽，保留已通过验收的图。", error: false));
                    }
                }
                finally { if (!_shortcuts.IsComplete) _shortcuts.Cancel(); }
            }
        }
    }
}

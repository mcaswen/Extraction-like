using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>一次固定编辑意图的有界几何预览；没有任何持久化权限。</summary>
    public sealed class MapGraphEditOperation
    {
        private readonly MapGraphLayoutDraft _intent;
        private readonly MapGraphOrthogonalLayoutSolver _solver;
        private readonly List<string> _failures = new List<string>();
        public MapGraphLayoutDraft Original { get; }
        public MapGraphLayoutDraft Result { get; private set; }
        public long InputRevision { get; }
        public string Label { get; }
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public IReadOnlyList<string> Failures { get; }
        public MapGraphEditOperation(MapGraphLayoutDraft original, MapGraphLayoutDraft intent, MapGraphGenerationSettings settings, long inputRevision, string label)
        {
            Original = original ?? throw new ArgumentNullException(nameof(original)); _intent = intent ?? throw new ArgumentNullException(nameof(intent));
            InputRevision = inputRevision; Label = label; Failures = _failures.AsReadOnly();
            var identity = MapGraphLayoutIntentValidation.Validate(intent, original, MapGraphIntentPreservation.None);
            if (!identity.IsValid || original.Nodes.Count != intent.Nodes.Count || original.Zones.Count != intent.Zones.Count ||
                original.Nodes.Any(n => !intent.Graph.TryGetNode(n.NodeId, out _)) || original.Zones.Any(z => !intent.Graph.TryGetZone(z.ZoneId, out _)))
            { _failures.Add("EditCannotChangeSceneIdentity"); IsComplete = true; return; }
            if (MapGraphValidation.Validate(intent).IsValid) { Result = intent; IsComplete = true; return; }
            _solver = new MapGraphOrthogonalLayoutSolver(intent, intent.Edges, settings, maximumSolutions: 4,
                maximumCoordinateIterations: Math.Max(settings.MaximumLayoutIterations, intent.Nodes.Count * 32));
        }
        public int Advance(int maximumWorkItems = 64, double maximumMilliseconds = 6)
        {
            if (IsComplete || IsCancelled || maximumWorkItems <= 0 || maximumMilliseconds <= 0) return 0;
            var timer = Stopwatch.StartNew(); int work = 0;
            do { _solver.Advance(1); work++; } while (!_solver.IsComplete && work < maximumWorkItems && timer.Elapsed.TotalMilliseconds < maximumMilliseconds);
            if (_solver.IsComplete)
            {
                IsComplete = true;
                if (_solver.Result != null && MapGraphValidation.Validate(_solver.Result, _intent, MapGraphIntentPreservation.AllIntent | MapGraphIntentPreservation.Topology).IsValid) Result = _solver.Result;
                else _failures.AddRange(_solver.FailureCounts.Select(p => p.Key + "=" + p.Value));
                if (Result == null && _failures.Count == 0) _failures.Add("NoValidEditLayout");
            }
            return work;
        }
        public void Cancel() { IsCancelled = true; _solver?.Cancel(); Result = null; }
    }
}

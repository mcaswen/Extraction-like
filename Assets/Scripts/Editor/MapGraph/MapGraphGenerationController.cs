using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>一次 Editor 生成任务；只编排临时计算，不订阅 Update，不写资产或场景。</summary>
    public sealed class MapGraphGenerationController
    {
        private readonly Func<MapGraphSceneSnapshot> _capture;
        private readonly MapGraphLayoutDraft _previous;
        private readonly MapGraphGenerationSettings _settings;
        private readonly List<MapGraphValidationIssue> _diagnostics = new List<MapGraphValidationIssue>();
        private MapGraphSceneSnapshot _scene;
        private MapGraphLayoutDraft _reference;
        private MapGraphSceneNavigationScan _scan;
        private MapGraphConnectionGenerator _connections;
        private MapGraphOrthogonalLayoutSolver _layout;
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        public long InputRevision { get; }
        public MapGraphGenerationMode Mode { get; }
        public MapGraphGenerationStage Stage { get; private set; } = MapGraphGenerationStage.Collecting;
        public bool IsRunning => Stage <= MapGraphGenerationStage.Validating;
        public MapGraphGenerationResult Result { get; private set; }
        public IReadOnlyList<MapGraphValidationIssue> Diagnostics { get; }
        public int WorkItems { get; private set; }
        public long NavigationQueries => _scan?.TotalQueryCount ?? 0;
        public int NavigationWorkItems => _scan?.WorkItemCount ?? 0;
        public int SearchStates => (_connections?.SearchStates ?? 0) + (_layout?.SearchStates ?? 0);
        public int CoordinateIterations => (_connections?.CoordinateIterations ?? 0) + (_layout?.CoordinateIterations ?? 0);
        public double ElapsedMilliseconds { get; private set; }
        public double MaximumAdvanceMilliseconds { get; private set; }
        public double MaximumStepMilliseconds { get; private set; }
        public double CaptureMilliseconds { get; private set; }

        public MapGraphGenerationController(Scene scene, MapGraphGenerationSettings settings, MapGraphLayoutDraft previous = null,
            MapGraphGenerationMode mode = MapGraphGenerationMode.ConnectionsAndLayout, long inputRevision = 0)
            : this(() => MapGraphSceneCollector.Capture(scene), settings, previous, mode, inputRevision) { }

        internal MapGraphGenerationController(Func<MapGraphSceneSnapshot> capture, MapGraphGenerationSettings settings,
            MapGraphLayoutDraft previous = null, MapGraphGenerationMode mode = MapGraphGenerationMode.ConnectionsAndLayout, long inputRevision = 0)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            // 设置由 Unity 序列化字段定义；任务冻结副本，窗口继续编辑原设置不会改变本次计算。
            _settings = JsonUtility.FromJson<MapGraphGenerationSettings>(JsonUtility.ToJson(settings));
            _previous = previous; Mode = mode; InputRevision = inputRevision; Diagnostics = _diagnostics.AsReadOnly();
        }

        /// <summary>时间上限在原子工作之后检查；采集/独立校验不能被中途抢占，耗时另行公开。</summary>
        public int Advance(int maximumWorkItems = 64, double maximumMilliseconds = 6)
        {
            if (!IsRunning || maximumWorkItems <= 0 || maximumMilliseconds <= 0 || double.IsNaN(maximumMilliseconds) || double.IsInfinity(maximumMilliseconds)) return 0;
            long started = Stopwatch.GetTimestamp(); int work = 0, navigationWork = 0;
            while (IsRunning && work < maximumWorkItems)
            {
                if (Stage == MapGraphGenerationStage.ScanningNavigation && navigationWork >= _settings.QueriesPerEditorTick) break;
                int navigationBefore = NavigationWorkItems; long stepStarted = Stopwatch.GetTimestamp();
                try { Step(); }
                catch (Exception exception) { Fail("GenerationException", exception.GetType().Name + ": " + exception.Message); }
                MaximumStepMilliseconds = Math.Max(MaximumStepMilliseconds, MillisecondsSince(stepStarted));
                navigationWork += NavigationWorkItems - navigationBefore; work++; WorkItems++;
                if (MillisecondsSince(started) >= maximumMilliseconds) break;
            }
            double elapsed = MillisecondsSince(started);
            ElapsedMilliseconds += elapsed; MaximumAdvanceMilliseconds = Math.Max(MaximumAdvanceMilliseconds, elapsed);
            return work;
        }

        public void Cancel()
        {
            if (Stage == MapGraphGenerationStage.Cancelled) return;
            StopTasks(); Result = null; Stage = MapGraphGenerationStage.Cancelled;
        }

        /// <summary>应用/保存入口必须重新取得有效结果；预览 Result 不能作为仍然有效的证明。</summary>
        public bool TryGetCurrentResult(out MapGraphGenerationResult result)
        {
            result = null;
            if (Stage != MapGraphGenerationStage.Ready || Result == null) return false;
            try { if (!CheckCurrentInputs()) return false; }
            catch (Exception exception) { Fail("GenerationInputUnavailable", exception.Message, MapGraphGenerationStage.Stale); return false; }
            result = Result; return true;
        }

        private void Step()
        {
            switch (Stage)
            {
                case MapGraphGenerationStage.Collecting:
                    if (!Enum.IsDefined(typeof(MapGraphGenerationMode), Mode)) { Fail("UnknownGenerationMode"); return; }
                    if (Mode != MapGraphGenerationMode.ConnectionsAndLayout && _previous == null) { Fail("LayoutOnlyNeedsExistingGraph"); return; }
                    _scene = Capture();
                    if (_scene == null || !_scene.IsValid) { Fail("InvalidSceneInput", _scene == null ? "采集没有返回输入。" : string.Join("\n", _scene.Diagnostics)); return; }
                    if (!ValidateSynchronization()) return;
                    _reference = Mode == MapGraphGenerationMode.ValidateOnly ? _previous : MapGraphLayoutGenerator.CreateReference(_scene, _settings, _previous);
                    _scan = new MapGraphSceneNavigationScan(_scene); Stage = MapGraphGenerationStage.ScanningNavigation; break;
                case MapGraphGenerationStage.ScanningNavigation:
                    _scan.Advance(1);
                    if (!_scan.IsComplete) break;
                    foreach (var anchor in _scan.Anchors.Where(a => !a.IsValid))
                        _diagnostics.Add(new MapGraphValidationIssue("MissingNavigationAnchor", anchor.NodeId, anchor.ProfileId, anchor.Failure));
                    if (_diagnostics.Any(i => i.IsError)) { Fail("NavigationScanFailed"); return; }
                    if (Mode == MapGraphGenerationMode.ValidateOnly) { Stage = MapGraphGenerationStage.Validating; break; }
                    if (Mode == MapGraphGenerationMode.ConnectionsAndLayout)
                        _connections = new MapGraphConnectionGenerator(_reference, _scan.Connections, _scene.Profiles.Select(p => p.Data.ProfileId), _settings);
                    else
                        _layout = new MapGraphOrthogonalLayoutSolver(_reference, _previous.Edges, _settings,
                            maximumCoordinateIterations: Math.Max(_settings.MaximumLayoutIterations, _reference.Nodes.Count * 32));
                    Stage = MapGraphGenerationStage.Generating; break;
                case MapGraphGenerationStage.Generating:
                    if (_connections != null) { _connections.Advance(1); if (_connections.IsComplete) Stage = MapGraphGenerationStage.Validating; }
                    else { _layout.Advance(1); if (_layout.IsComplete) Stage = MapGraphGenerationStage.Validating; }
                    break;
                case MapGraphGenerationStage.Validating:
                    Publish(); break;
            }
        }

        private bool ValidateSynchronization()
        {
            if (_previous == null) return true;
            if (!_previous.Graph.IsValid) { Fail("InvalidExistingGraph", string.Join("\n", _previous.Graph.ValidationErrors)); return false; }
            foreach (var zone in _previous.Zones)
            {
                var current = _scene.Zones.FirstOrDefault(z => z.Id == zone.ZoneId);
                if (current == null || current.SourceObjectId != zone.SourceObjectId)
                    _diagnostics.Add(new MapGraphValidationIssue("ZoneSynchronizationRequired", zone.ZoneId, detail:
                        current == null ? $"区域“{zone.DisplayName}”在当前场景中已不存在。旧来源：{zone.SourceObjectId}" :
                        $"区域“{zone.DisplayName}”的来源身份变化：{zone.SourceObjectId} → {current.SourceObjectId}"));
            }
            foreach (var node in _previous.Nodes)
            {
                var current = _scene.Nodes.FirstOrDefault(n => n.Id == node.NodeId);
                if (current == null || current.SourceObjectId != node.SourceObjectId || current.ZoneId != node.ZoneId || current.Kind != node.NodeKind)
                {
                    var changes = new List<string>();
                    if (current == null) changes.Add($"群“{node.DisplayName}”在当前场景中已不存在。旧层级：{node.Description}；旧来源：{node.SourceObjectId}");
                    else
                    {
                        changes.Add($"群“{current.Name}”，层级：{current.HierarchyPath}");
                        if (current.SourceObjectId != node.SourceObjectId) changes.Add($"来源身份：{node.SourceObjectId} → {current.SourceObjectId}");
                        if (current.ZoneId != node.ZoneId) changes.Add($"区域归属：{node.ZoneId} → {current.ZoneId}");
                        if (current.Kind != node.NodeKind) changes.Add($"群类型：{node.NodeKind} → {current.Kind}");
                    }
                    _diagnostics.Add(new MapGraphValidationIssue("NodeSynchronizationRequired", node.NodeId, detail: string.Join("；", changes)));
                }
            }
            if (Mode != MapGraphGenerationMode.ConnectionsAndLayout && (_scene.Nodes.Count != _previous.Nodes.Count || _scene.Zones.Count != _previous.Zones.Count))
                _diagnostics.Add(new MapGraphValidationIssue("LayoutOnlyCannotSynchronizeTopology", "graph"));
            if (!_diagnostics.Any(i => i.IsError)) return true;
            Fail("SceneSynchronizationRequired", $"场景：{_scene.ScenePath}。请核对上方具体差异，再同步地图；重复点击生成不会清除旧节点。"); return false;
        }

        private void Publish()
        {
            var draft = Mode == MapGraphGenerationMode.ValidateOnly ? _previous : _connections?.Result ?? _layout?.Result;
            if (_connections != null) _diagnostics.AddRange(_connections.Diagnostics);
            if (draft == null)
            { Fail("NoPublishableLayout", _layout == null ? "" : string.Join("\n", _layout.FailureCounts.Select(p => p.Key + "=" + p.Value))); return; }
            var intent = MapGraphIntentPreservation.AllIntent;
            if (Mode != MapGraphGenerationMode.ConnectionsAndLayout) intent |= MapGraphIntentPreservation.Topology;
            _diagnostics.AddRange(MapGraphValidation.Validate(draft, _reference, intent).Issues);
            foreach (var profile in _scene.Profiles)
                _diagnostics.AddRange(MapGraphNavigationValidation.Validate(draft,
                    _scan.Connections.Where(c => c.ProfileId == profile.Data.ProfileId).Select(c => c.Edge).ToArray(), Mode == MapGraphGenerationMode.ConnectionsAndLayout).Issues);
            if (_diagnostics.Any(i => i.IsError)) { Fail("GeneratedLayoutValidationFailed"); return; }
            if (!CheckCurrentInputs()) return;
            Result = new MapGraphGenerationResult(RequestId, InputRevision, Mode, _scene, draft, _scan.Anchors, _scan.Connections, JsonUtility.ToJson(_settings));
            Stage = MapGraphGenerationStage.Ready;
        }

        private bool CheckCurrentInputs()
        {
            var current = Capture();
            bool unchanged = current != null && current.IsValid && current.SceneGuid == _scene.SceneGuid && current.ScenePath == _scene.ScenePath &&
                current.SceneFingerprint == _scene.SceneFingerprint && current.NavigationFingerprint == _scene.NavigationFingerprint &&
                current.RuntimeNavigationFingerprint == _scene.RuntimeNavigationFingerprint;
            if (!unchanged) Fail("GenerationInputsChanged", "场景或导航输入已经变化，请重新生成。", MapGraphGenerationStage.Stale);
            return unchanged;
        }

        private MapGraphSceneSnapshot Capture()
        {
            long started = Stopwatch.GetTimestamp();
            try { Physics.SyncTransforms(); return _capture(); }
            finally { CaptureMilliseconds += MillisecondsSince(started); }
        }
        private void Fail(string code, string detail = "", MapGraphGenerationStage stage = MapGraphGenerationStage.Failed)
        { _diagnostics.Add(new MapGraphValidationIssue(code, "graph", detail: detail)); StopTasks(); Result = null; Stage = stage; }
        private void StopTasks() { _scan?.Cancel(); _connections?.Cancel(); _layout?.Cancel(); }
        private static double MillisecondsSince(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    }
}

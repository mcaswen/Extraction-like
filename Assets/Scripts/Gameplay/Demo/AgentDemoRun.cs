#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Telemetry;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.View;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Demo
{
    /// <summary>
    /// 一次导演运行的现场：焦点智能体、它的记录器和地图绑定，推进条件用到的记忆，以及导演经正式入口做的操作
    /// （地图提交路线、让敌人察觉智能体）。指令、路线结果和关键时刻写进事件记录。
    /// </summary>
    public sealed class AgentDemoRun
    {
        private const float AlertTriggerRange = 35f, StationaryAlertRange = 80f, StationarySeconds = 1f, RealertSeconds = 6f, ScanSeconds = .25f,
            MinimumRouteMeters = 25f;

        private readonly List<string> _routeNodes = new List<string>();
        private readonly List<global::EnemyHealthController> _alerted = new List<global::EnemyHealthController>();
        private Vector3 _startPosition, _stepPosition;
        private string _rememberedRoute = string.Empty, _rememberedTarget = string.Empty, _submittedNode = string.Empty;
        private bool _resumedSeen, _supersededSeen, _alerting;
        private float _nextAlertTime, _stationarySince;
        private Vector3 _lastScanPosition;

        public AgentDemoRun(AgentDemoScene scene, int seed, AgentDemoLog log)
        {
            Scene = scene;
            Seed = seed;
            Log = log;
        }

        public AgentDemoScene Scene { get; }
        public int Seed { get; }
        public AgentDemoLog Log { get; }
        public AgentPawnRoot Pawn { get; private set; }
        public AgentBrainRecorder Recorder { get; private set; }
        public MapGraphBindingAuthoring Binding { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public bool Failed => FailureReason.Length > 0;

        public void Fail(string reason)
        {
            if (!Failed) FailureReason = reason;
        }

        internal void Attach(AgentPawnRoot pawn, AgentBrainRecorder recorder, MapGraphBindingAuthoring binding)
        {
            Pawn = pawn;
            Recorder = recorder;
            Binding = binding;
            _startPosition = _stepPosition = pawn.Position;
            pawn.DirectiveLifecycle.ResultPublished += OnDirective;
            pawn.RouteResultPublished += OnRoute;
        }

        internal void Detach()
        {
            if (Pawn == null) return;
            Pawn.DirectiveLifecycle.ResultPublished -= OnDirective;
            Pawn.RouteResultPublished -= OnRoute;
        }

        internal void BeginStep() => _stepPosition = Pawn.Position;

        /// <summary>
        /// 每帧由导演调用：智能体走到敌人 35 米内时让它察觉；智能体停下来（例如在箱旁等背包）时把范围放到 80 米，
        /// 敌人会被引到它现在的位置。迟迟没打起来就再叫一次；反击开始后不再叫敌人。
        /// </summary>
        internal void Tick()
        {
            if (!_alerting) return;
            if (IsRetaliatingOverRememberedCommand())
            {
                _alerting = false;
                return;
            }
            if (Time.unscaledTime < _nextAlertTime) return;
            if (Planar(Pawn.Position - _lastScanPosition) > .1f) _stationarySince = Time.unscaledTime;
            _lastScanPosition = Pawn.Position;
            bool stationary = Time.unscaledTime - _stationarySince >= StationarySeconds;
            _nextAlertTime = Time.unscaledTime + (AlertNearestEnemy(stationary ? StationaryAlertRange : AlertTriggerRange, !stationary) ? RealertSeconds : ScanSeconds);
        }

        // —— 推进条件 ——

        public bool HasDecisionRound => Recorder != null && Recorder.RoundTotal > 0 && Recorder.GetRound(0).Count > 0;

        public bool HasChosenCandidate => HasDecisionRound && Recorder.GetRound(0).ChosenRow >= 0;

        public bool RouteActive(AgentRouteSource source)
        {
            AgentRouteSnapshot route = Pawn.RouteSnapshot;
            return route.IsActive && route.Request.Source == source;
        }

        public bool MovedSinceStart(float meters) => Planar(Pawn.Position - _startPosition) >= meters;

        public bool MovedSinceStep(float meters) => Planar(Pawn.Position - _stepPosition) >= meters;

        /// <summary>记下当前路线和它的目标群，之后判断"同一条命令恢复"和"被替换"；总是返回真</summary>
        public bool RememberRoute()
        {
            AgentRouteSnapshot route = Pawn.RouteSnapshot;
            _rememberedRoute = route.Request.RequestId ?? string.Empty;
            _rememberedTarget = route.NodeIds != null && route.NodeIds.Count > 0 ? route.NodeIds[route.NodeIds.Count - 1] : string.Empty;
            Log.Begin("remember").Field("route", _rememberedRoute).Field("target", _rememberedTarget).End();
            return true;
        }

        public bool RememberCommand() => RememberRoute();

        /// <summary>反击命令占着活动槽，记下的那条路线的命令在挂起槽</summary>
        public bool IsRetaliatingOverRememberedCommand()
        {
            AgentDirectiveLifecycleController lifecycle = Pawn.DirectiveLifecycle;
            return lifecycle.Active.HasValue && AgentManualDirectiveLock.IsCombatDamageDirective(lifecycle.Active.Value) &&
                   lifecycle.SuspendedDirective.HasValue && lifecycle.SuspendedDirective.Value.RouteContext.RootRequestId == _rememberedRoute;
        }

        /// <summary>挂起的那条路线发布了"恢复"，并且又回到活动槽，路线还是同一条</summary>
        public bool RememberedCommandResumed()
        {
            AgentDirectiveLifecycleController lifecycle = Pawn.DirectiveLifecycle;
            return _resumedSeen && lifecycle.Active.HasValue && !AgentManualDirectiveLock.IsCombatDamageDirective(lifecycle.Active.Value) &&
                   lifecycle.Active.Value.RouteContext.RootRequestId == _rememberedRoute && Pawn.RouteSnapshot.Request.RequestId == _rememberedRoute;
        }

        /// <summary>记下的自主路线被替换，现在执行的是指挥员路线</summary>
        public bool CommanderRouteReplacedRemembered()
        {
            AgentRouteSnapshot route = Pawn.RouteSnapshot;
            return _supersededSeen && route.IsActive && route.Request.Source == AgentRouteSource.Player && route.Request.RequestId != _rememberedRoute;
        }

        // —— 导演操作（正式入口） ——

        /// <summary>
        /// 选一个资源群，经地图正式入口给焦点智能体下达路线。优先选图上路程至少 25 米里最近的；
        /// avoidEnemyNodes 时避开途经敌人群的路线，免得途中被路线本身派去交战；excludeCurrentTarget 时不选当前路线的目标群。
        /// </summary>
        public void SubmitCommanderRoute(bool avoidEnemyNodes, bool excludeCurrentTarget = false)
        {
            if (Binding == null) { Fail("场景没有就绪"); return; }
            var kinds = new Dictionary<string, MapGraphNodeKind>();
            foreach (MapGraphNodeDefinition node in Binding.MapDefinition.Nodes)
                if (node != null) kinds[node.NodeId] = node.NodeKind;

            // 依次比较：是否途经敌人群（需要避开时）、是否短于 25 米、图上路程
            string best = string.Empty;
            float bestRank = float.PositiveInfinity, bestLength = 0f;
            bool bestPassesEnemies = false;
            int estimated = 0;
            foreach (MapGraphTargetBinding binding in Binding.TargetBindings)
            {
                if (binding == null || !(binding.DirectTarget is ResourceClusterAuthoring cluster) || !cluster.isActiveAndEnabled || cluster.HasBeenCompleted) continue;
                if (excludeCurrentTarget && (binding.NodeId == _rememberedTarget)) continue;
                if (!Pawn.TryEstimateRoute(AgentTargetRef.FromConcreteObject(AgentTargetKind.Resource, cluster.gameObject, cluster.TargetId), _routeNodes, out float length)) continue;
                estimated++;
                bool passesEnemies = PassesEnemies(kinds);
                float rank = (avoidEnemyNodes && passesEnemies ? 2000000f : 0f) + (length < MinimumRouteMeters ? 1000000f : 0f) + length;
                if (rank >= bestRank) continue;
                best = binding.NodeId;
                bestRank = rank;
                bestLength = length;
                bestPassesEnemies = passesEnemies;
            }

            _submittedNode = best;
            if (_submittedNode.Length == 0) { Fail("找不到合适的资源群"); return; }
            MapGraphPresenter presenter = Object.FindObjectOfType<MapGraphPresenter>();
            bool viaMap = presenter != null && presenter.isActiveAndEnabled;
            if (viaMap) presenter.SubmitNode(_submittedNode);
            else AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest(_submittedNode, AgentRouteSource.Player, Pawn.AgentId));
            Log.Begin("commander-route").Field("node", _submittedNode).Field("length", bestLength).Field("passesEnemies", bestPassesEnemies)
                .Field("candidates", estimated).Field("viaMap", viaMap).End();
        }

        /// <summary>开始等智能体走近敌人：进入 35 米内时让最近的敌人察觉它（敌人走自己的追击和开火）</summary>
        public void ArmEnemyAlert()
        {
            _alerting = true;
            _nextAlertTime = _stationarySince = Time.unscaledTime;
            _lastScanPosition = Pawn.Position;
        }

        // 让给定范围内离智能体最近的存活敌人察觉智能体（skipAlerted 时跳过叫过的）；没有这样的敌人时返回假
        private bool AlertNearestEnemy(float range, bool skipAlerted)
        {
            global::EnemyHealthController nearest = null;
            float nearestDistance = range;
            foreach (global::EnemyHealthController enemy in Object.FindObjectsOfType<global::EnemyHealthController>())
            {
                if (enemy == null || !enemy.IsAlive || (skipAlerted && _alerted.Contains(enemy)) ||
                    enemy.GetComponent<global::IEnemyCombatAlertReceiver>() == null) continue;
                float distance = Vector3.Distance(enemy.transform.position, Pawn.Position);
                if (distance >= nearestDistance) continue;
                nearest = enemy;
                nearestDistance = distance;
            }

            if (nearest == null) return false;
            if (!_alerted.Contains(nearest)) _alerted.Add(nearest);
            foreach (global::IEnemyCombatAlertReceiver receiver in nearest.GetComponents<global::IEnemyCombatAlertReceiver>())
                receiver.NotifyCombatAlert(Pawn.CachedTransform);
            Log.Begin("enemy-alert").Field("enemy", nearest.name).Field("distance", nearestDistance).End();
            return true;
        }

        private bool PassesEnemies(Dictionary<string, MapGraphNodeKind> kinds)
        {
            foreach (string node in _routeNodes)
                if (kinds.TryGetValue(node, out MapGraphNodeKind kind) && (kind == MapGraphNodeKind.EnemySource || kind == MapGraphNodeKind.ActiveEnemy))
                    return true;
            return false;
        }

        private void OnDirective(AgentDirectiveResult result)
        {
            AgentDirectiveRequest request = result.Request;
            if (result.Stage == AgentDirectiveStage.Resumed && request.RouteContext.RootRequestId == _rememberedRoute) _resumedSeen = true;
            Log.Begin("directive").Field("stage", result.Stage.ToString()).Field("type", request.DirectiveType.ToString())
                .Field("command", request.CommandId).Field("route", request.RouteContext.RootRequestId ?? string.Empty)
                .Field("retaliation", AgentManualDirectiveLock.IsCombatDamageDirective(request))
                .Field("target", request.TargetObject != null ? request.TargetObject.name : string.Empty)
                .Field("reason", result.Reason.ToString()).End();
        }

        private void OnRoute(AgentRouteResult result)
        {
            if (result.Request.RequestId == _rememberedRoute && result.Stage == AgentRouteStage.Cancelled) _supersededSeen = true;
            Log.Begin("route").Field("stage", result.Stage.ToString()).Field("request", result.Request.RequestId)
                .Field("source", result.Request.Source.ToString()).Field("node", result.Request.TargetNodeId)
                .Field("reason", result.Reason.ToString()).Field("replan", result.IsReplan).End();
        }

        private static float Planar(Vector3 delta)
        {
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Binding;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.Overlay
{
    /// <summary>
    /// 一台智能体的叠加层：视线、接近点、导航路径、决策图路线和攻击距离。
    /// 只读遥测记录和只读入口，不重算判定；每帧只改已有线的点和颜色。
    /// </summary>
    public sealed class AgentBrainOverlayDrawer
    {
        /// <summary>射击判定后视线保持显示的时长（不受暂停影响的时钟）</summary>
        public const float SightHoldSeconds = 1.5f;

        /// <summary>接近点求解后保持显示的时长</summary>
        public const float ProbeHoldSeconds = 3f;

        // 线宽按屏幕像素给出，每帧按智能体处的像素尺寸换算成米，镜头拉远拉近时粗细不变
        public const float PathWidth = 4f, RouteWidth = 5f, SightWidth = 4f, MarkWidth = 3f, GuideWidth = 2f;
        public const float PathLift = .1f, RouteLift = .3f, MarkLift = .05f;

        public static readonly Color SightClear = Style.Success;
        public static readonly Color SightBlocked = Style.Failure;
        public static readonly Color ProbeChosen = Style.Glow;
        public static readonly Color ProbeCannotFire = Style.Failure;
        public static readonly Color ProbeUnreachable = Style.Dim;
        public static readonly Color PathColor = Style.Teal;
        public static readonly Color RouteColor = Style.Violet;
        public static readonly Color RiskColor = Style.Amber;
        public static readonly Color RangeColor = new Color(Style.Muted.r, Style.Muted.g, Style.Muted.b, .55f);
        private static readonly Color SightBeyondBlock = new Color(Style.Failure.r, Style.Failure.g, Style.Failure.b, .3f);
        private static readonly Color RingGuide = new Color(Style.Muted.r, Style.Muted.g, Style.Muted.b, .35f);

        private readonly AgentBrainOverlayLines _lines;
        private readonly Vector3[] _points = new Vector3[AgentBrainOverlayLines.MaxPoints];
        private readonly List<global::EnemyHealthController> _enemies = new List<global::EnemyHealthController>(16);
        private AgentBrainRecorder _recorder;
        private long _shotVersion = -1, _probeVersion = -1;
        private float _shotTime = float.NegativeInfinity, _probeTime = float.NegativeInfinity;
        private float _pixel;

        public AgentBrainOverlayDrawer(AgentBrainOverlayLines lines)
        {
            _lines = lines;
        }

        public AgentBrainRecorder Recorder => _recorder;

        public AgentBrainOverlayLines Lines => _lines;

        /// <summary>本帧画出的计入风险的敌人数（测试读取）</summary>
        public int RiskEnemyCount { get; private set; }

        /// <summary>本帧路线折线经过的群锚点数（测试读取）</summary>
        public int RouteAnchorCount { get; private set; }

        public void Draw(AgentBrainRecorder recorder, MapGraphBindingAuthoring binding, float now)
        {
            if (!ReferenceEquals(recorder, _recorder))
            {
                _recorder = recorder;
                _shotVersion = _probeVersion = -1;
                _shotTime = _probeTime = float.NegativeInfinity;
            }

            _lines.Begin();
            RiskEnemyCount = 0;
            RouteAnchorCount = 0;
            AgentPawnRoot pawn = recorder != null ? recorder.Pawn : null;
            if (pawn != null && recorder.IsAttached && !pawn.IsDead)
            {
                _pixel = PixelSize(pawn.Position);
                DrawRange(pawn);
                DrawPath(recorder);
                DrawRoute(pawn, binding);
                DrawProbes(recorder.ApproachProbes, now);
                DrawSight(recorder.Shooter, now);
            }
            _lines.End();
        }

        /// <summary>主相机在该点处一个屏幕像素对应的米数；没有主相机时按 2 厘米</summary>
        public static float PixelSize(Vector3 at)
        {
            Camera camera = Camera.main;
            int height = Screen.height > 0 ? Screen.height : 1080;
            if (camera == null) return .02f;
            if (camera.orthographic) return 2f * camera.orthographicSize / height;
            float depth = Mathf.Max(.1f, Vector3.Dot(at - camera.transform.position, camera.transform.forward));
            return 2f * depth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) / height;
        }

        public void Hide()
        {
            _lines.HideAll();
            RiskEnemyCount = 0;
            RouteAnchorCount = 0;
        }

        private void DrawRange(AgentPawnRoot pawn)
        {
            float range = pawn.Blackboard != null ? pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.AttackRange) : 0f;
            if (range > 0f) _lines.Circle(pawn.Position + Vector3.up * MarkLift, range, RangeColor, GuideWidth * _pixel);
        }

        private void DrawPath(AgentBrainRecorder recorder)
        {
            int count = Mathf.Min(recorder.PathCornerCount, _points.Length);
            for (int index = 0; index < count; index++) _points[index] = recorder.GetPathCorner(index) + Vector3.up * PathLift;
            _lines.Polyline(_points, count, PathColor, PathWidth * _pixel);
        }

        // 从智能体出发，经当前群和之后的各群；途经敌人群里存活的敌人就是路线风险计入的敌人
        private void DrawRoute(AgentPawnRoot pawn, MapGraphBindingAuthoring binding)
        {
            AgentRouteSnapshot route = pawn.RouteSnapshot;
            if (binding == null || !route.IsActive || route.NodeIds == null) return;
            int count = 0;
            _points[count++] = pawn.Position + Vector3.up * RouteLift;
            for (int index = Mathf.Max(0, route.StepIndex); index < route.NodeIds.Count && count < _points.Length; index++)
            {
                string nodeId = route.NodeIds[index];
                if (!binding.TryGetNavigationAnchor(nodeId, out Vector3 anchor)) continue;
                Vector3 lifted = anchor + Vector3.up * RouteLift;
                _points[count++] = lifted;
                RouteAnchorCount++;
                bool current = index == route.StepIndex;
                _lines.Circle(lifted, current ? 1f : .7f, RouteColor, (current ? RouteWidth : MarkWidth) * _pixel);
                DrawRiskEnemies(binding, nodeId);
            }
            _lines.Polyline(_points, count, RouteColor, RouteWidth * _pixel);
        }

        private void DrawRiskEnemies(MapGraphBindingAuthoring binding, string nodeId)
        {
            if (!binding.TryGetBinding(nodeId, out MapGraphTargetBinding target)) return;
            ActiveEnemyClusterAuthoring active = target.DirectTarget is EnemySourceClusterAuthoring source
                ? source.ConfiguredActiveEnemyCluster : target.DirectTarget as ActiveEnemyClusterAuthoring;
            if (active == null) return;
            active.CopyAliveEnemiesTo(_enemies);
            for (int index = 0; index < _enemies.Count; index++)
            {
                _lines.Circle(_enemies[index].transform.position + Vector3.up * MarkLift, .8f, RiskColor, PathWidth * _pixel);
                RiskEnemyCount++;
            }
        }

        // 三圈环扫里同一圈的点按探测顺序连起来；每个点按结果着色，选中点加一圈
        private void DrawProbes(AgentApproachProbeLog log, float now)
        {
            if (log.Version != _probeVersion)
            {
                _probeVersion = log.Version;
                _probeTime = now;
            }
            if (log.Count == 0 || now - _probeTime > ProbeHoldSeconds) return;

            ConnectRing(log, AgentApproachProbeStage.Ring1);
            ConnectRing(log, AgentApproachProbeStage.Ring2);
            for (int index = 0; index < log.Count; index++)
            {
                Vector3 point = log.GetPoint(index) + Vector3.up * MarkLift;
                byte outcome = log.GetOutcome(index);
                if (outcome == 0)
                {
                    _lines.Circle(point, 7f * _pixel, ProbeChosen, PathWidth * _pixel);
                    _lines.Circle(point, 14f * _pixel, ProbeChosen, GuideWidth * _pixel);
                }
                else _lines.Circle(point, 6f * _pixel, ProbeColor(outcome), MarkWidth * _pixel);
            }
        }

        private void ConnectRing(AgentApproachProbeLog log, AgentApproachProbeStage stage)
        {
            int count = 0;
            for (int index = 0; index < log.Count; index++)
                if (log.GetStage(index) == stage) _points[count++] = log.GetPoint(index) + Vector3.up * MarkLift;
            _lines.Polyline(_points, count, RingGuide, GuideWidth * _pixel, count == 8);
        }

        // 1 采样不到、2 不可达、3 落点偏离、4 没有拐点：走不到；5 看不见、6 打不到：无法开火
        public static Color ProbeColor(byte outcome)
            => outcome == 0 ? ProbeChosen : outcome >= 5 ? ProbeCannotFire : ProbeUnreachable;

        private void DrawSight(AgentCombatShooter shooter, float now)
        {
            if (shooter == null) return;
            if (shooter.LastShotVersion != _shotVersion)
            {
                _shotVersion = shooter.LastShotVersion;
                _shotTime = now;
            }
            if (now - _shotTime > SightHoldSeconds || shooter.LastShotTarget == null) return;

            Vector3 origin = shooter.LastShotOrigin;
            Vector3 aim = shooter.LastShotAimPoint;
            switch (shooter.LastShotResult)
            {
                case TargetVisibilityResult.Visible:
                    _lines.Segment(origin, aim, SightClear, SightWidth * _pixel);
                    break;
                case TargetVisibilityResult.Occluded:
                    Vector3 block = shooter.LastShotBlockPoint;
                    _lines.Segment(origin, block, SightBlocked, SightWidth * _pixel);
                    _lines.Segment(block, aim, SightBeyondBlock, SightWidth * _pixel);
                    if (shooter.LastShotBlocker != null) _lines.Box(shooter.LastShotBlocker.bounds, SightBlocked, MarkWidth * _pixel);
                    break;
                default:
                    _lines.Segment(origin, aim, Style.Dim, SightWidth * _pixel);
                    break;
            }
        }
    }
}
#endif

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using ExtractionLike.Aerospace;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Telemetry;
using Gameplay.Agent.Telemetry.Overlay;
using Gameplay.Agent.Telemetry.UI;
using Gameplay.MapGraph.Binding;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    /// <summary>
    /// U 叠加层：三档切换、科普档案打开时隐藏；隔墙交战时视线标为被挡并框出墙、接近点标出选中点、导航路径和攻击距离与遥测一致；
    /// 决策图路线经过各群锚点并标出途经敌人群的存活敌人；带挡点输出的可见性判定与原判定一致；逐帧更新不分配内存。
    /// </summary>
    public sealed class BrainOverlayTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator OverlayCyclesThreeModesAndHidesUnderScienceDossier()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "Overlay", Vector3.zero);
            var overlay = AgentBrainOverlay.Ensure();
            try
            {
                yield return null;
                Assert.That(overlay.CurrentMode, Is.EqualTo(AgentBrainOverlay.Mode.Off));
                overlay.Cycle();
                yield return null;
                Assert.That(overlay.CurrentMode, Is.EqualTo(AgentBrainOverlay.Mode.Focused));
                Assert.That(overlay.IsShown && overlay.Legend.enabled, Is.True);
                Assert.That(overlay.Drawers.Count, Is.EqualTo(1));
                Assert.That(overlay.Drawers[0].Lines.UsedCount, Is.GreaterThan(0), "至少画出攻击距离圈");
                overlay.Cycle();
                Assert.That(overlay.CurrentMode, Is.EqualTo(AgentBrainOverlay.Mode.All));

                AerospaceUiInputGate.Set(true);
                yield return null;
                Assert.That(overlay.IsShown, Is.False, "科普档案打开时隐藏");
                Assert.That(overlay.Legend.enabled, Is.False);
                Assert.That(overlay.Drawers.All(d => d.Lines.UsedCount == 0), Is.True, "隐藏时不留线");
                AerospaceUiInputGate.Set(false);
                yield return null;
                yield return null;
                Assert.That(overlay.IsShown, Is.True);

                overlay.Cycle();
                yield return null;
                Assert.That(overlay.CurrentMode, Is.EqualTo(AgentBrainOverlay.Mode.Off));
                Assert.That(overlay.Legend.enabled, Is.False);
                Assert.That(overlay.Drawers.All(d => d.Lines.UsedCount == 0), Is.True);
                ContractCompleted = true;
            }
            finally
            {
                AerospaceUiInputGate.Set(false);
                Cleanup(overlay, null);
            }
        }

        [UnityTest]
        public IEnumerator OverlayShowsBlockedSightProbesPathAndAttackRange()
        {
            TestNavMeshBuilder.Flat(World, 120);
            var agent = AgentFactory.Create(World, "Sight", Vector3.zero, 8, false, false);
            float range = agent.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.AttackRange);
            Assert.That(range, Is.GreaterThan(2f));
            var enemy = EnemyFactory.Passive(World, new Vector3(0, 0, range * .6f));
            var wall = World.Root("Sight wall").AddComponent<BoxCollider>();
            wall.transform.position = new Vector3(0, 1.5f, range * .3f);
            wall.size = new Vector3(5, 3, .4f);
            var overlay = AgentBrainOverlay.Ensure();
            var panel = AgentBrainPanel.Ensure();
            try
            {
                overlay.SetMode(AgentBrainOverlay.Mode.Focused);
                panel.SetOpen(true);
                yield return null;
                AgentBrainRecorder recorder = AgentBrainTelemetryHub.Instance.Find(agent);
                Assert.That(recorder, Is.Not.Null);

                agent.TakeCombatDamage(10, agent.Position, Vector3.back, enemy.gameObject);
                yield return RuntimeWait.Until(() => recorder.Shooter.LastShotResult == TargetVisibilityResult.Occluded &&
                                                     recorder.Shooter.LastShotBlocker == wall, "shot judgment blocked by the wall", 5);
                overlay.TickFrame();
                var lines = Lines(overlay);
                Vector3 origin = recorder.Shooter.LastShotOrigin;
                Assert.That(lines.Any(l => Same(l.startColor, AgentBrainOverlayDrawer.SightBlocked) && l.positionCount == 2 &&
                                           (l.GetPosition(0) - origin).sqrMagnitude < 1e-4f), Is.True, "枪口到挡点画成被挡的颜色");
                Assert.That(lines.Any(l => l.positionCount == 16 && Enumerable.Range(0, 16).All(i => OnBoundsCorner(l.GetPosition(i), wall.bounds))),
                    Is.True, "框出挡住视线的墙");
                yield return BrainPanelTests.Capture(panel, agent, "overlay-blocked-sight", null, overlay.Legend);

                yield return RuntimeWait.Until(() => recorder.ApproachProbes.ChosenIndex >= 0, "approach point chosen around the wall", 5);
                overlay.TickFrame();
                lines = Lines(overlay);
                Vector3 chosen = recorder.ApproachProbes.GetPoint(recorder.ApproachProbes.ChosenIndex) + Vector3.up * AgentBrainOverlayDrawer.MarkLift;
                Assert.That(lines.Count(l => Same(l.startColor, AgentBrainOverlayDrawer.ProbeChosen) && Centered(l, chosen)), Is.EqualTo(2),
                    "选中的接近点画两圈");
                int failed = Enumerable.Range(0, recorder.ApproachProbes.Count).Count(i => recorder.ApproachProbes.GetOutcome(i) != 0);
                Assert.That(lines.Count(l => l.positionCount == 40 && (Same(l.startColor, AgentBrainOverlayDrawer.ProbeCannotFire) ||
                                                                         Same(l.startColor, AgentBrainOverlayDrawer.ProbeUnreachable))),
                    Is.EqualTo(failed), "每个没选中的探测点一个圈");

                yield return RuntimeWait.Until(() => recorder.PathCornerCount >= 2, "navigation path issued", 5);
                overlay.TickFrame();
                lines = Lines(overlay);
                var path = lines.Single(l => Same(l.startColor, AgentBrainOverlayDrawer.PathColor));
                Assert.That(path.positionCount, Is.EqualTo(recorder.PathCornerCount));
                for (int i = 0; i < path.positionCount; i++)
                    Assert.That((path.GetPosition(i) - recorder.GetPathCorner(i) - Vector3.up * AgentBrainOverlayDrawer.PathLift).sqrMagnitude, Is.LessThan(1e-4f));
                var ring = lines.Single(l => Same(l.startColor, AgentBrainOverlayDrawer.RangeColor));
                Vector3 center = agent.Position + Vector3.up * AgentBrainOverlayDrawer.MarkLift;
                Assert.That(Vector3.Distance(ring.GetPosition(0), center), Is.EqualTo(range).Within(.01f), "攻击距离圈半径与黑板一致");
                yield return BrainPanelTests.Capture(panel, agent, "overlay-approach", null, overlay.Legend);

                long allocated = 0;
                yield return ManagedAllocation.MeasureFrame(() =>
                {
                    for (int i = 0; i < 200; i++) overlay.TickFrame();
                }, value => allocated = value);
                CaseArtifactWriter.Trace("brain-overlay-allocation", "bytes=" + allocated + " for 200 frames; lines=" + overlay.Drawers[0].Lines.UsedCount);
                if (allocated != ManagedAllocation.Unavailable) Assert.That(allocated, Is.LessThan(1024), "叠加层逐帧更新不应分配内存");
                ContractCompleted = true;
            }
            finally { Cleanup(overlay, panel); }
        }

        [UnityTest]
        public IEnumerator OverlayDrawsRouteThroughAnchorsAndMarksRiskEnemies()
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Route", Vector3.zero, 4, false, false);
            var first = TargetFactory.Resources(World, new Vector3(12, 0, 0));
            var enemies = TargetFactory.Enemies(World, EnemyFactory.Passive(World, new Vector3(30, 0, 8)), EnemyFactory.Passive(World, new Vector3(33, 0, 8)));
            var last = TargetFactory.Resources(World, new Vector3(50, 0, 0));
            var binding = MapRouteFactory.Bind(World, new GameplayTargetClusterAuthoringBase[] { first, enemies, last },
                new[] { new Vector3(12, 0, 0), new Vector3(31, 0, 4), new Vector3(50, 0, 0) }, MapRouteFactory.Chain(3));
            agent.ConfigureRoutes(MapRouteFactory.Environment(binding, agent));
            var overlay = AgentBrainOverlay.Ensure();
            try
            {
                overlay.SetMode(AgentBrainOverlay.Mode.Focused);
                yield return null;
                agent.TrySubmitRoute(new AgentRouteRequest("n2", AgentRouteSource.Player, agent.AgentId, "overlay-route"));
                yield return RuntimeWait.Until(() => agent.RouteSnapshot.IsActive && agent.RouteSnapshot.Request.RequestId == "overlay-route",
                    "player route accepted", 5);
                overlay.TickFrame();
                AgentBrainOverlayDrawer drawer = overlay.Drawers[0];
                var route = agent.RouteSnapshot;
                int remaining = route.NodeIds.Count - route.StepIndex;
                Assert.That(drawer.RouteAnchorCount, Is.EqualTo(remaining), "从当前群起每个群一个锚点");
                Assert.That(route.NodeIds, Does.Contain("n1"), "路线途经敌人群");
                Assert.That(drawer.RiskEnemyCount, Is.EqualTo(2), "途经敌人群的两个存活敌人都标出");

                var lines = Lines(overlay);
                var polyline = lines.Single(l => Same(l.startColor, AgentBrainOverlayDrawer.RouteColor) && l.positionCount == remaining + 1);
                Assert.That((polyline.GetPosition(0) - agent.Position - Vector3.up * AgentBrainOverlayDrawer.RouteLift).sqrMagnitude, Is.LessThan(1e-4f));
                for (int i = 0; i < remaining; i++)
                {
                    Assert.That(binding.TryGetNavigationAnchor(route.NodeIds[route.StepIndex + i], out Vector3 anchor), Is.True);
                    Assert.That((polyline.GetPosition(i + 1) - anchor - Vector3.up * AgentBrainOverlayDrawer.RouteLift).sqrMagnitude, Is.LessThan(1e-4f));
                }
                Assert.That(lines.Count(l => Same(l.startColor, AgentBrainOverlayDrawer.RiskColor)), Is.EqualTo(2));

                long allocated = 0;
                yield return ManagedAllocation.MeasureFrame(() =>
                {
                    for (int i = 0; i < 200; i++) overlay.TickFrame();
                }, value => allocated = value);
                CaseArtifactWriter.Trace("brain-overlay-route", "nodes=" + string.Join(",", route.NodeIds) + "; step=" + route.StepIndex + "; bytes=" + allocated);
                if (allocated != ManagedAllocation.Unavailable) Assert.That(allocated, Is.LessThan(1024), "画路线逐帧不应分配内存");
                ContractCompleted = true;
            }
            finally { Cleanup(overlay, null); }
        }

        [UnityTest]
        public IEnumerator VisibilityWithBlockerMatchesPlainCheck()
        {
            var viewer = World.Root("Viewer").transform;
            var target = World.Root("Target").AddComponent<BoxCollider>().transform;
            target.position = new Vector3(0, 1, 10);
            var wall = World.Root("Blocking wall").AddComponent<BoxCollider>();
            wall.size = new Vector3(4, 4, .3f);
            yield return null;

            foreach (var (wallZ, range, expected) in new[] { (5f, 20f, TargetVisibilityResult.Occluded), (40f, 20f, TargetVisibilityResult.Visible),
                         (5f, 6f, TargetVisibilityResult.OutOfRange) })
            {
                wall.transform.position = new Vector3(0, 1, wallZ);
                Physics.SyncTransforms();
                Vector3 origin = new Vector3(0, 1, 0);
                var plain = TargetVisibilityQuery.Check(viewer, origin, target, range);
                var detailed = TargetVisibilityQuery.Check(viewer, origin, target, range, 360f, Physics.DefaultRaycastLayers, out Collider blocker, out Vector3 point);
                Assert.That(plain, Is.EqualTo(expected));
                Assert.That(detailed, Is.EqualTo(plain), "带挡点输出的判定与原判定一致");
                if (expected == TargetVisibilityResult.Occluded)
                {
                    Assert.That(blocker, Is.SameAs(wall));
                    Assert.That(point.z, Is.EqualTo(wallZ - .15f).Within(.01f), "挡点在墙面上");
                }
                else Assert.That(blocker, Is.Null);
                Assert.That(TargetVisibilityQuery.ClearSegment(viewer, target, origin, target.position, Physics.DefaultRaycastLayers, null, out _, out _),
                    Is.EqualTo(TargetVisibilityQuery.ClearSegment(viewer, target, origin, target.position)));
            }
            ContractCompleted = true;
        }

        private static List<LineRenderer> Lines(AgentBrainOverlay overlay)
        {
            var lines = new List<LineRenderer>();
            foreach (var drawer in overlay.Drawers)
                for (int i = 0; i < drawer.Lines.UsedCount; i++) lines.Add(drawer.Lines.Get(i));
            return lines;
        }

        private static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < .002f && Mathf.Abs(a.g - b.g) < .002f &&
                                                       Mathf.Abs(a.b - b.b) < .002f && Mathf.Abs(a.a - b.a) < .002f;

        private static bool Centered(LineRenderer line, Vector3 center)
        {
            if (line.positionCount != 40) return false;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < line.positionCount; i++) sum += line.GetPosition(i);
            return (sum / line.positionCount - center).sqrMagnitude < 1e-3f;
        }

        private static bool OnBoundsCorner(Vector3 p, Bounds b)
            => (Mathf.Abs(p.x - b.min.x) < 1e-3f || Mathf.Abs(p.x - b.max.x) < 1e-3f) &&
               (Mathf.Abs(p.y - b.min.y) < 1e-3f || Mathf.Abs(p.y - b.max.y) < 1e-3f) &&
               (Mathf.Abs(p.z - b.min.z) < 1e-3f || Mathf.Abs(p.z - b.max.z) < 1e-3f);

        private static void Cleanup(AgentBrainOverlay overlay, AgentBrainPanel panel)
        {
            if (overlay != null) Object.DestroyImmediate(overlay.gameObject);
            if (panel != null) Object.DestroyImmediate(panel.gameObject);
            if (AgentBrainTelemetryHub.Instance != null) Object.DestroyImmediate(AgentBrainTelemetryHub.Instance.gameObject);
        }
    }
}

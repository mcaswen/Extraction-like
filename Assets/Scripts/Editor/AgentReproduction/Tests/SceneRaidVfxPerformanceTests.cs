using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidVfxPerformanceTests : ReproductionTestFixture
    {
        private static T Callback<T>(object instance, string name) where T : Delegate =>
            (T)Delegate.CreateDelegate(typeof(T), instance, instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic));
        public static string[] HotPaths = { "MudIdle", "RobotGlyph", "RobotLock", "RobotBeam" };

        [UnityTest]
        public IEnumerator WarmVisualUpdatesDoNotAllocatePathArrays([ValueSource(nameof(HotPaths))] string hotPath)
        {
            Action refresh;
            if (hotPath == "MudIdle")
            {
                var mud = World.Root("Mud allocation probe").AddComponent<MudTidalAberrationVfx>();
                mud.ConfigureAsEnemyDrivenVisual(null, null, null, 3, 3);
                refresh = Callback<Action>(mud, "UpdateIdleTentacles");
            }
            else
            {
                var robot = World.Root("Robot allocation probe").AddComponent<RobotAnchorBeamVfx>();
                RuntimeFixtureAccess.Configure(robot, "_autoActivateOnPlayerRange", false);
                if (hotPath == "RobotBeam")
                {
                    var beam = Callback<Action<Vector3, Vector3>>(robot, "UpdateBeamVisual");
                    refresh = () => beam(Vector3.up, new Vector3(0, 2, 8));
                }
                else
                {
                    var callback = Callback<Action<float>>(robot, hotPath == "RobotGlyph" ? "UpdateGlyphVisual" : "UpdateLockLine");
                    refresh = () => callback(0.5f);
                }
            }
            yield return null;
            for (int i = 0; i < 10; i++) refresh();
            using (var calibration = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                GC.KeepAlive(new byte[4096]);
                calibration.Stop();
                Assert.That(calibration.Valid && calibration.Count > 0, Is.True, "Allocation measurement must detect a known allocation.");
            }
            int allocations;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                for (int i = 0; i < 100; i++) refresh();
                recorder.Stop();
                allocations = recorder.Count;
            }
            CaseArtifactWriter.Trace("visual-allocation", hotPath + ": " + allocations + " allocation samples / 100 calls");
            Assert.That(allocations, Is.Zero, "Warm visual geometry callbacks must reuse their path arrays.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator MudPathsRemainIndependentResizeAndFollowTarget()
        {
            var target = World.Root("Mud visual target").transform; target.position = new Vector3(0, 0, 8);
            var mud = World.Root("Mud paths").AddComponent<MudTidalAberrationVfx>();
            mud.ConfigureAsEnemyDrivenVisual(target, null, null, 3, 3);
            mud.PlayTentacleContactVisual(target); mud.PlayWaterJetVisual(target);
            yield return null;
            var jet = GameObject.Find("MudHighPressureWaterJetVfx"); Assert.That(jet, Is.Not.Null);
            var outer = jet.transform.Find("WaterJetOuter").GetComponent<LineRenderer>();
            var core = jet.transform.Find("WaterJetCore").GetComponent<LineRenderer>();
            var spray = jet.transform.Find("WaterJetSprayEdge").GetComponent<LineRenderer>();
            Assert.That((outer.GetPosition(9) - core.GetPosition(9)).sqrMagnitude, Is.GreaterThan(0.000001f));
            Assert.That((outer.GetPosition(9) - spray.GetPosition(9)).sqrMagnitude, Is.GreaterThan(0.000001f));
            var bodies = Object.FindObjectsOfType<LineRenderer>().Where(x => x.name == "TentacleBody").ToArray();
            Assert.That(bodies.Length, Is.EqualTo(7));
            Assert.That(bodies.Select(x => x.GetPosition(0)).Distinct().Count(), Is.GreaterThan(3));
            RuntimeFixtureAccess.Configure(mud, "_pathPointCount", 31);
            target.position = new Vector3(3, 0, 7);
            yield return null; yield return null;
            foreach (var line in new[] { outer, core, spray })
            {
                Assert.That(line.positionCount, Is.EqualTo(31));
                Assert.That(Vector3.Distance(line.GetPosition(30), target.position + Vector3.up * 0.9f), Is.LessThan(0.001f));
            }
            foreach (var line in bodies) Assert.That(line.positionCount, Is.EqualTo(31));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RobotBeamPathsRemainIndependentResizeAndPreserveEndpoints()
        {
            var robot = World.Root("Robot paths").AddComponent<RobotAnchorBeamVfx>();
            RuntimeFixtureAccess.Configure(robot, "_autoActivateOnPlayerRange", false);
            yield return null;
            var refresh = Callback<Action<Vector3, Vector3>>(robot, "UpdateBeamVisual");
            Vector3 origin = new Vector3(1, 2, 3), target = new Vector3(4, 5, 9);
            refresh(origin, target);
            var names = new[] { "_outerBeamLine", "_coreBeamLine", "_wispLineA", "_wispLineB" };
            var lines = names.Select(x => RuntimeFixtureAccess.Read<LineRenderer>(robot, x)).ToArray();
            Assert.That(lines.Select(x => x.GetPosition(x.positionCount / 2)).Distinct().Count(), Is.EqualTo(4));
            RuntimeFixtureAccess.Configure(robot, "_beamPointCount", 37);
            refresh(origin, target);
            foreach (var line in lines)
            {
                Assert.That(line.positionCount, Is.EqualTo(37));
                Assert.That(Vector3.Distance(line.GetPosition(0), origin), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(line.GetPosition(36), target), Is.LessThan(0.001f));
            }
            var rings = new[] { "_baseRingLine", "_topRingLine", "_originRingLine" }
                .Select(x => RuntimeFixtureAccess.Read<LineRenderer>(robot, x)).ToArray();
            Assert.That(rings.Select(x => x.GetPosition(0)).Distinct().Count(), Is.EqualTo(3));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DisablingMudDuringBothAttacksDestroysOwnedVisualRoots()
        {
            var target = World.Root("Disable visual target").transform; target.position = Vector3.forward * 8;
            var mud = World.Root("Disable mud").AddComponent<MudTidalAberrationVfx>();
            mud.ConfigureAsEnemyDrivenVisual(target, null, null, 3, 3);
            mud.PlayTentacleContactVisual(target); mud.PlayWaterJetVisual(target);
            yield return null;
            var contact = GameObject.Find("MudContactTentacleVfx");
            var jet = GameObject.Find("MudHighPressureWaterJetVfx");
            Assert.That(contact, Is.Not.Null); Assert.That(jet, Is.Not.Null);
            World.Own(contact); World.Own(jet);
            mud.enabled = false;
            yield return null; yield return null;
            Assert.That(contact == null, Is.True, "Stopped contact coroutine must not orphan its root.");
            Assert.That(jet == null, Is.True, "Stopped jet coroutine must not orphan its root.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ReenabledMudRestoresMaterialsForRecreatedTentacles()
        {
            var mud = World.Root("Reenable mud").AddComponent<MudTidalAberrationVfx>();
            mud.ConfigureAsEnemyDrivenVisual(null, null, null, 3, 3);
            yield return null;
            mud.enabled = false; yield return null; yield return null;
            mud.enabled = true; yield return null; yield return null;
            var lines = Object.FindObjectsOfType<LineRenderer>().Where(x => x.name == "TentacleBody" || x.name == "TentacleRim").ToArray();
            Assert.That(lines.Length, Is.EqualTo(12));
            foreach (var line in lines) Assert.That(line.sharedMaterial, Is.Not.Null, "Recreated tentacles need owned materials after OnDisable destroys them.");
            ContractCompleted = true;
        }
    }
}

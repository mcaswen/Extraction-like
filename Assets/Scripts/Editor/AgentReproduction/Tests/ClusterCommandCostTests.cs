using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ClusterCommandCostTests : ReproductionTestFixture
    {
        public static string[] Kinds = { "Resource", "Enemy", "Extraction" };
        public static bool[] Distances = { false, true };
        public static int[] MemberCounts = { 1, 8 };

        [Serializable] private sealed class Sample
        {
            public string kind, phase, commandId, gcUnit;
            public bool far;
            public int members, frameBefore, frameAfter, gcAllocationSamples, navigationChecks;
            public float distance, discoveryRange;
            public double milliseconds;
            public long resourcePathCalculations;
        }

        private static Sample Measure(Action action)
        {
            var sample = new Sample();
            using var navigation = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Anomaly.Navigation.Check", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            sample.frameBefore = Time.frameCount;
            long start = Stopwatch.GetTimestamp();
            action();
            long end = Stopwatch.GetTimestamp();
            allocations.Stop(); navigation.Stop();
            sample.frameAfter = Time.frameCount;
            Assert.That(navigation.Valid && allocations.Valid, Is.True, "Both synchronous probes must be available.");
            Assert.That(navigation.Count, Is.LessThan(4096), "A full buffer cannot prove complete evidence.");
            Assert.That(allocations.Count, Is.LessThan(4096));
            Assert.That(sample.frameAfter, Is.EqualTo(sample.frameBefore));
            sample.milliseconds = (end - start) * 1000d / Stopwatch.Frequency;
            sample.gcAllocationSamples = allocations.Count;
            sample.navigationChecks = navigation.Count;
            return sample;
        }

        [UnityTest]
        public IEnumerator ProbeCalibratesSynchronousNavigationAndAllocation()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var buffer = new AgentNavigationQuery.Buffer();
            AgentNavigationQuery.Check(agent.NavMeshAgent, Vector3.right * 20, 0, buffer);
            Action empty = () => { };
            Measure(empty); // Warm only the measurement wrapper.
            var zero = Measure(empty);
            Assert.That(zero.navigationChecks, Is.Zero);
            Assert.That(zero.gcAllocationSamples, Is.Zero);
            long before = buffer.CalculationCount;
            var known = Measure(() =>
            {
                GC.KeepAlive(new byte[4096]);
                AgentNavigationQuery.Check(agent.NavMeshAgent, Vector3.right * 20, 0, buffer);
                AgentNavigationQuery.Check(agent.NavMeshAgent, Vector3.right * 21, 0, buffer);
            });
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name == "GC.Alloc") known.gcUnit = description.UnitType.ToString();
            }
            Assert.That(known.gcUnit, Is.EqualTo("TimeNanoseconds"), "Allocation marker values must never be mistaken for bytes.");
            Assert.That(known.gcAllocationSamples, Is.EqualTo(1));
            Assert.That(buffer.CalculationCount - before, Is.EqualTo(2));
            Assert.That(known.navigationChecks, Is.EqualTo(2), "Individual samples must be readable in this same call.");
            var twice = Measure(() => { GC.KeepAlive(new byte[8192]); GC.KeepAlive(new byte[4096]); });
            Assert.That(twice.gcAllocationSamples, Is.EqualTo(2));
            Assert.That(twice.navigationChecks, Is.Zero);
            CaseArtifactWriter.Trace("command-cost-calibration", JsonUtility.ToJson(known));
            CaseArtifactWriter.Trace("command-cost-calibration-two-allocations", JsonUtility.ToJson(twice));
            ContractCompleted = true;
            yield return null;
        }

        private GameplayTargetClusterAuthoringBase CreateCluster(string kind, Vector3[] positions)
        {
            if (kind == "Resource") return TargetFactory.Resources(World, positions);
            if (kind == "Enemy") return TargetFactory.Enemies(World,
                positions.Select(p => EnemyFactory.Passive(World, p)).ToArray());
            var result = TargetFactory.Extraction(World, positions[0]);
            result.gameObject.SetActive(false);
            var members = new List<GameplayTargetEntityMember>(result.ExtractionMembers);
            for (int i = 1; i < positions.Length; i++)
            {
                var extra = TargetFactory.Extraction(World, positions[i]);
                members.AddRange(extra.ExtractionMembers);
                extra.gameObject.SetActive(false); // Points are independent roots in the fixture factory.
            }
            RuntimeFixtureAccess.Configure(result, "_extractionMembers", members);
            result.gameObject.SetActive(true);
            return result;
        }

        [UnityTest]
        public IEnumerator FirstRepeatedAndBurstCommandsHaveMeasuredCost(
            [ValueSource(nameof(Kinds))] string kind,
            [ValueSource(nameof(Distances))] bool far,
            [ValueSource(nameof(MemberCounts))] int members)
        {
            TestNavMeshBuilder.Flat(World, 1200);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            float distance = far ? 450 : 30;
            Assert.That(far ? distance >= 2 * agent.TargetDiscoveryRange : distance <= agent.TargetDiscoveryRange, Is.True);
            var positions = Enumerable.Range(0, members).Select(i => new Vector3(distance + 2 * i, 0, i % 2 * 2)).ToArray();
            var cluster = CreateCluster(kind, positions);
            yield return null;
            // Register the general query marker without querying the measured target or its resource cache.
            AgentNavigationQuery.Check(agent.NavMeshAgent, Vector3.right * 10, 0);
            Action empty = () => { };
            Measure(empty);
            var dispatcher = new AgentTargetCommandDispatcher();
            var samples = new List<Sample>();
            var requests = new List<AgentDirectiveRequest>();
            var resource = cluster as ResourceClusterAuthoring;
            for (int i = 0; i < 10; i++)
            {
                long resourceBefore = resource != null ? resource.NavigationPathCalculationCount : 0;
                if (i == 0) Assert.That(resourceBefore, Is.Zero, "First use must not inherit a queried resource cache.");
                bool accepted = false;
                AgentDirectiveRequest request = default;
                Action submit = () => accepted = dispatcher.TrySubmitClusterCommand(cluster, "1", out request);
                var sample = Measure(submit);
                sample.resourcePathCalculations = resource != null ? resource.NavigationPathCalculationCount - resourceBefore : 0;
                sample.kind = kind; sample.far = far; sample.members = members;
                sample.distance = distance; sample.discoveryRange = agent.TargetDiscoveryRange;
                sample.phase = i == 0 ? "FirstTargetUse" : i == 1 ? "SameTargetRepeat" : "Burst";
                sample.commandId = request.CommandId;
                Assert.That(accepted, Is.True);
                Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(request.CommandId));
                Assert.That(sample.navigationChecks, Is.GreaterThan(0));
                requests.Add(request); samples.Add(sample);
            }
            foreach (var sample in samples) CaseArtifactWriter.Trace("command-cost", JsonUtility.ToJson(sample));
            foreach (var old in requests.Take(9)) Assert.That(agent.FinishDirective(old.CommandId), Is.False);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(requests.Last().CommandId));
            yield return RuntimeWait.Until(() => agent.Position.x > 2, "measured latest command really moves", 5);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(requests.Last().CommandId));
            ContractCompleted = true;
        }
    }
}

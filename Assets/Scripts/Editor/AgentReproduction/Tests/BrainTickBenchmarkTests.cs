using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    /// <summary>大脑单次推进的耗时和托管分配基准，供行为树重构前后对比；只记录数值，不设阈值。</summary>
    public sealed class BrainTickBenchmarkTests : ReproductionTestFixture
    {
        private const int Warmup = 200;
        private const int Samples = 5000;

        public static string[] Scenarios = { "ExploreIdle", "SearchApproach", "CombatCooldown" };

        [UnityTest]
        public IEnumerator BrainTickCostIsRecorded([ValueSource(nameof(Scenarios))] string scenario)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Bench", Vector3.zero, 0, false, false);
            if (scenario == "SearchApproach") Submit(TargetFactory.Resources(World, new Vector3(40, 0, 0)), agent);
            if (scenario == "CombatCooldown") Submit(TargetFactory.Enemies(World, EnemyFactory.Passive(World, new Vector3(0, 0, 5))), agent);
            yield return null;
            Physics.SyncTransforms();

            var brain = (AgentBrainController)typeof(AgentPawnRoot).GetField("_brainController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent);
            double time = Time.timeAsDouble;
            for (int i = 0; i < Warmup; i++) brain.Tick(0.016f, time);
            string state = agent.CurrentMacroStateName;

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < Samples; i++) brain.Tick(0.016f, time);
            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            double microseconds = watch.Elapsed.TotalMilliseconds * 1000d / Samples;
            double bytes = (double)allocated / Samples;
            string line = $"scenario={scenario}; state={state}; samples={Samples}; usPerTick={microseconds:0.###}; bytesPerTick={bytes:0.#}";
            CaseArtifactWriter.Trace("brain-benchmark", line);
            UnityEngine.Debug.Log("[BrainBenchmark] " + line);
            ContractCompleted = true;
        }

        private static void Submit(Gameplay.Targets.Authoring.GameplayTargetClusterAuthoringBase target, AgentPawnRoot agent)
            => Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(target, agent.AgentIdValue, out _), Is.True);
    }
}

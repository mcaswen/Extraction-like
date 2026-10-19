using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Runtime;
using Gameplay.Agent.AI.Trees;
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
        private const int AllocationSamples = 500;

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

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < Samples; i++) brain.Tick(0.016f, time);
            watch.Stop();
            // 一帧内额外推进固定次数，与空闲帧的 GC 分配计数相减
            long allocated = ManagedAllocation.Unavailable;
            yield return ManagedAllocation.MeasureFrame(() => { for (int i = 0; i < AllocationSamples; i++) brain.Tick(0.016f, time); }, value => allocated = value);

            double microseconds = watch.Elapsed.TotalMilliseconds * 1000d / Samples;
            string bytes = allocated == ManagedAllocation.Unavailable ? "unavailable" : ((double)allocated / AllocationSamples).ToString("0.#");
            string line = $"scenario={scenario}; state={state}; samples={Samples}; usPerTick={microseconds:0.###}; bytesPerTick={bytes}";
            CaseArtifactWriter.Trace("brain-benchmark", line);
            UnityEngine.Debug.Log("[BrainBenchmark] " + line);
            ContractCompleted = true;
        }

        // 同一进程、同一智能体上交替推进新旧两棵树，抵消机器负载和单次运行的波动；每轮各推进一段，取每次耗时的中位数
        private const int Rounds = 12;
        private const int TicksPerRound = 500;

        [UnityTest]
        public IEnumerator RefactoredTreeCostComparedWithLegacy([Values("SearchApproach", "CombatCooldown")] string scenario)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Bench", Vector3.zero, 0, false, false);
            if (scenario == "SearchApproach") Submit(TargetFactory.Resources(World, new Vector3(40, 0, 0)), agent);
            else Submit(TargetFactory.Enemies(World, EnemyFactory.Passive(World, new Vector3(0, 0, 5))), agent);
            yield return null;
            Physics.SyncTransforms();

            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var brain = (AgentBrainController)typeof(AgentPawnRoot).GetField("_brainController", Private).GetValue(agent);
            double time = Time.timeAsDouble;
            for (int i = 0; i < Warmup; i++) brain.Tick(0.016f, time);
            var machine = (HierarchicalStateMachine)typeof(AgentBrainController).GetField("_stateMachine", Private).GetValue(brain);
            StateMachineState state = machine.CurrentLeafState;
            Assert.That(state.StateName, Is.EqualTo(scenario == "SearchApproach" ? "SearchResource" : "Combat"));

            FieldInfo runnerField = typeof(StateMachineState).GetField("_behaviorTreeRunner", Private);
            var refactored = (BehaviorTreeRunner)runnerField.GetValue(state);
            var treeContext = (BehaviorTreeContext)typeof(BehaviorTreeRunner).GetField("_context", Private).GetValue(refactored);
            var legacy = new BehaviorTreeRunner(scenario == "SearchApproach"
                ? AgentLegacyBrainTrees.SearchResource(AgentMacroStateId.SearchResource) : AgentLegacyBrainTrees.Combat(), treeContext);
            runnerField.SetValue(state, legacy);
            for (int i = 0; i < Warmup; i++) brain.Tick(0.016f, time);

            var perTick = new[] { new List<double>(), new List<double>() };
            var runners = new[] { legacy, refactored };
            var watch = new Stopwatch();
            for (int round = 0; round < Rounds; round++)
            {
                for (int variant = 0; variant < 2; variant++)
                {
                    runnerField.SetValue(state, runners[variant]);
                    watch.Restart();
                    for (int i = 0; i < TicksPerRound; i++) brain.Tick(0.016f, time);
                    watch.Stop();
                    perTick[variant].Add(watch.Elapsed.TotalMilliseconds * 1000d / TicksPerRound);
                }
            }

            var bytes = new long[2];
            for (int variant = 0; variant < 2; variant++)
            {
                runnerField.SetValue(state, runners[variant]);
                int index = variant;
                yield return ManagedAllocation.MeasureFrame(() => { for (int i = 0; i < AllocationSamples; i++) brain.Tick(0.016f, time); }, value => bytes[index] = value);
            }

            runnerField.SetValue(state, refactored);
            Assert.That(machine.CurrentLeafState, Is.SameAs(state), "对比期间不应发生状态转移");
            double legacyUs = Median(perTick[0]), refactoredUs = Median(perTick[1]);
            string line = $"scenario={scenario}; rounds={Rounds}x{TicksPerRound}; legacyUs={legacyUs:0.###}; refactoredUs={refactoredUs:0.###}; ratio={refactoredUs / legacyUs:0.###}; " +
                $"legacyBytes={Bytes(bytes[0])}; refactoredBytes={Bytes(bytes[1])}";
            CaseArtifactWriter.Trace("brain-benchmark-ab", line);
            UnityEngine.Debug.Log("[BrainBenchmarkAB] " + line);
            ContractCompleted = true;
        }

        private static double Median(List<double> values)
        {
            values.Sort();
            int middle = values.Count / 2;
            return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2d;
        }

        private static string Bytes(long allocated)
            => allocated == ManagedAllocation.Unavailable ? "unavailable" : ((double)allocated / AllocationSamples).ToString("0.#");

        private static void Submit(Gameplay.Targets.Authoring.GameplayTargetClusterAuthoringBase target, AgentPawnRoot agent)
            => Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(target, agent.AgentIdValue, out _), Is.True);
    }
}

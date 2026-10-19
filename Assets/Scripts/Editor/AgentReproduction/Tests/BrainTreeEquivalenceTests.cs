using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Brain;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Core.BehaviorTree.Nodes.Composites;
using Gameplay.Agent.AI.Factories;
using Gameplay.Agent.AI.Trees;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace AgentReproduction.Tests
{
    /// <summary>重构后的行为树与重构前逐帧副作用一致（差分对拍）。</summary>
    public sealed class BrainTreeEquivalenceTests : ReproductionTestFixture
    {
        public static string[] NavigateScenarios = { "ArriveAndFinish", "RetargetMidway", "InvalidTarget", "WrongKind", "WrongType", "NoDirective" };
        public static string[] InvestigateScenarios = { "ArriveAndHold", "WrongKind", "InvalidTarget" };

        private static AgentDirectiveRequest Move(string id, AgentTargetKind kind, Vector3 position, bool hasPosition = true)
            => new AgentDirectiveRequest(AgentDirectiveType.MoveTo, AgentTargetRef.FromAbstractPoint(kind, id + "-target", position, hasPosition), commandId: id);

        private static List<BrainStep> NavigateScript(string scenario, AgentTargetKind kind)
        {
            var steps = new List<BrainStep> { new BrainStep("place", 0, s => s.PlaceAgent(Vector3.zero)) };
            switch (scenario)
            {
                case "ArriveAndFinish":
                case "ArriveAndHold":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(Move("Nav-1", kind, new Vector3(10, 0, 0)))));
                    steps.Add(new BrainStep("halfway", 2, s => s.PlaceAgent(new Vector3(5, 0, 0))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(9, 0, 0))));
                    steps.Add(new BrainStep("cleared", 2, s => s.ClearDirective()));
                    break;
                case "RetargetMidway":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Move("Nav-1", kind, new Vector3(10, 0, 0)))));
                    steps.Add(new BrainStep("retarget", 2, s => s.SetDirective(Move("Nav-2", kind, new Vector3(-10, 0, 0)))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(-9, 0, 0))));
                    break;
                case "InvalidTarget":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(Move("Nav-1", kind, Vector3.zero, false))));
                    break;
                case "WrongKind":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(Move("Nav-1", kind == AgentTargetKind.Location ? AgentTargetKind.EnemySource : AgentTargetKind.Location, new Vector3(10, 0, 0)))));
                    break;
                case "WrongType":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(new AgentDirectiveRequest(AgentDirectiveType.Search,
                        AgentTargetRef.FromAbstractPoint(AgentTargetKind.Resource, "r", new Vector3(10, 0, 0)), commandId: "Search-1"))));
                    break;
                default:
                    steps.Add(new BrainStep("idle", 3));
                    break;
            }

            return steps;
        }

        private static BrainScenario AgentOnly(TestWorldBuilder world)
            => new BrainScenario { Pawn = AgentFactory.Create(world, "Equivalence", Vector3.zero, 0, false, false) };

        // 两个变体各自在全新的场景对象上运行；每个变体构建后先走一帧，让组件完成 Start
        private IEnumerator Compare(
            string label,
            System.Func<BehaviorTreeType> legacy,
            System.Func<BehaviorTreeType> refactored,
            System.Func<List<BrainStep>> script,
            System.Func<TestWorldBuilder, BrainScenario> setup,
            params string[] requiredLines)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var traces = new List<string>[2];
            for (int variant = 0; variant < 2; variant++)
            {
                using (var runWorld = new TestWorldBuilder())
                {
                    BrainScenario scenario = setup(runWorld);
                    yield return null;
                    Physics.SyncTransforms();
                    traces[variant] = BrainTreeHarness.Run(scenario.Pawn, variant == 0 ? legacy : refactored, script(), scenario.Probes);
                }
            }

            CaseArtifactWriter.Trace("equivalence", label + "\n" + string.Join("\n", traces[1]));
            string difference = BrainTreeHarness.FirstDifference(traces[0], traces[1]);
            Assert.That(difference, Is.Null, label + " 与重构前不一致：" + difference);
            foreach (string required in requiredLines)
                Assert.That(traces[0].Exists(line => line.Contains(required)), Is.True, label + " 脚本没有覆盖：" + required);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator NavigateTreeMatchesLegacy([ValueSource(nameof(NavigateScenarios))] string scenario)
        {
            string[] required = scenario == "ArriveAndFinish" ? new[] { "finish Nav-1 None matched=True" }
                : scenario == "InvalidTarget" ? new[] { "finish Nav-1 InvalidTarget matched=True" }
                : scenario == "RetargetMidway" ? new[] { "finish Nav-2 None matched=True" } : new string[0];
            yield return Compare("Navigate/" + scenario, AgentLegacyBrainTrees.Navigate, AgentBrainTrees.Navigate,
                () => NavigateScript(scenario, AgentTargetKind.Location), AgentOnly, required);
        }

        [UnityTest]
        public IEnumerator InvestigateEnemySourceTreeMatchesLegacy([ValueSource(nameof(InvestigateScenarios))] string scenario)
        {
            string[] required = scenario == "ArriveAndHold" ? new[] { "Arrived" }
                : scenario == "InvalidTarget" ? new[] { "finish Nav-1 InvalidTarget matched=True" } : new string[0];
            yield return Compare("InvestigateEnemySource/" + scenario, AgentLegacyBrainTrees.InvestigateEnemySource, AgentBrainTrees.InvestigateEnemySource,
                () => NavigateScript(scenario, AgentTargetKind.EnemySource), AgentOnly, required);
        }

        [UnityTest]
        public IEnumerator FactoryUsesRefactoredMoveTrees()
        {
            var factory = new AgentBrainStateFactory();
            Assert.That(factory.CreateNavigateState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateInvestigateEnemySourceState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            ContractCompleted = true;
            yield return null;
        }
    }
}

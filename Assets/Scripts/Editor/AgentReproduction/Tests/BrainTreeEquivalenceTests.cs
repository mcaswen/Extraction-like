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
        public static string[] ExtractionScenarios = { "ArriveLeaveReturn", "DirectPoint", "NoPointAtTarget", "WrongKind", "AbortWhileInside" };

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
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(9.9f, 0, 0))));
                    steps.Add(new BrainStep("cleared", 2, s => s.ClearDirective()));
                    break;
                case "RetargetMidway":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Move("Nav-1", kind, new Vector3(10, 0, 0)))));
                    steps.Add(new BrainStep("retarget", 2, s => s.SetDirective(Move("Nav-2", kind, new Vector3(-10, 0, 0)))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(-9.9f, 0, 0))));
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

        // 智能体的移动停止距离 0.25、交互距离来自真实配置；到达类步骤把智能体放在目标 0.2 米以内
        private static readonly Vector3 ExitPosition = new Vector3(10, 0, 0);

        // 撤离：智能体 + 一个撤离群（单个撤离点）+ RaidFlowController；探针读取撤离计时登记
        private static BrainScenario ExtractionWorld(TestWorldBuilder world)
        {
            var scenario = AgentOnly(world);
            TargetFactory.Extraction(world, ExitPosition);
            var raid = world.Root("Equivalence raid").AddComponent<RaidFlowController>();
            scenario.Probes.Add(() =>
            {
                var presence = RuntimeFixtureAccess.Read<IDictionary>(raid, "_activeExtractionProgressByAgentId");
                var entries = new List<string>();
                foreach (DictionaryEntry entry in presence)
                    entries.Add(entry.Key + "@" + BrainTrace.Format(RuntimeFixtureAccess.Read<ExtractionPointController>(entry.Value, "ExtractionPoint")));
                return "presence [" + string.Join(",", entries) + "]";
            });
            return scenario;
        }

        private static AgentDirectiveRequest Extract(string id, AgentTargetRef target)
            => new AgentDirectiveRequest(AgentDirectiveType.Extract, target, commandId: id);

        private static List<BrainStep> ExtractionScript(string scenario)
        {
            var steps = new List<BrainStep> { new BrainStep("place", 0, s => s.PlaceAgent(Vector3.zero)) };
            GameObject Cluster() => GameObject.Find("Test extraction cluster");
            GameObject Point() => GameObject.Find("Test extraction");
            switch (scenario)
            {
                case "ArriveLeaveReturn":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction, Cluster(), "exit")))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(ExitPosition + new Vector3(-0.15f, 0, 0))));
                    steps.Add(new BrainStep("leave", 2, s => s.PlaceAgent(new Vector3(5, 0, 0))));
                    steps.Add(new BrainStep("return", 2, s => s.PlaceAgent(ExitPosition + new Vector3(0, 0, 0.15f))));
                    steps.Add(new BrainStep("cleared", 2, s => s.ClearDirective()));
                    break;
                case "DirectPoint":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction, Point(), "point")))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(ExitPosition + new Vector3(-0.15f, 0, 0))));
                    break;
                case "NoPointAtTarget":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromAbstractPoint(AgentTargetKind.Extraction, "nowhere", new Vector3(-10, 0, 0))))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(-9.9f, 0, 0))));
                    break;
                case "WrongKind":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Location, Cluster(), "exit")))));
                    break;
                default:
                    steps.Add(new BrainStep("issue", 1, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction, Cluster(), "exit")))));
                    steps.Add(new BrainStep("arrive", 2, s => s.PlaceAgent(ExitPosition + new Vector3(-0.15f, 0, 0))));
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
        public IEnumerator ExtractionTreeMatchesLegacy([ValueSource(nameof(ExtractionScenarios))] string scenario)
        {
            string[] required = scenario == "ArriveLeaveReturn" ? new[] { "presence [Equivalence@Test extraction]", "presence []" }
                : scenario == "DirectPoint" ? new[] { "presence [Equivalence@Test extraction]" }
                : scenario == "NoPointAtTarget" ? new[] { "finish Exit-1 InvalidTarget matched=True" }
                : scenario == "AbortWhileInside" ? new[] { "presence [Equivalence@Test extraction]" } : new string[0];
            yield return Compare("Extraction/" + scenario, AgentLegacyBrainTrees.Extraction, AgentBrainTrees.Extraction,
                () => ExtractionScript(scenario), ExtractionWorld, required);
        }

        [UnityTest]
        public IEnumerator FactoryUsesRefactoredTrees()
        {
            var factory = new AgentBrainStateFactory();
            Assert.That(factory.CreateNavigateState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateInvestigateEnemySourceState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateExtractionState().BoundBehaviorTree.RootNode.GetChild(1), Is.TypeOf<ReactiveSequenceNode>());
            ContractCompleted = true;
            yield return null;
        }
    }
}

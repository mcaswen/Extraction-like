using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Brain;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Core.BehaviorTree.Nodes.Composites;
using Gameplay.Agent.AI.Factories;
using Gameplay.Agent.AI.Trees;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Runtime;
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
        public static string[] CombatScenarios = { "FireAndKill", "SkillThenShoot", "LostSightTimeout", "ManualApproachBudget", "Unreachable", "ShooterMissing", "NavigationNotReady", "TargetMissing", "CommandSwitch" };

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

        // 停止距离（移动 0.25）和交互距离（0）来自真实配置，到达容差至少 0.1；撤离到达步骤放在 0.05 米以内
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
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(ExitPosition + new Vector3(-0.05f, 0, 0))));
                    steps.Add(new BrainStep("leave", 2, s => s.PlaceAgent(new Vector3(5, 0, 0))));
                    steps.Add(new BrainStep("return", 2, s => s.PlaceAgent(ExitPosition + new Vector3(0, 0, 0.05f))));
                    steps.Add(new BrainStep("cleared", 2, s => s.ClearDirective()));
                    break;
                case "DirectPoint":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction, Point(), "point")))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(ExitPosition + new Vector3(-0.05f, 0, 0))));
                    break;
                case "NoPointAtTarget":
                    steps.Add(new BrainStep("issue", 2, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromAbstractPoint(AgentTargetKind.Extraction, "nowhere", new Vector3(-10, 0, 0))))));
                    steps.Add(new BrainStep("arrive", 3, s => s.PlaceAgent(new Vector3(-9.95f, 0, 0))));
                    break;
                case "WrongKind":
                    steps.Add(new BrainStep("issue", 3, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Location, Cluster(), "exit")))));
                    break;
                default:
                    steps.Add(new BrainStep("issue", 1, s => s.SetDirective(Extract("Exit-1", AgentTargetRef.FromConcreteObject(AgentTargetKind.Extraction, Cluster(), "exit")))));
                    steps.Add(new BrainStep("arrive", 2, s => s.PlaceAgent(ExitPosition + new Vector3(-0.05f, 0, 0))));
                    break;
            }

            return steps;
        }

        private static readonly Vector3 EnemyPosition = new Vector3(0, 0, 5);

        // 战斗：智能体 + 被动敌人（可选墙、悬空、缺射手、导航未就绪）；探针读取敌人血量、射手诊断和攻击锁定
        private static BrainScenario CombatWorld(TestWorldBuilder world, string scenario)
        {
            float moveSpeed = scenario == "ManualApproachBudget" ? 4f : 0f;
            var scenarioWorld = new BrainScenario { Pawn = AgentFactory.Create(world, "Equivalence", Vector3.zero, moveSpeed, false, scenario == "SkillThenShoot") };
            var pawn = scenarioWorld.Pawn;
            Vector3 enemyPosition = scenario == "Unreachable" ? new Vector3(0, 20, 0)
                : scenario == "ManualApproachBudget" ? new Vector3(0, 0, 10) : EnemyPosition;
            var enemy = EnemyFactory.Passive(world, enemyPosition);
            enemy.gameObject.name = "Equivalence enemy";
            TargetFactory.Enemies(world, enemy).gameObject.name = "Equivalence enemies";
            if (scenario == "LostSightTimeout" || scenario == "ManualApproachBudget" || scenario == "NavigationNotReady" || scenario == "CommandSwitch")
                world.Cube("Equivalence wall", new Vector3(0, 2, 2.5f), new Vector3(6, 5, 0.2f));
            // 射手是智能体根的必需组件，不能移除；禁用后即"未配置"
            if (scenario == "ShooterMissing") pawn.GetComponent<AgentCombatShooter>().enabled = false;
            var shooter = pawn.GetComponent<AgentCombatShooter>();
            var combat = pawn.GetComponent<AgentCombatController>();
            scenarioWorld.Probes.Add(() => "agent " + BrainTrace.Format(pawn.Position) + " onNav=" + pawn.NavMeshAgent.isOnNavMesh + " sight=" +
                Gameplay.Perception.TargetVisibilityQuery.Check(pawn.transform, Gameplay.Perception.CombatAimPointResolver.Resolve(pawn.transform), enemy.transform, 100f));
            scenarioWorld.Probes.Add(() => "enemy hp=" + enemy.GetCurrentHealthRatio().ToString("0.####") + " alive=" + (enemy.GetCurrentHealthRatio() > 0f));
            scenarioWorld.Probes.Add(() => shooter == null ? "shooter none" : "shooter " + shooter.LastShotResult + "/" + shooter.LastShotFailure);
            scenarioWorld.Probes.Add(() => combat == null ? "combat none" : "combat next=" + RuntimeFixtureAccess.Read<double>(combat, "_nextAttackTime").ToString("0.###"));
            return scenarioWorld;
        }

        private static AgentDirectiveRequest Engage(string id, AgentPawnRoot pawn, int priority = 0)
        {
            var enemy = GameObject.Find("Equivalence enemy");
            return AgentDirectiveRequest.EngageConcreteEnemy(enemy, "", pawn.AgentId, id, priority);
        }

        private static List<BrainStep> CombatScript(string scenario)
        {
            var steps = new List<BrainStep> { new BrainStep("place", 0, s => s.PlaceAgent(Vector3.zero)) };
            switch (scenario)
            {
                case "FireAndKill":
                case "SkillThenShoot":
                    steps.Add(new BrainStep("issue", 12, s => s.SetDirective(Engage("Combat-1", s.Pawn))));
                    steps.Add(new BrainStep("kill", 3, s => GameObject.Find("Equivalence enemy").GetComponent<EnemyHealthController>().TakeDamage(1000000f)));
                    break;
                case "LostSightTimeout":
                case "Unreachable":
                case "ShooterMissing":
                    steps.Add(new BrainStep("issue", 26, s => s.SetDirective(Engage("Combat-1", s.Pawn))));
                    break;
                case "ManualApproachBudget":
                    steps.Add(new BrainStep("issue", 30, s => s.SetDirective(Engage("ManualTargetClick-1", s.Pawn, AgentManualDirectiveLock.ManualDirectivePriority))));
                    steps.Add(new BrainStep("closer", 60, s => s.PlaceAgent(new Vector3(0, 0, 1))));
                    break;
                case "NavigationNotReady":
                    steps.Add(new BrainStep("issue", 3, s => { s.Pawn.NavMeshAgent.enabled = false; s.SetDirective(Engage("Combat-1", s.Pawn)); }));
                    steps.Add(new BrainStep("ready", 3, s => s.Pawn.NavMeshAgent.enabled = true));
                    break;
                case "TargetMissing":
                    steps.Add(new BrainStep("issue", 3, s =>
                    {
                        GameObject.Find("Equivalence enemy").GetComponent<EnemyHealthController>().TakeDamage(1000000f);
                        s.SetDirective(new AgentDirectiveRequest(AgentDirectiveType.Engage,
                            AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, GameObject.Find("Equivalence enemies"), "enemies"), commandId: "Combat-1"));
                    }));
                    break;
                case "CommandSwitch":
                    steps.Add(new BrainStep("first", 12, s => s.SetDirective(Engage("Combat-1", s.Pawn))));
                    steps.Add(new BrainStep("second", 26, s => s.SetDirective(Engage("Combat-2", s.Pawn))));
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
                var rootsBefore = new HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
                using (var runWorld = new TestWorldBuilder())
                {
                    BrainScenario scenario = setup(runWorld);
                    yield return null;
                    Physics.SyncTransforms();
                    traces[variant] = BrainTreeHarness.Run(scenario.Pawn, variant == 0 ? legacy : refactored, script(), scenario.Probes);
                }

                // 子弹、技能特效等运行中生成的对象不归场景构建方所有，也要清掉，不能飞进下一个变体
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!rootsBefore.Contains(root)) Object.DestroyImmediate(root);
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
        public IEnumerator CombatTreeMatchesLegacy([ValueSource(nameof(CombatScenarios))] string scenario)
        {
            string[] required;
            switch (scenario)
            {
                case "FireAndKill": required = new[] { "shooter Visible", "finish Combat-1 None matched=True" }; break;
                case "SkillThenShoot": required = new[] { "finish Combat-1 None matched=True" }; break;
                case "LostSightTimeout": required = new[] { "finish Combat-1 LostSight matched=True" }; break;
                case "ManualApproachBudget": required = new[] { "finish ManualTargetClick-1 LostSight matched=True" }; break;
                case "Unreachable": required = new[] { "finish Combat-1 Unreachable matched=True" }; break;
                case "ShooterMissing": required = new[] { "finish Combat-1 AttackUnavailable matched=True" }; break;
                case "NavigationNotReady": required = new[] { "move " }; break;
                case "TargetMissing": required = new[] { "finish Combat-1 None matched=True" }; break;
                default: required = new[] { "finish Combat-2 LostSight matched=True" }; break;
            }

            yield return Compare("Combat/" + scenario, AgentLegacyBrainTrees.Combat, AgentBrainTrees.Combat,
                () => CombatScript(scenario), world => CombatWorld(world, scenario), required);
        }

        [UnityTest]
        public IEnumerator FactoryUsesRefactoredTrees()
        {
            var factory = new AgentBrainStateFactory();
            Assert.That(factory.CreateNavigateState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateInvestigateEnemySourceState().BoundBehaviorTree.RootNode, Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateExtractionState().BoundBehaviorTree.RootNode.GetChild(1), Is.TypeOf<ReactiveSequenceNode>());
            Assert.That(factory.CreateCombatState().BoundBehaviorTree.RootNode, Is.TypeOf<Core.BehaviorTree.Nodes.Decorators.ScopeDecorator>());
            ContractCompleted = true;
            yield return null;
        }
    }
}

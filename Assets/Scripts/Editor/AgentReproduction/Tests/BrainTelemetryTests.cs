using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Core.StateMachine.Runtime;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Telemetry;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    /// <summary>大脑遥测只读：挂上与不挂时游戏结果一致，记录内容与实际计算一致，遥测代码不调用会改变状态的方法。</summary>
    public sealed class BrainTelemetryTests : ReproductionTestFixture
    {
        // 遥测、面板、叠加层只能读；这些调用会刷新群状态、推进修订号、改写黑板、指令或导航，周期性读取一律禁止
        private static readonly string[] ForbiddenInTelemetry =
        {
            "TryGetNearestReachableIncompleteResource", "HasPendingResources", "AppendNavigationDebugSnapshot",
            "GetOrCreate(", "TryGetFacts", "TryEstimateRoute", "TryCopyRouteNodeEnemies", ".CanShootAt(",
            "TryCollectExtractableItemsForAgent", "TryGetExtractionInventorySummary", "TryGetCarriedLootSummary", "RefreshTarget(",
            "SyncEquippedItemRuntimeDataFromGrid", ".SetValue(", ".RemoveValue(", "SetFact(", "Submit", "FinishDirective(",
            "ClearDirective(", "Cancel", "MoveDirective(", "StopDirectiveMovement(", "Warp(", "SetDestination(", ".destination =",
            "ResetPath(", "SetPath(", ".isStopped =", "Physics.SyncTransforms", "TrySetFocusedAgent", "TryFocusNextAgent",
            "TakeDamage(", "TakeCombatDamage(", "CalculatePath("
        };

        private static readonly string[] TelemetryFolders = { "Scripts/Gameplay/Agent/Telemetry" };

        private sealed class CapturingSink : IAgentDecisionRoundSink
        {
            public readonly List<(int index, AgentDecisionCandidate candidate, AgentCandidateVerdict verdict, AgentScoreBreakdown breakdown)> Rows =
                new List<(int, AgentDecisionCandidate, AgentCandidateVerdict, AgentScoreBreakdown)>();
            public int BestIndex = -2;
            public void BeginRound(double timeSeconds, in AgentDecisionContext context) { }
            public void BeginList(bool fallbackExits) { }
            public void RecordCandidate(int index, in AgentDecisionCandidate candidate, AgentCandidateVerdict verdict, in AgentScoreBreakdown breakdown)
                => Rows.Add((index, candidate, verdict, breakdown));
            public void EndList(int bestIndex) => BestIndex = bestIndex;
            public void RecordSkip(double timeSeconds, AgentDecisionSkipReason reason) { }
        }

        [UnityTest]
        public IEnumerator DecisionTelemetryMatchesScoringAndLeavesChoiceUnchanged([Values(1f, 0.1f)] float healthRatio)
        {
            var config = AssetDatabase.LoadAssetAtPath<AgentDecisionConfig>("Assets/SO/Agent/SO_Agent_DecisionConfig.asset");
            Assert.That(config, Is.Not.Null);
            var enemy = EnemyFactory.Passive(World, new Vector3(0, 0, 12));
            GameObject Target(string name, Vector3 position) { var go = World.Root(name); go.transform.position = position; return go; }
            var candidates = new List<AgentDecisionCandidate>
            {
                new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource, "near-resource",
                    Target("near resource", new Vector3(8, 0, 0)), new Vector3(8, 0, 0), 64f, null, false, true, 0.4f),
                new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource, "far-resource",
                    Target("far resource", new Vector3(60, 0, 0)), new Vector3(60, 0, 0), 3600f, null, true),
                new AgentDecisionCandidate(AgentDecisionTargetKind.ActiveEnemy, AgentDirectiveType.Engage, AgentTargetKind.Enemy, "enemy",
                    enemy.gameObject, enemy.transform.position, 144f, enemy, false),
                new AgentDecisionCandidate(AgentDecisionTargetKind.Extraction, AgentDirectiveType.Extract, AgentTargetKind.Extraction, "exit",
                    Target("exit", new Vector3(-90, 0, 0)), new Vector3(-90, 0, 0), 8100f, null, false, true, 1.2f),
                new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource, "missing",
                    null, Vector3.zero, 1f, null, false)
            };
            var context = new AgentDecisionContext(Vector3.zero, 10f, 100f * healthRatio, 100f, 0f, "far-resource",
                new List<EnemyHealthController> { enemy }, 4200);
            yield return null;

            var plain = new AgentTargetDecisionService(config);
            bool plainChose = plain.TryChooseTarget(context, candidates, out AgentDecisionResult plainResult);
            var observed = new AgentTargetDecisionService(config);
            var sink = new CapturingSink();
            observed.Telemetry = sink;
            bool observedChose = observed.TryChooseTarget(context, candidates, out AgentDecisionResult observedResult);

            Assert.That(observedChose, Is.EqualTo(plainChose));
            Assert.That(observedResult.Candidate.TargetId, Is.EqualTo(plainResult.Candidate.TargetId));
            Assert.That(observedResult.Score, Is.EqualTo(plainResult.Score));
            Assert.That(observedResult.Risk, Is.EqualTo(plainResult.Risk));
            Assert.That(observedResult.Reason, Is.EqualTo(plainResult.Reason));
            Assert.That(sink.Rows.Count, Is.EqualTo(candidates.Count), "每个候选都要有记录");

            foreach (var row in sink.Rows)
            {
                if (row.verdict == AgentCandidateVerdict.Invalid) { Assert.That(row.candidate.TargetId, Is.EqualTo("missing")); continue; }
                float risk = plain.RiskForTelemetryCheck(context, row.candidate);
                Assert.That(row.breakdown.Risk, Is.EqualTo(risk), row.candidate.TargetId + " 风险与评分不一致");
                if (row.verdict == AgentCandidateVerdict.NotBest)
                    Assert.That(row.breakdown.Total, Is.EqualTo(plain.ScoreForTelemetryCheck(context, row.candidate, risk)), row.candidate.TargetId + " 拆项总分与实际得分不一致");
            }

            Assert.That(sink.Rows.Find(r => r.candidate.TargetId == "enemy").verdict,
                Is.EqualTo(healthRatio < config.MinCombatHealthRatio ? AgentCandidateVerdict.LowHealthCombat : AgentCandidateVerdict.RiskOverThreshold));
            if (plainChose) Assert.That(candidates[sink.BestIndex].TargetId, Is.EqualTo(plainResult.Candidate.TargetId));
            CaseArtifactWriter.Trace("decision-telemetry", "health=" + healthRatio + "; chose=" + plainResult.Candidate.TargetId + "; score=" + plainResult.Score);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ApproachProbesDoNotChangeTheChosenPoint([Values("Open", "Walled", "Floating")] string layout)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Approach", Vector3.zero, 0, false, false);
            var enemy = EnemyFactory.Passive(World, layout == "Floating" ? new Vector3(0, 20, 0) : new Vector3(0, 0, 9));
            if (layout == "Walled") World.Cube("Approach wall", new Vector3(0, 2, 4.5f), new Vector3(6, 5, 0.2f));
            yield return null;
            Physics.SyncTransforms();
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.AttackRange, out float range);

            var plain = new AgentCombatApproachQuery.Buffer();
            bool plainResolved = AgentCombatApproachQuery.TryResolve(agent, enemy, range, plain, out Vector3 plainPoint);
            var logged = new AgentCombatApproachQuery.Buffer { Probes = new AgentApproachProbeLog() };
            bool loggedResolved = AgentCombatApproachQuery.TryResolve(agent, enemy, range, logged, out Vector3 loggedPoint);

            Assert.That(loggedResolved, Is.EqualTo(plainResolved));
            Assert.That(loggedPoint, Is.EqualTo(plainPoint));
            Assert.That(logged.CalculationCount, Is.EqualTo(plain.CalculationCount), "记录不得增加导航查询");
            Assert.That(logged.Probes.Count, Is.GreaterThan(0));
            if (loggedResolved) Assert.That(logged.Probes.GetPoint(logged.Probes.ChosenIndex), Is.EqualTo(loggedPoint));
            else Assert.That(logged.Probes.ChosenIndex, Is.EqualTo(-1));
            CaseArtifactWriter.Trace("approach-probes", layout + "; resolved=" + loggedResolved + "; probes=" + logged.Probes.Count + "; chosen=" + logged.Probes.ChosenIndex);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator HubRecordsTheLiveBrainAndDetachesCleanly()
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Telemetry", Vector3.zero, 6, false, false);
            var target = TargetFactory.Resources(World, new Vector3(12, 0, 0));
            var hub = AgentBrainTelemetryHub.Ensure();
            try
            {
                yield return null;
                var recorder = hub.Find(agent);
                Assert.That(recorder, Is.Not.Null);
                Assert.That(recorder.IsAttached, Is.True);
                Assert.That(recorder.EngageMemory, Is.Not.Null, "应找到交战树的执行记忆");
                Assert.That(recorder.SearchMemory, Is.Not.Null, "应找到搜索树的执行记忆");
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(target, agent.AgentIdValue, out _), Is.True);
                yield return RuntimeWait.Until(() => recorder.TransitionTotal > 0 && recorder.CurrentLeaf != null && recorder.CurrentLeaf.StateName == "SearchResource",
                    "recorder sees the transition into search", 5);
                yield return null;
                Assert.That(recorder.NodeEvents.TotalRecorded, Is.GreaterThan(0));
                Assert.That(recorder.EvaluatedCount, Is.GreaterThan(0), "应记录本帧状态机求值的出边");
                var last = recorder.GetTransition(recorder.TransitionCount - 1);
                Assert.That(last.To.StateName, Is.EqualTo("SearchResource"));
                Assert.That(last.Transition, Is.Not.Null);
                CaseArtifactWriter.Trace("telemetry-hub", "transitions=" + recorder.TransitionTotal + "; events=" + recorder.NodeEvents.TotalRecorded +
                    "; evaluated=" + recorder.EvaluatedCount + "; corners=" + recorder.PathCornerCount);

                Object.DestroyImmediate(hub.gameObject);
                Assert.That(recorder.IsAttached, Is.False);
                var brain = typeof(AgentPawnRoot).GetProperty("BrainController", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent);
                var machine = (HierarchicalStateMachine)brain.GetType().GetProperty("StateMachine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(brain);
                Assert.That(machine.Observer, Is.Null, "摘除后状态机不再有观察者");
                ContractCompleted = true;
            }
            finally
            {
                if (hub != null) Object.DestroyImmediate(hub.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator TelemetryCodeAvoidsStateChangingCalls()
        {
            var violations = new List<string>();
            foreach (string folder in TelemetryFolders)
            {
                string root = Path.Combine(Application.dataPath, folder);
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.StartsWith("//") || line.StartsWith("///")) continue;
                        foreach (string call in ForbiddenInTelemetry)
                            if (line.Contains(call)) violations.Add(Path.GetFileName(file) + ":" + (i + 1) + " " + call);
                    }
                }
            }

            Assert.That(violations, Is.Empty, "遥测代码调用了会改变状态的方法：\n" + string.Join("\n", violations));
            ContractCompleted = true;
            yield return null;
        }
    }
}

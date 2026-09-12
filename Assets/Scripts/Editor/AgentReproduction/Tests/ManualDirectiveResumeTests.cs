using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ManualDirectiveResumeTests : ReproductionTestFixture
    {
        public static string[] Kinds = { "Search", "Engage", "Extract" };
        public static int[] Speeds = { 1, 4 };
        public static string[] Endings = { "NewOrder", "RejectedOrder", "Cancel", "Death", "InvalidTarget", "FailedRetaliation" };

        private GameplayTargetClusterAuthoringBase Target(string kind, Vector3 position) => kind == "Search"
            ? TargetFactory.Resources(World, position) : kind == "Extract"
            ? TargetFactory.Extraction(World, position) : TargetFactory.Enemies(World, EnemyFactory.Passive(World, position));

        private static AgentDirectiveRequest Submit(GameplayTargetClusterAuthoringBase target, AgentPawnRoot agent)
        {
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(target, agent.AgentIdValue, out var request), Is.True);
            return request;
        }

        [UnityTest]
        public IEnumerator MovingManualTaskResumesAfterActualRetaliationDeath(
            [ValueSource(nameof(Kinds))] string kind, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Resume", Vector3.zero, 8, false, false);
            var target = Target(kind, new Vector3(45, 0, 0));
            yield return null;
            Time.timeScale = speed;
            var original = Submit(target, agent);
            var events = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = events.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                yield return RuntimeWait.Until(() => agent.Position.x > 2, "manual task moves", 5);
                var attacker = EnemyFactory.Passive(World, agent.Position + new Vector3(0, 0, 10));
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
                Assert.That(AgentManualDirectiveLock.IsCombatDamageDirective(agent.DirectiveLifecycle.Active.Value), Is.True);
                Assert.That(events.Count(e => e.Request.CommandId == original.CommandId && e.Stage == AgentDirectiveStage.Suspended), Is.EqualTo(1));
                var readModel = new SceneRaidReadModel(new SceneRaidIdentityMap(), _ => null);
                Assert.That(readModel.CaptureDirective(agent.DirectiveLifecycle.Active.Value).suspendedCommand,
                    Is.EqualTo(original.CommandId), "Runtime diagnostics must expose every suspended player task.");
                // 连续受击和自动选择均不能覆盖已挂起的玩家意图。
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(retaliation));
                yield return RuntimeWait.Until(() => agent.CurrentMacroStateId == AgentMacroStateId.Combat, "retaliation runs", 5);
                attacker.TakeDamage(100000);
                yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == original.CommandId, "same manual command resumes", 5);
                float resumedX = agent.Position.x;
                yield return RuntimeWait.Until(() => agent.Position.x > resumedX + 2, "resumed task actually moves", 5);
                Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(original.CommandId));
                Assert.That(agent.DirectiveLifecycle.Active?.TargetId, Is.EqualTo(original.TargetId));
                Assert.That(agent.NavMeshAgent.destination.x, Is.GreaterThan(25));
                Assert.That(events.Count(e => e.Request.CommandId == original.CommandId && e.Stage == AgentDirectiveStage.Resumed), Is.EqualTo(1));
                Assert.That(events.Any(e => e.Request.CommandId == original.CommandId &&
                    (e.Stage == AgentDirectiveStage.Cancelled || e.Stage == AgentDirectiveStage.Failed)), Is.False);
                Assert.That(agent.FinishDirective(retaliation), Is.False, "Late retaliation completion cannot finish the resumed task.");
                CaseArtifactWriter.Trace("manual-resume", $"kind={kind}; speed={speed}; original={original.CommandId}; active={agent.DirectiveLifecycle.Active?.CommandId}; position={agent.Position}; destination={agent.NavMeshAgent.destination}");
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator SuspendedPlayerTaskHonorsTerminalAndReplacementRules([ValueSource(nameof(Endings))] string ending)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Boundary", Vector3.zero, 0, false, false);
            var target = Target("Search", new Vector3(40, 0, 0));
            var original = Submit(target, agent);
            var attacker = EnemyFactory.Passive(World, new Vector3(0, 0, 10));
            var events = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = events.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
                AgentDirectiveRequest newer = default;
                if (ending == "NewOrder") newer = Submit(Target("Search", new Vector3(-40, 0, 0)), agent);
                if (ending == "RejectedOrder")
                    Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(null, agent.AgentIdValue, out _), Is.False);
                if (ending == "Cancel") agent.ClearDirective();
                if (ending == "Death") agent.TakeCombatDamage(1000000, agent.Position, Vector3.back, attacker.gameObject);
                if (ending == "InvalidTarget") target.gameObject.SetActive(false);
                if (ending == "FailedRetaliation") agent.FinishDirective(retaliation, AgentDirectiveFailure.LostSight);
                else { attacker.TakeDamage(100000); agent.DirectiveLifecycle.Tick(); }

                if (ending == "RejectedOrder" || ending == "FailedRetaliation")
                {
                    Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(original.CommandId));
                    Assert.That(events.Count(e => e.Request.CommandId == original.CommandId && e.Stage == AgentDirectiveStage.Resumed), Is.EqualTo(1));
                }
                else
                {
                    Assert.That(events.Any(e => e.Request.CommandId == original.CommandId && e.Stage == AgentDirectiveStage.Resumed), Is.False);
                    Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(ending == "NewOrder" ? newer.CommandId : null));
                    if (ending == "InvalidTarget")
                        Assert.That(events.Any(e => e.Request.CommandId == original.CommandId && e.Stage == AgentDirectiveStage.Failed &&
                            e.Reason == AgentDirectiveFailure.InvalidTarget), Is.True);
                }
                Assert.That(agent.FinishDirective(retaliation), Is.False);
                CaseArtifactWriter.Trace("resume-boundary", $"ending={ending}; original={original.CommandId}; active={agent.DirectiveLifecycle.Active?.CommandId}");
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoAgentsKeepIndependentPlayerTasks()
        {
            TestNavMeshBuilder.Flat(World, 160);
            var a = AgentFactory.Create(World, "A", Vector3.zero, 0, false, false);
            var b = AgentFactory.Create(World, "B", new Vector3(3, 0, 0), 0, false, false);
            var taskA = Submit(Target("Search", new Vector3(40, 0, 0)), a);
            var taskB = Submit(Target("Engage", new Vector3(-40, 0, 0)), b);
            var enemyA = EnemyFactory.Passive(World, new Vector3(0, 0, 10));
            var enemyB = EnemyFactory.Passive(World, new Vector3(5, 0, 10));
            a.TakeCombatDamage(10, a.Position, Vector3.back, enemyA.gameObject);
            b.TakeCombatDamage(10, b.Position, Vector3.back, enemyB.gameObject);
            string retaliationB = b.DirectiveLifecycle.Active.Value.CommandId;
            enemyA.TakeDamage(100000);
            a.DirectiveLifecycle.Tick();
            Assert.That(a.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(taskA.CommandId));
            Assert.That(b.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(retaliationB));
            enemyB.TakeDamage(100000);
            b.DirectiveLifecycle.Tick();
            Assert.That(b.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(taskB.CommandId));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedSuspendedTargetEndsSuccessfully([ValueSource(nameof(Kinds))] string kind)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Completed", Vector3.zero, 0, false, false);
            var target = Target(kind, new Vector3(40, 0, 0));
            var original = Submit(target, agent);
            var attacker = kind == "Engage" ? original.TargetObject.GetComponent<EnemyHealthController>()
                : EnemyFactory.Passive(World, new Vector3(0, 0, 10));
            var events = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = events.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                agent.TakeCombatDamage(10, agent.Position, Vector3.back, attacker.gameObject);
                if (kind == "Search") target.MarkCompleted();
                if (kind == "Extract") original.TargetObject.SetActive(false);
                attacker.TakeDamage(100000);
                agent.DirectiveLifecycle.Tick();
                Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
                var terminal = events.Last(e => e.Request.CommandId == original.CommandId);
                Assert.That(terminal.Stage, Is.EqualTo(kind == "Extract" ? AgentDirectiveStage.Failed : AgentDirectiveStage.Completed));
                if (kind == "Extract") Assert.That(terminal.Reason, Is.EqualTo(AgentDirectiveFailure.InvalidTarget));
                Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.False);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
            yield return null;
        }
    }
}

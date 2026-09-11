using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using Gameplay.Targets.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ClusterCommandTransitionTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static string[] Transitions = { "Search>Search", "Search>Engage", "Search>Extract",
            "Engage>Search", "Engage>Engage", "Engage>Extract", "Extract>Search", "Extract>Engage", "Extract>Extract" };
        public static string[] InvalidKinds = { "Null", "Disabled", "Completed", "Unreachable", "MissingAgent" };

        private GameplayTargetClusterAuthoringBase Target(string kind, Vector3 position) => kind == "Search"
            ? TargetFactory.Resources(World, position) : kind == "Extract"
            ? TargetFactory.Extraction(World, position) : TargetFactory.Enemies(World, EnemyFactory.Passive(World, position));

        private static AgentDirectiveRequest Submit(GameplayTargetClusterAuthoringBase cluster, string agent)
        {
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent, out var request), Is.True);
            return request;
        }

        [UnityTest]
        public IEnumerator MovingCommandReplacementIgnoresOldCompletion(
            [ValueSource(nameof(Transitions))] string transition, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            string[] types = transition.Split('>');
            var first = Target(types[0], new Vector3(40, 0, 0));
            var second = Target(types[1], new Vector3(-40, 0, 0));
            yield return null;
            Time.timeScale = speed;
            var old = Submit(first, "1");
            yield return RuntimeWait.Until(() => agent.Position.x > 2, "first command actual movement", 5);
            var results = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = results.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                var current = Submit(second, "1");
                Assert.That(agent.FinishDirective(old.CommandId), Is.False, "Injected obsolete completion must be ignored.");
                Assert.That(results.Count(x => x.Request.CommandId == old.CommandId && x.Stage == AgentDirectiveStage.Cancelled &&
                    x.Reason == AgentDirectiveFailure.Superseded), Is.EqualTo(1));
                yield return RuntimeWait.Until(() => agent.Position.x < -2, "replacement actual movement", 5);
                Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(current.CommandId));
                Assert.That(agent.NavMeshAgent.destination.x, Is.LessThan(-20));
                Assert.That(results.Any(x => x.Stage == AgentDirectiveStage.Failed || x.Stage == AgentDirectiveStage.Rejected), Is.False);
                CaseArtifactWriter.Trace("replacement-progress", transition + "; speed=" + speed + "; position=" + agent.Position +
                    "; old=" + old.CommandId + "; current=" + current.CommandId);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator RepeatedAndReturningCommandsKeepLatestIdentity([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8);
            var a = Target("Search", new Vector3(30, 0, 0));
            var b = Target("Search", new Vector3(-30, 0, 0));
            yield return null;
            Time.timeScale = speed;
            var old = new[] { Submit(a, "1"), Submit(a, "1"), Submit(b, "1") };
            var last = Submit(a, "1");
            Assert.That(old.Select(x => x.CommandId).Append(last.CommandId).Distinct().Count(), Is.EqualTo(4));
            foreach (var request in old) Assert.That(agent.FinishDirective(request.CommandId), Is.False);
            yield return RuntimeWait.Until(() => agent.Position.x > 3, "repeated command progress", 5);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(last.CommandId));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator FocusedAndExplicitRoutingKeepOtherAgentTask()
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", Vector3.zero, 8);
            var second = AgentFactory.Create(World, "2", new Vector3(0, 0, 8), 8);
            var a = Target("Search", new Vector3(30, 0, 0));
            var b = Target("Extract", new Vector3(-30, 0, 8));
            yield return null;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            Assert.That(registry.TrySetFocusedAgent("1"), Is.True);
            var firstRequest = Submit(a, "");
            Assert.That(firstRequest.TargetAgentId.Value, Is.EqualTo("1"));
            Assert.That(second.DirectiveLifecycle.Active.HasValue, Is.False);
            Assert.That(registry.TrySetFocusedAgent("2"), Is.True);
            var secondRequest = Submit(b, "");
            Assert.That(secondRequest.TargetAgentId.Value, Is.EqualTo("2"));
            Assert.That(first.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(firstRequest.CommandId));
            var explicitRequest = Submit(b, "1");
            Assert.That(explicitRequest.TargetAgentId.Value, Is.EqualTo("1"));
            Assert.That(second.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(secondRequest.CommandId));
            yield return RuntimeWait.Until(() => first.Position.x < -2 && second.Position.x < -2, "both routed commands move", 5);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator InvalidCommandPreservesActiveTask([ValueSource(nameof(InvalidKinds))] string kind)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8);
            var current = Submit(Target("Search", new Vector3(30, 0, 0)), "1");
            var invalid = kind == "Null" ? null : Target("Extract", new Vector3(kind == "Unreachable" ? 300 : -20, 0, 0));
            if (kind == "Disabled") invalid.enabled = false;
            if (kind == "Completed") invalid.MarkCompleted();
            var results = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = results.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(invalid, kind == "MissingAgent" ? "absent" : "1", out _), Is.False);
                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results[0].Stage, Is.EqualTo(AgentDirectiveStage.Rejected));
                var reason = kind == "Completed" ? AgentDirectiveFailure.TargetCompleted : kind == "Unreachable"
                    ? AgentDirectiveFailure.Unreachable : kind == "MissingAgent" ? AgentDirectiveFailure.NoAgent : AgentDirectiveFailure.InvalidTarget;
                Assert.That(results[0].Reason, Is.EqualTo(reason));
                Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(current.CommandId));
                yield return RuntimeWait.Until(() => agent.Position.x > 3, "old task survives rejection", 5);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator DeadExplicitAgentDoesNotRerouteToLivingFocus()
        {
            TestNavMeshBuilder.Flat(World);
            var dead = AgentFactory.Create(World, "1", Vector3.zero);
            var living = AgentFactory.Create(World, "2", new Vector3(0, 0, 8), 8);
            var target = Target("Search", new Vector3(30, 0, 8));
            var aliveRequest = Submit(target, "2");
            Assert.That(AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2"), Is.True);
            dead.TakeCombatDamage(1000000, dead.Position, Vector3.left, null);
            Assert.That(dead.IsDead, Is.True);
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(target, "1", out _), Is.False);
            Assert.That(living.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(aliveRequest.CommandId));
            yield return RuntimeWait.Until(() => living.Position.x > 3, "living command unaffected", 5);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator SharedEnemyDeathReleasesBothCommands()
        {
            TestNavMeshBuilder.Flat(World);
            var first = AgentFactory.Create(World, "1", new Vector3(0, 0, -3), 8, false, false);
            var second = AgentFactory.Create(World, "2", new Vector3(0, 0, 3), 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(14, 0, 0), 80);
            var cluster = TargetFactory.Enemies(World, enemy);
            yield return null;
            Time.timeScale = 4;
            var events = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = events.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                var a = Submit(cluster, "1"); var b = Submit(cluster, "2");
                yield return RuntimeWait.Until(() => enemy == null || !enemy.IsAlive, "real shared enemy kill", 10);
                yield return RuntimeWait.Until(() => !first.DirectiveLifecycle.Active.HasValue && !second.DirectiveLifecycle.Active.HasValue,
                    "both shared tasks complete", 3);
                foreach (var request in new[] { a, b })
                {
                    Assert.That(events.Count(x => x.Request.CommandId == request.CommandId && x.Stage == AgentDirectiveStage.Completed), Is.EqualTo(1));
                    Assert.That(events.Any(x => x.Request.CommandId == request.CommandId && x.Stage == AgentDirectiveStage.Failed), Is.False);
                }
                Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(first), Is.False);
                Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(second), Is.False);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator ResourceCommandDuringRetaliationDiscardsOldExtraction()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var original = Submit(Target("Extract", new Vector3(30, 0, 0)), "1");
            var enemy = EnemyFactory.Passive(World, new Vector3(14, 0, 0));
            agent.TakeCombatDamage(10, agent.Position, Vector3.right, enemy.gameObject);
            var retaliation = agent.DirectiveLifecycle.Active.Value;
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction?.CommandId, Is.EqualTo(original.CommandId));
            var current = Submit(Target("Search", new Vector3(-30, 0, 0)), "1");
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue, Is.False);
            Assert.That(agent.FinishDirective(retaliation.CommandId), Is.False);
            Assert.That(agent.FinishDirective(original.CommandId), Is.False);
            yield return RuntimeWait.Until(() => agent.Position.x < -3, "resource replaces retaliation", 5);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(current.CommandId));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator EightCommandBurstIsBoundedAndLatestMoves([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8);
            var a = Target("Search", new Vector3(30, 0, 0)); var b = Target("Search", new Vector3(-30, 0, 0));
            AgentCommandFeedbackInstaller.EnsureInstalled();
            var presenter = UnityEngine.Object.FindObjectOfType<AgentCommandFeedbackPresenter>();
            yield return null;
            Time.timeScale = speed;
            var requests = new List<AgentDirectiveRequest>();
            for (int i = 0; i < 8; i++) requests.Add(Submit(i % 2 == 0 ? a : b, "1"));
            var pending = RuntimeFixtureAccess.Read<Queue<AgentDirectiveResult>>(presenter, "_pending");
            Assert.That(pending.Count, Is.LessThanOrEqualTo(4));
            foreach (var request in requests.Take(7)) Assert.That(agent.FinishDirective(request.CommandId), Is.False);
            yield return RuntimeWait.Until(() => agent.Position.x < -3, "burst latest command progresses", 5);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(requests.Last().CommandId));
            presenter.enabled = false;
            Assert.That(pending, Is.Empty);
            Submit(a, "1");
            Assert.That(pending, Is.Empty, "Disabled presenter must unsubscribe.");
            ContractCompleted = true;
        }
    }
}

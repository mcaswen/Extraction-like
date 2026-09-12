using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class AgentRouteLifecycleTests : ReproductionTestFixture
    {
        private static AgentDirectiveRequest Move(string root, long version = 1, bool player = false)
            => new AgentDirectiveRequest(AgentDirectiveType.MoveTo,
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Location, "node", new Vector3(-15, 0, 0)),
                "payload-kept", commandId: root + "_move", routeContext: new AgentDirectiveRouteContext(root, version, "node", 2, player));

        [UnityTest]
        public IEnumerator AutonomousRouteStepResumesAfterEffectiveDamage()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "route-auto", Vector3.zero);
            var enemy = EnemyFactory.Passive(World, new Vector3(20, 0, 0));
            var initial = agent.DirectiveLifecycle.SubmitRouteStep(Move("route-a"));
            Assert.That(initial.Accepted, Is.True);
            Assert.That(initial.Request.TargetAgentId, Is.EqualTo(agent.AgentId));
            Assert.That(initial.Request.PayloadId, Is.EqualTo("payload-kept"));
            var events = new List<AgentDirectiveResult>();
            agent.DirectiveLifecycle.ResultPublished += events.Add;
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject);
            string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.Value.RouteContext.Equals(initial.Request.RouteContext), Is.True);
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject);
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(retaliation));
            Assert.That(agent.FinishDirective(retaliation), Is.True);
            Assert.That(agent.DirectiveLifecycle.Active.Value.RouteContext.Equals(initial.Request.RouteContext), Is.True);
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.False);
            Assert.That(events.FindAll(x => x.Stage == AgentDirectiveStage.Suspended).Count, Is.EqualTo(1));
            Assert.That(events.FindAll(x => x.Stage == AgentDirectiveStage.Resumed).Count, Is.EqualTo(1));
            agent.DirectiveLifecycle.ResultPublished -= events.Add;
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator RejectedReplacementPreservesActiveRetaliationAndSuspendedRoute()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "route-reject", Vector3.zero);
            var enemy = EnemyFactory.Passive(World, new Vector3(20, 0, 0));
            var accepted = agent.DirectiveLifecycle.SubmitRouteStep(Move("player-a", player: true));
            Assert.That(accepted.Accepted, Is.True, accepted.Reason.ToString());
            var original = accepted.Request;
            Assert.That(AgentManualDirectiveLock.IsManualDirective(original), Is.True, "玩家身份不依赖旧点击命令前缀");
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject);
            string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
            var invalid = Move("player-b", 2, true).WithTargetRef(default);
            Assert.That(agent.DirectiveLifecycle.SubmitRouteStep(invalid).Accepted, Is.False);
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(retaliation));
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.Value.CommandId, Is.EqualTo(original.CommandId));
            agent.FinishDirective(retaliation);
            Assert.That(agent.DirectiveLifecycle.Active.Value.RouteContext.Equals(original.RouteContext), Is.True);
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator AcceptedReplacementDiscardsOldRouteAndObsoleteCallbacks()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "route-replace", Vector3.zero);
            var enemy = EnemyFactory.Passive(World, new Vector3(20, 0, 0));
            var accepted = agent.DirectiveLifecycle.SubmitRouteStep(Move("a"));
            Assert.That(accepted.Accepted, Is.True, accepted.Reason.ToString());
            var old = accepted.Request;
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject);
            string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
            var newer = agent.DirectiveLifecycle.SubmitRouteStep(Move("b", 2));
            Assert.That(newer.Accepted, Is.True);
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.False);
            Assert.That(agent.FinishDirective(retaliation), Is.False);
            Assert.That(agent.FinishDirective(old.CommandId), Is.False);
            Assert.That(agent.DirectiveLifecycle.Active.Value.RouteContext.RootRequestId, Is.EqualTo("b"));
            // 相同物理目标属于另一根任务时不能沿用旧 CommandId。
            var latest = agent.DirectiveLifecycle.SubmitRouteStep(Move("c", 3));
            Assert.That(latest.Request.CommandId, Is.EqualTo("c_move"));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator CompletedSuspendedEnemyRetainsStepIdentityInCompletion()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "route-enemy", Vector3.zero);
            var target = EnemyFactory.Passive(World, new Vector3(15, 0, 0));
            var attacker = EnemyFactory.Passive(World, new Vector3(20, 0, 0));
            var context = new AgentDirectiveRouteContext("enemy-route", 1, "enemy-cluster", 1, false);
            var request = AgentDirectiveRequest.EngageConcreteEnemy(target.gameObject, commandId: "member-one").WithRouteContext(context);
            Assert.That(agent.DirectiveLifecycle.SubmitRouteStep(request).Accepted, Is.True);
            var results = new List<AgentDirectiveResult>(); agent.DirectiveLifecycle.ResultPublished += results.Add;
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, attacker.gameObject);
            string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
            Object.DestroyImmediate(target.gameObject);
            agent.FinishDirective(retaliation);
            var terminal = results.FindAll(x => x.Request.CommandId == "member-one" && x.Stage == AgentDirectiveStage.Completed);
            Assert.That(terminal.Count, Is.EqualTo(1)); Assert.That(terminal[0].Request.RouteContext.Equals(context), Is.True);
            Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.False);
            agent.DirectiveLifecycle.ResultPublished -= results.Add;
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator RouteProtectionAndInstanceEventsStayWithinOneAgent()
        {
            TestNavMeshBuilder.Flat(World);
            var a = AgentFactory.Create(World, "route-a", Vector3.zero);
            var b = AgentFactory.Create(World, "route-b", new Vector3(0, 0, 5));
            var eventsA = new List<AgentDirectiveResult>(); var eventsB = new List<AgentDirectiveResult>();
            a.DirectiveLifecycle.ResultPublished += eventsA.Add; b.DirectiveLifecycle.ResultPublished += eventsB.Add;
            var request = Move("root", player: true);
            var copy = request.WithTargetAgentId(a.AgentId).WithTargetRef(request.TargetRef);
            Assert.That(copy.RouteContext.Equals(request.RouteContext), Is.True);
            Assert.That(a.DirectiveLifecycle.SubmitRouteStep(copy).Accepted, Is.True);
            Assert.That(eventsB, Is.Empty);
            var unscoped = Move("ordinary").WithRouteContext(default);
            Assert.That(a.TrySubmitDirective(unscoped).Reason, Is.EqualTo(AgentDirectiveFailure.Superseded));
            Assert.That(a.DirectiveLifecycle.SubmitRouteStep(unscoped).Reason, Is.EqualTo(AgentDirectiveFailure.InvalidTarget));
            Assert.That(b.DirectiveLifecycle.SubmitRouteStep(Move("other")).Accepted, Is.True);
            Assert.That(eventsA.TrueForAll(x => x.Request.TargetAgentId == a.AgentId), Is.True);
            Assert.That(eventsB.Count, Is.EqualTo(1));
            a.DirectiveLifecycle.ResultPublished -= eventsA.Add; b.DirectiveLifecycle.ResultPublished -= eventsB.Add;
            ContractCompleted = true; yield break;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class AgentRouteMovementTests : ReproductionTestFixture
    {
        private static AgentDirectiveRequest Move(string root, Vector3 destination, long version = 1)
            => new AgentDirectiveRequest(AgentDirectiveType.MoveTo,
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Location, "node", destination), commandId: root + "_move",
                routeContext: new AgentDirectiveRouteContext(root, version, "node", 0, true));

        [UnityTest]
        public IEnumerator LocationDirectiveActuallyMovesAndCompletesOnceAtTheAnchor()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale = 4;
            var agent = AgentFactory.Create(World, "route-move", Vector3.zero, moveSpeed: 8, skills: false);
            Vector3 start = agent.Position, destination = new Vector3(12, 0, 0);
            var results = new List<AgentDirectiveResult>(); agent.DirectiveLifecycle.ResultPublished += results.Add;
            var accepted = agent.DirectiveLifecycle.SubmitRouteStep(Move("route", destination));
            Assert.That(accepted.Accepted, Is.True, accepted.Reason.ToString());
            yield return RuntimeWait.Until(() => !agent.DirectiveLifecycle.Active.HasValue, "移动到群锚点");
            Assert.That(agent.Position.x - start.x, Is.GreaterThan(9));
            Assert.That(AgentNavigationQuery.Check(agent.NavMeshAgent, destination, 2).Status, Is.EqualTo(AgentNavigationStatus.Arrived));
            var completed = results.FindAll(x => x.Stage == AgentDirectiveStage.Completed);
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(completed[0].Request.RouteContext.Equals(accepted.Request.RouteContext), Is.True);
            yield return null; yield return null;
            Assert.That(results.FindAll(x => x.Stage == AgentDirectiveStage.Completed).Count, Is.EqualTo(1));
            CaseArtifactWriter.Trace("anchor-arrived", "start="+start.ToString("R")+"; final="+agent.Position.ToString("R")+"; anchor="+destination);
            agent.DirectiveLifecycle.ResultPublished -= results.Add;
            ContractCompleted = true;
        }

        [UnityTest] public IEnumerator TravelRetaliationResumesAtNormalSpeed() { yield return Retaliation(1); }
        [UnityTest] public IEnumerator TravelRetaliationResumesAtFourTimesSpeed() { yield return Retaliation(4); }

        private IEnumerator Retaliation(float speed)
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale = speed;
            var agent = AgentFactory.Create(World, "route-retaliation", Vector3.zero, moveSpeed: 8, skills: false);
            Vector3 destination = new Vector3(20, 0, 0);
            var request = Move("resume-route", destination);
            Assert.That(agent.DirectiveLifecycle.SubmitRouteStep(request).Accepted, Is.True);
            yield return RuntimeWait.Until(() => agent.Position.x > 3 && agent.CurrentMacroStateId == AgentMacroStateId.Navigate, "真实行进");
            var enemy = EnemyFactory.Passive(World, agent.Position + new Vector3(0, 0, 12));
            agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject);
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.Value.CommandId, Is.EqualTo(request.CommandId));
            yield return RuntimeWait.Until(() => agent.CurrentMacroStateId == AgentMacroStateId.Combat, "进入反击");
            // 构造外部清除攻击者，真实生命周期负责完成反击并恢复，测试不重新发原路线。
            Object.DestroyImmediate(enemy.gameObject);
            yield return RuntimeWait.Until(() => agent.CurrentMacroStateId == AgentMacroStateId.Navigate, "恢复行进");
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(request.CommandId));
            yield return RuntimeWait.Until(() => !agent.DirectiveLifecycle.Active.HasValue, "恢复后到达原锚点");
            Assert.That(AgentNavigationQuery.Check(agent.NavMeshAgent, destination, 2).Status, Is.EqualTo(AgentNavigationStatus.Arrived));
            Assert.That(agent.DirectiveLifecycle.SuspendedDirective.HasValue, Is.False);
            CaseArtifactWriter.Trace("retaliation-arrival", "scale="+speed+"; final="+agent.Position.ToString("R"));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ReplacementWithinNavigateUsesTheNewDestinationAndIdentity()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale = 4;
            var agent = AgentFactory.Create(World, "route-turn", Vector3.zero, moveSpeed: 8, skills: false);
            var old = Move("old", new Vector3(20,0,0)); var newer = Move("new", new Vector3(-12,0,0), 2);
            var results = new List<AgentDirectiveResult>(); agent.DirectiveLifecycle.ResultPublished += results.Add;
            Assert.That(agent.DirectiveLifecycle.SubmitRouteStep(old).Accepted, Is.True);
            yield return RuntimeWait.Until(() => agent.Position.x > 3, "旧目标方向真实行进");
            Assert.That(agent.DirectiveLifecycle.SubmitRouteStep(newer).Accepted, Is.True);
            Assert.That(agent.FinishDirective(old.CommandId), Is.False);
            yield return RuntimeWait.Until(() => !agent.DirectiveLifecycle.Active.HasValue, "改令后到达新目标");
            Assert.That(agent.Position.x, Is.LessThan(-9));
            Assert.That(results.FindAll(x => x.Request.CommandId == old.CommandId && x.Stage == AgentDirectiveStage.Completed), Is.Empty);
            Assert.That(results.FindAll(x => x.Request.CommandId == newer.CommandId && x.Stage == AgentDirectiveStage.Completed).Count, Is.EqualTo(1));
            agent.DirectiveLifecycle.ResultPublished -= results.Add;
            ContractCompleted = true;
        }
    }
}

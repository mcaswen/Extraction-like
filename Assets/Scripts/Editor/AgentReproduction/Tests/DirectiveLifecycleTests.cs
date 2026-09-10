using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class DirectiveLifecycleTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator InvalidSuspendedExtractionFailsAfterRetaliation()
        {
            var agent=Create(out var original);
            var enemy=EnemyFactory.Passive(World,new Vector3(20,0,0));
            AgentDirectiveResult? failed=null;
            System.Action<AgentDirectiveResult> observe=result=>
            {
                if(result.Request.CommandId==original.CommandId && result.Stage==AgentDirectiveStage.Failed) failed=result;
            };
            AgentDirectiveFeedbackChannel.Published+=observe;
            try
            {
                agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.True);
                original.TargetObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
                yield return RuntimeWait.Until(()=>failed.HasValue,"invalid extraction resume failure");
                Assert.That(failed.Value.Reason,Is.EqualTo(AgentDirectiveFailure.InvalidTarget));
                Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False);
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.False);
                ContractCompleted=true;
            }
            finally { AgentDirectiveFeedbackChannel.Published-=observe; }
        }
        private AgentPawnRoot Create(out AgentDirectiveRequest extraction)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Lifecycle", Vector3.zero);
            var exit = TargetFactory.Extraction(World, new Vector3(-20,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit, agent.AgentIdValue, out extraction), Is.True);
            return agent;
        }

        [UnityTest]
        public IEnumerator RepeatedDamagePreservesOneExtractionAndOldCompletionCannotClearNewOrder()
        {
            var agent = Create(out var original);
            var enemy = EnemyFactory.Passive(World, new Vector3(20,0,0));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            string retaliation = agent.DirectiveLifecycle.Active.Value.CommandId;
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.Value.CommandId, Is.EqualTo(original.CommandId));
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(retaliation));
            var newerExit = TargetFactory.Extraction(World, new Vector3(-10,0,10));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(newerExit,agent.AgentIdValue,out var newer),Is.True);
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.False);
            Assert.That(agent.FinishDirective(retaliation),Is.False);
            yield return null;
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(newer.CommandId));
            CaseArtifactWriter.Trace("contract-completed", "Repeated damage preserved one extraction; obsolete callback ignored.");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator CancelAndDeathDiscardSuspendedExtraction()
        {
            var agent = Create(out _);
            var enemy = EnemyFactory.Passive(World,new Vector3(20,0,0));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            agent.ClearDirective();
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False);
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.False);
            var exit=TargetFactory.Extraction(World,new Vector3(-10,0,0));
            new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out _);
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            agent.TakeCombatDamage(1000000,agent.Position,Vector3.left,enemy.gameObject);
            yield return null;
            Assert.That(agent.IsDead,Is.True);
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False);
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.False);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ManualResourceIgnoresSightButEffectiveDamageInterrupts()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"F3",Vector3.zero);
            var resource=World.Root("Resource"); resource.transform.position=new Vector3(-20,0,0);
            var manual=AgentDirectiveRequest.SearchConcreteResource(resource,"resource",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("resource"),1000);
            Assert.That(agent.TrySubmitDirective(manual).Accepted,Is.True);
            var enemy=EnemyFactory.Passive(World,new Vector3(20,0,0));
            agent.SetVisibleEnemy(true);
            for(int i=0;i<4;i++) yield return null;
            Assert.That(agent.CurrentMacroStateId,Is.EqualTo(AgentMacroStateId.SearchResource));
            agent.TakeCombatDamage(0,agent.Position,Vector3.left,enemy.gameObject);
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(manual.CommandId));
            agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject);
            yield return RuntimeWait.Until(()=>agent.CurrentMacroStateId==AgentMacroStateId.Combat,"resource damage interrupt");
            Assert.That(agent.DirectiveLifecycle.Active.Value.DirectiveType,Is.EqualTo(AgentDirectiveType.Engage));
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue,Is.False);
            ContractCompleted=true;
        }
    }
}

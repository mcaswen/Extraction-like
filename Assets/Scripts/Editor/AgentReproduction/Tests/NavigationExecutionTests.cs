using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class NavigationExecutionTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator ZeroStoppingDistanceArrivesAndNavigationLossTerminates()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Nav",Vector3.zero,4f);
            var exit=TargetFactory.Extraction(World,new Vector3(1,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out var order),Is.True);
            yield return RuntimeWait.Until(()=>AgentNavigationQuery.Check(agent.NavMeshAgent,new Vector3(1,0,0),0).Status==AgentNavigationStatus.Arrived,"zero stopping distance arrival");
            Assert.That(Vector3.Distance(agent.NavMeshAgent.nextPosition,new Vector3(1,agent.NavMeshAgent.nextPosition.y,0)),Is.LessThan(0.13f));
            var failures=new List<AgentDirectiveResult>();
            AgentDirectiveFeedbackChannel.Published+=failures.Add;
            NavMesh.RemoveAllNavMeshData();
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"navigation unavailable terminal failure",8);
            AgentDirectiveFeedbackChannel.Published-=failures.Add;
            Assert.That(failures.Exists(x=>x.Request.CommandId==order.CommandId && x.Stage==AgentDirectiveStage.Failed && x.Reason==AgentDirectiveFailure.Unreachable),Is.True);
            Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(agent),Is.False);
            CaseArtifactWriter.Trace("contract-completed","Zero tolerance arrived; navigation loss reported and lock released.");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator NoProgressFailsAndRejectedReplacementPreservesCurrentTask()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Stalled",Vector3.zero,0f);
            var exit=TargetFactory.Extraction(World,new Vector3(10,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out var first),Is.True);
            var invalid=TargetFactory.Extraction(World,new Vector3(200,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(invalid,agent.AgentIdValue,out _),Is.False);
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(first.CommandId));
            var failures=new List<AgentDirectiveResult>(); AgentDirectiveFeedbackChannel.Published+=failures.Add;
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"no progress timeout",8);
            AgentDirectiveFeedbackChannel.Published-=failures.Add;
            Assert.That(failures.Exists(x=>x.Reason==AgentDirectiveFailure.NoProgress),Is.True);
            ContractCompleted=true;
        }
    }
}

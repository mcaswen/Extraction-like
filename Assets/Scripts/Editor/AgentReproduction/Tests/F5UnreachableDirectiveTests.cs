using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class F5UnreachableDirectiveTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator DisconnectedExtractionIsRejected()
        {
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(0,-0.1f,0),new Vector3(20,0.2f,20)),new Bounds(new Vector3(40,-0.1f,0),new Vector3(20,0.2f,20)));
            var agent=AgentFactory.Create(World,"F5",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(40,0,0));
            Assert.That(NavMesh.SamplePosition(new Vector3(40,0,0),out NavMeshHit destination,1,NavMesh.AllAreas),Is.True);
            var path=new NavMeshPath();
            agent.NavMeshAgent.CalculatePath(destination.position,path);
            Assert.That(path.status,Is.Not.EqualTo(NavMeshPathStatus.PathComplete),"Fixture requires a disconnected path.");
            bool accepted=new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out AgentDirectiveRequest request);
            CaseArtifactWriter.Trace("unreachable",$"path={path.status}; accepted={accepted}; command={request.CommandId}");
            Assert.That(accepted,Is.False,"An impossible extraction command must be rejected.");
            ContractCompleted=true;
            yield return null;
        }
    }
}

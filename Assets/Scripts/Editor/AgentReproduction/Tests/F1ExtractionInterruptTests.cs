using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class F1ExtractionInterruptTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator ExtractionResumesAfterRetaliation()
        {
            TestNavMeshBuilder.Flat(World);
            AgentPawnRoot agent=AgentFactory.Create(World,"F1",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(-20,0,0));
            EnemyHealthController enemy=EnemyFactory.Passive(World,new Vector3(20,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,"F1",out AgentDirectiveRequest original),Is.True);
            yield return RuntimeWait.Until(()=>agent.CurrentMacroStateId==AgentMacroStateId.Extraction,"initial extraction");
            Assert.That(agent.TakeCombatDamage(10,agent.Position,Vector3.left,enemy.gameObject),Is.GreaterThan(0));
            int switches=0;
            AgentMacroStateId previous=agent.CurrentMacroStateId;
            for(int i=0;i<20;i++)
            {
                yield return null;
                agent.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest,out AgentDirectiveRequest current);
                agent.Blackboard.TryGetValue(AgentBlackboardKeys.ShouldExtract,out bool extracting);
                CaseArtifactWriter.Trace("retaliation",$"state={agent.CurrentMacroStateId}; directive={current.DirectiveType}; extract={extracting}");
                if(previous!=agent.CurrentMacroStateId) switches++;
                previous=agent.CurrentMacroStateId;
            }
            Object.Destroy(enemy.gameObject);
            for(int i=0;i<20;i++) yield return null;
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest,out AgentDirectiveRequest restored);
            CaseArtifactWriter.Trace("resume",$"switches={switches}; state={agent.CurrentMacroStateId}; command={restored.CommandId}; expected={original.CommandId}");
            Assert.That(switches,Is.LessThanOrEqualTo(1),"Retaliation must not alternate with extraction.");
            Assert.That(restored.CommandId,Is.EqualTo(original.CommandId));
            Assert.That(restored.DirectiveType,Is.EqualTo(AgentDirectiveType.Extract));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ExtractionRemainsWithoutDamage()
        {
            TestNavMeshBuilder.Flat(World);
            AgentPawnRoot agent=AgentFactory.Create(World,"F1-control",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(-20,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out AgentDirectiveRequest original),Is.True);
            for(int i=0;i<20;i++) yield return null;
            Assert.That(agent.CurrentMacroStateId,Is.EqualTo(AgentMacroStateId.Extraction));
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest,out AgentDirectiveRequest current);
            Assert.That(current.CommandId,Is.EqualTo(original.CommandId));
            CaseArtifactWriter.Trace("control-completed", current.CommandId);
            ContractCompleted=true;
        }
    }
}

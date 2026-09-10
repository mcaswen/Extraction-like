using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.SO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class TargetDecisionTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator FailedAutomaticTargetIsDeferredPerAgentAndMovementAllowsRetry()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Retry A",Vector3.zero,0,true,false);
            RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(agent,"_pawnConfig"),"_targetDiscoveryInterval",0.1f);
            var targetA=TargetFactory.Resources(World,new Vector3(4,0,0));
            var targetB=TargetFactory.Resources(World,new Vector3(10,0,0));
            bool failed=false;
            System.Action<Gameplay.Agent.Commands.AgentDirectiveResult> observe=result=>
            {
                if (agent == null || targetA == null) return;
                if(result.Request.TargetAgentId.Equals(agent.AgentId) && result.Request.TargetObject==targetA.gameObject &&
                    result.Stage==Gameplay.Agent.Commands.AgentDirectiveStage.Failed && result.Reason==Gameplay.Agent.Commands.AgentDirectiveFailure.NoProgress) failed=true;
            };
            Gameplay.Agent.Commands.AgentDirectiveFeedbackChannel.Published+=observe;
            try
            {
                yield return RuntimeWait.Until(()=>agent.DirectiveLifecycle.Active.HasValue,"automatic resource selection");
                Assert.That(agent.DirectiveLifecycle.Active.Value.TargetObject,Is.SameAs(targetA.gameObject));
                yield return RuntimeWait.Until(()=>failed,"actual navigation no-progress failure",10);
                yield return RuntimeWait.Until(()=>agent.DirectiveLifecycle.Active.HasValue && agent.DirectiveLifecycle.Active.Value.TargetObject==targetB.gameObject,"failed candidate must yield to B",2);
                var other=AgentFactory.Create(World,"Retry B",new Vector3(0,0,1),0,true,false);
                yield return RuntimeWait.Until(()=>other.DirectiveLifecycle.Active.HasValue,"other agent unaffected",2);
                Assert.That(other.DirectiveLifecycle.Active.Value.TargetObject,Is.SameAs(targetA.gameObject));
                var manual=AgentDirectiveRequest.SearchConcreteResource(targetA.gameObject,"retry",agent.AgentId,Gameplay.Agent.Runtime.AgentManualDirectiveLock.CreateCommandId("retry"),1000);
                Assert.That(agent.TrySubmitDirective(manual).Accepted,Is.True,"Manual choice bypasses automatic deferral.");
                agent.ClearDirective();
                Assert.That(agent.NavMeshAgent.Warp(new Vector3(0,0,-1)),Is.True);
                yield return RuntimeWait.Until(()=>agent.DirectiveLifecycle.Active.HasValue,"retry after changed position",2);
                Assert.That(agent.DirectiveLifecycle.Active.Value.TargetObject,Is.SameAs(targetA.gameObject));
                ContractCompleted=true;
            }
            finally { Gameplay.Agent.Commands.AgentDirectiveFeedbackChannel.Published-=observe; }
        }
        [UnityTest]
        public IEnumerator ResourceSelectionAndRetentionUseSameMemberDistance()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"F4",Vector3.zero,0,true,false);
            RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(agent,"_pawnConfig"),"_targetDiscoveryInterval",0.2f);
            TargetFactory.Resources(World,new Vector3(3,0,0),new Vector3(35,0,0));
            TargetFactory.Enemies(World,EnemyFactory.Passive(World,new Vector3(0,0,10)));
            yield return RuntimeWait.Until(()=>agent.DirectiveLifecycle.Active.HasValue,"initial discovery");
            Assert.That(agent.DirectiveLifecycle.Active.Value.DirectiveType,Is.EqualTo(AgentDirectiveType.Search));
            float until=Time.time+2f;
            yield return RuntimeWait.Until(()=>
            {
                Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.True);
                Assert.That(agent.DirectiveLifecycle.Active.Value.DirectiveType,Is.EqualTo(AgentDirectiveType.Search),"Retaining a resource must not switch to cluster-center distance.");
                return Time.time>=until;
            },"stable selection");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DecisionUsesActualDefense()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Defense",Vector3.zero);
            TargetFactory.Resources(World,new Vector3(5,0,0));
            yield return null;
            var combat=agent.GetComponent<AgentCombatController>();
            combat.ApplyConfig(combat.StyleConfig,new AgentCombatRuntimeStats(10000,100,123));
            var decision=ConfigureDecision(agent.gameObject);
            decision.RefreshTarget();
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.DecisionDefense,out float defense);
            Assert.That(agent.Defense,Is.EqualTo(123));
            Assert.That(defense,Is.EqualTo(agent.Defense));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DecisionCountsAllUniqueVisibleRiskMembers()
        {
            TestNavMeshBuilder.Flat(World,250);
            var agent=AgentFactory.Create(World,"Risks",Vector3.zero);
            var a=EnemyFactory.Passive(World,new Vector3(0,0,6));
            var b=EnemyFactory.Passive(World,new Vector3(5,0,6));
            var hidden=EnemyFactory.Passive(World,new Vector3(-5,0,6));
            var far=EnemyFactory.Passive(World,new Vector3(300,0,0));
            TargetFactory.Enemies(World,a,b,hidden,far);
            TargetFactory.Enemies(World,a);
            World.Cube("Cover",new Vector3(-2.5f,2,3),new Vector3(2,5,0.2f));
            yield return null; Physics.SyncTransforms();
            Assert.That(Vector3.Distance(agent.Position,far.transform.position),Is.GreaterThan(agent.TargetDiscoveryRange));
            ConfigureDecision(agent.gameObject).RefreshTarget();
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.DecisionRiskEnemyCount,out int count);
            Assert.That(count,Is.EqualTo(2));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator DecisionFindsFarReachableExitWhenNearExitIsDisconnected()
        {
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(0,-0.1f,0),new Vector3(150,0.2f,150)),
                new Bounds(new Vector3(0,5.9f,8),new Vector3(6,0.2f,6)));
            var agent=AgentFactory.Create(World,"Fallback",Vector3.zero);
            RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(agent,"_pawnConfig"),"_targetDiscoveryRange",5f);
            TargetFactory.Extraction(World,new Vector3(0,6,8));
            var far=TargetFactory.Extraction(World,new Vector3(50,0,0));
            yield return null;
            ConfigureDecision(agent.gameObject).RefreshTarget();
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.True);
            Assert.That(agent.DirectiveLifecycle.Active.Value.TargetObject,Is.SameAs(far.ExtractionMembers[0].EntityObject));
            ContractCompleted=true;
        }

        private AgentTargetDecisionController ConfigureDecision(GameObject agent)
        {
            var controller=agent.GetComponent<AgentTargetDecisionController>() ?? agent.AddComponent<AgentTargetDecisionController>();
            RuntimeFixtureAccess.Configure(controller,"_decisionConfig",World.Own(ScriptableObject.CreateInstance<AgentDecisionConfig>()));
            RuntimeFixtureAccess.Configure(controller,"_enableDecisionModule",true);
            controller.enabled=true;
            Assert.That(controller.IsDecisionModuleActive,Is.True);
            return controller;
        }
    }
}

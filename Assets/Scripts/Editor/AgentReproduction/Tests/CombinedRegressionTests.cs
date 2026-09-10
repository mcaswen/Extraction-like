using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Talent;
using Gameplay.Perception;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class CombinedRegressionTests : ReproductionTestFixture
    {
        public static string[] MissingAttack={"Shooter","Projectile"};
        public static string[] InvalidTargets={"DisabledAgent","DisabledExit","CompletedCluster"};
        [UnityTest]
        public IEnumerator UnavailableParticipantsRejectCommands([ValueSource(nameof(InvalidTargets))] string kind)
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Invalid",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(8,0,0));
            var point=exit.ExtractionMembers[0].EntityObject;
            yield return null;
            if(kind=="DisabledAgent") agent.enabled=false;
            else if(kind=="DisabledExit") point.GetComponent<ExtractionPointController>().enabled=false;
            else exit.MarkCompleted();
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out _),Is.False);
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ShieldAbsorptionDoesNotInterruptResourceButDisplacementIsIndependent()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Shield",Vector3.zero);
            var combat=agent.GetComponent<AgentCombatController>();
            var talent=agent.GetComponent<AgentTalentRuntimeController>();
            talent.UnlockNode(AgentTalentNodeId.EarthReactiveShield);
            combat.ApplyConfig(combat.StyleConfig,new AgentCombatRuntimeStats(10000,100,100));
            var resource=TargetFactory.Resources(World,new Vector3(10,0,0));
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,15));
            yield return null;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(resource,agent.AgentIdValue,out var order),Is.True);
            Assert.That(agent.TakeCombatDamage(10,agent.Position,Vector3.back,enemy.gameObject),Is.EqualTo(0));
            Assert.That(talent.ActiveEarthShieldValue,Is.GreaterThan(0));
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(order.CommandId));
            Assert.That(agent.NavMeshAgent.Warp(new Vector3(3,0,0)),Is.True);
            yield return null;
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId,Is.EqualTo(order.CommandId));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator BlockedMuzzleCannotUseDirectDamageFallback()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Muzzle",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,6));
            yield return null;
            var muzzle=World.Root("Muzzle origin");
            muzzle.transform.SetParent(agent.transform,false);
            muzzle.transform.position=CombatAimPointResolver.Resolve(agent.transform)+Vector3.up*2+Vector3.forward*0.6f;
            var shooter=agent.GetComponent<AgentCombatShooter>();
            RuntimeFixtureAccess.Configure(shooter,"_firePoint",muzzle.transform);
            World.Cube("Muzzle obstruction",muzzle.transform.position,new Vector3(0.3f,0.3f,0.3f));
            Physics.SyncTransforms();
            Assert.That(TargetVisibilityQuery.Check(agent.transform,CombatAimPointResolver.Resolve(agent.transform),enemy.transform,20),Is.EqualTo(TargetVisibilityResult.Visible));
            Assert.That(shooter.CanShootAt(enemy,20),Is.False);
            float health=enemy.GetCurrentHealthRatio();
            Assert.That(agent.TrySubmitDirective(AgentDirectiveRequest.EngageConcreteEnemy(enemy.gameObject,"enemy",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("muzzle"),1000)).Accepted,Is.True);
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"blocked muzzle terminal failure",5);
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator MissingAttackConfigurationTerminatesWithoutDirectDamage([ValueSource(nameof(MissingAttack))] string missing)
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Missing attack",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var shooter=agent.GetComponent<AgentCombatShooter>();
            if(missing=="Shooter") shooter.enabled=false;
            else
            {
                RuntimeFixtureAccess.Configure(shooter,"_bulletPrefab",null);
                RuntimeFixtureAccess.Configure(shooter,"_createFallbackBulletIfPrefabMissing",false);
            }
            yield return null;
            float health=enemy.GetCurrentHealthRatio();
            agent.TrySubmitDirective(AgentDirectiveRequest.EngageConcreteEnemy(enemy.gameObject,"enemy",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("missing"),1000));
            float until=Time.time+0.6f;
            yield return RuntimeWait.Until(()=>Time.time>=until,"configuration failure observation");
            Assert.That(agent.DirectiveLifecycle.Active.HasValue,Is.False,"Missing attack configuration must terminate, not hold the command forever.");
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator ActualExtractionDestroysAAndRangedEnemyDamagesB()
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"Extract A",new Vector3(0,0,5),0,false,false);
            var b=AgentFactory.Create(World,"Remain B",new Vector3(5,0,7),0,false,false);
            var enemy=(RangedEnemyBehaviorController)EnemyFactory.Formal(World,"Ranged",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(30,0,0));
            var point=exit.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>();
            point.ExtractionDurationSeconds=0.15f;
            var raid=World.Root("Raid flow").AddComponent<RaidFlowController>();
            for(int i=0;i<4;i++) yield return null;
            Assert.That(enemy.PlayerTransform,Is.SameAs(a.transform));
            raid.SetAgentInsideExtractionPoint(a.AgentIdValue,point,true);
            yield return RuntimeWait.Until(()=>a==null,"real raid extraction destroys A");
            Assert.That(Time.timeScale,Is.GreaterThan(0));
            yield return RuntimeWait.Until(()=>enemy.PlayerTransform==b.transform,"enemy rebinds after raid extraction");
            int health=b.CurrentHealth;
            yield return RuntimeWait.Until(()=>b.CurrentHealth<health,"rebound enemy actually damages B",8);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator TwoAgentsKeepIndependentSuspendedExtractions()
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"A",Vector3.zero,0,false,false);
            var b=AgentFactory.Create(World,"B",new Vector3(4,0,0),0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,15));
            var exitA=TargetFactory.Extraction(World,new Vector3(-15,0,0));
            var exitB=TargetFactory.Extraction(World,new Vector3(15,0,0));
            yield return null;
            var dispatcher=new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(exitA,a.AgentIdValue,out _),Is.True);
            Assert.That(dispatcher.TrySubmitClusterCommand(exitB,b.AgentIdValue,out _),Is.True);
            string aId=a.DirectiveLifecycle.Active.Value.CommandId, bId=b.DirectiveLifecycle.Active.Value.CommandId;
            a.TakeCombatDamage(10,a.Position,Vector3.back,enemy.gameObject);
            b.TakeCombatDamage(10,b.Position,Vector3.back,enemy.gameObject);
            a.TakeCombatDamage(10,a.Position,Vector3.back,enemy.gameObject);
            Assert.That(a.DirectiveLifecycle.SuspendedExtraction.Value.CommandId,Is.EqualTo(aId));
            Assert.That(b.DirectiveLifecycle.SuspendedExtraction.Value.CommandId,Is.EqualTo(bId));
            Object.Destroy(enemy.gameObject);
            yield return RuntimeWait.Until(()=>a.DirectiveLifecycle.Active.HasValue && a.DirectiveLifecycle.Active.Value.CommandId==aId &&
                b.DirectiveLifecycle.Active.HasValue && b.DirectiveLifecycle.Active.Value.CommandId==bId,"independent resumes");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RuntimePathDisconnectionReleasesManualCommand()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Broken path",Vector3.zero);
            var exit=TargetFactory.Extraction(World,new Vector3(12,0,0));
            yield return null;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out _),Is.True);
            agent.NavMeshAgent.enabled=false;
            NavMesh.RemoveAllNavMeshData();
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(0,-0.1f,0),new Vector3(6,0.2f,6)),
                new Bounds(new Vector3(12,-0.1f,0),new Vector3(6,0.2f,6)));
            agent.NavMeshAgent.enabled=true;
            Assert.That(agent.NavMeshAgent.Warp(Vector3.zero),Is.True);
            var path=new NavMeshPath();
            agent.NavMeshAgent.CalculatePath(new Vector3(12,0,0),path);
            Assert.That(path.status,Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"dynamic path failure",5);
            Assert.That(AgentManualDirectiveLock.ShouldHoldManualDirective(agent),Is.False);
            ContractCompleted=true;
        }
    }
}

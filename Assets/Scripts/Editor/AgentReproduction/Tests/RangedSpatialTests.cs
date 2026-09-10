using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class RangedSpatialTests : ReproductionTestFixture
    {
        public static int[] Elevations={-1,1};
        public static string[] RangedKinds={"Ranged","AnchorSentinel"};
        public static bool[] Cover={false,true};
        [UnityTest]
        public IEnumerator BossAnchorSweepsThreeDimensionalPath([ValueSource(nameof(Cover))] bool wall)
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Anchor target",new Vector3(0,0,6));
            yield return null;
            if(wall) World.Cube("Anchor cover",new Vector3(0,3,3),new Vector3(5,8,0.03f));
            var projectileObject=World.Root("Actual anchor");
            projectileObject.transform.position=new Vector3(0,7,0);
            var projectile=projectileObject.AddComponent<HunterBossAnchorProjectile>();
            projectile.Damage=100; projectile.KnockbackStrength=0; projectile.LifeTime=0.4f;
            projectile.Launch((Gameplay.Perception.CombatAimPointResolver.Resolve(agent.transform)-projectile.transform.position).normalized*1000);
            Physics.SyncTransforms();
            var body=projectile.GetComponent<Rigidbody>();
            bool predicted=Gameplay.Perception.ProjectileSweepQuery.TryFirstHit(projectile.transform,null,body.position,body.position+body.velocity*Time.fixedDeltaTime,0.15f,out Collider firstHit);
            CaseArtifactWriter.Trace("anchor-launch",$"position={body.position}; velocity={body.velocity}; target={agent.Position}; predicted={predicted}; firstHit={(firstHit != null ? firstHit.name : "none")}");
            Assert.That(predicted,Is.True,"Constructed trajectory must intersect its wall or agent.");
            int health=agent.CurrentHealth;
            float until=Time.time+0.5f;
            yield return RuntimeWait.Until(()=>Time.time>=until,"anchor collision observation",4);
            Assert.That(health-agent.CurrentHealth,Is.EqualTo(wall?0:100));
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator EnemyShootsAcrossHeight([ValueSource(nameof(RangedKinds))] string kind, [ValueSource(nameof(Elevations))] int elevation)
        {
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(0,-0.1f,0),new Vector3(6,0.2f,4)),
                new Bounds(new Vector3(0,2.9f,6),new Vector3(6,0.2f,4)));
            Vector3 low=Vector3.zero, high=new Vector3(0,3,6);
            var agent=AgentFactory.Create(World,"Height target",elevation>0?high:low,0,false,false);
            var enemy=EnemyFactory.Formal(World,kind,elevation>0?low:high);
            enemy.transform.rotation=Quaternion.LookRotation(elevation>0?Vector3.forward:Vector3.back);
            int initial=agent.CurrentHealth;
            float nextTrace=0;
            yield return RuntimeWait.Until(()=>
            {
                if(kind=="AnchorSentinel" && Time.time>=nextTrace)
                {
                    var sentinel=(AnchorSentinelBehaviorController)enemy;
                    var origin=sentinel.EyeOrigin!=null?sentinel.EyeOrigin.position:enemy.transform.position+Vector3.up*1.8f;
                    CaseArtifactWriter.Trace("sentinel-height",$"state={sentinel.CurrentState}; timer={RuntimeFixtureAccess.Read<float>(sentinel,"_stateTimer")}; origin={origin}; target={agent.Position}; visibility={Gameplay.Perception.TargetVisibilityQuery.Check(enemy.transform,origin,agent.transform,sentinel.DetectionRange)}");
                    nextTrace=Time.time+1;
                }
                return agent.CurrentHealth<initial;
            },"enemy projectile crosses height",20);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator EngageCanFireAcrossDisconnectedNavigationWithoutWalking()
        {
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(0,-0.1f,0),new Vector3(6,0.2f,4)));
            var agent=AgentFactory.Create(World,"Island shooter",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,3,6));
            yield return null; Physics.SyncTransforms();
            float health=enemy.GetCurrentHealthRatio();
            var request=AgentDirectiveRequest.EngageConcreteEnemy(enemy.gameObject,"elevated",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("elevated"),1000);
            Assert.That(agent.TrySubmitDirective(request).Accepted,Is.True);
            Vector3 position=agent.Position;
            yield return RuntimeWait.Until(()=>enemy.GetCurrentHealthRatio()<health,"disconnected island combat",5);
            Assert.That(Vector3.Distance(position,agent.Position),Is.LessThan(0.2f));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator EnemyBulletWithoutWallHitsAgentExactlyOnce()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Open path",new Vector3(0,0,6));
            yield return null;
            var bulletObject=World.Root("Fast bullet",false);
            bulletObject.transform.position=new Vector3(0,agent.Position.y,0);
            bulletObject.AddComponent<SphereCollider>().radius=0.08f;
            bulletObject.AddComponent<Rigidbody>().useGravity=false;
            var bullet=bulletObject.AddComponent<EnemyBulletController>();
            bullet.MoveSpeed=1000f; bullet.Damage=100; bullet.LifeTime=0.5f;
            int health=agent.CurrentHealth;
            bulletObject.SetActive(true);
            yield return RuntimeWait.Until(()=>agent.CurrentHealth<health,"high speed projectile positive control",4);
            for(int i=0;i<4;i++) yield return null;
            Assert.That(health-agent.CurrentHealth,Is.EqualTo(100));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator WallInsertedAfterPlayerLaunchStopsProjectile()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Dynamic cover",Vector3.zero);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,6));
            yield return null; Physics.SyncTransforms();
            float health=enemy.GetCurrentHealthRatio();
            Assert.That(agent.GetComponent<AgentCombatShooter>().TryShootAt(enemy,100),Is.True);
            World.Cube("New thin wall",new Vector3(0,2,3),new Vector3(5,5,0.02f));
            Physics.SyncTransforms();
            float until=Time.time+0.6f;
            yield return RuntimeWait.Until(()=>Time.time>=until,"dynamic cover observation",4);
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator PlayerProjectileHitsElevatedEnemy()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Shooter",Vector3.zero);
            var enemy=EnemyFactory.Passive(World,new Vector3(4,3,0));
            yield return null;
            Physics.SyncTransforms();
            float health=enemy.GetCurrentHealthRatio();
            Assert.That(agent.GetComponent<AgentCombatShooter>().TryShootAt(enemy,100),Is.True);
            yield return RuntimeWait.Until(()=>enemy.GetCurrentHealthRatio()<health,"3D projectile hit",4);
            CaseArtifactWriter.Trace("contract-completed","Elevated enemy damaged by physical player projectile.");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator PlayerShooterRejectsOccludedTarget()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Occluded",Vector3.zero);
            var enemy=EnemyFactory.Passive(World,new Vector3(5,0,0));
            World.Cube("Wall",new Vector3(2.5f,2,0),new Vector3(0.2f,5,4));
            yield return null; Physics.SyncTransforms();
            float health=enemy.GetCurrentHealthRatio();
            Assert.That(agent.GetComponent<AgentCombatShooter>().TryShootAt(enemy,100),Is.False);
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator EnemyBulletStopsAtThinWallBeforeAgent()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Behind wall",new Vector3(0,0,6));
            var wall=World.Cube("Thin wall",new Vector3(0,1,3),new Vector3(5,4,0.03f));
            var bulletObject=World.Root("Fast enemy bullet",false);
            bulletObject.transform.position=new Vector3(0,1,0);
            bulletObject.AddComponent<SphereCollider>().radius=0.08f;
            bulletObject.AddComponent<Rigidbody>().useGravity=false;
            var bullet=bulletObject.AddComponent<EnemyBulletController>();
            bullet.MoveSpeed=15f; bullet.Damage=100; bullet.LifeTime=0.5f;
            bulletObject.SetActive(true);
            int health=agent.CurrentHealth;
            float deadline=Time.time+0.7f;
            yield return RuntimeWait.Until(()=>Time.time>=deadline,"bullet lifetime",4);
            Assert.That(agent.CurrentHealth,Is.EqualTo(health));
            ContractCompleted=true;
        }
    }
}

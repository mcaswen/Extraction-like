using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class EnemyTargetBindingTests : ReproductionTestFixture
    {
        public static string[] Kinds = { "Base", "Ranged", "AnchorSentinel", "HunterBoss", "ModernStrander", "TidalAberration", "AncientStrander" };
        public static string[] Invalidations = { "Disabled", "Destroyed", "Unregistered" };
        public static string[] PatrolKinds = { "Base", "Ranged", "ModernStrander", "TidalAberration", "AncientStrander" };
        [UnityTest]
        public IEnumerator InvalidTargetIsNotReacquired([ValueSource(nameof(Invalidations))] string mode)
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"A",new Vector3(0,0,5));
            var b=AgentFactory.Create(World,"B",new Vector3(4,0,7));
            var enemy=EnemyFactory.Formal(World,"HunterBoss",Vector3.zero);
            for(int i=0;i<4;i++) yield return null;
            Assert.That(RuntimeFixtureAccess.Read<Transform>(enemy,"PlayerTransform"),Is.SameAs(a.transform));
            if(mode=="Disabled") a.enabled=false;
            else if(mode=="Destroyed") Object.Destroy(a.gameObject);
            else Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance.Unregister(a);
            for(int i=0;i<4;i++) yield return null;
            Assert.That(RuntimeFixtureAccess.Read<Transform>(enemy,"PlayerTransform"),Is.SameAs(b.transform));
            Assert.That(RuntimeFixtureAccess.Read<ICombatDamageReceiver>(enemy,"_combatDamageReceiver").DamageRootTransform,Is.SameAs(b.transform));
            Assert.That(RuntimeFixtureAccess.Read<IExternalMovementReceiver>(enemy,"_externalMovementReceiver"),Is.SameAs(b));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator WallUnderSharedSceneRootIsNotAnAgentDamageReceiver()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Shared",Vector3.zero);
            var sceneRoot=World.Root("Shared scene parent");
            agent.transform.SetParent(sceneRoot.transform,true);
            var wall=World.Cube("Wall",new Vector3(4,1,0),Vector3.one);
            wall.transform.SetParent(sceneRoot.transform,true);
            Assert.That(CombatDamageUtility.TryGetDamageReceiver(wall.GetComponent<Collider>(),out _),Is.False);
            Assert.That(PlayerTargetResolver.TryGetAgentHealth(wall.transform,out _),Is.False);
            yield return null;
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator DeadTargetRebinds([ValueSource(nameof(Kinds))] string kind)
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"A",new Vector3(0,0,8));
            var b=AgentFactory.Create(World,"B",new Vector3(8,0,10));
            Component enemy=EnemyFactory.Formal(World,kind,Vector3.zero);
            for(int i=0;i<5;i++) yield return null;
            Assert.That(RuntimeFixtureAccess.Read<Transform>(enemy,"PlayerTransform"),Is.SameAs(a.transform),"Initial control must bind A.");
            a.TakeCombatDamage(1000000,a.Position,Vector3.forward,enemy.gameObject);
            Assert.That(a.IsDead,Is.True);
            for(int i=0;i<8;i++) yield return null;
            Transform target=RuntimeFixtureAccess.Read<Transform>(enemy,"PlayerTransform");
            var receiver=RuntimeFixtureAccess.Read<ICombatDamageReceiver>(enemy,"_combatDamageReceiver");
            CaseArtifactWriter.Trace("binding",$"kind={kind}; target={target?.name}; receiver={receiver?.DamageRootTransform?.name}; A-alive={!a.IsDead}");
            Assert.That(target,Is.SameAs(b.transform));
            Assert.That(receiver,Is.Not.Null);
            Assert.That(receiver.DamageRootTransform,Is.SameAs(b.transform));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator VisibleFartherCandidateIsScannedDuringPatrol([ValueSource(nameof(PatrolKinds))] string kind)
        {
            TestNavMeshBuilder.Flat(World);
            var a=AgentFactory.Create(World,"Hidden A",new Vector3(0,0,5));
            var b=AgentFactory.Create(World,"Visible B",new Vector3(5,0,6));
            World.Cube("Wall",new Vector3(0,2,2.5f),new Vector3(2,5,0.4f));
            var controller=EnemyFactory.Formal(World,kind,Vector3.zero,true);
            var enemy=(IEnemyVisionSource)controller;
            yield return null;
            enemy.VisionTransform.rotation=Quaternion.LookRotation(new Vector3(b.Position.x-enemy.VisionTransform.position.x,0,b.Position.z-enemy.VisionTransform.position.z));
            Physics.SyncTransforms();
            CaseArtifactWriter.Trace("patrol-geometry",$"kind={kind}; origin={enemy.VisionTransform.position}; direction={enemy.VisionTransform.forward}; eye={enemy.EyeHeight}; A={a.Position}; B={b.Position}; state={RuntimeFixtureAccess.Read<object>(controller,"CurrentState")}");
            Assert.That(EnemyVisionUtility.CanSeeTarget(enemy.VisionTransform,a.transform,enemy.DetectionRange,enemy.ViewAngle,enemy.LineOfSightBlockMask,enemy.EyeHeight,enemy.TargetHeight),Is.False);
            Assert.That(EnemyVisionUtility.CanSeeTarget(enemy.VisionTransform,b.transform,enemy.DetectionRange,enemy.ViewAngle,enemy.LineOfSightBlockMask,enemy.EyeHeight,enemy.TargetHeight),Is.True);
            yield return RuntimeWait.Until(()=>enemy.PlayerTransform==b.transform && RuntimeFixtureAccess.Read<object>(controller,"CurrentState").ToString()!="Patrol","visible patrol candidate",5);
            Assert.That(enemy.PlayerTransform,Is.SameAs(b.transform));
            Assert.That(RuntimeFixtureAccess.Read<object>(controller,"CurrentState").ToString(),Is.Not.EqualTo("Patrol"));
            ContractCompleted=true;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Targeting;
using Gameplay.Agent.Combat;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class PerceptionCandidateTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator AreaSkillsRespectCastRangeAndDynamicCover()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Skill spatial",Vector3.zero,0,false,false);
            var target=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var covered=EnemyFactory.Passive(World,new Vector3(3,0,5));
            var wall=World.Cube("Area cover",new Vector3(1.5f,2,5),new Vector3(0.1f,5,5));
            var config=World.Own(ScriptableObject.CreateInstance<AgentAreaDamageSkillConfig>());
            var skill=config.CreateRuntimeSkill();
            var stats=new AgentCombatRuntimeStats(100,100,0);
            yield return null; Physics.SyncTransforms();
            var shortRange=new AgentCombatSkillContext(agent.transform,null,stats,~0,0.1f,castRange:2);
            Assert.That(skill.TryCast(shortRange,AgentCombatSkillTarget.FromEnemy(target),Time.timeAsDouble,out _),Is.False);
            float initial=target.GetCurrentHealthRatio(), hiddenHealth=covered.GetCurrentHealthRatio();
            var context=new AgentCombatSkillContext(agent.transform,null,stats,~0,0.1f,playPrototypeSkillVfx:false,castRange:20);
            Assert.That(skill.TryCast(context,AgentCombatSkillTarget.FromEnemy(target),Time.timeAsDouble,out _),Is.True);
            Assert.That(target.GetCurrentHealthRatio(),Is.LessThan(initial));
            Assert.That(covered.GetCurrentHealthRatio(),Is.EqualTo(hiddenHealth));
            var field=World.Root("DOT field").AddComponent<AgentCombatAreaDamageOverTime>();
            field.transform.position=target.transform.position;
            field.Initialize(agent.transform,~0,4,100,3,0.1f,default,0,null,Color.white,true);
            float until=Time.time+0.3f;
            yield return RuntimeWait.Until(()=>Time.time>=until,"covered DOT ticks");
            Assert.That(covered.GetCurrentHealthRatio(),Is.EqualTo(hiddenHealth));
            wall.SetActive(false); Physics.SyncTransforms();
            yield return RuntimeWait.Until(()=>covered.GetCurrentHealthRatio()<hiddenHealth,"uncovered DOT positive control");
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator DiscoverySkipsOccludedNearestMemberAndDeduplicatesVisibleEnemies()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Discovery",Vector3.zero,0f,true,false);
            var hidden=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var visible=EnemyFactory.Passive(World,new Vector3(5,0,6));
            var a=TargetFactory.Enemies(World,hidden,visible);
            var b=TargetFactory.Enemies(World,visible);
            World.Cube("Wall",new Vector3(0,2,2.5f),new Vector3(2,5,0.2f));
            yield return null;
            Physics.SyncTransforms();
            var candidates=new List<AgentTargetCandidate>();
            new AgentTargetCandidateCollector().CollectVisibleEnemies(agent,new GameplayTargetClusterAuthoringBase[]{a,b},30,candidates);
            Assert.That(candidates.Count,Is.EqualTo(1));
            Assert.That(candidates[0].Enemy,Is.SameAs(visible));
            yield return RuntimeWait.Until(()=>agent.DirectiveLifecycle.Active.HasValue,"automatic discovery");
            Assert.That(agent.DirectiveLifecycle.Active.Value.TargetObject,Is.SameAs(visible.gameObject));
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator KnownAttackerBehindWallIsNotReportedAsVisible()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Known attacker",Vector3.zero);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,6));
            World.Cube("Wall",new Vector3(0,2,3),new Vector3(4,5,0.2f));
            yield return null; Physics.SyncTransforms();
            Assert.That(agent.TakeCombatDamage(10,agent.Position,Vector3.back,enemy.gameObject),Is.GreaterThan(0));
            yield return null;
            agent.Blackboard.TryGetValue(AgentBlackboardKeys.HasVisibleEnemy,out bool visible);
            Assert.That(visible,Is.False);
            Assert.That(agent.CurrentMacroStateId,Is.EqualTo(AgentMacroStateId.Combat));
            yield return RuntimeWait.Until(()=>!agent.DirectiveLifecycle.Active.HasValue,"lost sight ends bounded pursuit",6);
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator RangeUsesHeightAndTriggersDoNotOcclude()
        {
            var viewer=World.Root("Viewer");
            var target=World.Root("Target"); target.transform.position=new Vector3(0,4,2);
            target.AddComponent<BoxCollider>();
            var trigger=World.Cube("Trigger",new Vector3(0,2,1),new Vector3(4,4,0.2f));
            trigger.GetComponent<Collider>().isTrigger=true;
            Physics.SyncTransforms();
            Assert.That(TargetVisibilityQuery.Check(viewer.transform,Vector3.zero,target.transform,3),Is.EqualTo(TargetVisibilityResult.OutOfRange));
            Assert.That(TargetVisibilityQuery.Check(viewer.transform,Vector3.zero,target.transform,6),Is.EqualTo(TargetVisibilityResult.Visible));
            trigger.GetComponent<Collider>().isTrigger=false; Physics.SyncTransforms();
            Assert.That(TargetVisibilityQuery.Check(viewer.transform,Vector3.zero,target.transform,6),Is.EqualTo(TargetVisibilityResult.Occluded));
            yield return null;
            ContractCompleted=true;
        }
    }
}

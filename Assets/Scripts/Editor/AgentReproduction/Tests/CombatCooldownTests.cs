using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class CombatCooldownTests : ReproductionTestFixture
    {
        public static string[] Changes={"Stats","Totem","EquivalentConfig"};
        [UnityTest]
        public IEnumerator ReorderedSkillsRetainCooldownAndNewSkillStartsReady()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Reordered",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var old=World.Own(ScriptableObject.CreateInstance<AgentAreaDamageSkillConfig>());
            var added=World.Own(ScriptableObject.CreateInstance<AgentAreaDamageSkillConfig>());
            RuntimeFixtureAccess.Configure(old,"_skillId","old");
            RuntimeFixtureAccess.Configure(added,"_skillId","new");
            var combat=agent.GetComponent<AgentCombatController>();
            var style=World.Own(Object.Instantiate(combat.StyleConfig));
            RuntimeFixtureAccess.Configure(style,"_skills",new AgentCombatSkillConfigBase[]{old});
            combat.ApplyConfig(style,new AgentCombatRuntimeStats(10000,100,0));
            yield return null; Physics.SyncTransforms();
            double now=Time.timeAsDouble;
            Assert.That(combat.TryCastReadySkill(enemy,now,out _),Is.True);
            RuntimeFixtureAccess.Configure(style,"_skills",new AgentCombatSkillConfigBase[]{added,old});
            combat.ApplyConfig(style,new AgentCombatRuntimeStats(10000,100,0));
            Assert.That(combat.TryCastReadySkill(enemy,now+0.1,out _),Is.True,"New skill should start ready.");
            Assert.That(combat.TryCastReadySkill(enemy,now+0.2,out _),Is.False,"Both skills must now be cooling down.");
            ContractCompleted=true;
        }
        [UnityTest]
        public IEnumerator SkillCooldownSurvivesRefresh([ValueSource(nameof(Changes))] string change)
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Cooldown",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var skill=World.Own(ScriptableObject.CreateInstance<AgentAreaDamageSkillConfig>());
            RuntimeFixtureAccess.Configure(skill,"_skillId","cooldown-test");
            RuntimeFixtureAccess.Configure(skill,"_cooldownSeconds",5f);
            var style=World.Own(Object.Instantiate(agent.GetComponent<AgentCombatController>().StyleConfig));
            RuntimeFixtureAccess.Configure(style,"_skills",new AgentCombatSkillConfigBase[]{skill});
            var combat=agent.GetComponent<AgentCombatController>();
            combat.ApplyConfig(style,new AgentCombatRuntimeStats(10000,100,0));
            yield return null; Physics.SyncTransforms();
            double now=Time.timeAsDouble;
            Assert.That(combat.TryCastReadySkill(enemy,now,out _),Is.True,"Single skill positive control.");
            if(change=="EquivalentConfig")
            {
                style=World.Own(Object.Instantiate(style));
                RuntimeFixtureAccess.Configure(style,"_skills",new AgentCombatSkillConfigBase[]{World.Own(Object.Instantiate(skill))});
            }
            combat.ApplyConfig(style,new AgentCombatRuntimeStats(10000,change=="Stats"?110:100,0),
                change=="Totem"?new TotemModifierSet{AttackRangePercent=0.25f}:default);
            float health=enemy.GetCurrentHealthRatio();
            Assert.That(combat.TryCastReadySkill(enemy,now+0.1,out _),Is.False,"Refresh must preserve the previous cast deadline.");
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            Assert.That(combat.TryCastReadySkill(enemy,now+5.01,out _),Is.True,"The same skill becomes ready after its original deadline.");
            ContractCompleted=true;
        }

        [UnityTest]
        public IEnumerator CombatReentryDoesNotResetOrdinaryAttackLock()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Attack lock",Vector3.zero,0,false,false);
            var enemy=EnemyFactory.Passive(World,new Vector3(0,0,5));
            var combat=agent.GetComponent<AgentCombatController>();
            yield return null;
            combat.LockAttack(Time.timeAsDouble,2);
            float health=enemy.GetCurrentHealthRatio();
            for(int i=0;i<3;i++)
            {
                var request=AgentDirectiveRequest.EngageConcreteEnemy(enemy.gameObject,"enemy",agent.AgentId,AgentManualDirectiveLock.CreateCommandId("enemy"),1000);
                Assert.That(agent.TrySubmitDirective(request).Accepted,Is.True);
                for(int frame=0;frame<4;frame++) yield return null;
                agent.ClearDirective();
                for(int frame=0;frame<2;frame++) yield return null;
            }
            Assert.That(combat.IsAttackReady(Time.timeAsDouble),Is.False);
            Assert.That(enemy.GetCurrentHealthRatio(),Is.EqualTo(health));
            ContractCompleted=true;
        }
    }
}

using AgentReproduction.Infrastructure;
using Gameplay.Agent.Core;
using Gameplay.Agent.SO;
using Gameplay.Agent.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class AgentFactory
    {
        public static AgentPawnRoot Create(TestWorldBuilder world, string id, Vector3 position, float moveSpeed=0, bool discovery=false, bool skills=true)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab/Agent.prefab");
            Assert.That(prefab,Is.Not.Null);
            GameObject staging=world.Root("Staging " + id,false);
            GameObject instance=Object.Instantiate(prefab,staging.transform);
            AgentPawnRoot pawn=instance.GetComponentInChildren<AgentPawnRoot>(true);
            Assert.That(pawn,Is.Not.Null);
            Assert.That(pawn.Blackboard,Is.Null,"Configure before Awake.");
            AgentPawnConfig config=world.Own(Object.Instantiate(RuntimeFixtureAccess.Read<AgentPawnConfig>(pawn,"_pawnConfig")));
            RuntimeFixtureAccess.Configure(config,"_enableTargetDiscovery",discovery);
            RuntimeFixtureAccess.Configure(config,"_moveSpeed",moveSpeed);
            RuntimeFixtureAccess.Configure(config,"_maxHealth",10000);
            RuntimeFixtureAccess.Configure(config,"_defense",0f);
            if (!skills && config.CombatStyleConfig != null)
            {
                var style=world.Own(Object.Instantiate(config.CombatStyleConfig));
                RuntimeFixtureAccess.Configure(style,"_skills",new AgentCombatSkillConfigBase[0]);
                RuntimeFixtureAccess.Configure(config,"_combatStyleConfig",style);
            }
            RuntimeFixtureAccess.Configure(pawn,"_pawnConfig",config);
            Assert.That(pawn.TryAssignAgentId(id),Is.True);
            pawn.transform.position=position;
            staging.SetActive(true);
            Assert.That(pawn.Blackboard,Is.Not.Null);
            Assert.That(pawn.NavMeshAgent.isOnNavMesh,Is.True);
            return pawn;
        }
    }
}

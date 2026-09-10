using UnityEngine;
using UnityEditor;
using NUnit.Framework;

namespace AgentReproduction.World
{
    public static class EnemyFactory
    {
        public static Component Formal(TestWorldBuilder world, string kind, Vector3 position)
        {
            string path = kind == "Base" ? "Base/Enemy" : kind == "Ranged" ? "Base/Pfb_Enemy_RangedEnemy" :
                kind == "HunterBoss" ? "Boss/Pfb_Enemy_HunterBoss" : "Common/Pfb_Enemy_Common_" + kind;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Pawn/" + path + ".prefab");
            Assert.That(prefab, Is.Not.Null, path);
            GameObject staging = world.Root("Enemy staging",false);
            GameObject instance = Object.Instantiate(prefab,staging.transform);
            instance.transform.position=position;
            string controller = kind == "Base" ? "EnemyBehaviorController" : kind == "Ranged" ? "RangedEnemyBehaviorController" : kind + "BehaviorController";
            Component component = instance.GetComponent(controller);
            Assert.That(component,Is.Not.Null,controller);
            staging.SetActive(true);
            return component;
        }
        public static EnemyHealthController Passive(TestWorldBuilder world, Vector3 position)
        {
            GameObject root=world.Root("Passive damage source",false);
            root.transform.position=position;
            CapsuleCollider collider=root.AddComponent<CapsuleCollider>();
            collider.center=Vector3.up; collider.height=2; collider.radius=0.4f;
            EnemyHealthController health=root.AddComponent<EnemyHealthController>();
            health.MaxHealth=10000;
            root.SetActive(true);
            return health;
        }
    }
}

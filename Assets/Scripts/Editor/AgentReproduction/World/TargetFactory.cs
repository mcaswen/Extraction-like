using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class TargetFactory
    {
        public static ResourceClusterAuthoring Resources(TestWorldBuilder world, params Vector3[] positions)
        {
            var members=new List<GameplayTargetEntityMember>();
            foreach(var position in positions)
            {
                var item=world.Root("Loot member").AddComponent<WorldLootItem>();
                item.transform.position=position;
                item.ItemData=world.Own(ScriptableObject.CreateInstance<InventoryItemData>());
                item.CurrentAmount=1;
                members.Add(new GameplayTargetEntityMember(item.GetInstanceID().ToString(),item.gameObject));
            }
            var root=world.Root("Resource cluster",false);
            var cluster=root.AddComponent<ResourceClusterAuthoring>();
            RuntimeFixtureAccess.Configure(cluster,"_resourceMembers",members);
            root.SetActive(true);
            return cluster;
        }
        public static ActiveEnemyClusterAuthoring Enemies(TestWorldBuilder world, params EnemyHealthController[] enemies)
        {
            GameObject root=world.Root("Enemy cluster",false);
            var cluster=root.AddComponent<ActiveEnemyClusterAuthoring>();
            var members=new List<GameplayTargetEntityMember>();
            foreach(var enemy in enemies) members.Add(new GameplayTargetEntityMember(enemy.GetInstanceID().ToString(),enemy.gameObject));
            RuntimeFixtureAccess.Configure(cluster,"_initialEnemies",members);
            root.SetActive(true);
            return cluster;
        }
        public static ExtractionClusterAuthoring Extraction(TestWorldBuilder world, Vector3 position)
        {
            GameObject point=world.Root("Test extraction",false);
            point.transform.position=position;
            BoxCollider collider=point.AddComponent<BoxCollider>();
            collider.isTrigger=true; collider.size=new Vector3(2,3,2);
            point.AddComponent<ExtractionPointController>();
            GameObject clusterRoot=world.Root("Test extraction cluster",false);
            ExtractionClusterAuthoring cluster=clusterRoot.AddComponent<ExtractionClusterAuthoring>();
            RuntimeFixtureAccess.Configure(cluster,"_extractionMembers",new List<GameplayTargetEntityMember> {new GameplayTargetEntityMember("ExitMember",point)});
            point.SetActive(true); clusterRoot.SetActive(true);
            return cluster;
        }
    }
}

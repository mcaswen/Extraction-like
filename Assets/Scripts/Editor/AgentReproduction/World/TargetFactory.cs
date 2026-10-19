using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class TargetFactory
    {
        public static ResourceClusterAuthoring ResourceBoxes(TestWorldBuilder world, params LootBoxEntity[] boxes)
        {
            var members = new List<GameplayTargetEntityMember>();
            foreach (var box in boxes) members.Add(new GameplayTargetEntityMember(box.GetInstanceID().ToString(), box.gameObject));
            var root = world.Root("Box resource cluster", false);
            var cluster = root.AddComponent<ResourceClusterAuthoring>();
            RuntimeFixtureAccess.Configure(cluster, "_resourceMembers", members);
            root.SetActive(true);
            return cluster;
        }

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
        public static ExtractionClusterAuthoring Extraction(TestWorldBuilder world, Vector3 position) => ExtractionPoints(world, position);
        // 第一个点叫 "Test extraction"，之后依次 "Test extraction 2"、"Test extraction 3"
        public static ExtractionClusterAuthoring ExtractionPoints(TestWorldBuilder world, params Vector3[] positions)
        {
            var points=new List<GameObject>();
            var members=new List<GameplayTargetEntityMember>();
            for(int i=0;i<positions.Length;i++)
            {
                string suffix=i==0?"":" "+(i+1);
                GameObject point=world.Root("Test extraction"+suffix,false);
                point.transform.position=positions[i];
                BoxCollider collider=point.AddComponent<BoxCollider>();
                collider.isTrigger=true; collider.size=new Vector3(2,3,2);
                point.AddComponent<ExtractionPointController>();
                points.Add(point);
                members.Add(new GameplayTargetEntityMember("ExitMember"+suffix.Trim(),point));
            }
            GameObject clusterRoot=world.Root("Test extraction cluster",false);
            ExtractionClusterAuthoring cluster=clusterRoot.AddComponent<ExtractionClusterAuthoring>();
            RuntimeFixtureAccess.Configure(cluster,"_extractionMembers",members);
            foreach(GameObject point in points) point.SetActive(true);
            clusterRoot.SetActive(true);
            return cluster;
        }
    }
}

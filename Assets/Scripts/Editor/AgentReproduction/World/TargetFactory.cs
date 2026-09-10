using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class TargetFactory
    {
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

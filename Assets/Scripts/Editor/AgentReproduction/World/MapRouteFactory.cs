using System.Collections.Generic;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class MapRouteFactory
    {
        public static MapGraphEdgeDefinition[] Chain(int count)
        {
            var edges = new MapGraphEdgeDefinition[count-1];
            for (int i=0;i<edges.Length;i++) edges[i]=new MapGraphEdgeDefinition("e"+i,"n"+i,"n"+(i+1),1,MapGraphAxis.Horizontal,MapGraphEdgeOrigin.Manual);
            return edges;
        }
        public static AgentRouteEnvironment Environment(MapGraphBindingAuthoring binding, AgentPawnRoot agent, long version=1)
        {
            var profile=AgentNavigationProfile.FromAgent(agent.NavMeshAgent);
            // 构造图实际测量每条保存边，不能用显示距离代替导航长度。
            var service=new MapGraphNavigationCostService(binding.MapDefinition,binding,profile,"","");
            while(service.PendingEdgeCount>0) service.ProcessPending(2);
            return new AgentRouteEnvironment(new MapGraphService(binding.MapDefinition),binding.MapDefinition.Revision,version,
                service.Snapshot,new MapGraphRouteTargetResolver(binding),profile);
        }
        public static MapGraphBindingAuthoring Bind(TestWorldBuilder world,
            GameplayTargetClusterAuthoringBase[] clusters, Vector3[] anchors, MapGraphEdgeDefinition[] edges = null)
        {
            var nodes = new List<MapGraphNodeDefinition>(); var bindings = new List<MapGraphTargetBinding>();
            for (int i = 0; i < clusters.Length; i++)
            {
                var kind = clusters[i] is ResourceClusterAuthoring ? MapGraphNodeKind.Resource :
                    clusters[i] is ExtractionClusterAuthoring ? MapGraphNodeKind.Extraction :
                    clusters[i] is EnemySourceClusterAuthoring ? MapGraphNodeKind.EnemySource : MapGraphNodeKind.ActiveEnemy;
                nodes.Add(new MapGraphNodeDefinition("n"+i, kind, new Vector2(i*80,0), zoneId:"unassigned"));
                bindings.Add(new MapGraphTargetBinding("n"+i, clusters[i], anchors[i]));
            }
            var definition = world.Own(ScriptableObject.CreateInstance<SO_MapGraphDefinition>());
            definition.ApplyCommandData("route-test", "route-test", "", new[] {
                new MapGraphZoneDefinition("unassigned", "未分区", new Rect(-50,-100,1000,200), new Vector2(80,20), isSynthetic:true)
            }, nodes, edges ?? new MapGraphEdgeDefinition[0], new MapGraphLayoutConstraints(), new MapGraphNavigationBakeData());
            var binding = world.Root("Route binding").AddComponent<MapGraphBindingAuthoring>();
            binding.Configure(definition, bindings, new[] {new MapGraphZoneBinding("unassigned",null,isSynthetic:true)});
            Assert.That(binding.IsValid, Is.True, string.Join(";",binding.ValidationErrors));
            return binding;
        }
    }
}

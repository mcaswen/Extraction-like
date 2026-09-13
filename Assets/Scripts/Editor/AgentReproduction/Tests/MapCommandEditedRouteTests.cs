using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Config;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandEditedRouteTests:ReproductionTestFixture
    {
        private const string AssetPath="Assets/MapCommandEditedRoute_Reproduction.asset";
        public static string[] Edits={"Deleted","Restored","LayoutOnly"};
        [UnityTest]public IEnumerator SavedAuthorConnectionsDetermineTheActualVisitedGroups([ValueSource(nameof(Edits))]string edit)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>(AssetPath),Is.Null);
            TestNavMeshBuilder.Flat(World);
            var pawn=AgentFactory.Create(World,"2",Vector3.left*4,8,false,false);
            var points=new[]{Vector3.zero,Vector3.right*16,new Vector3(16,0,16),Vector3.forward*16};
            var clusters=points.Select(p=>{var e=EnemyFactory.Passive(World,p+Vector3.forward*3);var c=TargetFactory.Enemies(World,e);
                Object.DestroyImmediate(e.gameObject);return (GameplayTargetClusterAuthoringBase)c;}).ToArray();
            var edges=new[]{new MapGraphEdgeDefinition("e01","n0","n1",1,MapGraphAxis.Horizontal,MapGraphEdgeOrigin.Manual),
                new MapGraphEdgeDefinition("e12","n1","n2",1,MapGraphAxis.Vertical,MapGraphEdgeOrigin.Manual),
                new MapGraphEdgeDefinition("e23","n2","n3",1,MapGraphAxis.Horizontal,MapGraphEdgeOrigin.Manual)};
            var binding=MapRouteFactory.Bind(World,clusters,points,edges);var initial=binding.MapDefinition;
            var draft=new MapGraphLayoutDraft(new[]{new MapGraphZoneDefinition("unassigned","试验区",new Rect(-120,-120,240,240),new Vector2(40,20),isSynthetic:true)},
                initial.Nodes.Select((n,i)=>n.WithLayout(new Vector2(points[i].x*5-40,points[i].z*5-40),"","",false)),edges,initial.LayoutConstraints);
            var original=draft;
            draft=MapGraphEditOperations.DeleteEdge(draft,"e01");
            draft=MapGraphEditOperations.AddEdge(draft,"n0","n3",MapGraphAxis.Vertical);
            if(edit!="Deleted")
            {
                draft.Graph.TryGetEdgeBetween("n0","n3",out var shortcut);
                draft=MapGraphEditOperations.DeleteEdge(draft,shortcut.EdgeId);
                draft=MapGraphEditOperations.AddEdge(draft,"n0","n1",MapGraphAxis.Horizontal);
            }
            if(edit=="LayoutOnly")
            {
                draft=MapGraphEditOperations.MoveZone(draft,"unassigned",new Rect(180,-120,240,240));
                draft=MapGraphEditOperations.StyleEdge(draft,"e12",3,5,4,true,Color.cyan);
            }
            var preview=new MapGraphEditOperation(original,draft,initial.GenerationSettings,1,"路线验收编辑");
            for(int i=0;!preview.IsComplete&&i<1000;i++){preview.Advance(128,6);yield return null;}
            Assert.That(preview.Result,Is.Not.Null,string.Join(";",preview.Failures));draft=preview.Result;
            Assert.That(MapGraphValidation.Validate(draft).IsValid,Is.True);
            var asset=Object.Instantiate(initial);
            asset.ApplyCommandData("edited-route",edit,"",draft.Zones,draft.Nodes,draft.Edges,draft.Constraints,new MapGraphNavigationBakeData());
            AssetDatabase.CreateAsset(asset,AssetPath);AssetDatabase.SaveAssetIfDirty(asset);
            Resources.UnloadAsset(asset);asset=AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetPath);
            Assert.That(asset,Is.Not.Null);Assert.That(asset.Edges.Count,Is.EqualTo(3));
            var positions=clusters.Select(c=>c.transform.position).ToArray();
            binding.Configure(asset,binding.TargetBindings,binding.ZoneBindings);
            var installer=World.Root("Saved topology installer").AddComponent<RaidMapCommandInstaller>();installer.Configure(binding);Time.timeScale=4;
            var expected=edit=="Deleted"?new[]{"n0","n3","n2"}:new[]{"n0","n1","n2"};
            var visited=new List<string>();
            string previousNode=null;
            void RecordVisit(string node)
            {
                var delta=pawn.Position-points[int.Parse(node.Substring(1))];
                Assert.That(new Vector2(delta.x,delta.z).magnitude,Is.LessThan(.4f),"游标离开前必须真的到达 "+node);
                visited.Add(node);CaseArtifactWriter.Trace("visited",node+"; position="+pawn.Position);
            }
            pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,pawn.AgentId));
            yield return RuntimeWait.Until(()=>
            {
                var root=pawn.RouteSnapshot;
                if(root.HasRoute&&previousNode!=root.CurrentNodeId)
                {
                    if(previousNode!=null)RecordVisit(previousNode);
                    previousNode=root.CurrentNodeId;
                }
                if(root.Stage==AgentRouteStage.Completed&&!visited.Contains(root.CurrentNodeId))RecordVisit(root.CurrentNodeId);
                return root.Stage==AgentRouteStage.Completed||root.Stage==AgentRouteStage.Failed;
            },"保存后的作者图实际执行",20);
            Assert.That(pawn.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Completed),pawn.RouteSnapshot.Failure.ToString());
            Assert.That(pawn.RouteSnapshot.NodeIds,Is.EqualTo(expected));Assert.That(visited,Is.EqualTo(expected));
            Assert.That(clusters.Select(c=>c.transform.position),Is.EqualTo(positions));
            Assert.That(new Vector2(pawn.Position.x-16,pawn.Position.z-16).magnitude,Is.LessThan(.4f));
            ContractCompleted=true;
        }
        [TearDown]public void RemoveOwnedAsset()
        { if(AssetDatabase.LoadAssetAtPath<Object>(AssetPath)!=null)AssetDatabase.DeleteAsset(AssetPath); }
    }
}

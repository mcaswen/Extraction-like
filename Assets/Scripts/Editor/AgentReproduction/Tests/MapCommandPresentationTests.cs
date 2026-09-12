using System.Collections;
using System.Linq;
using System.Reflection;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.SO;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.View;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandPresentationTests : ReproductionTestFixture
    {
        private AgentPawnRoot _first,_second;
        private RaidMapCommandInstaller _installer;
        private MapGraphBindingAuthoring _binding;
        private MapGraphPresenter Map=>_installer.Presentation;
        private void Setup(bool two=false,bool resource=false)
        {
            MapGraphUguiPrefabFactory.EnsureCurrentAssets(); TestNavMeshBuilder.Flat(World);
            if(EventSystem.current==null)World.Root("UI input").AddComponent<EventSystem>();
            _first=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            if(two)_second=AgentFactory.Create(World,"2",Vector3.left*3+Vector3.forward*4,4,false,false);
            var positions=new[]{Vector3.zero,Vector3.right*16,Vector3.right*32};
            var clusters=positions.Select(x=>{var enemy=EnemyFactory.Passive(World,x+Vector3.forward*3);
                var cluster=TargetFactory.Enemies(World,enemy);Object.DestroyImmediate(enemy.gameObject);return (GameplayTargetClusterAuthoringBase)cluster;}).ToArray();
            if(resource)clusters[0]=TargetFactory.Resources(World,Vector3.right*2);
            _binding=MapRouteFactory.Bind(World,clusters,positions,MapRouteFactory.Chain(3));
            _installer=World.Root("Map installer").AddComponent<RaidMapCommandInstaller>();_installer.Configure(_binding);
            Time.timeScale=4; Assert.That(Map,Is.Not.Null);Canvas.ForceUpdateCanvases();Map.TickPresentation();
        }
        private void Click(string id,PointerEventData.InputButton button=PointerEventData.InputButton.Left)
        { ExecuteEvents.Execute(Map.Overlay.NodeViews[id].gameObject,new PointerEventData(EventSystem.current){button=button},ExecuteEvents.pointerClickHandler); }
        private IEnumerator Observe(double seconds)
        {double end=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<end){Map.TickPresentation();yield return null;}}
        private static void Stop(AgentPawnRoot pawn)=>RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(pawn,"_pawnConfig"),"_moveSpeed",0f);
        [UnityTest] public IEnumerator SavedPrefabHasChineseFontAndFormalZonesHaveCenteredNamesAndStraightEdges()
        {
            Setup();var definition=AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>("Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset");
            Map.Overlay.Initialize(definition,Map.Overlay.Theme,_=>{},(_,__)=>{});
            Map.Viewport.SetExpanded(true);Canvas.ForceUpdateCanvases();
            Assert.That(Map.Overlay.ZoneViews.Count,Is.EqualTo(7));Assert.That(Map.Overlay.NodeViews.Count,Is.EqualTo(28));
            foreach(var view in Map.Overlay.ZoneViews.Values)
            {Assert.That(view.NameRect.anchoredPosition,Is.EqualTo(Vector2.zero));Assert.That(view.NameRect.position,Is.EqualTo(view.RectTransform.position));}
            foreach(var view in Map.Overlay.EdgeViews.Values)
            {Assert.That(view.GeometryValid,Is.True,view.EdgeId);Assert.That(Mathf.Abs(view.FromPort.x-view.ToPort.x)<.01f||Mathf.Abs(view.FromPort.y-view.ToPort.y)<.01f,Is.True);}
            foreach(char c in "区域指挥资源敌人撤离渔村雨林龙骨礁") Assert.That(Map.Overlay.Theme.Font.HasCharacter(c),Is.True,c.ToString());
            ContractCompleted=true;yield break;
        }
        [UnityTest] public IEnumerator ActualNodeHandlerSubmitsOneRootToTheFocusedAgent()
        {
            Setup(true);AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2");
            int planning=0;_second.RouteResultPublished+=r=>{if(r.Stage==AgentRouteStage.Planning)planning++;};
            Click("n2");Assert.That(Map.SubmittedCommandCount,Is.EqualTo(1));Assert.That(planning,Is.EqualTo(1));
            Assert.That(Map.LastSubmittedResult.Request.TargetAgentId,Is.EqualTo(_second.AgentId));
            yield return RuntimeWait.Until(()=>_second.RouteSnapshot.IsActive,"节点点击的真实根接受");
            Assert.That(_first.RouteSnapshot.HasRoute,Is.False);Assert.That(_second.RouteSnapshot.Request.Source,Is.EqualTo(AgentRouteSource.Player));
            yield return Observe(.06);Assert.That(Map.Nodes["n2"].TargetIdentities,Is.EqualTo("2"));ContractCompleted=true;
        }
        [UnityTest] public IEnumerator CompactExpandedZoomAndDragShareTheSameViewsAndExecutedRoute()
        {
            Setup();Click("n2");yield return RuntimeWait.Until(()=>_first.RouteSnapshot.StepIndex==1&&_first.Position.x>3,"进入可见线路");
            Stop(_first);yield return Observe(.25);int builds=Map.Overlay.BuildCount;var root=_first.RouteSnapshot.Request.RequestId;
            var node=Map.Overlay.NodeViews["n1"];var edge=Map.Overlay.EdgeViews["e0"];
            Map.Viewport.SetExpanded(true);Map.Viewport.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,2)});
            Map.Viewport.OnDrag(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,delta=new Vector2(20,-10)});
            yield return Observe(.06);Assert.That(Map.Viewport.Zoom,Is.GreaterThan(1));Assert.That(Map.Viewport.Pan,Is.Not.EqualTo(Vector2.zero));
            Assert.That(Map.Overlay.NodeViews["n1"],Is.SameAs(node));Assert.That(Map.Overlay.BuildCount,Is.EqualTo(builds));
            var state=Map.Projection.AgentStates.Single();var marker=Map.Overlay.Markers.Single();
            Assert.That(marker.Position,Is.EqualTo(edge.Position(state.CurrentEdgeFromNodeId,state.CurrentEdgeProgress01)));
            Assert.That(MapGraphAgentMarkerLayout.DistanceToSegment(marker.Position,edge.FromPort,edge.ToPort),Is.LessThan(.001f));
            CollectionAssert.AreEqual(_first.RouteSnapshot.NodeIds.Skip(_first.RouteSnapshot.StepIndex).ToArray(),state.RemainingPathNodeIds);
            Map.Viewport.SetExpanded(false);yield return Observe(.06);
            Assert.That(_first.RouteSnapshot.Request.RequestId,Is.EqualTo(root));Assert.That(Map.Overlay.BuildCount,Is.EqualTo(builds));ContractCompleted=true;
        }
        [UnityTest] public IEnumerator SingleProcessingAgentIsOffsetAndRetaliationKeepsThatCluster()
        {
            Setup(resource:true);Click("n2");
            yield return RuntimeWait.Until(()=>_first.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"正式资源等待");
            yield return Observe(.06);var marker=Map.Overlay.Markers.Single();var node=Map.Overlay.NodeViews["n0"];
            Vector2 offset=marker.Position-node.RectTransform.anchoredPosition;
            Assert.That(offset.magnitude,Is.GreaterThan(node.VisualRadius+Map.Overlay.Theme.CompactAgentSize*.5f));
            Assert.That(marker.OnEdge,Is.False);
            var enemy=EnemyFactory.Passive(World,_first.Position+Vector3.forward*15);
            _first.TakeCombatDamage(10,_first.Position,Vector3.left,enemy.gameObject);yield return Observe(.06);
            Assert.That(Map.Projection.AgentStates.Single().IsRetaliating,Is.True);
            Assert.That(Map.Overlay.Markers.Single().Position,Is.EqualTo(node.RectTransform.anchoredPosition+offset));
            Object.DestroyImmediate(enemy.gameObject);ContractCompleted=true;
        }
        [UnityTest] public IEnumerator OverlappingTravellersCombineIdentitiesOnTheSameLine()
        {
            Setup(true);
            Assert.That(_first.NavMeshAgent.avoidancePriority,Is.Not.EqualTo(_second.NavMeshAgent.avoidancePriority));
            Click("n2");AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2");Click("n2");
            Assert.That(Map.LastSubmittedResult.Request.TargetAgentId,Is.EqualTo(_second.AgentId));
            double end=Time.realtimeSinceStartupAsDouble+5;
            while(Time.realtimeSinceStartupAsDouble<end)
            {
                if(_first.RouteSnapshot.StepIndex==1&&_first.Position.x>3)Stop(_first);
                if(_second.RouteSnapshot.StepIndex==1&&_second.Position.x>1)Stop(_second);
                if(_first.RouteSnapshot.StepIndex==1&&_second.RouteSnapshot.StepIndex==1&&_first.Position.x>3&&_second.Position.x>1)break;
                yield return null;
            }
            string Probe(AgentPawnRoot pawn)=>pawn.AgentIdValue+"="+pawn.Position.ToString("R")+";root="+pawn.RouteSnapshot.Request.RequestId+";stage="+pawn.RouteSnapshot.Stage+";failure="+pawn.RouteSnapshot.Failure+";step="+pawn.RouteSnapshot.StepIndex+";phase="+pawn.RouteSnapshot.CurrentStep.Phase;
            Assert.That(_first.RouteSnapshot.StepIndex==1&&_second.RouteSnapshot.StepIndex==1,Is.True,Probe(_first)+" | "+Probe(_second));
            Stop(_first);Stop(_second);yield return Observe(.25);
            var travelling=Map.Overlay.Markers.Where(x=>x.OnEdge).ToArray();
            Assert.That(travelling.Length,Is.EqualTo(1));Assert.That(travelling[0].Identity,Is.EqualTo("1/2"));
            Assert.That(travelling[0].Members.Count,Is.EqualTo(2));
            var edge=Map.Overlay.EdgeViews["e0"];
            Assert.That(MapGraphAgentMarkerLayout.DistanceToSegment(travelling[0].Position,edge.FromPort,edge.ToPort),Is.LessThan(.001));
            Assert.That(Map.Nodes["n2"].TargetIdentities,Is.EqualTo("1/2"));ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RightClickDoesNotSubmitAndRejectedReplacementKeepsTheDisplayedRoot()
        {
            Setup();Click("n2",PointerEventData.InputButton.Right);Assert.That(Map.SubmittedCommandCount,Is.Zero);
            Click("n2");yield return RuntimeWait.Until(()=>_first.RouteSnapshot.IsActive,"有效路线");
            string id=_first.RouteSnapshot.Request.RequestId;
            _binding.TryResolveTargetForNodeId("n1",out var target);target.enabled=false;
            Click("n1");Assert.That(Map.LastSubmittedResult.Stage,Is.EqualTo(AgentRouteStage.Rejected));
            Assert.That(_first.RouteSnapshot.Request.RequestId,Is.EqualTo(id));
            target.enabled=true;ContractCompleted=true;
        }
        [UnityTest] public IEnumerator MenuCleanupRemovesCommandHudAndInstallerReenableDoesNotDuplicateIt()
        {
            Setup();var first=Map;_installer.enabled=false;yield return null;
            Assert.That(first==null,Is.True);_installer.enabled=true;yield return null;
            Assert.That(Object.FindObjectsOfType<MapGraphPresenter>().Length,Is.EqualTo(1));
            typeof(StorageScreenController).GetMethod("RemoveRaidOnlyHud",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            yield return null;Assert.That(Object.FindObjectsOfType<MapGraphPresenter>().Length,Is.Zero);
            Assert.That(_first.RouteSnapshot.IsInstalled,Is.True);ContractCompleted=true;
        }
        [UnityTest] public IEnumerator TwoProcessingMarkersRemainDistinctAndStableAfterOneAgentLeaves()
        {
            Setup(true,resource:true);Click("n2");AgentRuntimeRegistry.ActiveInstance.TrySetFocusedAgent("2");Click("n2");
            yield return Observe(.1);Map.Viewport.SetExpanded(true);yield return Observe(.06);
            Assert.That(Map.Overlay.Markers.Count,Is.EqualTo(2));
            var first=Map.Overlay.Markers.Single(x=>x.Id=="1");var second=Map.Overlay.Markers.Single(x=>x.Id=="2");
            Assert.That(first.OnEdge||second.OnEdge,Is.False);
            Assert.That(Vector2.Distance(first.Position,second.Position),Is.GreaterThanOrEqualTo(Map.Overlay.Theme.ExpandedAgentSize*1.5f));
            Assert.That(Map.Projection.AgentStates[0].AgentColor,Is.Not.EqualTo(Map.Projection.AgentStates[1].AgentColor));
            Vector2 preserved=second.Position;Object.DestroyImmediate(_first.gameObject);yield return Observe(.1);
            Assert.That(Map.Overlay.Markers.Single().Position,Is.EqualTo(preserved));ContractCompleted=true;
        }
        [UnityTest] public IEnumerator LayoutRevisionRefreshesTheViewWithoutMovingWorldTargetsOrSubmittingCommands()
        {
            Setup();yield return Observe(.06);Map.Viewport.SetExpanded(true);
            var definition=_binding.MapDefinition;var before=Map.Overlay.NodeViews["n1"].RectTransform.anchoredPosition;
            _binding.TargetBindings[1].TryGetNavigationAnchor(out var anchor);
            var nodes=definition.Nodes.Select(x=>x.NodeId=="n1"?x.WithLayout(x.Position+Vector2.right*20,x.RowId,x.ColumnId,x.PositionLocked):x).ToArray();
            definition.ApplyCommandData(definition.MapId,definition.DisplayName,definition.StartNodeId,definition.Zones,nodes,definition.Edges,definition.LayoutConstraints,definition.NavigationBake);
            _installer.TickInstallation();Map.TickPresentation();yield return Observe(.06);
            Assert.That(Map.Overlay.NodeViews["n1"].RectTransform.anchoredPosition,Is.Not.EqualTo(before));
            _binding.TargetBindings[1].TryGetNavigationAnchor(out var after);Assert.That(after,Is.EqualTo(anchor));
            Assert.That(Map.Viewport.IsExpanded,Is.True);Assert.That(Map.SubmittedCommandCount,Is.Zero);Assert.That(_first.RouteSnapshot.HasRoute,Is.False);
            ContractCompleted=true;
        }
    }
}

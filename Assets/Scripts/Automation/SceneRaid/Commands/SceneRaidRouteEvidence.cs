#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Agent.Core;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.View;
using Gameplay.Raid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>只读根路线和显示证据。普通位置最多 4 Hz；失败事件可触发有次数上限的导航诊断，不发指令。</summary>
    public sealed class SceneRaidRouteEvidence : IDisposable
    {
        [Serializable] public sealed class RootRecord
        {
            public string agent, requestId, source, stage, failure, goal, currentNode, previousNode, entryFrom;
            public string stepPhase, commandId, pendingId, pendingGoal;
            public bool installed, hasRoute, active, pending, retaliating, dead;
            public long version, graphRevision, costRevision, bindingRevision, environmentVersion, currentCostRevision;
            public int cursor, aliveMembers, health;
            public string[] nodes;
            public Vector3 position, anchor;
            public float anchorDistance3D, anchorDistancePlanar;
        }
        [Serializable] private sealed class ResultRecord
        {
            public string agent, requestId, source, goal, stage, reason, detail;
            public long version;
            public bool replan;
            public RootRecord snapshot;
        }
        [Serializable] public sealed class DisplayRecord
        {
            public string agent, requestId, goal, node, mode, edge, from, to, distanceSource, distanceFailure, markerIdentity;
            public long version, pathVersion, queries;
            public int cursor, markerMembers;
            public bool onEdge, validDistance, hasPosition, retaliating, hasMarker;
            public float progress, baselineProgress, baselineDistance, remainingDistance, tolerance;
            public double sampleTime;
            public Vector3 sampleAnchor;
            public Vector2 graphPosition, fromPort, toPort, markerPosition;
            public string[] remaining;
        }
        [Serializable] private sealed class FrameRecord
        {
            public string graphId, focusedAgent;
            public long graphRevision, bindingRevision, edgeQueries;
            public int pendingEdges, fingerprints, uiCommands;
            public bool expanded;
            public RootRecord[] roots;
            public DisplayRecord[] displays;
        }
        [Serializable] private sealed class EdgeRecord { public string id, from, to; public Vector2 a, b; }
        [Serializable] private sealed class NodeRecord { public string id, zone, kind; }
        [Serializable] private sealed class GraphRecord
        {
            public string graphId;
            public long revision;
            public NodeRecord[] nodes;
            public EdgeRecord[] edges;
        }
        private sealed class Subscription
        {
            public AgentPawnRoot Pawn;
            public Action<AgentRouteResult> Handler;
            public AgentRouteSnapshot Last;
            public bool Sampled;
        }
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly SceneRaidEnemyProcessingProbe _processingProbe = new SceneRaidEnemyProcessingProbe();
        private readonly Dictionary<AgentPawnRoot,Subscription> _pawns=new Dictionary<AgentPawnRoot,Subscription>();
        private AgentRuntimeRegistry _registry;
        private RaidMapCommandInstaller _installer;
        private double _nextSample,_nextDisplay,_nextInstaller;
        private long _graphRevision=-1;
        private string _graphId;
        private bool _disposed;
        public int ResultCount { get; private set; }
        public SceneRaidRouteEvidence(SceneRaidEvidenceWriter writer)
        {
            _writer=writer??throw new ArgumentNullException(nameof(writer));
            SceneManager.sceneLoaded+=SceneLoaded;
            AttachRegistry();
        }
        private void SceneLoaded(Scene scene,LoadSceneMode mode)=>AttachRegistry();
        private void AttachRegistry()
        {
            var registry=AgentRuntimeRegistry.ActiveInstance;
            if(registry==_registry)return;
            if(_registry!=null){_registry.AgentRegistered-=Register;_registry.AgentUnregistered-=Unregister;}
            _registry=registry;
            if(registry==null)return;
            registry.AgentRegistered+=Register;registry.AgentUnregistered+=Unregister;
            foreach(var handle in registry.RegisteredAgents)Register(handle);
        }
        private void Register(AgentRuntimeHandle handle)
        {
            var pawn=handle.PawnRoot;if(pawn==null||_pawns.ContainsKey(pawn))return;
            var subscription=new Subscription{Pawn=pawn};
            subscription.Handler=result=>RecordResult(pawn,result);
            _pawns.Add(pawn,subscription);pawn.RouteResultPublished+=subscription.Handler;
        }
        private void Unregister(AgentRuntimeHandle handle)
        {
            var pawn=handle.PawnRoot;
            if(ReferenceEquals(pawn,null)||!_pawns.TryGetValue(pawn,out var subscription))return;
            if(pawn!=null){_writer.Add("route.unregistered",JsonUtility.ToJson(Capture(pawn)));pawn.RouteResultPublished-=subscription.Handler;}
            _pawns.Remove(pawn);
        }
        private void RecordResult(AgentPawnRoot pawn,AgentRouteResult result)
        {
            ResultCount++;
            _writer.Add("route.result",JsonUtility.ToJson(new ResultRecord{
                agent=result.Request.TargetAgentId.Value,requestId=result.Request.RequestId,source=result.Request.Source.ToString(),
                goal=result.Request.TargetNodeId,stage=result.Stage.ToString(),reason=result.Reason.ToString(),detail=result.Detail,
                version=result.RouteVersion,replan=result.IsReplan,snapshot=pawn!=null?Capture(pawn):null}));
            if(result.Reason==AgentRouteFailure.Unreachable&&_installer!=null)
            {
                var probe=_processingProbe.CaptureOnce(pawn,_installer.Binding);
                if(probe!=null)_writer.Add("diagnostic.enemyProcessing",JsonUtility.ToJson(probe));
            }
        }
        public void Tick(bool force=false)
        {
            if(_disposed)return;
            double now=Time.realtimeSinceStartupAsDouble;if(!force&&now<_nextSample)return;_nextSample=now+.05;
            AttachRegistry();
            foreach(var subscription in _pawns.Values)
            {
                var pawn=subscription.Pawn;if(pawn==null)continue;
                var root=pawn.RouteSnapshot;var last=subscription.Last;
                bool changed=!subscription.Sampled||root.Request.RequestId!=last.Request.RequestId||root.RouteVersion!=last.RouteVersion||
                    root.Stage!=last.Stage||root.StepIndex!=last.StepIndex||root.CurrentStep.Phase!=last.CurrentStep.Phase||
                    root.CurrentStep.CommandId!=last.CurrentStep.CommandId||root.CurrentStep.IsRetaliating!=last.CurrentStep.IsRetaliating||
                    root.CurrentStep.AliveMembers!=last.CurrentStep.AliveMembers||root.HasPendingRequest!=last.HasPendingRequest||
                    root.PendingRequest.RequestId!=last.PendingRequest.RequestId;
                if(changed){_writer.Add("route.state",JsonUtility.ToJson(Capture(pawn)));subscription.Last=root;subscription.Sampled=true;}
            }
            if(_installer==null&&now>=_nextInstaller)
            {_nextInstaller=now+.5;_installer=UnityEngine.Object.FindObjectOfType<RaidMapCommandInstaller>();}
            if(_installer==null||_installer.Binding==null||_installer.Presentation==null)return;
            var definition=_installer.Binding.MapDefinition;if(definition==null)return;
            if(_graphId!=definition.MapId||_graphRevision!=definition.Revision)
            {
                _graphId=definition.MapId;_graphRevision=definition.Revision;
                var graph=_installer.Environments.Graph;
                _writer.Add("route.graph",JsonUtility.ToJson(new GraphRecord{graphId=_graphId,revision=_graphRevision,
                    nodes=definition.Nodes.Select(x=>new NodeRecord{id=x.NodeId,zone=x.ZoneId,kind=x.NodeKind.ToString()}).ToArray(),
                    edges=definition.Edges.Select(x=>new EdgeRecord{id=x.EdgeId,from=x.FromNodeId,to=x.ToNodeId,
                        a=graph.GetNodePosition(x.FromNodeId),b=graph.GetNodePosition(x.ToNodeId)}).ToArray()}));
            }
            if(!force&&now<_nextDisplay)return;_nextDisplay=now+.25;
            var map=_installer.Presentation;
            _writer.Add("route.frame",JsonUtility.ToJson(new FrameRecord{
                graphId=_graphId,graphRevision=definition.Revision,bindingRevision=_installer.Binding.Revision,
                edgeQueries=_installer.Environments.CalculationCount,pendingEdges=_installer.Environments.PendingEdgeCount,
                fingerprints=_installer.FingerprintCaptureCount,expanded=map.Viewport.IsExpanded,uiCommands=map.SubmittedCommandCount,
                focusedAgent=_registry!=null?_registry.FocusedAgentId.Value:string.Empty,
                roots=_pawns.Keys.Where(x=>x!=null).Select(Capture).ToArray(),displays=CaptureDisplay(map)}));
        }
        public static RootRecord Capture(AgentPawnRoot pawn)
        {
            var root=pawn.RouteSnapshot;var step=root.CurrentStep;Vector3 delta=pawn.Position-step.Anchor;
            return new RootRecord{agent=pawn.AgentIdValue,requestId=root.Request.RequestId,source=root.Request.Source.ToString(),
                stage=root.Stage.ToString(),failure=root.Failure.ToString(),goal=root.Request.TargetNodeId,currentNode=root.CurrentNodeId,
                previousNode=root.PreviousNodeId,entryFrom=root.EntryFromNodeId,stepPhase=step.Phase.ToString(),commandId=step.CommandId,
                pendingId=root.PendingRequest.RequestId,pendingGoal=root.PendingRequest.TargetNodeId,installed=root.IsInstalled,hasRoute=root.HasRoute,
                active=root.IsActive,pending=root.HasPendingRequest,retaliating=step.IsRetaliating,dead=pawn.IsDead,version=root.RouteVersion,
                graphRevision=root.GraphRevision,costRevision=root.CostRevision,bindingRevision=root.BindingRevision,
                environmentVersion=root.EnvironmentVersion,currentCostRevision=root.CurrentCostRevision,cursor=root.StepIndex,
                aliveMembers=step.AliveMembers,health=pawn.CurrentHealth,nodes=root.NodeIds?.ToArray()??Array.Empty<string>(),
                position=pawn.Position,anchor=step.Anchor,anchorDistance3D=delta.magnitude,anchorDistancePlanar=new Vector2(delta.x,delta.z).magnitude};
        }
        public static DisplayRecord[] CaptureDisplay(MapGraphPresenter map)
        {
            var records=new List<DisplayRecord>();
            foreach(var state in map.Projection.AgentStates)
            {
                var row=new DisplayRecord{agent=state.AgentId,requestId=state.RootRequestId,version=state.RouteVersion,cursor=state.StepIndex,
                    goal=state.CurrentTargetNodeId,node=state.CurrentStepNodeId,mode=state.DisplayMode.ToString(),edge=state.CurrentEdgeId,
                    from=state.CurrentEdgeFromNodeId,to=state.CurrentEdgeToNodeId,onEdge=state.IsOnEdge,validDistance=state.HasValidDistance,
                    hasPosition=state.HasGraphPosition,retaliating=state.IsRetaliating,progress=state.CurrentEdgeProgress01,
                    baselineProgress=state.CurrentEdgeSegmentStartProgress01,baselineDistance=state.BaselineDistance,
                    remainingDistance=state.RemainingDistance,tolerance=state.ArrivalTolerance,pathVersion=state.DistancePathVersion,
                    graphPosition=state.GraphPosition,remaining=state.RemainingPathNodeIds.ToArray(),
                    queries=map.Projection.GetDistanceCalculationCount(state.AgentId),distanceFailure=state.DistanceFailure};
                if(map.Projection.TryGetDistanceSample(state.AgentId,out var sample))
                {row.distanceSource=sample.Source.ToString();row.sampleTime=sample.SampleTime;row.sampleAnchor=sample.Anchor;}
                if(state.IsOnEdge&&map.Overlay.EdgeViews.TryGetValue(state.CurrentEdgeId,out var edge))
                {row.fromPort=edge.Position(state.CurrentEdgeFromNodeId,0);row.toPort=edge.Position(state.CurrentEdgeFromNodeId,1);}
                foreach(var marker in map.Overlay.Markers)
                    if(marker.Members.Any(x=>x.AgentId==state.AgentId))
                    {row.hasMarker=true;row.markerPosition=marker.Position;row.markerIdentity=marker.Identity;row.markerMembers=marker.Members.Count;break;}
                records.Add(row);
            }
            return records.ToArray();
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;SceneManager.sceneLoaded-=SceneLoaded;
            if(_registry!=null){_registry.AgentRegistered-=Register;_registry.AgentUnregistered-=Unregister;}
            foreach(var item in _pawns.Values)if(item.Pawn!=null)item.Pawn.RouteResultPublished-=item.Handler;
            _pawns.Clear();
        }
    }
}
#endif

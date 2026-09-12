using System.Collections.Generic;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>20 Hz 读取根路线，只投影真实游标和原群距离，不提交命令或自行寻路。</summary>
    public sealed class AgentGraphProjectionController : MonoBehaviour
    {
        private sealed class ProjectionCache
        {
            public readonly MapGraphRouteDistanceSampler Sampler=new MapGraphRouteDistanceSampler();
            public MapGraphAgentPositionProjection Position=new MapGraphAgentPositionProjection();
            public string SegmentId;
            public bool WasActive;
        }
        [SerializeField] private MapGraphBindingAuthoring _bindingAuthoring;
        private readonly MapGraphRuntimeState _state=new MapGraphRuntimeState();
        private readonly Dictionary<string,ProjectionCache> _caches=new Dictionary<string,ProjectionCache>();
        private readonly HashSet<string> _activeIds=new HashSet<string>();
        private readonly List<string> _removed=new List<string>();
        private SO_MapGraphDefinition _definition;
        private MapGraphService _graph;
        private double _nextSample;
        public IReadOnlyList<MapGraphAgentRuntimeState> AgentStates => _state.AgentStates;
        public int Revision { get; private set; }
        public void Initialize(SO_MapGraphDefinition definition,MapGraphBindingAuthoring binding)
        {
            if (_graph!=null && _definition==definition && _bindingAuthoring==binding) return;
            _definition=definition; _bindingAuthoring=binding; _graph=definition!=null?new MapGraphService(definition):null;
            _state.Clear(); _caches.Clear(); _nextSample=0; Revision++;
        }
        public void Tick(float deltaTime) => TickAt(Time.realtimeSinceStartupAsDouble);
        public void TickAt(double now)
        {
            if (now<_nextSample) return;
            _nextSample=now+0.05;
            if (_definition==null && _bindingAuthoring!=null) Initialize(_bindingAuthoring.MapDefinition,_bindingAuthoring);
            var registry=AgentRuntimeRegistry.ActiveInstance;
            if (_graph==null || !_graph.IsValid || registry==null) { _state.Clear(); _caches.Clear(); Revision++; return; }
            _activeIds.Clear();
            var handles=registry.RegisteredAgents;
            for(int i=0;i<handles.Count;i++)
            {
                var handle=handles[i];
                if (!handle.IsValid || handle.ReadOnly==null || (_bindingAuthoring!=null && handle.CachedTransform.gameObject.scene!=_bindingAuthoring.gameObject.scene)) continue;
                string id=handle.AgentId.Value; _activeIds.Add(id);
                var state=_state.GetOrCreateAgentState(id);
                if (!_caches.TryGetValue(id,out var cache))
                {
                    _caches.Add(id,cache=new ProjectionCache()); state.DisplayName=id; state.AgentColor=StableColor(id);
                }
                Project(state,cache,handle.ReadOnly,now);
            }
            _state.RemoveAgentsExcept(_activeIds); _removed.Clear();
            foreach(var pair in _caches) if(!_activeIds.Contains(pair.Key)) _removed.Add(pair.Key);
            for(int i=0;i<_removed.Count;i++) _caches.Remove(_removed[i]);
            Revision++;
        }
        public bool TryGetDistanceSample(string agentId,out MapGraphRouteDistanceSample sample)
        {
            if (_caches.TryGetValue(agentId,out var cache)) { sample=cache.Sampler.LastSample; return true; }
            sample=default; return false;
        }
        public long GetDistanceCalculationCount(string agentId) => _caches.TryGetValue(agentId,out var cache)?cache.Sampler.CalculationCount:0;
        private void Project(MapGraphAgentRuntimeState state,ProjectionCache cache,IAgentReadOnly agent,double now)
        {
            var root=agent.RouteSnapshot; var step=root.CurrentStep;
            bool changed=state.RootRequestId!=root.Request.RequestId || state.RouteVersion!=root.RouteVersion || state.StepIndex!=root.StepIndex || cache.WasActive!=root.IsActive;
            if (changed)
            {
                state.RootRequestId=root.Request.RequestId; state.RouteVersion=root.RouteVersion; state.StepIndex=root.StepIndex;
                state.CopyRemaining(root.NodeIds,root.StepIndex,root.IsActive);
                cache.SegmentId=state.RootRequestId+":"+state.RouteVersion+":"+state.StepIndex;
            }
            cache.WasActive=root.IsActive;
            state.IsPlayerRoute=root.Request.Source==AgentRouteSource.Player;
            state.IsRetaliating=step.IsRetaliating;
            state.HasPendingRoute=root.HasPendingRequest;
            state.PendingTargetNodeId=root.HasPendingRequest?root.PendingRequest.TargetNodeId:string.Empty;
            state.CurrentTargetNodeId=root.IsActive?root.Request.TargetNodeId:string.Empty;
            state.CurrentStepNodeId=root.CurrentNodeId;
            state.PreviousNodeId=root.PreviousNodeId;
            state.DisplayMode=ResolveMode(root,agent.IsDead);
            if (root.IsActive && step.Phase==AgentClusterStepPhase.Travelling &&
                _graph.TryGetEdgeBetween(root.PreviousNodeId,root.CurrentNodeId,out var edge))
            {
                state.CurrentNodeId=string.Empty; state.CurrentEdgeId=edge.EdgeId;
                state.CurrentEdgeFromNodeId=root.PreviousNodeId; state.CurrentEdgeToNodeId=root.CurrentNodeId;
                state.CurrentEdgeLengthUnits=edge.LengthUnits; state.DisplayMode=MapGraphAgentDisplayMode.Travelling;
                var distance=cache.Sampler.Sample(agent,root,now);
                state.CurrentEdgeProgress01=cache.Position.Update(cache.SegmentId,edge.EdgeId,root.PreviousNodeId,root.CurrentNodeId,
                    distance.IsValid,distance.RemainingDistance,distance.ArrivalTolerance,distance.PathVersion);
                state.HasValidDistance=distance.IsValid; state.RemainingDistance=distance.RemainingDistance;
                state.ArrivalTolerance=distance.ArrivalTolerance; state.DistanceFailure=distance.Failure; state.DistancePathVersion=distance.PathVersion;
                state.BaselineDistance=cache.Position.BaselineDistance; state.CurrentEdgeSegmentStartProgress01=cache.Position.BaselineProgress;
                state.GraphPosition=MapGraphAgentPositionProjection.PositionOnSegment(_graph.GetNodePosition(root.PreviousNodeId),_graph.GetNodePosition(root.CurrentNodeId),state.CurrentEdgeProgress01);
                state.HasGraphPosition=true; return;
            }
            // 处理时结束上一段投影。下一次出发同一条边也必须重新冻结基准。
            if (state.IsOnEdge) cache.Position=new MapGraphAgentPositionProjection();
            state.ClearEdgeTravel();
            if (_graph.TryGetNode(root.CurrentNodeId,out _)) state.CurrentNodeId=root.CurrentNodeId;
            else if (string.IsNullOrEmpty(state.CurrentNodeId) && _bindingAuthoring!=null &&
                _bindingAuthoring.TryFindNearestBoundNode(agent.Position,2.5f,out string nearby)) state.CurrentNodeId=nearby;
            state.HasGraphPosition=_graph.TryGetNode(state.CurrentNodeId,out _);
            if (state.HasGraphPosition) state.GraphPosition=_graph.GetNodePosition(state.CurrentNodeId);
        }
        private static MapGraphAgentDisplayMode ResolveMode(AgentRouteSnapshot root,bool dead)
        {
            if(dead || root.Stage==AgentRouteStage.Dead) return MapGraphAgentDisplayMode.Dead;
            if(root.Stage==AgentRouteStage.Extracted) return MapGraphAgentDisplayMode.Extracted;
            if(!root.IsActive) return !root.HasRoute?MapGraphAgentDisplayMode.Idle :
                root.Stage==AgentRouteStage.Completed?MapGraphAgentDisplayMode.Completed :
                root.Stage==AgentRouteStage.Failed?MapGraphAgentDisplayMode.Failed:MapGraphAgentDisplayMode.Idle;
            switch(root.CurrentStep.Phase)
            {
                case AgentClusterStepPhase.Travelling:return MapGraphAgentDisplayMode.Entering;
                case AgentClusterStepPhase.WaitingForInventory:
                case AgentClusterStepPhase.WaitingForMember:
                case AgentClusterStepPhase.WaitingSpawn:return MapGraphAgentDisplayMode.Waiting;
                case AgentClusterStepPhase.Extracting:return MapGraphAgentDisplayMode.Extracting;
                default:return MapGraphAgentDisplayMode.Processing;
            }
        }
        private static Color StableColor(string id)
        {
            int hash=17; for(int i=0;i<id.Length;i++) hash=hash*31+id[i];
            return Color.HSVToRGB(Mathf.Abs(hash%360)/360f,0.45f,0.95f);
        }
    }
}

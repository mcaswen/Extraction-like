using System.Collections.Generic;
using System.Text;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.View
{
    /// <summary>地图输入和展示事实汇总。命令由 Router 执行，View 只消费缓存。</summary>
    public sealed class MapGraphPresenter : MonoBehaviour
    {
        public sealed class NodeState
        {
            public bool Available,Completed,Pending;
            public string TargetIdentities=string.Empty;
            internal readonly StringBuilder Identities=new StringBuilder();
        }
        [SerializeField] private SO_MapGraphTheme _theme;
        [SerializeField] private AgentGraphProjectionController _projection;
        [SerializeField] private MapGraphOverlayController _overlay;
        [SerializeField] private MapGraphViewport _viewport;
        private readonly Dictionary<string,NodeState> _nodes=new Dictionary<string,NodeState>();
        private readonly HashSet<string> _routeEdges=new HashSet<string>(),_focusedEdges=new HashSet<string>();
        private MapGraphRouteEnvironmentService _environments;
        private MapGraphService _graph;
        private MapGraphBindingAuthoring _binding;
        private SO_MapGraphDefinition _definition;
        private long _definitionRevision;
        private string _hoveredNode;
        private double _nextFacts;
        private int _lastProjectionRevision=-1,_lastViewportRevision=-1;
        public AgentGraphProjectionController Projection=>_projection;
        public MapGraphOverlayController Overlay=>_overlay;
        public MapGraphViewport Viewport=>_viewport;
        public IReadOnlyDictionary<string,NodeState> Nodes=>_nodes;
        public int SubmittedCommandCount { get; private set; }
        public AgentRouteResult LastSubmittedResult { get; private set; }
        public MapGraphBindingAuthoring Binding=>_binding;
        public void Initialize(MapGraphBindingAuthoring binding,MapGraphRouteEnvironmentService environments)
        {
            _binding=binding; _environments=environments; _graph=environments.Graph;
            _definition=binding.MapDefinition;_definitionRevision=_definition.Revision;
            _nodes.Clear(); foreach(var node in binding.MapDefinition.Nodes)_nodes.Add(node.NodeId,new NodeState());
            _projection.Initialize(binding.MapDefinition,binding);
            _overlay.Initialize(binding.MapDefinition,_theme,SubmitNode,HoverNode);
            _viewport.Initialize(_overlay); _nextFacts=0; TickPresentation();
        }
        private void Update() => TickPresentation();
        public void TickPresentation()
        {
            if(_environments==null)return;
            if(_binding!=null&&_binding.MapDefinition!=null&&(_definition!=_binding.MapDefinition||_definitionRevision!=_binding.MapDefinition.Revision))
            {bool expanded=_viewport.IsExpanded;Initialize(_binding,_environments);_viewport.SetExpanded(expanded);return;}
            _projection.Tick(0);
            bool viewportChanged=_lastViewportRevision!=_viewport.Revision;
            if(!viewportChanged&&_lastProjectionRevision==_projection.Revision)return;
            _lastProjectionRevision=_projection.Revision; _lastViewportRevision=_viewport.Revision;
            if(Time.realtimeSinceStartupAsDouble>=_nextFacts)
            {
                _nextFacts=Time.realtimeSinceStartupAsDouble+.25;
                foreach(var pair in _nodes)
                {
                    if(_environments.Targets!=null&&_environments.Targets.TryGetFacts(pair.Key,out var facts))
                    { pair.Value.Available=facts.CanTraverse; pair.Value.Completed=facts.Status==AgentRouteTargetStatus.Completed; }
                    else { pair.Value.Available=false; pair.Value.Completed=false; }
                }
            }
            string focused=FocusedId();
            foreach(var node in _nodes.Values) {node.Identities.Clear();node.Pending=false;}
            _routeEdges.Clear();_focusedEdges.Clear();
            var states=_projection.AgentStates;
            for(int i=0;i<states.Count;i++)
            {
                var state=states[i];
                if(_nodes.TryGetValue(state.CurrentTargetNodeId??string.Empty,out var goal))
                { if(goal.Identities.Length>0)goal.Identities.Append('/'); goal.Identities.Append(state.DisplayName); }
                if(state.HasPendingRoute&&_nodes.TryGetValue(state.PendingTargetNodeId??string.Empty,out var pending)) pending.Pending=true;
                if(state.IsOnEdge) AddEdge(state.CurrentEdgeId,state.AgentId==focused);
                var sequence=state.RemainingPathNodeIds;
                for(int n=1;n<sequence.Count;n++) if(_graph.TryGetEdgeBetween(sequence[n-1],sequence[n],out var edge)) AddEdge(edge.EdgeId,state.AgentId==focused);
            }
            foreach(var node in _nodes.Values)
            {
                // 稳定帧不新建 TMP 文本字符串。
                if(!Matches(node.Identities,node.TargetIdentities)) node.TargetIdentities=node.Identities.ToString();
            }
            _overlay.Refresh(states,_nodes,_routeEdges,_focusedEdges,focused);
            RefreshDetail(focused,states);
        }
        public void SubmitNode(string nodeId)
        {
            if(_environments==null||!isActiveAndEnabled)return;
            SubmittedCommandCount++;
            LastSubmittedResult=AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest(nodeId,AgentRouteSource.Player));
        }
        private void HoverNode(string id,bool enter) { if(enter)_hoveredNode=id; else if(_hoveredNode==id)_hoveredNode=null; }
        private void AddEdge(string id,bool focused) {_routeEdges.Add(id);if(focused)_focusedEdges.Add(id);}
        private void RefreshDetail(string focused,IReadOnlyList<MapGraphAgentRuntimeState> states)
        {
            if(!string.IsNullOrEmpty(_hoveredNode)&&_graph.TryGetNode(_hoveredNode,out var node))
            {
                string zone=_graph.TryGetZone(node.ZoneId,out var area)?area.DisplayName+" · ":string.Empty;
                string kind=node.NodeKind==MapGraphNodeKind.Resource?"资源群":node.NodeKind==MapGraphNodeKind.Extraction?"撤离点":"敌人群";
                var facts=_nodes[_hoveredNode];
                _overlay.SetDetail(zone+kind+(facts.Completed?" · 已完成，前往此处":!facts.Available?" · 暂不可用":" · 点击下达路线")); return;
            }
            for(int i=0;i<states.Count;i++)if(states[i].AgentId==focused)
            {
                var state=states[i]; string source=state.IsPlayerRoute?"玩家":"自主";
                _overlay.SetDetail("角色 "+state.DisplayName+"  ·  "+source+"  ·  "+StatusText(state)); return;
            }
            _overlay.SetDetail("选择角色后，点击群下达路线");
        }
        private static string FocusedId()
        {
            var registry=AgentRuntimeRegistry.ActiveInstance;
            return registry!=null&&registry.TryGetFocusedHandle(out var handle)?handle.AgentId.Value:string.Empty;
        }
        private static bool Matches(StringBuilder builder,string text)
        { if(builder.Length!=text.Length)return false;for(int i=0;i<text.Length;i++)if(builder[i]!=text[i])return false;return true; }
        public static string StatusText(MapGraphAgentRuntimeState state)
        {
            if(state.IsRetaliating)return "反击，随后恢复路线";
            if(state.HasPendingRoute)return "规划中";
            switch(state.DisplayMode)
            {
                case MapGraphAgentDisplayMode.Entering:return "进入路线";
                case MapGraphAgentDisplayMode.Travelling:return state.HasValidDistance?"行进中":"等待导航";
                case MapGraphAgentDisplayMode.Processing:return "处理中";
                case MapGraphAgentDisplayMode.Waiting:return "等待处理";
                case MapGraphAgentDisplayMode.Extracting:return "撤离中";
                case MapGraphAgentDisplayMode.Completed:return "已到达";
                case MapGraphAgentDisplayMode.Failed:return "路线中断";
                case MapGraphAgentDisplayMode.Dead:return "已战死";
                case MapGraphAgentDisplayMode.Extracted:return "已撤离";
                default:return "待命";
            }
        }
    }
}

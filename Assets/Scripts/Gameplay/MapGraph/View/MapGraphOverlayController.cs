using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>只装配和刷新地图子视图，输入及执行事实由 Presenter 提供。</summary>
    public sealed class MapGraphOverlayController : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private RectTransform _zonesRoot,_edgesRoot,_nodesRoot,_agentsRoot;
        [SerializeField] private MapGraphZoneView _zoneViewPrefab;
        [SerializeField] private MapGraphNodeView _nodeViewPrefab;
        [SerializeField] private MapGraphEdgeView _edgeViewPrefab;
        [SerializeField] private MapGraphAgentView _agentViewPrefab;
        [SerializeField] private TMP_Text _title,_hint,_legend,_detail;
        private readonly Dictionary<string,MapGraphNodeView> _nodes=new Dictionary<string,MapGraphNodeView>();
        private readonly Dictionary<string,MapGraphEdgeView> _edges=new Dictionary<string,MapGraphEdgeView>();
        private readonly Dictionary<string,MapGraphZoneView> _zones=new Dictionary<string,MapGraphZoneView>();
        private readonly Dictionary<string,MapGraphAgentView> _agents=new Dictionary<string,MapGraphAgentView>();
        private readonly List<Rect> _nameBounds=new List<Rect>();
        private readonly MapGraphAgentMarkerLayout _markerLayout=new MapGraphAgentMarkerLayout();
        private readonly HashSet<string> _visible=new HashSet<string>();
        private readonly HashSet<string> _aliveAgentIds=new HashSet<string>();
        private readonly List<string> _removedAgentViews=new List<string>();
        private SO_MapGraphDefinition _definition;
        private SO_MapGraphTheme _theme;
        private MapGraphService _graph;
        private Rect _bounds;
        private Vector2 _viewSize,_pan;
        private float _scale,_zoom=1;
        private bool _expanded;
        public IReadOnlyDictionary<string,MapGraphNodeView> NodeViews=>_nodes;
        public IReadOnlyDictionary<string,MapGraphEdgeView> EdgeViews=>_edges;
        public IReadOnlyDictionary<string,MapGraphZoneView> ZoneViews=>_zones;
        public IReadOnlyDictionary<string,MapGraphAgentView> AgentViews=>_agents;
        public IReadOnlyList<MapGraphAgentMarkerLayout.Marker> Markers=>_markerLayout.Markers;
        public SO_MapGraphTheme Theme=>_theme;
        public int BuildCount { get; private set; }
        public RectTransform Content=>_content;
        public bool IsExpanded=>_expanded;
        public void Initialize(SO_MapGraphDefinition definition,SO_MapGraphTheme theme,Action<string> click,Action<string,bool> hover)
        {
            _definition=definition; _theme=theme; _graph=new MapGraphService(definition);
            _backgroundImage.color=theme.Background;
            foreach(var label in new[]{_title,_hint,_legend,_detail}) { label.font=theme.Font; label.raycastTarget=false; label.color=theme.MutedText; }
            _title.text="区域指挥"; _title.color=theme.Text;
            _legend.text="资源   /   敌人   /   撤离";
            ClearChildren(_zonesRoot); ClearChildren(_edgesRoot); ClearChildren(_nodesRoot); ClearChildren(_agentsRoot);
            _zones.Clear(); _edges.Clear(); _nodes.Clear(); _agents.Clear();
            bool first=true;
            foreach(var zone in definition.Zones)
            {
                var view=Instantiate(_zoneViewPrefab,_zonesRoot); view.Initialize(zone,theme); _zones.Add(zone.ZoneId,view);
                if(first) { _bounds=zone.Bounds; first=false; }
                else _bounds=Rect.MinMaxRect(Mathf.Min(_bounds.xMin,zone.Bounds.xMin),Mathf.Min(_bounds.yMin,zone.Bounds.yMin),Mathf.Max(_bounds.xMax,zone.Bounds.xMax),Mathf.Max(_bounds.yMax,zone.Bounds.yMax));
            }
            foreach(var node in definition.Nodes) { var view=Instantiate(_nodeViewPrefab,_nodesRoot); view.Initialize(node,theme,click,hover); _nodes.Add(node.NodeId,view); }
            foreach(var edge in definition.Edges) { var view=Instantiate(_edgeViewPrefab,_edgesRoot); view.Initialize(edge,theme); _edges.Add(edge.EdgeId,view); }
            if(first) _bounds=new Rect(-200,-200,400,400);
            BuildCount++; SetViewport(_viewSize==Vector2.zero?new Vector2(380,330):_viewSize,_expanded,_zoom,_pan);
        }
        public void SetViewport(Vector2 viewSize,bool expanded,float zoom,Vector2 pan)
        {
            _viewSize=viewSize; _expanded=expanded; _zoom=zoom; _pan=pan;
            if(_definition==null) return;
            _scale=Mathf.Min(Mathf.Max(1,viewSize.x-40)/Mathf.Max(1,_bounds.width),Mathf.Max(1,viewSize.y-40)/Mathf.Max(1,_bounds.height))*zoom;
            _nameBounds.Clear(); float nodeSize=expanded?_theme.ExpandedNodeSize:_theme.CompactNodeSize;
            foreach(var zone in _definition.Zones)
            {
                Vector2 center=MapPosition(zone.Bounds.center); Vector2 safe=zone.NameSafeSize*_scale;
                _zones[zone.ZoneId].Layout(center,zone.Bounds.size*_scale,safe,expanded);
                _nameBounds.Add(new Rect(center-safe*.5f,safe));
            }
            foreach(var node in _definition.Nodes) _nodes[node.NodeId].Layout(MapPosition(_graph.GetNodePosition(node.NodeId)),nodeSize,expanded);
            foreach(var edge in _definition.Edges)
                _edges[edge.EdgeId].Layout(MapPosition(_graph.GetNodePosition(edge.FromNodeId)),MapPosition(_graph.GetNodePosition(edge.ToNodeId)),
                    edge.WidthOverride>0?Mathf.Max(1,edge.WidthOverride*_scale):_theme.EdgeWidth,
                    nodeSize*.5f+edge.FromInset*_scale,nodeSize*.5f+edge.ToInset*_scale);
            _hint.text=expanded?"M 收起 · H 隐藏 · 滚轮缩放 · 拖动平移":"M 展开 · H 隐藏";
            _title.fontSize=expanded?20:14; _hint.fontSize=expanded?12:10; _legend.fontSize=expanded?12:10; _detail.fontSize=expanded?12:10;
            _detail.rectTransform.sizeDelta=new Vector2(viewSize.x-36,20);
        }
        public Vector2 MapPosition(Vector2 point)=>(point-_bounds.center)*_scale+_pan;
        public void Refresh(IReadOnlyList<MapGraphAgentRuntimeState> states,IReadOnlyDictionary<string,MapGraphPresenter.NodeState> nodes,
            ISet<string> routeEdges,ISet<string> focusedEdges,string focusedId)
        {
            foreach(var pair in _nodes)
                if(nodes.TryGetValue(pair.Key,out var state)) pair.Value.Refresh(state.Available,state.Completed,state.TargetIdentities,state.Pending);
            foreach(var pair in _edges) pair.Value.Refresh(routeEdges.Contains(pair.Key),focusedEdges.Contains(pair.Key));
            float size=_expanded?_theme.ExpandedAgentSize:_theme.CompactAgentSize;
            _markerLayout.Build(states,_nodes,_edges,_nameBounds,size,focusedId); _visible.Clear();
            foreach(var marker in _markerLayout.Markers)
            {
                _visible.Add(marker.Id);
                if(!_agents.TryGetValue(marker.Id,out var view)) { view=Instantiate(_agentViewPrefab,_agentsRoot); view.Initialize(marker.Id,_theme); _agents.Add(marker.Id,view); }
                view.gameObject.SetActive(true);
                var first=marker.Members[0];
                bool retaliation=false,waiting=false;
                foreach(var member in marker.Members){retaliation|=member.IsRetaliating;waiting|=member.DisplayMode==MapGraphAgentDisplayMode.Waiting;}
                view.Refresh(marker.Position,size*(marker.Members.Count>1?1.3f:1),marker.Identity,MapGraphPresenter.StatusText(first),first.AgentColor,marker.Focused,_expanded,retaliation,waiting);
            }
            _aliveAgentIds.Clear();foreach(var state in states)_aliveAgentIds.Add(state.AgentId);
            _removedAgentViews.Clear();
            foreach(var pair in _agents)
            {
                if(!_visible.Contains(pair.Key))pair.Value.gameObject.SetActive(false);
                if(!_aliveAgentIds.Contains(pair.Key))_removedAgentViews.Add(pair.Key);
            }
            foreach(string id in _removedAgentViews){Destroy(_agents[id].gameObject);_agents.Remove(id);}
        }
        public void SetDetail(string text) => _detail.text=text??string.Empty;
        private static void ClearChildren(Transform root)
        { for(int i=root.childCount-1;i>=0;i--) {var child=root.GetChild(i).gameObject; child.SetActive(false); Destroy(child);} }
    }
}

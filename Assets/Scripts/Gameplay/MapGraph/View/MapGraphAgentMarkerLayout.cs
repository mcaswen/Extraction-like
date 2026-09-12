using System;
using System.Collections.Generic;
using System.Text;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.View
{
    /// <summary>屏幕空间角色排布。行进重叠合并身份，处理槽位避让图标、名称和线路。</summary>
    public sealed class MapGraphAgentMarkerLayout
    {
        public sealed class Marker
        {
            public string Id;
            public readonly List<MapGraphAgentRuntimeState> Members=new List<MapGraphAgentRuntimeState>();
            public Vector2 Position;
            public bool OnEdge,Focused;
            public string Identity=string.Empty;
            private readonly List<string> _identityIds=new List<string>();
            private readonly StringBuilder _text=new StringBuilder();
            internal void RefreshIdentity()
            {
                bool changed=_identityIds.Count!=Members.Count;
                for(int i=0;!changed&&i<Members.Count;i++) changed=_identityIds[i]!=Members[i].DisplayName;
                if(!changed) return;
                _identityIds.Clear(); _text.Clear();
                for(int i=0;i<Members.Count;i++) { if(i>0)_text.Append('/'); _text.Append(Members[i].DisplayName); _identityIds.Add(Members[i].DisplayName); }
                Identity=_text.ToString();
            }
        }
        private sealed class Slot { public string Node; public int Index; }
        private readonly List<MapGraphAgentRuntimeState> _ordered=new List<MapGraphAgentRuntimeState>();
        private readonly Dictionary<string,Marker> _cache=new Dictionary<string,Marker>();
        private readonly Dictionary<string,Slot> _slots=new Dictionary<string,Slot>();
        private readonly List<Marker> _markers=new List<Marker>();
        private readonly HashSet<string> _alive=new HashSet<string>();
        private readonly List<string> _removed=new List<string>();
        private readonly List<Vector2> _reservedProcessing=new List<Vector2>();
        private int[] _parents=Array.Empty<int>();
        private Vector2[] _positions=Array.Empty<Vector2>();
        private static readonly Vector2[] Directions={new Vector2(1,1).normalized,new Vector2(-1,1).normalized,new Vector2(1,-1).normalized,new Vector2(-1,-1).normalized,Vector2.up,Vector2.right,Vector2.down,Vector2.left};
        public IReadOnlyList<Marker> Markers=>_markers;
        public void Build(IReadOnlyList<MapGraphAgentRuntimeState> states,IReadOnlyDictionary<string,MapGraphNodeView> nodes,
            IReadOnlyDictionary<string,MapGraphEdgeView> edges,IReadOnlyList<Rect> nameBounds,float agentSize,string focusedId)
        {
            _ordered.Clear(); _alive.Clear(); _markers.Clear();
            for(int i=0;i<states.Count;i++)
            {
                var state=states[i]; _alive.Add(state.AgentId);
                if(state.HasGraphPosition && state.DisplayMode!=MapGraphAgentDisplayMode.Dead && state.DisplayMode!=MapGraphAgentDisplayMode.Extracted) _ordered.Add(state);
            }
            Prune(_slots); Prune(_cache);
            _ordered.Sort((a,b)=>string.CompareOrdinal(a.AgentId,b.AgentId));
            int count=_ordered.Count;
            if(_parents.Length<count) { _parents=new int[count*2]; _positions=new Vector2[count*2]; }
            for(int i=0;i<count;i++)
            {
                var state=_ordered[i]; _parents[i]=i;
                if(state.IsOnEdge && edges.TryGetValue(state.CurrentEdgeId,out var edge) && edge.GeometryValid)
                    _positions[i]=edge.Position(state.CurrentEdgeFromNodeId,state.CurrentEdgeProgress01);
                else if(nodes.TryGetValue(state.CurrentNodeId,out var node))
                    _positions[i]=ProcessingPosition(node,GetSlot(state),nodes,edges,nameBounds,agentSize);
                else _positions[i]=state.GraphPosition;
            }
            // 同一真实边上足够接近的核心组成一个标记；不同方向使用同一屏幕位置比较。
            for(int i=0;i<count;i++) for(int j=0;j<i;j++)
                if(_ordered[i].IsOnEdge && _ordered[i].CurrentEdgeId==_ordered[j].CurrentEdgeId &&
                    Vector2.Distance(_positions[i],_positions[j])<agentSize*1.2f)
                {int a=Root(i),b=Root(j);_parents[Mathf.Max(a,b)]=Mathf.Min(a,b);}
            for(int i=0;i<count;i++)
            {
                int leader=Root(i); string id=_ordered[leader].AgentId;
                if(!_cache.TryGetValue(id,out var marker)) _cache.Add(id,marker=new Marker{Id=id});
                if(leader==i) { marker.Members.Clear(); marker.Position=Vector2.zero; marker.OnEdge=_ordered[i].IsOnEdge; marker.Focused=false; _markers.Add(marker); }
                marker.Members.Add(_ordered[i]); marker.Position+=_positions[i]; marker.Focused|=_ordered[i].AgentId==focusedId;
            }
            for(int i=0;i<_markers.Count;i++) { var marker=_markers[i]; marker.Position/=marker.Members.Count; marker.RefreshIdentity(); }
        }
        private int Root(int i) { while(_parents[i]!=i)i=_parents[i]; return i; }
        private void Prune<T>(Dictionary<string,T> cache)
        { _removed.Clear(); foreach(var pair in cache) if(!_alive.Contains(pair.Key)) _removed.Add(pair.Key); foreach(string id in _removed)cache.Remove(id); }
        private int GetSlot(MapGraphAgentRuntimeState state)
        {
            if(_slots.TryGetValue(state.AgentId,out var current) && current.Node==state.CurrentNodeId) return current.Index;
            int index=0;
            while(true) { bool used=false; foreach(var slot in _slots.Values) if(slot.Node==state.CurrentNodeId&&slot.Index==index) { used=true; break; } if(!used)break; index++; }
            _slots[state.AgentId]=new Slot{Node=state.CurrentNodeId,Index=index}; return index;
        }
        private Vector2 ProcessingPosition(MapGraphNodeView node,int slot,
            IReadOnlyDictionary<string,MapGraphNodeView> nodes,IReadOnlyDictionary<string,MapGraphEdgeView> edges,IReadOnlyList<Rect> names,float agentSize)
        {
            _reservedProcessing.Clear();
            // 保留空下来的较早槽位，注销其他角色不会让已有角色换位。
            for(int i=0;i<=slot;i++)_reservedProcessing.Add(ChooseProcessingPosition(node,i,nodes,edges,names,agentSize,_reservedProcessing));
            return _reservedProcessing[slot];
        }
        private static Vector2 ChooseProcessingPosition(MapGraphNodeView node,int slot,
            IReadOnlyDictionary<string,MapGraphNodeView> nodes,IReadOnlyDictionary<string,MapGraphEdgeView> edges,IReadOnlyList<Rect> names,float agentSize,IReadOnlyList<Vector2> occupied)
        {
            Vector2 center=node.RectTransform.anchoredPosition;
            float radius=node.VisualRadius+agentSize*.5f+5;
            Vector2 best=center+Directions[slot%8]*radius; float bestScore=float.PositiveInfinity;
            for(int ring=0;ring<3;ring++) for(int candidate=0;candidate<8;candidate++)
            {
                int direction=(slot+candidate)%8;
                Vector2 p=center+Directions[direction]*(radius+agentSize*(ring+slot/8));
                float penalty=candidate*.01f+ring*.1f;
                var footprint=new Rect(p-Vector2.one*(agentSize*.75f),Vector2.one*(agentSize*1.5f));
                for(int i=0;i<occupied.Count;i++)if(Vector2.Distance(p,occupied[i])<agentSize*1.5f)penalty+=1000;
                for(int i=0;i<names.Count;i++) if(footprint.Overlaps(names[i]))penalty+=100;
                foreach(var other in nodes.Values)
                {
                    if(Vector2.Distance(p,other.RectTransform.anchoredPosition)<other.VisualRadius+agentSize*.75f)penalty+=30;
                    if(other.HasTargetLabel&&footprint.Overlaps(other.TargetLabelBounds))penalty+=100;
                }
                foreach(var edge in edges.Values) if(edge.GeometryValid&&DistanceToSegment(p,edge.FromPort,edge.ToPort)<agentSize*.55f)penalty+=10;
                if(penalty<bestScore) {bestScore=penalty;best=p;}
                if(penalty<.01f) return p;
            }
            return best;
        }
        public static float DistanceToSegment(Vector2 p,Vector2 a,Vector2 b)
        { Vector2 d=b-a; float t=d.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0; return Vector2.Distance(p,a+d*t); }
    }
}

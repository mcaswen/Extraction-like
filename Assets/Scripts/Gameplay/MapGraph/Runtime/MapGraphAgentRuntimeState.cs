using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Runtime
{
    public enum MapGraphAgentDisplayMode { Unlocalized, Entering, Travelling, Processing, Waiting, Extracting, Idle, Completed, Failed, Dead, Extracted }

    /// <summary>执行事实的展示缓存。只有 Binding 投影器写入，视图不能推进路线。</summary>
    public sealed class MapGraphAgentRuntimeState
    {
        private readonly List<string> _remaining = new List<string>();
        private readonly IReadOnlyList<string> _remainingView;
        public MapGraphAgentRuntimeState(string agentId) { AgentId=agentId??string.Empty; _remainingView=_remaining.AsReadOnly(); }
        public string AgentId { get; }
        public string DisplayName { get; internal set; } = "Agent";
        public Color AgentColor { get; internal set; } = Color.white;
        public string RootRequestId { get; internal set; } = string.Empty;
        public long RouteVersion { get; internal set; }
        public int StepIndex { get; internal set; } = -1;
        public string CurrentStepNodeId { get; internal set; } = string.Empty;
        public string CurrentNodeId { get; internal set; } = string.Empty;
        public string PreviousNodeId { get; internal set; } = string.Empty;
        public string CurrentTargetNodeId { get; internal set; } = string.Empty;
        public string PendingTargetNodeId { get; internal set; } = string.Empty;
        public bool IsPlayerRoute { get; internal set; }
        public bool IsRetaliating { get; internal set; }
        public bool HasPendingRoute { get; internal set; }
        public MapGraphAgentDisplayMode DisplayMode { get; internal set; }
        public IReadOnlyList<string> RemainingPathNodeIds => _remainingView;
        public string CurrentEdgeId { get; internal set; } = string.Empty;
        // 两端按真实执行方向排列，进度始终从来源端口 0 到目标端口 1。
        public string CurrentEdgeFromNodeId { get; internal set; } = string.Empty;
        public string CurrentEdgeToNodeId { get; internal set; } = string.Empty;
        public float CurrentEdgeLengthUnits { get; internal set; }
        public float CurrentEdgeProgress01 { get; internal set; }
        public float CurrentEdgeSegmentStartProgress01 { get; internal set; }
        public float CurrentEdgeTargetProgress01 => 1;
        public Vector2 GraphPosition { get; internal set; }
        public bool IsOnEdge => !string.IsNullOrEmpty(CurrentEdgeId);
        public bool HasGraphPosition { get; internal set; }
        public bool HasValidDistance { get; internal set; }
        public float RemainingDistance { get; internal set; }
        public float ArrivalTolerance { get; internal set; }
        public float BaselineDistance { get; internal set; }
        public string DistanceFailure { get; internal set; } = string.Empty;
        public long DistancePathVersion { get; internal set; }
        internal void CopyRemaining(IReadOnlyList<string> nodes,int cursor,bool active)
        {
            _remaining.Clear();
            if (active && nodes!=null) for(int i=Mathf.Max(0,cursor);i<nodes.Count;i++) _remaining.Add(nodes[i]);
        }
        internal void ClearEdgeTravel()
        {
            CurrentEdgeId=CurrentEdgeFromNodeId=CurrentEdgeToNodeId=string.Empty;
            CurrentEdgeLengthUnits=CurrentEdgeProgress01=CurrentEdgeSegmentStartProgress01=0;
            HasValidDistance=false;
        }
    }
}

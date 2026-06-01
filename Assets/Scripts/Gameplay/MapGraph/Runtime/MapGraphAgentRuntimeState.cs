using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>
    /// 单个 Agent 在抽象图 UI 上的运行时投影状态
    /// 只记录图上表现所需的节点、边、目标和显示位置，不承载真实战斗状态
    /// </summary>
    public sealed class MapGraphAgentRuntimeState
    {
        private readonly List<string> _remainingPathNodeIds = new List<string>();

        public MapGraphAgentRuntimeState(string agentId)
        {
            AgentId = agentId ?? string.Empty;
        }

        /// <summary>
        /// Agent 稳定 ID
        /// </summary>
        public string AgentId { get; }

        /// <summary>
        /// UI 显示名称
        /// </summary>
        public string DisplayName { get; set; } = "Agent";

        /// <summary>
        /// UI 显示颜色
        /// </summary>
        public Color AgentColor { get; set; } = Color.white;

        /// <summary>
        /// 当前所在节点 ID
        /// 为空表示 Agent 正在边上移动
        /// </summary>
        public string CurrentNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 最近一次离开的节点 ID
        /// </summary>
        public string PreviousNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 当前图上目标节点 ID
        /// </summary>
        public string CurrentTargetNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 当前路径剩余节点序列
        /// </summary>
        public List<string> RemainingPathNodeIds => _remainingPathNodeIds;

        /// <summary>
        /// 当前所在边 ID
        /// </summary>
        public string CurrentEdgeId { get; set; } = string.Empty;

        /// <summary>
        /// 当前边起点节点 ID
        /// </summary>
        public string CurrentEdgeFromNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 当前边终点节点 ID
        /// </summary>
        public string CurrentEdgeToNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 当前边长度
        /// </summary>
        public float CurrentEdgeLengthUnits { get; set; }

        /// <summary>
        /// Agent 在当前边上的进度
        /// </summary>
        public float CurrentEdgeProgress01 { get; set; }

        /// <summary>
        /// 本段移动的起始进度
        /// </summary>
        public float CurrentEdgeSegmentStartProgress01 { get; set; }

        /// <summary>
        /// 本段移动的目标进度
        /// </summary>
        public float CurrentEdgeTargetProgress01 { get; set; } = 1f;

        /// <summary>
        /// Agent 在抽象图坐标系中的当前位置
        /// </summary>
        public Vector2 GraphPosition { get; set; }

        /// <summary>
        /// 当前是否处在边上
        /// </summary>
        public bool IsOnEdge => !string.IsNullOrWhiteSpace(CurrentEdgeId);

        /// <summary>
        /// 清空边上移动状态，把 Agent 收回节点态
        /// </summary>
        public void ClearEdgeTravel()
        {
            CurrentEdgeId = string.Empty;
            CurrentEdgeFromNodeId = string.Empty;
            CurrentEdgeToNodeId = string.Empty;
            CurrentEdgeLengthUnits = 0f;
            CurrentEdgeProgress01 = 0f;
            CurrentEdgeSegmentStartProgress01 = 0f;
            CurrentEdgeTargetProgress01 = 1f;
        }
    }
}

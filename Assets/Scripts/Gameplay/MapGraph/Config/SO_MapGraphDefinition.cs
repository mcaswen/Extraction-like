using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// 实时玩法抽象图配置
    /// 用 ScriptableObject 保存节点、边和 UI 布局，运行时只读取不写回
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_MapGraph_Definition",
        menuName = "Gameplay/Map Graph/Graph Definition")]
    public sealed class SO_MapGraphDefinition : ScriptableObject
    {
        [SerializeField] private string _mapId = "map_graph";
        [SerializeField] private string _displayName = "Map Graph";
        [SerializeField] private string _startNodeId;
        [SerializeField] private List<MapGraphNodeDefinition> _nodes =
            new List<MapGraphNodeDefinition>();
        [SerializeField] private List<MapGraphEdgeDefinition> _edges =
            new List<MapGraphEdgeDefinition>();

        /// <summary>
        /// 图配置稳定 ID
        /// </summary>
        public string MapId => _mapId ?? string.Empty;

        /// <summary>
        /// 图显示名称
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? MapId : _displayName;

        /// <summary>
        /// 默认起点节点 ID
        /// </summary>
        public string StartNodeId => _startNodeId ?? string.Empty;

        /// <summary>
        /// 图中的全部节点定义
        /// </summary>
        public IReadOnlyList<MapGraphNodeDefinition> Nodes => _nodes;

        /// <summary>
        /// 图中的全部边定义
        /// </summary>
        public IReadOnlyList<MapGraphEdgeDefinition> Edges => _edges;

        private void OnValidate()
        {
            _mapId = NormalizeId(_mapId);
            _startNodeId = NormalizeId(_startNodeId);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

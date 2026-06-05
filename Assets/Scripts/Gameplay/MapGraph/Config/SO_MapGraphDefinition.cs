using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [SerializeField] private MapGraphNodeIconSet _nodeIconSet = new MapGraphNodeIconSet();
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
        /// 节点图标配置
        /// 从旧桌游地图转换时会完整复制旧 NodeIconSet
        /// </summary>
        public MapGraphNodeIconSet NodeIconSet => _nodeIconSet;

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

        [ContextMenu("Apply MVP Graph Preset")]
        public void ApplyMvpGraphPresetToAsset()
        {
            Dictionary<string, MapGraphNodeDefinition> existingNodesById =
                new Dictionary<string, MapGraphNodeDefinition>(StringComparer.Ordinal);

            if (_nodes != null)
            {
                foreach (MapGraphNodeDefinition node in _nodes)
                {
                    if (node != null &&
                        !string.IsNullOrWhiteSpace(node.NodeId) &&
                        !existingNodesById.ContainsKey(node.NodeId))
                    {
                        existingNodesById.Add(node.NodeId, node);
                    }
                }
            }

            List<MapGraphNodeDefinition> presetNodes = new List<MapGraphNodeDefinition>();
            foreach (MvpMapGraphNodePresetDefinition presetNode in MvpMapGraphPresetConfig.CreateNodePresets())
            {
                Vector2 position = presetNode.DefaultPosition;
                Sprite icon = null;
                existingNodesById.TryGetValue(presetNode.NodeId, out MapGraphNodeDefinition existingNode);
                if (existingNode != null)
                {
                    position = existingNode.Position;
                    icon = existingNode.Icon;
                }

                if (_nodeIconSet != null)
                {
                    Sprite iconSetSprite = _nodeIconSet.GetIcon(
                        presetNode.IconKind,
                        presetNode.ResourceTier,
                        presetNode.DangerTier);
                    if (iconSetSprite != null)
                        icon = iconSetSprite;
                }

                presetNodes.Add(new MapGraphNodeDefinition(
                    presetNode.NodeId,
                    presetNode.NodeKind,
                    position,
                    presetNode.NodeId,
                    presetNode.Description,
                    icon,
                    presetNode.IconKind,
                    presetNode.ResourceTier,
                    presetNode.DangerTier));
            }

            _mapId = MvpMapGraphPresetConfig.MapId;
            _displayName = MvpMapGraphPresetConfig.DisplayName;
            _startNodeId = MvpMapGraphPresetConfig.StartNodeId;
            _nodes = presetNodes;
            _edges = MvpMapGraphPresetConfig.CreateEdgePresets();
            MarkDirty();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}

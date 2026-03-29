using System;
using System.Collections.Generic;
using BoardGame.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoardGame.Config
{
    /// <summary>
    /// 固定地图 ScriptableObject 定义
    /// 用于配置节点、边、起点以及 2D 布局坐标
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_BoardGame_MapDefinition",
        menuName = "BoardGame/Map Definition")]
    public sealed class SO_BoardGame_MapDefinition : ScriptableObject
    {
        [SerializeField] private string _mapId = "sample_board_map"; // 地图唯一 ID
        [SerializeField] private string _displayName = "Sample Fixed Map"; // 地图显示名称
        [SerializeField] private string _startNodeId = "start"; // AI 开局所在节点 ID
        [SerializeField] private List<BoardMapNodeDefinition> _nodes = new List<BoardMapNodeDefinition>(); // 地图节点列表
        [SerializeField] private List<BoardMapEdgeDefinition> _edges = new List<BoardMapEdgeDefinition>(); // 地图边列表

        public string MapId => _mapId;
        public string DisplayName => _displayName;
        public string StartNodeId => _startNodeId;
        public IReadOnlyList<BoardMapNodeDefinition> Nodes => _nodes;
        public IReadOnlyList<BoardMapEdgeDefinition> Edges => _edges;

        [ContextMenu("Fill Sample Map")]
        private void FillWithSampleMap()
        {
            _mapId = "sample_board_map";
            _displayName = "Sample Fixed Map";
            _startNodeId = "start";

            _nodes = new List<BoardMapNodeDefinition>
            {
                new BoardMapNodeDefinition("start", BoardNodeType.Start, new Vector2(-8f, 0f)),
                new BoardMapNodeDefinition("resource_low", BoardNodeType.Resource, new Vector2(-4f, 2.5f), BoardResourceTier.Low),
                new BoardMapNodeDefinition("enemy_low", BoardNodeType.Enemy, new Vector2(-2f, -2.5f), BoardResourceTier.None, BoardDangerTier.Low),
                new BoardMapNodeDefinition("resource_mid", BoardNodeType.Resource, new Vector2(0f, 3f), BoardResourceTier.Medium),
                new BoardMapNodeDefinition("enemy_mid", BoardNodeType.Enemy, new Vector2(1.5f, -2f), BoardResourceTier.None, BoardDangerTier.Medium),
                new BoardMapNodeDefinition("resource_high", BoardNodeType.Resource, new Vector2(4f, 2f), BoardResourceTier.High),
                new BoardMapNodeDefinition("boss", BoardNodeType.Boss, new Vector2(5.5f, -2.5f), BoardResourceTier.None, BoardDangerTier.High),
                new BoardMapNodeDefinition("extract", BoardNodeType.Extract, new Vector2(8f, 0f))
            };

            _edges = new List<BoardMapEdgeDefinition>
            {
                new BoardMapEdgeDefinition("edge_start_resource_low", "start", "resource_low", 3.2f),
                new BoardMapEdgeDefinition("edge_start_enemy_low", "start", "enemy_low", 3f),
                new BoardMapEdgeDefinition("edge_resource_low_resource_mid", "resource_low", "resource_mid", 2.8f),
                new BoardMapEdgeDefinition("edge_resource_low_enemy_mid", "resource_low", "enemy_mid", 3.7f),
                new BoardMapEdgeDefinition("edge_enemy_low_enemy_mid", "enemy_low", "enemy_mid", 2.8f),
                new BoardMapEdgeDefinition("edge_resource_mid_resource_high", "resource_mid", "resource_high", 3.1f),
                new BoardMapEdgeDefinition("edge_enemy_mid_resource_high", "enemy_mid", "resource_high", 4f),
                new BoardMapEdgeDefinition("edge_enemy_mid_boss", "enemy_mid", "boss", 3.2f),
                new BoardMapEdgeDefinition("edge_resource_high_extract", "resource_high", "extract", 3f),
                new BoardMapEdgeDefinition("edge_boss_extract", "boss", "extract", 3f)
            };

            MarkDirty();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 用场景节点数据回写地图 SO
        /// 已存在节点会保留类型 等级 和描述
        /// 新增节点会按起点或普通资源点占位创建
        /// </summary>
        public void ImportSceneNodeLayout(
            IReadOnlyList<BoardMapSceneNodeImportEntry> importEntries,
            bool removeMissingNodes,
            string importedStartNodeId)
        {
            if (importEntries == null)
            {
                return;
            }

            Dictionary<string, BoardMapNodeDefinition> existingNodesById =
                new Dictionary<string, BoardMapNodeDefinition>(StringComparer.Ordinal);

            foreach (BoardMapNodeDefinition node in _nodes)
            {
                if (!string.IsNullOrEmpty(node.NodeId) && !existingNodesById.ContainsKey(node.NodeId))
                {
                    existingNodesById.Add(node.NodeId, node);
                }
            }

            List<BoardMapNodeDefinition> mergedNodes = new List<BoardMapNodeDefinition>(importEntries.Count);
            HashSet<string> importedNodeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BoardMapSceneNodeImportEntry importEntry in importEntries)
            {
                if (string.IsNullOrEmpty(importEntry.NodeId) || importedNodeIds.Contains(importEntry.NodeId))
                {
                    continue;
                }

                if (existingNodesById.TryGetValue(importEntry.NodeId, out BoardMapNodeDefinition existingNode))
                {
                    BoardNodeType mergedNodeType = importEntry.IsStartNode ? BoardNodeType.Start : existingNode.NodeType;
                    mergedNodes.Add(new BoardMapNodeDefinition(
                        importEntry.NodeId,
                        mergedNodeType,
                        importEntry.Position,
                        existingNode.ResourceTier,
                        existingNode.DangerTier,
                        existingNode.Description));
                }
                else
                {
                    BoardNodeType newNodeType = importEntry.IsStartNode ? BoardNodeType.Start : BoardNodeType.Resource;
                    mergedNodes.Add(new BoardMapNodeDefinition(importEntry.NodeId, newNodeType, importEntry.Position));
                }

                importedNodeIds.Add(importEntry.NodeId);
            }

            if (!removeMissingNodes)
            {
                foreach (BoardMapNodeDefinition existingNode in _nodes)
                {
                    if (!importedNodeIds.Contains(existingNode.NodeId))
                    {
                        mergedNodes.Add(existingNode);
                    }
                }
            }

            _nodes = mergedNodes;

            if (!string.IsNullOrEmpty(importedStartNodeId))
            {
                _startNodeId = importedStartNodeId;
            }

            MarkDirty();
            AssetDatabase.SaveAssets();
        }
#endif

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// 单个节点的静态定义
    /// </summary>
    [Serializable]
    public sealed class BoardMapNodeDefinition
    {
        [SerializeField] private string _nodeId; // 节点唯一 ID
        [SerializeField] private string _description; // 节点补充说明
        [SerializeField] private BoardNodeType _nodeType; // 节点主类型
        [SerializeField] private BoardResourceTier _resourceTier; // 资源点等级，仅资源点使用
        [SerializeField] private BoardDangerTier _dangerTier; // 危险等级，仅敌人点和 Boss 点使用
        [SerializeField] private Vector2 _position; // 2D 地图上的坐标

        public BoardMapNodeDefinition(
            string nodeId,
            BoardNodeType nodeType,
            Vector2 position,
            BoardResourceTier resourceTier = BoardResourceTier.None,
            BoardDangerTier dangerTier = BoardDangerTier.None,
            string description = "")
        {
            _nodeId = nodeId;
            _nodeType = nodeType;
            _position = position;
            _resourceTier = resourceTier;
            _dangerTier = dangerTier;
            _description = description;
        }

        public string NodeId => _nodeId;
        public string Description => _description;
        public BoardNodeType NodeType => _nodeType;
        public BoardResourceTier ResourceTier => _resourceTier;
        public BoardDangerTier DangerTier => _dangerTier;
        public Vector2 Position => _position;
    }

    /// <summary>
    /// 场景节点导入快照
    /// 用于把场景摆点结果写回地图 SO
    /// </summary>
    public readonly struct BoardMapSceneNodeImportEntry
    {
        public BoardMapSceneNodeImportEntry(string nodeId, Vector2 position, bool isStartNode)
        {
            NodeId = nodeId;
            Position = position;
            IsStartNode = isStartNode;
        }

        public string NodeId { get; }
        public Vector2 Position { get; }
        public bool IsStartNode { get; }
    }

    /// <summary>
    /// 单条边的静态定义
    /// </summary>
    [Serializable]
    public sealed class BoardMapEdgeDefinition
    {
        [SerializeField] private string _edgeId; // 边唯一 ID
        [SerializeField] private string _fromNodeId; // 边起点节点 ID
        [SerializeField] private string _toNodeId; // 边终点节点 ID
        [SerializeField] private float _lengthUnits = 1f; // 边长度，用于换算移动时长

        public BoardMapEdgeDefinition(
            string edgeId,
            string fromNodeId,
            string toNodeId,
            float lengthUnits)
        {
            _edgeId = edgeId;
            _fromNodeId = fromNodeId;
            _toNodeId = toNodeId;
            _lengthUnits = Mathf.Max(0.1f, lengthUnits);
        }

        public string EdgeId => _edgeId;
        public string FromNodeId => _fromNodeId;
        public string ToNodeId => _toNodeId;
        public float LengthUnits => _lengthUnits;
    }
}

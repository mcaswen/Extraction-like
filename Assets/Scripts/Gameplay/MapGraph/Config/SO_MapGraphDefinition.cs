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
    public sealed class SO_MapGraphDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int CommandSchemaVersion = 2;
        // 缺少字段的旧资产仍是 0，不能因加载就被解释成正式指挥图。
        [SerializeField] private int _schemaVersion;
        [SerializeField] private long _revision;
        [SerializeField] private string _mapId = "map_graph";
        [SerializeField] private string _displayName = "Map Graph";
        [SerializeField] private string _startNodeId;
        [SerializeField] private MapGraphNodeIconSet _nodeIconSet = new MapGraphNodeIconSet();
        [SerializeField] private List<MapGraphNodeDefinition> _nodes =
            new List<MapGraphNodeDefinition>();
        [SerializeField] private List<MapGraphEdgeDefinition> _edges =
            new List<MapGraphEdgeDefinition>();
        [SerializeField] private List<MapGraphZoneDefinition> _zones = new List<MapGraphZoneDefinition>();
        [SerializeField] private MapGraphGenerationSettings _generationSettings = new MapGraphGenerationSettings();
        [SerializeField] private MapGraphLayoutConstraints _layoutConstraints = new MapGraphLayoutConstraints();
        [SerializeField] private MapGraphNavigationBakeData _navigationBake = new MapGraphNavigationBakeData();
        [NonSerialized] private IReadOnlyList<MapGraphNodeDefinition> _nodeView;
        [NonSerialized] private IReadOnlyList<MapGraphEdgeDefinition> _edgeView;
        [NonSerialized] private IReadOnlyList<MapGraphZoneDefinition> _zoneView;
        public int SchemaVersion => _schemaVersion;
        public bool IsCommandGraph => _schemaVersion == CommandSchemaVersion;
        public long Revision => _revision;
        public IReadOnlyList<MapGraphZoneDefinition> Zones => _zoneView ??= _zones.AsReadOnly();
        public MapGraphGenerationSettings GenerationSettings => _generationSettings;
        public MapGraphLayoutConstraints LayoutConstraints => _layoutConstraints;
        public MapGraphNavigationBakeData NavigationBake => _navigationBake;

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
        public IReadOnlyList<MapGraphNodeDefinition> Nodes => _nodeView ??= _nodes.AsReadOnly();

        /// <summary>
        /// 图中的全部边定义
        /// </summary>
        public IReadOnlyList<MapGraphEdgeDefinition> Edges => _edgeView ??= _edges.AsReadOnly();

        private void OnValidate()
        {
            _mapId = NormalizeId(_mapId);
            _startNodeId = NormalizeId(_startNodeId);
            ResetViews();
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => ResetViews();
        private void ResetViews() { _nodeView = null; _edgeView = null; _zoneView = null; }

#if UNITY_EDITOR
        /// <summary>生成器通过校验后，在 Undo 事务中一次替换。运行时不能写回正式图资产。</summary>
        public void ApplyCommandData(string mapId, string displayName, string startNodeId,
            IEnumerable<MapGraphZoneDefinition> zones, IEnumerable<MapGraphNodeDefinition> nodes,
            IEnumerable<MapGraphEdgeDefinition> edges, MapGraphLayoutConstraints constraints,
            MapGraphNavigationBakeData navigationBake, MapGraphGenerationSettings settings = null)
        {
            if (string.IsNullOrWhiteSpace(mapId)) throw new ArgumentException("地图 ID 不能为空。", nameof(mapId));
            var zoneCopy = new List<MapGraphZoneDefinition>(zones ?? throw new ArgumentNullException(nameof(zones)));
            var nodeCopy = new List<MapGraphNodeDefinition>(nodes ?? throw new ArgumentNullException(nameof(nodes)));
            var edgeCopy = new List<MapGraphEdgeDefinition>(edges ?? throw new ArgumentNullException(nameof(edges)));
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            if (navigationBake == null) throw new ArgumentNullException(nameof(navigationBake));
            _mapId = mapId.Trim(); _displayName = displayName; _startNodeId = NormalizeId(startNodeId);
            _zones = zoneCopy; _nodes = nodeCopy; _edges = edgeCopy;
            _layoutConstraints = constraints; _navigationBake = navigationBake;
            if (settings != null) _generationSettings = settings;
            _schemaVersion = CommandSchemaVersion; _revision++;
            ResetViews(); MarkDirty();
        }
#endif

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
            _schemaVersion = 0; _revision++;
            _zones = new List<MapGraphZoneDefinition>();
            _layoutConstraints = new MapGraphLayoutConstraints();
            _navigationBake = new MapGraphNavigationBakeData();
            ResetViews();
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

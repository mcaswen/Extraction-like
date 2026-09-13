using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>只属于 Editor 会话的摆放草稿。未验证数据不进入运行时图资产。</summary>
    public sealed class MapGraphPlacementState : ScriptableObject
    {
        [SerializeField] private bool _active;
        [SerializeField] private float _gridSpacing = 80;
        [SerializeField] private int _adjustmentCells = 1;
        [SerializeField] private List<MapGraphZoneDefinition> _zones = new List<MapGraphZoneDefinition>();
        [SerializeField] private List<MapGraphNodeDefinition> _nodes = new List<MapGraphNodeDefinition>();
        [SerializeField] private List<MapGraphEdgeDefinition> _edges = new List<MapGraphEdgeDefinition>();
        [SerializeField] private MapGraphLayoutConstraints _constraints = new MapGraphLayoutConstraints();
        [SerializeField] private string _start;
        private MapGraphLayoutDraft _cache;
        [Serializable] private sealed class OwnedLayout
        {
            public List<MapGraphZoneDefinition> zones;
            public List<MapGraphNodeDefinition> nodes;
            public List<MapGraphEdgeDefinition> edges;
            public MapGraphLayoutConstraints constraints;
        }
        public bool Active => _active;
        public float GridSpacing => _gridSpacing;
        public int AdjustmentCells => _adjustmentCells;
        public MapGraphLayoutDraft Draft => !_active ? null : _cache ??= new MapGraphLayoutDraft(_zones, _nodes, _edges, _constraints, _start);
        public void SetSettings(float spacing, int cells)
        { _gridSpacing = MapGraphGridPlacement.ValidSpacing(spacing); _adjustmentCells = Mathf.Clamp(cells, 1, 4); }
        public void SetDraft(MapGraphLayoutDraft draft)
        {
            _active = draft != null; _cache = null;
            // List 的浅拷贝仍共用节点等内部对象。Unity Undo 会回填对象字段，必须隔离全部可序列化数据。
            var owned = draft == null ? null : JsonUtility.FromJson<OwnedLayout>(JsonUtility.ToJson(new OwnedLayout
            {
                zones = new List<MapGraphZoneDefinition>(draft.Zones), nodes = new List<MapGraphNodeDefinition>(draft.Nodes),
                edges = new List<MapGraphEdgeDefinition>(draft.Edges), constraints = draft.Constraints
            }));
            _zones = owned?.zones ?? new List<MapGraphZoneDefinition>();
            _nodes = owned?.nodes ?? new List<MapGraphNodeDefinition>();
            _edges = owned?.edges ?? new List<MapGraphEdgeDefinition>();
            _constraints = owned?.constraints ?? new MapGraphLayoutConstraints();
            _start = draft?.StartNodeId ?? "";
        }
        public void Invalidate() { _cache = null; _constraints?.OnAfterDeserialize(); }
    }
}

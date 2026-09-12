using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>场景直接引用的唯一所有者和索引；不选择成员、不推进路线、不查询 NavMesh。</summary>
    public sealed class MapGraphBindingAuthoring : MonoBehaviour
    {
        [SerializeField] private SO_MapGraphDefinition _mapDefinition;
        [SerializeField] private List<MapGraphTargetBinding> _targetBindings = new List<MapGraphTargetBinding>();
        [SerializeField] private List<MapGraphZoneBinding> _zoneBindings = new List<MapGraphZoneBinding>();
        private readonly Dictionary<string, MapGraphTargetBinding> _byNode = new Dictionary<string, MapGraphTargetBinding>(StringComparer.Ordinal);
        private readonly Dictionary<GameplayTargetAuthoringBase, string> _byTarget = new Dictionary<GameplayTargetAuthoringBase, string>();
        private readonly Dictionary<string, MapGraphZoneBinding> _byZone = new Dictionary<string, MapGraphZoneBinding>(StringComparer.Ordinal);
        private readonly HashSet<TargetZoneAuthoring> _zoneTargets = new HashSet<TargetZoneAuthoring>();
        private readonly List<string> _errors = new List<string>();
        private IReadOnlyList<MapGraphTargetBinding> _targetView;
        private IReadOnlyList<MapGraphZoneBinding> _zoneView;
        private IReadOnlyList<string> _errorView;
        private bool _indexed;
        public SO_MapGraphDefinition MapDefinition => _mapDefinition;
        public IReadOnlyList<MapGraphTargetBinding> TargetBindings => _targetView ??= _targetBindings.AsReadOnly();
        public IReadOnlyList<MapGraphZoneBinding> ZoneBindings => _zoneView ??= _zoneBindings.AsReadOnly();
        public IReadOnlyList<string> ValidationErrors { get { EnsureIndexes(); return _errorView ??= _errors.AsReadOnly(); } }
        public bool IsValid { get { EnsureIndexes(); return _errors.Count == 0; } }
        public long Revision { get; private set; }

        private void OnEnable() => RebuildIndexes();
        private void OnValidate() { _indexed = false; _targetView = null; _zoneView = null; }

        /// <summary>场景生成器或组合根一次提交绑定，调用方集合之后的变更不影响配置。</summary>
        public void Configure(SO_MapGraphDefinition definition, IEnumerable<MapGraphTargetBinding> targets,
            IEnumerable<MapGraphZoneBinding> zones)
        {
            var targetCopy = new List<MapGraphTargetBinding>(targets ?? throw new ArgumentNullException(nameof(targets)));
            var zoneCopy = new List<MapGraphZoneBinding>(zones ?? throw new ArgumentNullException(nameof(zones)));
            _mapDefinition = definition; _targetBindings = targetCopy; _zoneBindings = zoneCopy;
            _targetView = null; _zoneView = null;
            RebuildIndexes();
        }

        public void RebuildIndexes()
        {
            _indexed = true; Revision++;
            _byNode.Clear(); _byTarget.Clear(); _byZone.Clear(); _zoneTargets.Clear(); _errors.Clear();
            if (_mapDefinition == null) _errors.Add("MissingMapDefinition");
            var graph = _mapDefinition != null ? new MapGraphService(_mapDefinition) : null;
            if (graph != null && !graph.IsValid) _errors.AddRange(graph.ValidationErrors);
            foreach (var binding in _zoneBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.ZoneId) || (binding.IsSynthetic ? binding.Zone != null : binding.Zone == null))
                { _errors.Add("InvalidZoneBinding"); continue; }
                if (_byZone.ContainsKey(binding.ZoneId)) { _errors.Add("DuplicateZoneBinding:" + binding.ZoneId); continue; }
                _byZone.Add(binding.ZoneId, binding);
                if (binding.Zone != null && !_zoneTargets.Add(binding.Zone)) _errors.Add("DuplicateZoneTarget:" + binding.ZoneId);
                if (graph != null)
                {
                    if (!graph.TryGetZone(binding.ZoneId, out var zone)) _errors.Add("OrphanZoneBinding:" + binding.ZoneId);
                    else if (zone.IsSynthetic != binding.IsSynthetic) _errors.Add("ZoneBindingKindMismatch:" + binding.ZoneId);
                    else if (zone.SourceObjectId.Length > 0 && zone.SourceObjectId != binding.SourceObjectId) _errors.Add("ZoneSourceIdentityMismatch:" + binding.ZoneId);
                }
            }
            foreach (var binding in _targetBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.NodeId))
                { _errors.Add("InvalidTargetBinding"); continue; }
                if (_byNode.ContainsKey(binding.NodeId)) { _errors.Add("DuplicateNodeBinding:" + binding.NodeId); continue; }
                _byNode.Add(binding.NodeId, binding);
                if (graph != null && !graph.TryGetNode(binding.NodeId, out _)) _errors.Add("OrphanNodeBinding:" + binding.NodeId);
                var target = binding.DirectTarget;
                if (target != null)
                {
                    if (_byTarget.ContainsKey(target)) _errors.Add("DuplicateTargetBinding:" + binding.NodeId);
                    else _byTarget.Add(target, binding.NodeId);
                }
                if (_mapDefinition == null || !_mapDefinition.IsCommandGraph) continue;
                if (!(target is GameplayTargetClusterAuthoringBase cluster))
                { _errors.Add("MissingDirectCluster:" + binding.NodeId); continue; }
                if (!binding.TryGetNavigationAnchor(out _)) _errors.Add("MissingNavigationAnchor:" + binding.NodeId);
                if (graph.TryGetNode(binding.NodeId, out var node) &&
                    (!_byZone.TryGetValue(node.ZoneId, out var zone) || zone.Zone != cluster.Zone))
                    _errors.Add("ClusterZoneMismatch:" + binding.NodeId);
                if (node != null && node.SourceObjectId.Length > 0 && node.SourceObjectId != binding.SourceObjectId)
                    _errors.Add("SourceIdentityMismatch:" + binding.NodeId);
            }
            if (_mapDefinition == null || !_mapDefinition.IsCommandGraph) return;
            foreach (var node in _mapDefinition.Nodes)
                if (node != null && !_byNode.ContainsKey(node.NodeId)) _errors.Add("MissingNodeBinding:" + node.NodeId);
            foreach (var zone in _mapDefinition.Zones)
                if (zone != null && !_byZone.ContainsKey(zone.ZoneId)) _errors.Add("MissingZoneBinding:" + zone.ZoneId);
        }

        /// <summary>按直接引用辨认静态节点，不受运行时 TargetId 再生成影响。</summary>
        public bool TryGetNodeIdForDirectTarget(GameplayTargetAuthoringBase target, out string nodeId)
        {
            EnsureIndexes(); nodeId = string.Empty;
            return _errors.Count == 0 && target != null && _byTarget.TryGetValue(target, out nodeId);
        }

        /// <summary>旧 ID 兼容查询。ID 会在注册时改变，故读取当前值；多个匹配明确拒绝。</summary>
        public bool TryGetNodeIdForTargetId(string targetId, out string nodeId)
        {
            nodeId = string.Empty;
            string normalized = NormalizeId(targetId);
            if (normalized.Length == 0) return false;
            foreach (var binding in _targetBindings)
            {
                if (binding == null || !binding.MatchesTargetId(normalized)) continue;
                if (nodeId.Length > 0) { nodeId = string.Empty; return false; }
                nodeId = binding.NodeId;
            }
            return nodeId.Length > 0;
        }

        public bool TryGetBinding(string nodeId, out MapGraphTargetBinding binding)
        {
            EnsureIndexes(); binding = null;
            return _errors.Count == 0 && _byNode.TryGetValue(NormalizeId(nodeId), out binding);
        }

        public bool TryGetZone(string zoneId, out TargetZoneAuthoring zone)
        {
            EnsureIndexes(); zone = null;
            if (!_byZone.TryGetValue(NormalizeId(zoneId), out var binding)) return false;
            zone = binding.Zone; return zone != null;
        }

        public bool TryGetTargetIdForNodeId(string nodeId, out string targetId)
        {
            targetId = TryGetBinding(nodeId, out var binding) ? binding.TargetId : string.Empty;
            return targetId.Length > 0;
        }

        public bool TryResolveTargetForNodeId(string nodeId, out GameplayTargetAuthoringBase target)
        {
            target = null;
            return TryGetBinding(nodeId, out var binding) && binding.TryResolveTarget(out target);
        }

        public bool TryGetWorldPositionForNodeId(string nodeId, out Vector3 position)
        {
            position = default;
            return TryGetBinding(nodeId, out var binding) && binding.TryGetWorldPosition(out position);
        }

        public bool TryGetNavigationAnchor(string nodeId, out Vector3 position)
        {
            position = default;
            return TryGetBinding(nodeId, out var binding) && binding.TryGetNavigationAnchor(out position);
        }

        /// <summary>旧展示的平面最近点查询。正式路线入图不以它代替导航或绕过图连接。</summary>
        public bool TryFindNearestBoundNode(Vector3 worldPosition, float maxDistance, out string nodeId)
        {
            nodeId = string.Empty;
            float limit = Mathf.Max(0, maxDistance); float nearest = float.MaxValue;
            foreach (var binding in _targetBindings)
            {
                if (binding == null || binding.NodeId.Length == 0 || !binding.TryGetWorldPosition(out var position)) continue;
                var delta = position - worldPosition; delta.y = 0;
                float distance = delta.sqrMagnitude;
                if (distance > limit * limit || distance >= nearest) continue;
                nearest = distance; nodeId = binding.NodeId;
            }
            return nodeId.Length > 0;
        }

        /// <summary>历史展示对象入口；正式成员/来源群规范化由 ClusterResolver 处理。</summary>
        public bool TryGetNodeIdForTargetObject(GameObject targetObject, out string nodeId)
        {
            nodeId = string.Empty;
            if (targetObject == null) return false;
            var target = targetObject.GetComponent<GameplayTargetAuthoringBase>();
            if (target == null) target = targetObject.GetComponentInParent<GameplayTargetAuthoringBase>();
            if (target == null) target = targetObject.GetComponentInChildren<GameplayTargetAuthoringBase>();
            return target != null && (TryGetNodeIdForDirectTarget(target, out nodeId) || TryGetNodeIdForTargetId(target.TargetId, out nodeId));
        }

        private void EnsureIndexes() { if (!_indexed) RebuildIndexes(); }
        private static string NormalizeId(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

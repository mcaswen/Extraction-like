using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>校验烘焙并有预算地补算已保存的图边；不改图拓扑、不回写资产、不执行移动。</summary>
    public sealed class MapGraphNavigationCostService
    {
        private readonly MapGraphBindingAuthoring _binding;
        private readonly SO_MapGraphDefinition _definition;
        private readonly long _definitionRevision, _bindingRevision;
        private readonly AgentNavigationProfile _profile;
        private readonly MapGraphNavigationProfileData _profileData;
        private readonly List<MapGraphEdgeDefinition> _orderedEdges;
        private readonly Dictionary<string, MapGraphEdgeDefinition> _edges = new Dictionary<string, MapGraphEdgeDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapGraphNavigationEdgeBake> _measurements = new Dictionary<string, MapGraphNavigationEdgeBake>(StringComparer.Ordinal);
        private readonly Dictionary<string, MapGraphEdgeCost> _costs = new Dictionary<string, MapGraphEdgeCost>(StringComparer.Ordinal);
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly HashSet<string> _pendingSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly AgentNavigationSegmentQuery.Buffer _buffer = new AgentNavigationSegmentQuery.Buffer();
        private long _revision;
        private int _anchorCursor;
        private MapGraphCostSnapshot _snapshot;
        private bool _requiresRebuild;
        public MapGraphCostSnapshot Snapshot { get { EnsureContext(); return _snapshot; } }
        public int PendingEdgeCount { get { EnsureContext(); return _pendingSet.Count; } }
        public bool RequiresRebuild { get { EnsureContext(); return _requiresRebuild; } }
        public long CalculationCount => _buffer.CalculationCount;
        public string LastInvalidationReason { get; private set; }

        public MapGraphNavigationCostService(SO_MapGraphDefinition definition, MapGraphBindingAuthoring binding,
            AgentNavigationProfile profile, string sceneFingerprint, string navigationFingerprint, string runtimeNavigationFingerprint = "")
        {
            if (definition == null || !definition.IsCommandGraph || !new MapGraphService(definition).IsValid)
                throw new ArgumentException("需要有效的正式指挥图。", nameof(definition));
            if (binding == null || binding.MapDefinition != definition || !binding.IsValid)
                throw new ArgumentException("场景绑定必须完整并对应当前图。", nameof(binding));
            _binding = binding; _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _definition = definition; _definitionRevision = definition.Revision; _bindingRevision = binding.Revision;
            _profileData = CaptureProfile(profile);
            _orderedEdges = new List<MapGraphEdgeDefinition>(definition.Edges);
            _orderedEdges.Sort((a, b) => string.CompareOrdinal(a.EdgeId, b.EdgeId));
            foreach (var edge in _orderedEdges) _edges.Add(edge.EdgeId, edge);
            var bake = definition.NavigationBake;
            bool editorContext = !string.IsNullOrEmpty(sceneFingerprint) && !string.IsNullOrEmpty(navigationFingerprint) &&
                bake != null && bake.SceneFingerprint == sceneFingerprint && bake.NavigationFingerprint == navigationFingerprint;
            bool runtimeContext = !string.IsNullOrEmpty(runtimeNavigationFingerprint) && bake != null &&
                !runtimeNavigationFingerprint.StartsWith("live-links:", StringComparison.Ordinal) &&
                bake.RuntimeNavigationFingerprint == runtimeNavigationFingerprint;
            bool valid = bake != null && (editorContext || runtimeContext) && MatchesProfile(bake.Profile, profile);
            var bakedById = new Dictionary<string, MapGraphNavigationEdgeBake>(StringComparer.Ordinal);
            if (valid)
            {
                foreach (var entry in bake.Edges)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.EdgeId) || bakedById.ContainsKey(entry.EdgeId)) { valid = false; break; }
                    bakedById.Add(entry.EdgeId, entry);
                }
            }
            foreach (var edge in _orderedEdges)
            {
                if (valid && bakedById.TryGetValue(edge.EdgeId, out var entry) &&
                    entry.FromNodeId == edge.FromNodeId && entry.ToNodeId == edge.ToNodeId && AnchorsMatch(edge, entry))
                    Store(entry);
                else Enqueue(edge.EdgeId);
            }
            LastInvalidationReason = valid ? "MissingOrMovedEdge" : "BakeContextMismatch";
            Publish();
        }

        /// <summary>每条边最多两个有向查询。预算按边计，不因地图重绘隐式调用。</summary>
        public int ProcessPending(int maximumEdges)
        {
            if (!EnsureContext()) return 0;
            int processed = 0;
            while (processed < Math.Max(0, maximumEdges) && _pending.Count > 0)
            {
                string id = _pending.Dequeue(); _pendingSet.Remove(id);
                var edge = _edges[id];
                MapGraphNavigationEdgeBake result;
                if (_binding.TryGetNavigationAnchor(edge.FromNodeId, out var from) &&
                    _binding.TryGetNavigationAnchor(edge.ToNodeId, out var to))
                    result = MeasureEdge(edge, from, to, _profile, _buffer);
                else result = new MapGraphNavigationEdgeBake(id, edge.FromNodeId, edge.ToNodeId, default, default,
                    float.PositiveInfinity, float.PositiveInfinity, "MissingNavigationAnchor", "MissingNavigationAnchor");
                Store(result); processed++;
            }
            if (processed > 0) Publish();
            return processed;
        }

        /// <summary>失效立即移除旧成本，后续补算才发布新可达性。旧快照始终不变。</summary>
        public bool InvalidateEdge(string edgeId, string reason)
        {
            if (!EnsureContext()) return false;
            if (edgeId == null || !_edges.ContainsKey(edgeId)) return false;
            LastInvalidationReason = reason;
            _measurements.Remove(edgeId); Enqueue(edgeId);
            if (_costs.Remove(edgeId)) Publish();
            return true;
        }

        public void InvalidateAll(string reason)
        {
            if (!EnsureContext()) return;
            LastInvalidationReason = reason; _measurements.Clear(); _costs.Clear();
            foreach (var edge in _orderedEdges) Enqueue(edge.EdgeId);
            Publish();
        }

        /// <summary>有限轮询锚点变化，未查询 NavMesh；一次失效只入队一次。</summary>
        public int RefreshChangedAnchors(int maximumEdges)
        {
            if (!EnsureContext()) return 0;
            int changed = 0;
            int count = Math.Min(Math.Max(0, maximumEdges), _orderedEdges.Count);
            for (int i = 0; i < count; i++)
            {
                var edge = _orderedEdges[_anchorCursor++ % _orderedEdges.Count];
                if (_measurements.TryGetValue(edge.EdgeId, out var measurement) && !AnchorsMatch(edge, measurement))
                { InvalidateEdge(edge.EdgeId, "AnchorChanged"); changed++; }
            }
            // 保持游标有界，避免长时间会话整数溢出。
            if (_orderedEdges.Count > 0) _anchorCursor %= _orderedEdges.Count;
            return changed;
        }

        public bool TryGetMeasurement(string edgeId, out MapGraphNavigationEdgeBake measurement)
        {
            measurement = null;
            return EnsureContext() && edgeId != null && _measurements.TryGetValue(edgeId, out measurement);
        }

        /// <summary>Editor 完成生成后取值保存；此方法本身不写入 SO。</summary>
        public MapGraphNavigationBakeData CreateBakeData(string sceneFingerprint, string navigationFingerprint, string runtimeNavigationFingerprint = "")
        {
            if (!EnsureContext()) throw new InvalidOperationException("地图或绑定已替换，需要重建成本服务。");
            var entries = new List<MapGraphNavigationEdgeBake>();
            foreach (var edge in _orderedEdges)
                if (_measurements.TryGetValue(edge.EdgeId, out var entry)) entries.Add(entry);
            return new MapGraphNavigationBakeData(sceneFingerprint, navigationFingerprint, _revision, _profileData, entries, runtimeNavigationFingerprint);
        }

        /// <summary>生成器候选和运行时补边共用的双向测量入口。</summary>
        public static MapGraphNavigationEdgeBake MeasureEdge(MapGraphEdgeDefinition edge, Vector3 from, Vector3 to,
            AgentNavigationProfile profile, AgentNavigationSegmentQuery.Buffer buffer)
        {
            var forward = AgentNavigationSegmentQuery.Calculate(profile, from, to, buffer);
            var reverse = AgentNavigationSegmentQuery.Calculate(profile, to, from, buffer);
            return new MapGraphNavigationEdgeBake(edge.EdgeId, edge.FromNodeId, edge.ToNodeId, from, to,
                forward.Length, reverse.Length, forward.Failure, reverse.Failure);
        }

        public static MapGraphNavigationProfileData CaptureProfile(AgentNavigationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var costs = new float[32];
            var key = new StringBuilder();
            key.Append(profile.AgentTypeId.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(profile.AreaMask.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(profile.SampleRadius.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(profile.HeightTolerance.ToString("R", CultureInfo.InvariantCulture));
            for (int i = 0; i < costs.Length; i++)
            { costs[i] = profile.GetAreaCost(i); key.Append('|').Append(costs[i].ToString("R", CultureInfo.InvariantCulture)); }
            return new MapGraphNavigationProfileData(Hash128.Compute(key.ToString()).ToString(), profile.AgentTypeId,
                profile.AreaMask, profile.SampleRadius, profile.HeightTolerance, costs);
        }

        private static bool MatchesProfile(MapGraphNavigationProfileData data, AgentNavigationProfile profile)
        {
            if (data == null || data.AgentTypeId != profile.AgentTypeId || data.AreaMask != profile.AreaMask ||
                data.SampleRadius != profile.SampleRadius || data.HeightTolerance != profile.HeightTolerance || data.AreaCosts.Count != 32) return false;
            for (int i = 0; i < 32; i++) if (data.AreaCosts[i] != profile.GetAreaCost(i)) return false;
            return true;
        }

        private bool AnchorsMatch(MapGraphEdgeDefinition edge, MapGraphNavigationEdgeBake entry)
            => _binding.TryGetNavigationAnchor(edge.FromNodeId, out var from) &&
                _binding.TryGetNavigationAnchor(edge.ToNodeId, out var to) &&
                (from - entry.FromAnchor).sqrMagnitude <= 0.0001f && (to - entry.ToAnchor).sqrMagnitude <= 0.0001f;

        private void Store(MapGraphNavigationEdgeBake entry)
        {
            _measurements[entry.EdgeId] = entry;
            // 首版图是双向语义，单向可达也不能偷偷变成有向边。
            if (entry.ForwardAvailable && entry.ReverseAvailable)
                _costs[entry.EdgeId] = new MapGraphEdgeCost(entry.EdgeId, entry.ForwardLength, entry.ReverseLength);
            else _costs.Remove(entry.EdgeId);
        }

        private void Enqueue(string edgeId) { if (_pendingSet.Add(edgeId)) _pending.Enqueue(edgeId); }
        private bool EnsureContext()
        {
            if (_requiresRebuild) return false;
            if (_definition != null && _binding != null && _binding.MapDefinition == _definition &&
                _definition.Revision == _definitionRevision && _binding.IsValid && _binding.Revision == _bindingRevision) return true;
            _requiresRebuild = true; LastInvalidationReason = "GraphOrBindingReplaced";
            _measurements.Clear(); _costs.Clear(); _pending.Clear(); _pendingSet.Clear(); Publish();
            return false;
        }
        private void Publish() => _snapshot = new MapGraphCostSnapshot(_profileData.ProfileId, ++_revision, _costs.Values);
    }
}

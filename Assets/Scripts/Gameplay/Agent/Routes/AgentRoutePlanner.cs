using System;
using System.Collections.Generic;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>一次有预算的入图查询和纯图规划，不执行指令、不修改场景。</summary>
    public sealed class AgentRoutePlanner
    {
        private readonly MapGraphService _graph;
        private readonly MapGraphCostSnapshot _costs;
        private readonly IAgentRouteTargetResolver _resolver;
        private readonly Vector3 _origin;
        private readonly string _target;
        private readonly long _graphRevision, _bindingRevision;
        private readonly Func<Vector3, Vector3, AgentNavigationSegmentResult> _measure;
        private readonly Dictionary<string, AgentRouteTargetFacts> _facts = new Dictionary<string, AgentRouteTargetFacts>(StringComparer.Ordinal);
        private readonly List<string> _candidates = new List<string>();
        private int _cursor;
        private string _entry;
        private float _entryLength = float.PositiveInfinity;
        private AgentRoutePlan _result;
        public bool IsDone { get; private set; }
        public bool IsCancelled { get; private set; }
        public AgentRouteFailure Failure { get; private set; }
        public int QueryCount { get; private set; }
        public AgentRoutePlan Result { get { CheckRevision(); return _result; } }

        public AgentRoutePlanner(MapGraphService graph, long graphRevision, MapGraphCostSnapshot costs,
            IAgentRouteTargetResolver resolver, AgentNavigationProfile profile, Vector3 origin, string targetNodeId,
            IReadOnlyList<string> allowedEntryNodes = null)
            : this(graph, graphRevision, costs, resolver, origin, targetNodeId,
                CreateMeasurement(profile), allowedEntryNodes) { }

        // 构造验证只替换世界导航测量，图搜索/入图选择/预算保持正式实现。
        internal AgentRoutePlanner(MapGraphService graph, long graphRevision, MapGraphCostSnapshot costs,
            IAgentRouteTargetResolver resolver, Vector3 origin, string targetNodeId,
            Func<Vector3, Vector3, AgentNavigationSegmentResult> measure, IReadOnlyList<string> allowedEntryNodes = null)
        {
            _graph = graph; _graphRevision = graphRevision; _costs = costs; _resolver = resolver;
            _origin = origin; _target = targetNodeId; _measure = measure;
            _bindingRevision = resolver?.Revision ?? 0;
            if (graph == null || !graph.IsValid || costs == null || resolver == null || measure == null)
            { Fail(AgentRouteFailure.MapUnavailable); return; }
            if (!Finite(origin.x) || !Finite(origin.y) || !Finite(origin.z) || string.IsNullOrWhiteSpace(targetNodeId))
            { Fail(AgentRouteFailure.InvalidRequest); return; }
            foreach (string id in graph.OrderedNodeIds)
                if (resolver.TryGetFacts(id, out var facts) && facts.NodeId == id) _facts.Add(id, facts);
            if (!_facts.TryGetValue(targetNodeId, out var target))
            { Fail(AgentRouteFailure.MissingTarget); return; }
            if (!target.CanTraverse) { Fail(AgentRouteFailure.TargetUnavailable); return; }
            var candidates = allowedEntryNodes ?? graph.OrderedNodeIds;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < candidates.Count; i++)
            {
                string id = candidates[i];
                if (string.IsNullOrWhiteSpace(id) || !graph.TryGetNode(id, out _))
                { Fail(AgentRouteFailure.InvalidRequest); return; }
                if (unique.Add(id) && _facts.TryGetValue(id, out var facts) && facts.CanTraverse) _candidates.Add(id);
            }
            _candidates.Sort(StringComparer.Ordinal);
            CheckRevision();
        }

        /// <summary>最多执行 maxQueries 次入图导航测量。返回本次实际尝试数，失败采样也计入。</summary>
        public int Advance(int maxQueries)
        {
            if (maxQueries < 1) throw new ArgumentOutOfRangeException(nameof(maxQueries));
            if (!CheckRevision() || IsDone) return 0;
            int work = 0;
            while (_cursor < _candidates.Count && work < maxQueries)
            {
                string node = _candidates[_cursor++];
                var segment = _measure(_origin, _facts[node].Anchor);
                work++; QueryCount++;
                if (!CheckRevision()) return work;
                if (segment.IsComplete && Finite(segment.Length) && segment.Length >= 0 && segment.Length < _entryLength)
                { _entry = node; _entryLength = segment.Length; }
            }
            if (_cursor < _candidates.Count) return work;
            if (_entry == null) { Fail(AgentRouteFailure.NoReachableEntry); return work; }
            // 先固定入口，再按作者图寻路。不能换一个靠近终点的入口来绕过断边/途中群。
            var path = new MapGraphPathfindingService(_graph).ResolveFromNode(_entry, _target,
                new TraversableCosts(_costs, _facts));
            if (!path.IsValid) { Fail(AgentRouteFailure.Disconnected); return work; }
            var nodes = new List<string>(path.RemainingNodeIds.Count + 1) { _entry };
            for (int i = 0; i < path.RemainingNodeIds.Count; i++) nodes.Add(path.RemainingNodeIds[i]);
            _result = new AgentRoutePlan(nodes, _origin, _entryLength, path.TotalEstimatedLengthUnits,
                _graphRevision, _costs.Revision, _bindingRevision, _costs.ProfileId);
            IsDone = true;
            return work;
        }

        public void Cancel() { IsCancelled = true; Fail(AgentRouteFailure.Superseded); }

        private bool CheckRevision()
        {
            if (IsCancelled) return false;
            if (_resolver != null && _resolver.Revision != _bindingRevision)
            { Fail(AgentRouteFailure.StaleContext); return false; }
            return true;
        }
        private void Fail(AgentRouteFailure failure) { _result = null; Failure = failure; IsDone = true; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Func<Vector3, Vector3, AgentNavigationSegmentResult> CreateMeasurement(AgentNavigationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            return (origin, destination) => AgentNavigationSegmentQuery.Calculate(profile, origin, destination, buffer);
        }

        private sealed class TraversableCosts : IMapGraphCostProvider
        {
            private readonly MapGraphCostSnapshot _costs;
            private readonly Dictionary<string, AgentRouteTargetFacts> _facts;
            public TraversableCosts(MapGraphCostSnapshot costs, Dictionary<string, AgentRouteTargetFacts> facts)
            { _costs = costs; _facts = facts; }
            public bool TryGetCost(MapGraphEdgeDefinition edge, string fromNodeId, out float cost)
            {
                cost = 0;
                return _facts.TryGetValue(edge.FromNodeId, out var a) && a.CanTraverse &&
                    _facts.TryGetValue(edge.ToNodeId, out var b) && b.CanTraverse && _costs.TryGetCost(edge, fromNodeId, out cost);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    public sealed class MapGraphScannedAnchor
    {
        public string ProfileId { get; }
        public string NodeId { get; }
        public bool IsValid { get; }
        public Vector3 Position { get; }
        public string MemberSourceId { get; }
        public string Failure { get; }
        public int Samples { get; }
        public bool OriginReachabilityChecked { get; }
        public bool ReachableFromAgentOrigin { get; }
        public MapGraphScannedAnchor(string profileId, string nodeId, bool valid, Vector3 position, string memberSourceId,
            string failure, int samples, bool originChecked = false, bool reachable = false)
        {
            ProfileId = profileId; NodeId = nodeId; IsValid = valid; Position = position; MemberSourceId = memberSourceId;
            Failure = failure; Samples = samples; OriginReachabilityChecked = originChecked; ReachableFromAgentOrigin = reachable;
        }
    }

    public sealed class MapGraphScannedConnection
    {
        public string ProfileId { get; }
        public MapGraphNavigationEdgeBake Edge { get; }
        public MapGraphScannedConnection(string profileId, MapGraphNavigationEdgeBake edge) { ProfileId = profileId; Edge = edge; }
    }

    /// <summary>Editor 的分批导航扫描。每个工作项是一候选采样或一对有向查询，可取消，不更改场景。</summary>
    public sealed class MapGraphSceneNavigationScan
    {
        private readonly MapGraphSceneSnapshot _scene;
        private readonly AgentNavigationSegmentQuery.Buffer _buffer = new AgentNavigationSegmentQuery.Buffer();
        private readonly AgentNavigationSegmentQuery.Buffer _originBuffer = new AgentNavigationSegmentQuery.Buffer();
        private readonly List<MapGraphScannedAnchor> _anchors = new List<MapGraphScannedAnchor>();
        private readonly List<MapGraphScannedConnection> _connections = new List<MapGraphScannedConnection>();
        private readonly Dictionary<string, MapGraphScannedAnchor> _byNodeProfile = new Dictionary<string, MapGraphScannedAnchor>(StringComparer.Ordinal);
        private int _profileIndex, _nodeIndex, _candidateIndex, _toIndex = 1;
        private bool _scanningConnections;
        private string _lastSampleFailure;
        private MapGraphScannedAnchor _pendingCandidate, _fallbackCandidate;
        private int _originIndex;
        public IReadOnlyList<MapGraphScannedAnchor> Anchors { get; }
        public IReadOnlyList<MapGraphScannedConnection> Connections { get; }
        public bool IsComplete { get; private set; }
        public bool IsCancelled { get; private set; }
        public long DirectedQueryCount => _buffer.CalculationCount;
        public long OriginQueryCount => _originBuffer.CalculationCount;
        public long TotalQueryCount => DirectedQueryCount + OriginQueryCount;
        public int SampleCount { get; private set; }
        public int WorkItemCount { get; private set; }
        public double ElapsedMilliseconds { get; private set; }

        public MapGraphSceneNavigationScan(MapGraphSceneSnapshot scene)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            Anchors = _anchors.AsReadOnly(); Connections = _connections.AsReadOnly();
        }

        public void Cancel() { if (!IsComplete) IsCancelled = true; }

        public int Advance(int maximumWorkItems)
        {
            if (IsComplete || IsCancelled || maximumWorkItems <= 0) return 0;
            long start = Stopwatch.GetTimestamp();
            int work = 0;
            while (work < maximumWorkItems && !IsComplete)
            {
                if (_profileIndex >= _scene.Profiles.Count)
                {
                    if (_scanningConnections) { IsComplete = true; break; }
                    _scanningConnections = true; _profileIndex = 0; _nodeIndex = 0; _toIndex = 1;
                    continue;
                }
                var profile = _scene.Profiles[_profileIndex];
                string profileId = profile.Data.ProfileId;
                if (!_scanningConnections)
                {
                    if (_nodeIndex >= _scene.Nodes.Count) { _profileIndex++; _nodeIndex = 0; continue; }
                    var node = _scene.Nodes[_nodeIndex];
                    if (_pendingCandidate != null)
                    {
                        if (_originIndex >= profile.AgentOrigins.Count)
                        { _fallbackCandidate ??= _pendingCandidate; _pendingCandidate = null; continue; }
                        var path = AgentNavigationSegmentQuery.Calculate(profile.QueryProfile,
                            profile.AgentOrigins[_originIndex++], _pendingCandidate.Position, _originBuffer);
                        work++;
                        if (path.IsComplete)
                        {
                            StoreAnchor(new MapGraphScannedAnchor(profileId, node.Id, true, _pendingCandidate.Position,
                                _pendingCandidate.MemberSourceId, null, _candidateIndex, true, true));
                            NextNode();
                        }
                        continue;
                    }
                    if (_candidateIndex >= node.Candidates.Count)
                    {
                        StoreAnchor(_fallbackCandidate != null
                            ? new MapGraphScannedAnchor(profileId, node.Id, true, _fallbackCandidate.Position,
                                _fallbackCandidate.MemberSourceId, null, _candidateIndex, true, false)
                            : new MapGraphScannedAnchor(profileId, node.Id, false, default, string.Empty,
                                _lastSampleFailure ?? "NoAnchorCandidates", _candidateIndex));
                        NextNode(); continue;
                    }
                    var candidate = node.Candidates[_candidateIndex++];
                    bool valid = AgentNavigationSegmentQuery.TrySampleAnchor(profile.QueryProfile, candidate.Position,
                        out var position, out _lastSampleFailure);
                    SampleCount++; work++;
                    if (valid)
                    {
                        var anchor = new MapGraphScannedAnchor(profileId, node.Id, true, position, candidate.MemberSourceId, null, _candidateIndex);
                        if (profile.AgentOrigins.Count == 0) { StoreAnchor(anchor); NextNode(); }
                        else { _pendingCandidate = anchor; _originIndex = 0; }
                    }
                    continue;
                }
                if (_nodeIndex >= _scene.Nodes.Count - 1) { _profileIndex++; _nodeIndex = 0; _toIndex = 1; continue; }
                if (_toIndex >= _scene.Nodes.Count) { _nodeIndex++; _toIndex = _nodeIndex + 1; continue; }
                var from = _scene.Nodes[_nodeIndex]; var to = _scene.Nodes[_toIndex++];
                var first = _byNodeProfile[Key(profileId, from.Id)]; var second = _byNodeProfile[Key(profileId, to.Id)];
                string edgeId = ConnectionId(from.Id, to.Id);
                MapGraphNavigationEdgeBake edge;
                if (first.IsValid && second.IsValid)
                {
                    var definition = new MapGraphEdgeDefinition(edgeId, from.Id, to.Id, 1,
                        origin: MapGraphEdgeOrigin.Generated);
                    edge = MapGraphNavigationCostService.MeasureEdge(definition, first.Position, second.Position, profile.QueryProfile, _buffer);
                }
                else edge = new MapGraphNavigationEdgeBake(edgeId, from.Id, to.Id, first.Position, second.Position,
                    float.PositiveInfinity, float.PositiveInfinity, "MissingAnchor", "MissingAnchor");
                _connections.Add(new MapGraphScannedConnection(profileId, edge)); work++;
            }
            WorkItemCount += work;
            ElapsedMilliseconds += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            return work;
        }

        public bool TryGetAnchor(string profileId, string nodeId, out MapGraphScannedAnchor anchor)
            => _byNodeProfile.TryGetValue(Key(profileId, nodeId), out anchor);

        public static string ConnectionId(string a, string b)
        {
            string ordered = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
            return MapGraphSceneCollector.StableId("edge", ordered);
        }

        private void NextNode()
        {
            _nodeIndex++; _candidateIndex = 0; _lastSampleFailure = null;
            _pendingCandidate = null; _fallbackCandidate = null; _originIndex = 0;
        }
        private void StoreAnchor(MapGraphScannedAnchor value)
        { _anchors.Add(value); _byNodeProfile.Add(Key(value.ProfileId, value.NodeId), value); }
        private static string Key(string profileId, string nodeId) => profileId + "/" + nodeId;
    }
}

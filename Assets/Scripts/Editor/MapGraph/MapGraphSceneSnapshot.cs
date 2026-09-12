using System.Collections.Generic;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>场景采集的稳定输入；引用仅用于 Editor 预览和最终写入场景 Binding。</summary>
    public sealed class MapGraphSceneSnapshot
    {
        public string ScenePath { get; }
        public string SceneGuid { get; }
        public string SceneFingerprint { get; }
        public string NavigationFingerprint { get; }
        public IReadOnlyList<MapGraphSceneZone> Zones { get; }
        public IReadOnlyList<MapGraphSceneNode> Nodes { get; }
        public IReadOnlyList<MapGraphSceneProfile> Profiles { get; }
        public IReadOnlyList<string> Diagnostics { get; }
        public bool IsValid => Diagnostics.Count == 0 && Nodes.Count > 0 && Profiles.Count > 0;
        public MapGraphSceneSnapshot(string scenePath, string sceneGuid, string sceneFingerprint, string navigationFingerprint,
            IEnumerable<MapGraphSceneZone> zones, IEnumerable<MapGraphSceneNode> nodes, IEnumerable<MapGraphSceneProfile> profiles,
            IEnumerable<string> diagnostics)
        {
            ScenePath = scenePath; SceneGuid = sceneGuid; SceneFingerprint = sceneFingerprint; NavigationFingerprint = navigationFingerprint;
            Zones = new List<MapGraphSceneZone>(zones).AsReadOnly(); Nodes = new List<MapGraphSceneNode>(nodes).AsReadOnly();
            Profiles = new List<MapGraphSceneProfile>(profiles).AsReadOnly(); Diagnostics = new List<string>(diagnostics).AsReadOnly();
        }
    }

    public sealed class MapGraphSceneZone
    {
        public string Id { get; }
        public string SourceObjectId { get; }
        public string Name { get; }
        public TargetZoneAuthoring Target { get; }
        public Rect WorldBounds { get; }
        public MapGraphSceneZone(string id, string sourceObjectId, string name, TargetZoneAuthoring target, Rect worldBounds)
        { Id = id; SourceObjectId = sourceObjectId; Name = name; Target = target; WorldBounds = worldBounds; }
    }

    public readonly struct MapGraphAnchorCandidate
    {
        public Vector3 Position { get; }
        public string MemberSourceId { get; }
        public string Derivation { get; }
        public MapGraphAnchorCandidate(Vector3 position, string memberSourceId, string derivation = "Authored")
        { Position = position; MemberSourceId = memberSourceId; Derivation = derivation; }
    }

    public sealed class MapGraphSceneNode
    {
        public string Id { get; }
        public string SourceObjectId { get; }
        public string ZoneId { get; }
        public string Name { get; }
        public string HierarchyPath { get; }
        public string PrefabPath { get; }
        public GameplayTargetClusterAuthoringBase Target { get; }
        public Vector3 WorldCenter { get; }
        public Rect WorldBounds { get; }
        public MapGraphNodeKind Kind { get; }
        public IReadOnlyList<MapGraphAnchorCandidate> Candidates { get; }
        public MapGraphSceneNode(string id, string sourceObjectId, string zoneId, string name, string hierarchyPath,
            string prefabPath, GameplayTargetClusterAuthoringBase target, Vector3 center, Rect worldBounds,
            MapGraphNodeKind kind, IEnumerable<MapGraphAnchorCandidate> candidates)
        {
            Id = id; SourceObjectId = sourceObjectId; ZoneId = zoneId; Name = name; HierarchyPath = hierarchyPath;
            PrefabPath = prefabPath; Target = target; WorldCenter = center; WorldBounds = worldBounds; Kind = kind;
            Candidates = new List<MapGraphAnchorCandidate>(candidates).AsReadOnly();
        }
    }

    public sealed class MapGraphSceneProfile
    {
        public AgentNavigationProfile QueryProfile { get; }
        public MapGraphNavigationProfileData Data { get; }
        public IReadOnlyList<string> AgentSourceIds { get; }
        public IReadOnlyList<Vector3> AgentOrigins { get; }
        public MapGraphSceneProfile(AgentNavigationProfile profile, MapGraphNavigationProfileData data,
            IEnumerable<string> agentSourceIds, IEnumerable<Vector3> agentOrigins)
        {
            QueryProfile = profile; Data = data;
            AgentSourceIds = new List<string>(agentSourceIds).AsReadOnly(); AgentOrigins = new List<Vector3>(agentOrigins).AsReadOnly();
        }
    }
}

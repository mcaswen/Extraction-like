using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>读取实际场景、Prefab 实例和导航配置；不执行玩法、不写资产、不生成地图几何。</summary>
    public static class MapGraphSceneCollector
    {
        public static MapGraphSceneSnapshot Capture(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在 Edit Mode 采集场景作者配置。");
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new ArgumentException("需要已保存并加载的场景。", nameof(scene));
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            var diagnostics = new List<string>();
            var zoneTargets = Collect<TargetZoneAuthoring>(scene);
            var clusters = Collect<GameplayTargetClusterAuthoringBase>(scene);
            var actors = Collect<AgentPawnRoot>(scene);
            var surfaces = Collect<NavMeshSurface>(scene);
            var zones = new List<MapGraphSceneZone>();
            var nodes = new List<MapGraphSceneNode>();
            var zoneIds = new Dictionary<TargetZoneAuthoring, string>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in zoneTargets)
            {
                string sourceId = SourceId(target), id = StableId("zone", sourceId);
                if (!identities.Add(sourceId)) diagnostics.Add("DuplicateSceneIdentity:" + sourceId);
                zoneIds.Add(target, id);
                zones.Add(new MapGraphSceneZone(id, sourceId, target.DisplayName, target, BoundsOf(target.RangePoints, target.CenterPosition)));
            }
            var sourceOwnedActive = new HashSet<ActiveEnemyClusterAuthoring>();
            foreach (var cluster in clusters)
                if (cluster is EnemySourceClusterAuthoring source && source.ActiveEnemyCluster != null)
                {
                    if (!sourceOwnedActive.Add(source.ActiveEnemyCluster)) diagnostics.Add("SharedActiveEnemyCluster:" + Hierarchy(source.transform));
                }
            var candidateBuffer = new List<Vector3>();
            string unassignedSource = guid + ":unassigned", unassignedId = StableId("zone", unassignedSource);
            bool hasUnassigned = false; Rect unassignedBounds = default;
            foreach (var cluster in clusters)
            {
                if (cluster is ActiveEnemyClusterAuthoring active && sourceOwnedActive.Contains(active)) continue;
                string sourceId = SourceId(cluster), id = StableId("cluster", sourceId);
                if (!identities.Add(sourceId)) diagnostics.Add("DuplicateSceneIdentity:" + sourceId);
                string zoneId = string.Empty;
                if (cluster.Zone == null)
                {
                    zoneId = unassignedId;
                    var bounds = BoundsOf(cluster.RangePoints, cluster.CenterPosition);
                    unassignedBounds = hasUnassigned ? MapGraphGeometry.Union(unassignedBounds, bounds) : bounds;
                    hasUnassigned = true;
                }
                else if (!zoneIds.TryGetValue(cluster.Zone, out zoneId)) diagnostics.Add("ForeignClusterZone:" + Hierarchy(cluster.transform));
                else
                {
                    bool registered = false;
                    foreach (var item in cluster.Zone.Clusters) if (item == cluster) { registered = true; break; }
                    if (!registered) diagnostics.Add("ClusterNotRegisteredInZone:" + Hierarchy(cluster.transform));
                }
                var candidates = new List<MapGraphAnchorCandidate>();
                MapGraphNodeKind kind = MapGraphNodeKind.Custom;
                if (cluster is ResourceClusterAuthoring resource)
                {
                    kind = MapGraphNodeKind.Resource;
                    foreach (var member in resource.ResourceMembers)
                    {
                        if (member?.EntityObject == null || !member.EntityObject.activeInHierarchy) continue;
                        if (!resource.TryCopyNavigationApproachCandidates(member.EntityObject, resource.CenterPosition, candidateBuffer)) continue;
                        string memberId = SourceId(member.EntityObject);
                        foreach (var candidate in candidateBuffer) candidates.Add(new MapGraphAnchorCandidate(candidate, memberId, "ResourceApproach"));
                    }
                }
                else if (cluster is EnemySourceClusterAuthoring source)
                {
                    kind = MapGraphNodeKind.EnemySource;
                    foreach (var point in source.SpawnPoints)
                        if (point != null && point.gameObject.activeInHierarchy)
                        {
                            candidates.Add(new MapGraphAnchorCandidate(point.position, SourceId(point)));
                            candidates.Add(new MapGraphAnchorCandidate(source.ProjectPositionToGround(point.position), SourceId(point), "GroundProjection"));
                            if (point.TryGetComponent<global::EnemySpawnPoint>(out var spawn) && spawn.TryGetNavigationGroundCandidate(out var spawnGround))
                                candidates.Add(new MapGraphAnchorCandidate(spawnGround, SourceId(point), "SpawnNavigation"));
                        }
                }
                else if (cluster is ExtractionClusterAuthoring extraction)
                {
                    kind = MapGraphNodeKind.Extraction;
                    foreach (var member in extraction.ExtractionMembers)
                        if (member?.EntityObject != null && member.EntityObject.activeInHierarchy)
                            candidates.Add(new MapGraphAnchorCandidate(member.Position, SourceId(member.EntityObject)));
                }
                else if (cluster is ActiveEnemyClusterAuthoring standalone)
                {
                    kind = MapGraphNodeKind.ActiveEnemy;
                    foreach (var member in standalone.InitialEnemies)
                        if (member?.EntityObject != null)
                            candidates.Add(new MapGraphAnchorCandidate(member.Position, SourceId(member.EntityObject)));
                }
                else diagnostics.Add("UnsupportedCluster:" + Hierarchy(cluster.transform));
                // 几何距离只用于稳定地优先尝试中心附近的真实成员；可达与高度另由导航扫描验证。
                Vector3 center = cluster.CenterPosition;
                candidates.Sort((a, b) => CompareCandidate(a, b, center));
                if (candidates.Count == 0) diagnostics.Add("NoAnchorCandidates:" + Hierarchy(cluster.transform));
                string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cluster.gameObject) ?? string.Empty;
                nodes.Add(new MapGraphSceneNode(id, sourceId, zoneId, cluster.DisplayName, Hierarchy(cluster.transform), prefab,
                    cluster, center, BoundsOf(cluster.RangePoints, center), kind, candidates));
            }
            if (hasUnassigned) zones.Add(new MapGraphSceneZone(unassignedId, unassignedSource, "未分区", null, unassignedBounds, true));
            zones.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            nodes.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var profiles = CaptureProfiles(actors, diagnostics);
            string navigationFingerprint = NavigationFingerprint(surfaces, diagnostics);
            string runtimeNavigationFingerprint = Gameplay.MapGraph.Binding.MapGraphNavigationFingerprint.Capture(scene);
            var fingerprint = new StringBuilder(guid);
            foreach (var zone in zones) { fingerprint.Append('|').Append(zone.SourceObjectId).Append('|').Append(zone.Name); AppendBounds(fingerprint, zone.WorldBounds); }
            foreach (var node in nodes)
            {
                fingerprint.Append('|').Append(node.SourceObjectId).Append('|').Append(node.ZoneId).Append('|').Append((int)node.Kind);
                fingerprint.Append('|').Append(node.Name).Append('|').Append(node.HierarchyPath).Append('|').Append(node.PrefabPath);
                AppendVector(fingerprint, node.WorldCenter);
                AppendBounds(fingerprint, node.WorldBounds);
                foreach (var candidate in node.Candidates) { fingerprint.Append('|').Append(candidate.MemberSourceId).Append('|').Append(candidate.Derivation); AppendVector(fingerprint, candidate.Position); }
            }
            foreach (var profile in profiles)
            {
                fingerprint.Append('|').Append(profile.Data.ProfileId);
                for (int i = 0; i < profile.AgentSourceIds.Count; i++)
                { fingerprint.Append('|').Append(profile.AgentSourceIds[i]); AppendVector(fingerprint, profile.AgentOrigins[i]); }
            }
            return new MapGraphSceneSnapshot(scene.path, guid, Hash128.Compute(fingerprint.ToString()).ToString(), navigationFingerprint,
                zones, nodes, profiles, diagnostics, runtimeNavigationFingerprint);
        }

        public static string SourceId(UnityEngine.Object value) => GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        public static string StableId(string prefix, string sourceId) => prefix + "_" + Hash128.Compute(sourceId).ToString();

        private static List<T> Collect<T>(Scene scene) where T : Component
        {
            var items = new List<T>();
            var buffer = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            { root.GetComponentsInChildren(true, buffer); items.AddRange(buffer); }
            return items;
        }

        private static List<MapGraphSceneProfile> CaptureProfiles(List<AgentPawnRoot> actors, List<string> diagnostics)
        {
            actors.Sort((a, b) => string.CompareOrdinal(SourceId(a), SourceId(b)));
            var profiles = new Dictionary<string, AgentNavigationProfile>(StringComparer.Ordinal);
            var data = new Dictionary<string, MapGraphNavigationProfileData>(StringComparer.Ordinal);
            var identities = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var origins = new Dictionary<string, List<Vector3>>(StringComparer.Ordinal);
            foreach (var actor in actors)
            {
                var nav = actor.GetComponent<NavMeshAgent>();
                if (nav == null) { diagnostics.Add("AgentMissingNavigation:" + Hierarchy(actor.transform)); continue; }
                // Edit Mode 的 Agent 尚未绑定 NavMesh，不能调用实例 GetAreaCost。
                // 现有 Prefab 序列化类型/区域/尺寸，区域成本来自项目；运行时覆写由成本服务按 profile 失效处理。
                using var authored = new SerializedObject(nav);
                var costs = new float[32];
                for (int i = 0; i < costs.Length; i++) costs[i] = NavMesh.GetAreaCost(i);
                var profile = new AgentNavigationProfile(authored.FindProperty("m_AgentTypeID").intValue,
                    unchecked((int)authored.FindProperty("m_WalkableMask").longValue),
                    Mathf.Max(0.5f, authored.FindProperty("m_Radius").floatValue * 2),
                    Mathf.Max(0.5f, authored.FindProperty("m_Height").floatValue * 0.5f), costs);
                var snapshot = MapGraphNavigationCostService.CaptureProfile(profile);
                string id = snapshot.ProfileId;
                if (!profiles.ContainsKey(id))
                {
                    profiles.Add(id, profile); data.Add(id, snapshot);
                    identities.Add(id, new List<string>()); origins.Add(id, new List<Vector3>());
                }
                identities[id].Add(SourceId(actor));
                origins[id].Add(nav.transform.position - Vector3.up * authored.FindProperty("m_BaseOffset").floatValue * Mathf.Abs(nav.transform.lossyScale.y));
            }
            var keys = new List<string>(profiles.Keys); keys.Sort(StringComparer.Ordinal);
            var result = new List<MapGraphSceneProfile>();
            foreach (string key in keys) result.Add(new MapGraphSceneProfile(profiles[key], data[key], identities[key], origins[key]));
            if (result.Count == 0) diagnostics.Add("NoAgentNavigationProfile");
            return result;
        }

        private static string NavigationFingerprint(List<NavMeshSurface> surfaces, List<string> diagnostics)
        {
            surfaces.Sort((a, b) => string.CompareOrdinal(SourceId(a), SourceId(b)));
            var key = new StringBuilder();
            foreach (var surface in surfaces)
            {
                string path = AssetDatabase.GetAssetPath(surface.navMeshData);
                if (surface.navMeshData == null || path.Length == 0)
                { diagnostics.Add("MissingNavMeshData:" + Hierarchy(surface.transform)); continue; }
                key.Append('|').Append(SourceId(surface)).Append('|').Append(AssetDatabase.AssetPathToGUID(path))
                    .Append('|').Append(AssetDatabase.GetAssetDependencyHash(path));
                AppendVector(key, surface.transform.position); AppendVector(key, surface.transform.eulerAngles);
                AppendVector(key, surface.transform.lossyScale);
                if (!surface.isActiveAndEnabled) diagnostics.Add("InactiveNavMeshSurface:" + Hierarchy(surface.transform));
            }
            if (surfaces.Count == 0) diagnostics.Add("NoNavMeshSurface");
            return Hash128.Compute(key.ToString()).ToString();
        }

        private static int CompareCandidate(MapGraphAnchorCandidate a, MapGraphAnchorCandidate b, Vector3 center)
        {
            int comparison = (a.Position - center).sqrMagnitude.CompareTo((b.Position - center).sqrMagnitude);
            if (comparison == 0) comparison = string.CompareOrdinal(a.MemberSourceId, b.MemberSourceId);
            if (comparison == 0) comparison = a.Position.x.CompareTo(b.Position.x);
                if (comparison == 0) comparison = a.Position.z.CompareTo(b.Position.z);
            if (comparison == 0) comparison = a.Position.y.CompareTo(b.Position.y);
            return comparison == 0 ? string.CompareOrdinal(a.Derivation, b.Derivation) : comparison;
        }

        private static Rect BoundsOf(IReadOnlyList<Vector3> points, Vector3 fallback)
        {
            if (points == null || points.Count == 0) return new Rect(fallback.x - 2, fallback.z - 2, 4, 4);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var point in points) { min = Vector2.Min(min, new Vector2(point.x, point.z)); max = Vector2.Max(max, new Vector2(point.x, point.z)); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AppendBounds(StringBuilder value, Rect rect)
        { AppendVector(value, new Vector3(rect.x, rect.y, rect.width)); AppendVector(value, new Vector3(rect.height, 0, 0)); }
        private static void AppendVector(StringBuilder value, Vector3 point)
            => value.Append('|').Append(point.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(point.y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(point.z.ToString("R", CultureInfo.InvariantCulture));
        private static string Hierarchy(Transform target)
        {
            string path = target.name;
            while (target.parent != null) { target = target.parent; path = target.name + "/" + path; }
            return path;
        }
    }
}

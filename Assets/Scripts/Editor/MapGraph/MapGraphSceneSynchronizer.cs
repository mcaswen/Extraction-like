using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>场景拥有身份和归属，地图拥有人工布局。只合并草稿，不扫描导航或写入世界。</summary>
    public static class MapGraphSceneSynchronizer
    {
        public static MapGraphSceneSynchronizationResult Prepare(MapGraphSceneSnapshot scene, MapGraphLayoutDraft previous,
            MapGraphGenerationSettings settings, long revision = 0)
        {
            if (scene == null || previous == null || settings == null) throw new ArgumentNullException();
            if (!scene.IsValid || !previous.Graph.IsValid) throw new InvalidOperationException("请先处理场景采集问题：" + string.Join("；", scene.Diagnostics));
            foreach (string source in previous.Nodes.Select(n => n.SourceObjectId).Concat(previous.Zones.Select(z => z.SourceObjectId)))
                CheckScene(source, scene.SceneGuid);
            foreach (string source in scene.Nodes.Select(n => n.SourceObjectId).Concat(scene.Zones.Select(z => z.SourceObjectId)))
            {
                CheckScene(source, scene.SceneGuid);
                if (source.EndsWith("-0-0", StringComparison.Ordinal)) throw new InvalidOperationException("请先保存新增的场景对象，再同步地图。");
            }
            var changes = new List<MapGraphValidationIssue>();
            void Changed(string code, string id, string detail, string related = "")
                => changes.Add(new MapGraphValidationIssue(code, id, related, detail, false));
            var defaults = MapGraphLayoutGenerator.CreateReference(scene, settings);
            var zones = new List<MapGraphZoneDefinition>();
            foreach (var current in defaults.Zones)
            {
                if (previous.Graph.TryGetZone(current.ZoneId, out var old))
                {
                    if (old.SourceObjectId != current.SourceObjectId) throw new InvalidOperationException("区域身份冲突：" + current.DisplayName);
                    zones.Add(new MapGraphZoneDefinition(current.ZoneId, current.DisplayName, old.Bounds, old.NameSafeSize, old.LayoutLocked, current.SourceObjectId, current.IsSynthetic));
                    if (old.DisplayName != current.DisplayName) Changed("SceneZoneRenamed", current.ZoneId, $"区域名称：{old.DisplayName} → {current.DisplayName}");
                }
                else { zones.Add(current); Changed("SceneZoneAdded", current.ZoneId, $"新增区域「{current.DisplayName}」。"); }
            }
            var zoneIndex = zones.ToDictionary(z => z.ZoneId, StringComparer.Ordinal);
            var sceneZones = scene.Zones.ToDictionary(z => z.Id, StringComparer.Ordinal);
            var nodes = new List<MapGraphNodeDefinition>();
            foreach (var current in defaults.Nodes)
            {
                var zone = zoneIndex[current.ZoneId];
                if (previous.Graph.TryGetNode(current.NodeId, out var old))
                {
                    if (old.SourceObjectId != current.SourceObjectId) throw new InvalidOperationException("群身份冲突：" + current.DisplayName);
                    Vector2 local = old.Position;
                    if (old.ZoneId != current.ZoneId)
                    {
                        previous.Graph.TryGetZone(old.ZoneId, out var oldZone);
                        local = FitLocal(new Vector2(old.Position.x / oldZone.Bounds.width * zone.Bounds.width,
                            old.Position.y / oldZone.Bounds.height * zone.Bounds.height), zone, old.Footprint);
                        Changed("SceneNodeReassigned", old.NodeId, $"群「{old.DisplayName}」换区：{oldZone.DisplayName} → {zone.DisplayName}，保留区域内相对位置。", current.ZoneId);
                    }
                    if (old.NodeKind != current.NodeKind) Changed("SceneNodeKindChanged", old.NodeId, $"群「{old.DisplayName}」类型：{old.NodeKind} → {current.NodeKind}");
                    if (old.DisplayName != current.DisplayName || old.Description != current.Description)
                        Changed("SceneNodeRenamed", old.NodeId, $"群名称/层级更新：{old.Description} → {current.Description}");
                    nodes.Add(new MapGraphNodeDefinition(old.NodeId, current.NodeKind, local, current.DisplayName, current.Description,
                        old.Icon, old.IconKind, old.ResourceTier, old.DangerTier, current.ZoneId, old.Footprint, old.RowId, old.ColumnId, old.PositionLocked, current.SourceObjectId));
                }
                else
                {
                    var source = scene.Nodes.First(n => n.Id == current.NodeId); var world = sceneZones[current.ZoneId].WorldBounds;
                    var local = FitLocal(new Vector2((source.WorldCenter.x - world.center.x) / Mathf.Max(1, world.width) * zone.Bounds.width,
                        (source.WorldCenter.z - world.center.y) / Mathf.Max(1, world.height) * zone.Bounds.height), zone, current.Footprint);
                    nodes.Add(current.WithLayout(local, current.RowId, current.ColumnId, false));
                    Changed("SceneNodeAdded", current.NodeId, $"新增群「{zone.DisplayName} / {current.DisplayName}」，已映射到所属区域。");
                }
            }
            var ids = new HashSet<string>(nodes.Select(n => n.NodeId), StringComparer.Ordinal);
            foreach (var old in previous.Nodes.Where(n => !ids.Contains(n.NodeId)))
                Changed("SceneNodeRemoved", old.NodeId, $"移除场景中已不存在的群「{old.DisplayName}」，旧层级：{old.Description}");
            foreach (var old in previous.Zones.Where(z => !zoneIndex.ContainsKey(z.ZoneId)))
                Changed("SceneZoneRemoved", old.ZoneId, $"移除场景中已不存在的区域「{old.DisplayName}」。");
            var edges = previous.Edges.Where(e => ids.Contains(e.FromNodeId) && ids.Contains(e.ToNodeId)).ToArray();
            foreach (var edge in previous.Edges.Where(e => !ids.Contains(e.FromNodeId) || !ids.Contains(e.ToNodeId)))
                Changed("SceneEdgeRemoved", edge.EdgeId, $"移除已失去端点的连线「{NodeLabel(edge.FromNodeId)} ↔ {NodeLabel(edge.ToNodeId)}」。");
            var exclusions = previous.Constraints.ExcludedConnections.Where(e => ids.Contains(e.FirstNodeId) && ids.Contains(e.SecondNodeId)).ToArray();
            if (exclusions.Length != previous.Constraints.ExcludedConnections.Count)
                Changed("SceneExclusionsRemoved", "graph", $"清理 {previous.Constraints.ExcludedConnections.Count - exclusions.Length} 对失去端点的禁连。");
            string start = ids.Contains(previous.StartNodeId) ? previous.StartNodeId : "";
            var layout = changes.Count == 0 ? previous : MapGraphGridPlacement.WithPositions(new MapGraphLayoutDraft(zones, nodes, edges,
                new MapGraphLayoutConstraints(previous.Constraints.Alignments, exclusions), start), null);
            return new MapGraphSceneSynchronizationResult(scene, previous, layout, changes, revision);
            string NodeLabel(string id)
            {
                previous.Graph.TryGetNode(id, out var node);
                previous.Graph.TryGetZone(node.ZoneId, out var zone);
                return zone.DisplayName + " / " + node.DisplayName + "（" + id.Substring(Math.Max(0, id.Length - 6)) + "）";
            }
        }

        private static Vector2 FitLocal(Vector2 local, MapGraphZoneDefinition zone, Vector2 footprint)
        {
            var half = Vector2.Max(Vector2.zero, (zone.Bounds.size - footprint) * 0.5f);
            return new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
        }
        private static void CheckScene(string source, string sceneGuid)
        {
            const string prefix = "GlobalObjectId_V1-2-";
            if (source.StartsWith(prefix, StringComparison.Ordinal) && source.Length >= prefix.Length + 32 &&
                source.Substring(prefix.Length, 32) != sceneGuid)
                throw new InvalidOperationException("地图属于其他场景，不能将其节点作为已删除对象同步。");
        }
    }
}

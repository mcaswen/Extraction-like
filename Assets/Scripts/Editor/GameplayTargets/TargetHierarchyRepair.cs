using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Targets.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalySearch.Editor.GameplayTargets
{
    /// <summary>显式执行的场景接线修复；不安装运行时或逐帧扫描。</summary>
    public static class TargetHierarchyRepair
    {
        [Serializable]
        public sealed class Report
        {
            public int zones, clusters, resources, exits, spawnPoints;
            public List<string> changes = new List<string>();
            public List<string> warnings = new List<string>();
        }

        public static Report Repair(Scene scene, Material fallbackMaterial)
        {
            if (!scene.IsValid() || !scene.isLoaded || Application.isPlaying)
                throw new InvalidOperationException("层级修复必须在已加载场景的 Edit Mode 执行。");

            var report = new Report();
            var clusters = Components<GameplayTargetClusterAuthoringBase>(scene);
            var zones = Components<TargetZoneAuthoring>(scene);
            report.zones = zones.Length;
            report.clusters = clusters.Length;
            report.resources = Components<LootBoxEntity>(scene).Length;
            report.exits = Components<ExtractionPointController>(scene).Length;
            report.spawnPoints = Components<EnemySpawnPoint>(scene).Length;

            foreach (var cluster in clusters)
            {
                if (cluster is ResourceClusterAuthoring)
                    RepairMembers<LootBoxEntity>(cluster, "_resourceMembers", report);
                else if (cluster is ExtractionClusterAuthoring)
                    RepairMembers<ExtractionPointController>(cluster, "_extractionMembers", report);
                else if (cluster is EnemySourceClusterAuthoring)
                    RepairSpawns(cluster, report);

                var zone = Nearest<TargetZoneAuthoring>(cluster.transform.parent);
                var serialized = new SerializedObject(cluster);
                if (zone != null)
                    SetReference(serialized, "_zone", zone, report);
                else if (serialized.FindProperty("_zone").objectReferenceValue == null)
                    report.warnings.Add(Path(cluster) + "：没有祖先 Zone 或显式 Zone，未猜测归属。");
                else
                    report.warnings.Add(Path(cluster) + "：没有祖先 Zone，保留显式 Zone 绑定。");
            }

            // 双向关系在所有 Cluster 的归属修正后统一建立，避免旧 Zone 残留。
            foreach (var zone in zones)
            {
                var owned = clusters.Where(c => c.Zone == zone).Cast<Object>().ToArray();
                RepairReferences(new SerializedObject(zone), "_clusters", owned, report);
            }

            Physics.SyncTransforms();
            foreach (var cluster in clusters.OrderByDescending(c => Depth(c.transform)))
                TargetRangeRendererRepair.Repair(cluster, fallbackMaterial, report);
            foreach (var zone in zones.OrderByDescending(z => Depth(z.transform)))
                TargetRangeRendererRepair.Repair(zone, fallbackMaterial, report);
            return report;
        }

        public static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        public static T Nearest<T>(Transform current) where T : Component
        {
            while (current != null)
            {
                if (current.TryGetComponent<T>(out var component)) return component;
                current = current.parent;
            }
            return null;
        }

        public static T[] Owned<T>(GameplayTargetClusterAuthoringBase owner) where T : Component =>
            owner.GetComponentsInChildren<T>(true)
                .Where(c => Nearest<GameplayTargetClusterAuthoringBase>(c.transform) == owner).ToArray();

        public static string Path(Component component)
        {
            var segments = new List<string>();
            for (var t = component.transform; t != null; t = t.parent)
                segments.Add(t.name + "[" + t.GetSiblingIndex() + "]");
            segments.Reverse();
            return string.Join("/", segments) + ":" + component.GetType().Name;
        }

        private static int Depth(Transform transform)
        {
            int depth = 0;
            for (; transform != null; transform = transform.parent) depth++;
            return depth;
        }

        private static void RepairMembers<T>(GameplayTargetClusterAuthoringBase owner, string field, Report report)
            where T : Component
        {
            var expected = Owned<T>(owner).Select(c => c.gameObject).ToList();
            var remaining = new HashSet<GameObject>(expected);
            var serialized = new SerializedObject(owner);
            var list = serialized.FindProperty(field);
            // 从尾部移除会保留最后重复项，故从前向后处理，保留首次成员的 ID/状态。
            for (int i = 0; i < list.arraySize;)
            {
                var item = list.GetArrayElementAtIndex(i);
                var entity = item.FindPropertyRelative("_entityObject").objectReferenceValue as GameObject;
                var matches = entity == null ? Array.Empty<T>() : entity.GetComponentsInChildren<T>(true);
                var resolved = matches.Length == 1 ? matches[0].gameObject : entity;
                if (resolved != null && remaining.Remove(resolved))
                {
                    item.FindPropertyRelative("_entityObject").objectReferenceValue = resolved;
                    i++;
                }
                else list.DeleteArrayElementAtIndex(i);
            }
            foreach (var entity in expected.Where(remaining.Contains))
            {
                int index = list.arraySize++;
                var item = list.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("_entityObject").objectReferenceValue = entity;
                // Unity 扩展数组会复制上一项；新成员不能继承上一项的完成状态或 ID。
                item.FindPropertyRelative("_entityId").stringValue =
                    owner.TargetId + "_Entity_" + Guid.NewGuid().ToString("N");
                item.FindPropertyRelative("_state._hasBeenTouched").boolValue = false;
                item.FindPropertyRelative("_state._hasBeenCompleted").boolValue = false;
            }
            Apply(serialized, field, report);
            if (expected.Count == 0) report.warnings.Add(Path(owner) + "：层级内没有 " + typeof(T).Name);
        }

        private static void RepairSpawns(GameplayTargetClusterAuthoringBase owner, Report report)
        {
            var expected = Owned<EnemySpawnPoint>(owner).Select(p => (Object)p.transform).ToArray();
            RepairReferences(new SerializedObject(owner), "_spawnPoints", expected, report);
            if (expected.Length == 0) report.warnings.Add(Path(owner) + "：层级内没有 EnemySpawnPoint。");
        }

        internal static void RepairReferences(SerializedObject serialized, string field, Object[] expected, Report report)
        {
            var remaining = new HashSet<Object>(expected);
            var ordered = new List<Object>();
            var list = serialized.FindProperty(field);
            for (int i = 0; i < list.arraySize; i++)
            {
                var reference = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (reference != null && remaining.Remove(reference)) ordered.Add(reference);
            }
            ordered.AddRange(expected.Where(remaining.Contains));
            list.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
            Apply(serialized, field, report);
        }

        internal static void SetReference(SerializedObject serialized, string field, Object value, Report report)
        {
            serialized.FindProperty(field).objectReferenceValue = value;
            Apply(serialized, field, report);
        }

        internal static void Apply(SerializedObject serialized, string reason, Report report)
        {
            if (!serialized.hasModifiedProperties) return;
            Undo.RecordObject(serialized.targetObject, "修复目标层级接线");
            serialized.ApplyModifiedProperties();
            Mark(serialized.targetObject);
            report.changes.Add(Path((Component)serialized.targetObject) + "：" + reason);
        }

        internal static void Mark(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}

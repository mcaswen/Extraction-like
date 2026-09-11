using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gameplay.Targets.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalySearch.Editor.GameplayTargets
{
    public static class TargetHierarchyRepairEntry
    {
        public const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";

        [Serializable] private sealed class Evidence
        {
            public string scene, status;
            public TargetHierarchyRepair.Report repair;
            public int validatedTargets;
            public List<string> bindings = new List<string>();
        }

        [MenuItem("Tools/Gameplay Targets/Repair Active Scene From Hierarchy")]
        public static void RepairActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("按层级修复目标接线");
            var report = TargetHierarchyRepair.Repair(scene, TargetRangeRendererRepair.GetOrCreateMaterial());
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[TargetHierarchyRepair] Zone={report.zones} Cluster={report.clusters} " +
                      $"修改={report.changes.Count} 提示={report.warnings.Count}\n" +
                      string.Join("\n", report.changes.Concat(report.warnings)));
        }

        // 仅显式 -executeMethod 运行；不会在用户打开工程/编译时自动修改场景。
        public static void RunBatch()
        {
            string output = Argument("-targetHierarchyOutput");
            try
            {
                if (!Application.isBatchMode || string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("批处理需要隔离工程和输出目录参数。");
                Directory.CreateDirectory(output);
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var invariant = CaptureProtectedValues(scene);
                var material = TargetRangeRendererRepair.GetOrCreateMaterial();
                var report = TargetHierarchyRepair.Repair(scene, material);
                Require(invariant == CaptureProtectedValues(scene), "修复改变了布局或撤离点配置。");
                var evidence = Validate(scene);
                evidence.repair = report;
                AssetDatabase.SaveAssets();
                Require(EditorSceneManager.SaveScene(scene), "保存场景失败。");

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Require(invariant == CaptureProtectedValues(scene), "重载后布局或撤离点配置改变。");
                Validate(scene);
                var repeated = TargetHierarchyRepair.Repair(scene, material);
                Require(repeated.changes.Count == 0, "二次修复不幂等：" + string.Join("\n", repeated.changes));
                evidence.status = "PASS";
                File.WriteAllText(Path.Combine(output, "repair-result.json"), JsonUtility.ToJson(evidence, true));
                Debug.Log($"[TargetHierarchyRepair] PASS targets={evidence.validatedTargets} changes={report.changes.Count}");
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, "repair-error.txt"), ex.ToString());
                }
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static Evidence Validate(Scene scene)
        {
            var evidence = new Evidence { scene = scene.path };
            var clusters = TargetHierarchyRepair.Components<GameplayTargetClusterAuthoringBase>(scene);
            var zones = TargetHierarchyRepair.Components<TargetZoneAuthoring>(scene);
            foreach (var cluster in clusters)
            {
                var zone = TargetHierarchyRepair.Nearest<TargetZoneAuthoring>(cluster.transform.parent);
                if (zone != null) Require(cluster.Zone == zone, TargetHierarchyRepair.Path(cluster) + " Zone 不匹配。");
                if (cluster.Zone != null)
                    Require(cluster.Zone.Clusters.Count(c => c == cluster) == 1, "Zone 列表缺失或重复。");
                foreach (var otherZone in zones.Where(z => z != cluster.Zone))
                    Require(!otherZone.Clusters.Contains(cluster), "旧 Zone 仍保留错归属 Cluster。");

                string field = cluster is ResourceClusterAuthoring ? "_resourceMembers" :
                    cluster is ExtractionClusterAuthoring ? "_extractionMembers" :
                    cluster is EnemySourceClusterAuthoring ? "_spawnPoints" : null;
                if (field != null)
                {
                    var actual = new List<Object>();
                    var list = new SerializedObject(cluster).FindProperty(field);
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var item = list.GetArrayElementAtIndex(i);
                        actual.Add(field == "_spawnPoints" ? item.objectReferenceValue :
                            item.FindPropertyRelative("_entityObject").objectReferenceValue);
                    }
                    var expected = new List<Object>();
                    foreach (Transform child in cluster.GetComponentsInChildren<Transform>(true))
                    {
                        if (TargetHierarchyRepair.Nearest<GameplayTargetClusterAuthoringBase>(child) != cluster) continue;
                        if (field == "_resourceMembers" && child.GetComponent<LootBoxEntity>() != null ||
                            field == "_extractionMembers" && child.GetComponent<ExtractionPointController>() != null)
                            expected.Add(child.gameObject);
                        if (field == "_spawnPoints" && child.GetComponent<EnemySpawnPoint>() != null) expected.Add(child);
                    }
                    Require(actual.All(x => x != null) && actual.Distinct().Count() == actual.Count &&
                            actual.Count == expected.Count && expected.All(actual.Contains),
                        TargetHierarchyRepair.Path(cluster) + " 成员未完整匹配层级。");
                    evidence.bindings.Add(TargetHierarchyRepair.Path(cluster) + " -> " +
                        (cluster.Zone != null ? TargetHierarchyRepair.Path(cluster.Zone) : "<no zone>") +
                        " members=" + actual.Count);
                }
            }
            foreach (var owner in clusters.Cast<GameplayTargetAuthoringBase>().Concat(zones))
            {
                var line = new SerializedObject(owner).FindProperty("_rangeLineRenderer").objectReferenceValue as LineRenderer;
                Require(line != null && line.gameObject == owner.gameObject && line.enabled &&
                        line.sharedMaterial != null && line.sharedMaterial.shader != null && line.loop && line.useWorldSpace,
                    TargetHierarchyRepair.Path(owner) + " 范围线接线不完整。");
                Require(line.positionCount > 1 || owner is ActiveEnemyClusterAuthoring && owner.HasBeenCompleted,
                    TargetHierarchyRepair.Path(owner) + " 范围线没有顶点。");
                for (int i = 0; i < line.positionCount; i++)
                {
                    Vector3 point = line.GetPosition(i);
                    Require(IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z), "范围线包含无效坐标。");
                }
                evidence.validatedTargets++;
            }
            return evidence;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string CaptureProtectedValues(Scene scene)
        {
            // 引用修复不能通过移动已有物体/更改撤离规则蒙混过关。
            return string.Join("\n", TargetHierarchyRepair.Components<Transform>(scene).Select(t =>
                TargetHierarchyRepair.Path(t) + " " + t.localPosition.ToString("R") + " " +
                t.localRotation.ToString("R") + " " + t.localScale.ToString("R"))) + "\n" +
                string.Join("\n", TargetHierarchyRepair.Components<ExtractionPointController>(scene)
                    .Select(p => TargetHierarchyRepair.Path(p) + " " + p.ExtractionPointName + " " +
                        p.ExtractionDurationSeconds.ToString("R") + " " + p.DetectionHorizontalScale.ToString("R") +
                        " " + p.WorldPromptVerticalOffset.ToString("R")));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}

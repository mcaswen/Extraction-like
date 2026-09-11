using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnomalySearch.Automation.SceneRaid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalySearch.Editor.SceneRaid
{
    public static class SceneRaidSceneRepairEntry
    {
        [Serializable] private sealed class Result
        {
            public string runId, status, owner, mission;
            public int before, after, repeatedChanges;
            public List<string> removed = new List<string>();
            public List<string> rebound = new List<string>();
        }
        private static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        public static void RunBatch()
        {
            SceneRaidScenarioConfig config = null;
            try
            {
                config = SceneRaidScenarioConfig.LoadExplicit();
                if (!Application.isBatchMode || config == null || config.mode != "Audit")
                    throw new InvalidOperationException("Explicit isolated batch repair session required.");
                var scene = EditorSceneManager.OpenScene(config.scenePath, OpenSceneMode.Single);
                string protectedBefore = ProtectedValues(scene);
                var result = new Result { runId = config.runId, before = Components<RaidFlowController>(scene).Count() };
                Repair(scene, result);
                Require(ProtectedValues(scene) == protectedBefore, "Repair changed protected scene values.");
                Require(EditorSceneManager.SaveScene(scene), "Cannot save repaired scene.");
                scene = EditorSceneManager.OpenScene(config.scenePath, OpenSceneMode.Single);
                Require(ProtectedValues(scene) == protectedBefore, "Protected scene values changed on reload.");
                var repeated = new Result();
                Repair(scene, repeated);
                result.after = Components<RaidFlowController>(scene).Count();
                result.repeatedChanges = repeated.removed.Count + repeated.rebound.Count;
                Require(result.after == 1 && result.repeatedChanges == 0, "Repair must be idempotent and leave one owner.");
                SceneRaidSceneAudit.Save(scene, config);
                result.status = "PASS";
                File.WriteAllText(Path.Combine(config.outputPath, "repair-result.json"), JsonUtility.ToJson(result, true));
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                if (config != null) File.WriteAllText(Path.Combine(config.outputPath, "repair-error.txt"), ex.ToString());
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        private static void Repair(Scene scene, Result result)
        {
            var owners = Components<RaidFlowController>(scene).ToArray();
            var keeper = owners.SingleOrDefault(x => x.transform.parent == null && x.name == "RaidFlowController");
            Require(keeper != null && keeper.enabled && keeper.gameObject.activeInHierarchy, "Expected active root RaidFlowController is missing or ambiguous.");
            result.owner = SceneRaidIdentityMap.HierarchyPath(keeper.transform);
            result.mission = keeper.MissionName;
            var duplicates = new HashSet<Object>(owners.Where(x => x != keeper).Cast<Object>());
            // 扫描所有引用，避免移除后留下 Missing 引用；只改本场景的引用/实例覆盖。
            foreach (var component in Components<Component>(scene))
            {
                if (component == null || duplicates.Contains(component)) continue;
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference ||
                            property.objectReferenceValue == null || !duplicates.Contains(property.objectReferenceValue)) continue;
                        result.rebound.Add(SceneRaidIdentityMap.HierarchyPath(component.transform) + " :: " + property.propertyPath);
                        property.objectReferenceValue = keeper;
                        changed = true;
                    }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            foreach (var duplicate in owners.Where(x => x != keeper))
            {
                result.removed.Add(SceneRaidIdentityMap.HierarchyPath(duplicate.transform));
                Object.DestroyImmediate(duplicate);
            }
        }

        private static string ProtectedValues(Scene scene)
        {
            // 使用稳定字段，不比较跨加载会改变的 InstanceID。
            var values = new List<string>();
            foreach (var transform in Components<Transform>(scene))
            {
                string path = SceneRaidIdentityMap.HierarchyPath(transform);
                values.Add(path + " | " + JsonUtility.ToJson(new TransformData(transform)));
                values.Add(path + " | components=" + string.Join(",", transform.GetComponents<Component>()
                    .Where(x => !(x is RaidFlowController)).Select(x => x == null ? "Missing" : x.GetType().FullName)));
            }
            var keeper = Components<RaidFlowController>(scene).Single(x => x.transform.parent == null && x.name == "RaidFlowController");
            values.Add("ownerConfig=" + EditorJsonUtility.ToJson(keeper));
            foreach (var point in Components<ExtractionPointController>(scene))
                values.Add(SceneRaidIdentityMap.HierarchyPath(point.transform) + " | pointConfig=" + EditorJsonUtility.ToJson(point));
            values.Sort(StringComparer.Ordinal);
            return string.Join("\n", values);
        }
        [Serializable] private sealed class TransformData
        {
            public Vector3 localPosition, localScale;
            public Quaternion localRotation;
            public bool active;
            public TransformData(Transform value)
            { localPosition = value.localPosition; localScale = value.localScale; localRotation = value.localRotation; active = value.gameObject.activeSelf; }
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}

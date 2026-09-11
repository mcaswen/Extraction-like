using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Core;
using Gameplay.Targets.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AnomalySearch.Editor.SceneRaid
{
    public static class SceneRaidSceneAudit
    {
        [Serializable] private sealed class Reference
        { public string property, identity, assetPath, globalId; }
        [Serializable] private sealed class Entry
        {
            public string identity, component, globalId, prefabAsset, prefabSourceId, serialized;
            public Vector3 position, scale;
            public bool active;
            public List<Reference> references = new List<Reference>();
        }
        [Serializable] private sealed class Count { public string type; public int count; }
        [Serializable] private sealed class Override
        { public string instance, sourceGlobalId, property, value, reference; }
        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public string runId, scene, sceneGuid, status = "AUDITED";
            public int zones, clusters, agents, lootBoxes, extractionPoints, extractionClusters, raidFlows, missingScripts;
            public List<Count> componentCounts = new List<Count>();
            public List<Entry> objects = new List<Entry>();
            public List<Override> prefabOverrides = new List<Override>();
            public List<string> missingScriptObjects = new List<string>();
        }

        public static void Save(Scene scene, SceneRaidScenarioConfig config)
        {
            var report = new Report { runId = config.runId, scene = scene.path, sceneGuid = AssetDatabase.AssetPathToGUID(scene.path) };
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Component>(true)).ToArray();
            report.missingScripts = all.Count(x => x == null);
            foreach (var transform in all.OfType<Transform>())
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing > 0) report.missingScriptObjects.Add(SceneRaidIdentityMap.HierarchyPath(transform) + " count=" + missing);
            }
            report.zones = all.OfType<TargetZoneAuthoring>().Count();
            report.clusters = all.OfType<GameplayTargetClusterAuthoringBase>().Count();
            report.extractionClusters = all.OfType<ExtractionClusterAuthoring>().Count();
            report.agents = all.OfType<AgentPawnRoot>().Count();
            report.lootBoxes = all.OfType<LootBoxEntity>().Count();
            report.extractionPoints = all.OfType<ExtractionPointController>().Count();
            report.raidFlows = all.OfType<RaidFlowController>().Count();
            report.componentCounts = all.Where(x => x != null).GroupBy(x => x.GetType().FullName)
                .OrderBy(g => g.Key).Select(g => new Count { type = g.Key, count = g.Count() }).ToList();
            var visitedAssets = new HashSet<Object>();
            var prefabRoots = new HashSet<GameObject>();
            foreach (var component in all)
            {
                if (component == null) continue;
                bool relevant = component is GameplayTargetAuthoringBase || component is AgentPawnRoot ||
                    component is LootBoxEntity || component is ExtractionPointController || component is RaidFlowController ||
                    component is Camera || component.GetType().Name == "EnemySpawnPoint" ||
                    component.GetType().Name == "NavMeshSurface" || component.GetType().Name == "OceanFFTGenerator";
                if (!relevant) continue;
                Add(component, report, visitedAssets);
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(component);
                if (root != null) prefabRoots.Add(root);
            }
            foreach (var root in prefabRoots.OrderBy(x => SceneRaidIdentityMap.HierarchyPath(x.transform)))
            {
                var modifications = PrefabUtility.GetPropertyModifications(root);
                if (modifications == null) continue;
                foreach (var modification in modifications)
                    report.prefabOverrides.Add(new Override
                    {
                        instance = SceneRaidIdentityMap.HierarchyPath(root.transform),
                        sourceGlobalId = Global(modification.target), property = modification.propertyPath,
                        value = modification.value, reference = Global(modification.objectReference)
                    });
            }
            File.WriteAllText(Path.Combine(config.outputPath, "scene-audit.json"), JsonUtility.ToJson(report, true));
        }
        private static string Global(Object obj) => obj == null ? "" : GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
        private static void Add(Object obj, Report report, HashSet<Object> visitedAssets)
        {
            var component = obj as Component;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(obj);
            var entry = new Entry
            {
                identity = component != null ? SceneRaidIdentityMap.HierarchyPath(component.transform) : AssetDatabase.GetAssetPath(obj),
                component = obj.GetType().FullName, globalId = Global(obj),
                prefabAsset = source != null ? AssetDatabase.GetAssetPath(source) : "", prefabSourceId = Global(source),
                serialized = EditorJsonUtility.ToJson(obj, false), active = component == null || component.gameObject.activeInHierarchy,
                position = component != null ? component.transform.position : Vector3.zero,
                scale = component != null ? component.transform.lossyScale : Vector3.one
            };
            report.objects.Add(entry);
            using (var serialized = new SerializedObject(obj))
            {
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null) continue;
                    var referenced = value as Component;
                    var go = value as GameObject;
                    entry.references.Add(new Reference
                    {
                        property = property.propertyPath, globalId = Global(value), assetPath = AssetDatabase.GetAssetPath(value),
                        identity = referenced != null ? SceneRaidIdentityMap.HierarchyPath(referenced.transform) :
                            go != null ? SceneRaidIdentityMap.HierarchyPath(go.transform) : value.name
                    });
                    if (value is ScriptableObject && visitedAssets.Add(value)) Add(value, report, visitedAssets);
                }
            }
        }
    }
}

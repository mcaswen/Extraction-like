using System.Linq;
using AnomalySearch.Editor.GameplayTargets;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    public sealed class TargetHierarchyRepairTests
    {
        private Scene _scene;
        private Material _material;

        [SetUp] public void SetUp()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(_material);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static T Add<T>(string name, Transform parent = null) where T : Component
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        [Test] public void NestedClustersOwnOnlyTheirLootIncludingInactiveChildren()
        {
            var zone = Add<TargetZoneAuthoring>("zone");
            var outer = Add<ResourceClusterAuthoring>("outer", zone.transform);
            var own = Add<LootBoxEntity>("own", outer.transform);
            var inner = Add<ResourceClusterAuthoring>("inner", outer.transform);
            var nested = Add<LootBoxEntity>("nested", inner.transform);
            TargetHierarchyRepair.Repair(_scene, _material);
            Assert.That(outer.ResourceMembers.Select(m => m.EntityObject), Is.EqualTo(new[] { own.gameObject }));
            Assert.That(inner.ResourceMembers.Select(m => m.EntityObject), Is.EqualTo(new[] { nested.gameObject }));
            Assert.That(zone.Clusters, Is.EquivalentTo(new[] { outer, inner }));
        }

        [Test] public void NearestZoneReplacesWrongBindingAndCleansOldList()
        {
            var outer = Add<TargetZoneAuthoring>("outer");
            var inner = Add<TargetZoneAuthoring>("inner", outer.transform);
            var cluster = Add<ExtractionClusterAuthoring>("exit", inner.transform);
            SetReference(cluster, "_zone", outer);
            var old = new SerializedObject(outer);
            old.FindProperty("_clusters").arraySize = 2;
            old.FindProperty("_clusters").GetArrayElementAtIndex(0).objectReferenceValue = cluster;
            old.FindProperty("_clusters").GetArrayElementAtIndex(1).objectReferenceValue = cluster;
            old.ApplyModifiedPropertiesWithoutUndo();
            var collider = Add<BoxCollider>("point", cluster.transform);
            var point = collider.gameObject.AddComponent<ExtractionPointController>();
            point.ExtractionDurationSeconds = 3.2f;
            point.DetectionHorizontalScale = 0.08f;
            TargetHierarchyRepair.Repair(_scene, _material);
            Assert.That(cluster.Zone, Is.SameAs(inner));
            Assert.That(outer.Clusters, Is.Empty);
            Assert.That(inner.Clusters, Is.EqualTo(new[] { cluster }));
            Assert.That(cluster.ExtractionMembers.Single().EntityObject, Is.SameAs(point.gameObject));
            Assert.That(point.ExtractionDurationSeconds, Is.EqualTo(3.2f));
            Assert.That(point.DetectionHorizontalScale, Is.EqualTo(0.08f));
            Assert.That(new SerializedObject(cluster).FindProperty("_rangeColliderTarget").objectReferenceValue,
                Is.SameAs(point.gameObject));
        }

        [Test] public void ExistingMemberKeepsIdentityAndStateNewMemberDoesNotCopyIt()
        {
            var cluster = Add<ResourceClusterAuthoring>("resource");
            var first = Add<LootBoxEntity>("first", cluster.transform);
            TargetHierarchyRepair.Repair(_scene, _material);
            string id = cluster.ResourceMembers.Single().EntityId;
            var serialized = new SerializedObject(cluster);
            var item = serialized.FindProperty("_resourceMembers").GetArrayElementAtIndex(0);
            item.FindPropertyRelative("_state._hasBeenTouched").boolValue = true;
            item.FindPropertyRelative("_state._hasBeenCompleted").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var second = Add<LootBoxEntity>("second", cluster.transform);
            TargetHierarchyRepair.Repair(_scene, _material);
            Assert.That(cluster.ResourceMembers[0].EntityObject, Is.SameAs(first.gameObject));
            Assert.That(cluster.ResourceMembers[0].EntityId, Is.EqualTo(id));
            Assert.That(cluster.ResourceMembers[0].HasBeenCompleted, Is.True);
            Assert.That(cluster.ResourceMembers[1].EntityObject, Is.SameAs(second.gameObject));
            Assert.That(cluster.ResourceMembers[1].EntityId, Is.Not.EqualTo(id));
            Assert.That(cluster.ResourceMembers[1].HasBeenCompleted, Is.False);
            Assert.That(cluster.ResourceMembers[1].HasBeenTouched, Is.False);
        }

        [Test] public void WrongRendererIsNotMutatedAndRepairIsIdempotent()
        {
            var a = Add<ExtractionClusterAuthoring>("a");
            var b = Add<ExtractionClusterAuthoring>("b");
            var other = b.gameObject.AddComponent<LineRenderer>();
            SetReference(a, "_rangeLineRenderer", other);
            TargetHierarchyRepair.Repair(_scene, _material);
            var lineA = a.GetComponent<LineRenderer>();
            Assert.That(lineA, Is.Not.SameAs(other));
            Assert.That(lineA.sharedMaterial, Is.SameAs(_material));
            Assert.That(lineA.loop && lineA.useWorldSpace && lineA.enabled, Is.True);
            Assert.That(lineA.positionCount, Is.GreaterThan(1));
            int count = TargetHierarchyRepair.Components<LineRenderer>(_scene).Length;
            Assert.That(TargetHierarchyRepair.Repair(_scene, _material).changes, Is.Empty);
            Assert.That(TargetHierarchyRepair.Components<LineRenderer>(_scene).Length, Is.EqualTo(count));
        }

        [Test] public void SpawnCollectionDoesNotLeakAcrossNestedClusterAndRemovesStaleReferences()
        {
            var a = Add<EnemySourceClusterAuthoring>("a");
            var own = Add<EnemySpawnPoint>("own", a.transform);
            var b = Add<EnemySourceClusterAuthoring>("b", a.transform);
            var nested = Add<EnemySpawnPoint>("nested", b.transform);
            var so = new SerializedObject(a);
            var list = so.FindProperty("_spawnPoints");
            list.arraySize = 3;
            list.GetArrayElementAtIndex(0).objectReferenceValue = nested.transform;
            list.GetArrayElementAtIndex(1).objectReferenceValue = own.transform;
            list.GetArrayElementAtIndex(2).objectReferenceValue = own.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            TargetHierarchyRepair.Repair(_scene, _material);
            Assert.That(a.SpawnPoints, Is.EqualTo(new[] { own.transform }));
            Assert.That(b.SpawnPoints, Is.EqualTo(new[] { nested.transform }));
        }

        [Test] public void SharedRangeMaterialRendersVertexColor()
        {
            var material = TargetRangeRendererRepair.GetOrCreateMaterial();
            var line = Add<LineRenderer>("visible line");
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPositions(new[] { new Vector3(-1, 0, 0), new Vector3(1, 0, 0) });
            line.startColor = line.endColor = Color.green;
            line.startWidth = line.endWidth = 0.2f;
            line.gameObject.SetActive(true);
            var camera = Add<Camera>("camera");
            camera.transform.position = new Vector3(0, 0, -5);
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = new RenderTexture(128, 128, 24);
            var pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.gameObject.SetActive(true);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
                string[] arguments = System.Environment.GetCommandLineArgs();
                int index = System.Array.IndexOf(arguments, "-targetHierarchyOutput");
                if (index >= 0 && index + 1 < arguments.Length)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(arguments[index + 1], "range-line.png"), pixels.EncodeToPNG());
                Assert.That(pixels.GetPixels().Count(c => c.g > 0.25f && c.g > c.r * 2 && c.g > c.b * 2),
                    Is.GreaterThan(30), "真实 URP 渲染必须保留 LineRenderer 的顶点颜色。");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [Test] public void ZoneUsesFullOutlineWhenChildCachesAreCold()
        {
            var zone = Add<TargetZoneAuthoring>("zone");
            var a = Add<ResourceClusterAuthoring>("a", zone.transform);
            var b = Add<ResourceClusterAuthoring>("b", zone.transform);
            Add<LootBoxEntity>("loot a", a.transform).transform.position = new Vector3(-7, 0, 3);
            Add<LootBoxEntity>("loot b", b.transform).transform.position = new Vector3(8, 0, -2);
            TargetHierarchyRepair.Repair(_scene, _material);
            Vector3[] expected = zone.RangePoints.ToArray();
            var cache = typeof(GameplayTargetClusterAuthoringBase).GetField("_rangeCache",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            // 模拟反序列化后尚未执行子群 OnValidate 的空缓存，不改目标或成员事实。
            cache.SetValue(a, new Gameplay.Targets.Runtime.GameplayTargetRangeCache());
            cache.SetValue(b, new Gameplay.Targets.Runtime.GameplayTargetRangeCache());
            Assert.That(a.RangePoints, Is.Empty);
            Assert.That(b.RangePoints, Is.Empty);
            zone.RefreshRangeShape();
            Assert.That(zone.RangePoints.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(Vector3.Distance(zone.RangePoints[i], expected[i]), Is.LessThan(0.0001f));
        }

        private static void SetReference(Object owner, string field, Object reference)
        {
            var so = new SerializedObject(owner);
            so.FindProperty(field).objectReferenceValue = reference;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

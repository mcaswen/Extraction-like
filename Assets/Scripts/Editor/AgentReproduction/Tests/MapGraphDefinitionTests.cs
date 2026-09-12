using System;
using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphDefinitionTests : ReproductionTestFixture
    {
        private MapGraphZoneDefinition Zone(string id = "zone", float x = 100)
            => new MapGraphZoneDefinition(id, "实验室", new Rect(x, 200, 300, 180), new Vector2(100, 28), true, "scene-object-zone");
        private MapGraphNodeDefinition Node(string id = "A", string zone = "zone")
            => new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, new Vector2(-80, 45), "资源群",
                zoneId: zone, footprint: new Vector2(30, 28), rowId: "row", columnId: "column-" + id,
                positionLocked: true, sourceObjectId: "scene-object-" + id);
        private MapGraphLayoutConstraints Constraints()
            => new MapGraphLayoutConstraints(new[] { new MapGraphAlignmentConstraint("row", MapGraphAxis.Horizontal, 335, true) },
                new[] { new MapGraphConnectionExclusion("C", "A") });
        private MapGraphNavigationBakeData Bake()
        {
            var costs = new float[32]; for (int i = 0; i < 32; i++) costs[i] = 1; costs[3] = 7;
            return new MapGraphNavigationBakeData("scene-hash", "nav-hash", 42,
                new MapGraphNavigationProfileData("profile", 0, -1, 2, 0.5f, costs),
                new[] { new MapGraphNavigationEdgeBake("AB", "A", "B", Vector3.zero, Vector3.right * 10,
                    12.5f, float.PositiveInfinity, reverseFailure: "PathPartial") });
        }
        private SO_MapGraphDefinition CreateDefinition()
        {
            var value = World.Own(ScriptableObject.CreateInstance<SO_MapGraphDefinition>());
            value.ApplyCommandData("command-map", "指挥地图", "A", new[] { Zone(), Zone("empty", 500) },
                new[] { Node(), Node("B").WithLayout(new Vector2(80, 45), "row", "column-B", false),
                    Node("C").WithLayout(new Vector2(-80, -45), "row-C", "column-A", false) },
                new[] { new MapGraphEdgeDefinition("AB", "A", "B", 99, MapGraphAxis.Horizontal, MapGraphEdgeOrigin.Manual,
                    3, 5, 2, true, Color.cyan) }, Constraints(), Bake());
            return value;
        }

        [UnityTest]
        public IEnumerator SaveReloadKeepsZonesManualEditsAndNavigationIdentity()
        {
            const string assetPath = "Assets/MapGraphDefinition_Reproduction.asset";
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Null);
            var definition = CreateDefinition();
            try
            {
                AssetDatabase.CreateAsset(definition, assetPath);
                AssetDatabase.SaveAssets();
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Resources.UnloadAsset(definition);
                var loaded = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(assetPath);
                Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(guid));
                AssertDefinition(loaded);
                Assert.That(new MapGraphService(loaded).IsValid, Is.True);
            }
            finally { AssetDatabase.DeleteAsset(assetPath); }
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator LocalCoordinatesFollowZoneWithoutChangingPathCost()
        {
            var definition = CreateDefinition();
            var original = new MapGraphService(definition);
            Assert.That(original.GetNodePosition("A"), Is.EqualTo(new Vector2(170, 335)));
            var cost = new MapGraphCostSnapshot("profile", 1, new[] { new MapGraphEdgeCost("AB", 12, 14) });
            var before = new MapGraphPathfindingService(original, cost).ResolveFromNode("A", "B");
            definition.ApplyCommandData(definition.MapId, definition.DisplayName, "A", new[] { Zone(x: 700), Zone("empty") },
                definition.Nodes, definition.Edges, definition.LayoutConstraints, definition.NavigationBake);
            var moved = new MapGraphService(definition);
            Assert.That(moved.GetNodePosition("A"), Is.EqualTo(new Vector2(770, 335)));
            Assert.That(original.GetNodePosition("A"), Is.EqualTo(new Vector2(170, 335)), "Existing graph snapshots retain their own layout.");
            var after = new MapGraphPathfindingService(moved, cost).ResolveFromNode("A", "B");
            Assert.That(after.IsValid && before.IsValid, Is.True);
            Assert.That(after.RemainingNodeIds, Is.EqualTo(before.RemainingNodeIds));
            Assert.That(after.TotalEstimatedLengthUnits, Is.EqualTo(before.TotalEstimatedLengthUnits));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator BadZoneReferencesAndFutureSchemasAreDiagnosed()
        {
            var missing = new MapGraphService(new[] { Node() }, Array.Empty<MapGraphEdgeDefinition>());
            Assert.That(missing.IsValid, Is.False);
            Assert.That(missing.ValidationErrors, Does.Contain("UnknownZone:A:zone"));
            var duplicate = new MapGraphService(new[] { Node() }, Array.Empty<MapGraphEdgeDefinition>(), zones: new[] { Zone(), Zone() });
            Assert.That(duplicate.ValidationErrors, Does.Contain("DuplicateZone:zone"));
            var definition = CreateDefinition();
            RuntimeFixtureAccess.Configure(definition, "_schemaVersion", 999);
            Assert.That(new MapGraphService(definition).ValidationErrors, Does.Contain("UnsupportedSchema:999"));
            RuntimeFixtureAccess.Configure(definition, "_schemaVersion", SO_MapGraphDefinition.CommandSchemaVersion);
            definition.ApplyCommandData("map", "map", "A", new[] { Zone() }, new[] { Node(zone: "") },
                Array.Empty<MapGraphEdgeDefinition>(), Constraints(), Bake());
            Assert.That(new MapGraphService(definition).ValidationErrors, Does.Contain("MissingNodeZone:A"));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator CollectionsAreCopiedAndEditsProduceNewNodeDefinitions()
        {
            var nodes = new List<MapGraphNodeDefinition> { Node() };
            var zones = new List<MapGraphZoneDefinition> { Zone() };
            var definition = World.Own(ScriptableObject.CreateInstance<SO_MapGraphDefinition>());
            definition.ApplyCommandData("map", "map", "A", zones, nodes, Array.Empty<MapGraphEdgeDefinition>(), Constraints(), Bake());
            nodes.Clear(); zones.Clear();
            Assert.That(definition.Nodes.Count, Is.EqualTo(1));
            Assert.That(definition.Zones.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<MapGraphNodeDefinition>)definition.Nodes).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<MapGraphZoneDefinition>)definition.Zones).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<float>)definition.NavigationBake.Profile.AreaCosts)[3] = 8);
            var original = definition.Nodes[0];
            var moved = original.WithLayout(Vector2.zero, "new-row", "new-column", false);
            Assert.That(original.Position, Is.EqualTo(new Vector2(-80, 45)));
            Assert.That(moved.SourceObjectId, Is.EqualTo(original.SourceObjectId));
            Assert.That(moved.NodeId, Is.EqualTo(original.NodeId));
            Assert.That(moved.PositionLocked, Is.False);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator FailedDirectionAndManualExclusionSurviveJsonRoundTrip()
        {
            var bake = JsonUtility.FromJson<MapGraphNavigationBakeData>(JsonUtility.ToJson(Bake()));
            Assert.That(bake.Edges[0].ForwardAvailable, Is.True);
            Assert.That(bake.Edges[0].ForwardLength, Is.EqualTo(12.5f));
            Assert.That(bake.Edges[0].ReverseAvailable, Is.False);
            Assert.That(bake.Edges[0].ReverseLength, Is.EqualTo(float.PositiveInfinity));
            Assert.That(bake.Edges[0].ReverseFailure, Is.EqualTo("PathPartial"));
            var constraints = JsonUtility.FromJson<MapGraphLayoutConstraints>(JsonUtility.ToJson(Constraints()));
            Assert.That(constraints.IsExcluded("A", "C") && constraints.IsExcluded("C", "A"), Is.True);
            Assert.That(constraints.IsExcluded("A", "B"), Is.False);
            Assert.That(constraints.Alignments[0].Coordinate, Is.EqualTo(335));
            Assert.That(constraints.Alignments[0].Locked, Is.True);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ExplicitReplacementIsAtomicAndInvalidatesCachedViews()
        {
            var definition = CreateDefinition();
            var oldNodes = definition.Nodes;
            long revision = definition.Revision;
            Assert.Throws<ArgumentNullException>(() => definition.ApplyCommandData("bad", "bad", "", definition.Zones,
                null, definition.Edges, Constraints(), Bake()));
            Assert.That(definition.Revision, Is.EqualTo(revision));
            Assert.That(definition.MapId, Is.EqualTo("command-map"));
            definition.ApplyCommandData("changed", "changed", "C", definition.Zones, new[] { Node("C") },
                Array.Empty<MapGraphEdgeDefinition>(), Constraints(), Bake());
            Assert.That(definition.Revision, Is.EqualTo(revision + 1));
            Assert.That(definition.Nodes[0].NodeId, Is.EqualTo("C"));
            Assert.That(oldNodes[0].NodeId, Is.EqualTo("A"));
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(CreateDefinition()), definition);
            Assert.That(definition.Nodes[0].NodeId, Is.EqualTo("A"));
            AssertDefinition(definition);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator NestedOverwriteRefreshesReadOnlyCollections()
        {
            var constraints = Constraints();
            Assert.That(constraints.Alignments[0].Id, Is.EqualTo("row"));
            var replacement = new MapGraphLayoutConstraints(
                new[] { new MapGraphAlignmentConstraint("new-row", MapGraphAxis.Horizontal, 99) },
                new[] { new MapGraphConnectionExclusion("B", "C") });
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(replacement), constraints);
            Assert.That(constraints.Alignments[0].Id, Is.EqualTo("new-row"));
            Assert.That(constraints.IsExcluded("A", "C"), Is.False);
            var bake = Bake();
            Assert.That(bake.Edges[0].EdgeId, Is.EqualTo("AB"));
            var failed = new MapGraphNavigationEdgeBake("BC", "B", "C", Vector3.zero, Vector3.one,
                3, 3, "Rejected", "Rejected");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new MapGraphNavigationBakeData("scene", "nav", 2,
                bake.Profile, new[] { failed })), bake);
            Assert.That(bake.Edges[0].EdgeId, Is.EqualTo("BC"));
            Assert.That(bake.Edges[0].ForwardAvailable || bake.Edges[0].ReverseAvailable, Is.False,
                "An explicit failure must not become a valid edge just because a numeric distance is present.");
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator LoadingLegacyAssetNeverAutoMigratesIt()
        {
            string[] guids = AssetDatabase.FindAssets("t:SO_MapGraphDefinition");
            SO_MapGraphDefinition legacy = null;
            foreach (string guid in guids)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.Nodes.Count == 25) { legacy = candidate; break; }
            }
            Assert.That(legacy, Is.Not.Null);
            string before = EditorJsonUtility.ToJson(legacy);
            var graph = new MapGraphService(legacy);
            Assert.That(legacy.SchemaVersion, Is.Zero);
            Assert.That(legacy.IsCommandGraph, Is.False);
            Assert.That(graph.IsValid, Is.True);
            foreach (var node in legacy.Nodes) Assert.That(graph.GetNodePosition(node.NodeId), Is.EqualTo(node.Position));
            Assert.That(EditorJsonUtility.ToJson(legacy), Is.EqualTo(before));
            ContractCompleted = true;
            yield break;
        }

        private static void AssertDefinition(SO_MapGraphDefinition value)
        {
            Assert.That(value.IsCommandGraph, Is.True);
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(value.Zones.Count, Is.EqualTo(2), "Empty zones remain part of the map.");
            Assert.That(value.Zones[0].DisplayName, Is.EqualTo("实验室"));
            Assert.That(value.Zones[0].NameSafeBounds.center, Is.EqualTo(value.Zones[0].Bounds.center));
            Assert.That(value.Zones[0].LayoutLocked, Is.True);
            Assert.That(value.Nodes[0].PositionLocked, Is.True);
            Assert.That(value.Nodes[0].SourceObjectId, Is.EqualTo("scene-object-A"));
            Assert.That(value.Nodes[0].RowId, Is.EqualTo("row"));
            Assert.That(value.Edges[0].Axis, Is.EqualTo(MapGraphAxis.Horizontal));
            Assert.That(value.Edges[0].Origin, Is.EqualTo(MapGraphEdgeOrigin.Manual));
            Assert.That(value.Edges[0].FromInset, Is.EqualTo(3));
            Assert.That(value.Edges[0].ToInset, Is.EqualTo(5));
            Assert.That(value.Edges[0].WidthOverride, Is.EqualTo(2));
            Assert.That(value.Edges[0].UseColorOverride, Is.True);
            Assert.That(value.Edges[0].ColorOverride, Is.EqualTo(Color.cyan));
            Assert.That(value.NavigationBake.SceneFingerprint, Is.EqualTo("scene-hash"));
            Assert.That(value.NavigationBake.NavigationFingerprint, Is.EqualTo("nav-hash"));
            Assert.That(value.NavigationBake.Revision, Is.EqualTo(42));
            Assert.That(value.NavigationBake.Profile.ProfileId, Is.EqualTo("profile"));
            Assert.That(value.NavigationBake.Profile.AreaCosts[3], Is.EqualTo(7));
            Assert.That(value.LayoutConstraints.IsExcluded("A", "C"), Is.True);
            Assert.That(value.GenerationSettings.MaximumSearchStates, Is.GreaterThan(0));
        }
    }
}

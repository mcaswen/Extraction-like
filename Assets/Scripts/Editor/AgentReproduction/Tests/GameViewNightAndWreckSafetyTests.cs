using System.Linq;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    /// <summary>Read-only checks for game-camera ground lights and wreck composition.</summary>
    public sealed class GameViewNightAndWreckSafetyTests
    {
        private Scene scene;
        private SceneSetup[] setup;
        private GameObject root;
        private bool active;

        [OneTimeSetUp]
        public void OpenFormalScene()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            root = scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.GameViewNightRoot);
            active = root.activeSelf;
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (root != null) root.SetActive(active);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void SavedRootIsCompleteAndBoundToExistingCycle()
        {
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateGameViewNightRoot(root, true));
            var controller = root.GetComponent<AerospaceNightGroundLighting>();
            Assert.That(controller.Emissions.Length, Is.EqualTo(7));
            Assert.That(controller.Lights.Length, Is.EqualTo(3));
            Assert.That(controller.cycle.cycleSeconds, Is.EqualTo(160f));
        }

        [Test]
        public void RootHasNoPhysicsGameplayOrNavigationAgents()
        {
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<NavMeshAgent>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<AgentPawnRoot>(true), Is.Empty);
            var modifier = root.GetComponent<NavMeshModifier>();
            Assert.That(modifier.ignoreFromBuild && modifier.applyToChildren && modifier.AffectsAgentType(0) && modifier.AffectsAgentType(1234), Is.True);
        }

        [Test]
        public void ActualNavMeshSurfaceCollectionExcludesEveryNewMesh()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene, root.transform));

        [Test]
        public void TogglePreservesAllOriginalSerializedObjects()
        {
            string before = AerospaceSceneLayoutBuilder.CaptureGameViewNightOriginalState(scene);
            try
            {
                root.SetActive(false);
                Assert.That(AerospaceSceneLayoutBuilder.CaptureGameViewNightOriginalState(scene), Is.EqualTo(before));
                root.SetActive(true);
                Assert.That(AerospaceSceneLayoutBuilder.CaptureGameViewNightOriginalState(scene), Is.EqualTo(before));
            }
            finally { root.SetActive(active); }
        }

        [Test]
        public void BothComparisonStatesPreserveTwoAgentNavigationExactly()
        {
            var before = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            try
            {
                foreach (bool enabled in new[] { false, true })
                {
                    root.SetActive(enabled);
                    Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before, AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene)));
                }
            }
            finally { root.SetActive(active); }
        }

        [Test]
        public void NightPhaseRaisesOnlyNewLocalLightsWithoutShadows()
        {
            var controller = root.GetComponent<AerospaceNightGroundLighting>();
            controller.ApplyPhase(0f);
            Assert.That(controller.Lights.All(b => b.light.intensity == 0f), Is.True);
            controller.ApplyPhase(.5f);
            Assert.That(controller.Lights.All(b => b.light.intensity == 14f && b.light.shadows == LightShadows.None), Is.True);
            controller.ApplyPhase(controller.cycle.startPhase);
        }

        [Test]
        public void RuntimeEmissionUsesPropertyBlocksAndNeverWritesSharedMaterials()
        {
            var controller = root.GetComponent<AerospaceNightGroundLighting>();
            var originals = controller.Emissions.Select(b => b.renderer.sharedMaterials[b.materialIndex].GetColor("_EmissionColor")).ToArray();
            controller.ApplyPhase(.5f);
            for (int i = 0; i < originals.Length; i++)
                Assert.That(controller.Emissions[i].renderer.sharedMaterials[controller.Emissions[i].materialIndex].GetColor("_EmissionColor"), Is.EqualTo(originals[i]));
            controller.ApplyPhase(controller.cycle.startPhase);
        }

        [Test]
        public void WreckCompositionIsBroadAndTopReadableInsteadOfTowerDependent()
        {
            Transform composition = root.transform.Find("02_Wreck_Top_View_Composition");
            Assert.That(composition.Find("SITE07_Segmented_Survey_Ring"), Is.Null);
            Assert.That(composition.Find("Wreck_Axis_Light_Bands"), Is.Not.Null);
            Assert.That(root.transform.Find("03_Three_Perimeter_Scan_Anchors").childCount, Is.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<Light>(true).Max(l => l.transform.position.y - TerrainHeight(l.transform.position)), Is.LessThan(6f));
        }

        [Test]
        public void LegacyBlueRoadBandsAreRemovedWithoutTextFacingDependency()
        {
            Assert.That(root.transform.Find("01_Top_Readable_Night_Route_Bands"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(AerospaceSceneLayoutBuilder.GameViewNightAssets + "/Meshes/Mesh_GV_Route_Bands_01.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(AerospaceSceneLayoutBuilder.GameViewNightAssets + "/Meshes/Mesh_GV_Route_Bands_02.asset"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(AerospaceSceneLayoutBuilder.GameViewNightAssets + "/Meshes/Mesh_GV_Site07_Segmented_Ring.asset"), Is.Null);
            Assert.That(root.GetComponentsInChildren<TMPro.TextMeshPro>(true), Is.Empty);
        }

        private static float TerrainHeight(Vector3 point)
        {
            foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= origin.x && point.x < origin.x + size.x && point.z >= origin.z && point.z < origin.z + size.z)
                    return terrain.SampleHeight(point) + origin.y;
            }
            return point.y;
        }
    }
}

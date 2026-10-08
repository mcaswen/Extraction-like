using System.Linq;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    /// <summary>Read-only safety checks for the crash-atmosphere, skyline and impact-response set.</summary>
    public sealed class CrashAtmosphereSkylineSafetyTests
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
            root = scene.GetRootGameObjects().Single(item => item.name == AerospaceSceneLayoutBuilder.CrashAtmosphereRoot);
            active = root.activeSelf;
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (root != null) root.SetActive(active);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void SavedRootIsCompleteAndUsesExistingCycle()
        {
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateCrashAtmosphereRoot(root, true));
            var controller = root.GetComponent<AerospaceCrashAtmosphere>();
            Assert.That(controller.cycle.cycleSeconds, Is.EqualTo(160f));
            Assert.That(controller.SteamSystems.Length, Is.EqualTo(2));
            Assert.That(controller.SparkSystem, Is.Not.Null);
            Assert.That(controller.VaporPuffs.Length, Is.EqualTo(4));
            Assert.That(controller.VaporRenderers.Length, Is.EqualTo(4));
            Assert.That(controller.RadarPivots.Length, Is.EqualTo(2));
            Assert.That(controller.SkylineBeaconHeads.Length, Is.EqualTo(2));
            Assert.That(controller.WreckMaterialBindings.Length, Is.GreaterThan(0));
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
        public void ActualNavMeshSurfaceCollectionExcludesEveryNewVisual()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene, root.transform));

        [Test]
        public void TogglePreservesExistingSceneAndBothAgentPathsExactly()
        {
            string originals = AerospaceSceneLayoutBuilder.CaptureCrashAtmosphereOriginalState(scene);
            var navigation = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            try
            {
                foreach (bool enabled in new[] { false, true })
                {
                    root.SetActive(enabled);
                    Assert.That(AerospaceSceneLayoutBuilder.CaptureCrashAtmosphereOriginalState(scene), Is.EqualTo(originals));
                    Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(navigation, AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene)));
                }
            }
            finally { root.SetActive(active); }
        }

        [Test]
        public void ParticlesAreBoundedAndHaveNoCollision()
        {
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(systems.Length, Is.EqualTo(3));
            Assert.That(systems.All(system => !system.collision.enabled && system.main.maxParticles <= 40), Is.True);
            foreach (ParticleSystem system in systems)
            {
                system.Simulate(2.2f, true, true, true);
                Assert.That(system.particleCount, Is.GreaterThan(0), system.name + " did not emit during a deterministic preview.");
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        [Test]
        public void ScanRadarSteamAndWreckGradeAnimateWithoutMutatingSharedMaterials()
        {
            var controller = root.GetComponent<AerospaceCrashAtmosphere>();
            Material scan = controller.ScanBeam.sharedMaterial;
            Color baseColor = scan.GetColor("_BaseColor");
            Color emission = scan.HasProperty("_EmissionColor") ? scan.GetColor("_EmissionColor") : Color.black;
            Quaternion radarBefore = controller.RadarPivots[0].localRotation;
            var sharedWreckMaterials = controller.WreckMaterialBindings.Select(binding => binding.renderer.sharedMaterials[binding.materialIndex]).Distinct().ToArray();
            Color[] wreckColors = sharedWreckMaterials.Select(material => material.GetColor("_BaseColor")).ToArray();
            controller.ApplyPreview(.5f, 4.2f);
            Assert.That(scan.GetColor("_BaseColor"), Is.EqualTo(baseColor));
            if (scan.HasProperty("_EmissionColor")) Assert.That(scan.GetColor("_EmissionColor"), Is.EqualTo(emission));
            Assert.That(controller.RadarPivots[0].localRotation, Is.Not.EqualTo(radarBefore));
            for (int i = 0; i < sharedWreckMaterials.Length; i++) Assert.That(sharedWreckMaterials[i].GetColor("_BaseColor"), Is.EqualTo(wreckColors[i]));
            Assert.That(controller.BeaconLights.All(light => light.shadows == LightShadows.None && light.intensity >= 0f), Is.True);
            Assert.That(controller.FractureRimLight.shadows, Is.EqualTo(LightShadows.None));
            controller.ApplyPreview(controller.cycle.startPhase, 0f);
        }

        [Test]
        public void SkylineUsesTwoDistantTallAerospaceLandmarks()
        {
            Transform skyline = root.transform.Find("02_Long_Range_Aerospace_Skyline");
            Assert.That(skyline.childCount, Is.EqualTo(2));
            Vector3 wreck = new Vector3(290f, 0f, 17f);
            foreach (Transform landmark in skyline)
            {
                Vector3 flat = landmark.position; flat.y = 0f;
                Assert.That(Vector3.Distance(flat, wreck), Is.GreaterThan(70f));
                float top = landmark.GetComponentsInChildren<Renderer>(true).Max(renderer => renderer.bounds.max.y);
                Assert.That(top - TerrainHeight(landmark.position), Is.GreaterThan(18f));
            }
        }

        [Test]
        public void ImpactResponseReadsAlongCrashDirectionWithoutChangingTerrain()
        {
            Renderer response = root.transform.Find("03_Impact_Ground_Response/Directional_Scorch_Oil_And_Charred_Falloff").GetComponent<Renderer>();
            Assert.That(response.bounds.size.x, Is.GreaterThan(48f));
            Assert.That(response.bounds.size.z, Is.LessThan(30f));
            Assert.That(response.sharedMaterials.Length, Is.EqualTo(4));
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

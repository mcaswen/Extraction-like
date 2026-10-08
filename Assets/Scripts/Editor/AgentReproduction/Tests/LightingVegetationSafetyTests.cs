using System;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    /// <summary>Read-only formal-scene checks. No saves, asset writes, lighting or navigation bakes.</summary>
    public sealed class LightingVegetationSafetyTests
    {
        private Scene scene;
        private SceneSetup[] setup;
        private AerospaceLightingVegetationProfile profile;
        private bool originalActive;
        private Material temporarySky;
        private Material originalSky;
        private float originalPhase;

        [OneTimeSetUp]
        public void OpenFormalScene()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            profile = scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.LightingPaletteRoot).GetComponent<AerospaceLightingVegetationProfile>();
            originalActive = profile.gameObject.activeSelf;
            originalSky = RenderSettings.skybox; originalPhase = profile.cycle.CurrentPhase;
            temporarySky = new Material(profile.cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        }

        [TearDown]
        public void RestoreAfterEachTest()
        {
            profile.gameObject.SetActive(true);
            profile.cycle.ApplyPhase(originalPhase, temporarySky);
            RenderSettings.skybox = originalSky;
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (profile != null) profile.gameObject.SetActive(originalActive);
            if (temporarySky != null) Object.DestroyImmediate(temporarySky);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void SavedProfileIsConfiguredAndAddsNoGeometryPhysicsOrNavigation()
        {
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.VerifyLightingAndVegetationV1(scene));
            Assert.That(profile.Renderers.Length, Is.EqualTo(54));
            Assert.That(profile.Terrains.Length, Is.EqualTo(9));
            Assert.That(profile.GetComponentsInChildren<Light>(true).Length, Is.EqualTo(2));
            Assert.That(profile.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene, profile.transform));
            foreach (Transform child in profile.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
        }

        [Test]
        public void AllNineTerrainCopiesKeepExactHeightHolesPaintingTreesAndDetailPlacement()
        {
            foreach (var binding in profile.Terrains)
            {
                Assert.That(binding.styled, Is.Not.SameAs(binding.original));
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertPaletteTerrainCopy(binding.original, binding.styled));
                Assert.That(binding.collider.terrainData, Is.SameAs(binding.terrain.terrainData));
                Assert.That(AssetDatabase.GetAssetPath(binding.styled), Does.StartWith(AerospaceSceneLayoutBuilder.LightingPaletteAssets + "/Terrain/"));
            }
        }

        [Test]
        public void SourceResourcesRemainByteForByteUnmodified()
        {
            string report = "UserSettings/ScenePreviews/Aerospace_LightingAndVegetation_V1/source-asset-hashes.txt";
            Assert.That(File.Exists(report), Is.True);
            foreach (string line in File.ReadAllLines(report))
            {
                int separator = line.IndexOf("  ", StringComparison.Ordinal);
                string expected = line.Substring(0, separator), path = line.Substring(separator + 2);
                using (var stream = File.OpenRead(path)) using (var hash = System.Security.Cryptography.SHA256.Create())
                    Assert.That(BitConverter.ToString(hash.ComputeHash(stream)), Is.EqualTo(expected), path);
            }
        }

        [Test]
        public void FoliageCopiesOnlyUseValidShadersAndPreserveAlphaAndTextureReferences()
        {
            foreach (var binding in profile.Renderers)
                for (int index = 0; index < binding.original.Length; index++)
                {
                    Material original = binding.original[index], styled = binding.styled[index];
                    Assert.That(styled.shader, Is.Not.Null);
                    Assert.That(ShaderUtil.ShaderHasError(styled.shader), Is.False);
                    foreach (string texture in new[] { "_BaseMap", "_BumpMap", "_LeafTex", "_TunkTex" })
                        if (original.HasProperty(texture)) Assert.That(styled.GetTexture(texture), Is.SameAs(original.GetTexture(texture)));
                    foreach (string scalar in new[] { "_AlphaClip", "_Cutoff", "_Surface", "_Cull" })
                        if (original.HasProperty(scalar)) Assert.That(styled.GetFloat(scalar), Is.EqualTo(original.GetFloat(scalar)));
                    if (original != styled) Assert.That(AssetDatabase.GetAssetPath(styled), Does.StartWith(AerospaceSceneLayoutBuilder.LightingPaletteAssets));
                }
        }

        [Test]
        public void RootToggleRestoresOriginalMaterialsTerrainCameraAndFog()
        {
            profile.gameObject.SetActive(false);
            foreach (var binding in profile.Renderers)
                Assert.That(AerospaceLightingVegetationProfile.MaterialsEqual(binding.renderer.sharedMaterials, binding.original), Is.True);
            foreach (var binding in profile.Terrains)
            {
                Assert.That(binding.terrain.terrainData, Is.SameAs(binding.original));
                Assert.That(binding.collider.terrainData, Is.SameAs(binding.original));
            }
            foreach (var binding in profile.Cameras) Assert.That(binding.camera.renderPostProcessing, Is.EqualTo(binding.originalPostProcessing));
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(profile.gradingVolume.enabled, Is.False);
            Assert.That(profile.cycle.sunLight.shadows, Is.EqualTo(LightShadows.None));
            profile.gameObject.SetActive(true);
            foreach (var binding in profile.Renderers)
                Assert.That(AerospaceLightingVegetationProfile.MaterialsEqual(binding.renderer.sharedMaterials, binding.styled), Is.True);
            foreach (var binding in profile.Terrains) Assert.That(binding.terrain.terrainData, Is.SameAs(binding.styled));
            Assert.That(profile.Cameras.Single().camera.renderPostProcessing, Is.True);
            Assert.That(profile.gradingVolume.enabled, Is.True);
        }

        [Test]
        public void BothComparisonStatesKeepAllOriginalTransformsTargetsAndGameplayConfiguration()
        {
            string before = AerospaceSceneLayoutBuilder.CaptureLightingPaletteProtectedState(scene);
            foreach (bool active in new[] { false, true, false, true })
            {
                profile.gameObject.SetActive(active);
                Assert.That(AerospaceSceneLayoutBuilder.CaptureLightingPaletteProtectedState(scene), Is.EqualTo(before));
            }
        }

        [Test]
        public void DayAndNightAndBothComparisonStatesKeepBothAgentsExactNavmeshAndPaths()
        {
            var before = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            foreach (bool active in new[] { false, true }) foreach (float phase in new[] { 0f, .25f, .5f })
            {
                profile.gameObject.SetActive(active); profile.cycle.ApplyPhase(phase, temporarySky);
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before, AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene)));
            }
        }

        [Test]
        public void NightHasReadableMoonFillAndOnlySubtleDistanceHaze()
        {
            profile.cycle.ApplyPhase(.5f, temporarySky);
            Assert.That(profile.cycle.sunLight.intensity, Is.GreaterThanOrEqualTo(.6f));
            Assert.That(RenderSettings.ambientEquatorColor.maxColorComponent, Is.GreaterThanOrEqualTo(.35f));
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(150));
            foreach (Light light in profile.GetComponentsInChildren<Light>()) Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
        }

        [Test]
        public void GradingAvoidsVignetteDepthOfFieldAndSharedProfileMutationAcrossPhases()
        {
            VolumeProfile shared = profile.gradingVolume.sharedProfile;
            string before = EditorJsonUtility.ToJson(shared) + string.Join("", shared.components.Select(EditorJsonUtility.ToJson));
            foreach (float phase in new[] { 0f, .25f, .5f, .75f, 0f }) profile.cycle.ApplyPhase(phase, temporarySky);
            string after = EditorJsonUtility.ToJson(shared) + string.Join("", shared.components.Select(EditorJsonUtility.ToJson));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(shared.TryGet(out DepthOfField _), Is.False);
            Assert.That(shared.TryGet(out Vignette _), Is.False);
            Assert.That(shared.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.intensity.value, Is.LessThanOrEqualTo(.2f));
            Assert.That(shared.TryGet(out Tonemapping tone), Is.True);
            Assert.That(tone.mode.value, Is.EqualTo(TonemappingMode.Neutral));
        }

        [Test]
        public void ExistingSkyboxesAnd160SecondTimingRemainAvailable()
        {
            Assert.That(profile.cycle.skyboxVersion1, Is.Not.Null);
            Assert.That(profile.cycle.skyboxVersion2, Is.Not.Null);
            Assert.That(profile.cycle.cycleSeconds, Is.EqualTo(160));
            Assert.That(AerospaceDayNightCycle.PhaseAfter(0, 160, profile.cycle.cycleSeconds), Is.EqualTo(0).Within(.0001f));
        }

        [Test]
        public void OptionalOutlineTuningIsCameraSceneLocalAndDisappearsWhenHidden()
        {
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
            Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.SameAs(profile));
            profile.gameObject.SetActive(false);
            Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.Null);
            profile.gameObject.SetActive(true);
            Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.SameAs(profile));
            Assert.That(profile.sobelIntensityScale, Is.InRange(.4f, .8f));
            Assert.That(profile.sobelNormalThreshold, Is.GreaterThan(.25f));
        }

        [Test]
        public void RevertingDoesNotOverwriteArtistsLaterMaterialReplacement()
        {
            var binding = profile.Renderers.First();
            var current = binding.renderer.sharedMaterials;
            var artistMaterial = new Material(current[0]) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var replacement = (Material[])current.Clone(); replacement[0] = artistMaterial;
                binding.renderer.sharedMaterials = replacement;
                profile.gameObject.SetActive(false);
                Assert.That(binding.renderer.sharedMaterials[0], Is.SameAs(artistMaterial));
            }
            finally
            {
                binding.renderer.sharedMaterials = binding.original;
                profile.gameObject.SetActive(true);
                Object.DestroyImmediate(artistMaterial);
            }
        }

        [Test]
        public void HiddenPreviewCameraNeedsExplicitAssociationAndCannotLeakSceneTuning()
        {
            var go = new GameObject("IsolatedHiddenPaletteCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = go.AddComponent<Camera>();
            try
            {
                Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.Null);
                AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, profile);
                Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.SameAs(profile));
                profile.gameObject.SetActive(false);
                Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.Null);
                profile.gameObject.SetActive(true);
                AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, null);
                Assert.That(AerospaceLightingVegetationProfile.ForCamera(camera), Is.Null);
            }
            finally
            {
                AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, null);
                Object.DestroyImmediate(go);
            }
        }
    }
}

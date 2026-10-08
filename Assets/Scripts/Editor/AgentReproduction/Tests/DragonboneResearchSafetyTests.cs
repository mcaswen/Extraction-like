using System.Linq;
using ExtractionLike.Environment;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    /// <summary>Checks the saved scene without saving changes or baking navigation.</summary>
    public sealed class DragonboneResearchSafetyTests
    {
        private Scene scene;
        private SceneSetup[] setup;
        private GameObject root;
        private bool wasActive;

        [OneTimeSetUp]
        public void OpenFormalScene()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            root = scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.DragonResearchRoot);
            wasActive = root.activeSelf;
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (root != null) root.SetActive(wasActive);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void DetailsHaveNoPhysicsAgentsTargetsOrRealLightsAndAreExcludedFromNavigation()
        {
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(root));
            Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene, root.transform));
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
        }

        [Test]
        public void CompleteFootprintsAvoidPathsLootVisualsAndEnemySpawns()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.VerifyDragonResearch(scene));

        [Test]
        public void OnlyOneSignFourLowTagsAndTwoStaticProbesExist()
        {
            Assert.That(root.transform.Find("01_Entry_Research_Sign"), Is.Not.Null);
            Assert.That(root.transform.Find("02_Low_Sampling_Tags").childCount, Is.EqualTo(4));
            Assert.That(root.transform.Find("03_Fixed_Scanning_Probes_No_AI").childCount, Is.EqualTo(2));
            Assert.That(root.transform.Cast<Transform>().Count(t => t.name.StartsWith("04_Cable_Branch_")), Is.EqualTo(2));
        }

        [Test]
        public void ChineseLabelsUseTheirOwnCompleteStaticAtlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AerospaceSceneLayoutBuilder.DragonResearchFontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.atlasTextures.Length, Is.EqualTo(1));
            foreach (char c in AerospaceSceneLayoutBuilder.DragonResearchCharacters) Assert.That(font.HasCharacter(c), Is.True, c.ToString());
            foreach (var text in root.GetComponentsInChildren<TextMeshPro>(true)) Assert.That(text.font, Is.SameAs(font));
        }

        [Test]
        public void CheckboxRestoresOnlyThreeLocalLightsAndPreservesMovedStation()
        {
            var lighting = root.GetComponent<AerospaceResearchLightingOverride>();
            Assert.That(lighting.Entries.Length, Is.EqualTo(3));
            var station = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "Aerospace_SetDress_V1");
            Vector3 position = station.localPosition, scale = station.localScale;
            Quaternion rotation = station.localRotation;
            root.SetActive(true);
            try
            {
                foreach (var entry in lighting.Entries) Assert.That(entry.light.intensity, Is.EqualTo(entry.surveyIntensity));
                root.SetActive(false);
                foreach (var entry in lighting.Entries) Assert.That(entry.light.intensity, Is.EqualTo(entry.originalIntensity));
                Assert.That(station.localPosition, Is.EqualTo(position));
                Assert.That(station.localScale, Is.EqualTo(scale));
                Assert.That(station.localRotation, Is.EqualTo(rotation));
                root.SetActive(true);
                foreach (var entry in lighting.Entries) Assert.That(entry.light.intensity, Is.EqualTo(entry.surveyIntensity));
            }
            finally { root.SetActive(wasActive); }
        }

        [Test]
        public void ManualLightChangesAreNotOverwrittenWhenDetailsAreHidden()
        {
            var lighting = root.GetComponent<AerospaceResearchLightingOverride>();
            var entry = lighting.Entries[0];
            try
            {
                entry.light.intensity = entry.surveyIntensity + 1;
                root.SetActive(false);
                Assert.That(entry.light.intensity, Is.EqualTo(entry.surveyIntensity + 1));
            }
            finally { root.SetActive(true); root.SetActive(wasActive); }
        }

        [Test]
        public void HidingDetailsAlsoUpdatesSavedStationPrefabLightOverrides()
        {
            var lighting = root.GetComponent<AerospaceResearchLightingOverride>();
            try
            {
                root.SetActive(false);
                foreach (var entry in lighting.Entries)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(entry.light);
                    var modification = PrefabUtility.GetPropertyModifications(entry.light)
                        .FirstOrDefault(m => m.target == source && m.propertyPath == "m_Intensity");
                    float savedIntensity = modification == null ? source.intensity : float.Parse(modification.value, System.Globalization.CultureInfo.InvariantCulture);
                    Assert.That(savedIntensity, Is.EqualTo(entry.originalIntensity));
                }
            }
            finally { root.SetActive(wasActive); }
        }

        [Test]
        public void DeletingLightingControllerRestoresItsPreviousLocalIntensity()
        {
            var lightObject = new GameObject("ResearchTemporaryLight") { hideFlags = HideFlags.HideAndDontSave };
            var controllerObject = new GameObject("ResearchTemporaryController") { hideFlags = HideFlags.HideAndDontSave };
            var light = lightObject.AddComponent<Light>();
            light.intensity = 1800;
            try
            {
                var controller = controllerObject.AddComponent<AerospaceResearchLightingOverride>();
                controller.Configure(new[] { light }, new[] { 90f });
                Assert.That(light.intensity, Is.EqualTo(90));
                Object.DestroyImmediate(controllerObject);
                Assert.That(light.intensity, Is.EqualTo(1800));
            }
            finally
            {
                if (controllerObject != null) Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void DetailsTogglePreservesBothAgentPathsAndExactNavigationGeometry()
        {
            try
            {
                root.SetActive(false);
                var original = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
                root.SetActive(true);
                var updated = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
                Assert.That(updated.Agents, Is.EqualTo(2));
                Assert.That(updated.Probes, Is.EqualTo(1512));
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(original, updated));
            }
            finally { root.SetActive(wasActive); }
        }
    }
}

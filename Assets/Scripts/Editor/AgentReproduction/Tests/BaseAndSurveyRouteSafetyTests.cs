using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    /// <summary>Saved-scene checks only; never saves the formal scene or bakes navigation.</summary>
    public sealed class BaseAndSurveyRouteSafetyTests
    {
        private Scene scene;
        private SceneSetup[] setup;
        private GameObject living, route;
        private bool livingActive, routeActive;

        [OneTimeSetUp]
        public void OpenFormalScene()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            living = scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.BaseLivingRoot);
            route = scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.SurveyRouteRoot);
            livingActive = living.activeSelf; routeActive = route.activeSelf;
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (living != null) living.SetActive(livingActive);
            if (route != null) route.SetActive(routeActive);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void SavedGroupsAreStrictlyVisualOnly()
        {
            foreach (var root in new[] { living, route })
            {
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(root));
                Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
            }
        }

        [Test]
        public void ActualNavigationBuildInputsExcludeBothGroupsForAllAgentTypes()
        {
            foreach (var root in new[] { living, route })
            {
                var modifier = root.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                Assert.That(modifier.AffectsAgentType(0) && modifier.AffectsAgentType(1234), Is.True);
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene, root.transform));
            }
        }

        [Test]
        public void FootprintsAvoidRoutesInteractionSpaceEnemySpawnsTreesAndExistingEquipment()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.VerifyBaseAndRoute(scene));

        [Test]
        public void SparseLayoutHasTwoWallPlaquesTwoUtilityCornersOneRelayAndThreeRouteSigns()
        {
            Assert.That(living.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Wall_Identification_")), Is.EqualTo(2));
            Assert.That(living.GetComponentsInChildren<Transform>(true).Count(t => t.name.EndsWith("Maintenance_Corner")), Is.EqualTo(2));
            Assert.That(living.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Small_Static_Communications_Node"), Is.EqualTo(1));
            Assert.That(route.transform.Find("01_Three_Survey_Direction_Signs").childCount, Is.EqualTo(3));
            Assert.That(living.GetComponentsInChildren<Transform>(true).Concat(route.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "Small_Leveling_Footing"), Is.False);
        }

        [Test]
        public void LabelsHaveTheirOwnCompleteStaticChineseAtlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AerospaceSceneLayoutBuilder.BaseRouteFontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            foreach (char c in AerospaceSceneLayoutBuilder.BaseRouteCharacters) Assert.That(font.HasCharacter(c), Is.True, c.ToString());
            foreach (var text in living.GetComponentsInChildren<TextMeshPro>(true).Concat(route.GetComponentsInChildren<TextMeshPro>(true)))
            {
                Assert.That(text.font, Is.SameAs(font));
                foreach (char c in text.text) Assert.That(font.HasCharacter(c), Is.True, text.text);
                Assert.That(text.raycastTarget, Is.False);
            }
        }

        [Test]
        public void DirectionSignsAreReadableOnBothSidesAndMatchEstablishedSurveySiteIds()
        {
            foreach (Transform sign in route.transform.Find("01_Three_Survey_Direction_Signs"))
            {
                Assert.That(sign.Find("South_Readable_Face"), Is.Not.Null);
                Assert.That(sign.Find("North_Readable_Face"), Is.Not.Null);
                var texts = sign.GetComponentsInChildren<TextMeshPro>(true);
                Assert.That(texts.Count(t => t.text.Contains("SITE-07")), Is.EqualTo(2));
                Assert.That(texts.Count(t => t.text.Contains("DB-01")), Is.EqualTo(2));
                Assert.That(sign.Find("Small_Base").localScale.x, Is.LessThan(1));
            }
        }

        [Test]
        public void GroupsCanBeHiddenIndependentlyWithoutChangingOriginalSceneComponents()
        {
            string before = AerospaceSceneLayoutBuilder.CaptureBaseRouteOriginalSceneState(scene);
            try
            {
                foreach (bool baseEnabled in new[] { false, true }) foreach (bool routeEnabled in new[] { false, true })
                {
                    living.SetActive(baseEnabled); route.SetActive(routeEnabled);
                    Assert.That(AerospaceSceneLayoutBuilder.CaptureBaseRouteOriginalSceneState(scene), Is.EqualTo(before));
                }
            }
            finally { living.SetActive(livingActive); route.SetActive(routeActive); }
        }

        [Test]
        public void AllFourComparisonStatesPreserveBothAgentsPathsAndExactNavigationGeometry()
        {
            var before = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            try
            {
                foreach (bool baseEnabled in new[] { false, true }) foreach (bool routeEnabled in new[] { false, true })
                {
                    living.SetActive(baseEnabled); route.SetActive(routeEnabled);
                    var after = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
                    Assert.That(after.Agents, Is.EqualTo(2));
                    Assert.That(after.Probes, Is.EqualTo(1512));
                    Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before, after));
                }
            }
            finally { living.SetActive(livingActive); route.SetActive(routeActive); }
        }
    }
}

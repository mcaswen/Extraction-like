using System;
using System.Linq;
using Gameplay.Agent.Core;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    /// <summary>Read-only formal scene checks. Never saves the scene, builds navigation or enters gameplay.</summary>
    public sealed class AerospaceInvestigationSafetyTests
    {
        private Scene _scene;
        private SceneSetup[] _setup;
        private GameObject _v1;
        private GameObject _v2;
        private bool _v1Active;
        private bool _v2Active;

        [OneTimeSetUp]
        public void LoadSavedFormalScene()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
            _scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            _v1 = _scene.GetRootGameObjects().Single(r => r.name == "Aerospace_RocketCrash_V1");
            _v2 = _scene.GetRootGameObjects().Single(r => r.name == AerospaceSceneLayoutBuilder.InvestigationRootName);
            _v1Active = _v1.activeSelf;
            _v2Active = _v2.activeSelf;
        }

        [OneTimeTearDown]
        public void RestoreEditorStateWithoutSaving()
        {
            if (_v1 != null) _v1.SetActive(_v1Active);
            if (_v2 != null) _v2.SetActive(_v2Active);
            if (_setup != null && _setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(_setup);
        }

        [Test]
        public void SavedV2HasNoPhysicsNavigationAgentsOrGameplayTargets()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(_v2));

        [Test]
        public void NavigationExclusionAppliesRecursivelyToAllAgentTypes()
        {
            var modifier = _v2.GetComponent<NavMeshModifier>();
            Assert.That(modifier.isActiveAndEnabled && modifier.ignoreFromBuild && modifier.applyToChildren, Is.True);
            Assert.That(modifier.AffectsAgentType(0), Is.True);
            Assert.That(modifier.AffectsAgentType(1234), Is.True);
            Assert.That(_v2.GetComponentsInChildren<NavMeshModifier>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void ActualSurfaceBuildCollectionExcludesAllSurveyMeshes()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(_scene, _v2.transform));

        [Test]
        public void SwitchingWreckVersionsPreservesBothAgentPathResultsAndNavigationGeometry()
        {
            try
            {
                _v2.SetActive(false);
                _v1.SetActive(true);
                var original = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(_scene);
                _v1.SetActive(false);
                _v2.SetActive(true);
                var investigation = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(_scene);
                Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(original, investigation));
                Assert.That(investigation.Agents, Is.EqualTo(2));
                Assert.That(investigation.Probes, Is.GreaterThan(1000));
            }
            finally { _v1.SetActive(_v1Active); _v2.SetActive(_v2Active); }
        }

        [Test]
        public void CompleteRenderedEquipmentFootprintsAvoidPublishedRoutesAndResourceInteractionSpace()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.VerifyInvestigationEquipmentShoulders(_scene));

        [Test]
        public void OriginalWreckRemainsUnweatheredAndOnlyTwoPlayableAgentsExist()
        {
            Assert.That(_v1.activeSelf, Is.False);
            Assert.That(_v2.activeSelf, Is.True);
            var originalMaterials = _v1.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials);
            Assert.That(originalMaterials.Any(m => m != null && m.name == "M_Crash_Hull_OffWhite"), Is.True);
            Assert.That(originalMaterials.Any(m => m != null && m.name.StartsWith("M_Survey_")), Is.False);
            Assert.That(_scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<AgentPawnRoot>(true).Length), Is.EqualTo(2));
            Assert.That(_v2.GetComponentsInChildren<NavMeshAgent>(true), Is.Empty);
            Assert.That(_v2.GetComponentsInChildren<AgentPawnRoot>(true), Is.Empty);
        }

        [Test]
        public void VisualValidatorRejectsAnAccidentallyAddedCollider()
        {
            var root = new GameObject("SurveyInvalidPhysicsTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var modifier = root.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
                modifier.applyToChildren = true;
                root.AddComponent<BoxCollider>();
                AerospaceSceneLayoutBuilder.SetSurveyVisualOnly(root);
                Assert.Throws<InvalidOperationException>(() => AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(root));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(2f, 3f, 3f)]
        [TestCase(-2f, 0f, 2f)]
        [TestCase(12f, 0f, 2f)]
        public void ProtectedCorridorDistanceIncludesSegmentsAndEndpoints(float x, float y, float expected)
            => Assert.That(AerospaceSceneLayoutBuilder.InvestigationSegmentDistance(new Vector2(x, y), Vector2.zero, new Vector2(10, 0)), Is.EqualTo(expected).Within(0.00001f));
    }
}

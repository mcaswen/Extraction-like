using System.Linq;
using ExtractionLike.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AgentReproduction.Tests
{
    /// <summary>Read-only checks: isolated editor workspace only; never saves or bakes navigation.</summary>
    public sealed class WenchangLandingSafetyTests
    {
        Scene scene;
        SceneSetup[] setup;
        AerospaceLandingSite site;

        [OneTimeSetUp]
        public void OpenScene()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity", OpenSceneMode.Single);
            site = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AerospaceLandingSite>(true)).Single();
        }

        [OneTimeTearDown]
        public void RestoreWithoutSaving()
        {
            if (site != null) AerospaceSceneLayoutBuilder.SetLandingVersion(site, true);
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        [Test]
        public void FormalSceneHasCompleteUprightVisualOnlyVehicle()
            => Assert.DoesNotThrow(() => AerospaceSceneLayoutBuilder.ValidateWenchangLanding(scene));

        [Test]
        public void AllThreeLodsRetainChineseIdentityAndFlagWithConsistentBounds()
        {
            Bounds? first = null;
            foreach (var lod in site.vehicleLods.GetLODs())
            {
                Assert.That(lod.renderers.Any(r => r.name.Contains("Ink_Identity")), Is.True);
                Assert.That(lod.renderers.Any(r => r.name.Contains("FlagRed_Identity")), Is.True);
                Assert.That(lod.renderers.Any(r => r.name.Contains("FlagGold_Identity")), Is.True);
                var bounds = lod.renderers[0].bounds;
                foreach (var renderer in lod.renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (first.HasValue)
                {
                    Assert.That(Vector3.Distance(first.Value.center, bounds.center), Is.LessThan(.3f));
                    Assert.That(Vector3.Distance(first.Value.size, bounds.size), Is.LessThan(.5f));
                }
                else first = bounds;
            }
            Assert.That(site.sourceMasterSha256, Is.EqualTo("ee7a98b5609b1d747bdeb42510375d8978d6e00032dbf9f8bbe4618a49e03c61"));
        }

        [Test]
        public void LandingFeetHaveTerrainSupportedContactBerms()
        {
            Assert.That(site.groundContactPoints.Length, Is.EqualTo(4));
            var ground = site.transform.Find("02_Local_Landing_Scorch_And_Foot_Contact");
            for (int i = 0; i < 4; i++)
            {
                var mesh = ground.Find("Landing_Foot_Contact_" + i).GetComponent<MeshFilter>().sharedMesh;
                var contact = site.groundContactPoints[i];
                Assert.That(Mathf.Abs(mesh.vertices.Last().y - contact.y), Is.LessThan(.04f));
                foreach (var point in mesh.vertices.Skip(20).Take(20))
                {
                    var terrain = Terrain.activeTerrains.First(t => point.x >= t.transform.position.x && point.x <= t.transform.position.x + t.terrainData.size.x && point.z >= t.transform.position.z && point.z <= t.transform.position.z + t.terrainData.size.z);
                    float height = terrain.SampleHeight(point) + terrain.transform.position.y;
                    Assert.That(point.y - height, Is.InRange(.02f, .05f));
                }
            }
        }

        [Test]
        public void RestrainedSmokeEmitsWithoutCollisionOrExcessiveOpacity()
        {
            Assert.That(site.smoke.Length, Is.EqualTo(2));
            Assert.That(site.smokeWisps.Length, Is.EqualTo(2));
            foreach (var wisp in site.smokeWisps)
            {
                Assert.That(wisp.gameObject.activeInHierarchy, Is.True);
                Assert.That(wisp.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(12));
                Assert.That(wisp.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(ShaderUtil.ShaderHasError(wisp.sharedMaterial.shader), Is.False);
                Assert.That(wisp.sharedMaterial.renderQueue, Is.EqualTo(3000));
            }
            foreach (var system in site.smoke)
            {
                try
                {
                    Assert.That(system.gameObject.activeInHierarchy, Is.True);
                    Assert.That(system.collision.enabled || system.trigger.enabled, Is.False);
                    Assert.That(system.main.maxParticles, Is.LessThanOrEqualTo(28));
                    Assert.That(system.main.startColor.colorMax.a, Is.LessThanOrEqualTo(.60f));
                    Assert.That(system.GetComponent<ParticleSystemRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    system.Simulate(5.5f, true, true, true);
                    Assert.That(system.particleCount, Is.InRange(1, system.main.maxParticles));
                }
                finally { system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }
        }

        [Test]
        public void OldWreckEffectsAreHiddenButSurveyAndSkylineRemain()
        {
            Assert.That(site.previousVisuals.All(p => p.target != null && !p.target.activeSelf), Is.True);
            var roots = scene.GetRootGameObjects();
            var investigation = roots.Single(r => r.name == AerospaceSceneLayoutBuilder.InvestigationRootName);
            Assert.That(investigation.transform.Find("03_Unmanned_Investigation_Equipment").gameObject.activeInHierarchy, Is.True);
            Assert.That(investigation.transform.Find("01_Preserved_Wreck_With_Weathering/03_Scattered_Rocket_Parts_Across_Map").gameObject.activeInHierarchy, Is.True);
            var atmosphere = roots.Single(r => r.name == AerospaceSceneLayoutBuilder.CrashAtmosphereRoot).GetComponent<AerospaceCrashAtmosphere>();
            Assert.That(atmosphere.RadarPivots.All(t => t.gameObject.activeInHierarchy), Is.True);
            Assert.That(atmosphere.SteamSystems.All(p => !p.gameObject.activeInHierarchy), Is.True);
            Assert.That(atmosphere.SparkSystem.gameObject.activeInHierarchy, Is.False);
        }

        [Test]
        public void CompareAndRestorePreservesBothAgentsAndEveryNavigationProbe()
        {
            var before = AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            Assert.That(before.Agents, Is.EqualTo(2));
            Assert.That(before.Probes, Is.GreaterThan(1000));
            try
            {
                AerospaceSceneLayoutBuilder.SetLandingVersion(site, false);
                foreach (var visual in site.previousVisuals) Assert.That(visual.target.activeSelf, Is.EqualTo(visual.active));
                AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before, AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene));
                AerospaceSceneLayoutBuilder.SetLandingVersion(site, true);
                AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before, AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene));
            }
            finally { AerospaceSceneLayoutBuilder.SetLandingVersion(site, true); }
        }

        [Test]
        public void NightFillKeepsExistingCycleAndDoesNotMutateMaterials()
        {
            var materials = site.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            var originals = materials.Select(EditorJsonUtility.ToJson).ToArray();
            try
            {
                site.ApplyPhase(0); Assert.That(site.inspectionLights.All(l => l.intensity == 0), Is.True);
                site.ApplyPhase(.5f); Assert.That(site.inspectionLights.All(l => l.intensity > 0 && l.shadows == LightShadows.None), Is.True);
                Assert.That(site.cycle.cycleSeconds, Is.EqualTo(160f));
                for (int i = 0; i < materials.Length; i++) Assert.That(EditorJsonUtility.ToJson(materials[i]), Is.EqualTo(originals[i]));
            }
            finally { site.ApplyPhase(site.cycle.startPhase); }
        }
    }
}

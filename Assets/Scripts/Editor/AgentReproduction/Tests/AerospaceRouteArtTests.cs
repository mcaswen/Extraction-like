using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using ExtractionLike.Environment;
using Gameplay.MapGraph.Binding;
using Gameplay.Targets.Authoring;
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
    /// <summary>Read-only saved-scene safety and reversibility. Never rebakes navigation or saves the scene.</summary>
    public sealed class AerospaceRouteArtTests
    {
        Scene scene; SceneSetup[] setup; AerospaceRouteSetDressing art;
        [OneTimeSetUp] public void OpenScene()
        {
            setup=EditorSceneManager.GetSceneManagerSetup();
            scene=EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",OpenSceneMode.Single);
            art=All<AerospaceRouteSetDressing>().Single();
        }
        [OneTimeTearDown] public void Restore()
        {
            AerospaceSceneLayoutBuilder.SetRouteArtAppearance(true);
            if(setup!=null&&setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        T[] All<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();

        [Test] public void AllSavedContainersHaveExactlyOneReplacementAndOriginalEntity()
        {
            Assert.That(art.cases.Length,Is.EqualTo(32));
            CollectionAssert.AreEquivalent(All<LootBoxEntity>(),art.cases.Select(c=>c.container));
            Assert.That(art.cases.Select(c=>c.container).Distinct().Count(),Is.EqualTo(32));
            foreach(var c in art.cases)
            {
                Assert.That(c.replacement.transform.parent,Is.EqualTo(c.container.transform));
                Assert.That(c.container.GetComponents<LootBoxEntity>().Length,Is.EqualTo(1));
                Assert.That(c.container.GetComponentsInChildren<LODGroup>(true).Length,Is.EqualTo(1));
                Assert.That(c.originalRenderers.All(r=>r!=null&&r.sharedMaterials.All(m=>m.shader.name=="Aerospace/Navigation Source Only")),Is.True);
                Assert.That(c.container.GetComponent<Collider>(),Is.Not.Null);
            }
        }
        [Test] public void InstallationFootprintsAndNavigationHashAreStillValid()
            =>Assert.DoesNotThrow(AerospaceSceneLayoutBuilder.ValidateAerospaceRouteNetwork);
        [Test] public void AllPublishedEdgesAreCoveredWithoutReplacingTheirBindings()
        {
            var binding=All<MapGraphBindingAuthoring>().Single();
            Assert.That(binding.IsValid,Is.True);
            Assert.That(binding.MapDefinition.Edges.Count,Is.EqualTo(26));
            Assert.That(art.coveredEdges,Is.EqualTo(26));
            Assert.That(art.placements.Length,Is.GreaterThan(45));
            foreach(var p in art.placements)Assert.That(binding.MapDefinition.Edges.Any(e=>e.EdgeId==p.edgeId),Is.True);
        }
        [Test] public void AddedArtHasNoPhysicsTargetsOrGameplayBehaviour()
        {
            Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(art.gameObject));
            foreach(var c in art.cases)Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(c.replacement));
            Assert.That(All<Collider>().Length,Is.EqualTo(art.originalColliderCount));
            Assert.That(art.GetComponentsInChildren<Light>(true),Is.Empty,"No expensive overlapping realtime lights.");
            Assert.That(art.GetComponentsInChildren<ParticleSystem>(true),Is.Empty,"No particles concealing combat.");
        }
        [Test] public void DecorativeMeshesDoNotEnterActualNavigationBuildSources()
        {
            Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene,art.transform));
            foreach(var c in art.cases)Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene,c.replacement.transform));
        }
        [Test] public void AppearanceTogglePreservesLootPhysicsAndAll1512PathProbes()
        {
            string gameplay=AerospaceSceneLayoutBuilder.RouteGameplaySignature(scene);
            var dressed=AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            try
            {
                AerospaceSceneLayoutBuilder.SetRouteArtAppearance(false);
                foreach(var c in art.cases)
                {
                    Assert.That(c.replacement.activeSelf,Is.False);
                    CollectionAssert.AreEqual(c.originalEnabled,c.originalRenderers.Select(r=>r.enabled));
                    for(int i=0;i<c.originalRenderers.Length;i++)CollectionAssert.AreEqual(c.originalStates[i].materials,c.originalRenderers[i].sharedMaterials);
                }
                Assert.That(AerospaceSceneLayoutBuilder.RouteGameplaySignature(scene),Is.EqualTo(gameplay));
                Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(dressed,AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene)));
            }
            finally {AerospaceSceneLayoutBuilder.SetRouteArtAppearance(true);}
        }
        [Test] public void CollisionBasedNavigationBuildSourcesAreIdenticalWithBothAppearances()
        {
            string before=BuildSources();
            try{AerospaceSceneLayoutBuilder.SetRouteArtAppearance(false);Assert.That(BuildSources(),Is.EqualTo(before));}
            finally{AerospaceSceneLayoutBuilder.SetRouteArtAppearance(true);}
        }
        string BuildSources()
        {
            var method=typeof(NavMeshSurface).GetMethod("CollectSources",BindingFlags.NonPublic|BindingFlags.Instance);
            var signatures=new List<string>();
            foreach(var surface in All<NavMeshSurface>())
            {
                var sources=(List<NavMeshBuildSource>)method.Invoke(surface,null);
                signatures.AddRange(sources.Select(s=>s.shape+"|"+s.area+"|"+s.size.ToString("F5")+"|"+s.transform.ToString("F5")+"|"+s.sourceObject?.GetInstanceID()+"|"+s.component?.GetInstanceID()));
            }
            return string.Join("\n",signatures.OrderBy(s=>s,StringComparer.Ordinal));
        }
        [TestCase("RecoveryCase",12000)]
        [TestCase("SolarRelay",8500)]
        [TestCase("EngineCradle",23000)]
        [TestCase("SurveyBeacon",2500)]
        public void ModelsHaveThreeValidGeometricLodsAndSharedMaterials(string name,int budget)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AerospaceSceneLayoutBuilder.RouteArtAssets+"/Prefabs/"+name+".prefab");
            var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.That(lods.Length,Is.EqualTo(3));int previous=int.MaxValue;
            foreach(var lod in lods)
            {
                int triangles=lod.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                Assert.That(triangles,Is.LessThan(previous));Assert.That(triangles,Is.LessThan(budget));previous=triangles;
                Assert.That(lod.renderers.Length,Is.LessThanOrEqualTo(name=="RecoveryCase"?14:6));
                foreach(var r in lod.renderers)foreach(var m in r.sharedMaterials)
                {Assert.That(m,Is.Not.Null);Assert.That(ShaderUtil.ShaderHasError(m.shader),Is.False);Assert.That(m.enableInstancing,Is.True);}
            }
        }
        [Test] public void RealContainerSessionsStillResolveTheirSourceAndGrid()
        {
            foreach(var c in art.cases)
            {
                Assert.That(c.container.GetContainerName(),Is.Not.Empty);
                Assert.That(c.container.ContainerColumns,Is.GreaterThan(0));
                Assert.That(c.container.ContainerRows,Is.GreaterThan(0));
                Assert.That(c.container.SearchRequiredSeconds,Is.GreaterThan(0));
                Assert.That(c.container.UseBoardGameResourceRules,Is.True);
            }
            var members=All<ResourceClusterAuthoring>().SelectMany(c=>c.ResourceMembers).Select(m=>m.EntityObject).ToArray();
            foreach(var c in art.cases)Assert.That(members.Contains(c.container.gameObject),Is.True);
        }
    }
}

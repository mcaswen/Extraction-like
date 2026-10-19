using System;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using System.Collections.Generic;

namespace AgentReproduction.Tests
{
    /// <summary>Scenery and navigation regression tests; temporary bakes never save or replace source assets.</summary>
    public sealed class ForestRecoveryStationTests
    {
        Scene scene; SceneSetup[] setup; AerospaceForestStation station;
        T[] All<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        [OneTimeSetUp] public void Load()
        {
            setup=EditorSceneManager.GetSceneManagerSetup();
            scene=EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",OpenSceneMode.Single);
            station=All<AerospaceForestStation>().Single();station.ApplyDisplay();
        }
        [TearDown] public void ResetDisplay()
        {
            if(station==null)return;
            All<AerospaceLightingVegetationProfile>().Single().gameObject.SetActive(true);
            station.gameObject.SetActive(true);station.ApplyDisplay();station.ApplyVisuals(0,0);
        }
        [OneTimeTearDown] public void Restore()
        {if(setup!=null&&setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);}

        [Test] public void FieldApronShaderCompilesAndUsesFeatherableUvCoordinates()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/ForestRecoveryStation/Materials/M_Forest_Ground.mat");
            Assert.That(material.shader.name,Is.EqualTo("Aerospace/Field Apron"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader),Is.False);
            foreach(string name in new[]{"Engine_Work_Apron","Telemetry_Work_Apron"})
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Environment/ForestRecoveryStation/Meshes/"+name+".asset");
                Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.All(uv=>uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1),Is.True);
            }
        }
        [Test] public void MachineryAmbienceStartsSilentAndStopsWhenStationIsDisabled()
        {
            var audio=station.machineryAmbience;
            Assert.That(audio,Is.Not.Null);Assert.That(audio.clip,Is.Not.Null);
            Assert.That(audio.playOnAwake,Is.False);Assert.That(audio.loop,Is.True);
            Assert.That(audio.volume,Is.Zero);Assert.That(audio.clip.length,Is.EqualTo(4).Within(.01f));
            audio.volume=.1f;station.gameObject.SetActive(false);
            Assert.That(audio.volume,Is.Zero);Assert.That(audio.isPlaying,Is.False);
        }

        [Test] public void DecorativeRootHasNoPhysicsGameplayOrNavigationSources()
        {
            Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.ValidateInvestigationVisualRoot(station.gameObject));
            Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.AssertNoInvestigationNavigationSources(scene,station.transform));
            Assert.That(station.GetComponentsInChildren<LootBoxEntity>(true),Is.Empty);
            Assert.That(station.GetComponentsInChildren<Canvas>(true),Is.Empty);
            Assert.That(station.GetComponentsInChildren<ParticleSystem>(true),Is.Empty);
            Assert.That(All<Collider>().Length,Is.EqualTo(297));
            Assert.That(All<LootBoxEntity>().Length,Is.EqualTo(32));
        }
        [Test] public void TogglePreservesEveryOriginalNavigationProbeAndBuildSource()
        {
            string gameplay=AerospaceSceneLayoutBuilder.RouteGameplaySignature(scene),sources=AerospaceSceneLayoutBuilder.ForestBuildSources(scene);
            var before=AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene);
            foreach(bool enabled in new[]{false,true,false,true})
            {
                station.gameObject.SetActive(enabled);
                Assert.That(AerospaceSceneLayoutBuilder.RouteGameplaySignature(scene),Is.EqualTo(gameplay));
                Assert.That(AerospaceSceneLayoutBuilder.ForestBuildSources(scene),Is.EqualTo(sources));
                Assert.DoesNotThrow(()=>AerospaceSceneLayoutBuilder.AssertInvestigationSnapshotEqual(before,AerospaceSceneLayoutBuilder.CaptureInvestigationNavigation(scene)));
            }
            Assert.That(before.Agents,Is.EqualTo(2));Assert.That(before.Probes,Is.EqualTo(1512));Assert.That(before.Complete,Is.EqualTo(1308));
        }
        [Test] public void GroundCoverOnlyRemovesExplicitLocalNonCollidingGrass()
        {
            Assert.That(station.terrainDisplays.Length,Is.EqualTo(1));
            foreach(var b in station.terrainDisplays)
            {
                var source=b.sourceStyled.treeInstances;var kept=b.displayStyled.treeInstances;
                Assert.That(b.removedInstanceIndices,Is.Not.Empty);
                Assert.That(source.Length-kept.Length,Is.EqualTo(b.removedInstanceIndices.Length));
                foreach(int i in b.removedInstanceIndices)
                {
                    var prefab=b.sourceStyled.treePrototypes[source[i].prototypeIndex].prefab;
                    Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
                    Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).All(f=>f.name.Contains("Grass")),Is.True);
                    var p=b.terrain.transform.position+Vector3.Scale(source[i].position,b.sourceStyled.size);
                    Assert.That(p.x,Is.InRange(3,64));Assert.That(p.z,Is.InRange(-51,57));
                }
                var indices=b.removedInstanceIndices.ToHashSet();
                var expected=source.Where((t,i)=>!indices.Contains(i)).ToArray();
                // Compare every authored field exactly, excluding Unity's internal temporaryDistance
                // cache, which changes when the terrain is rendered from a different camera.
                CollectionAssert.AreEqual(
                    expected.Select(t=>(t.position,t.widthScale,t.heightScale,t.rotation,t.color,t.lightmapColor,t.prototypeIndex)).ToArray(),
                    kept.Select(t=>(t.position,t.widthScale,t.heightScale,t.rotation,t.color,t.lightmapColor,t.prototypeIndex)).ToArray());
            }
        }
        [Test] public void EquipmentDoesNotSitOnWalkableGroundEvenWithoutColliders()
            =>Assert.DoesNotThrow(AerospaceSceneLayoutBuilder.ValidateForestWalkableFootprints);

        [Test] public void FreshLocalNavigationBakeIsGeometricallyIdenticalWithAndWithoutStation()
        {
            var surfaces=All<NavMeshSurface>();
            var collect=typeof(NavMeshSurface).GetMethod("CollectSources",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            string Bake(bool decorated)
            {
                station.gameObject.SetActive(decorated);
                var signatures=new List<string>();
                foreach(var surface in surfaces)
                {
                    var sources=(List<NavMeshBuildSource>)collect.Invoke(surface,null);
                    var data=NavMeshBuilder.BuildNavMeshData(surface.GetBuildSettings(),sources,new Bounds(new Vector3(30,15,0),new Vector3(120,90,170)),Vector3.zero,Quaternion.identity);
                    Assert.That(data,Is.Not.Null);
                    var instance=NavMesh.AddNavMeshData(data);
                    try
                    {
                        var mesh=NavMesh.CalculateTriangulation();var triangles=new List<string>();
                        Assert.That(mesh.indices,Is.Not.Empty,"The local bake must contain real navigable geometry.");
                        Assert.That(mesh.indices.Length%3,Is.Zero,"Navigation geometry must contain complete triangles.");
                        for(int i=0;i<mesh.indices.Length;i+=3)
                        {
                            var corners=new[]{mesh.vertices[mesh.indices[i]].ToString("F3"),mesh.vertices[mesh.indices[i+1]].ToString("F3"),mesh.vertices[mesh.indices[i+2]].ToString("F3")};
                            Array.Sort(corners,StringComparer.Ordinal);triangles.Add(mesh.areas[i/3]+"|"+string.Join("|",corners));
                        }
                        triangles.Sort(StringComparer.Ordinal);
                        using(var hash=System.Security.Cryptography.SHA256.Create())signatures.Add(BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",triangles)))));
                        Debug.Log("FOREST_FRESH_LOCAL_BAKE decorated="+decorated+" triangles="+triangles.Count);
                    }
                    finally{instance.Remove();UnityEngine.Object.DestroyImmediate(data);}
                }
                return string.Join(";",signatures);
            }
            foreach(var surface in surfaces)surface.RemoveData();
            try{Assert.That(Bake(true),Is.EqualTo(Bake(false)));}
            finally{station.gameObject.SetActive(true);foreach(var surface in surfaces)surface.AddData();}
        }
        [Test] public void GroundHeightHolesPaintingDetailsAndCollisionRemainOriginal()
        {
            foreach(var b in station.terrainDisplays)
            {
                foreach(var pair in new[]{new[]{b.sourceOriginal,b.displayOriginal},new[]{b.sourceStyled,b.displayStyled}})
                {
                    var a=pair[0];var c=pair[1];Assert.That(c.size,Is.EqualTo(a.size));
                    CollectionAssert.AreEqual(a.GetHeights(0,0,a.heightmapResolution,a.heightmapResolution),c.GetHeights(0,0,c.heightmapResolution,c.heightmapResolution));
                    CollectionAssert.AreEqual(a.GetHoles(0,0,a.holesResolution,a.holesResolution),c.GetHoles(0,0,c.holesResolution,c.holesResolution));
                    CollectionAssert.AreEqual(a.GetAlphamaps(0,0,a.alphamapWidth,a.alphamapHeight),c.GetAlphamaps(0,0,c.alphamapWidth,c.alphamapHeight));
                    for(int i=0;i<a.detailPrototypes.Length;i++)CollectionAssert.AreEqual(a.GetDetailLayer(0,0,a.detailWidth,a.detailHeight,i),c.GetDetailLayer(0,0,c.detailWidth,c.detailHeight,i));
                }
                Assert.That(b.terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(b.sourceStyled));
            }
        }
        [Test] public void ExistingPaletteAndNewSceneryToggleIndependently()
        {
            var palette=All<AerospaceLightingVegetationProfile>().Single();
            foreach(bool decorated in new[]{true,false,true})foreach(bool styled in new[]{false,true})
            {
                station.gameObject.SetActive(decorated);palette.gameObject.SetActive(styled);
                foreach(var b in station.terrainDisplays)
                {
                    Assert.That(b.terrain.terrainData,Is.SameAs(decorated?(styled?b.displayStyled:b.displayOriginal):(styled?b.sourceStyled:b.sourceOriginal)));
                    Assert.That(b.terrain.GetComponent<TerrainCollider>().terrainData,Is.SameAs(styled?b.sourceStyled:b.sourceOriginal));
                }
            }
        }
        [Test] public void OriginalFoliageAndReplacedEquipmentCanBeRestored()
        {
            Assert.That(station.hiddenFoliage.Length,Is.GreaterThan(0));
            foreach(var b in station.hiddenFoliage)
            {Assert.That(b.renderer.enabled,Is.EqualTo(b.originalEnabled));CollectionAssert.AreEqual(b.hiddenMaterials,b.renderer.sharedMaterials);}
            station.gameObject.SetActive(false);
            foreach(var b in station.hiddenFoliage)
            {Assert.That(b.renderer.enabled,Is.EqualTo(b.originalEnabled));CollectionAssert.AreEqual(b.originalMaterials,b.renderer.sharedMaterials);}
        }
        [Test] public void MotionIsBoundedAndDisablingRestoresExactTransform()
        {
            Assert.That(station.motions.Length,Is.EqualTo(6));
            foreach(float time in new[]{0,7,14,29,120,10000})
            {
                station.ApplyVisuals(time,.5f);
                foreach(var m in station.motions)
                {
                    Assert.That(Vector3.Distance(m.target.localPosition,m.originPosition),Is.LessThanOrEqualTo(Mathf.Abs(m.distance)+.001f));
                    Assert.That(Quaternion.Angle(m.target.localRotation,m.originRotation),Is.LessThanOrEqualTo(Mathf.Abs(m.degrees)+.02f));
                }
            }
            station.gameObject.SetActive(false);
            foreach(var m in station.motions){Assert.That(m.target.localPosition,Is.EqualTo(m.originPosition));Assert.That(m.target.localRotation,Is.EqualTo(m.originRotation));}
        }
        [Test] public void LightingIsLocalShadowlessAndUsesExisting160SecondCycle()
        {
            Assert.That(station.workLights.Length,Is.EqualTo(3));Assert.That(station.cycle.cycleSeconds,Is.EqualTo(160));
            station.ApplyVisuals(0,.5f);
            foreach(var light in station.workLights)
            {
                Assert.That(light.type,Is.EqualTo(LightType.Spot));Assert.That(light.shadows,Is.EqualTo(LightShadows.None));
                Assert.That(light.range,Is.LessThanOrEqualTo(18));Assert.That(light.intensity,Is.InRange(.5f,2f));
            }
            station.ApplyVisuals(0,0);foreach(var light in station.workLights)Assert.That(light.intensity,Is.LessThan(.1f));
        }
        [TestCase("EngineInspectionRig",60000)]
        [TestCase("FieldPowerModule",12000)]
        [TestCase("RecoveryMarker",6000)]
        [TestCase("TrackingTerminal",18000)]
        public void ArtHasReducedLodsSharedValidMaterialsAndNoMissingAssets(string name,int budget)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AerospaceSceneLayoutBuilder.ForestAssets+"/Prefabs/"+name+".prefab");
            Assert.That(prefab,Is.Not.Null);Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
            var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.That(lods.Length,Is.EqualTo(3));int previous=budget;
            foreach(var lod in lods)
            {
                int triangles=lod.renderers.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                Assert.That(triangles,Is.GreaterThan(500).And.LessThan(previous));previous=triangles;
                Assert.That(lod.renderers.Length,Is.LessThanOrEqualTo(12));
                foreach(var r in lod.renderers)foreach(var m in r.sharedMaterials)
                {Assert.That(m,Is.Not.Null);Assert.That(m.enableInstancing,Is.True);Assert.That(ShaderUtil.ShaderHasError(m.shader),Is.False);}
            }
        }
    }
}

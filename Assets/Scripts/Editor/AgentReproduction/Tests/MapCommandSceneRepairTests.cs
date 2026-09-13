using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Binding;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    /// <summary>显式运行的场景维护，产物只写 Runner 拥有的副本，普通回归不调用。</summary>
    public sealed class MapCommandSceneRepairTests
    {
        private const string ScenePath="Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        private const string NavPath="Assets/Scenes/Scene_DB/Scenezl_Final 1/NavMesh-CommandRoutes.asset";
        [UnityTest] public IEnumerator SeparateOverlappingLaboratorySentinelSpawn()
        {
            Assert.That(Application.companyName,Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(Application.dataPath.Replace('\\','/'),Does.Contain("/.agent-repro/"));
            Assert.That(Application.isPlaying,Is.False);
            var scene=EditorSceneManager.OpenScene(ScenePath);yield return null;
            var group=GameObject.Find("Zone-实验室/Env/实验室/EnemySourceCluster");
            Assert.That(group,Is.Not.Null);
            var spawn=group.GetComponentsInChildren<EnemySpawnPoint>().Single(x=>Vector3.Distance(x.transform.position,new Vector3(-388.91922f,2.22719f,158.9296f))<.01f).gameObject;
            Assert.That(spawn,Is.Not.Null);
            var before=spawn.transform.position;
            Assert.That(before.x,Is.EqualTo(-388.91922f).Within(.01f),"维护仅针对已确认的原出生点，避免重复偏移");
            var after=before+Vector3.left*4;
            Assert.That(NavMesh.SamplePosition(before,out var from,3,NavMesh.AllAreas),Is.True);
            Assert.That(NavMesh.SamplePosition(after,out var to,3,NavMesh.AllAreas),Is.True);
            var path=new NavMeshPath();Assert.That(NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path),Is.True);
            Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            var source=spawn.GetComponent<EnemySpawnPoint>();var serialized=EditorJsonUtility.ToJson(source);
            spawn.transform.position=after;PrefabUtility.RecordPrefabInstancePropertyModifications(spawn.transform);
            Assert.That(EditorJsonUtility.ToJson(source),Is.EqualTo(serialized));
            EditorSceneManager.MarkSceneDirty(scene);Assert.That(EditorSceneManager.SaveScene(scene),Is.True);
            CaseArtifactWriter.Trace("spawn.separated","before="+before+"; after="+after+"; local="+spawn.transform.localPosition);
        }
        [UnityTest] public IEnumerator GroundDragonboneMarkerAndRebakeOwnedNavigationAndMap()
        {
            Assert.That(Application.companyName,Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(Application.dataPath.Replace('\\','/'),Does.Contain("/.agent-repro/"));
            Assert.That(Application.isPlaying,Is.False);
            CaseArtifactWriter.Trace("setup","隔离副本维护，原导航资产保留。");
            var scene=EditorSceneManager.OpenScene(ScenePath);yield return null;
            var root=GameObject.Find("Zone-龙骨礁/ExtractionCluster");Assert.That(root,Is.Not.Null);
            var floor=GameObject.Find("Zone-龙骨礁/tripo_convert_cfc50981-c500-4787-94ae-f41149c942c9/tripo_part_6");
            Assert.That(floor,Is.Not.Null);
            var mesh=floor.GetComponent<MeshFilter>();Assert.That(mesh,Is.Not.Null);
            var before=root.transform.position;
            float ground=FindSurfaceHeight(mesh,before);Assert.That(ground,Is.InRange(6f,8f));
            float halfHeight=root.GetComponent<Renderer>().bounds.extents.y;
            root.transform.position=new Vector3(before.x,ground+halfHeight,before.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            Physics.SyncTransforms();
            var surface=Object.FindObjectOfType<NavMeshSurface>();Assert.That(surface,Is.Not.Null);
            Assert.That(surface.useGeometry,Is.EqualTo(NavMeshCollectGeometry.RenderMeshes));
            var original=surface.navMeshData;
            surface.BuildNavMesh();var generated=surface.navMeshData;
            Assert.That(generated,Is.Not.Null);Assert.That(generated,Is.Not.SameAs(original));
            var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if(existing==null)AssetDatabase.CreateAsset(generated,NavPath);
            else
            {
                surface.RemoveData();EditorUtility.CopySerialized(generated,existing);
                surface.navMeshData=existing;surface.AddData();Object.DestroyImmediate(generated);
            }
            EditorUtility.SetDirty(surface.navMeshData);EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();Assert.That(EditorSceneManager.SaveScene(scene),Is.True);
            // 新烘焙的原生三角形顺序和反序列化顺序可能不同，以玩家将加载的保存态生成缓存。
            scene=EditorSceneManager.OpenScene(ScenePath);yield return null;
            root=GameObject.Find("Zone-龙骨礁/ExtractionCluster");
            var binding=Object.FindObjectOfType<MapGraphBindingAuthoring>();Assert.That(binding,Is.Not.Null);
            using(var document=new MapGraphEditorDocument(binding.MapDefinition))
            {
                var request=document.BeginGeneration(scene);
                double deadline=EditorApplication.timeSinceStartup+180;
                while(request.IsRunning&&EditorApplication.timeSinceStartup<deadline){request.Advance(64,6);yield return null;}
                Assert.That(document.TryApplyGeneration(request,out string failure),Is.True,string.Join(";",request.Diagnostics)+failure);
                Assert.That(MapGraphAuthoringTransaction.TrySave(document,scene,null,out binding,out failure),Is.True,failure);
            }
            var output=Path.Combine(TestRunContext.Load().outputPath,"scene-repair");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"repair.json"),JsonUtility.ToJson(new Repair{before=before,after=root.transform.position,ground=ground,
                navigation=NavPath,edges=binding.MapDefinition.Edges.Count,runtimeFingerprint=binding.MapDefinition.NavigationBake.RuntimeNavigationFingerprint},true));
            CaseArtifactWriter.Trace("repaired","保留原导航资产，实际地面贴合后重烘焙并更新图。");
        }
        [UnityTest] public IEnumerator RefreshGraphBakeFromSavedScene()
        {
            Assert.That(Application.companyName,Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(Application.dataPath.Replace('\\','/'),Does.Contain("/.agent-repro/"));
            var scene=EditorSceneManager.OpenScene(ScenePath);yield return null;
            var binding=Object.FindObjectOfType<MapGraphBindingAuthoring>();
            using var document=new MapGraphEditorDocument(binding.MapDefinition);
            var request=document.BeginGeneration(scene,MapGraphGenerationMode.ValidateOnly);
            double deadline=EditorApplication.timeSinceStartup+180;
            while(request.IsRunning&&EditorApplication.timeSinceStartup<deadline){request.Advance(64,6);yield return null;}
            Assert.That(document.TryApplyGeneration(request,out string failure),Is.True,string.Join(";",request.Diagnostics)+failure);
            Assert.That(MapGraphAuthoringTransaction.TrySave(document,scene,null,out binding,out failure),Is.True,failure);
            CaseArtifactWriter.Trace("cache.saved",binding.MapDefinition.NavigationBake.RuntimeNavigationFingerprint);
        }
        [UnityTest] public IEnumerator RefreshGraphBakeFromSavedBindingsWithoutChangingLayout()
        {
            Assert.That(Application.companyName,Is.EqualTo("AnomalySearch.Automation"));
            Assert.That(Application.dataPath.Replace('\\','/'),Does.Contain("/.agent-repro/"));
            Assert.That(Application.isPlaying,Is.False);
            var scene=EditorSceneManager.OpenScene(ScenePath);yield return null;
            byte[] sceneBefore=File.ReadAllBytes(ScenePath);
            var binding=Object.FindObjectOfType<MapGraphBindingAuthoring>();Assert.That(binding.IsValid,Is.True);
            var snapshot=MapGraphSceneCollector.Capture(scene);Assert.That(snapshot.IsValid,Is.True,string.Join(";",snapshot.Diagnostics));
            Assert.That(snapshot.Profiles.Count,Is.EqualTo(1),"本维护用例针对当前单一实际导航 profile");
            var definition=binding.MapDefinition;
            var costs=new MapGraphNavigationCostService(definition,binding,snapshot.Profiles[0].QueryProfile,"","","");
            while(costs.PendingEdgeCount>0){costs.ProcessPending(2);yield return null;}
            Assert.That(costs.Snapshot.Count,Is.EqualTo(definition.Edges.Count),"所有保存连接必须双向完整");
            var bake=costs.CreateBakeData(snapshot.SceneFingerprint,snapshot.NavigationFingerprint,snapshot.RuntimeNavigationFingerprint);
            // 显式隔离维护只补算已有绑定的导航成本，不执行会重新选择锚点的布局生成。
            definition.ApplyCommandData(definition.MapId,definition.DisplayName,definition.StartNodeId,definition.Zones,definition.Nodes,
                definition.Edges,definition.LayoutConstraints,bake,definition.GenerationSettings);
            EditorUtility.SetDirty(definition);AssetDatabase.SaveAssetIfDirty(definition);
            CollectionAssert.AreEqual(sceneBefore,File.ReadAllBytes(ScenePath),"缓存维护不保存场景或更换群导航锚点");
            CaseArtifactWriter.Trace("cache.saved-bindings",definition.NavigationBake.RuntimeNavigationFingerprint+"; queries="+costs.CalculationCount);
        }
        // XZ 重心插值只读取指定可见地板的三角形，不能用无碰撞 FBX 下方的 Terrain 代替。
        private static float FindSurfaceHeight(MeshFilter filter,Vector3 location)
        {
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;
            float best=float.NegativeInfinity;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=filter.transform.TransformPoint(vertices[indices[i]]);
                var b=filter.transform.TransformPoint(vertices[indices[i+1]]);
                var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                if(Mathf.Abs(denominator)<.00001f)continue;
                float u=((b.z-c.z)*(location.x-c.x)+(c.x-b.x)*(location.z-c.z))/denominator;
                float v=((c.z-a.z)*(location.x-c.x)+(a.x-c.x)*(location.z-c.z))/denominator;
                float w=1-u-v;
                if(u<0||v<0||w<0)continue;
                float height=u*a.y+v*b.y+w*c.y;
                if(height<location.y&&height>best)best=height;
            }
            Assert.That(float.IsNegativeInfinity(best),Is.False,"正式可见地面没有下方交点");return best;
        }
        [Serializable]private sealed class Repair{public Vector3 before,after;public float ground;public string navigation,runtimeFingerprint;public int edges;}
        [TearDown]public void Cleanup(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);NavMesh.RemoveAllNavMeshData();CaseArtifactWriter.Complete("COMPLETED");}
    }
}

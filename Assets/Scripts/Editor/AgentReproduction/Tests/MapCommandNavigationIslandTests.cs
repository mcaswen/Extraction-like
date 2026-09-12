using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandNavigationIslandTests
    {
        private const string ScenePath="Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        [Serializable] private sealed class PathRecord {public string from,to,status;public Vector3 origin,anchor,sampledOrigin,sampledAnchor;public Vector3[] corners;}
        [Serializable] private sealed class Triangle {public Vector3 a,b,c;public int area;}
        [Serializable] private sealed class SurfaceRecord {public string path,prefab,component,mesh;public Vector3 position,min,max;public bool trigger;}
        [Serializable] private sealed class HitRecord {public string path;public Vector3 point,normal;}
        [Serializable] private sealed class Evidence
        {
            public string scene,sceneFingerprint,navFingerprint;
            public Vector3 anchor;
            public PathRecord[] paths;
            public Triangle[] triangles;
            public SurfaceRecord[] colliders,renderers;
            public HitRecord[] groundHits;
        }
        private SceneView _view;
        private Action<SceneView> _draw;
        [UnityTest] public IEnumerator BothAuthoredExtractionAnchorsAreReachableFromTheInitialAgents()
        {
            var run=TestRunContext.Load();Assert.That(run.graphics,Is.True);
            Assert.That(Application.isPlaying,Is.False);Assert.That(Application.companyName,Is.EqualTo("AnomalySearch.Automation"));
            CaseArtifactWriter.Trace("setup","真实场景导航岛，保存物理/导航/画面证据后再断言可达性。");
            var scene=EditorSceneManager.OpenScene(ScenePath);yield return null;Physics.SyncTransforms();
            var snapshot=MapGraphSceneCollector.Capture(scene);Assert.That(snapshot.IsValid,Is.True);
            var binding=Object.FindObjectOfType<MapGraphBindingAuthoring>();
            var profile=snapshot.Profiles[0].QueryProfile;
            Assert.That(binding.MapDefinition.NavigationBake.RuntimeNavigationFingerprint,Is.EqualTo(snapshot.RuntimeNavigationFingerprint));
            var cache=new MapGraphNavigationCostService(binding.MapDefinition,binding,profile,"","",snapshot.RuntimeNavigationFingerprint);
            Assert.That(cache.PendingEdgeCount,Is.Zero,"正式图的导航缓存应直接复用");
            Assert.That(cache.CalculationCount,Is.Zero);
            Assert.That(cache.Snapshot.Count,Is.EqualTo(binding.MapDefinition.Edges.Count));
            var filter=new NavMeshQueryFilter{agentTypeID=profile.AgentTypeId,areaMask=profile.AreaMask};
            for(int i=0;i<32;i++)filter.SetAreaCost(i,profile.GetAreaCost(i));
            var paths=new List<PathRecord>();
            foreach(var node in snapshot.Nodes.Where(x=>x.Kind==MapGraphNodeKind.Extraction))
            {
                var entry=binding.TargetBindings.First(x=>x.NodeId==node.Id);Assert.That(entry.TryGetNavigationAnchor(out var target),Is.True);
                for(int i=0;i<snapshot.Profiles[0].AgentOrigins.Count;i++)
                    paths.Add(Query(snapshot.Profiles[0].AgentSourceIds[i],node.Id,snapshot.Profiles[0].AgentOrigins[i],target,profile,filter));
            }
            var islandEntry=binding.TargetBindings.First(x=>x.NodeId=="cluster_a84fd1c0b6b5590d95cbf011798b2e2a");
            Assert.That(islandEntry.TryGetNavigationAnchor(out var center),Is.True);
            var nearby=binding.TargetBindings.Where(x=>x.NodeId!=islandEntry.NodeId&&x.TryGetNavigationAnchor(out _))
                .GroupBy(x=>x.NodeId).Select(x=>x.First()).OrderBy(x=>{x.TryGetNavigationAnchor(out var p);return (p-center).sqrMagnitude;}).Take(4);
            foreach(var entry in nearby){entry.TryGetNavigationAnchor(out var origin);paths.Add(Query(entry.NodeId,islandEntry.NodeId,origin,center,profile,filter));}
            var nav=NavMesh.CalculateTriangulation();var triangles=new List<Triangle>();
            for(int i=0;i<nav.indices.Length;i+=3)
            {
                var a=nav.vertices[nav.indices[i]];var b=nav.vertices[nav.indices[i+1]];var c=nav.vertices[nav.indices[i+2]];
                if(((a+b+c)/3-center).sqrMagnitude<35*35)triangles.Add(new Triangle{a=a,b=b,c=c,area=nav.areas[i/3]});
            }
            var colliders=Physics.OverlapSphere(center,30,Physics.AllLayers,QueryTriggerInteraction.Collide).Select(x=>Describe(x,x.bounds,x.isTrigger)).ToArray();
            var renderers=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Renderer>(true))
                .Where(x=>x.enabled&&!(x is LineRenderer)&&x.bounds.SqrDistance(center)<30*30).Select(x=>Describe(x,x.bounds,false)).ToArray();
            var evidence=new Evidence{scene=ScenePath,sceneFingerprint=snapshot.SceneFingerprint,navFingerprint=snapshot.NavigationFingerprint,
                anchor=center,paths=paths.ToArray(),triangles=triangles.ToArray(),colliders=colliders,renderers=renderers,
                groundHits=Physics.RaycastAll(center+Vector3.up*40,Vector3.down,100,Physics.AllLayers,QueryTriggerInteraction.Ignore)
                    .OrderBy(x=>x.distance).Select(x=>new HitRecord{path=Hierarchy(x.collider.transform),point=x.point,normal=x.normal}).ToArray()};
            string output=Path.Combine(run.outputPath,"navigation-island");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"geometry.json"),JsonUtility.ToJson(evidence,true));
            _view=ScriptableObject.CreateInstance<SceneView>();_view.position=new Rect(80,80,1280,800);_view.ShowUtility();_view.Focus();
            _view.drawGizmos=false;_view.sceneLighting=true;
            _draw=view=>
            {
                if(view!=_view)return;
                Handles.color=new Color(.1f,.9f,1,.8f);
                foreach(var t in triangles){Handles.DrawLine(t.a,t.b);Handles.DrawLine(t.b,t.c);Handles.DrawLine(t.c,t.a);}
                Handles.color=Color.yellow;Handles.DrawWireDisc(center,Vector3.up,1);Handles.Label(center+Vector3.up*2,"AUTHORED EXIT ANCHOR");
            };
            SceneView.duringSceneGui+=_draw;
            _view.orthographic=true;
            _view.LookAtDirect(center,Quaternion.Euler(40,-40,0),28);
            for(int i=0;i<40;i++){_view.Repaint();yield return null;}
            UnityEditorViewCapture.Capture(_view,Path.Combine(output,"01-oblique.png"));
            _view.LookAtDirect(center,Quaternion.Euler(90,0,0),35);
            for(int i=0;i<20;i++){_view.Repaint();yield return null;}
            UnityEditorViewCapture.Capture(_view,Path.Combine(output,"02-top.png"));
            Assert.That(paths.Take(4).All(x=>x.status=="PathComplete"),Is.True,
                string.Join("; ",paths.Take(4).Select(x=>x.from+" → "+x.to+": "+x.status)));
        }
        private static PathRecord Query(string from,string to,Vector3 origin,Vector3 anchor,AgentNavigationProfile profile,NavMeshQueryFilter filter)
        {
            var row=new PathRecord{from=from,to=to,origin=origin,anchor=anchor,corners=Array.Empty<Vector3>()};
            if(!AgentNavigationSegmentQuery.TrySampleAnchor(profile,origin,out row.sampledOrigin,out string failure)){row.status="Origin:"+failure;return row;}
            if(!AgentNavigationSegmentQuery.TrySampleAnchor(profile,anchor,out row.sampledAnchor,out failure)){row.status="Destination:"+failure;return row;}
            var path=new NavMeshPath();NavMesh.CalculatePath(row.sampledOrigin,row.sampledAnchor,filter,path);
            row.status=path.status.ToString();row.corners=path.corners;return row;
        }
        private static SurfaceRecord Describe(Component component,Bounds bounds,bool trigger)
        {
            var mesh=component.GetComponent<MeshFilter>();
            return new SurfaceRecord{path=Hierarchy(component.transform),prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component),
                component=component.GetType().Name,mesh=mesh!=null&&mesh.sharedMesh!=null?mesh.sharedMesh.name:string.Empty,
                position=component.transform.position,min=bounds.min,max=bounds.max,trigger=trigger};
        }
        private static string Hierarchy(Transform target)
        {string value=target.name;while(target.parent!=null){target=target.parent;value=target.name+"/"+value;}return value;}
        [TearDown]public void Cleanup()
        {
            if(_draw!=null)SceneView.duringSceneGui-=_draw;if(_view!=null)_view.Close();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);NavMesh.RemoveAllNavMeshData();CaseArtifactWriter.Complete("COMPLETED");
        }
    }
}

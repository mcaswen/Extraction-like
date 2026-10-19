using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExtractionLike.Environment;
using Gameplay.MapGraph.Binding;
using Gameplay.Agent.Core;
using Gameplay.Targets.Authoring;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Visual-only dressing of every published route; keeps original loot entities and colliders.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string RouteNetworkRoot = "Aerospace_AllRoutes_SetDress";
    public const string RouteArtAssets = "Assets/Art/Environment/AerospaceRouteNetwork";
    public const string RouteArtReports = "UserSettings/ScenePreviews/AerospaceAllRoutes";
    public const float CompactRecoveryCaseScale = .75f;
    static readonly string[] RouteArtModels = { "RecoveryCase", "SolarRelay", "EngineCradle", "SurveyBeacon" };
    sealed class ArtEdge { public string id, zone; public Vector3[] corners; public float length; }

    [MenuItem("Tools/Extraction-like/Aerospace/All Routes/Install Aerospace Route Art")]
    public static void BuildAerospaceRouteNetwork()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, RouteNetworkRoot) != null) throw new InvalidOperationException("全路线装饰已存在，不覆盖人工调整。");
        EnsureSceneBackup("UserSettings/SceneBackups/Before_Aerospace_AllRoutes.unity");
        BuildRouteArtPrefabs();
        var boxes = AllInScene<LootBoxEntity>(scene).OrderBy(b => GlobalObjectId.GetGlobalObjectIdSlow(b).ToString()).ToArray();
        var navBefore = CaptureInvestigationNavigation(scene);
        string gameplayBefore = RouteGameplaySignature(scene);
        string terrainBefore = InvestigationTerrainSignature(scene);
        var originalTransforms = AllInScene<Transform>(scene).ToDictionary(t => t, t => RouteTransformSignature(t));
        var protectedPoints = InvestigationInteractionPoints(scene);
        protectedPoints.AddRange(AllInScene<EnemySourceClusterAuthoring>(scene).SelectMany(s => s.SpawnPoints).Where(t => t != null).Select(t => t.position));
        protectedPoints.AddRange(AllInScene<AgentPawnRoot>(scene).Select(a => a.transform.position));
        var edges = RouteArtEdges(scene);
        var corridors = edges.Select(e => e.corners).Concat(InvestigationRouteCorridors(scene)).ToList();
        // Actual within-cluster searches also travel from their anchor to each container.
        var binding = AllInScene<MapGraphBindingAuthoring>(scene).Single();
        foreach (var b in binding.TargetBindings)
        {
            if (!(b.DirectTarget is ResourceClusterAuthoring cluster) || !b.TryGetNavigationAnchor(out var start)) continue;
            foreach (var member in cluster.ResourceMembers)
            {
                if (!NavMesh.SamplePosition(member.Position, out var end, 15, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(start, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) corridors.Add(path.corners);
            }
        }
        var guard = new RouteArtGuard(scene, corridors, protectedPoints);
        var root = new GameObject(RouteNetworkRoot);
        var modifier = root.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
        var manifest = root.AddComponent<AerospaceRouteSetDressing>();
        manifest.originalNavigationHash = navBefore.GeometryHash;
        manifest.originalColliderCount = AllInScene<Collider>(scene).Length;
        manifest.originalAgentCount = navBefore.Agents;
        var cases = new List<AerospaceRouteSetDressing.CaseAppearance>();
        var placements = new List<AerospaceRouteSetDressing.RoutePlacement>();
        try
        {
            foreach (var box in boxes) cases.Add(ReplaceRecoveryCase(box));
            manifest.cases = cases.ToArray();
            int index = 0;
            foreach (var edge in edges)
            {
                var group = CreateSection(root.transform, "Route_" + (++index).ToString("00") + "_" + edge.zone);
                int count = Mathf.Max(1, Mathf.CeilToInt(edge.length / 38f));
                for (int i = 0; i < count; i++)
                {
                    float distance = edge.length * (i + .5f) / count;
                    Vector3 point = RouteArtPoint(edge, distance, out var direction);
                    int kind = RouteArtKind(edge.zone, index + i);
                    var assembly = MakeRouteArtAssembly(group, kind);
                    if (!guard.TryPlace(assembly, point, direction, distance, edge))
                    {
                        Object.DestroyImmediate(assembly.gameObject);
                        assembly = MakeRouteArtAssembly(group, 5);
                        if (!guard.TryPlace(assembly, point, direction, distance, edge)) { Object.DestroyImmediate(assembly.gameObject); continue; }
                    }
                    TryGetBounds(assembly.gameObject, out var bounds);
                    placements.Add(new AerospaceRouteSetDressing.RoutePlacement { edgeId=edge.id, purpose=assembly.name, assembly=assembly,
                        footprintRadius=new Vector2(bounds.extents.x, bounds.extents.z).magnitude });
                }
            }
            manifest.placements = placements.ToArray();
            SetSurveyVisualOnly(root);
            ValidateInvestigationVisualRoot(root);
            foreach (var c in cases) ValidateInvestigationVisualRoot(c.replacement);
            foreach (var p in placements) guard.AssertClear(p.assembly);
            foreach (var pair in originalTransforms)
                if (pair.Key == null || RouteTransformSignature(pair.Key) != pair.Value) throw new InvalidOperationException("Original object transform/activity changed.");
            AssertInvestigationSnapshotEqual(navBefore, CaptureInvestigationNavigation(scene));
            if (RouteGameplaySignature(scene) != gameplayBefore || InvestigationTerrainSignature(scene) != terrainBefore)
                throw new InvalidOperationException("Original gameplay configuration or terrain changed; refusing to save.");
            AssertNoInvestigationNavigationSources(scene, root.transform);
            foreach (var c in cases) AssertNoInvestigationNavigationSources(scene, c.replacement.transform);
            var coverage = RouteArtCoverage(scene, manifest, edges);
            manifest.coveredEdges = coverage.Count(x => x.covered);
            Directory.CreateDirectory(RouteArtReports);
            File.WriteAllText(RouteArtReports+"/coverage-draft.json",JsonUtility.ToJson(new RouteArtInstallation{cases=cases.Count,assemblies=placements.Count,coverage=coverage.ToArray()},true));
            if (manifest.coveredEdges != edges.Count)
            {
                var cycle=FindDayNight(scene);var sky=new Material(cycle.skyboxVersion2);cycle.ApplyPhase(0,sky);
                foreach(var edge in edges.Where(e=>coverage.Any(c=>c.edgeId==e.id&&!c.covered)))
                    RenderActualGameView(scene,RouteArtReports+"/gap-"+edge.id+".png",RouteArtPoint(edge,edge.length*.5f,out _));
                Object.DestroyImmediate(sky);
                throw new InvalidOperationException("Not every published route has visible aerospace equipment: " + string.Join(",", coverage.Where(x => !x.covered).Select(x => x.edgeId)));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(RouteArtReports);
            File.WriteAllText(RouteArtReports + "/installation.json", JsonUtility.ToJson(new RouteArtInstallation {
                cases=cases.Count, assemblies=placements.Count, edges=edges.Count, coveredEdges=manifest.coveredEdges,
                navigationHash=navBefore.GeometryHash, pathProbes=navBefore.Probes, completePaths=navBefore.Complete,
                gameplayHash=gameplayBefore, colliderCount=manifest.originalColliderCount, coverage=coverage.ToArray()
            }, true));
            Debug.Log("ALL_ROUTE_ART_INSTALLED cases="+cases.Count+" assemblies="+placements.Count+" covered="+manifest.coveredEdges+"/"+edges.Count);
        }
        catch
        {
            foreach (var c in cases) { ApplyCaseArt(c,false); if(c.replacement!=null) Object.DestroyImmediate(c.replacement); }
            Object.DestroyImmediate(root); throw;
        }
    }

    static T[] AllInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    static string RouteTransformSignature(Transform t) => t.localPosition.ToString("F6")+t.localRotation.ToString("F6")+t.localScale.ToString("F6")+"|"+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+t.name;

    public static string RouteGameplaySignature(Scene scene)
    {
        // Serialize only original gameplay/physics, not the new passive art manifest or transforms.
        var data = AllInScene<Component>(scene).Where(c=>c!=null && (c is MonoBehaviour || c is Collider || c is Rigidbody || c is NavMeshAgent || c is NavMeshObstacle))
            .Where(c=>!(c is AerospaceRouteSetDressing) &&
                !(c is AerospaceForestStation || (c is NavMeshModifier && c.GetComponentInParent<AerospaceForestStation>(true) != null)) &&
                !((c is NavMeshModifier || c is RecoveryCaseOpeningFeedback) && IsRouteArt(c.transform)))
            .Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c)+"|"+EditorJsonUtility.ToJson(c)).OrderBy(s=>s,StringComparer.Ordinal);
        using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n",data))));
    }

    static bool IsRouteArt(Transform t)
    {
        for(;t!=null;t=t.parent) if(t.name==RouteNetworkRoot || t.name=="Aerospace_RecoveryCase_Visual") return true;
        return false;
    }

    static void BuildRouteArtPrefabs(bool recoveryOnly = false)
    {
        foreach(var folder in new[]{"Materials","Prefabs"}) EnsureAssetFolder(RouteArtAssets+"/"+folder);
        foreach(string key in new[]{"Ceramic","Graphite","Alloy","Orange","Signal","Solar"}) RouteArtMaterial(key);
        foreach(string asset in RouteArtModels)
        {
            if (recoveryOnly && asset != "RecoveryCase") continue;
            var root = new GameObject(asset);
            var nav = root.AddComponent<NavMeshModifier>(); nav.ignoreFromBuild=true; nav.applyToChildren=true;
            var lods = new LOD[3];
            for(int i=0;i<3;i++)
            {
                string path=RouteArtAssets+"/Models/"+asset+"_LOD"+i+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null) throw new FileNotFoundException(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.addCollider=false; importer.importAnimation=false; importer.importLights=false; importer.importCameras=false;
                importer.importNormals=ModelImporterNormals.Import; importer.isReadable=true; importer.globalScale=1;
                importer.SaveAndReimport();
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
                model.name="LOD"+i;
                var renderers=model.GetComponentsInChildren<MeshRenderer>();
                foreach(var renderer in renderers)
                {
                    string key=new[]{"Ceramic","Graphite","Alloy","Orange","Signal","Solar"}.Single(k=>renderer.name.Contains("Route_"+k));
                    renderer.sharedMaterials=Enumerable.Repeat(RouteArtMaterial(key),renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode=ShadowCastingMode.On;
                }
                lods[i]=new LOD(i==0?.18f:i==1?.06f:.009f,renderers);
            }
            if (asset == "RecoveryCase") BuildRecoveryCaseRig(root, lods);
            var group=root.AddComponent<LODGroup>(); group.SetLODs(lods);group.RecalculateBounds();
            // The open lid stays in the original horizontal footprint but grows vertically.
            if (asset == "RecoveryCase") { group.localReferencePoint = new Vector3(0, 1.75f, 0); group.size = 4f; }
            SetSurveyVisualOnly(root);
            PrefabUtility.SaveAsPrefabAsset(root,RouteArtAssets+"/Prefabs/"+asset+".prefab");Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    static void BuildRecoveryCaseRig(GameObject root, LOD[] lods)
    {
        var lids = new List<Transform>(); var left = new List<Transform>(); var right = new List<Transform>();
        var signals = new List<Renderer>();
        for (int level = 0; level < lods.Length; level++)
        {
            // Unpack the imported model before reparenting its articulated meshes.
            var imported = root.transform.Find("LOD" + level).gameObject;
            PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (string part in new[]{"Lid", "LockLeft", "LockRight"})
            {
                var pivot = new GameObject("LOD" + level + "_" + part + "_Pivot").transform;
                pivot.SetParent(root.transform, false);
                if (part == "Lid") pivot.localPosition = new Vector3(0, 1.40f, 1.075f);
                foreach (var renderer in lods[level].renderers.Where(r => r.name.Contains("_" + part + "_")))
                    renderer.transform.SetParent(pivot, true);
                if (part == "Lid") lids.Add(pivot); else if (part == "LockLeft") left.Add(pivot); else right.Add(pivot);
            }
            signals.AddRange(lods[level].renderers.Where(r => r.name.Contains("Route_Signal")));
        }
        var sound = root.AddComponent<AudioSource>();
        sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/GameAudio/SFX/recovery_case_open.wav");
        if (sound.clip == null) throw new FileNotFoundException("Recovery case opening sound must be generated first.");
        sound.playOnAwake = false; sound.loop = false; sound.volume = .82f;
        sound.spatialBlend = .35f; sound.minDistance = 16; sound.maxDistance = 120;
        sound.dopplerLevel = 0; sound.rolloffMode = AudioRolloffMode.Linear; sound.priority = 90;
        root.AddComponent<RecoveryCaseOpeningFeedback>().Configure(lids.ToArray(), left.ToArray(), right.ToArray(), signals.ToArray(), sound);
    }

    public static void BuildRecoveryCaseFeedback()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string gameplay = RouteGameplaySignature(scene);
        var navigation = CaptureInvestigationNavigation(scene);
        BuildRouteArtPrefabs(true);
        var manifest = AllInScene<AerospaceRouteSetDressing>(scene).Single();
        foreach (var entry in manifest.cases) FitRecoveryCase(entry);
        Debug.Log("RECOVERY_NAV_BEFORE="+navigation.GeometryHash+" saved="+manifest.originalNavigationHash+" after="+CaptureInvestigationNavigation(scene).GeometryHash);
        ValidateAerospaceRouteNetwork();
        if (gameplay != RouteGameplaySignature(scene)) throw new InvalidOperationException("Original gameplay configuration changed.");
        AssertInvestigationSnapshotEqual(navigation, CaptureInvestigationNavigation(scene));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("RECOVERY_CASE_FEEDBACK_BUILT cases=" + manifest.cases.Length + " originalGameplayAndNavigationUnchanged=true");
    }

    public static void RefreshRouteArtModelAssets()
    {
        BuildRouteArtPrefabs();
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var manifest=AllInScene<AerospaceRouteSetDressing>(scene).Single();
        foreach(var c in manifest.cases)
        {
            if(c.originalStates==null||c.originalStates.Length==0)c.originalStates=c.originalRenderers.Select(r=>new AerospaceRouteSetDressing.OriginalRendererState{materials=r.sharedMaterials,shadowCasting=r.shadowCastingMode}).ToArray();
            FitRecoveryCase(c);
            ApplyCaseArt(c,true);
        }
        ValidateAerospaceRouteNetwork();
        EditorUtility.SetDirty(manifest);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        RenderAerospaceAllRoutes();
    }

    static Material RouteArtMaterial(string key)
    {
        string path=RouteArtAssets+"/Materials/M_Route_"+key+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null)return mat;
        Color color=key=="Ceramic"?new Color(.72f,.79f,.81f):key=="Graphite"?new Color(.032f,.059f,.076f):
            key=="Alloy"?new Color(.32f,.43f,.49f):key=="Orange"?new Color(.94f,.25f,.035f):key=="Signal"?new Color(.10f,.73f,.94f):new Color(.018f,.10f,.22f);
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="M_Route_"+key,enableInstancing=true};
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",key=="Alloy"?.75f:key=="Solar"?.35f:key=="Graphite"?.25f:.04f);
        mat.SetFloat("_Smoothness",key=="Alloy"?.62f:.32f);
        if(key=="Signal") {mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*1.35f);}
        AssetDatabase.CreateAsset(mat,path);return mat;
    }

    static Bounds RouteLocalBounds(Transform root, IEnumerable<Renderer> renderers)
    {
        bool any=false;Bounds bounds=default;
        foreach(var r in renderers)
        {
            Bounds b=r.localBounds;
            for(int i=0;i<8;i++)
            {
                var p=root.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);
            }
        }
        if(!any)throw new InvalidOperationException("Empty visual bounds.");return bounds;
    }

    static AerospaceRouteSetDressing.CaseAppearance ReplaceRecoveryCase(LootBoxEntity container)
    {
        var old=container.GetComponentsInChildren<Renderer>(true);
        if(old.Length==0)throw new InvalidOperationException("Container has no visual: "+container.name);
        Bounds target=RouteLocalBounds(container.transform,old);
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RouteArtAssets+"/Prefabs/RecoveryCase.prefab"),container.transform);
        visual.name="Aerospace_RecoveryCase_Visual";
        var record=new AerospaceRouteSetDressing.CaseAppearance {container=container,originalRenderers=old,originalEnabled=old.Select(r=>r.enabled).ToArray(),replacement=visual,originalLocalBounds=target,visualScale=CompactRecoveryCaseScale,
            originalStates=old.Select(r=>new AerospaceRouteSetDressing.OriginalRendererState{materials=r.sharedMaterials,shadowCasting=r.shadowCastingMode}).ToArray()};
        FitRecoveryCase(record);ApplyCaseArt(record,true);
        return record;
    }

    static void FitRecoveryCase(AerospaceRouteSetDressing.CaseAppearance c)
    {
        var t=c.replacement.transform;t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;
        var source=RouteLocalBounds(t,c.replacement.GetComponentsInChildren<Renderer>(true));var target=RecoveryCaseDisplayBounds(c);
        Vector3 scale=new Vector3(target.size.x/source.size.x,target.size.y/source.size.y,target.size.z/source.size.z);
        t.localScale=scale;t.localPosition=target.center-Vector3.Scale(source.center,scale);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    public static Bounds RecoveryCaseDisplayBounds(AerospaceRouteSetDressing.CaseAppearance c)
    {
        // Legacy records remain full-sized until explicitly resized. Anchor the bottom,
        // not the centre, so changing only the art never lifts it off its existing base.
        float factor=c.visualScale>0 ? Mathf.Clamp(c.visualScale,.35f,1f) : 1f;
        Vector3 size=c.originalLocalBounds.size*factor,center=c.originalLocalBounds.center;
        center.y=c.originalLocalBounds.min.y+size.y*.5f;
        return new Bounds(center,size);
    }

    static Material RouteNavigationOnlyMaterial()
    {
        string path=RouteArtAssets+"/Materials/M_NavigationOnly.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
        m=new Material(Shader.Find("Aerospace/Navigation Source Only"));AssetDatabase.CreateAsset(m,path);return m;
    }

    static void ApplyCaseArt(AerospaceRouteSetDressing.CaseAppearance c,bool show)
    {
        c.replacement.SetActive(show);
        for(int i=0;i<c.originalRenderers.Length;i++)
        {
            var r=c.originalRenderers[i];r.enabled=c.originalEnabled[i];
            r.sharedMaterials=show?Enumerable.Repeat(RouteNavigationOnlyMaterial(),c.originalStates[i].materials.Length).ToArray():c.originalStates[i].materials;
            r.shadowCastingMode=show?ShadowCastingMode.Off:c.originalStates[i].shadowCasting;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(c.replacement);
    }

    static List<ArtEdge> RouteArtEdges(Scene scene)
    {
        var binding=AllInScene<MapGraphBindingAuthoring>(scene).Single();
        var edges=new List<ArtEdge>();
        foreach(var edge in binding.MapDefinition.Edges)
        {
            if(!binding.TryGetNavigationAnchor(edge.FromNodeId,out var a)||!binding.TryGetNavigationAnchor(edge.ToNodeId,out var b))throw new InvalidOperationException("Unbound route: "+edge.EdgeId);
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("Original route is disconnected: "+edge.EdgeId);
            binding.TryGetBinding(edge.FromNodeId,out var from);
            var corners=path.corners;
            edges.Add(new ArtEdge{id=edge.EdgeId,zone=from.DirectTarget.transform.parent.name.Replace("Zone-",""),corners=corners,length=Enumerable.Range(1,corners.Length-1).Sum(i=>Vector3.Distance(corners[i-1],corners[i]))});
        }
        return edges;
    }

    static Vector3 RouteArtPoint(ArtEdge edge,float distance,out Vector3 direction)
    {
        distance=Mathf.Clamp(distance,0,edge.length);
        for(int i=1;i<edge.corners.Length;i++)
        {
            float length=Vector3.Distance(edge.corners[i-1],edge.corners[i]);
            if(distance<=length||i==edge.corners.Length-1){direction=(edge.corners[i]-edge.corners[i-1]).normalized;return Vector3.Lerp(edge.corners[i-1],edge.corners[i],length>.001f?distance/length:0);}
            distance-=length;
        }
        direction=Vector3.forward;return edge.corners[0];
    }

    static int RouteArtKind(string zone,int seed)
    {
        if(seed%3==0)return 5;
        if(zone.Contains("实验室"))return seed%2==0?1:4;
        if(zone.Contains("雨林"))return seed%2==0?0:2;
        if(zone.Contains("奇点"))return seed%2==0?2:1;
        if(zone.Contains("渔村"))return seed%2==0?3:1;
        if(zone.Contains("龙骨"))return seed%2==0?0:2;
        return seed%2==0?0:4;
    }

    static Transform MakeRouteArtAssembly(Transform parent,int kind)
    {
        string[] names={"Solar_Telemetry_Station","Recovered_Engine_Transport","Tracking_Dish","Return_Cargo_Drone","Pressurant_Service_Unit","Optical_Survey_Beacon"};
        var assembly=CreateSection(parent,names[kind]);
        if(kind==0||kind==1||kind==5)
        {
            string model=kind==0?"SolarRelay":kind==1?"EngineCradle":"SurveyBeacon";
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RouteArtAssets+"/Prefabs/"+model+".prefab"),assembly);
            go.transform.localScale=Vector3.one*(kind==0?1.7f:kind==1?2.1f:1.65f);
        }
        else
        {
            string filename=kind==2?"satelliteDish_detailed":kind==3?"craft_cargoA":"machine_barrelLarge";
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SpaceModels+"/"+filename+".fbx"),assembly);
            var b=RouteLocalBounds(go.transform,go.GetComponentsInChildren<Renderer>());
            float scale=(kind==2?7.7f:kind==3?9.5f:6.8f)/Mathf.Max(b.size.x,b.size.y,b.size.z);
            go.transform.localScale=Vector3.one*scale;go.transform.localPosition=-b.center*scale+Vector3.up*b.size.y*scale*.5f;
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials=r.sharedMaterials.Select(m=>RouteArtMaterial(m==null?"Ceramic":m.name.Contains("Orange")?"Orange":m.name.Contains("Dark")?"Graphite":m.name.Contains("Metal")?"Alloy":"Ceramic")).ToArray();
            var lod=go.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.014f,go.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
            // A few small skid pads keep equipment grounded; no fence, deck across the path, or collider.
            for(int i=0;i<2;i++)SurveyCube(assembly,"Equipment_Skid",new Vector3((i==0?-1:1)*2, .10f,0),new Vector3(.55f,.20f,4.2f),RouteArtMaterial("Graphite"));
        }
        return assembly;
    }

    sealed class RouteArtGuard
    {
        readonly List<Vector3[]> paths; readonly List<Vector3> points; readonly List<Bounds> obstacles=new List<Bounds>();
        readonly List<Vector3> trees=new List<Vector3>(); readonly List<KeyValuePair<Vector2,float>> reserved=new List<KeyValuePair<Vector2,float>>();
        public RouteArtGuard(Scene scene,List<Vector3[]> paths,List<Vector3> points)
        {
            this.paths=paths;this.points=points;Physics.SyncTransforms();
            foreach(var r in AllInScene<MeshRenderer>(scene).Where(r=>r.enabled&&r.gameObject.activeInHierarchy))
            {var b=r.bounds;if(b.size.y<.7f||b.size.x>350||b.size.z>350)continue;obstacles.Add(b);}
            foreach(var t in AllInScene<Terrain>(scene))foreach(var tree in t.terrainData.treeInstances)trees.Add(t.transform.position+Vector3.Scale(tree.position,t.terrainData.size));
        }
        bool RouteClear(Vector2 p,float radius)
        {
            if(points.Any(v=>Vector2.Distance(p,new Vector2(v.x,v.z))<radius+6))return false;
            foreach(var line in paths)for(int i=1;i<line.Length;i++)if(InvestigationSegmentDistance(p,new Vector2(line[i-1].x,line[i-1].z),new Vector2(line[i].x,line[i].z))<radius+4.5f)return false;
            return true;
        }
        public bool TryPlace(Transform assembly,Vector3 point,Vector3 direction,float distance,ArtEdge edge)
        {
            TryGetBounds(assembly.gameObject,out var original);
            float radius=new Vector2(original.extents.x,original.extents.z).magnitude;
            foreach(float along in new[]{0f,-7f,7f,-14f,14f,-23f,23f})
            {
                var route=RouteArtPoint(edge,distance+along,out direction);direction.y=0;direction.Normalize();
                var side=new Vector3(direction.z,0,-direction.x);
                foreach(float off in new[]{radius+6.0f,radius+10f,radius+15f,radius+21f})foreach(int sign in new[]{1,-1})
                {
                    var p=route+side*off*sign;var xz=new Vector2(p.x,p.z);
                    if(!RouteClear(xz,radius)||reserved.Any(r=>Vector2.Distance(xz,r.Key)<radius+r.Value+3))continue;
                    float y,min,max;
                    try {y=RouteEquipmentFloor(p.x,p.z,route.y);min=y;max=y;
                        foreach(var offset in new[]{new Vector2(radius*.7f,0),new Vector2(-radius*.7f,0),new Vector2(0,radius*.7f),new Vector2(0,-radius*.7f)}){float h=RouteEquipmentFloor(p.x+offset.x,p.z+offset.y,route.y);min=Mathf.Min(min,h);max=Mathf.Max(max,h);}}
                    catch(InvalidOperationException){continue;}
                    if(max-min>.65f||Mathf.Abs(y-route.y)>4)continue;
                    if(trees.Any(t=>Vector2.Distance(xz,new Vector2(t.x,t.z))<radius+2.7f))continue;
                    // A large hollow ruin has one enclosing bounds but open walkable interior.
                    // Test its actual collider geometry, not a solid box across the whole landmark.
                    if(obstacles.Any(b=>(b.size.x<35||b.size.z<35)&&b.max.y>y+.65f && b.min.y<y+original.size.y+1 && DistanceToBoundsXZ(xz,b)<radius+.45f))continue;
                    if(Physics.OverlapBox(new Vector3(p.x,max+.55f+original.size.y*.5f,p.z),new Vector3(radius*.72f,original.size.y*.5f,radius*.72f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>!(c is TerrainCollider)))continue;
                    // Face the instruments toward the approach, while the top profile remains readable.
                    assembly.rotation=Quaternion.Euler(0,Vector3.SignedAngle(Vector3.forward,side*-sign,Vector3.up),0);
                    assembly.position=new Vector3(p.x,max+.025f,p.z);
                    TryGetBounds(assembly.gameObject,out var adjusted);
                    assembly.position+=Vector3.up*(max+.025f-adjusted.min.y);
                    reserved.Add(new KeyValuePair<Vector2,float>(xz,radius));return true;
                }
            }
            return false;
        }
        static float RouteEquipmentFloor(float x,float z,float expected)
        {
            float floor=GroundHeight(x,z);
            if(NavMesh.SamplePosition(new Vector3(x,expected,z),out var surface,2.6f,NavMesh.AllAreas) &&
                Vector2.Distance(new Vector2(x,z),new Vector2(surface.position.x,surface.position.z))<.4f)
                floor=Mathf.Max(floor,surface.position.y-.15f);
            foreach(var hit in Physics.RaycastAll(new Vector3(x,expected+10,z),Vector3.down,30,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.normal.y<.72f || Mathf.Abs(hit.point.y-expected)>2.6f)continue;
                if(hit.collider.GetComponentInParent<LootBoxEntity>()!=null||hit.collider.GetComponentInParent<AgentPawnRoot>()!=null)continue;
                floor=Mathf.Max(floor,hit.point.y);
            }
            return floor;
        }
        public void AssertClear(Transform assembly)
        {
            TryGetBounds(assembly.gameObject,out var b);
            // Oriented assemblies use their original enclosing radius; use true mesh vertices for the final cylinder.
            Vector3 center=assembly.position;float radius=0;
            foreach(var r in assembly.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                foreach(var v in mesh.vertices){var w=r.transform.TransformPoint(v)-center;radius=Mathf.Max(radius,new Vector2(w.x,w.z).magnitude);}
            }
            if(!RouteClear(new Vector2(center.x,center.z),radius))throw new InvalidOperationException("Route art infringes a route: "+assembly.name);
        }
    }

    [Serializable] public sealed class RouteArtCoverageEntry { public string edgeId; public bool covered; public int samples, readableSamples; public float maxDistance; }
    [Serializable] public sealed class RouteArtInstallation { public int cases,assemblies,edges,coveredEdges,pathProbes,completePaths,colliderCount;public string navigationHash,gameplayHash;public RouteArtCoverageEntry[] coverage; }
    static List<RouteArtCoverageEntry> RouteArtCoverage(Scene scene,AerospaceRouteSetDressing manifest,List<ArtEdge> edges)
    {
        var positions=manifest.placements.Select(p=>p.assembly.position).Concat(manifest.cases.Select(c=>c.container.transform.position)).ToArray();
        var result=new List<RouteArtCoverageEntry>();
        foreach(var edge in edges)
        {
            var c=new RouteArtCoverageEntry{edgeId=edge.id,samples=Mathf.Max(3,Mathf.CeilToInt(edge.length/12)+1)};
            for(int i=0;i<c.samples;i++)
            {
                var p=RouteArtPoint(edge,edge.length*i/(c.samples-1),out _);
                float d=positions.Min(v=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(v.x,v.z)));
                c.maxDistance=Mathf.Max(c.maxDistance,d);if(d<48)c.readableSamples++;
            }
            c.covered=c.readableSamples==c.samples;result.Add(c);
        }
        return result;
    }

    public static void ValidateAerospaceRouteNetwork()
    {
        var scene=SceneManager.GetActiveScene();var m=AllInScene<AerospaceRouteSetDressing>(scene).Single();
        if(m.cases.Length!=AllInScene<LootBoxEntity>(scene).Length||m.cases.Select(c=>c.container).Distinct().Count()!=m.cases.Length)throw new InvalidOperationException("Containers missing or duplicated.");
        foreach(var c in m.cases)
        {
            if(c.container==null||c.replacement==null||!c.replacement.activeSelf||c.originalRenderers.Any(r=>r==null||r.sharedMaterials.Any(m=>m==null||m.shader.name!="Aerospace/Navigation Source Only")))throw new InvalidOperationException("Incomplete replacement.");
            if(c.replacement.transform.parent!=c.container.transform)throw new InvalidOperationException("Case binding changed.");
            var b=RouteLocalBounds(c.container.transform,c.replacement.GetComponentsInChildren<Renderer>(true));
            var expected=RecoveryCaseDisplayBounds(c);
            if(Vector3.Distance(b.center,expected.center)>.015f||Vector3.Distance(b.size,expected.size)>.02f)throw new InvalidOperationException("Replacement does not match its ground-anchored display scale: "+GetPath(c.container.transform)+" new="+b+" expected="+expected);
            ValidateInvestigationVisualRoot(c.replacement);
        }
        ValidateInvestigationVisualRoot(m.gameObject);
        if(AllInScene<Collider>(scene).Length!=m.originalColliderCount)throw new InvalidOperationException("Collider count changed.");
        if(CaptureInvestigationNavigation(scene).GeometryHash!=m.originalNavigationHash)throw new InvalidOperationException("Navigation data changed.");
        if(RouteArtCoverage(scene,m,RouteArtEdges(scene)).Any(e=>!e.covered))throw new InvalidOperationException("Route coverage gap.");
    }

    public static void VerifyAerospaceRouteNetworkDelivery()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        ValidateAerospaceRouteNetwork();
        var install=JsonUtility.FromJson<RouteArtInstallation>(File.ReadAllText(RouteArtReports+"/installation.json"));
        if(RouteGameplaySignature(scene)!=install.gameplayHash)throw new InvalidOperationException("Saved gameplay component baseline does not match the original installation snapshot.");
        Debug.Log("ROUTE_ART_DELIVERY_VERIFIED originalGameplayUnchanged=true cases="+install.cases+" routes="+install.edges+" assemblies="+install.assemblies);
    }

    [MenuItem("Tools/Extraction-like/Aerospace/All Routes/Compare Original Appearance")]
    static void CompareOriginalRouteArt()=>SetRouteArtAppearance(false);
    [MenuItem("Tools/Extraction-like/Aerospace/All Routes/Show Aerospace Appearance")]
    static void ShowNewRouteArt()=>SetRouteArtAppearance(true);
    public static void SetRouteArtAppearance(bool show)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先退出 Play 模式。");
        var m=AllInScene<AerospaceRouteSetDressing>(SceneManager.GetActiveScene()).Single();
        Undo.RecordObject(m.gameObject,"Compare route art"); m.gameObject.SetActive(show);
        foreach(var c in m.cases)
        {
            Undo.RecordObject(c.replacement,"Compare case art");c.replacement.SetActive(show);
            foreach(var r in c.originalRenderers)Undo.RecordObject(r,"Compare case art");
            ApplyCaseArt(c,show);
        }
        EditorSceneManager.MarkSceneDirty(m.gameObject.scene);
    }

    public static void RenderAerospaceAllRoutes()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var cycle=FindDayNight(scene);var sky=new Material(cycle.skyboxVersion2);
        Directory.CreateDirectory(RouteArtReports);
        var graph=AllInScene<MapGraphBindingAuthoring>(scene).Single();
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            foreach(float phase in new[]{0f,.5f})
            {
                cycle.ApplyPhase(phase,sky);
                foreach(var site in AllInScene<AerospaceLandingSite>(scene))site.ApplyPhase(phase);
                foreach(var light in AllInScene<AerospaceNightGroundLighting>(scene))light.ApplyPhase(phase);
                foreach(var group in graph.TargetBindings.GroupBy(b=>b.DirectTarget.transform.parent.name))
                {
                    var b=group.FirstOrDefault(t=>t.DirectTarget is ResourceClusterAuthoring)??group.First();b.TryGetNavigationAnchor(out var p);
                    string path=RouteArtReports+"/"+(phase==0?"day-":"night-")+group.Key+".png";
                    RenderActualGameView(scene,path,p);if(phase==0)RenderActualGameView(scene,path,p);
                }
            }
            cycle.ApplyPhase(0,sky);
            int i=0;foreach(var edge in RouteArtEdges(scene))RenderActualGameView(scene,RouteArtReports+"/route-"+(++i).ToString("00")+".png",RouteArtPoint(edge,edge.length*.5f,out _));
            var caseGo=AllInScene<AerospaceRouteSetDressing>(scene).Single().cases.First(c=>GetPath(c.container.transform).Contains("员工食堂")).replacement;
            TryGetBounds(caseGo,out var bounds);
            RenderCameraPreview(RouteArtReports+"/recovery-case-closeup.png",bounds.center+new Vector3(15,14,-19),bounds.center,40);
        }
        finally{ShaderUtil.allowAsyncCompilation=async;Object.DestroyImmediate(sky);}
    }

    public static void BuildAndRenderAerospaceAllRoutes(){BuildAerospaceRouteNetwork();RenderAerospaceAllRoutes();}
}

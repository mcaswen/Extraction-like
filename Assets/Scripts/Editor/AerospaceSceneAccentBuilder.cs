using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using Gameplay.Agent.Core;
using Gameplay.Targets.Authoring;
using Gameplay.MapGraph.Binding;
using ExtractionLike.Environment;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    public const string SceneAccentRoot = "Aerospace_ThreeAreas_Research_Details";
    public const string SceneAccentReports = "UserSettings/ScenePreviews/AerospaceThreeAreas";
    public const string SceneAccentAssets = "Assets/Art/Environment/AerospaceResearchDetails";
    static readonly string[] AccentModels = { "ReturnCapsule", "OrbitalSatellite", "TelemetryDish" };
    [Serializable] public class AccentLocation { public string path; public Vector3 position, size; }
    [Serializable] public class AccentDelivery { public string navigationHash, gameplayHash; public int pathProbes, completePaths, colliders, agents; public AccentLocation[] placements; }
    sealed class AccentSpec
    {
        public string area, name, model; public Vector2 desired; public float span, yaw;
        public AccentSpec(string area,string name,string model,float x,float z,float span,float yaw=0)
        {this.area=area;this.name=name;this.model=model;desired=new Vector2(x,z);this.span=span;this.yaw=yaw;}
    }
    static AccentSpec[] AccentPlan() => new[] {
        new AccentSpec("01_StoneArch_Telemetry","Arch_Tracking_Dish","TelemetryDish",44,-40,6.8f,160),
        new AccentSpec("01_StoneArch_Telemetry","Arch_Solar_Power","SolarRelay",65,-44,5.8f,145),
        new AccentSpec("01_StoneArch_Telemetry","Arch_Engine_Survey","EngineCradle",6,-36,5.4f,20),
        new AccentSpec("01_StoneArch_Telemetry","Arch_Wireless_Node","SolarRelay",99,-47,5.3f,-30),
        new AccentSpec("02_Rainforest_Capsule_Service","Forest_Return_Capsule","ReturnCapsule",36,243,9f,20),
        new AccentSpec("02_Rainforest_Capsule_Service","Forest_Tracking_Dish","TelemetryDish",4,180,8.5f,-20),
        new AccentSpec("02_Rainforest_Capsule_Service","Forest_Satellite_Sample","OrbitalSatellite",37,193,12f,20),
        new AccentSpec("02_Rainforest_Capsule_Service","Forest_Engine_Cradle","EngineCradle",57,219,6.7f,55),
        new AccentSpec("03_Tower_Orbital_Research","Tower_Satellite_Calibration","OrbitalSatellite",51,-112,12.5f,20),
        new AccentSpec("03_Tower_Orbital_Research","Tower_Return_Capsule","ReturnCapsule",-50,-112,8.5f,-15),
        new AccentSpec("03_Tower_Orbital_Research","Tower_Tracking_Dish","TelemetryDish",52,-128,6.8f,150),
        new AccentSpec("03_Tower_Orbital_Research","Tower_Engine_Sample","EngineCradle",49,-153,8f,-20)
    };

    [MenuItem("Tools/Extraction-like/Aerospace/Three Areas/Install Research Details")]
    public static void BuildSceneResearchAccents()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        if(FindByName(scene,SceneAccentRoot)!=null)throw new InvalidOperationException("Research details already exist; preserve manual positioning.");
        var parent=FindByName(scene,RouteNetworkRoot);
        if(parent==null)throw new InvalidOperationException("The existing route-art root is missing.");
        EnsureSceneBackup("UserSettings/SceneBackups/Before_ThreeAreas_20261019.unity");
        var before=CaptureInvestigationNavigation(scene);
        string gameplay=RouteGameplaySignature(scene),terrain=InvestigationTerrainSignature(scene);
        var originals=AllInScene<Transform>(scene).ToDictionary(t=>t,t=>RouteTransformSignature(t));
        int colliders=AllInScene<Collider>(scene).Length;
        BuildResearchAccentPrefabs();
        var paths=AccentProtectedPaths(scene);var points=AccentProtectedPoints(scene);
        var guard=new AccentPlacementGuard(scene,paths,points);
        var root=CreateSection(parent,SceneAccentRoot);
        var modifier=root.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
        var groups=new Dictionary<string,Transform>();
        var placements=new List<AccentLocation>();
        try
        {
            foreach(var spec in AccentPlan())
            {
                if(!groups.TryGetValue(spec.area,out var group))groups.Add(spec.area,group=CreateSection(root,spec.area));
                string folder=AccentModels.Contains(spec.model)?SceneAccentAssets:RouteArtAssets;
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/Prefabs/"+spec.model+".prefab");
                if(asset==null)throw new FileNotFoundException(spec.model);
                var assembly=CreateSection(group,spec.name);
                var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,assembly);
                var b=RouteLocalBounds(model.transform,model.GetComponentsInChildren<Renderer>(true));
                float scale=spec.span/Mathf.Max(b.size.x,b.size.y,b.size.z);
                model.transform.localScale=Vector3.one*scale;
                model.transform.localPosition=new Vector3(-b.center.x,-b.min.y,-b.center.z)*scale;
                assembly.rotation=Quaternion.Euler(0,spec.yaw,0);
                guard.Place(assembly,spec.desired);
                AddAccentLevelingFeet(assembly,spec.model,spec.span);
                TryGetBounds(assembly.gameObject,out b);
                placements.Add(new AccentLocation{path=GetPath(assembly),position=assembly.position,size=b.size});
            }
            SetSurveyVisualOnly(root.gameObject);ValidateInvestigationVisualRoot(root.gameObject);
            AssertNoInvestigationNavigationSources(scene,root);
            foreach(var group in groups.Values)foreach(Transform item in group)guard.AssertClear(item);
            foreach(var p in originals)if(p.Key==null||RouteTransformSignature(p.Key)!=p.Value)throw new InvalidOperationException("An original transform changed.");
            AssertInvestigationSnapshotEqual(before,CaptureInvestigationNavigation(scene));
            if(RouteGameplaySignature(scene)!=gameplay||InvestigationTerrainSignature(scene)!=terrain||AllInScene<Collider>(scene).Length!=colliders)
                throw new InvalidOperationException("Original gameplay, terrain or collision changed; refusing to save.");
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            AssetDatabase.SaveAssets();Directory.CreateDirectory(SceneAccentReports);
            File.WriteAllText(SceneAccentReports+"/installation.json",JsonUtility.ToJson(new AccentDelivery{
                navigationHash=before.GeometryHash,gameplayHash=gameplay,pathProbes=before.Probes,completePaths=before.Complete,
                colliders=colliders,agents=before.Agents,placements=placements.ToArray()},true));
            Debug.Log("THREE_AREAS_INSTALLED assemblies="+placements.Count+" nav="+before.GeometryHash);
            RenderResearchAccentViews(scene);
        }
        catch {Object.DestroyImmediate(root.gameObject);throw;}
    }

    static void BuildResearchAccentPrefabs()
    {
        EnsureAssetFolder(SceneAccentAssets+"/Prefabs");EnsureAssetFolder(SceneAccentAssets+"/Materials");
        string foilPath=SceneAccentAssets+"/Materials/M_Research_ThermalFoil.mat";
        var foil=AssetDatabase.LoadAssetAtPath<Material>(foilPath);
        if(foil==null)
        {
            foil=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Research_ThermalFoil",enableInstancing=true};
            foil.SetColor("_BaseColor",new Color(.57f,.32f,.075f));foil.SetFloat("_Metallic",.70f);foil.SetFloat("_Smoothness",.42f);
            AssetDatabase.CreateAsset(foil,foilPath);
        }
        foreach(var name in AccentModels)
        {
            var root=new GameObject(name);var nav=root.AddComponent<NavMeshModifier>();nav.ignoreFromBuild=true;nav.applyToChildren=true;
            var lods=new LOD[3];
            for(int i=0;i<3;i++)
            {
                string path=SceneAccentAssets+"/Models/"+name+"_LOD"+i+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null)throw new FileNotFoundException(path);
                importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.addCollider=false;
                importer.importAnimation=false;importer.importLights=false;importer.importCameras=false;
                importer.importNormals=ModelImporterNormals.Import;importer.isReadable=true;importer.globalScale=1;importer.SaveAndReimport();
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);model.name="LOD"+i;
                var renderers=model.GetComponentsInChildren<MeshRenderer>();
                foreach(var r in renderers)
                {
                    string key=new[]{"Ceramic","Graphite","Alloy","Orange","Signal","Solar","Foil"}.Single(k=>r.name.Contains("Route_"+k));
                    r.sharedMaterials=Enumerable.Repeat(key=="Foil"?foil:RouteArtMaterial(key),r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode=ShadowCastingMode.On;
                }
                lods[i]=new LOD(i==0?.19f:i==1?.07f:.009f,renderers);
            }
            var lod=root.AddComponent<LODGroup>();lod.SetLODs(lods);lod.RecalculateBounds();SetSurveyVisualOnly(root);
            PrefabUtility.SaveAsPrefabAsset(root,SceneAccentAssets+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
    }

    static void AddAccentLevelingFeet(Transform assembly,string model,float span)
    {
        // Extend the existing transport stand down to uneven terrain; never flatten
        // the ground or leave an unsupported slab suspended over the roadside.
        float x=span*(model=="OrbitalSatellite"?.17f:model=="SolarRelay"?.10f:.34f);
        float z=span*(model=="OrbitalSatellite"?.15f:model=="SolarRelay"?.10f:.34f);
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            var top=assembly.TransformPoint(new Vector3(x*sx,.22f,z*sz));
            float ground=BaseRouteFloorHeight(top.x,top.z),height=top.y-ground;
            if(height<.08f)continue;
            var point=assembly.InverseTransformPoint(new Vector3(top.x,ground+height*.5f,top.z));
            SurveyCube(assembly,"Adjustable_Stand_Foot",point,new Vector3(.34f,height,.34f),RouteArtMaterial("Alloy"));
            SurveyCube(assembly,"Stand_Ground_Pad",assembly.InverseTransformPoint(new Vector3(top.x,ground+.035f,top.z)),new Vector3(.68f,.14f,.68f),RouteArtMaterial("Graphite"));
        }
    }

    static List<Vector3[]> AccentProtectedPaths(Scene scene)
    {
        var paths=RouteArtEdges(scene).Select(e=>e.corners).Concat(InvestigationRouteCorridors(scene)).ToList();
        var binding=AllInScene<MapGraphBindingAuthoring>(scene).Single();
        foreach(var b in binding.TargetBindings)
        {
            if(!(b.DirectTarget is ResourceClusterAuthoring cluster)||!b.TryGetNavigationAnchor(out var start))continue;
            foreach(var member in cluster.ResourceMembers)
            {
                if(!NavMesh.SamplePosition(member.Position,out var end,15,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();
                if(NavMesh.CalculatePath(start,end.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)paths.Add(path.corners);
            }
        }
        return paths;
    }
    static List<Vector3> AccentProtectedPoints(Scene scene)
    {
        var p=InvestigationInteractionPoints(scene);
        p.AddRange(AllInScene<EnemySourceClusterAuthoring>(scene).SelectMany(c=>c.SpawnPoints).Where(t=>t!=null).Select(t=>t.position));
        p.AddRange(AllInScene<AgentPawnRoot>(scene).Select(a=>a.transform.position));return p;
    }
    sealed class AccentPlacementGuard
    {
        readonly List<Vector3[]> paths;readonly List<Vector3> points;readonly Bounds[] obstacles;readonly Bounds[] trees;
        readonly List<Bounds> placed=new List<Bounds>();
        public AccentPlacementGuard(Scene scene,List<Vector3[]> paths,List<Vector3> points)
        {
            this.paths=paths;this.points=points;Physics.SyncTransforms();
            obstacles=AllInScene<MeshRenderer>(scene).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&
                r.sharedMaterials.All(m=>m!=null&&m.shader.name!="Aerospace/Navigation Source Only"))
                .Select(r=>r.bounds).Where(b=>b.size.y>.65f&&b.size.x<200&&b.size.z<200).ToArray();
            var treeBounds=new List<Bounds>();
            foreach(var t in AllInScene<Terrain>(scene).Where(t=>t.isActiveAndEnabled&&t.drawTreesAndFoliage))
            {
                var prototypes=t.terrainData.treePrototypes;
                var shapes=prototypes.Select(p=>p.prefab==null?new Bounds():RouteLocalBounds(p.prefab.transform,p.prefab.GetComponentsInChildren<Renderer>(true))).ToArray();
                var groundCover=prototypes.Select(p=> {
                    if(p.prefab==null)return false;
                    var meshes=p.prefab.GetComponentsInChildren<MeshFilter>(true);
                    return meshes.Length>0&&meshes.All(f=>f.name.IndexOf("Grass",StringComparison.OrdinalIgnoreCase)>=0)&&p.prefab.GetComponentsInChildren<Collider>(true).Length==0;
                }).ToArray();
                foreach(var tree in t.terrainData.treeInstances)
                {
                    var shape=shapes[tree.prototypeIndex];var prefab=prototypes[tree.prototypeIndex].prefab;
                    var scale=Vector3.Scale(new Vector3(tree.widthScale,tree.heightScale,tree.widthScale),prefab.transform.localScale);
                    var pos=t.transform.position+Vector3.Scale(tree.position,t.terrainData.size);
                    var matrix=Matrix4x4.TRS(pos,Quaternion.AngleAxis(tree.rotation*Mathf.Rad2Deg,Vector3.up)*prefab.transform.localRotation,scale);
                    var b=new Bounds(matrix.MultiplyPoint3x4(shape.min),Vector3.zero);
                    for(int c=0;c<8;c++)b.Encapsulate(matrix.MultiplyPoint3x4(shape.center+Vector3.Scale(shape.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))));
                    // Terrain 'trees' also contain low grass. Use their real prototype
                    // dimensions instead of reserving a tree-sized exclusion for every tuft.
                    if(!groundCover[tree.prototypeIndex]&&b.size.y>.65f)treeBounds.Add(b);
                }
            }
            trees=treeBounds.ToArray();
        }
        public void Place(Transform item,Vector2 desired)
        {
            TryGetBounds(item.gameObject,out var b);float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
            int blockedRoute=0,blockedSlope=0,blockedTree=0,blockedMesh=0,blockedCollider=0;
            for(int ring=0;ring<=14;ring++)for(int n=0;n<(ring==0?1:32);n++)
            {
                float a=n*Mathf.PI*2/32;var p=desired+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*ring*2;
                if(!SurveyPositionClear(p,radius,paths,points)||placed.Any(v=>DistanceToBoundsXZ(p,v)<radius+2)){blockedRoute++;continue;}
                float min=BaseRouteFloorHeight(p.x,p.y),max=min;
                foreach(var offset in new[]{new Vector2(radius*.65f,0),new Vector2(-radius*.65f,0),new Vector2(0,radius*.65f),new Vector2(0,-radius*.65f)})
                {float h=BaseRouteFloorHeight(p.x+offset.x,p.y+offset.y);min=Mathf.Min(min,h);max=Mathf.Max(max,h);}
                if(max-min>1.15f){blockedSlope++;continue;}
                if(trees.Any(t=>t.max.y>max+.65f&&t.min.y<max+b.size.y+1&&DistanceToBoundsXZ(p,t)<radius+.6f)){blockedTree++;continue;}
                if(obstacles.Any(v=>v.max.y>max+.65f&&v.min.y<max+b.size.y+2&&DistanceToBoundsXZ(p,v)<radius+.6f)){blockedMesh++;continue;}
                if(Physics.OverlapBox(new Vector3(p.x,max+b.size.y*.5f+.5f,p.y),new Vector3(b.extents.x,b.extents.y,b.extents.z),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>!(c is TerrainCollider))){blockedCollider++;continue;}
                item.position=new Vector3(p.x,max+.025f,p.y);TryGetBounds(item.gameObject,out var placedBounds);
                item.position+=Vector3.up*(max+.025f-placedBounds.min.y);TryGetBounds(item.gameObject,out placedBounds);placed.Add(placedBounds);
                Debug.Log("RESEARCH_ACCENT "+item.name+" at "+item.position+" radius="+radius);return;
            }
            throw new InvalidOperationException("No safe position near the requested location for "+item.name+" radius="+radius+" route="+blockedRoute+" slope="+blockedSlope+" tree="+blockedTree+" mesh="+blockedMesh+" collider="+blockedCollider);
        }
        public void AssertClear(Transform item)=>ValidateSurveyFootprint(item.gameObject,paths,points);
    }

    public static void ValidateSceneResearchAccents()
    {
        var scene=SceneManager.GetActiveScene();var root=FindByName(scene,SceneAccentRoot);
        if(root==null||root.childCount!=3)throw new InvalidOperationException("Expected three requested research areas.");
        ValidateInvestigationVisualRoot(root.gameObject);AssertNoInvestigationNavigationSources(scene,root);
        var paths=AccentProtectedPaths(scene);var points=AccentProtectedPoints(scene);
        foreach(Transform group in root)
        {
            if(group.childCount!=4)throw new InvalidOperationException("Expected four distinct research assemblies per area.");
            foreach(Transform assembly in group)ValidateSurveyFootprint(assembly.gameObject,paths,points);
        }
    }
    public static void RenderSceneResearchAccents()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);RenderResearchAccentViews(scene);
    }
    static void RenderResearchAccentViews(Scene scene)
    {
        Directory.CreateDirectory(SceneAccentReports);ShaderUtil.allowAsyncCompilation=false;
        var cycle=FindDayNight(scene);var sky=new Material(cycle.skyboxVersion2);
        var lighting=AerospaceLightingState.Capture(cycle.sunLight);float originalPhase=cycle.CurrentPhase;
        var fill=cycle.secondaryDirectionalLights.Where(l=>l!=null).Select(AerospaceFillLightState.Capture).ToArray();
        var focuses=new[]{new Vector3(51,1,-41),new Vector3(12,1,218),new Vector3(20,1,-113)};
        var root=FindByName(scene,SceneAccentRoot);
        bool active=root!=null&&root.gameObject.activeSelf;
        try
        {
            if(root!=null)root.gameObject.SetActive(true);
            foreach(float phase in new[]{0f,.5f})
            {
                cycle.ApplyPhase(phase,sky);
                for(int i=0;i<focuses.Length;i++)
                {
                    string path=SceneAccentReports+"/area-"+(i+1)+(phase==0?"-day":"-night")+".png";
                    RenderActualGameView(scene,path,focuses[i]);RenderActualGameView(scene,path,focuses[i]);
                    if(phase==0&&root!=null)
                    {
                        root.gameObject.SetActive(false);RenderActualGameView(scene,SceneAccentReports+"/area-"+(i+1)+"-before.png",focuses[i]);root.gameObject.SetActive(true);
                    }
                }
            }
        }
        finally
        {
            if(root!=null)root.gameObject.SetActive(active);
            cycle.ApplyPhase(originalPhase,sky);lighting.Restore(cycle.sunLight);
            foreach(var state in fill)state.Restore();Object.DestroyImmediate(sky);
        }
    }
}

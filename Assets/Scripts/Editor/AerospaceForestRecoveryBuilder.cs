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
using ExtractionLike.Environment;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    public const string ForestRoot = "Aerospace_Forest_Recovery_Station";
    public const string ForestAssets = "Assets/Art/Environment/ForestRecoveryStation";
    public const string ForestReports = "UserSettings/ScenePreviews/ForestRecoveryStation";
    [Serializable] public class ForestProbeRow { public string name; public Vector3 position, size; public string source; public bool collider; }
    [Serializable] public class ForestProbeData { public ForestProbeRow[] meshes, foliage, terrains; public Vector3[] points; public ForestLine[] routes; }
    [Serializable] public class ForestLine { public Vector3[] corners; }
    [Serializable] public class ForestDelivery
    {
        public string navigationHash, gameplayHash, originalBuildSources, finalBuildSources;
        public int agents, colliders, pathProbes, completePaths, removedGrassInstances, hiddenRenderers;
        public AccentLocation[] placements;
    }
    static readonly string[] ForestModels={"EngineInspectionRig","FieldPowerModule","RecoveryMarker","TrackingTerminal"};
    static readonly Vector4[] ForestClearings={new Vector4(29,-22,15,14),new Vector4(44,-40,19,10),new Vector4(12,52,4,4),new Vector4(9,-27,4,4)};

    [MenuItem("Tools/Extraction-like/Aerospace/Forest Recovery/Install Station")]
    public static void BuildForestRecovery()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Save and exit Play mode first.");
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        if(FindByName(scene,ForestRoot)!=null)throw new InvalidOperationException("Station already installed; preserve manual edits.");
        Directory.CreateDirectory(ForestReports);
        EnsureSceneBackup("UserSettings/SceneBackups/Before_ForestRecovery_20261019.unity");
        var before=CaptureInvestigationNavigation(scene);
        string gameplay=RouteGameplaySignature(scene),sources=ForestBuildSources(scene,ForestReports+"/build-sources-before.txt");
        var transforms=AllInScene<Transform>(scene).ToDictionary(t=>t,RouteTransformSignature);
        int colliders=AllInScene<Collider>(scene).Length;
        BuildForestPrefabs();
        var go=new GameObject(ForestRoot);SceneManager.MoveGameObjectToScene(go,scene);
        var station=go.AddComponent<AerospaceForestStation>();station.cycle=FindDayNight(scene);
        var modifier=go.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
        var placements=new List<AccentLocation>();
        var hidden=new List<AerospaceForestStation.RendererDisplay>();
        try
        {
            foreach(var r in AllInScene<MeshRenderer>(scene))
            {
                string path=GetPath(r.transform);
                bool replaced=path.Contains("01_StoneArch_Telemetry/Arch_Tracking_Dish/")||path.Contains("01_StoneArch_Telemetry/Arch_Solar_Power/")||path.Contains("01_StoneArch_Telemetry/Arch_Engine_Survey/");
                bool foliage=(path.StartsWith("item/Trees/",StringComparison.Ordinal)||path.Contains("SM_Env_CropField_Clump_02/")||path=="item/SM_Env_Background_Hill_01 (3)")&&InsideForestClearing(r.bounds.center,1.1f);
                if(!replaced&&!foliage)continue;
                if(r.GetComponentInParent<Collider>()!=null||r.GetComponentsInChildren<Collider>(true).Length>0)continue;
                hidden.Add(new AerospaceForestStation.RendererDisplay{renderer=r,originalEnabled=r.enabled,originalMaterials=r.sharedMaterials,hiddenMaterials=Enumerable.Repeat(RouteNavigationOnlyMaterial(),r.sharedMaterials.Length).ToArray()});
            }
            station.hiddenFoliage=hidden.ToArray();
            ConfigureForestGroundCover(scene,station);station.ApplyDisplay();
            var paths=AccentProtectedPaths(scene);var points=AccentProtectedPoints(scene);
            var equipment=CreateSection(go.transform,"01_回收发动机检测工位");
            var comms=CreateSection(go.transform,"02_遥测供电工位");
            var shoulders=CreateSection(go.transform,"03_路肩回收区标识");
            ForestPlace(equipment,"Recovered_Engine_Inspection","EngineInspectionRig",29,-22,18,145,placements);
            ForestPlace(comms,"Tracking_Antenna","TrackingTerminal",41,-40,8.0f,0,placements);
            ForestPlace(comms,"Solar_Power_And_Battery","FieldPowerModule",51,-40,8.5f,0,placements);
            ForestPlace(shoulders,"North_Recovery_Marker","RecoveryMarker",12,52,4.4f,0,placements);
            ForestPlace(shoulders,"South_Recovery_Marker","RecoveryMarker",9,-27,4.4f,-15,placements);
            CreateForestPad(equipment,"Engine_Work_Apron",new Vector2(29,-22),new Vector2(24,24));
            CreateForestPad(comms,"Telemetry_Work_Apron",new Vector2(44,-39.5f),new Vector2(26,12));
            var cables=CreateSection(go.transform,"04_路外线缆与检修盖板");
            ForestCable(cables,new[]{new Vector2(36,-29),new Vector2(36,-32),new Vector2(47,-32),new Vector2(47,-40)});
            ForestCable(cables,new[]{new Vector2(41,-43),new Vector2(47,-43),new Vector2(50,-43)});
            ForestServiceCover(cables,new Vector2(12,30));
            ForestServiceCover(cables,new Vector2(9,5));
            ForestServiceCover(cables,new Vector2(6,-14));
            ConfigureForestMotion(station);
            var lights=new List<Light>();
            foreach(var p in new[]{new Vector3(23,8,-17),new Vector3(37,8,-17),new Vector3(50,6,-36)})
            {
                var lamp=CreateSection(go.transform,"工作照明_仅夜间局部照亮");lamp.position=new Vector3(p.x,BaseRouteFloorHeight(p.x,p.z)+p.y,p.z);
                var light=lamp.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.color=new Color(.80f,.91f,1);light.range=18;light.spotAngle=74;light.innerSpotAngle=40;
                light.shadows=LightShadows.None;light.renderMode=LightRenderMode.Auto;
                lamp.rotation=Quaternion.LookRotation(new Vector3(p.x,0,p.z-5)-lamp.position,Vector3.up);lights.Add(light);
            }
            station.workLights=lights.ToArray();station.ApplyVisuals(0,0);
            SetSurveyVisualOnly(go);ValidateInvestigationVisualRoot(go);AssertNoInvestigationNavigationSources(scene,go.transform);
            foreach(var group in new[]{equipment,comms,shoulders,cables})
                foreach(Transform item in group)ValidateSurveyFootprint(item.gameObject,paths,points);
            ValidateForestWalkableFootprints();
            foreach(var p in transforms)if(p.Key==null||RouteTransformSignature(p.Key)!=p.Value)throw new InvalidOperationException("An existing transform changed: "+p.Key);
            AssertInvestigationSnapshotEqual(before,CaptureInvestigationNavigation(scene));
            string afterSources=ForestBuildSources(scene,ForestReports+"/build-sources-after.txt");
            if(sources!=afterSources)throw new InvalidOperationException("Navigation build sources changed.");
            if(RouteGameplaySignature(scene)!=gameplay||AllInScene<Collider>(scene).Length!=colliders)throw new InvalidOperationException("Gameplay or collision changed.");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            File.WriteAllText(ForestReports+"/installation.json",JsonUtility.ToJson(new ForestDelivery{navigationHash=before.GeometryHash,gameplayHash=gameplay,originalBuildSources=sources,finalBuildSources=afterSources,agents=before.Agents,colliders=colliders,pathProbes=before.Probes,completePaths=before.Complete,hiddenRenderers=hidden.Count,removedGrassInstances=station.terrainDisplays.Sum(b=>b.removedInstanceIndices.Length),placements=placements.ToArray()},true));
            Debug.Log("FOREST_STATION_INSTALLED "+JsonUtility.ToJson(new ForestDelivery{agents=before.Agents,colliders=colliders,pathProbes=before.Probes,completePaths=before.Complete,hiddenRenderers=hidden.Count,removedGrassInstances=station.terrainDisplays.Sum(b=>b.removedInstanceIndices.Length)}));
            RenderForestViews(scene);
        }
        catch{Object.DestroyImmediate(go);throw;}
    }

    static bool InsideForestClearing(Vector3 p,float expand=1)
    {
        return ForestClearings.Any(c=>Mathf.Pow((p.x-c.x)/(c.z*expand),2)+Mathf.Pow((p.z-c.y)/(c.w*expand),2)<1);
    }
    static void ConfigureForestGroundCover(Scene scene,AerospaceForestStation station)
    {
        EnsureAssetFolder(ForestAssets+"/TerrainDisplay");
        var profile=AllInScene<AerospaceLightingVegetationProfile>(scene).Single();
        var bindings=new List<AerospaceForestStation.TerrainDisplay>();
        foreach(var b in profile.Terrains)
        {
            var trees=b.styled.treeInstances;var protos=b.styled.treePrototypes;
            var safe=protos.Select(p=>p.prefab!=null&&p.prefab.GetComponentsInChildren<Collider>(true).Length==0&&p.prefab.GetComponentsInChildren<MeshFilter>(true).All(f=>f.name.IndexOf("Grass",StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
            var removed=Enumerable.Range(0,trees.Length).Where(i=>safe[trees[i].prototypeIndex]&&InsideForestClearing(b.terrain.transform.position+Vector3.Scale(trees[i].position,b.styled.size))).ToArray();
            if(removed.Length==0)continue;
            var indices=new HashSet<int>(removed);
            TerrainData Copy(TerrainData source,string label)
            {
                // Copy the complete serialized asset, including embedded alphamap textures.
                // Instantiate/CopySerialized of TerrainData alone loses its texture subassets.
                string path=ForestAssets+"/TerrainDisplay/ForestRecovery_"+label+".asset";
                if(File.Exists(path)){File.Copy(AssetDatabase.GetAssetPath(source),path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
                else if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path))throw new IOException("Cannot copy display terrain.");
                var clone=AssetDatabase.LoadAssetAtPath<TerrainData>(path);clone.name="ForestRecovery_"+label;
                clone.treeInstances=source.treeInstances.Where((t,i)=>!indices.Contains(i)).ToArray();
                clone.SetBaseMapDirty();EditorUtility.SetDirty(clone);
                return clone;
            }
            bindings.Add(new AerospaceForestStation.TerrainDisplay{terrain=b.terrain,sourceOriginal=b.original,sourceStyled=b.styled,displayOriginal=Copy(b.original,"OriginalPalette"),displayStyled=Copy(b.styled,"StyledPalette"),removedInstanceIndices=removed});
        }
        station.terrainDisplays=bindings.ToArray();
    }
    static Material ForestMaterial(string key)
    {
        string path=ForestAssets+"/Materials/M_Forest_"+key+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        EnsureAssetFolder(ForestAssets+"/Materials");
        Color color;float metallic=.15f,smooth=.35f;
        switch(key)
        {
            case "Graphite":color=new Color(.035f,.053f,.065f);metallic=.45f;break;
            case "Alloy":color=new Color(.35f,.46f,.51f);metallic=.78f;smooth=.48f;break;
            case "Orange":color=new Color(.90f,.235f,.045f);break;
            case "Signal":color=new Color(.09f,.64f,.78f);break;
            case "Solar":color=new Color(.018f,.065f,.12f);metallic=.45f;break;
            case "Nozzle":color=new Color(.37f,.42f,.45f);metallic=.45f;smooth=.30f;break;
            case "Copper":color=new Color(.48f,.26f,.12f);metallic=.45f;break;
            case "Ground":color=new Color(.20f,.235f,.225f);metallic=0;smooth=.06f;break;
            default:color=new Color(.70f,.77f,.79f);break;
        }
        bool created=m==null;if(created)m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Forest_"+key,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
        if(key=="Signal"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.65f);}
        if(created)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);return m;
    }
    static void BuildForestPrefabs()
    {
        EnsureAssetFolder(ForestAssets+"/Prefabs");
        foreach(var name in ForestModels)
        {
            var root=new GameObject(name);var mod=root.AddComponent<NavMeshModifier>();mod.ignoreFromBuild=true;mod.applyToChildren=true;
            var lods=new LOD[3];
            for(int i=0;i<3;i++)
            {
                string path=ForestAssets+"/Models/"+name+"_LOD"+i+".fbx";
                var imp=AssetImporter.GetAtPath(path) as ModelImporter;if(imp==null)throw new FileNotFoundException(path);
                imp.materialImportMode=ModelImporterMaterialImportMode.None;imp.importAnimation=false;imp.importLights=false;imp.importCameras=false;imp.addCollider=false;imp.importNormals=ModelImporterNormals.Import;imp.isReadable=true;imp.SaveAndReimport();
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);model.name="LOD"+i;
                PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var renderers=model.GetComponentsInChildren<MeshRenderer>();
                foreach(var r in renderers)
                {
                    string key=new[]{"Ceramic","Graphite","Alloy","Orange","Signal","Solar","Nozzle","Copper"}.Single(k=>r.name.Contains("Forest_"+k));
                    r.sharedMaterials=Enumerable.Repeat(ForestMaterial(key),r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode=ShadowCastingMode.On;
                }
                // Group articulated meshes without altering imported geometry or vertex data.
                foreach(string part in new[]{"Scanner","Azimuth"})
                {
                    var moving=renderers.Where(r=>r.name.Contains("_"+part+"_")).ToArray();if(moving.Length==0)continue;
                    var pivot=CreateSection(model.transform,part+"_Pivot");
                    foreach(var r in moving)r.transform.SetParent(pivot,true);
                }
                lods[i]=new LOD(i==0?.20f:i==1?.08f:.006f,renderers);
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();SetSurveyVisualOnly(root);
            PrefabUtility.SaveAsPrefabAsset(root,ForestAssets+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);
        }
    }
    static void ForestPlace(Transform parent,string label,string model,float x,float z,float span,float yaw,List<AccentLocation> placements)
    {
        var assembly=CreateSection(parent,label);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ForestAssets+"/Prefabs/"+model+".prefab");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,assembly);
        var bounds=RouteLocalBounds(instance.transform,instance.GetComponentsInChildren<Renderer>(true));
        float scale=span/Mathf.Max(bounds.size.x,bounds.size.z);instance.transform.localScale=Vector3.one*scale;
        instance.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
        assembly.rotation=Quaternion.Euler(0,yaw,0);assembly.position=new Vector3(x,BaseRouteFloorHeight(x,z)+.12f,z);
        TryGetBounds(assembly.gameObject,out var b);
        float floor=BaseRouteFloorHeight(x,z);
        foreach(var v in new[]{new Vector2(b.min.x,b.min.z),new Vector2(b.max.x,b.min.z),new Vector2(b.min.x,b.max.z),new Vector2(b.max.x,b.max.z)})floor=Mathf.Max(floor,BaseRouteFloorHeight(v.x,v.y));
        assembly.position+=Vector3.up*(floor+.16f-b.min.y);TryGetBounds(assembly.gameObject,out b);
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            var foot=assembly.TransformPoint(new Vector3(sx*bounds.size.x*scale*.42f,0,sz*bounds.size.z*scale*.42f));
            float px=foot.x,pz=foot.z,ground=BaseRouteFloorHeight(px,pz),h=b.min.y+.50f-ground;
            if(h>.04f)SurveyCube(assembly,"Terrain_Leveling_Support",assembly.InverseTransformPoint(new Vector3(px,ground+h*.5f,pz)),new Vector3(.40f,h,.40f),ForestMaterial("Graphite"));
        }
        placements.Add(new AccentLocation{path=GetPath(assembly),position=assembly.position,size=b.size});
    }
    static void CreateForestPad(Transform parent,string name,Vector2 center,Vector2 size)
    {
        var pad=CreateSection(parent,name);EnsureAssetFolder(ForestAssets+"/Meshes");
        // Dense enough to follow the uneven terrain without small grass-coloured intersections.
        const int steps=48;var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int z=0;z<=steps;z++)for(int x=0;x<=steps;x++)
        {
            float px=center.x+(x/(float)steps-.5f)*size.x,pz=center.y+(z/(float)steps-.5f)*size.y;
            vertices.Add(new Vector3(px,BaseRouteFloorHeight(px,pz)+.08f,pz));
        }
        for(int z=0;z<steps;z++)for(int x=0;x<steps;x++)
        {
            int i=z*(steps+1)+x;triangles.AddRange(new[]{i,i+steps+1,i+1,i+1,i+steps+1,i+steps+2});
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=ForestAssets+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
        pad.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=pad.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=ForestMaterial("Ground");r.shadowCastingMode=ShadowCastingMode.Off;
        // Only short corner marks on the work apron, never blue lines along the road.
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            float x=center.x+sx*(size.x/2-.8f),z=center.y+sz*(size.y/2-.8f),y=BaseRouteFloorHeight(x,z)+.10f;
            SurveyCube(pad,"Work_Area_Corner",new Vector3(x,y,z),new Vector3(1.5f,.04f,.20f),ForestMaterial("Orange"));
            SurveyCube(pad,"Work_Area_Corner",new Vector3(x,y,z-sz*.65f),new Vector3(.20f,.04f,1.5f),ForestMaterial("Orange"));
        }
    }
    static void ForestCable(Transform parent,Vector2[] points)
    {
        for(int i=1;i<points.Length;i++)
        {
            var delta=points[i]-points[i-1];int count=Mathf.CeilToInt(delta.magnitude/1.5f);
            for(int j=0;j<count;j++)
            {
                var p=Vector2.Lerp(points[i-1],points[i],(j+.5f)/count);var segment=CreateSection(parent,"Cable_Tray_Protected_Shoulder");
                segment.position=new Vector3(p.x,BaseRouteFloorHeight(p.x,p.y)+.12f,p.y);segment.rotation=Quaternion.Euler(0,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg,0);
                SurveyCube(segment,"Sealed_cable_tray",Vector3.zero,new Vector3(.48f,.18f,delta.magnitude/count+.04f),ForestMaterial("Graphite"));
                SurveyCube(segment,"Retaining_clamp",new Vector3(0,.11f,0),new Vector3(.56f,.045f,.13f),ForestMaterial("Alloy"));
            }
        }
    }
    static void ForestServiceCover(Transform parent,Vector2 p)
    {
        var cover=CreateSection(parent,"路肩检修盖板");cover.position=new Vector3(p.x,BaseRouteFloorHeight(p.x,p.y)+.08f,p.y);
        SurveyCube(cover,"Recessed_Hatch",Vector3.zero,new Vector3(1.8f,.14f,2.4f),ForestMaterial("Alloy"));
        SurveyCube(cover,"Inset",new Vector3(0,.08f,0),new Vector3(1.5f,.02f,2.1f),ForestMaterial("Graphite"));
        SurveyCube(cover,"Handle",new Vector3(0,.13f,0),new Vector3(.55f,.08f,.15f),ForestMaterial("Ceramic"));
        SurveyCube(cover,"Edge_ID",new Vector3(.60f,.11f,0),new Vector3(.12f,.02f,1.6f),ForestMaterial("Orange"));
    }
    static void ConfigureForestMotion(AerospaceForestStation station)
    {
        station.motions=station.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Scanner_Pivot"||t.name=="Azimuth_Pivot").Select(t=>new AerospaceForestStation.Motion{target=t,originPosition=t.localPosition,originRotation=t.localRotation,axis=Vector3.up,distance=t.name=="Scanner_Pivot"?.25f:0,degrees=t.name=="Azimuth_Pivot"?7:0,period=t.name=="Azimuth_Pivot"?28:14}).ToArray();
        station.indicators=station.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.Contains("_Forest_Signal")).Select((r,i)=>new AerospaceForestStation.Indicator{renderer=r,offset=i*.4f}).ToArray();
    }
    public static string ForestBuildSources(Scene scene,string output=null)
    {
        var method=typeof(NavMeshSurface).GetMethod("CollectSources",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var list=new List<string>();var raw=new List<string>();
        foreach(var s in AllInScene<NavMeshSurface>(scene))
            foreach(var b in (List<NavMeshBuildSource>)method.Invoke(s,null))
            {
                Object source=b.sourceObject;string prefix=b.shape+"|"+b.area+"|"+b.size.ToString("F5")+"|"+b.transform.ToString("F5")+"|";
                string suffix="|"+(b.component==null?"null":GlobalObjectId.GetGlobalObjectIdSlow(b.component).ToString());
                raw.Add(prefix+(source==null?"null":GlobalObjectId.GetGlobalObjectIdSlow(source).ToString())+suffix);
                // Display terrain differs solely by non-colliding grass instances. Height,
                // holes, all colliding tree prototypes/instances and detail layers are validated separately.
                foreach(var station in AllInScene<AerospaceForestStation>(scene))foreach(var binding in station.terrainDisplays)
                {if(source==binding.displayStyled)source=binding.sourceStyled;else if(source==binding.displayOriginal)source=binding.sourceOriginal;}
                list.Add(prefix+(source==null?"null":GlobalObjectId.GetGlobalObjectIdSlow(source).ToString())+suffix);
            }
        list.Sort(StringComparer.Ordinal);raw.Sort(StringComparer.Ordinal);if(output!=null)File.WriteAllText(output,string.Join("\n",raw));
        using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",list))));
    }
    public static void RenderForestRecovery()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);RenderForestViews(scene);
    }
    public static void ValidateForestWalkableFootprints()
    {
        var scene=SceneManager.GetActiveScene();var station=AllInScene<AerospaceForestStation>(scene).Single();
        int checkedPoints=0;var failures=new List<string>();
        foreach(var lod in station.GetComponentsInChildren<LODGroup>(true))
        {
            if(!TryGetBounds(lod.gameObject,out var b))continue;
            for(float x=b.min.x+.3f;x<b.max.x;x+=1)for(float z=b.min.z+.3f;z<b.max.z;z+=1)
            {
                checkedPoints++;
                if(NavMesh.SamplePosition(new Vector3(x,BaseRouteFloorHeight(x,z),z),out var hit,.30f,NavMesh.AllAreas))
                    failures.Add(lod.name+" at "+hit.position);
            }
        }
        Debug.Log("FOREST_WALKABLE_FOOTPRINT_PROBES="+checkedPoints+" overlaps="+failures.Count);
        if(failures.Count>0)throw new InvalidOperationException("Scenery enters walkable ground: "+string.Join("; ",failures.Take(8)));
    }
    static void RenderForestViews(Scene scene)
    {
        Directory.CreateDirectory(ForestReports);ShaderUtil.allowAsyncCompilation=false;
        var station=AllInScene<AerospaceForestStation>(scene).Single();var cycle=station.cycle;
        var sky=new Material(cycle.skyboxVersion2);var saved=AerospaceLightingState.Capture(cycle.sunLight);float original=cycle.CurrentPhase;
        bool active=station.gameObject.activeSelf;
        try
        {
            foreach(float phase in new[]{0f,.5f})
            {
                station.gameObject.SetActive(true);station.ApplyDisplay();cycle.ApplyPhase(phase,sky);station.ApplyVisuals(0,phase);
                foreach(var focus in new[]{new Vector3(25,1,12),new Vector3(30,1,-30),new Vector3(4,1,52)})
                {
                    string name=focus.z==12?"approach":focus.z==-30?"station":"north";
                    string path=ForestReports+"/"+name+(phase==0?"-day":"-night")+".png";
                    RenderActualGameView(scene,path,focus);RenderActualGameView(scene,path,focus);
                }
                RenderPaletteView(ForestReports+(phase==0?"/overview-day.png":"/overview-night.png"),new Vector3(35,160,-85),new Vector3(35,0,25),49);
            }
            cycle.ApplyPhase(0,sky);station.ApplyVisuals(0,0);
            RenderPaletteView(ForestReports+"/engine-detail.png",new Vector3(12,24,-51),new Vector3(29,5,-22),44);
            foreach(float sec in new[]{0f,7f}){station.ApplyVisuals(sec,0);RenderPaletteView(ForestReports+"/motion-"+sec+".png",new Vector3(25,16,-59),new Vector3(38,4,-40),44);}
            station.gameObject.SetActive(false);RenderActualGameView(scene,ForestReports+"/comparison-before.png",new Vector3(30,1,-30));
        }
        finally{station.gameObject.SetActive(active);cycle.ApplyPhase(original,sky);station.ApplyVisuals(0,original);saved.Restore(cycle.sunLight);Object.DestroyImmediate(sky);}
    }
    public static void ProbeForestRecovery()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Directory.CreateDirectory(ForestReports);
        var rows=new List<ForestProbeRow>();var grounds=new List<ForestProbeRow>();
        foreach(var terrain in AllInScene<Terrain>(scene))
        {
            var data=terrain.terrainData;
            grounds.Add(new ForestProbeRow{name=GetPath(terrain.transform),position=terrain.transform.position,size=data.size,source=AssetDatabase.GetAssetPath(data)});
            foreach(var tree in data.treeInstances)
            {
                var p=terrain.transform.position+Vector3.Scale(tree.position,data.size);
                if(p.x < -30 || p.x > 120 || p.z < -65 || p.z > 140)continue;
                var prefab=data.treePrototypes[tree.prototypeIndex].prefab;
                var b=RouteLocalBounds(prefab.transform,prefab.GetComponentsInChildren<Renderer>(true));
                rows.Add(new ForestProbeRow{name=prefab.name,position=p,size=Vector3.Scale(b.size,new Vector3(tree.widthScale,tree.heightScale,tree.widthScale)),source=AssetDatabase.GetAssetPath(prefab),collider=prefab.GetComponentsInChildren<Collider>(true).Length>0});
            }
        }
        var meshes=AllInScene<MeshRenderer>(scene).Where(r=>r.gameObject.activeInHierarchy&&r.enabled&&r.bounds.center.x > -30&&r.bounds.center.x < 120&&r.bounds.center.z > -65&&r.bounds.center.z<140&&r.bounds.size.x<140&&r.bounds.size.z<140)
            .Select(r=>new ForestProbeRow{name=GetPath(r.transform),position=r.bounds.center,size=r.bounds.size,collider=r.GetComponent<Collider>()!=null}).ToArray();
        File.WriteAllText(ForestReports+"/probe.json",JsonUtility.ToJson(new ForestProbeData{meshes=meshes,foliage=rows.ToArray(),terrains=grounds.ToArray(),points=AccentProtectedPoints(scene).ToArray(),routes=AccentProtectedPaths(scene).Select(p=>new ForestLine{corners=p}).ToArray()},true));
        ShaderUtil.allowAsyncCompilation=false;
        var cycle=FindDayNight(scene);var sky=new Material(cycle.skyboxVersion2);cycle.ApplyPhase(0,sky);
        RenderActualGameView(scene,ForestReports+"/before-game.png",new Vector3(25,1,12));
        RenderActualGameView(scene,ForestReports+"/before-game.png",new Vector3(25,1,12));
        RenderPaletteView(ForestReports+"/before-overview.png",new Vector3(35,160,-85),new Vector3(35,0,25),49);
        Object.DestroyImmediate(sky);
        Debug.Log("FOREST_PROBE_READY");
    }
}

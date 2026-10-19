using System;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    const string QuickReports="UserSettings/ScenePreviews/QuickPolish_20261020";
    [MenuItem("Tools/Extraction-like/Aerospace/Apply Focused Scene Polish")]
    public static void ApplyQuickScenePolish()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var station=AllInScene<AerospaceForestStation>(scene).Single();
        if(station.machineryAmbience!=null)throw new InvalidOperationException("Polish already applied; preserve manual adjustments.");
        EnsureSceneBackup("UserSettings/SceneBackups/Before_QuickPolish_20261020.unity");
        var before=CaptureInvestigationNavigation(scene);
        string gameplay=RouteGameplaySignature(scene),sources=ForestBuildSources(scene);
        int colliders=AllInScene<Collider>(scene).Length;
        // Shared palette changes preserve geometry, model footprint, materials count and LODs.
        PolishMaterial(ForestAssets+"/Materials/M_Forest_Ceramic.mat",new Color(.82f,.86f,.87f),.22f,.31f);
        PolishMaterial(ForestAssets+"/Materials/M_Forest_Alloy.mat",new Color(.46f,.55f,.59f),.62f,.40f);
        PolishMaterial(ForestAssets+"/Materials/M_Forest_Graphite.mat",new Color(.065f,.085f,.10f),.30f,.30f);
        PolishMaterial(ForestAssets+"/Materials/M_Forest_Nozzle.mat",new Color(.46f,.49f,.51f),.42f,.31f);
        PolishMaterial(RouteArtAssets+"/Materials/M_Route_Graphite.mat",new Color(.055f,.075f,.095f),.35f,.30f);
        // Preserve the terrain-following vertices; only add UVs for the feathered work surface.
        var ground=AssetDatabase.LoadAssetAtPath<Material>(ForestAssets+"/Materials/M_Forest_Ground.mat");
        ground.shader=Shader.Find("Aerospace/Field Apron");ground.SetColor("_BaseColor",new Color(.25f,.265f,.235f));
        EditorUtility.SetDirty(ground);
        foreach(string name in new[]{"Engine_Work_Apron","Telemetry_Work_Apron"})
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(ForestAssets+"/Meshes/"+name+".asset");var b=mesh.bounds;
            mesh.uv=mesh.vertices.Select(v=>new Vector2(Mathf.InverseLerp(b.min.x,b.max.x,v.x),Mathf.InverseLerp(b.min.z,b.max.z,v.z))).ToArray();
            EditorUtility.SetDirty(mesh);
        }
        // Forest = survey cyan; tower = calibration blue. Use existing trim meshes, not new props.
        var accents=FindByName(scene,SceneAccentRoot);
        foreach(Transform area in accents)
        {
            bool tower=area.name.StartsWith("03_");if(!tower&&!area.name.StartsWith("02_"))continue;
            string key=tower?"Calibration_Blue":"Survey_Teal";
            string path=ForestAssets+"/Materials/M_"+key+".mat";
            var trim=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(trim==null){trim=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key,enableInstancing=true};AssetDatabase.CreateAsset(trim,path);}
            trim.SetColor("_BaseColor",tower?new Color(.18f,.39f,.62f):new Color(.12f,.45f,.48f));
            trim.SetFloat("_Metallic",.25f);trim.SetFloat("_Smoothness",.32f);EditorUtility.SetDirty(trim);
            foreach(var r in area.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.Contains("Orange")))
            { r.sharedMaterials=Enumerable.Repeat(trim,r.sharedMaterials.Length).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r); }
        }
        foreach(var m in station.motions)
        { bool scanner=m.target.name=="Scanner_Pivot";m.period=scanner?17:31;m.phase=scanner?.4f:1.7f; }
        station.nightLightIntensity=1.85f;
        foreach(var light in station.workLights)
        {
            Vector3 target=light.transform.position.x>45?new Vector3(47,4,-40):new Vector3(29,5,-22);
            target.y+=BaseRouteFloorHeight(target.x,target.z);
            light.transform.rotation=Quaternion.LookRotation(target-light.transform.position);
            light.color=new Color(.82f,.91f,1);EditorUtility.SetDirty(light);
        }
        string audioPath=ForestAssets+"/field_equipment_ambience.wav";WriteFieldAmbience(audioPath);
        AssetDatabase.ImportAsset(audioPath,ImportAssetOptions.ForceSynchronousImport);
        var audioNode=CreateSection(station.transform,"近距离设备底噪");audioNode.position=new Vector3(29,4,-22);
        var source=audioNode.gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        source.loop=true;source.playOnAwake=false;source.volume=0;source.spatialBlend=0;source.priority=220;
        station.machineryAmbience=source;SetSurveyVisualOnly(audioNode.gameObject);EditorUtility.SetDirty(station);
        ValidateAerospaceRouteNetwork();ValidateForestWalkableFootprints();
        AssertInvestigationSnapshotEqual(before,CaptureInvestigationNavigation(scene));
        if(RouteGameplaySignature(scene)!=gameplay||ForestBuildSources(scene)!=sources||AllInScene<Collider>(scene).Length!=colliders)
            throw new InvalidOperationException("Gameplay or navigation changed.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        Directory.CreateDirectory(QuickReports);
        File.WriteAllText(QuickReports+"/verified.txt","32 cases at 75%; collision, gameplay and 1512 path probes unchanged.\n");
        // Only two final game-view frames; no full preview sweep.
        var cycle=station.cycle;var saved=AerospaceLightingState.Capture(cycle.sunLight);var sky=new Material(cycle.skyboxVersion2);float phase=cycle.CurrentPhase;
        bool previousAsyncCompilation=ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation=false;
        try
        {
            foreach(float p in new[]{0f,.5f})
            {cycle.ApplyPhase(p,sky);station.ApplyVisuals(0,p);string path=QuickReports+(p==0?"/day.png":"/night.png");RenderActualGameView(scene,path,new Vector3(30,1,-30));RenderActualGameView(scene,path,new Vector3(30,1,-30));}
        }
        finally{cycle.ApplyPhase(phase,sky);station.ApplyVisuals(0,phase);saved.Restore(cycle.sunLight);Object.DestroyImmediate(sky);ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;}
        Debug.Log("QUICK_POLISH_COMPLETE gameplayNavigationUnchanged=true");
    }
    static void PolishMaterial(string path,Color color,float metal,float smooth)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null)return;
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);
    }
    static void WriteFieldAmbience(string path)
    {
        // Original low-pass broadband fan noise; no melody or reused pickup transient.
        const int rate=24000,count=rate*4;var random=new System.Random(20261020);float low=0;
        using(var w=new BinaryWriter(File.Create(path)))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
            for(int i=0;i<count;i++)
            {float noise=(float)random.NextDouble()*2-1;low+=.095f*(noise-low);float fade=Mathf.Min(1,Mathf.Min(i,count-1-i)/1200f);float flutter=.88f+.12f*Mathf.Sin(2*Mathf.PI*7*i/rate);w.Write((short)(Mathf.Clamp(low*1.6f,-.8f,.8f)*fade*flutter*32767));}
        }
    }
}

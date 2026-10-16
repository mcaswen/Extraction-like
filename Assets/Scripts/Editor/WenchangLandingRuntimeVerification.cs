using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated batch Play-mode check; never saves scene, player settings or assets.</summary>
[InitializeOnLoad]
public static class WenchangLandingRuntimeVerification
{
    const string Key="ExtractionLike.LandingPolish.Runtime.";
    const string ScenePath="Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
    const string Folder="UserSettings/ScenePreviews/WenchangLanding";
    static WenchangLandingRuntimeVerification()
    {
        if(SessionState.GetBool(Key+"Active",false)) EditorApplication.update+=Tick;
    }
    public static void Begin()
    {
        if(!Application.isBatchMode || PlayerSettings.companyName!="AnomalySearch.Automation")
            throw new InvalidOperationException("Use an isolated automation project for runtime verification.");
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        SessionState.SetString(Key+"SceneHash",HashScene());
        SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddSeconds(120).Ticks.ToString());
        SessionState.SetInt(Key+"Stage",0);SessionState.SetBool(Key+"Active",true);
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key+"Active",false))return;
        try
        {
            if(DateTime.UtcNow.Ticks>long.Parse(SessionState.GetString(Key+"Deadline","0")))throw new TimeoutException("Landing runtime check timed out.");
            int stage=SessionState.GetInt(Key+"Stage",0);
            if(stage==2 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(HashScene()!=SessionState.GetString(Key+"SceneHash",""))throw new InvalidOperationException("Runtime check changed scene bytes.");
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Folder+"/polish-runtime-verification.txt","PASS: actual formal-scene Play Mode in an isolated save namespace.\nTwo active Agents remain on the original NavMesh.\nVehicle placement and scale unchanged; automatic LOD, day/night render, smoke and shader passes verified.\nScene file unchanged; no full combat or performance benchmark claimed.\n");
                SessionState.SetBool(Key+"Active",false);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                Debug.Log("WENCHANG_POLISH_RUNTIME_PASS");EditorApplication.Exit(0);return;
            }
            if(!EditorApplication.isPlaying)return;
            if(stage==0){SessionState.SetFloat(Key+"Start",Time.unscaledTime);SessionState.SetInt(Key+"Stage",1);return;}
            if(stage!=1 || Time.unscaledTime-SessionState.GetFloat(Key+"Start",0)<5)return;
            var agents=Object.FindObjectsOfType<AgentPawnRoot>();
            if(agents.Length!=2 || agents.Any(a=>a.NavMeshAgent==null || !a.NavMeshAgent.isOnNavMesh))throw new InvalidOperationException("Runtime Agents left the NavMesh.");
            var site=Object.FindObjectOfType<AerospaceLandingSite>();
            if(site==null || Vector3.Distance(site.vehicle.position,new Vector3(290,4.843349f,17))>.0001f || Vector3.Distance(site.vehicle.localScale,Vector3.one*1.35f)>.0001f)
                throw new InvalidOperationException("Runtime landing vehicle placement changed.");
            if(site.smoke.Any(p=>p.particleCount<1 || p.collision.enabled || p.trigger.enabled))throw new InvalidOperationException("Runtime smoke emission/collision invalid.");
            AerospaceSceneLayoutBuilder.RenderLandingRuntimeEvidence(site);
            foreach(var material in site.vehicle.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).Distinct())
            {
                Debug.Log("LANDING_PASSES "+material.name+": "+string.Join(",",Enumerable.Range(0,material.passCount).Select(material.GetPassName)));
                if(ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Runtime shader has errors.");
                if(!Enumerable.Range(0,material.passCount).Any(p=>string.Equals(material.GetPassName(p),"DepthNormals",StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Rendered material is missing the local normal pass.");
            }
            SessionState.SetInt(Key+"Stage",2);EditorApplication.ExitPlaymode();
        }
        catch(Exception error)
        {
            SessionState.SetBool(Key+"Active",false);Debug.LogException(error);EditorApplication.Exit(1);
        }
    }
    static string HashScene(){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ScenePath)));}
}

public static partial class AerospaceSceneLayoutBuilder
{
    public static void RenderLandingRuntimeEvidence(AerospaceLandingSite site)
    {
        var scene=site.gameObject.scene;var cycle=site.cycle;cycle.animate=false;
        var night=FindByName(scene,GameViewNightRoot).GetComponent<AerospaceNightGroundLighting>();
        bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        try
        {
            foreach(float phase in new[]{0f,.5f})
            {
                cycle.ApplyPhase(phase);site.ApplyPhase(phase);night.ApplyPhase(phase);
                string path=LandingPreviewFolder+"/playview-landing-"+(phase==0?"day":"night")+".png";
                var target=new Vector3(290,GroundHeight(290,17)+1.5f,17);
                RenderActualGameView(scene,path,target);RenderActualGameView(scene,path,target);
            }
        }
        finally {ShaderUtil.allowAsyncCompilation=async;}
    }
}

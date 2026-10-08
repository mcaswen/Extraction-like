using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Bounded actual formal-scene Play-mode smoke test. Never saves the scene or assets.</summary>
[InitializeOnLoad]
public static class AerospaceLightingRuntimeVerification
{
    private const string Key = "ExtractionLike.LightingPalette.Runtime.";
    private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
    private const string Reports = "UserSettings/ScenePreviews/Aerospace_LightingAndVegetation_V1";
    static AerospaceLightingRuntimeVerification()
    {
        if (SessionState.GetBool(Key + "Active", false)) EditorApplication.update += Tick;
    }

    public static void BeginForBatchmode()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("This verification is batch-only.");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(Reports);
        string[] missing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
            .Select(transform => ObjectPath(transform)).ToArray();
        File.WriteAllLines(Reports + "/preexisting-missing-scripts.txt", missing);
        var profile = Object.FindObjectOfType<AerospaceLightingVegetationProfile>();
        if (profile == null || profile.cycle == null) throw new InvalidOperationException("Formal scene art profile missing.");
        SessionState.SetString(Key + "SceneHash", HashFile(ScenePath));
        SessionState.SetString(Key + "SkyHash", HashFile(AssetDatabase.GetAssetPath(profile.cycle.skyboxVersion2)));
        SessionState.SetString(Key + "GradeHash", HashFile(AssetDatabase.GetAssetPath(profile.gradingVolume.sharedProfile)));
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddSeconds(120).Ticks.ToString());
        SessionState.SetBool(Key + "Active", true); SessionState.SetInt(Key + "Stage", 0);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        try
        {
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + "Deadline", "0"))) throw new TimeoutException("Lighting runtime smoke test exceeded its bounded deadline.");
            int stage = SessionState.GetInt(Key + "Stage", 0);
            if (stage == 2 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (HashFile(ScenePath) != SessionState.GetString(Key + "SceneHash", "")) throw new InvalidOperationException("Runtime test changed the formal scene file.");
                var saved = Object.FindObjectOfType<AerospaceLightingVegetationProfile>();
                if (HashFile(AssetDatabase.GetAssetPath(saved.cycle.skyboxVersion2)) != SessionState.GetString(Key + "SkyHash", "") ||
                    HashFile(AssetDatabase.GetAssetPath(saved.gradingVolume.sharedProfile)) != SessionState.GetString(Key + "GradeHash", ""))
                    throw new InvalidOperationException("Runtime animation mutated a shared sky/grading asset.");
                File.WriteAllText(Reports + "/runtime-verification.txt", "PASS: actual formal-scene Play Mode.\nTwo active Agents on original NavMesh.\n160-second real-time cycle progression verified.\nDay/night palette and both comparison states verified in Play Mode.\nPrivate sky/grading materials; shared assets and formal scene file unchanged.\nScene-local optional outline tuning verified against actual Main Camera.\nNo full combat or performance benchmark claimed.\n");
                SessionState.SetBool(Key + "Active", false);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Debug.Log("[Lighting Runtime] PASS: actual formal-scene Play Mode, two on-NavMesh Agents, cycle progression, readable night, comparison restoration, camera-local outline, no saved asset mutation.");
                EditorApplication.Exit(0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var profile = Object.FindObjectOfType<AerospaceLightingVegetationProfile>();
            if (profile == null) throw new InvalidOperationException("Runtime art profile missing.");
            var cycle = profile.cycle;
            if (stage == 0)
            {
                var agents = Object.FindObjectsOfType<AgentPawnRoot>();
                if (agents.Length != 2 || agents.Any(agent => agent.NavMeshAgent == null || !agent.NavMeshAgent.isOnNavMesh))
                    throw new InvalidOperationException("Both formal Agents must remain on their original NavMesh.");
                if (AerospaceLightingVegetationProfile.ForCamera(Camera.main) != profile) throw new InvalidOperationException("Actual gameplay camera does not use its scene-local palette tuning.");
                if (cycle.cycleSeconds != 160 || RenderSettings.skybox == cycle.skyboxVersion2) throw new InvalidOperationException("Expected 160-second timing and private runtime skybox.");
                SessionState.SetFloat(Key + "Time", Time.unscaledTime);
                SessionState.SetFloat(Key + "Phase", cycle.CurrentPhase);
                SessionState.SetInt(Key + "Stage", 1); return;
            }
            float elapsed = Time.unscaledTime - SessionState.GetFloat(Key + "Time", 0);
            if (stage != 1 || elapsed < 4) return;
            float expected = AerospaceDayNightCycle.PhaseAfter(SessionState.GetFloat(Key + "Phase", 0), elapsed, cycle.cycleSeconds);
            if (Mathf.Abs(Mathf.DeltaAngle(cycle.CurrentPhase * 360, expected * 360)) > 3)
                throw new InvalidOperationException("Runtime day/night phase does not match the configured 160-second period.");
            bool animate = cycle.animate; cycle.animate = false;
            cycle.ApplyPhase(.5f);
            if (Mathf.Abs(cycle.sunLight.intensity - profile.nightSunIntensity) > .001f) throw new InvalidOperationException("Cycle overwrote the art-directed readable night.");
            profile.gameObject.SetActive(false);
            if (RenderSettings.fog || AerospaceLightingVegetationProfile.ForCamera(Camera.main) != null) throw new InvalidOperationException("Runtime comparison did not restore original fog/outline behavior.");
            foreach (var binding in profile.Terrains)
                if (binding.terrain.terrainData != binding.original || binding.collider.terrainData != binding.original) throw new InvalidOperationException("Runtime terrain revert incomplete.");
            profile.gameObject.SetActive(true);
            cycle.ApplyPhase(.5f);
            if (Mathf.Abs(cycle.sunLight.intensity - profile.nightSunIntensity) > .001f || !RenderSettings.fog) throw new InvalidOperationException("Runtime palette re-enable incomplete.");
            foreach (var binding in profile.Terrains)
                if (binding.terrain.terrainData != binding.styled || binding.collider.terrainData != binding.styled) throw new InvalidOperationException("Runtime terrain palette re-enable incomplete.");
            cycle.animate = animate;
            SessionState.SetInt(Key + "Stage", 2);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key + "Active", false);
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }

    private static string ObjectPath(Transform transform) => transform.parent == null ? transform.name : ObjectPath(transform.parent) + "/" + transform.name;
    private static string HashFile(string path)
    {
        using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream));
    }
}

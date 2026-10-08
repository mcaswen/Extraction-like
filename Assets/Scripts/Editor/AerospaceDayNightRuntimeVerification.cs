using System;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Batch-only, isolated Play-mode smoke test. Does not save or alter the formal scene.</summary>
[InitializeOnLoad]
public static class AerospaceDayNightRuntimeVerification
{
    private const string Key = "ExtractionLike.DayNightRuntimeProbe.";
    static AerospaceDayNightRuntimeVerification()
    {
        if (SessionState.GetBool(Key + "Active", false)) EditorApplication.update += Tick;
    }

    public static void BeginForBatchmode()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("This verification is batch-only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Light sun = new GameObject("Probe Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.87f;
        sun.color = new Color(1f, 0.8f, 0.6f);
        sun.transform.rotation = Quaternion.Euler(48f, 23f, 0f);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Skyboxes/Skybox_OrbitalTwilight.mat");
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.26f, 0.3f, 0.35f);
        RenderSettings.sun = sun;
        AerospaceDayNightCycle cycle = new GameObject("Isolated DayNight Probe").AddComponent<AerospaceDayNightCycle>();
        cycle.sunLight = sun;
        cycle.skyboxVersion1 = RenderSettings.skybox;
        cycle.skyboxVersion2 = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Skyboxes/Skybox_AerospaceDayNight_V2.mat");
        if (cycle.skyboxVersion2 == null) throw new InvalidOperationException("Build V2 first.");
        cycle.StoreVersion1Lighting();
        Light secondary = new GameObject("Probe Secondary Fill").AddComponent<Light>();
        secondary.type = LightType.Directional;
        secondary.intensity = 1.2f;
        secondary.color = new Color(1f, 0.5f, 0.2f);
        cycle.RegisterSecondaryLights(new[] { secondary });
        cycle.ApplyPhase(0f);
        SessionState.SetString(Key + "Material", EditorJsonUtility.ToJson(cycle.skyboxVersion2));
        SessionState.SetInt(Key + "Stage", 0);
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetString(Key + "Deadline", DateTime.UtcNow.AddSeconds(60).Ticks.ToString());
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        try
        {
            int stage = SessionState.GetInt(Key + "Stage", 0);
            if (stage == 2 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Key + "Active", false);
                Debug.Log("[DayNight V2 Runtime Verify] PASS: actual Play-mode phase progression, cloned material, no asset mutation, disable/re-enable cleanup, V1 light restoration. Formal scene untouched.");
                EditorApplication.Exit(0);
                return;
            }
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + "Deadline", "0")))
                throw new TimeoutException("DayNight runtime verification timed out.");
            if (!EditorApplication.isPlaying) return;
            AerospaceDayNightCycle cycle = Object.FindObjectOfType<AerospaceDayNightCycle>();
            if (cycle == null) throw new InvalidOperationException("Probe controller missing in Play mode.");
            if (stage == 0)
            {
                if (RenderSettings.skybox == cycle.skyboxVersion2 || RenderSettings.skybox == null)
                    throw new InvalidOperationException("Runtime did not create a private skybox material.");
                SessionState.SetFloat(Key + "Time", Time.unscaledTime);
                SessionState.SetFloat(Key + "Phase", cycle.CurrentPhase);
                SessionState.SetInt(Key + "Stage", 1);
                return;
            }
            if (stage != 1 || Time.unscaledTime - SessionState.GetFloat(Key + "Time", 0f) < 3f) return;
            float elapsed = Time.unscaledTime - SessionState.GetFloat(Key + "Time", 0f);
            float expected = AerospaceDayNightCycle.PhaseAfter(SessionState.GetFloat(Key + "Phase", 0f), elapsed, cycle.cycleSeconds);
            if (Mathf.Abs(Mathf.DeltaAngle(cycle.CurrentPhase * 360f, expected * 360f)) > 1f)
                throw new InvalidOperationException("Actual runtime progression did not match the configured cycle duration.");
            if (EditorJsonUtility.ToJson(cycle.skyboxVersion2) != SessionState.GetString(Key + "Material", ""))
                throw new InvalidOperationException("Runtime mutated the saved/shared V2 material.");
            cycle.enabled = false;
            if (RenderSettings.skybox != cycle.skyboxVersion2 || Mathf.Abs(cycle.sunLight.intensity - cycle.dayLightIntensity) > 0.001f)
                throw new InvalidOperationException("Disable failed to restore the pre-Play V2 lighting.");
            if (Mathf.Abs(cycle.secondaryDirectionalLights[0].intensity - 1.2f * cycle.secondaryDayMultiplier) > 0.001f)
                throw new InvalidOperationException("Disable failed to restore secondary pre-Play lighting.");
            cycle.enabled = true;
            if (RenderSettings.skybox == cycle.skyboxVersion2) throw new InvalidOperationException("Re-enable did not create a fresh private material.");
            cycle.enabled = false;
            cycle.RestoreVersion1();
            if (RenderSettings.skybox != cycle.skyboxVersion1 || RenderSettings.ambientMode != AmbientMode.Flat ||
                Mathf.Abs(cycle.sunLight.intensity - 0.87f) > 0.001f ||
                Quaternion.Angle(cycle.sunLight.transform.rotation, Quaternion.Euler(48f, 23f, 0f)) > 0.001f)
                throw new InvalidOperationException("Runtime V1 restoration failed.");
            if (Mathf.Abs(cycle.secondaryDirectionalLights[0].intensity - 1.2f) > 0.001f ||
                cycle.secondaryDirectionalLights[0].color != new Color(1f, 0.5f, 0.2f))
                throw new InvalidOperationException("Secondary V1 restoration failed.");
            SessionState.SetInt(Key + "Stage", 2);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key + "Active", false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}

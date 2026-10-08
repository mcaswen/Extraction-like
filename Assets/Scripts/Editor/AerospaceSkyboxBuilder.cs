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
    private const string DayNightRoot = "Aerospace_DayNight_Skybox_V2";
    private const string Skybox1Path = "Assets/Art/Environment/Skyboxes/Skybox_OrbitalTwilight.mat";
    private const string Skybox2Path = "Assets/Art/Environment/Skyboxes/Skybox_AerospaceDayNight_V2.mat";
    private const string SkyboxSources = "Assets/Art/External/Skyboxes_PolyHaven_CC0/Textures/";
    private const string DayNightBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_DayNight_Skybox_V2.unity";

    public static void BuildDayNightSkyboxForBatchmode()
    {
        EnsureSceneBackup(DayNightBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        InstallDayNight(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[DayNight V2] Installed online CC0 HDR pair, full cycle=160 seconds. Original V1 preserved. Backup=" + DayNightBackup);
    }

    [MenuItem("Tools/Extraction-like/Skybox/Install V2 In Current Scene")]
    public static void InstallDayNightInCurrentScene()
    {
        RequireEditMode();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("请先打开并保存需要设置天空盒的场景。");
        if (scene.path == ScenePath) EnsureSceneBackup(DayNightBackup);
        InstallDayNight(scene);
        Debug.Log("[DayNight V2] 已设置当前场景，请 Ctrl/Cmd+S 保存。版本切换位于 Tools/Extraction-like/Skybox。");
    }

    private static void InstallDayNight(Scene scene)
    {
        Material material = EnsureDayNightMaterial();
        AerospaceDayNightCycle cycle = FindDayNight(scene);
        if (cycle == null)
        {
            GameObject root = new GameObject(DayNightRoot);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Install Skybox V2");
            cycle = root.AddComponent<AerospaceDayNightCycle>();
            cycle.sunLight = RenderSettings.sun;
            if (cycle.sunLight == null)
                cycle.sunLight = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).FirstOrDefault(l => l.type == LightType.Directional);
            cycle.skyboxVersion1 = RenderSettings.skybox != null ? RenderSettings.skybox : AssetDatabase.LoadAssetAtPath<Material>(Skybox1Path);
            cycle.StoreVersion1Lighting();
        }
        cycle.skyboxVersion2 = material;
        cycle.RegisterSecondaryLights(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
            .Where(l => l.type == LightType.Directional && l != cycle.sunLight).ToArray());
        // Re-installation preserves the saved V1 baseline and the user's adjusted cycle settings.
        cycle.gameObject.SetActive(true);
        cycle.enabled = true;
        cycle.ApplyPhase(cycle.startPhase);
        EditorUtility.SetDirty(cycle);
        EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = cycle.gameObject;
    }

    private static Material EnsureDayNightMaterial()
    {
        AssetDatabase.Refresh();
        Texture2D day = ImportPanorama(SkyboxSources + "kloppenheim_06_puresky_2k.hdr");
        Texture2D night = ImportPanorama(SkyboxSources + "rogland_clear_night_2k.hdr");
        Shader shader = Shader.Find("ExtractionLike/Skybox/Aerospace Day Night Blend");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("天空盒 V2 Shader 未能正确编译，请检查 Console。");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Skybox2Path);
        if (material == null)
        {
            EnsureAssetFolder(Path.GetDirectoryName(Skybox2Path)?.Replace('\\', '/'));
            material = new Material(shader) { name = "Skybox_AerospaceDayNight_V2" };
            AssetDatabase.CreateAsset(material, Skybox2Path);
        }
        material.shader = shader;
        material.SetTexture("_DayTex", day);
        material.SetTexture("_NightTex", night);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D ImportPanorama(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("需要先下载网上天空 HDR 素材", path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("HDR 没有被识别为纹理：" + path);
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    [MenuItem("Tools/Extraction-like/Skybox/Use 1 - Original Bright Sky")]
    public static void UseOriginalSkybox()
    {
        RequireEditMode();
        AerospaceDayNightCycle cycle = RequireDayNight();
        RecordSkyboxUndo(cycle, "Use Skybox 1");
        cycle.enabled = false;
        cycle.RestoreVersion1();
        MarkSkyboxDirty(cycle);
        Debug.Log("[Skybox] 已切回原天空盒及原灯光。请保存场景。");
    }

    [MenuItem("Tools/Extraction-like/Skybox/Use 2 - Day Night Cycle")]
    public static void UseDayNightSkybox()
    {
        PreviewDayNightPhase(RequireDayNight(), RequireDayNight().startPhase);
    }

    [MenuItem("Tools/Extraction-like/Skybox/Preview 2/Day")]
    public static void PreviewSkyboxDay() { PreviewDayNightPhase(RequireDayNight(), 0f); }
    [MenuItem("Tools/Extraction-like/Skybox/Preview 2/Sunset")]
    public static void PreviewSkyboxSunset() { PreviewDayNightPhase(RequireDayNight(), 0.25f); }
    [MenuItem("Tools/Extraction-like/Skybox/Preview 2/Night")]
    public static void PreviewSkyboxNight() { PreviewDayNightPhase(RequireDayNight(), 0.5f); }
    [MenuItem("Tools/Extraction-like/Skybox/Preview 2/Sunrise")]
    public static void PreviewSkyboxSunrise() { PreviewDayNightPhase(RequireDayNight(), 0.75f); }

    public static void PreviewDayNightPhase(AerospaceDayNightCycle cycle, float phase)
    {
        RequireEditMode();
        RecordSkyboxUndo(cycle, "Preview Skybox V2");
        cycle.gameObject.SetActive(true);
        cycle.enabled = true;
        cycle.startPhase = Mathf.Repeat(phase, 1f);
        cycle.RegisterSecondaryLights(cycle.secondaryDirectionalLights);
        cycle.ApplyPhase(cycle.startPhase);
        MarkSkyboxDirty(cycle);
    }

    private static void RecordSkyboxUndo(AerospaceDayNightCycle cycle, string action)
    {
        Undo.RecordObject(cycle, action);
        Undo.RecordObject(cycle.gameObject, action);
        if (cycle.skyboxVersion2 != null) Undo.RecordObject(cycle.skyboxVersion2, action);
        if (cycle.sunLight != null)
        {
            Undo.RecordObject(cycle.sunLight, action);
            Undo.RecordObject(cycle.sunLight.transform, action);
        }
        foreach (Light light in cycle.secondaryDirectionalLights)
            if (light != null) Undo.RecordObject(light, action);
        RenderSettings settings = Resources.FindObjectsOfTypeAll<RenderSettings>().FirstOrDefault();
        if (settings != null) Undo.RecordObject(settings, action);
    }

    private static void MarkSkyboxDirty(AerospaceDayNightCycle cycle)
    {
        EditorUtility.SetDirty(cycle);
        if (cycle.skyboxVersion2 != null) EditorUtility.SetDirty(cycle.skyboxVersion2);
        EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene);
        SceneView.RepaintAll();
        Selection.activeGameObject = cycle.gameObject;
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先退出 Play 模式，再切换并保存天空盒预设。");
    }

    private static AerospaceDayNightCycle FindDayNight(Scene scene)
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AerospaceDayNightCycle>(true)).FirstOrDefault();
    }

    private static AerospaceDayNightCycle RequireDayNight()
    {
        RequireEditMode();
        AerospaceDayNightCycle cycle = FindDayNight(SceneManager.GetActiveScene());
        if (cycle == null) throw new InvalidOperationException("当前场景没有 V2 控制器，请先使用 Install V2 In Current Scene。");
        return cycle;
    }

    public static void VerifyAndRenderDayNightForBatchmode()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AerospaceDayNightCycle cycle = RequireDayNight();
        Material original = AssetDatabase.LoadAssetAtPath<Material>(Skybox1Path);
        if (cycle.skyboxVersion1 != original || cycle.skyboxVersion2 == null || cycle.sunLight == null || cycle.Version1Lighting == null)
            throw new InvalidOperationException("天空盒预设引用或原始灯光记录丢失。");
        if (Mathf.Abs(cycle.cycleSeconds - 160f) > 0.001f) throw new InvalidOperationException("Expected full cycle=160 seconds.");
        for (int quarter = 0; quarter <= 4; quarter++)
        {
            float expected = quarter == 4 ? 0f : quarter * 0.25f;
            if (Mathf.Abs(AerospaceDayNightCycle.PhaseAfter(0f, quarter * 40f, 160f) - expected) > 0.001f)
                throw new InvalidOperationException("Cycle timing calculation failed.");
        }
        string folder = "UserSettings/ScenePreviews/Aerospace_DayNight_V2";
        Directory.CreateDirectory(folder);
        Vector3 camera = new Vector3(322f, 26f, -17f);
        Vector3 target = new Vector3(272f, 18f, 52f);
        string[] labels = { "day", "sunset", "night", "sunrise" };
        for (int quarter = 0; quarter < 4; quarter++)
        {
            cycle.ApplyPhase(quarter * 0.25f);
            if (quarter == 2 && (cycle.sunLight.intensity < 0.3f || RenderSettings.ambientSkyColor.b < 0.4f))
                throw new InvalidOperationException("Night lighting fell below the visibility baseline.");
            RenderCameraPreview(folder + "/skybox-v2-" + labels[quarter] + ".png", camera, target, 63f);
        }
        cycle.RestoreVersion1();
        AssertLightingState(cycle.Version1Lighting, cycle.sunLight);
        foreach (AerospaceFillLightState fill in cycle.Version1FillLights)
            if (fill.light != null && (fill.light.color != fill.color || fill.light.intensity != fill.intensity || fill.light.enabled != fill.enabled))
                throw new InvalidOperationException("V1 secondary light restoration failed.");
        RenderCameraPreview(folder + "/skybox-v1-original.png", camera, target, 63f);
        if (ShaderUtil.ShaderHasError(cycle.skyboxVersion2.shader)) throw new InvalidOperationException("V2 shader error.");
        // Explicit previews only modify the V2 material; runtime clones must not mutate this asset.
        cycle.startPhase = 0f;
        cycle.enabled = true;
        cycle.ApplyPhase(0f);
        EditorUtility.SetDirty(cycle);
        EditorUtility.SetDirty(cycle.skyboxVersion2);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        cycle = RequireDayNight();
        if (!cycle.enabled || cycle.startPhase != 0f || RenderSettings.skybox != cycle.skyboxVersion2 || cycle.Version1Lighting.skybox != original)
            throw new InvalidOperationException("Scene save/reload or original V1 preservation failed.");
        Debug.Log("[DayNight V2 Verify] PASS: 160-second cycle quarters, night visibility, V1 sky/light restoration, shader compilation, scene reload. Previews=" + folder);
    }

    private static void AssertLightingState(AerospaceLightingState state, Light light)
    {
        if (RenderSettings.skybox != state.skybox || RenderSettings.ambientMode != state.ambientMode ||
            RenderSettings.ambientSkyColor != state.skyColor || RenderSettings.ambientEquatorColor != state.equatorColor ||
            RenderSettings.ambientGroundColor != state.groundColor || RenderSettings.ambientIntensity != state.ambientIntensity ||
            RenderSettings.reflectionIntensity != state.reflectionIntensity || RenderSettings.sun != state.environmentSun ||
            Quaternion.Angle(light.transform.rotation, state.lightRotation) > 0.001f || light.color != state.lightColor || light.intensity != state.lightIntensity)
            throw new InvalidOperationException("V1 lighting restoration did not match its saved baseline.");
    }

    public static void InspectSkyHdrForBatchmode()
    {
        foreach (string name in new[] { "kloppenheim_06_puresky_2k", "rogland_clear_night_2k" })
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyboxSources + name + ".hdr");
            RenderTexture rt = RenderTexture.GetTemporary(128, 64, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D pixels = new Texture2D(128, 64, TextureFormat.RGBAFloat, false, true);
            pixels.ReadPixels(new Rect(0, 0, 128, 64), 0, 0);
            pixels.Apply();
            var upperSky = pixels.GetPixels().Skip(128 * 36).Select(c => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f).OrderBy(v => v).ToArray();
            Debug.Log($"[Sky HDR] {name} format={source.graphicsFormat} skyLuminance p10={upperSky[upperSky.Length / 10]:F6} median={upperSky[upperSky.Length / 2]:F6} p90={upperSky[upperSky.Length * 9 / 10]:F6} max={upperSky.Last():F6}");
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(pixels);
        }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (Light light in Object.FindObjectsOfType<Light>())
            if (light.type == LightType.Directional) Debug.Log($"[Sky Directional] {GetPath(light.transform)} intensity={light.intensity} color={light.color} isSun={RenderSettings.sun == light}");
    }
}

[CustomEditor(typeof(AerospaceDayNightCycle))]
public sealed class AerospaceDayNightCycleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        AerospaceDayNightCycle cycle = (AerospaceDayNightCycle)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("完整一轮为 Cycle Seconds 秒。0=白天，0.25=傍晚，0.5=夜晚，0.75=清晨。编辑时不会自动循环；点击预览后保存，Play 时才循环。", MessageType.Info);
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Current Phase", cycle.CurrentPhase.ToString("F3"));
            Repaint();
            return;
        }
        if (GUILayout.Button("应用当前参数 / 预览 Start Phase")) AerospaceSceneLayoutBuilder.PreviewDayNightPhase(cycle, cycle.startPhase);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("白天")) AerospaceSceneLayoutBuilder.PreviewDayNightPhase(cycle, 0f);
        if (GUILayout.Button("傍晚")) AerospaceSceneLayoutBuilder.PreviewDayNightPhase(cycle, 0.25f);
        if (GUILayout.Button("夜晚")) AerospaceSceneLayoutBuilder.PreviewDayNightPhase(cycle, 0.5f);
        if (GUILayout.Button("清晨")) AerospaceSceneLayoutBuilder.PreviewDayNightPhase(cycle, 0.75f);
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("切回天空盒 1（恢复原灯光）")) AerospaceSceneLayoutBuilder.UseOriginalSkybox();
    }
}

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AerospaceSceneLayoutBuilder
{
    private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
    private const string BackupScenePath = "UserSettings/SceneBackups/Scenezl_Final 1_Before_Aerospace_SetDress_V1.unity";
    private const string SpaceModels = "Assets/Art/External/AerospaceKit_CC0/Models/Kenney_SpaceKit";
    private const string ModularModels = "Assets/Art/External/AerospaceKit_CC0/Models/Kenney_ModularSpaceKit";
    private const string LayoutRootName = "Aerospace_SetDress_V1";
    private const string LayoutPrefabPath = "Assets/Art/External/AerospaceKit_CC0/Prefabs/PFB_Dragonbone_Aerospace_SetDress_V1.prefab";
    private const string EmissiveMaterialPath = "Assets/Art/External/AerospaceKit_CC0/Materials/M_Aerospace_Emissive_Cyan.mat";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Dragonbone Layout V1")]
    public static void BuildLayoutV1()
    {
        EnsureBaselineBackup();
        ExtractionLike.Editor.AerospaceAssetSetup.RefreshForBatchmode();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform zone = FindByName(scene, "Zone-龙骨礁");
        Transform torus = zone == null ? null : FindByName(zone, "Torus");

        if (zone == null || torus == null)
        {
            throw new System.InvalidOperationException("Could not locate Zone-龙骨礁/Torus in the formal scene.");
        }

        Transform existingLayout = FindDirectChild(zone, LayoutRootName);
        if (existingLayout != null)
        {
            Object.DestroyImmediate(existingLayout.gameObject);
        }

        EnsureAssetFolder(Path.GetDirectoryName(LayoutPrefabPath)?.Replace('\\', '/'));
        Material emissive = EnsureEmissiveMaterial();

        GameObject template = new GameObject(LayoutRootName);
        template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform core = CreateSection(template.transform, "01_Core_Retrofit");
        PlaceModel(core, "Core_GimbalRing_A", SpaceModels + "/pipe_ringHigh.fbx", new Vector3(0f, 0f, 0f), 17f, new Vector3(0f, 0f, 0f));
        PlaceModel(core, "Core_GimbalRing_B", SpaceModels + "/pipe_ringHigh.fbx", new Vector3(0f, 0f, 0f), 16f, new Vector3(0f, 90f, 0f));
        PlaceModel(core, "Core_Generator_West", SpaceModels + "/machine_generatorLarge.fbx", new Vector3(-16f, 0f, -1f), 9f, new Vector3(0f, 90f, 0f));
        PlaceModel(core, "Core_Generator_East", SpaceModels + "/machine_generatorLarge.fbx", new Vector3(16f, 0f, -1f), 9f, new Vector3(0f, -90f, 0f));
        PlaceModel(core, "Core_CableBundle", ModularModels + "/cables.fbx", new Vector3(0f, 0.15f, -13f), 7f, new Vector3(0f, 10f, 0f));
        PlaceModel(core, "Core_Conduit_West", SpaceModels + "/pipe_ringSupport.fbx", new Vector3(-9f, 0f, 12f), 11f, new Vector3(0f, 25f, 0f));
        PlaceModel(core, "Core_Conduit_East", SpaceModels + "/pipe_ringSupport.fbx", new Vector3(9f, 0f, 12f), 11f, new Vector3(0f, -25f, 0f));
        PlaceModel(core, "Core_Deck_West", SpaceModels + "/platform_long.fbx", new Vector3(-10f, -0.35f, 0f), 7.5f, new Vector3(0f, 90f, 0f));
        PlaceModel(core, "Core_Deck_East", SpaceModels + "/platform_long.fbx", new Vector3(10f, -0.35f, 0f), 7.5f, new Vector3(0f, 90f, 0f));
        CreatePointLight(core, "Core_CyanLight", new Vector3(0f, 15f, 0f), new Color(0.08f, 0.72f, 1f), 1800f, 34f);

        Transform sensor = CreateSection(template.transform, "02_Sensor_Array");
        PlaceModel(sensor, "Sensor_Tower", SpaceModels + "/structure_detailed.fbx", new Vector3(39f, 0f, 18f), 12f, new Vector3(0f, -25f, 0f));
        PlaceModel(sensor, "Sensor_Dish_Primary", SpaceModels + "/satelliteDish_large.fbx", new Vector3(39f, 11.5f, 18f), 14f, new Vector3(0f, -35f, 0f));
        PlaceModel(sensor, "Sensor_WirelessRelay", SpaceModels + "/machine_wireless.fbx", new Vector3(29f, 0f, 13f), 12f, new Vector3(0f, -10f, 0f));
        PlaceModel(sensor, "Sensor_Dish_Secondary", SpaceModels + "/satelliteDish_detailed.fbx", new Vector3(51f, 0f, 8f), 10f, new Vector3(0f, -65f, 0f));
        CreatePointLight(sensor, "Sensor_CyanLight", new Vector3(39f, 20f, 18f), new Color(0.12f, 0.66f, 1f), 850f, 19f);

        Transform ribSupports = CreateSection(template.transform, "03_Rib_Maintenance");
        PlaceModel(ribSupports, "RibClamp_West", SpaceModels + "/supports_high.fbx", new Vector3(-28f, 0f, -7f), 14f, new Vector3(0f, 28f, 0f));
        PlaceModel(ribSupports, "RibClamp_East", SpaceModels + "/supports_high.fbx", new Vector3(28f, 0f, -7f), 14f, new Vector3(0f, -28f, 0f));
        PlaceModel(ribSupports, "MaintenanceFrame_West", SpaceModels + "/structure_detailed.fbx", new Vector3(-38f, 0f, 7f), 10f, new Vector3(0f, 35f, 0f));
        PlaceModel(ribSupports, "MaintenanceFrame_East", SpaceModels + "/structure_detailed.fbx", new Vector3(38f, 0f, 4f), 10f, new Vector3(0f, -35f, 0f));
        PlaceModel(ribSupports, "Conduit_West", SpaceModels + "/pipe_straight.fbx", new Vector3(-20f, 0f, -18f), 12f, new Vector3(0f, 90f, 0f));
        PlaceModel(ribSupports, "Conduit_East", SpaceModels + "/pipe_cornerRoundLarge.fbx", new Vector3(20f, 0f, -18f), 12f, new Vector3(0f, -90f, 0f));

        Transform approach = CreateSection(template.transform, "04_Southern_Approach");
        PlaceModel(approach, "Approach_Deck", SpaceModels + "/platform_large.fbx", new Vector3(0f, -6f, -38f), 11f, Vector3.zero);
        PlaceModel(approach, "Approach_Bridge", SpaceModels + "/platform_long.fbx", new Vector3(0f, -6f, -24f), 9f, Vector3.zero);
        PlaceModel(approach, "Approach_Gate", SpaceModels + "/gate_complex.fbx", new Vector3(0f, -6f, -29f), 14f, Vector3.zero);
        PlaceModel(approach, "Approach_Stairs", ModularModels + "/stairs-wide.fbx", new Vector3(0f, -6f, -50f), 1.6f, Vector3.zero);
        CreateBeacon(approach, "Beacon_SW", new Vector3(-9f, -5.8f, -47f), emissive);
        CreateBeacon(approach, "Beacon_SE", new Vector3(9f, -5.8f, -47f), emissive);
        CreateBeacon(approach, "Beacon_NW", new Vector3(-9f, -5.8f, -31f), emissive);
        CreateBeacon(approach, "Beacon_NE", new Vector3(9f, -5.8f, -31f), emissive);
        CreatePointLight(approach, "Approach_WarmLight", new Vector3(0f, 2f, -37f), new Color(1f, 0.48f, 0.16f), 700f, 20f);

        Transform expedition = CreateSection(template.transform, "05_Expedition_Equipment");
        PlaceModel(expedition, "Cargo_Shuttle", SpaceModels + "/craft_cargoB.fbx", new Vector3(-42f, -6f, 42f), 10f, new Vector3(0f, 35f, 0f));
        PlaceModel(expedition, "Survey_Rover", SpaceModels + "/rover.fbx", new Vector3(-28f, -6f, -41f), 12f, new Vector3(0f, -20f, 0f));
        PlaceModel(expedition, "Supply_Barrels", SpaceModels + "/barrels.fbx", new Vector3(-18f, -6f, -39f), 8f, new Vector3(0f, 12f, 0f));
        PlaceModel(expedition, "Survey_Dish", SpaceModels + "/satelliteDish_detailed.fbx", new Vector3(-44f, -6f, 18f), 11f, new Vector3(0f, 70f, 0f));
        PlaceModel(expedition, "Remote_PowerRelay", SpaceModels + "/machine_wirelessCable.fbx", new Vector3(44f, -6f, 39f), 12f, new Vector3(0f, -30f, 0f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, LayoutPrefabPath, out bool prefabSaved);
        Object.DestroyImmediate(template);
        if (!prefabSaved || prefab == null)
        {
            throw new System.InvalidOperationException("Failed to save the aerospace set-dressing prefab.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, zone) as GameObject;
        if (instance == null)
        {
            throw new System.InvalidOperationException("Failed to instantiate the aerospace set-dressing prefab.");
        }

        Renderer torusRenderer = torus.GetComponent<Renderer>();
        Vector3 anchor = torusRenderer == null
            ? torus.position
            : new Vector3(torusRenderer.bounds.center.x, torusRenderer.bounds.max.y - 0.2f, torusRenderer.bounds.center.z);
        instance.name = LayoutRootName;
        instance.transform.SetPositionAndRotation(anchor, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Aerospace Layout V1] Built {CountPlacedModels(instance.transform)} model placements at anchor {Format(anchor)}. Backup={BackupScenePath}");
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Remove Dragonbone Layout V1")]
    public static void RemoveLayoutV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform zone = FindByName(scene, "Zone-龙骨礁");
        Transform layout = zone == null ? null : FindDirectChild(zone, LayoutRootName);
        if (layout == null)
        {
            Debug.Log("[Aerospace Layout V1] No layout instance was found; scene left unchanged.");
            return;
        }

        Object.DestroyImmediate(layout.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Aerospace Layout V1] Removed the layout instance from the formal scene.");
    }

    public static void RenderLayoutPreview()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform zone = FindByName(scene, "Zone-龙骨礁");
        Transform layout = zone == null ? null : FindDirectChild(zone, LayoutRootName);
        if (layout == null)
        {
            throw new System.InvalidOperationException("Aerospace_SetDress_V1 was not found in the formal scene.");
        }

        TryGetBounds(layout.gameObject, out Bounds layoutBounds);
        Vector3 target = new Vector3(307.8f, 28f, 232f);
        RenderCameraPreview(
            "/tmp/extraction-aerospace-layout-v1-wide.png",
            target + new Vector3(0f, 185f, -225f),
            target,
            52f);
        RenderCameraPreview(
            "/tmp/extraction-aerospace-layout-v1-core.png",
            target + new Vector3(-105f, 88f, -125f),
            target + new Vector3(0f, 1f, 0f),
            48f);

        Debug.Log($"[Aerospace Layout V1 Preview] boundsCenter={Format(layoutBounds.center)} boundsSize={Format(layoutBounds.size)}");
    }

    public static void InspectModelBounds()
    {
        string[] paths =
        {
            SpaceModels + "/satelliteDish_large.fbx",
            SpaceModels + "/satelliteDish_detailed.fbx",
            SpaceModels + "/machine_generatorLarge.fbx",
            SpaceModels + "/machine_wireless.fbx",
            SpaceModels + "/machine_wirelessCable.fbx",
            SpaceModels + "/pipe_ringHigh.fbx",
            SpaceModels + "/pipe_ringSupport.fbx",
            SpaceModels + "/pipe_straight.fbx",
            SpaceModels + "/platform_large.fbx",
            SpaceModels + "/platform_long.fbx",
            SpaceModels + "/supports_high.fbx",
            SpaceModels + "/structure_detailed.fbx",
            SpaceModels + "/gate_complex.fbx",
            SpaceModels + "/craft_cargoB.fbx",
            SpaceModels + "/rover.fbx",
            ModularModels + "/cables.fbx",
            ModularModels + "/template-floor-big.fbx",
            ModularModels + "/template-wall-detail-a.fbx",
            ModularModels + "/stairs-wide.fbx"
        };

        Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var report = new StringBuilder("[Aerospace Model Bounds]\n");

        foreach (string path in paths)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                report.AppendLine($"MISSING {path}");
                continue;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset, previewScene) as GameObject;
            if (instance == null)
            {
                report.AppendLine($"FAILED {path}");
                continue;
            }

            if (TryGetBounds(instance, out Bounds bounds))
            {
                report.AppendLine($"MODEL {asset.name} center={Format(bounds.center)} size={Format(bounds.size)}");
            }
            else
            {
                report.AppendLine($"MODEL {asset.name} no-renderer");
            }

            Object.DestroyImmediate(instance);
        }

        Debug.Log(report.ToString());
    }

    private static void EnsureBaselineBackup()
    {
        EnsureSceneBackup(BackupScenePath);
    }

    private static void EnsureSceneBackup(string relativeBackupPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new IOException("Could not resolve the Unity project root for the scene backup.");
        }

        string sourcePath = Path.Combine(projectRoot, ScenePath);
        string backupPath = Path.Combine(projectRoot, relativeBackupPath);
        if (File.Exists(backupPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        File.Copy(sourcePath, backupPath);
    }

    private static void RenderCameraPreview(string outputPath, Vector3 position, Vector3 target, float fieldOfView)
    {
        GameObject cameraObject = new GameObject("AerospaceLayoutPreviewCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = position;
        camera.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1200f;
        camera.allowHDR = true;

        const int width = 1600;
        const int height = 900;
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(outputPath, image.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(cameraObject);
    }

    private static Material EnsureEmissiveMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(EmissiveMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new System.InvalidOperationException("URP/Lit shader is required for the aerospace layout.");
        }

        if (material == null)
        {
            material = new Material(shader) { name = "M_Aerospace_Emissive_Cyan" };
            AssetDatabase.CreateAsset(material, EmissiveMaterialPath);
        }

        Color baseColor = new Color(0.035f, 0.34f, 0.48f, 1f);
        Color emission = new Color(0.08f, 0.82f, 1f, 1f) * 3.2f;
        material.shader = shader;
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_Color", baseColor);
        material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform CreateSection(Transform parent, string name)
    {
        GameObject section = new GameObject(name);
        section.transform.SetParent(parent, false);
        return section.transform;
    }

    private static GameObject PlaceModel(
        Transform parent,
        string name,
        string assetPath,
        Vector3 localBasePosition,
        float uniformScale,
        Vector3 localEulerAngles)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            throw new FileNotFoundException($"Aerospace model not found: {assetPath}");
        }

        GameObject wrapper = new GameObject(name);
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.localPosition = localBasePosition;
        wrapper.transform.localRotation = Quaternion.Euler(localEulerAngles);

        GameObject model = PrefabUtility.InstantiatePrefab(asset, wrapper.transform) as GameObject;
        if (model == null)
        {
            Object.DestroyImmediate(wrapper);
            throw new System.InvalidOperationException($"Could not instantiate aerospace model: {assetPath}");
        }

        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * uniformScale;

        if (TryGetBounds(model, out Bounds bounds))
        {
            Vector3 targetWorld = parent.TransformPoint(localBasePosition);
            Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            wrapper.transform.position += targetWorld - currentBottomCenter;
        }

        return wrapper;
    }

    private static void CreatePointLight(
        Transform parent,
        string name,
        Vector3 localPosition,
        Color color,
        float intensity,
        float range)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
    }

    private static void CreateBeacon(Transform parent, string name, Vector3 localPosition, Material emissive)
    {
        GameObject beacon = new GameObject(name);
        beacon.transform.SetParent(parent, false);
        beacon.transform.localPosition = localPosition;

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(beacon.transform, false);
        pole.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        pole.transform.localScale = new Vector3(0.28f, 1.1f, 0.28f);
        pole.GetComponent<Renderer>().sharedMaterial = emissive;
        Object.DestroyImmediate(pole.GetComponent<Collider>());

        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "Lamp";
        lamp.transform.SetParent(beacon.transform, false);
        lamp.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        lamp.transform.localScale = Vector3.one * 0.72f;
        lamp.GetComponent<Renderer>().sharedMaterial = emissive;
        Object.DestroyImmediate(lamp.GetComponent<Collider>());
    }

    private static int CountPlacedModels(Transform root)
    {
        int count = 0;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == "Model")
            {
                count++;
            }
        }

        return count;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureAssetFolder(parent);
        }

        if (!string.IsNullOrEmpty(parent))
        {
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    public static void InspectScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var matches = new List<Transform>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            CollectMatches(root.transform, matches);
        }

        var report = new StringBuilder();
        report.AppendLine($"[Aerospace Layout Inspect] Scene={ScenePath} Roots={scene.rootCount} Matches={matches.Count}");

        Bounds boneBounds = default;
        bool hasBoneBounds = false;

        foreach (Transform transform in matches)
        {
            Renderer renderer = transform.GetComponent<Renderer>();
            string boundsText = renderer == null
                ? "no-renderer"
                : $"boundsCenter={Format(renderer.bounds.center)} boundsSize={Format(renderer.bounds.size)}";

            report.AppendLine($"MATCH path={GetPath(transform)} world={Format(transform.position)} scale={Format(transform.lossyScale)} {boundsText}");

            if (renderer != null && transform.name.StartsWith("Bone"))
            {
                if (!hasBoneBounds)
                {
                    boneBounds = renderer.bounds;
                    hasBoneBounds = true;
                }
                else
                {
                    boneBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (hasBoneBounds)
        {
            report.AppendLine($"BONE_BOUNDS center={Format(boneBounds.center)} size={Format(boneBounds.size)} min={Format(boneBounds.min)} max={Format(boneBounds.max)}");
            report.AppendLine("NEARBY_RENDERERS_BEGIN");

            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
            {
                Vector3 flatDelta = renderer.bounds.center - boneBounds.center;
                flatDelta.y = 0f;
                if (flatDelta.sqrMagnitude <= 55f * 55f && renderer.bounds.size.sqrMagnitude > 0.01f)
                {
                    report.AppendLine($"NEAR path={GetPath(renderer.transform)} center={Format(renderer.bounds.center)} size={Format(renderer.bounds.size)}");
                }
            }

            report.AppendLine("NEARBY_RENDERERS_END");
        }

        Transform dragonboneZone = FindByName(scene, "Zone-龙骨礁");
        if (dragonboneZone != null)
        {
            report.AppendLine("DRAGONBONE_ZONE_BEGIN");
            DumpSubtree(dragonboneZone, report, 0);
            report.AppendLine("DRAGONBONE_ZONE_END");
        }

        Debug.Log(report.ToString());
    }

    public static void InspectRoadNetwork()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var report = new StringBuilder($"[Aerospace Road Inspect] Scene={ScenePath}\n");
        Transform layoutRoot = FindByName(scene, "IslandWallLayoutRoot");

        if (layoutRoot != null)
        {
            report.AppendLine("ISLAND_LAYOUT_BEGIN");
            DumpSubtree(layoutRoot, report, 0);
            report.AppendLine("ISLAND_LAYOUT_END");
        }

        report.AppendLine("ZONE_ROOTS_BEGIN");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith("Zone-"))
            {
                string boundsText = TryGetBounds(root, out Bounds bounds)
                    ? $" center={Format(bounds.center)} size={Format(bounds.size)}"
                    : "";
                report.AppendLine($"ZONE name={root.name} world={Format(root.transform.position)}{boundsText}");
            }
        }
        report.AppendLine("ZONE_ROOTS_END");
        Debug.Log(report.ToString());
    }

    public static void InspectRoadsideTargets()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var report = new StringBuilder("[Aerospace Roadside Targets]\n");
        foreach (string zoneName in new[] { "Zone-员工食堂", "Zone-员工宿舍" })
        {
            Transform zone = FindByName(scene, zoneName);
            if (zone != null)
            {
                report.AppendLine(zoneName + "_BEGIN");
                DumpSubtree(zone, report, 0);
                report.AppendLine(zoneName + "_END");
            }
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith("Zone-") && TryGetBounds(root, out Bounds bounds))
            {
                report.AppendLine($"ROOT name={root.name} center={Format(bounds.center)} size={Format(bounds.size)}");
            }
        }
        foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
        {
            Bounds bounds = renderer.bounds;
            if (bounds.center.x > 160f && bounds.center.x < 280f && bounds.center.z > 0f && bounds.center.z < 140f && bounds.size.x < 50f && bounds.size.z < 50f)
            {
                report.AppendLine($"ROADSIDE_EXISTING path={GetPath(renderer.transform)} center={Format(bounds.center)} size={Format(bounds.size)}");
            }
        }
        Debug.Log(report.ToString());
    }

    public static void RenderRoadsideBeforePreview()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RenderCameraPreview("/tmp/extraction-roadside-before.png", new Vector3(140f, 95f, 105f), new Vector3(216f, 1f, -5f), 48f);
        RenderCameraPreview("/tmp/extraction-roadside-plan.png", new Vector3(203f, 210f, 61f), new Vector3(203f, 0f, 50f), 48f);
        Physics.SyncTransforms();
        var report = new StringBuilder("[Roadside Ground Samples]\n");
        foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
        {
            report.AppendLine($"TERRAIN path={GetPath(terrain.transform)} position={Format(terrain.transform.position)} size={Format(terrain.terrainData.size)}");
        }
        foreach (Vector2 point in new[] { new Vector2(185f, 18f), new Vector2(225f, 18f), new Vector2(180f, 55f), new Vector2(225f, 55f), new Vector2(205f, 95f), new Vector2(238f, 95f) })
        {
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, 180f, point.y), Vector3.down, 230f, ~0, QueryTriggerInteraction.Ignore))
            {
                report.AppendLine($"HIT xz={point} y={hit.point.y:F3} normal={Format(hit.normal)} collider={hit.collider.GetType().Name} path={GetPath(hit.transform)}");
            }
        }
        Debug.Log(report.ToString());
    }

    private static Transform FindByName(Scene scene, string targetName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindByName(root.transform, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindByName(Transform current, string targetName)
    {
        if (current.name == targetName)
        {
            return current;
        }

        foreach (Transform child in current)
        {
            Transform found = FindByName(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void DumpSubtree(Transform current, StringBuilder report, int depth)
    {
        Renderer renderer = current.GetComponent<Renderer>();
        string boundsText = renderer == null
            ? ""
            : $" renderer={renderer.GetType().Name} center={Format(renderer.bounds.center)} size={Format(renderer.bounds.size)}";
        report.AppendLine($"NODE depth={depth} name={current.name} world={Format(current.position)} local={Format(current.localPosition)} scale={Format(current.lossyScale)}{boundsText}");

        foreach (Transform child in current)
        {
            DumpSubtree(child, report, depth + 1);
        }
    }

    private static void CollectMatches(Transform current, ICollection<Transform> matches)
    {
        string name = current.name.ToLowerInvariant();
        if (name.Contains("bone")
            || current.name.Contains("龙")
            || current.name.Contains("礁")
            || name.Contains("crystal")
            || current.name.Contains("水晶")
            || name.Contains("torus")
            || name.Contains("zone"))
        {
            matches.Add(current);
        }

        foreach (Transform child in current)
        {
            CollectMatches(child, matches);
        }
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private static bool TryGetBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:F3},{value.y:F3},{value.z:F3})";
    }
}

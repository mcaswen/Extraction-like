using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Game-camera-first night lighting and old-wreck composition.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string GameViewNightRoot = "Aerospace_GameView_NightAndWreck_V1";
    public const string GameViewNightAssets = "Assets/Art/Environment/AerospaceGameViewNightAndWreckV1";
    private const string GameViewNightPrefab = GameViewNightAssets + "/Prefabs/PFB_Aerospace_GameView_NightAndWreck_V1.prefab";
    private const string GameViewNightBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_GameView_NightAndWreck_V1.unity";
    private const string GameViewNightPreviews = "UserSettings/ScenePreviews/Aerospace_GameView_NightAndWreck_V1";
    private static readonly Vector2 WreckFocus = new Vector2(290f, 17f);

    [MenuItem("Tools/Extraction-like/Aerospace/Build Game-View Night Route And Wreck V1")]
    public static void BuildGameViewNightAndWreckV1()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(GameViewNightBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, GameViewNightRoot) != null)
            throw new InvalidOperationException("Game-view night/wreck V1 already exists; hide or delete it explicitly before rebuilding.");
        Transform wreck = FindByName(scene, InvestigationRootName);
        if (wreck == null || !wreck.gameObject.activeSelf)
            throw new InvalidOperationException("The active old-wreck investigation V2 is required.");
        AerospaceDayNightCycle cycle = FindDayNight(scene);
        if (cycle == null) throw new InvalidOperationException("The 160-second day/night controller is required.");

        var navigation = CaptureInvestigationNavigation(scene);
        string originals = GameViewNightOriginalSignature(scene);
        string terrain = InvestigationTerrainSignature(scene);
        GameObject template = null;
        GameObject installed = null;
        try
        {
            foreach (string section in new[] { "Materials", "Meshes", "Prefabs" }) EnsureAssetFolder(GameViewNightAssets + "/" + section);
            Material cyan = GameViewNightMaterial("M_GV_Night_Cyan", new Color(.025f, .15f, .18f), new Color(.04f, .35f, .42f));
            Material amber = GameViewNightMaterial("M_GV_Night_Amber", new Color(.22f, .09f, .025f), new Color(.48f, .18f, .035f));
            Material dark = GameViewNightMaterial("M_GV_Frame_Dark", new Color(.045f, .065f, .075f), Color.black);

            template = new GameObject(GameViewNightRoot);
            var modifier = template.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            modifier.overrideArea = false;
            var emissionBindings = new List<AerospaceNightGroundLighting.EmissionBinding>();
            var lightBindings = new List<AerospaceNightGroundLighting.LightBinding>();

            Transform composition = CreateSection(template.transform, "02_Wreck_Top_View_Composition");
            float axisAngle = 80f * Mathf.Deg2Rad;
            Vector2 axis = new Vector2(Mathf.Sin(axisAngle), Mathf.Cos(axisAngle));
            Vector2[] axisLine = { WreckFocus - axis * 32f, WreckFocus + axis * 32f };
            Mesh axisMesh = StoreGameViewMesh("Mesh_GV_Wreck_Axis_Bands", BuildDashedGroundBands(axisLine, 8.2f, .92f, 3.5f, 1.7f));
            Renderer axisRenderer = CreateGameViewMesh(composition, "Wreck_Axis_Light_Bands", axisMesh, new[] { amber, amber, amber, amber });
            emissionBindings.Add(NightEmission(axisRenderer, 0, new Color(.025f, .008f, .002f), new Color(13f, 2.4f, .18f)));

            Transform anchors = CreateSection(template.transform, "03_Three_Perimeter_Scan_Anchors");
            Vector2[] anchorPoints = { new Vector2(326f, 29f), new Vector2(258f, 37f), new Vector2(287f, -11f) };
            for (int i = 0; i < anchorPoints.Length; i++)
            {
                Transform anchor = CreateGameViewScanAnchor(anchors, "Scan_Anchor_" + (i + 1).ToString("00"), anchorPoints[i], WreckFocus, dark, cyan, amber, out Renderer cyanTop, out Renderer amberTop, out Light lamp);
                emissionBindings.Add(NightEmission(cyanTop, 0, new Color(.004f, .018f, .022f), new Color(.8f, 9.5f, 13.5f)));
                emissionBindings.Add(NightEmission(amberTop, 0, new Color(.025f, .008f, .002f), new Color(12f, 2.2f, .16f)));
                lightBindings.Add(new AerospaceNightGroundLighting.LightBinding { light = lamp, dayIntensity = 0f, nightIntensity = 14f });
            }

            var controller = template.AddComponent<AerospaceNightGroundLighting>();
            controller.Configure(null, emissionBindings.ToArray(), lightBindings.ToArray());
            SetSurveyVisualOnly(template);
            ValidateGameViewNightRoot(template, false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, GameViewNightPrefab, out bool saved);
            if (!saved || prefab == null) throw new IOException("Could not save game-view night/wreck prefab.");
            Object.DestroyImmediate(template);
            template = null;

            installed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (installed == null) throw new IOException("Could not instantiate game-view night/wreck prefab.");
            installed.name = GameViewNightRoot;
            var installedController = installed.GetComponent<AerospaceNightGroundLighting>();
            installedController.cycle = cycle;
            installedController.ApplyPhase(cycle.startPhase);
            PrefabUtility.RecordPrefabInstancePropertyModifications(installedController);

            ValidateGameViewNightRoot(installed, true);
            AssertNoInvestigationNavigationSources(scene, installed.transform);
            AssertInvestigationSnapshotEqual(navigation, CaptureInvestigationNavigation(scene));
            if (GameViewNightOriginalSignature(scene) != originals || InvestigationTerrainSignature(scene) != terrain)
                throw new InvalidOperationException("Existing scene objects or terrain changed; formal scene will not be saved.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the formal scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(GameViewNightPreviews);
            File.WriteAllText(GameViewNightPreviews + "/navigation-verification.txt",
                "Game-view night route and wreck composition V1\nAgents=" + navigation.Agents + "\nPath probes=" + navigation.Probes +
                "\nComplete baseline=" + navigation.Complete + "\nVertices=" + navigation.Vertices + "\nSHA256=" + navigation.GeometryHash +
                "\nBefore/after navigation geometry, path results and path corners identical.\nOriginal scene objects and TerrainData unchanged.\nVisual root has no colliders, rigidbodies, obstacles, agents or gameplay targets and is excluded from all-agent NavMesh builds.\n",
                System.Text.Encoding.UTF8);
            if (!Application.isBatchMode) Selection.activeGameObject = installed;
            DeleteLegacyGameViewRouteAssets();
            Debug.Log("[GameView Night/Wreck V1] PASS: legacy blue road bands and cyan ground ring removed; amber axis bands and 3 night scan anchors installed. Navigation probes=" + navigation.Probes);
        }
        catch
        {
            if (installed != null) Object.DestroyImmediate(installed);
            throw;
        }
        finally { if (template != null) Object.DestroyImmediate(template); }
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Render Game-View Night Route And Wreck V1")]
    public static void RenderGameViewNightAndWreckV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform root = FindByName(scene, GameViewNightRoot);
        if (root == null) throw new InvalidOperationException("Install game-view night/wreck V1 first.");
        ValidateGameViewNightRoot(root.gameObject, true);
        Directory.CreateDirectory(GameViewNightPreviews);
        string folder = Path.GetFullPath(GameViewNightPreviews);
        AerospaceDayNightCycle cycle = FindDayNight(scene);
        AerospaceNightGroundLighting controller = root.GetComponent<AerospaceNightGroundLighting>();
        var lighting = AerospaceLightingState.Capture(cycle.sunLight);
        var fills = cycle.secondaryDirectionalLights.Where(l => l != null).Select(AerospaceFillLightState.Capture).ToArray();
        float phase = cycle.CurrentPhase;
        bool active = root.gameObject.activeSelf;
        Material sky = new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            root.gameObject.SetActive(true);
            cycle.ApplyPhase(0f, sky); controller.ApplyPhase(0f);
            RenderActualGameView(scene, folder + "/gameview-wreck-day.png", new Vector3(WreckFocus.x, GroundHeight(WreckFocus.x, WreckFocus.y) + 1.5f, WreckFocus.y));
            cycle.ApplyPhase(.5f, sky); controller.ApplyPhase(.5f);
            root.gameObject.SetActive(false);
            RenderActualGameView(scene, folder + "/gameview-wreck-night-before.png", new Vector3(WreckFocus.x, GroundHeight(WreckFocus.x, WreckFocus.y) + 1.5f, WreckFocus.y));
            root.gameObject.SetActive(true); controller.ApplyPhase(.5f);
            RenderActualGameView(scene, folder + "/gameview-wreck-night-after.png", new Vector3(WreckFocus.x, GroundHeight(WreckFocus.x, WreckFocus.y) + 1.5f, WreckFocus.y));
            RenderActualGameView(scene, folder + "/gameview-wreck-approach-night.png", new Vector3(270f, GroundHeight(270f, 4f) + 1.5f, 4f));
        }
        finally
        {
            root.gameObject.SetActive(active);
            cycle.ApplyPhase(phase, sky);
            if (active) controller.ApplyPhase(phase);
            lighting.Restore(cycle.sunLight);
            foreach (var fill in fills) fill.Restore();
            Object.DestroyImmediate(sky);
        }
        Debug.Log("[GameView Night/Wreck V1] Rendered actual 60-degree follow-camera previews: " + folder);
        if (Application.isBatchMode)
        {
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    public static void BuildAndRenderGameViewNightAndWreckV1()
    {
        BuildGameViewNightAndWreckV1();
        RenderGameViewNightAndWreckV1();
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Refresh Game-View Night Emission")]
    public static void RefreshGameViewNightEmission()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform root = FindByName(scene, GameViewNightRoot);
        if (root == null) throw new InvalidOperationException("Install game-view night/wreck V1 first.");
        RemoveLegacyBlueRoadBands(scene);
        float angle = 80f * Mathf.Deg2Rad;
        Vector2 axis = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        StoreGameViewMesh("Mesh_GV_Wreck_Axis_Bands", BuildDashedGroundBands(new[] { WreckFocus - axis * 32f, WreckFocus + axis * 32f }, 8.2f, .92f, 3.5f, 1.7f));
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(GameViewNightPrefab);
        try
        {
            var prefabController = prefabContents.GetComponent<AerospaceNightGroundLighting>();
            TuneGameViewNightBindings(prefabController, null);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, GameViewNightPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabContents); }
        var controller = root.GetComponent<AerospaceNightGroundLighting>();
        AerospaceDayNightCycle savedCycle = controller.cycle;
        PrefabUtility.RevertObjectOverride(controller, InteractionMode.AutomatedAction);
        TuneGameViewNightBindings(controller, savedCycle);
        controller.ApplyPhase(controller.cycle.startPhase);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save emission calibration.");
        Debug.Log("[GameView Night/Wreck V1] Removed legacy blue road bands and refreshed wreck-site emission.");
    }

    public static void RefreshAndRenderGameViewNightEmission()
    {
        RefreshGameViewNightEmission();
        RenderGameViewNightAndWreckV1();
    }

    private static void TuneGameViewNightBindings(AerospaceNightGroundLighting controller, AerospaceDayNightCycle cycle)
    {
        AerospaceNightGroundLighting.EmissionBinding[] bindings = controller.Emissions;
        foreach (var binding in bindings)
        {
            string materialName = binding.renderer.sharedMaterials[binding.materialIndex].name;
            bool amber = materialName.Contains("Amber", StringComparison.Ordinal);
            binding.dayEmission = amber ? new Color(.025f, .008f, .002f) : new Color(.004f, .018f, .022f);
            binding.nightEmission = amber ? new Color(12f, 2.2f, .16f) : new Color(.75f, 9f, 13f);
        }
        controller.Configure(cycle, bindings, controller.Lights);
    }

    public static void ValidateGameViewNightRoot(GameObject root, bool requireCycle)
    {
        if (root == null || root.name != GameViewNightRoot) throw new InvalidOperationException("Wrong game-view night root.");
        ValidateInvestigationVisualRoot(root);
        var controller = root.GetComponent<AerospaceNightGroundLighting>();
        if (controller == null || (requireCycle && controller.cycle == null)) throw new InvalidOperationException("Missing day/night ground-light controller.");
        if (controller.Emissions.Length != 7 || controller.Lights.Length != 3) throw new InvalidOperationException("Unexpected night-light binding counts.");
        if (root.transform.Find("01_Top_Readable_Night_Route_Bands") != null)
            throw new InvalidOperationException("Legacy blue road bands must not remain in the scene.");
        if (root.transform.Find("02_Wreck_Top_View_Composition/SITE07_Segmented_Survey_Ring") != null)
            throw new InvalidOperationException("Legacy cyan ground survey ring must not remain in the scene.");
        Light[] lights = root.GetComponentsInChildren<Light>(true);
        if (lights.Length != 3 || lights.Any(l => l.shadows != LightShadows.None || l.type != LightType.Point))
            throw new InvalidOperationException("Survey composition permits exactly three shadowless point lights.");
        if (root.GetComponentsInChildren<MeshRenderer>(true).Length < 20) throw new InvalidOperationException("Incomplete top-view composition.");
    }

    public static string CaptureGameViewNightOriginalState(Scene scene) => GameViewNightOriginalSignature(scene);

    public static void RemoveLegacyBlueRoadBands(Scene scene)
    {
        GameObject prefabContents = PrefabUtility.LoadPrefabContents(GameViewNightPrefab);
        try
        {
            Transform route = prefabContents.transform.Find("01_Top_Readable_Night_Route_Bands");
            Transform ring = prefabContents.transform.Find("02_Wreck_Top_View_Composition/SITE07_Segmented_Survey_Ring");
            var prefabController = prefabContents.GetComponent<AerospaceNightGroundLighting>();
            if (route != null) Object.DestroyImmediate(route.gameObject);
            if (ring != null) Object.DestroyImmediate(ring.gameObject);
            var emissions = prefabController.Emissions.Where(binding => binding != null && binding.renderer != null &&
                binding.renderer.transform.name.IndexOf("Route_Bands_", StringComparison.Ordinal) < 0 &&
                binding.renderer.transform.name != "SITE07_Segmented_Survey_Ring").ToArray();
            prefabController.Configure(null, emissions, prefabController.Lights);
            PrefabUtility.SaveAsPrefabAsset(prefabContents, GameViewNightPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefabContents); }

        Transform root = FindByName(scene, GameViewNightRoot);
        if (root == null) throw new InvalidOperationException("Game-view night/wreck V1 is missing.");
        Transform sceneRoute = root.Find("01_Top_Readable_Night_Route_Bands");
        Transform sceneRing = root.Find("02_Wreck_Top_View_Composition/SITE07_Segmented_Survey_Ring");
        if (sceneRoute != null) Object.DestroyImmediate(sceneRoute.gameObject);
        if (sceneRing != null) Object.DestroyImmediate(sceneRing.gameObject);
        var controller = root.GetComponent<AerospaceNightGroundLighting>();
        var sceneEmissions = controller.Emissions.Where(binding => binding != null && binding.renderer != null &&
            binding.renderer.transform.name.IndexOf("Route_Bands_", StringComparison.Ordinal) < 0 &&
            binding.renderer.transform.name != "SITE07_Segmented_Survey_Ring").ToArray();
        controller.Configure(controller.cycle, sceneEmissions, controller.Lights);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        DeleteLegacyGameViewRouteAssets();
        ValidateGameViewNightRoot(root.gameObject, true);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void DeleteLegacyGameViewRouteAssets()
    {
        AssetDatabase.DeleteAsset(GameViewNightAssets + "/Meshes/Mesh_GV_Route_Bands_01.asset");
        AssetDatabase.DeleteAsset(GameViewNightAssets + "/Meshes/Mesh_GV_Route_Bands_02.asset");
        AssetDatabase.DeleteAsset(GameViewNightAssets + "/Meshes/Mesh_GV_Site07_Segmented_Ring.asset");
    }

    private static AerospaceNightGroundLighting.EmissionBinding NightEmission(Renderer renderer, int index, Color day, Color night)
        => new AerospaceNightGroundLighting.EmissionBinding { renderer = renderer, materialIndex = index, dayEmission = day, nightEmission = night };

    private static Material GameViewNightMaterial(string name, Color color, Color emission)
    {
        string path = GameViewNightAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", .12f);
        material.SetFloat("_Smoothness", .28f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh StoreGameViewMesh(string name, Mesh generated)
    {
        string path = GameViewNightAssets + "/Meshes/" + name + ".asset";
        generated.name = name;
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Renderer CreateGameViewMesh(Transform parent, string name, Mesh mesh, Material[] materials)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        return renderer;
    }

    private static Mesh BuildDashedGroundBands(Vector2[] line, float sideOffset, float width, float dashLength, float gap)
    {
        var writer = new CrashMeshWriter();
        for (int segment = 1; segment < line.Length; segment++)
        {
            Vector2 from = line[segment - 1], to = line[segment];
            Vector2 direction = (to - from).normalized;
            Vector2 right = new Vector2(direction.y, -direction.x);
            float length = Vector2.Distance(from, to);
            for (float distance = gap * .5f; distance + .5f < length; distance += dashLength + gap)
            {
                float half = Mathf.Min(dashLength, length - distance) * .5f;
                Vector2 center = from + direction * (distance + half);
                foreach (float side in new[] { -sideOffset, sideOffset })
                {
                    Vector2 c = center + right * side;
                    Vector2 a = c - direction * half - right * (width * .5f);
                    Vector2 b = c + direction * half - right * (width * .5f);
                    Vector2 d = c - direction * half + right * (width * .5f);
                    Vector2 e = c + direction * half + right * (width * .5f);
                    AddDoubleSidedGroundQuad(writer, a, b, e, d, 0, .105f);
                }
            }
        }
        return writer.Finish();
    }

    private static Mesh BuildSegmentedGroundEllipse(Vector2 center, float radiusX, float radiusZ, float width, int segments)
    {
        var writer = new CrashMeshWriter();
        for (int i = 0; i < segments; i++)
        {
            if (i % 4 == 3) continue;
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + .78f) * Mathf.PI * 2f / segments;
            Vector2 outerA = center + new Vector2(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusZ);
            Vector2 outerB = center + new Vector2(Mathf.Cos(b) * radiusX, Mathf.Sin(b) * radiusZ);
            Vector2 innerA = center + new Vector2(Mathf.Cos(a) * (radiusX - width), Mathf.Sin(a) * (radiusZ - width));
            Vector2 innerB = center + new Vector2(Mathf.Cos(b) * (radiusX - width), Mathf.Sin(b) * (radiusZ - width));
            int material = i % 12 == 0 ? 1 : 0;
            AddDoubleSidedGroundQuad(writer, outerA, outerB, innerB, innerA, material, .11f);
        }
        return writer.Finish();
    }

    private static void AddDoubleSidedGroundQuad(CrashMeshWriter writer, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int material, float lift)
    {
        Vector3 A = new Vector3(a.x, GroundHeight(a.x, a.y) + lift, a.y);
        Vector3 B = new Vector3(b.x, GroundHeight(b.x, b.y) + lift, b.y);
        Vector3 C = new Vector3(c.x, GroundHeight(c.x, c.y) + lift, c.y);
        Vector3 D = new Vector3(d.x, GroundHeight(d.x, d.y) + lift, d.y);
        writer.Quad(A, B, C, D, material);
        writer.Quad(D, C, B, A, material);
    }

    private static Transform CreateGameViewScanAnchor(Transform parent, string name, Vector2 point, Vector2 focus, Material dark, Material cyan, Material amber,
        out Renderer cyanTop, out Renderer amberTop, out Light lamp)
    {
        Transform anchor = CreateSection(parent, name);
        anchor.position = new Vector3(point.x, GroundHeight(point.x, point.y) + .06f, point.y);
        Vector2 aim = focus - point;
        anchor.rotation = Quaternion.Euler(0f, Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg, 0f);
        SurveyCube(anchor, "Low_Dark_Base", new Vector3(0, .12f, 0), new Vector3(3.8f, .24f, 3.2f), dark);
        SurveyCube(anchor, "Scanner_Post", new Vector3(0, 1.65f, 0), new Vector3(.42f, 3.1f, .42f), dark);
        SurveyBeam(anchor, "Top_Frame_L", new Vector3(-1.45f, 3.05f, -.9f), new Vector3(0f, 3.05f, 1.35f), .16f, dark);
        SurveyBeam(anchor, "Top_Frame_R", new Vector3(0f, 3.05f, 1.35f), new Vector3(1.45f, 3.05f, -.9f), .16f, dark);
        SurveyBeam(anchor, "Top_Frame_Back", new Vector3(1.45f, 3.05f, -.9f), new Vector3(-1.45f, 3.05f, -.9f), .16f, dark);
        SurveyCube(anchor, "Cyan_Scan_Cap", new Vector3(0, 3.32f, .18f), new Vector3(1.25f, .18f, .62f), cyan);
        SurveyCube(anchor, "Amber_Site_Cap", new Vector3(0, .25f, -1.63f), new Vector3(2.4f, .16f, .16f), amber);
        cyanTop = FindByName(anchor, "Cyan_Scan_Cap").GetComponent<Renderer>();
        amberTop = FindByName(anchor, "Amber_Site_Cap").GetComponent<Renderer>();
        GameObject lightObject = new GameObject("Night_Ground_Pool_No_Shadow");
        lightObject.transform.SetParent(anchor, false);
        lightObject.transform.localPosition = new Vector3(0, 2.45f, .35f);
        lamp = lightObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 11f;
        lamp.color = new Color(.34f, .78f, 1f);
        lamp.intensity = 0f;
        lamp.shadows = LightShadows.None;
        lamp.renderMode = LightRenderMode.ForcePixel;
        lightObject.AddComponent<UniversalAdditionalLightData>();
        return anchor;
    }

    private static void RenderActualGameView(Scene scene, string path, Vector3 target)
    {
        Camera source = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
        CameraFollowController follow = source.GetComponent<CameraFollowController>();
        Vector3 forward = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AgentPawnRoot>(true)).OrderBy(a => a.name).First().transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 configured = follow != null ? follow.Offset : new Vector3(0, 70, -70);
        Vector3 offset = right * configured.x + Vector3.up * configured.y + forward * configured.z;
        GameObject cameraObject = new GameObject("ActualGameViewPreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(target + Vector3.up * 1.5f - (target + offset), Vector3.up));
        camera.clearFlags = source.clearFlags;
        camera.fieldOfView = source.fieldOfView;
        camera.nearClipPlane = source.nearClipPlane;
        camera.farClipPlane = source.farClipPlane;
        camera.cullingMask = source.cullingMask;
        camera.allowHDR = source.allowHDR;
        var sourceData = source.GetUniversalAdditionalCameraData();
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = sourceData.renderPostProcessing;
        data.volumeLayerMask = sourceData.volumeLayerMask;
        data.antialiasing = sourceData.antialiasing;
        AerospaceLightingVegetationProfile art = AerospaceLightingVegetationProfile.ForCamera(source);
        AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, art);
        var buffer = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = buffer;
            camera.Render();
            RenderTexture.active = buffer;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, null);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(buffer);
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static string GameViewNightOriginalSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == GameViewNightRoot) continue;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(GetPath(child) + "|" + child.localPosition.ToString("F5") + "|" + child.localRotation.ToString("F5") + "|" + child.localScale.ToString("F5") + "|" + child.gameObject.activeSelf + "|" + child.gameObject.layer);
                foreach (Component component in child.GetComponents<Component>())
                    entries.Add(GetPath(child) + "|" + (component == null ? "missing" : component.GetType().FullName + "|" + EditorJsonUtility.ToJson(component)));
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return string.Join("\n", entries);
    }
}

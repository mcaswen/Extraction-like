using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gameplay.Agent.Core;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Visual-only old wreck survey site. Never builds or edits navigation/terrain.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string InvestigationRootName = "Aerospace_RocketCrash_Investigation_V2";
    public const string InvestigationAssets = "Assets/Art/Environment/AerospaceRocketInvestigation";
    public const string InvestigationPrefabPath = InvestigationAssets + "/Prefabs/PFB_OldRocket_Investigation_V2.prefab";
    private const string InvestigationBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_Rocket_Investigation_V2.unity";
    private const string InvestigationPreviews = "UserSettings/ScenePreviews/Aerospace_RocketInvestigation_V2";
    private const string InvestigationFont = "Assets/ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string InvestigationGraph = "Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset";
    private const float SurveyRouteClearance = 3.5f;

    [MenuItem("Tools/Extraction-like/Aerospace/Build Old Wreck Investigation V2")]
    public static void BuildOldWreckInvestigationV2()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(InvestigationBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform original = FindByName(scene, CrashRootName);
        if (original == null) throw new InvalidOperationException("Rocket Crash V1 is required; it is retained for comparison.");
        if (FindByName(scene, InvestigationRootName) != null)
            throw new InvalidOperationException("V2 already exists. Use the version menus; remove V2 explicitly before rebuilding.");

        var before = CaptureInvestigationNavigation(scene);
        string originalSignature = InvestigationOriginalSignature(scene);
        List<Vector3[]> corridors = InvestigationRouteCorridors(scene);
        var protectedPoints = InvestigationInteractionPoints(scene);
        string terrainSignature = InvestigationTerrainSignature(scene);
        bool originalActive = original.gameObject.activeSelf;
        GameObject template = null;
        GameObject installed = null;
        try
        {
            EnsureAssetFolder(InvestigationAssets + "/Materials");
            EnsureAssetFolder(InvestigationAssets + "/Meshes");
            EnsureAssetFolder(InvestigationAssets + "/Prefabs");
            template = new GameObject(InvestigationRootName);
            var modifier = template.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            modifier.overrideArea = false;

            var hull = SurveyMaterial("M_Survey_Weathered_Hull", new Color(0.63f, 0.64f, 0.59f), 0.13f);
            var orange = SurveyMaterial("M_Survey_Faded_Orange", new Color(0.68f, 0.29f, 0.12f), 0.08f);
            var burnt = SurveyMaterial("M_Survey_Oxidized_Metal", new Color(0.16f, 0.18f, 0.17f), 0.16f);
            var rust = SurveyMaterial("M_Survey_Rust", new Color(0.30f, 0.17f, 0.085f), 0.03f);
            var moss = SurveyMaterial("M_Survey_Moss", new Color(0.20f, 0.25f, 0.12f), 0f);
            var dark = SurveyMaterial("M_Survey_Navy", new Color(0.075f, 0.12f, 0.16f), 0.12f);
            var clean = SurveyMaterial("M_Survey_Equipment_Gray", new Color(0.63f, 0.71f, 0.72f), 0.10f);
            var cyan = SurveyMaterial("M_Survey_Status_Cyan", new Color(0.035f, 0.40f, 0.51f), 0.05f, new Color(0.08f, 0.65f, 0.77f));
            var soil = SurveyMaterial("M_Survey_Old_Skid_Earth", new Color(0.25f, 0.23f, 0.17f), 0f);

            // Clone the scene instance, not an old asset: preserve any user adjustments to V1.
            GameObject wreck = Object.Instantiate(original.gameObject, template.transform);
            wreck.name = "01_Preserved_Wreck_With_Weathering";
            wreck.SetActive(true);
            foreach (Renderer renderer in wreck.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string name = materials[i] == null ? "" : materials[i].name;
                    if (name.Contains("Hull_OffWhite") || name == "M_Kenney_White") materials[i] = hull;
                    else if (name.Contains("Safety_Orange") || name == "M_Kenney_Orange") materials[i] = orange;
                    else if (name.Contains("Burnt_Metal")) materials[i] = burnt;
                }
                renderer.sharedMaterials = materials;
            }
            // No residual heat/fire: the cool inspection fill stays, but no new lights are added.
            Transform heat = FindByName(wreck.transform, "Wreck_Warm_Residual_Glow");
            if (heat != null) heat.gameObject.SetActive(false);
            foreach (Light light in wreck.GetComponentsInChildren<Light>(true))
                if (light.GetComponent<UniversalAdditionalLightData>() == null) light.gameObject.AddComponent<UniversalAdditionalLightData>();

            Transform weather = CreateSection(template.transform, "02_Aged_Hull_Surface_Details");
            AddSurveyTubeWeathering(weather, FindByName(wreck.transform, "MainHull_Broken_Open"), 4.1f, 18f, rust, moss, 0.7f);
            AddSurveyTubeWeathering(weather, FindByName(wreck.transform, "UpperStage_Separated"), 3.85f, 10f, rust, moss, 2.2f);
            AddSurveyTubeWeathering(weather, FindByName(wreck.transform, "BoosterHull"), 3.85f, 10f, rust, moss, 4f);

            Transform equipment = CreateSection(template.transform, "03_Unmanned_Investigation_Equipment");
            Transform relay = SafeSurveySection(equipment, "Remote_Telemetry_Station", new Vector2(296f, -4f), 4.5f, corridors, protectedPoints);
            PlaceModel(relay, "Passive_Dish", SpaceModels + "/satelliteDish_detailed.fbx", new Vector3(-1.5f, 0f, 0.3f), 5f, new Vector3(0f, -35f, 0f));
            PlaceModel(relay, "Data_Logger", SpaceModels + "/machine_wirelessCable.fbx", new Vector3(1.5f, 0f, 0.2f), 4f, new Vector3(0f, -55f, 0f));
            SurveyCube(relay, "Status_Only_No_Real_Light", new Vector3(1.5f, 2.05f, -0.45f), new Vector3(0.6f, 0.14f, 0.08f), cyan);
            ValidateSurveyFootprint(relay.gameObject, corridors, protectedPoints);

            Transform scanner = SafeSurveySection(equipment, "Fixed_Photogrammetry_Scanner", new Vector2(312f, 32f), 2.7f, corridors, protectedPoints);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                Vector3 foot = new Vector3(Mathf.Cos(angle) * 1.4f, 0.12f, Mathf.Sin(angle) * 1.4f);
                SurveyBeam(scanner, "Tripod_Leg_" + i, foot, new Vector3(0f, 3f, 0f), 0.16f, dark);
            }
            SurveyCube(scanner, "Scanner_Head", new Vector3(0f, 3.15f, 0f), new Vector3(1.2f, 0.5f, 0.8f), clean);
            SurveyCube(scanner, "Optical_Window", new Vector3(0f, 3.15f, -0.41f), new Vector3(0.8f, 0.23f, 0.045f), cyan);
            ValidateSurveyFootprint(scanner.gameObject, corridors, protectedPoints);

            Transform rover = SafeSurveySection(equipment, "Parked_Unmanned_Survey_Rover", new Vector2(279f, -3f), 3.8f, corridors, protectedPoints);
            PlaceModel(rover, "Static_Rover_No_AI", SpaceModels + "/rover.fbx", Vector3.zero, 5f, new Vector3(0f, -30f, 0f));
            ValidateSurveyFootprint(rover.gameObject, corridors, protectedPoints);

            Transform markers = CreateSection(template.transform, "04_Evidence_Tags_Not_A_Fence");
            Vector2[] evidence = { new Vector2(272f, 24f), new Vector2(287f, 33f), new Vector2(306f, 10f), new Vector2(322f, 24f), new Vector2(277f, 6f) };
            for (int i = 0; i < evidence.Length; i++)
            {
                Transform tag = SafeSurveySection(markers, "Evidence_" + (i + 1).ToString("00"), evidence[i], 0.8f, corridors, protectedPoints);
                SurveyCube(tag, "Thin_Post", new Vector3(0f, 1.3f, 0f), new Vector3(0.16f, 2.6f, 0.16f), dark);
                SurveyCube(tag, "Number_Plate", new Vector3(0f, 2.35f, 0f), new Vector3(0.95f, 0.8f, 0.12f), orange);
                SurveyText(tag, "Number", (i + 1).ToString("00"), new Vector3(0f, 2.35f, -0.071f), new Vector2(0.85f, 0.7f), 5.6f);
                SurveyCube(tag, "Cyan_Cap", new Vector3(0f, 2.85f, 0f), new Vector3(0.2f, 0.12f, 0.2f), cyan);
                ValidateSurveyFootprint(tag.gameObject, corridors, protectedPoints);
            }

            Transform info = SafeSurveySection(template.transform, "05_Roadside_Survey_Record", new Vector2(270f, -1f), 3.6f, corridors, protectedPoints);
            info.localRotation = Quaternion.Euler(0f, -45f, 0f);
            SurveyCube(info, "Record_Leg_L", new Vector3(-1.25f, 1.7f, 0f), new Vector3(0.18f, 3.4f, 0.18f), dark);
            SurveyCube(info, "Record_Leg_R", new Vector3(1.25f, 1.7f, 0f), new Vector3(0.18f, 3.4f, 0.18f), dark);
            SurveyCube(info, "Navy_Record_Panel", new Vector3(0f, 3.2f, 0f), new Vector3(4.4f, 2.6f, 0.16f), dark);
            SurveyCube(info, "Faded_Header", new Vector3(0f, 4.4f, -0.09f), new Vector3(4.3f, 0.18f, 0.025f), orange);
            SurveyText(info, "Record_Text", "SITE 07\nARCHIVED WRECK\nREMOTE SURVEY", new Vector3(0f, 3.2f, -0.091f), new Vector2(4.1f, 2.3f), 5f);
            ValidateSurveyFootprint(info.gameObject, corridors, protectedPoints);

            Transform ground = CreateSection(template.transform, "06_Old_Earth_Traces_Under_Returning_Grass");
            CreateSurveyGroundTrace(ground, "Old_Skid_Trace", new Vector2(264f, 13f), 14f, 2.3f, 80f, soil);
            SetSurveyVisualOnly(template);
            ValidateInvestigationVisualRoot(template);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, InvestigationPrefabPath, out bool saved);
            if (!saved || prefab == null) throw new IOException("Failed to save the survey prefab; formal scene is not saved.");
            Object.DestroyImmediate(template);
            template = null;
            installed = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (installed == null) throw new IOException("Could not instantiate survey prefab.");
            installed.name = InvestigationRootName;
            original.gameObject.SetActive(false);
            ValidateInvestigationVisualRoot(installed);
            AssertNoInvestigationNavigationSources(scene, installed.transform);
            AssertInvestigationSnapshotEqual(before, CaptureInvestigationNavigation(scene));
            if (InvestigationOriginalSignature(scene) != originalSignature || InvestigationTerrainSignature(scene) != terrainSignature)
                throw new InvalidOperationException("Existing non-wreck objects or terrain changed. Scene will not be saved.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            WriteInvestigationVerification(before, scene);
            Selection.activeGameObject = installed;
            Debug.Log("[Rocket Investigation V2] PASS: original gameplay/terrain retained; V1 retained inactive; visual-only V2 installed. Backup=" + InvestigationBackup);
        }
        catch
        {
            if (installed != null) Object.DestroyImmediate(installed);
            original.gameObject.SetActive(originalActive);
            throw;
        }
        finally { if (template != null) Object.DestroyImmediate(template); }
    }

    public static void BuildAndRenderOldWreckInvestigationV2()
    {
        BuildOldWreckInvestigationV2();
        string folder = Path.GetFullPath(InvestigationPreviews);
        Directory.CreateDirectory(folder);
        Scene scene = SceneManager.GetActiveScene();
        var cycle = FindDayNight(scene);
        RenderCameraPreview(folder + "/investigation-v2-day-close.png", new Vector3(332f, 39f, -19f), new Vector3(291f, 9f, 18f), 48f);
        RenderCameraPreview(folder + "/investigation-v2-day-wide.png", new Vector3(387f, 90f, -57f), new Vector3(290f, 8f, 18f), 47f);
        if (cycle != null)
        {
            cycle.ApplyPhase(0.5f);
            RenderCameraPreview(folder + "/investigation-v2-night.png", new Vector3(332f, 39f, -19f), new Vector3(291f, 9f, 18f), 48f);
            cycle.ApplyPhase(cycle.startPhase);
        }
        SetInvestigationVersion(scene, false);
        RenderCameraPreview(folder + "/investigation-v1-comparison.png", new Vector3(332f, 39f, -19f), new Vector3(291f, 9f, 18f), 48f);
        SetInvestigationVersion(scene, true);
        // Previews are temporary: do not save any render-generated changes to original lights.
        Debug.Log("[Rocket Investigation V2] Saved real Unity renders: " + folder);
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Wreck Comparison/Use Original V1")]
    public static void UseOriginalRocketV1() { SwitchInvestigationVersion(false); }

    [MenuItem("Tools/Extraction-like/Aerospace/Wreck Comparison/Use Investigation V2")]
    public static void UseRocketInvestigationV2() { SwitchInvestigationVersion(true); }

    private static void SwitchInvestigationVersion(bool useV2)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("请先打开正式场景 Scene_DB/Scenezl_Final 1。");
        var original = FindByName(scene, CrashRootName);
        var survey = FindByName(scene, InvestigationRootName);
        if (original == null || survey == null) throw new InvalidOperationException("Both wreck versions must be present.");
        Undo.RecordObjects(new Object[] { original.gameObject, survey.gameObject }, "Switch wreck landscape version");
        SetInvestigationVersion(scene, useV2);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Rocket Investigation] Switched landscape only; Cmd/Ctrl+S to save.");
    }

    private static void SetInvestigationVersion(Scene scene, bool useV2)
    {
        FindByName(scene, CrashRootName).gameObject.SetActive(!useV2);
        FindByName(scene, InvestigationRootName).gameObject.SetActive(useV2);
    }

    private static Material SurveyMaterial(string name, Color color, float metallic, Color emission = default)
    {
        string path = InvestigationAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.07f);
        if (emission.maxColorComponent > 0f)
        {
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
        }
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SurveyCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        => CreateBeaconPart(parent, name, position, scale, material);

    private static void SurveyBeam(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
    {
        Transform beam = CreateSection(parent, name);
        beam.localPosition = (a + b) * 0.5f;
        beam.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        SurveyCube(beam, "Visual_Beam", Vector3.zero, new Vector3(width, Vector3.Distance(a, b), width), material);
    }

    private static void SurveyText(Transform parent, string name, string value, Vector3 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshPro>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(InvestigationFont);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.85f, 0.90f, 0.87f);
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = size;
        text.transform.localPosition = position;
        text.ForceMeshUpdate();
    }

    private static Mesh StoreSurveyMesh(string name, Mesh mesh)
    {
        mesh.name = name;
        string path = InvestigationAssets + "/Meshes/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) throw new InvalidOperationException("Survey mesh already exists: " + path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void AddSurveyTubeWeathering(Transform parent, Transform tube, float radius, float length, Material rust, Material moss, float seed)
    {
        if (tube == null) throw new InvalidOperationException("Missing wreck tube for surface details.");
        var writer = new CrashMeshWriter();
        for (int i = 0; i < 14; i++)
        {
            float angle = 0.25f + Mathf.Repeat(i * 1.83f + seed, 2.8f);
            float z = Mathf.Sin(i * 3.71f + seed) * (length * 0.36f);
            float width = 0.09f + Mathf.Repeat(i * 0.13f, 0.18f);
            float streak = 0.7f + Mathf.Repeat(i * 0.69f, 2.1f);
            float r = radius + 0.028f;
            writer.Quad(CrashRingPoint(r, angle, z), CrashRingPoint(r, angle + width, z + 0.08f),
                CrashRingPoint(r, angle + width * 0.55f, Mathf.Min(length * 0.4f, z + streak)),
                CrashRingPoint(r, angle - width * 0.22f, Mathf.Min(length * 0.4f, z + streak * 0.83f)), i % 5 == 0 ? 1 : 0);
        }
        Mesh mesh = StoreSurveyMesh("Mesh_Weathering_" + tube.name, writer.Finish());
        GameObject skin = CreateCrashMeshPart(parent, "Oxidation_" + tube.name, mesh, tube.position, tube.eulerAngles, new[] { rust, moss, rust, rust });
        skin.transform.localScale = tube.lossyScale;
    }

    private static void CreateSurveyGroundTrace(Transform parent, string name, Vector2 center, float length, float width, float yaw, Material soil)
    {
        var writer = new CrashMeshWriter();
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 origin = new Vector3(center.x, GroundHeight(center.x, center.y) + 0.055f, center.y);
        for (int i = 0; i < 12; i++)
        {
            float z0 = -length + i * length * 2f / 12f;
            float z1 = z0 + length * 2f / 12f;
            Vector3[] vertices = { new Vector3(-width, 0f, z0), new Vector3(width, 0f, z0), new Vector3(width, 0f, z1), new Vector3(-width, 0f, z1) };
            for (int j = 0; j < 4; j++)
            {
                Vector3 p = origin + rotation * vertices[j];
                p.y = GroundHeight(p.x, p.z) + 0.055f;
                vertices[j] = p - origin;
            }
            writer.Quad(vertices[3], vertices[2], vertices[1], vertices[0], 0);
        }
        Mesh mesh = StoreSurveyMesh("Mesh_" + name, writer.Finish());
        CreateCrashMeshPart(parent, name, mesh, origin, Vector3.zero, new[] { soil, soil, soil, soil });
    }

    private static Transform SafeSurveySection(Transform parent, string name, Vector2 desired, float radius, List<Vector3[]> corridors, List<Vector3> points)
    {
        // Small bounded search only around the proposed shoulder, never relocate across the map.
        for (int ring = 0; ring <= 6; ring++)
        {
            int count = ring == 0 ? 1 : 16;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector2 candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * 2f);
                if (!SurveyPositionClear(candidate, radius, corridors, points)) continue;
                float y = GroundHeight(candidate.x, candidate.y);
                float min = y, max = y;
                foreach (Vector2 corner in new[] { new Vector2(-radius, -radius), new Vector2(-radius, radius), new Vector2(radius, -radius), new Vector2(radius, radius) })
                {
                    float h = GroundHeight(candidate.x + corner.x, candidate.y + corner.y);
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                }
                if (max - min > 1.8f) continue;
                Transform section = CreateSection(parent, name);
                section.position = new Vector3(candidate.x, y + 0.05f, candidate.y);
                Debug.Log("[Rocket Investigation placement] " + name + " at " + Format(section.position));
                return section;
            }
        }
        throw new InvalidOperationException("No safe shoulder for " + name + "; scene will not be saved.");
    }

    private static bool SurveyPositionClear(Vector2 point, float radius, List<Vector3[]> corridors, List<Vector3> protectedPoints)
    {
        foreach (Vector3 p in protectedPoints)
            if (Vector2.Distance(point, new Vector2(p.x, p.z)) < 8f + radius) return false;
        foreach (Vector3[] line in corridors)
            for (int i = 1; i < line.Length; i++)
                if (InvestigationSegmentDistance(point, new Vector2(line[i - 1].x, line[i - 1].z), new Vector2(line[i].x, line[i].z)) < SurveyRouteClearance + radius) return false;
        return true;
    }

    public static float InvestigationSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float t = delta.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
        return Vector2.Distance(point, a + delta * t);
    }

    private static void ValidateSurveyFootprint(GameObject group, List<Vector3[]> corridors, List<Vector3> points)
    {
        if (!TryGetBounds(group, out Bounds bounds)) throw new InvalidOperationException("Survey equipment has no bounds.");
        // Conservative enclosing circle covers the complete rendered equipment, not just its pivot.
        float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
        if (!SurveyPositionClear(new Vector2(bounds.center.x, bounds.center.z), radius, corridors, points))
            throw new InvalidOperationException("Equipment overlaps protected route/interaction space: " + group.name);
    }

    private static List<Vector3> InvestigationInteractionPoints(Scene scene)
    {
        var points = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameplayTargetClusterAuthoringBase>(true))
            .Select(c => c.CenterPosition).ToList();
        foreach (ResourceClusterAuthoring cluster in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ResourceClusterAuthoring>(true)))
            foreach (var member in cluster.ResourceMembers)
                if (member.EntityObject != null) points.Add(member.Position);
        return points;
    }

    private static List<Vector3[]> InvestigationRouteCorridors(Scene scene)
    {
        var graph = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(InvestigationGraph);
        var clusters = new Dictionary<string, GameplayTargetClusterAuthoringBase>();
        foreach (var node in graph.Nodes)
        {
            // Published node IDs are not necessarily the authoring component's TargetId.
            // Resolve the saved scene binding used by the actual command map instead.
            if (!GlobalObjectId.TryParse(node.SourceObjectId, out var id)) continue;
            Object source = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            GameObject go = source as GameObject;
            if (source is Component component) go = component.gameObject;
            var cluster = go == null ? null : go.GetComponent<GameplayTargetClusterAuthoringBase>();
            if (cluster != null && cluster.gameObject.scene == scene) clusters[node.NodeId] = cluster;
        }
        var result = new List<Vector3[]>();
        foreach (var edge in graph.Edges)
        {
            if (!clusters.TryGetValue(edge.FromNodeId, out var a) || !clusters.TryGetValue(edge.ToNodeId, out var b))
                throw new InvalidOperationException("Unresolved formal map edge: " + edge.EdgeId);
            var path = new NavMeshPath();
            if (NavMesh.SamplePosition(a.CenterPosition, out var start, 8f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(b.CenterPosition, out var end, 8f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                result.Add(path.corners);
            else result.Add(new[] { a.CenterPosition, b.CenterPosition }); // Still protect the logical corridor if already disconnected.
        }
        Debug.Log("[Rocket Investigation] Protected formal edge corridors=" + result.Count);
        return result;
    }

    public static void SetSurveyVisualOnly(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }
    }

    public static void ValidateInvestigationVisualRoot(GameObject root)
    {
        var modifier = root.GetComponent<NavMeshModifier>();
        if (modifier == null || !modifier.enabled || !modifier.ignoreFromBuild || !modifier.applyToChildren || !modifier.AffectsAgentType(0))
            throw new InvalidOperationException("Survey layout must explicitly ignore all-agent navigation builds, including children.");
        if (root.GetComponentsInChildren<Collider>(true).Length > 0 || root.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
            root.GetComponentsInChildren<NavMeshObstacle>(true).Length > 0 || root.GetComponentsInChildren<NavMeshAgent>(true).Length > 0 ||
            root.GetComponentsInChildren<GameplayTargetAuthoringBase>(true).Length > 0 || root.GetComponentsInChildren<AgentPawnRoot>(true).Length > 0)
            throw new InvalidOperationException("Survey landscape must not contain physics, agents or gameplay targets.");
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.gameObject.layer != LayerMask.NameToLayer("Ignore Raycast") || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                throw new InvalidOperationException("Survey landscape has an interactive layer or missing script.");
        }
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh == null) throw new InvalidOperationException("Survey landscape has a missing mesh.");
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    throw new InvalidOperationException("Survey landscape has a missing material/shader.");
    }

    public static void AssertNoInvestigationNavigationSources(Scene scene, Transform root)
    {
        var method = typeof(NavMeshSurface).GetMethod("CollectSources", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new InvalidOperationException("Cannot verify installed Navigation package source collection.");
        foreach (var surface in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true)))
        {
            var sources = (List<NavMeshBuildSource>)method.Invoke(surface, null);
            if (sources.Any(s => s.component != null && s.component.transform.IsChildOf(root)))
                throw new InvalidOperationException("A visual survey mesh entered the surface's actual navigation build sources.");
        }
    }

    public static void VerifyInvestigationEquipmentShoulders(Scene scene)
    {
        Transform root = FindByName(scene, InvestigationRootName);
        if (root == null) throw new InvalidOperationException("No investigation landscape found.");
        var corridors = InvestigationRouteCorridors(scene);
        var points = InvestigationInteractionPoints(scene);
        foreach (string section in new[] { "03_Unmanned_Investigation_Equipment", "04_Evidence_Tags_Not_A_Fence" })
        {
            Transform group = FindDirectChild(root, section);
            if (group == null) throw new InvalidOperationException("Missing investigation equipment section.");
            foreach (Transform item in group) ValidateSurveyFootprint(item.gameObject, corridors, points);
        }
        Transform record = FindDirectChild(root, "05_Roadside_Survey_Record");
        if (record == null) throw new InvalidOperationException("Missing survey record.");
        ValidateSurveyFootprint(record.gameObject, corridors, points);
    }

    public sealed class InvestigationNavigationSnapshot
    {
        public string GeometryHash;
        public string Paths;
        public int Agents;
        public int Probes;
        public int Complete;
        public int Vertices;
    }

    public static InvestigationNavigationSnapshot CaptureInvestigationNavigation(Scene scene)
    {
        var agents = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AgentPawnRoot>(true)).OrderBy(a => a.name).ToArray();
        var clusters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameplayTargetClusterAuthoringBase>(true))
            .OrderBy(c => c.TargetId).ToArray();
        var geometry = NavMesh.CalculateTriangulation();
        var snapshot = new InvestigationNavigationSnapshot { Agents = agents.Length, Vertices = geometry.vertices.Length };
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                foreach (Vector3 vertex in geometry.vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
                foreach (int index in geometry.indices) writer.Write(index);
                foreach (int area in geometry.areas) writer.Write(area);
            }
            using (var hash = SHA256.Create()) snapshot.GeometryHash = BitConverter.ToString(hash.ComputeHash(stream.ToArray()));
        }
        if (agents.Length != 2 || snapshot.Vertices == 0) throw new InvalidOperationException("Expected two formal Agents and a loaded original NavMesh.");
        var text = new StringBuilder();
        foreach (var agent in agents)
        {
            var nav = agent.NavMeshAgent;
            if (nav == null) throw new InvalidOperationException("Formal Agent has no navigation component.");
            var filter = new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask };
            // Compare all cluster pairs as well as both spawn-to-cluster paths; preserve even pre-existing failures.
            var starts = new[] { agent.transform.position }.Concat(clusters.Select(c => c.CenterPosition)).ToArray();
            foreach (Vector3 startPoint in starts)
            {
                foreach (var cluster in clusters)
                {
                    snapshot.Probes++;
                    var path = new NavMeshPath();
                    bool startFound = NavMesh.SamplePosition(startPoint, out var start, 8f, filter);
                    bool endFound = NavMesh.SamplePosition(cluster.CenterPosition, out var end, 8f, filter);
                    bool found = startFound && endFound && NavMesh.CalculatePath(start.position, end.position, filter, path);
                    text.Append(agent.name).Append('|').Append(startPoint.ToString("F5")).Append('|').Append(cluster.TargetId)
                        .Append('|').Append(startFound).Append('|').Append(endFound).Append('|').Append(found).Append('|').Append(path.status);
                    if (found && path.status == NavMeshPathStatus.PathComplete) snapshot.Complete++;
                    foreach (Vector3 corner in path.corners) text.Append('|').Append(corner.ToString("F5"));
                    text.AppendLine();
                }
            }
        }
        snapshot.Paths = text.ToString();
        if (snapshot.Complete == 0) throw new InvalidOperationException("No baseline paths could be checked.");
        return snapshot;
    }

    public static void AssertInvestigationSnapshotEqual(InvestigationNavigationSnapshot before, InvestigationNavigationSnapshot after)
    {
        if (before.GeometryHash != after.GeometryHash || before.Paths != after.Paths || before.Agents != after.Agents || before.Probes != after.Probes)
            throw new InvalidOperationException("Landscape changed original navigation geometry or Agent path results.");
        Debug.Log($"[Rocket Investigation Navigation] PASS: agents={after.Agents}, pathProbes={after.Probes}, complete={after.Complete}, vertices={after.Vertices}, identical geometry and path corners.");
    }

    private static string InvestigationOriginalSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == CrashRootName || root.name == InvestigationRootName) continue;
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

    private static string InvestigationTerrainSignature(Scene scene)
        => string.Join("\n", scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true))
            .OrderBy(t => GetPath(t.transform)).Select(t => GetPath(t.transform) + "|" + t.terrainData.GetInstanceID() + "|" + EditorJsonUtility.ToJson(t.terrainData)));

    private static void WriteInvestigationVerification(InvestigationNavigationSnapshot snapshot, Scene scene)
    {
        Directory.CreateDirectory(InvestigationPreviews);
        File.WriteAllText(InvestigationPreviews + "/navigation-verification.txt",
            "Old wreck investigation V2 / build verification\nScene=" + scene.path + "\n" +
            "Agents=" + snapshot.Agents + "\nPath probes=" + snapshot.Probes + "\nComplete baseline paths=" + snapshot.Complete +
            "\nNavMesh vertices=" + snapshot.Vertices + "\nGeometry SHA256=" + snapshot.GeometryHash +
            "\nBefore/after geometry and all sampled path results/corners identical.\nOriginal non-wreck serialized components and terrain identical.\n" +
            "No colliders, rigidbodies, obstacles, agents or target authoring on V2.\nActual NavMeshSurface.CollectSources contains no V2 components.\n", Encoding.UTF8);
    }
}

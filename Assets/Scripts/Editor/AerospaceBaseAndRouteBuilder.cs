using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using Gameplay.Agent.Core;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

/// <summary>Visual-only living facilities and sparse survey-route dressing. Does not rebuild navigation.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string BaseLivingRoot = "Aerospace_BaseLivingArea_V1";
    public const string SurveyRouteRoot = "Aerospace_InvestigationRoute_V1";
    public const string BaseRouteAssets = "Assets/Art/Environment/AerospaceBaseAndRoute_V1";
    public const string BaseRouteFontPath = BaseRouteAssets + "/Fonts/BaseRoute_Chinese_SDF.asset";
    public const string BaseRouteCharacters = "航空航天科研基地食堂宿舍生活保障区驻地维护供电通信节点检修设备仪器运输已归档火箭调查遗骸观测路线远程记录非交互 LFDBSITEPWR0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ/-";
    private const string BaseRouteBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_BaseAndRoute_V1.unity";
    private const string BaseRoutePreviews = "UserSettings/ScenePreviews/Aerospace_BaseAndRoute_V1";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Living Base And Survey Route V1")]
    public static void BuildLivingBaseAndSurveyRouteV1()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(BaseRouteBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, BaseLivingRoot) != null || FindByName(scene, SurveyRouteRoot) != null)
            throw new InvalidOperationException("Living base / route already exists; artist edits will not be overwritten.");
        Transform canteen = FindByName(scene, "Zone-员工食堂");
        Transform dorm = FindByName(scene, "Zone-员工宿舍");
        if (canteen == null || dorm == null) throw new InvalidOperationException("Original living zones are required.");
        var before = CaptureInvestigationNavigation(scene);
        string originals = BaseRouteOriginalSignature(scene);
        string terrain = InvestigationTerrainSignature(scene);
        var guard = new BaseRoutePlacementGuard(scene);
        var templates = new List<GameObject>();
        var installed = new List<GameObject>();
        try
        {
            foreach (string section in new[] { "Materials", "Fonts", "Prefabs" }) EnsureAssetFolder(BaseRouteAssets + "/" + section);
            TMP_FontAsset font = CreateBaseRouteFont();
            Material navy = BaseRouteMaterial("Navy", new Color(.065f, .11f, .15f), .18f);
            Material silver = BaseRouteMaterial("Weathered_White", new Color(.69f, .73f, .72f), .16f);
            Material orange = BaseRouteMaterial("Old_Orange", new Color(.72f, .32f, .13f), .04f);
            Material cyan = BaseRouteMaterial("Status_Cyan", new Color(.045f, .34f, .42f), .04f, new Color(.075f, .48f, .54f));
            Material faded = BaseRouteMaterial("Case_Gray", new Color(.33f, .40f, .42f), .12f);
            GameObject living = NewBaseRouteTemplate(BaseLivingRoot);
            GameObject route = NewBaseRouteTemplate(SurveyRouteRoot);
            templates.Add(living);
            templates.Add(route);

            Transform cafeGroup = CreateSection(living.transform, "01_Canteen_LF01");
            PlaceLivingWallSign(cafeGroup, canteen, "Wall (14)", "LF-01", "基地食堂", "生活保障区", 166f, font, navy, orange, silver, guard);
            Transform cafeUtility = guard.Place(cafeGroup, "Canteen_Maintenance_Corner", new Vector2(202f, -73f), 4.7f);
            CreateLivingUtility(cafeUtility, "PWR-01", font, navy, silver, orange, cyan, faded);
            guard.ValidateAndReserve(cafeUtility);

            Transform dormGroup = CreateSection(living.transform, "02_Quarters_LF02");
            PlaceLivingWallSign(dormGroup, dorm, "Wall_23_12_2 (34)", "LF-02", "科研宿舍", "驻地生活区", 196f, font, navy, orange, silver, guard);
            Transform dormUtility = guard.Place(dormGroup, "Quarters_Maintenance_Corner", new Vector2(264f, -185f), 4.7f);
            CreateLivingUtility(dormUtility, "PWR-02", font, navy, silver, orange, cyan, faded);
            guard.ValidateAndReserve(dormUtility);
            Transform comm = guard.Place(dormGroup, "Small_Static_Communications_Node", new Vector2(264f, -114f), 3.0f);
            PlaceModel(comm, "Existing_Kenney_Wireless_Model", SpaceModels + "/machine_wireless.fbx", Vector3.zero, 4f, new Vector3(0, -25, 0));
            SurveyCube(comm, "Equipment_Label", new Vector3(0, 1.1f, -1.62f), new Vector3(2.3f, .70f, .10f), navy);
            DragonText(comm, "Label_CN", "通信节点", new Vector3(0, 1.16f, -1.685f), new Vector2(2.1f, .35f), 2.2f, font);
            DragonText(comm, "Label_ID", "LF-02", new Vector3(0, .87f, -1.685f), new Vector2(2.0f, .22f), 1.35f, font);
            guard.ValidateAndReserve(comm);

            Transform guides = CreateSection(route.transform, "01_Three_Survey_Direction_Signs");
            Vector2[] desired = { new Vector2(244f, -32f), new Vector2(253f, 58f), new Vector2(229f, 116f) };
            string[] names = { "Guide_01_Base_Exit", "Guide_02_Wreck_Junction", "Guide_03_Dragonbone_Approach" };
            Vector3 wreckTarget = new Vector3(291f, 0, 18f), dragonTarget = new Vector3(276f, 0, 162f);
            for (int i = 0; i < desired.Length; i++)
            {
                Transform sign = guard.Place(guides, names[i], desired[i], 4.35f);
                CreateRouteDirectionSign(sign, i, wreckTarget, dragonTarget, font, navy, silver, orange, cyan);
                guard.ValidateAndReserve(sign);
            }

            Transform transport = CreateSection(route.transform, "02_Archived_Instrument_Transport_Cases");
            Transform cases = guard.Place(transport, "Two_Closed_Instrument_Cases_No_Loot", new Vector2(257f, 79f), 3.5f);
            CreateInstrumentCase(cases, "Transport_Case_A", new Vector3(-1.4f, 0, 0), "SITE-07", font, faded, silver, orange, navy);
            CreateInstrumentCase(cases, "Transport_Case_B", new Vector3(1.3f, 0, .35f), "DB-01", font, faded, silver, orange, navy);
            foreach (Transform box in cases) GroundBaseRouteAssembly(box);
            guard.ValidateAndReserve(cases);

            foreach (GameObject template in templates)
            {
                SetSurveyVisualOnly(template);
                ValidateInvestigationVisualRoot(template);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, BaseRouteAssets + "/Prefabs/PFB_" + template.name + ".prefab", out bool saved);
                if (!saved || prefab == null) throw new IOException("Could not save " + template.name);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = template.name;
                installed.Add(instance);
                Object.DestroyImmediate(template);
            }
            templates.Clear();
            VerifyBaseAndRoute(scene);
            AssertInvestigationSnapshotEqual(before, CaptureInvestigationNavigation(scene));
            if (BaseRouteOriginalSignature(scene) != originals || InvestigationTerrainSignature(scene) != terrain)
                throw new InvalidOperationException("Original scene components or terrain changed; formal scene will not be saved.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save formal scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(BaseRoutePreviews);
            File.WriteAllText(BaseRoutePreviews + "/navigation-verification.txt", "Living base / investigation route V1\nAgents=" + before.Agents + "\nPath probes=" + before.Probes + "\nComplete baseline=" + before.Complete + "\nVertices=" + before.Vertices + "\nSHA256=" + before.GeometryHash + "\nAll original serialized components and terrain identical.\nBefore/after navigation geometry, results and path corners identical.\nNo physics, agents, targets, real lights or navigation build sources added.\n", System.Text.Encoding.UTF8);
            Debug.Log("[Base Route V1] PASS: living facilities and 3 survey signs installed; all original objects / terrain retained; path probes=" + before.Probes);
        }
        catch
        {
            foreach (GameObject instance in installed) if (instance != null) Object.DestroyImmediate(instance);
            throw;
        }
        finally { foreach (GameObject template in templates) if (template != null) Object.DestroyImmediate(template); }
    }

    public static void BuildAndRenderLivingBaseAndSurveyRouteV1()
    {
        BuildLivingBaseAndSurveyRouteV1();
        RenderLivingBaseAndSurveyRouteV1();
    }

    private static GameObject NewBaseRouteTemplate(string name)
    {
        var root = new GameObject(name);
        var modifier = root.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;
        return root;
    }

    private static void PlaceLivingWallSign(Transform parent, Transform zone, string wallName, string id, string title, string subtitle, float desiredX, TMP_FontAsset font, Material navy, Material orange, Material silver, BaseRoutePlacementGuard guard)
    {
        Transform wall = FindByName(zone, wallName);
        if (wall == null || wall.GetComponent<Renderer>() == null) throw new InvalidOperationException("Required original wall: " + wallName);
        Bounds bounds = wall.GetComponent<Renderer>().bounds;
        for (int trial = 0; trial < 11; trial++)
        {
            float x = desiredX + (trial == 0 ? 0 : (trial % 2 == 0 ? -1 : 1) * ((trial + 1) / 2) * 4f);
            if (x < bounds.min.x + 4.5f || x > bounds.max.x - 4.5f) continue;
            Vector3 position = new Vector3(x, bounds.max.y - 1.75f, bounds.max.z + .15f);
            if (!guard.ClearRoutes(position, 4.25f)) continue;
            Transform sign = CreateSection(parent, "Wall_Identification_" + id);
            sign.position = position;
            sign.rotation = Quaternion.Euler(0, 180, 0);
            SurveyCube(sign, "Mounted_Navy_Plaque", Vector3.zero, new Vector3(8f, 2.6f, .18f), navy);
            SurveyCube(sign, "Orange_Header", new Vector3(0, 1.17f, -.10f), new Vector3(7.8f, .18f, .03f), orange);
            DragonText(sign, "Base_ID", "航空航天科研基地 / " + id, new Vector3(0, .75f, -.115f), new Vector2(7.6f, .5f), 3.15f, font);
            DragonText(sign, "Title_CN", title, new Vector3(0, -.02f, -.115f), new Vector2(7.2f, .8f), 6f, font);
            DragonText(sign, "Subtitle_CN", subtitle, new Vector3(0, -.87f, -.115f), new Vector2(7.2f, .4f), 3f, font);
            foreach (float bx in new[] { -3.72f, 3.72f }) foreach (float by in new[] { -.97f, .97f }) SurveyCube(sign, "Plaque_Fixing_Bolt", new Vector3(bx, by, -.13f), Vector3.one * .10f, silver);
            guard.ValidateAndReserve(sign, false);
            Debug.Log("[Base Route V1] Mounted " + id + " on retained wall at " + Format(position));
            return;
        }
        throw new InvalidOperationException("No protected-route-safe section of " + wallName);
    }

    private static void CreateLivingUtility(Transform parent, string id, TMP_FontAsset font, Material navy, Material silver, Material orange, Material cyan, Material faded)
    {
        Transform cabinet = CreateSection(parent, "Cabinet_Assembly");
        SurveyCube(cabinet, "Utility_Cabinet_Body", new Vector3(-1.55f, 1.85f, .15f), new Vector3(2.25f, 3.7f, 1.45f), silver);
        SurveyCube(cabinet, "Recessed_Service_Door", new Vector3(-1.55f, 1.85f, -.605f), new Vector3(1.85f, 3.2f, .10f), navy);
        SurveyCube(cabinet, "Safety_Orange_Cap", new Vector3(-1.55f, 3.52f, -.665f), new Vector3(1.9f, .19f, .025f), orange);
        SurveyCube(cabinet, "Cyan_Status_Only", new Vector3(-2.1f, 2.94f, -.675f), new Vector3(.20f, .15f, .025f), cyan);
        SurveyCube(cabinet, "Door_Handle", new Vector3(-.90f, 1.8f, -.72f), new Vector3(.09f, .50f, .08f), silver);
        for (int i = 0; i < 5; i++) SurveyCube(cabinet, "Vent_Slat_" + i, new Vector3(-1.55f, .63f + i * .19f, -.675f), new Vector3(1.35f, .045f, .025f), faded);
        DragonText(cabinet, "Cabinet_ID", id, new Vector3(-1.55f, 2.70f, -.68f), new Vector2(1.65f, .42f), 2.5f, font);
        DragonText(cabinet, "Cabinet_CN", "维护供电", new Vector3(-1.55f, 2.22f, -.68f), new Vector2(1.65f, .34f), 1.95f, font);
        CreateInstrumentCase(parent, "Closed_Maintenance_Case", new Vector3(1.55f, 0, .35f), "检修设备", font, faded, silver, orange, navy);
        SurveyBeam(parent, "Short_Utility_Conduit", new Vector3(-.43f, .22f, .1f), new Vector3(.50f, .22f, .1f), .11f, navy);
        // Face into the open exterior space, not into the original wall.
        parent.rotation = Quaternion.Euler(0, 180, 0);
        GroundBaseRouteAssembly(cabinet);
        GroundBaseRouteAssembly(FindDirectChild(parent, "Closed_Maintenance_Case"));
    }

    private static void GroundBaseRouteAssembly(Transform assembly)
    {
        if (TryGetBounds(assembly.gameObject, out Bounds bounds))
            assembly.position += Vector3.up * (BaseRouteFloorHeight(bounds.center.x, bounds.center.z) + .025f - bounds.min.y);
    }

    private static void CreateInstrumentCase(Transform parent, string name, Vector3 position, string id, TMP_FontAsset font, Material faded, Material silver, Material orange, Material navy)
    {
        Transform box = CreateSection(parent, name);
        box.localPosition = position;
        SurveyCube(box, "Case_Body_No_Loot", new Vector3(0, .78f, 0), new Vector3(2.35f, 1.55f, 1.60f), faded);
        SurveyCube(box, "Sealed_Lid", new Vector3(0, 1.59f, 0), new Vector3(2.44f, .14f, 1.67f), silver);
        foreach (float x in new[] { -.98f, .98f })
        {
            SurveyCube(box, "Corner_Rib", new Vector3(x, .8f, -.83f), new Vector3(.13f, 1.55f, .12f), navy);
            SurveyCube(box, "Closed_Latch", new Vector3(x * .7f, 1.25f, -.855f), new Vector3(.18f, .26f, .08f), silver);
        }
        SurveyCube(box, "Orange_Inventory_Band", new Vector3(0, 1.01f, -.835f), new Vector3(1.64f, .16f, .035f), orange);
        SurveyCube(box, "Recessed_Handle", new Vector3(0, .48f, -.84f), new Vector3(.65f, .13f, .06f), navy);
        DragonText(box, "Inventory_Label", id, new Vector3(0, .75f, -.865f), new Vector2(1.70f, .37f), 2.15f, font);
    }

    private static void CreateRouteDirectionSign(Transform parent, int index, Vector3 wreck, Vector3 dragon, TMP_FontAsset font, Material navy, Material silver, Material orange, Material cyan)
    {
        SurveyCube(parent, "Thin_Post", new Vector3(0, 2.45f, 0), new Vector3(.19f, 4.9f, .19f), silver);
        SurveyCube(parent, "Small_Base", new Vector3(0, .12f, 0), new Vector3(.65f, .24f, .65f), navy);
        SurveyCube(parent, "Survey_Direction_Panel", new Vector3(0, 4.35f, 0), new Vector3(7.4f, 3.0f, .20f), navy);
        // Readable from both directions without repeating lights or creating gates.
        for (int side = 0; side < 2; side++)
        {
            Transform face = CreateSection(parent, side == 0 ? "South_Readable_Face" : "North_Readable_Face");
            face.localPosition = new Vector3(0, 4.35f, side == 0 ? -.12f : .12f);
            face.localRotation = Quaternion.Euler(0, side * 180f, 0);
            SurveyCube(face, "Orange_Route_Header", new Vector3(0, 1.36f, -.01f), new Vector3(7.2f, .16f, .03f), orange);
            DragonText(face, "Header_CN", "基地调查路线", new Vector3(0, .97f, -.04f), new Vector2(6.8f, .45f), 3f, font);
            DragonText(face, "Wreck_Target", "火箭调查区 / SITE-07", new Vector3(-.5f, .25f, -.04f), new Vector2(5.7f, .5f), 3.15f, font);
            DragonText(face, "Dragon_Target", "遗骸观测区 / DB-01", new Vector3(-.5f, -.43f, -.04f), new Vector2(5.7f, .5f), 3.15f, font);
            DragonText(face, "Route_ID", "LF-01 / LF-02 / " + (index + 1).ToString("00"), new Vector3(0, -1.05f, -.04f), new Vector2(6.8f, .28f), 1.8f, font);
            DrawRouteArrow(face, "Wreck_Arrow", new Vector3(2.78f, .25f, -.07f), wreck - parent.position, orange);
            DrawRouteArrow(face, "Dragon_Arrow", new Vector3(2.78f, -.43f, -.07f), dragon - parent.position, cyan);
        }
    }

    private static void DrawRouteArrow(Transform face, string name, Vector3 position, Vector3 worldDirection, Material material)
    {
        Vector3 local = face.InverseTransformDirection(worldDirection);
        Vector3 direction = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? Vector3.right * Mathf.Sign(local.x) : Vector3.up * Mathf.Sign(local.z);
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);
        Transform arrow = CreateSection(face, name);
        SurveyBeam(arrow, "Arrow_Shaft", position - direction * .34f, position + direction * .34f, .065f, material);
        SurveyBeam(arrow, "Arrow_Head_L", position + direction * .34f, position + direction * .08f + perpendicular * .22f, .065f, material);
        SurveyBeam(arrow, "Arrow_Head_R", position + direction * .34f, position + direction * .08f - perpendicular * .22f, .065f, material);
    }

    public static void VerifyBaseAndRoute(Scene scene)
    {
        var corridors = InvestigationRouteCorridors(scene);
        var points = BaseRouteProtectedPoints(scene);
        var guard = new BaseRoutePlacementGuard(scene);
        foreach (string name in new[] { BaseLivingRoot, SurveyRouteRoot })
        {
            Transform root = FindByName(scene, name);
            if (root == null) throw new InvalidOperationException("Missing " + name);
            ValidateInvestigationVisualRoot(root.gameObject);
            AssertNoInvestigationNavigationSources(scene, root);
            if (root.GetComponentsInChildren<Light>(true).Length > 0) throw new InvalidOperationException("No extra real lights are allowed.");
            foreach (Transform group in root) foreach (Transform item in group)
            {
                ValidateSurveyFootprint(item.gameObject, corridors, points);
                if (!item.name.StartsWith("Wall_Identification_", StringComparison.Ordinal)) guard.VerifyOriginalSceneryClearance(item);
            }
        }
    }

    public static string CaptureBaseRouteOriginalSceneState(Scene scene) => BaseRouteOriginalSignature(scene);

    private static List<Vector3> BaseRouteProtectedPoints(Scene scene)
    {
        var points = DragonProtectedPoints(scene);
        points.AddRange(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AgentPawnRoot>(true)).Select(a => a.transform.position));
        return points;
    }

    private static string BaseRouteOriginalSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == BaseLivingRoot || root.name == SurveyRouteRoot) continue;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(GetPath(child) + "|" + child.localPosition.ToString("F5") + "|" + child.localRotation.ToString("F5") + "|" + child.localScale.ToString("F5") + "|" + child.gameObject.activeSelf + "|" + child.gameObject.layer);
                foreach (Component component in child.GetComponents<Component>()) entries.Add(GetPath(child) + "|" + (component == null ? "missing" : component.GetType().FullName + "|" + EditorJsonUtility.ToJson(component)));
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return string.Join("\n", entries);
    }

    private static Material BaseRouteMaterial(string name, Color color, float metallic, Color emission = default)
    {
        string path = BaseRouteAssets + "/Materials/M_BaseRoute_" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_BaseRoute_" + name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .24f);
        if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static TMP_FontAsset CreateBaseRouteFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BaseRouteFontPath);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/text-c.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        font.name = "BaseRoute_Chinese_SDF";
        if (!font.TryAddCharacters(BaseRouteCharacters, out string missing) && !string.IsNullOrEmpty(missing)) throw new InvalidOperationException("Missing label glyphs: " + missing);
        AssetDatabase.CreateAsset(font, BaseRouteFontPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        return font;
    }

    private sealed class BaseRoutePlacementGuard
    {
        private readonly List<Vector3[]> corridors;
        private readonly List<Vector3> points;
        private readonly List<Bounds> originalObstacles = new List<Bounds>();
        private readonly List<Vector3> trees = new List<Vector3>();
        private readonly List<KeyValuePair<Vector2, float>> reserved = new List<KeyValuePair<Vector2, float>>();
        public BaseRoutePlacementGuard(Scene scene)
        {
            corridors = InvestigationRouteCorridors(scene);
            points = BaseRouteProtectedPoints(scene);
            Physics.SyncTransforms();
            foreach (var renderer in scene.GetRootGameObjects().Where(r => r.activeSelf && r.name != BaseLivingRoot && r.name != SurveyRouteRoot && r.name != GameViewNightRoot).SelectMany(r => r.GetComponentsInChildren<MeshRenderer>()))
            {
                Bounds bounds = renderer.bounds;
                if (bounds.size.y < .65f || bounds.size.x > 250f || bounds.size.z > 250f || bounds.max.x < 100f || bounds.min.x > 400f || bounds.max.z < -220f || bounds.min.z > 190f) continue;
                originalObstacles.Add(bounds);
            }
            foreach (Terrain terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
                foreach (TreeInstance tree in terrain.terrainData.treeInstances)
                {
                    Vector3 position = terrain.transform.position + Vector3.Scale(tree.position, terrain.terrainData.size);
                    if (position.x > 100f && position.x < 400f && position.z > -220f && position.z < 190f) trees.Add(position);
                }
        }
        public bool ClearRoutes(Vector3 position, float radius) => SurveyPositionClear(new Vector2(position.x, position.z), radius, corridors, points);
        public void VerifyOriginalSceneryClearance(Transform item)
        {
            if (!TryGetBounds(item.gameObject, out Bounds bounds)) throw new InvalidOperationException("No item bounds: " + item.name);
            Vector2 center = new Vector2(bounds.center.x, bounds.center.z);
            float radius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            float floor = BaseRouteFloorHeight(center.x, center.y);
            if (trees.Any(t => Vector2.Distance(center, new Vector2(t.x, t.z)) < radius + 1.6f) ||
                originalObstacles.Any(b => b.max.y > floor + .7f && DistanceToBoundsXZ(center, b) < radius + .35f))
                throw new InvalidOperationException("Research details overlap original scenery: " + item.name);
        }
        public Transform Place(Transform parent, string name, Vector2 desired, float radius)
        {
            for (int ring = 0; ring <= 8; ring++) for (int i = 0; i < (ring == 0 ? 1 : 24); i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector2 p = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * 2f);
                if (!SurveyPositionClear(p, radius, corridors, points) || reserved.Any(r => Vector2.Distance(p, r.Key) < radius + r.Value + .6f)) continue;
                float y = BaseRouteFloorHeight(p.x, p.y), min = y, max = y;
                foreach (Vector2 offset in new[] { new Vector2(-radius * .65f, 0), new Vector2(radius * .65f, 0), new Vector2(0, -radius * .65f), new Vector2(0, radius * .65f) })
                {
                    float h = BaseRouteFloorHeight(p.x + offset.x, p.y + offset.y);
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                }
                if (max - min > 1.0f) continue;
                if (trees.Any(t => Vector2.Distance(p, new Vector2(t.x, t.z)) < radius + 1.6f)) continue;
                if (originalObstacles.Any(b => b.max.y > max + .70f && DistanceToBoundsXZ(p, b) < radius + .35f)) continue;
                Transform result = CreateSection(parent, name);
                result.position = new Vector3(p.x, y + .035f, p.y);
                Debug.Log("[Base Route V1] Placed " + name + " at " + Format(result.position));
                return result;
            }
            throw new InvalidOperationException("No safe, uncluttered shoulder for " + name + "; scene will not be saved.");
        }
        public void ValidateAndReserve(Transform item, bool reserve = true)
        {
            ValidateSurveyFootprint(item.gameObject, corridors, points);
            if (reserve && TryGetBounds(item.gameObject, out Bounds bounds)) reserved.Add(new KeyValuePair<Vector2, float>(new Vector2(bounds.center.x, bounds.center.z), new Vector2(bounds.extents.x, bounds.extents.z).magnitude));
        }
    }

    private static float DistanceToBoundsXZ(Vector2 point, Bounds bounds)
        => new Vector2(Mathf.Max(bounds.min.x - point.x, 0, point.x - bounds.max.x), Mathf.Max(bounds.min.z - point.y, 0, point.y - bounds.max.z)).magnitude;

    private static float BaseRouteFloorHeight(float x, float z)
    {
        float floor = GroundHeight(x, z);
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(x, 45f, z), Vector3.down, 70f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < .7f || hit.point.y > floor + 3f) continue;
            if (hit.collider.GetComponentInParent<Gameplay.Targets.Authoring.GameplayTargetAuthoringBase>() != null || hit.collider.GetComponentInParent<AgentPawnRoot>() != null) continue;
            floor = Mathf.Max(floor, hit.point.y);
        }
        return floor;
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Render Living Base And Survey Route V1")]
    public static void RenderLivingBaseAndSurveyRouteV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(BaseRoutePreviews);
        string folder = Path.GetFullPath(BaseRoutePreviews);
        Transform living = FindByName(scene, BaseLivingRoot), route = FindByName(scene, SurveyRouteRoot);
        if (living == null || route == null) throw new InvalidOperationException("Install living base and route first.");
        VerifyBaseAndRoute(scene);
        bool livingActive = living.gameObject.activeSelf, routeActive = route.gameObject.activeSelf;
        var cycle = Object.FindObjectOfType<AerospaceDayNightCycle>(true);
        var lighting = AerospaceLightingState.Capture(cycle != null ? cycle.sunLight : null);
        var fill = cycle == null ? new AerospaceFillLightState[0] : cycle.secondaryDirectionalLights.Where(l => l != null).Select(AerospaceFillLightState.Capture).ToArray();
        float phase = cycle != null ? cycle.CurrentPhase : 0;
        Material sky = cycle == null || cycle.skyboxVersion2 == null ? null : new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            living.gameObject.SetActive(true); route.gameObject.SetActive(true);
            RenderCameraPreview(folder + "/base-canteen.png", new Vector3(212f, 46f, 43f), new Vector3(173f, 5f, -43f), 50f);
            RenderCameraPreview(folder + "/base-quarters.png", new Vector3(280f, 48f, -93f), new Vector3(218f, 4f, -156f), 48f);
            RenderCameraPreview(folder + "/base-canteen-sign.png", new Vector3(181f, 15f, 19f), new Vector3(166f, 8f, -9f), 48f);
            Transform utility = FindByName(living, "Canteen_Maintenance_Corner");
            RenderCameraPreview(folder + "/base-maintenance-detail.png", utility.position + new Vector3(20f, 13f, 16f), utility.position + Vector3.up * 1.5f, 46f);
            Transform quartersUtility = FindByName(living, "Quarters_Maintenance_Corner");
            RenderCameraPreview(folder + "/quarters-maintenance-detail.png", quartersUtility.position + new Vector3(12f, 9f, 15f), quartersUtility.position + Vector3.up * 1.5f, 46f);
            RenderCameraPreview(folder + "/route-overview.png", new Vector3(332f, 105f, -52f), new Vector3(247f, 10f, 65f), 54f);
            Transform guide = FindByName(route, "Guide_02_Wreck_Junction");
            RenderCameraPreview(folder + "/route-guide.png", guide.position + new Vector3(5f, 10f, -21f), guide.position + Vector3.up * 4f, 48f);
            living.gameObject.SetActive(false); route.gameObject.SetActive(false);
            RenderCameraPreview(folder + "/base-canteen-before.png", new Vector3(212f, 46f, 43f), new Vector3(173f, 5f, -43f), 50f);
            RenderCameraPreview(folder + "/route-before.png", new Vector3(332f, 105f, -52f), new Vector3(247f, 10f, 65f), 54f);
            living.gameObject.SetActive(true); route.gameObject.SetActive(true);
            if (sky != null)
            {
                cycle.ApplyPhase(.5f, sky);
                RenderCameraPreview(folder + "/route-guide-night.png", guide.position + new Vector3(5f, 10f, -21f), guide.position + Vector3.up * 4f, 48f);
            }
        }
        finally
        {
            living.gameObject.SetActive(livingActive); route.gameObject.SetActive(routeActive);
            if (cycle != null && sky != null) cycle.ApplyPhase(phase, sky);
            lighting.Restore(cycle != null ? cycle.sunLight : null);
            foreach (var state in fill) state.Restore();
            if (sky != null) Object.DestroyImmediate(sky);
        }
        Debug.Log("[Base Route V1] Actual scene previews rendered: " + folder);
        if (Application.isBatchMode) { Selection.activeObject = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
}

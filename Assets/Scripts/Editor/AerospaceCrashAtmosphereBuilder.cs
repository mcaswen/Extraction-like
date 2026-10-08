using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Builds game-camera-readable wreck atmosphere, aerospace skyline and impact response.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string CrashAtmosphereRoot = "Aerospace_CrashAtmosphere_Skyline_V1";
    public const string CrashAtmosphereAssets = "Assets/Art/Environment/AerospaceCrashAtmosphereSkylineV1";
    private const string CrashAtmospherePrefab = CrashAtmosphereAssets + "/Prefabs/PFB_Aerospace_CrashAtmosphere_Skyline_V1.prefab";
    private const string CrashAtmosphereBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_CrashAtmosphere_Skyline_V1.unity";
    private const string CrashAtmospherePreviews = "UserSettings/ScenePreviews/Aerospace_CrashAtmosphere_Skyline_V1";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Crash Atmosphere And Skyline V1")]
    public static void BuildCrashAtmosphereAndSkylineV1()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(CrashAtmosphereBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, CrashAtmosphereRoot) != null)
            throw new InvalidOperationException("Crash atmosphere/skyline V1 already exists; delete it explicitly before rebuilding.");
        Transform wreck = FindByName(scene, InvestigationRootName);
        AerospaceDayNightCycle cycle = FindDayNight(scene);
        if (wreck == null || !wreck.gameObject.activeSelf || cycle == null)
            throw new InvalidOperationException("The active investigation wreck and day/night controller are required.");

        var navigation = CaptureInvestigationNavigation(scene);
        string terrain = InvestigationTerrainSignature(scene);
        string originals = CrashAtmosphereOriginalSignature(scene);
        GameObject template = null;
        GameObject installed = null;
        try
        {
            RemoveLegacyBlueRoadBands(scene);
            foreach (string section in new[] { "Materials", "Meshes", "Prefabs", "Textures" })
                EnsureAssetFolder(CrashAtmosphereAssets + "/" + section);

            Material steam = CrashParticleMaterial("M_CA_CoolingSteam", new Color(.48f, .62f, .65f, .52f), false);
            Material spark = CrashParticleMaterial("M_CA_ElectricalSparks", new Color(1f, .24f, .015f, 1f), true);
            Material scan = CrashTransparentMaterial("M_CA_SurveyScan", new Color(.025f, .62f, .74f, .12f), new Color(.08f, 2.5f, 3.2f), true, false);
            Material warning = CrashOpaqueMaterial("M_CA_HazardBeacon", new Color(.45f, .055f, .008f), .18f, new Color(2.6f, .14f, .008f));
            Material frame = CrashOpaqueMaterial("M_CA_EquipmentFrame", new Color(.055f, .075f, .085f), .42f, Color.black);
            Material vapor = CrashTransparentMaterial("M_CA_StylizedVapor", new Color(.18f, .24f, .27f, .65f), new Color(.025f, .08f, .1f), false, true);
            Material skyline = CrashOpaqueMaterial("M_CA_SkylineMetal", new Color(.37f, .45f, .49f), .48f, new Color(.016f, .026f, .032f));
            Material skylinePanel = CrashOpaqueMaterial("M_CA_SkylineIdentityPanel", new Color(.055f, .28f, .34f), .24f, new Color(.035f, .48f, .62f));
            Material ablation = CrashOpaqueMaterial("M_CA_FractureAblation", new Color(.045f, .062f, .072f), .2f, Color.black);
            Material scorch = CrashOpaqueMaterial("M_CA_ImpactScorch", new Color(.28f, .17f, .09f), .02f, Color.black);
            Material oil = CrashOpaqueMaterial("M_CA_OilResidue", new Color(.12f, .13f, .11f), .22f, Color.black);
            Material ash = CrashOpaqueMaterial("M_CA_CharredFalloff", new Color(.32f, .29f, .21f), 0f, Color.black);

            template = new GameObject(CrashAtmosphereRoot);
            var modifier = template.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            modifier.overrideArea = false;

            Transform atmosphere = CreateSection(template.transform, "01_Wreck_Dynamic_Atmosphere");
            ParticleSystem steamA = CreateSteam(atmosphere, "Cooling_Steam_Broken_Engine", new Vector2(266f, 12f), 4.6f, steam, 1.05f);
            ParticleSystem steamB = CreateSteam(atmosphere, "Cooling_Steam_Stage_Joint", new Vector2(293f, 18f), 6.2f, steam, .9f);
            ParticleSystem sparks = CreateSparks(atmosphere, "Intermittent_Electrical_Sparks", new Vector2(296f, 18f), 3.1f, spark);
            Transform[] vaporPuffs = CreateStylizedVaporPuffs(atmosphere, vapor);
            Transform scanPivot = CreateScanSweep(atmosphere, scan, out Renderer scanRenderer);
            CreateHazardBeacon(atmosphere, "Hazard_Beacon_West", new Vector2(260f, 22f), frame, warning, out Renderer beaconA, out Light lightA);
            CreateHazardBeacon(atmosphere, "Hazard_Beacon_East", new Vector2(322f, 12f), frame, warning, out Renderer beaconB, out Light lightB);
            CreateFractureContrast(atmosphere, ablation, out Light fractureRimLight);

            Transform skylineSection = CreateSection(template.transform, "02_Long_Range_Aerospace_Skyline");
            CreateRadarSkyline(skylineSection, "Long_Range_Radar_West", new Vector2(226f, 93f), -22f, skyline, skylinePanel, warning, true,
                out Transform radarWest, out Renderer skylineBeaconWest);
            CreateRadarSkyline(skylineSection, "Telemetry_Mast_East", new Vector2(342f, 68f), 28f, skyline, skylinePanel, warning, false,
                out Transform radarEast, out Renderer skylineBeaconEast);

            Transform ground = CreateSection(template.transform, "03_Impact_Ground_Response");
            Mesh responseMesh = StoreCrashAtmosphereMesh("Mesh_CA_Impact_Ground_Response", BuildImpactGroundResponse());
            CreateCrashMeshPart(ground, "Directional_Scorch_Oil_And_Charred_Falloff", responseMesh, Vector3.zero, Vector3.zero,
                new[] { scorch, oil, ash, scorch });

            var controller = template.AddComponent<AerospaceCrashAtmosphere>();
            controller.Configure(null, scanPivot, scanRenderer, new[] { beaconA, beaconB }, new[] { lightA, lightB },
                new[] { steamA, steamB }, sparks, vaporPuffs, new[] { radarWest, radarEast },
                new[] { skylineBeaconWest, skylineBeaconEast }, fractureRimLight);
            foreach (Collider collider in template.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            SetSurveyVisualOnly(template);
            ValidateCrashAtmosphereRoot(template, false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, CrashAtmospherePrefab, out bool saved);
            if (!saved || prefab == null) throw new IOException("Could not save crash-atmosphere prefab.");
            Object.DestroyImmediate(template);
            template = null;

            installed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (installed == null) throw new IOException("Could not instantiate crash-atmosphere prefab.");
            installed.name = CrashAtmosphereRoot;
            var installedController = installed.GetComponent<AerospaceCrashAtmosphere>();
            installedController.BindScene(cycle, CreateWreckContrastBindings(wreck));
            PrefabUtility.RecordPrefabInstancePropertyModifications(installedController);

            ValidateCrashAtmosphereRoot(installed, true);
            AssertNoInvestigationNavigationSources(scene, installed.transform);
            AssertNoInvestigationNavigationSources(scene, FindByName(scene, GameViewNightRoot));
            AssertInvestigationSnapshotEqual(navigation, CaptureInvestigationNavigation(scene));
            if (CrashAtmosphereOriginalSignature(scene) != originals || InvestigationTerrainSignature(scene) != terrain)
                throw new InvalidOperationException("Existing scene objects or TerrainData changed; formal scene will not be saved.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save formal scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(CrashAtmospherePreviews);
            File.WriteAllText(CrashAtmospherePreviews + "/navigation-verification.txt",
                "Crash atmosphere, skyline and ground response V1\nAgents=" + navigation.Agents + "\nPath probes=" + navigation.Probes +
                "\nComplete baseline=" + navigation.Complete + "\nVertices=" + navigation.Vertices + "\nSHA256=" + navigation.GeometryHash +
                "\nBefore/after navigation geometry, path results and path corners identical.\nTerrainData and existing scene objects unchanged.\n" +
                "Legacy blue road bands and cyan ground ring removed from prefab, scene and mesh assets.\nNew root has no colliders, rigidbodies, obstacles, agents or gameplay targets and is excluded from all-agent NavMesh builds.\n");
            if (!Application.isBatchMode) Selection.activeGameObject = installed;
            Debug.Log("[Crash Atmosphere/Skyline V1] PASS: particles, scan, hazard beacons, two skyline landmarks and directional ground response installed; blue road bands removed. Navigation probes=" + navigation.Probes);
        }
        catch
        {
            if (installed != null) Object.DestroyImmediate(installed);
            throw;
        }
        finally { if (template != null) Object.DestroyImmediate(template); }
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Render Crash Atmosphere And Skyline V1")]
    public static void RenderCrashAtmosphereAndSkylineV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform root = FindByName(scene, CrashAtmosphereRoot);
        if (root == null) throw new InvalidOperationException("Install crash atmosphere/skyline V1 first.");
        ValidateCrashAtmosphereRoot(root.gameObject, true);
        Directory.CreateDirectory(CrashAtmospherePreviews);
        string folder = Path.GetFullPath(CrashAtmospherePreviews);
        var cycle = FindDayNight(scene);
        var controller = root.GetComponent<AerospaceCrashAtmosphere>();
        var nightController = FindByName(scene, GameViewNightRoot).GetComponent<AerospaceNightGroundLighting>();
        var lighting = AerospaceLightingState.Capture(cycle.sunLight);
        var fills = cycle.secondaryDirectionalLights.Where(light => light != null).Select(AerospaceFillLightState.Capture).ToArray();
        float phase = cycle.CurrentPhase;
        bool active = root.gameObject.activeSelf;
        Material sky = new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            root.gameObject.SetActive(false);
            cycle.ApplyPhase(0f, sky); nightController.ApplyPhase(0f);
            RenderActualGameView(scene, folder + "/gameview-before-atmosphere.png", CrashGameTarget());

            root.gameObject.SetActive(true);
            controller.ApplyPreview(0f, 2.1f);
            SimulateCrashParticles(controller, 2.4f);
            RenderActualGameView(scene, folder + "/gameview-after-day.png", CrashGameTarget());
            RenderActualGameView(scene, folder + "/gameview-ground-response.png", new Vector3(279f, GroundHeight(279f, 15f) + 1.5f, 15f));

            cycle.ApplyPhase(.5f, sky); nightController.ApplyPhase(.5f); controller.ApplyPreview(.5f, 3.6f);
            SimulateCrashParticles(controller, 3.2f);
            RenderActualGameView(scene, folder + "/gameview-after-night.png", CrashGameTarget());
            RenderActualGameView(scene, folder + "/gameview-skyline-night.png", new Vector3(290f, GroundHeight(290f, 42f) + 1.5f, 42f));
        }
        finally
        {
            root.gameObject.SetActive(active);
            cycle.ApplyPhase(phase, sky);
            nightController.ApplyPhase(phase);
            if (active) controller.ApplyPreview(phase, 0f);
            lighting.Restore(cycle.sunLight);
            foreach (var fill in fills) fill.Restore();
            Object.DestroyImmediate(sky);
        }
        Debug.Log("[Crash Atmosphere/Skyline V1] Rendered actual game-view previews: " + folder);
        if (Application.isBatchMode)
        {
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    public static void BuildAndRenderCrashAtmosphereAndSkylineV1()
    {
        BuildCrashAtmosphereAndSkylineV1();
        RenderCrashAtmosphereAndSkylineV1();
    }

    public static void RebuildAndRenderCrashAtmosphereAndSkylineV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform existing = FindByName(scene, CrashAtmosphereRoot);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not remove the generated V1 root before rebuilding.");
        AssetDatabase.DeleteAsset(CrashAtmospherePrefab);
        AssetDatabase.SaveAssets();
        BuildAndRenderCrashAtmosphereAndSkylineV1();
    }

    public static void ValidateCrashAtmosphereRoot(GameObject root, bool requireCycle)
    {
        if (root == null || root.name != CrashAtmosphereRoot) throw new InvalidOperationException("Wrong crash-atmosphere root.");
        ValidateInvestigationVisualRoot(root);
        var controller = root.GetComponent<AerospaceCrashAtmosphere>();
        if (controller == null || (requireCycle && controller.cycle == null)) throw new InvalidOperationException("Missing crash-atmosphere controller or cycle.");
        if (controller.ScanPivot == null || controller.ScanBeam == null || controller.BeaconHeads.Length != 2 || controller.BeaconLights.Length != 2 ||
            controller.SteamSystems.Length != 2 || controller.SparkSystem == null || controller.VaporPuffs.Length != 4 ||
            controller.VaporRenderers.Length != 4 || controller.RadarPivots.Length != 2 || controller.SkylineBeaconHeads.Length != 2 ||
            controller.FractureRimLight == null || (requireCycle && controller.WreckMaterialBindings.Length == 0))
            throw new InvalidOperationException("Incomplete dynamic wreck atmosphere.");
        if (root.transform.Find("02_Long_Range_Aerospace_Skyline").childCount != 2)
            throw new InvalidOperationException("Exactly two distant aerospace skyline landmarks are required.");
        if (root.GetComponentsInChildren<ParticleSystem>(true).Length != 3 ||
            root.GetComponentsInChildren<ParticleSystem>(true).Any(system => system.collision.enabled))
            throw new InvalidOperationException("Particles must remain visual-only with collision disabled.");
        if (controller.BeaconLights.Any(light => light == null || light.shadows != LightShadows.None || light.type != LightType.Point))
            throw new InvalidOperationException("Hazard beacons must use shadowless local lights.");
        if (controller.FractureRimLight.shadows != LightShadows.None || controller.FractureRimLight.type != LightType.Point)
            throw new InvalidOperationException("Fracture rim light must remain a shadowless local light.");
        if (root.transform.Find("03_Impact_Ground_Response/Directional_Scorch_Oil_And_Charred_Falloff") == null)
            throw new InvalidOperationException("Impact ground response is missing.");
    }

    public static string CaptureCrashAtmosphereOriginalState(Scene scene) => CrashAtmosphereOriginalSignature(scene);

    private static Vector3 CrashGameTarget() => new Vector3(290f, GroundHeight(290f, 17f) + 1.5f, 17f);

    private static void SimulateCrashParticles(AerospaceCrashAtmosphere controller, float time)
    {
        foreach (ParticleSystem system in controller.SteamSystems) system.Simulate(time, true, true, true);
        controller.SparkSystem.Simulate(time, true, true, true);
    }

    private static ParticleSystem CreateSteam(Transform parent, string name, Vector2 point, float height, Material material, float scale)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(point.x, GroundHeight(point.x, point.y) + height, point.y);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        go.transform.localScale = Vector3.one * scale;
        var system = go.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 5.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.55f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.3f, 5.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.45f, .57f, .6f, .36f), new Color(.72f, .8f, .8f, .58f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;
        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(3.8f, 6.2f);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 13f;
        shape.radius = .65f;
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(.35f, .75f);
        noise.frequency = .28f;
        noise.scrollSpeed = .12f;
        var color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.68f, .78f, .8f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.8f, .18f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var collision = system.collision;
        collision.enabled = false;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 3;
        return system;
    }

    private static ParticleSystem CreateSparks(Transform parent, string name, Vector2 point, float height, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(point.x, GroundHeight(point.x, point.y) + height, point.y);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var system = go.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = true;
        main.duration = 2.8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(.08f, .22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .18f, .015f), new Color(1f, .72f, .12f));
        main.gravityModifier = 1.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 28;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(.15f, (short)4, (short)8), new ParticleSystem.Burst(1.65f, (short)2, (short)5) });
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = .38f;
        var collision = system.collision;
        collision.enabled = false;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 2.4f;
        renderer.velocityScale = .12f;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 5;
        return system;
    }

    private static Transform[] CreateStylizedVaporPuffs(Transform parent, Material material)
    {
        var data = new[]
        {
            (new Vector2(268.8f, 13.0f), 4.5f, new Vector3(3.1f, 4.5f, 2.8f), -18f),
            (new Vector2(272.4f, 14.1f), 6.0f, new Vector3(2.6f, 4.0f, 2.4f), 24f),
            (new Vector2(290.3f, 17.3f), 5.9f, new Vector3(2.9f, 4.6f, 2.6f), 12f),
            (new Vector2(294.0f, 18.2f), 7.5f, new Vector3(2.3f, 3.8f, 2.2f), -31f)
        };
        Mesh[] variants =
        {
            StoreCrashAtmosphereMesh("Mesh_CA_Vapor_Wisp_A", BuildVaporWisp(.18f, .08f)),
            StoreCrashAtmosphereMesh("Mesh_CA_Vapor_Wisp_B", BuildVaporWisp(-.12f, -.06f))
        };
        var result = new Transform[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            GameObject puff = new GameObject("Soft_Irregular_Vapor_Wisp_" + (i + 1).ToString("00"));
            puff.transform.SetParent(parent, false);
            puff.transform.position = new Vector3(data[i].Item1.x, GroundHeight(data[i].Item1.x, data[i].Item1.y) + data[i].Item2, data[i].Item1.y);
            puff.transform.rotation = Quaternion.Euler(0f, data[i].Item4, 0f);
            puff.transform.localScale = data[i].Item3;
            puff.AddComponent<MeshFilter>().sharedMesh = variants[i % variants.Length];
            var renderer = puff.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 4;
            result[i] = puff.transform;
        }
        return result;
    }

    private static Mesh BuildVaporWisp(float lean, float crown)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int card = 0; card < 3; card++)
        {
            float angle = card * Mathf.PI / 3f;
            Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 drift = new Vector3(lean, 0f, crown) * (1f + card * .14f);
            int start = vertices.Count;
            vertices.Add(-right * .42f);
            vertices.Add(right * .52f);
            vertices.Add(right * .31f + Vector3.up * 1.42f + drift);
            vertices.Add(-right * .24f + Vector3.up * 1.72f + drift * .72f);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        // Two soft crowns make the elongated plume readable from the steep isometric game camera.
        // They are transparent cards, not solid puffs, so the vapor keeps a light silhouette and receives no outline.
        for (int crownCard = 0; crownCard < 2; crownCard++)
        {
            float y = crownCard == 0 ? .58f : 1.18f;
            float radius = crownCard == 0 ? .52f : .38f;
            Vector3 offset = new Vector3(lean * y, 0f, crown * y);
            int start = vertices.Count;
            vertices.Add(offset + new Vector3(-radius * .82f, y, -radius));
            vertices.Add(offset + new Vector3(radius, y + .04f, -radius * .72f));
            vertices.Add(offset + new Vector3(radius * .76f, y, radius));
            vertices.Add(offset + new Vector3(-radius, y - .03f, radius * .68f));
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        float[] heights = { 0f, .48f, 1.08f, 1.7f };
        float[] radii = { .34f, .57f, .43f, .12f };
        const int sides = 8;
        for (int ring = 0; ring < heights.Length - 1; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float a = side * Mathf.PI * 2f / sides;
                float b = (side + 1) * Mathf.PI * 2f / sides;
                float ra = radii[ring] * (1f + Mathf.Sin(side * 2.13f + ring) * .1f);
                float rb = radii[ring] * (1f + Mathf.Sin((side + 1) * 2.13f + ring) * .1f);
                float nextRa = radii[ring + 1] * (1f + Mathf.Sin(side * 2.13f + ring + 1f) * .1f);
                float nextRb = radii[ring + 1] * (1f + Mathf.Sin((side + 1) * 2.13f + ring + 1f) * .1f);
                Vector3 lowerOffset = new Vector3(lean * heights[ring], heights[ring], crown * heights[ring]);
                Vector3 upperOffset = new Vector3(lean * heights[ring + 1], heights[ring + 1], crown * heights[ring + 1]);
                int start = vertices.Count;
                vertices.Add(lowerOffset + new Vector3(Mathf.Cos(a) * ra, 0f, Mathf.Sin(a) * ra));
                vertices.Add(lowerOffset + new Vector3(Mathf.Cos(b) * rb, 0f, Mathf.Sin(b) * rb));
                vertices.Add(upperOffset + new Vector3(Mathf.Cos(b) * nextRb, 0f, Mathf.Sin(b) * nextRb));
                vertices.Add(upperOffset + new Vector3(Mathf.Cos(a) * nextRa, 0f, Mathf.Sin(a) * nextRa));
                float u0 = (float)side / sides;
                float u1 = (float)(side + 1) / sides;
                uv.Add(new Vector2(u0, (float)ring / (heights.Length - 1)));
                uv.Add(new Vector2(u1, (float)ring / (heights.Length - 1)));
                uv.Add(new Vector2(u1, (float)(ring + 1) / (heights.Length - 1)));
                uv.Add(new Vector2(u0, (float)(ring + 1) / (heights.Length - 1)));
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Transform CreateScanSweep(Transform parent, Material material, out Renderer renderer)
    {
        Vector2 origin = new Vector2(312f, 32f);
        Vector2 target = new Vector2(290f, 17f);
        Transform pivot = CreateSection(parent, "Photogrammetry_Scan_Sweep");
        pivot.position = new Vector3(origin.x, GroundHeight(origin.x, origin.y) + 3.35f, origin.y);
        Vector2 aim = target - origin;
        pivot.rotation = Quaternion.Euler(0f, Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg, 0f);
        Mesh mesh = StoreCrashAtmosphereMesh("Mesh_CA_Scan_Wedge", BuildScanWedge());
        renderer = CreateGameViewMesh(pivot, "Low_Translucent_Scan_Fan", mesh, new[] { material, material, material, material });
        return pivot;
    }

    private static Mesh BuildScanWedge()
    {
        var writer = new CrashMeshWriter();
        Vector3 a = new Vector3(-.35f, 0f, .2f);
        Vector3 b = new Vector3(.35f, 0f, .2f);
        Vector3 c = new Vector3(3.2f, -3f, 35f);
        Vector3 d = new Vector3(-3.2f, -3f, 35f);
        writer.Quad(a, b, c, d, 0);
        writer.Quad(d, c, b, a, 0);
        return writer.Finish();
    }

    private static void CreateHazardBeacon(Transform parent, string name, Vector2 point, Material frame, Material warning,
        out Renderer head, out Light light)
    {
        Transform beacon = CreateSection(parent, name);
        beacon.position = new Vector3(point.x, GroundHeight(point.x, point.y) + .05f, point.y);
        SurveyCube(beacon, "Low_Frame", new Vector3(0f, .65f, 0f), new Vector3(.7f, 1.3f, .7f), frame);
        SurveyCube(beacon, "Warning_Head", new Vector3(0f, 1.55f, 0f), new Vector3(1.15f, .42f, 1.15f), warning);
        head = beacon.Find("Warning_Head").GetComponent<Renderer>();
        GameObject lightObject = new GameObject("Pulsed_Local_Light_No_Shadow");
        lightObject.transform.SetParent(beacon, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 0f;
        light.color = new Color(1f, .18f, .035f);
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        lightObject.AddComponent<UniversalAdditionalLightData>();
    }

    private static void CreateRadarSkyline(Transform parent, string name, Vector2 point, float yaw, Material metal,
        Material identityPanel, Material warning, bool largeDish, out Transform radarPivot, out Renderer warningHead)
    {
        Transform site = CreateSection(parent, name);
        site.position = new Vector3(point.x, GroundHeight(point.x, point.y) + .05f, point.y);
        site.rotation = Quaternion.Euler(0f, yaw, 0f);
        GameObject support = PlaceModel(site, "Silhouette_Support", SpaceModels + "/structure_detailed.fbx", Vector3.zero, largeDish ? 9.5f : 10.5f, Vector3.zero);
        Transform pivot = CreateSection(site, largeDish ? "Deep_Space_Radar_Slow_Pivot" : "Telemetry_Dish_Slow_Pivot");
        pivot.localPosition = new Vector3(0f, largeDish ? 13.2f : 15f, 0f);
        GameObject dish = PlaceModel(pivot, largeDish ? "Deep_Space_Radar" : "Telemetry_Dish",
            SpaceModels + (largeDish ? "/satelliteDish_large.fbx" : "/satelliteDish_detailed.fbx"),
            Vector3.zero, largeDish ? 13f : 15f, new Vector3(0f, largeDish ? -25f : 30f, 0f));
        OverrideMaterials(support, metal);
        OverrideMaterials(dish, metal);
        SurveyCube(site, "Signal_Mast", new Vector3(0f, largeDish ? 20f : 23f, 0f), new Vector3(.48f, largeDish ? 12f : 17f, .48f), metal);
        SurveyCube(site, "Cool_Telemetry_Identity_Bar", new Vector3(0f, largeDish ? 14.4f : 13.2f, -.35f),
            new Vector3(largeDish ? 4.6f : 4f, .42f, .32f), identityPanel);
        SurveyCube(site, "Aviation_Warning_Light", new Vector3(0f, largeDish ? 26.2f : 31.8f, 0f), new Vector3(1.05f, .5f, 1.05f), warning);
        warningHead = site.Find("Aviation_Warning_Light").GetComponent<Renderer>();
        radarPivot = pivot;
        if (!largeDish)
        {
            GameObject relay = PlaceModel(site, "Long_Range_Relay", SpaceModels + "/machine_wireless.fbx", new Vector3(4.8f, 0f, 0f), 8f, Vector3.zero);
            OverrideMaterials(relay, metal);
        }
    }

    private static void CreateFractureContrast(Transform parent, Material ablation, out Light rimLight)
    {
        Transform section = CreateSection(parent, "Fracture_Edge_Contrast_And_Local_Rim");
        Mesh ring = StoreCrashAtmosphereMesh("Mesh_CA_Fracture_Ablation_Ring", BuildFractureAblationRing());
        Vector3 axis = new Vector3(.982f, .055f, .18f).normalized;
        var data = new[]
        {
            (new Vector2(268.4f, 12.9f), 4.45f, 3.75f),
            (new Vector2(293.7f, 18.0f), 5.55f, 4.05f)
        };
        for (int i = 0; i < data.Length; i++)
        {
            Transform collar = CreateSection(section, "Dark_Ablation_Fracture_Collar_" + (i + 1).ToString("00"));
            collar.position = new Vector3(data[i].Item1.x, GroundHeight(data[i].Item1.x, data[i].Item1.y) + data[i].Item2, data[i].Item1.y);
            collar.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            collar.localScale = Vector3.one * data[i].Item3;
            collar.gameObject.AddComponent<MeshFilter>().sharedMesh = ring;
            var renderer = collar.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ablation;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        GameObject lightObject = new GameObject("Restrained_Cool_Fracture_Rim_No_Shadow");
        lightObject.transform.SetParent(section, false);
        lightObject.transform.position = new Vector3(294.5f, GroundHeight(294.5f, 18f) + 7.2f, 18f);
        rimLight = lightObject.AddComponent<Light>();
        rimLight.type = LightType.Point;
        rimLight.range = 11f;
        rimLight.intensity = 0f;
        rimLight.color = new Color(.24f, .56f, .78f);
        rimLight.shadows = LightShadows.None;
        rimLight.renderMode = LightRenderMode.ForcePixel;
        lightObject.AddComponent<UniversalAdditionalLightData>();
    }

    private static Mesh BuildFractureAblationRing()
    {
        const int segments = 24;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            float wobbleA = 1f + Mathf.Sin(i * 2.31f) * .07f;
            float wobbleB = 1f + Mathf.Sin((i + 1) * 2.31f) * .07f;
            int start = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a) * wobbleA, 0f, Mathf.Sin(a) * wobbleA));
            vertices.Add(new Vector3(Mathf.Cos(b) * wobbleB, 0f, Mathf.Sin(b) * wobbleB));
            vertices.Add(new Vector3(Mathf.Cos(b) * .72f, 0f, Mathf.Sin(b) * .72f));
            vertices.Add(new Vector3(Mathf.Cos(a) * .72f, 0f, Mathf.Sin(a) * .72f));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static AerospaceCrashAtmosphere.WreckMaterialBinding[] CreateWreckContrastBindings(Transform investigationRoot)
    {
        Transform preservedWreck = investigationRoot.Find("01_Preserved_Wreck_With_Weathering");
        if (preservedWreck == null) return Array.Empty<AerospaceCrashAtmosphere.WreckMaterialBinding>();
        var bindings = new List<AerospaceCrashAtmosphere.WreckMaterialBinding>();
        foreach (Renderer renderer in preservedWreck.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null) continue;
                string name = material.name;
                if (name == "M_Survey_Weathered_Hull")
                    bindings.Add(WreckBinding(renderer, index, new Color(.54f, .62f, .66f), new Color(.42f, .53f, .62f), Color.black, new Color(.006f, .018f, .03f)));
                else if (name == "M_Survey_Faded_Orange")
                    bindings.Add(WreckBinding(renderer, index, new Color(.82f, .27f, .055f), new Color(.72f, .19f, .035f), new Color(.015f, .002f, 0f), new Color(.16f, .018f, .001f)));
                else if (name == "M_Survey_Oxidized_Metal")
                    bindings.Add(WreckBinding(renderer, index, new Color(.07f, .09f, .1f), new Color(.035f, .05f, .07f), Color.black, Color.black));
            }
        }
        return bindings.ToArray();
    }

    private static AerospaceCrashAtmosphere.WreckMaterialBinding WreckBinding(Renderer renderer, int materialIndex,
        Color dayColor, Color nightColor, Color dayEmission, Color nightEmission)
        => new AerospaceCrashAtmosphere.WreckMaterialBinding
        {
            renderer = renderer,
            materialIndex = materialIndex,
            dayColor = dayColor,
            nightColor = nightColor,
            dayEmission = dayEmission,
            nightEmission = nightEmission
        };

    private static void OverrideMaterials(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
    }

    private static Mesh BuildImpactGroundResponse()
    {
        var writer = new CrashMeshWriter();
        AddIrregularGroundDisc(writer, new Vector2(259f, 11f), 3.2f, 1.05f, 0, .135f, 11, .22f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(268f, 12.5f), 3.8f, 1.35f, 2, .13f, 12, .18f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(278f, 14.5f), 4.6f, 1.85f, 0, .135f, 13, .2f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(288f, 17f), 8.2f, 4.5f, 2, .125f, 18, .16f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(292f, 18f), 4.5f, 2.3f, 1, .145f, 14, .22f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(272f, 13.5f), 3.6f, 1.65f, 1, .145f, 13, .28f, 10f);
        AddIrregularGroundDisc(writer, new Vector2(307f, 20f), 4.6f, 2f, 0, .14f, 12, .31f, 10f);
        return writer.Finish();
    }

    private static void AddIrregularGroundDisc(CrashMeshWriter writer, Vector2 center, float radiusX, float radiusZ, int material,
        float lift, int segments, float irregularity, float rotationDegrees)
    {
        Vector3 middle = TerrainPoint(center, lift);
        float rotation = rotationDegrees * Mathf.Deg2Rad;
        Vector2 right = new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation));
        Vector2 forward = new Vector2(-Mathf.Sin(rotation), Mathf.Cos(rotation));
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            float b = (i + 1) * Mathf.PI * 2f / segments;
            float ra = 1f + Mathf.Sin(i * 2.73f + center.x) * irregularity;
            float rb = 1f + Mathf.Sin((i + 1) * 2.73f + center.x) * irregularity;
            Vector2 pa = center + right * (Mathf.Cos(a) * radiusX * ra) + forward * (Mathf.Sin(a) * radiusZ * ra);
            Vector2 pb = center + right * (Mathf.Cos(b) * radiusX * rb) + forward * (Mathf.Sin(b) * radiusZ * rb);
            writer.Triangle(middle, TerrainPoint(pa, lift), TerrainPoint(pb, lift), material);
            writer.Triangle(middle, TerrainPoint(pb, lift), TerrainPoint(pa, lift), material);
        }
    }

    private static void AddTerrainQuad(CrashMeshWriter writer, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int material, float lift)
    {
        Vector3 A = TerrainPoint(a, lift), B = TerrainPoint(b, lift), C = TerrainPoint(c, lift), D = TerrainPoint(d, lift);
        writer.Quad(A, B, C, D, material);
        writer.Quad(D, C, B, A, material);
    }

    private static Vector3 TerrainPoint(Vector2 point, float lift)
        => new Vector3(point.x, GroundHeight(point.x, point.y) + lift, point.y);

    private static Material CrashOpaqueMaterial(string name, Color color, float metallic, Color emission)
    {
        string path = CrashAtmosphereAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .12f);
        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CrashTransparentMaterial(string name, Color color, Color emission, bool additive, bool softTexture)
    {
        string path = CrashAtmosphereAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Unlit");
        ConfigureTransparentMaterial(material, color, additive);
        if (softTexture && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", EnsureParticleDiscTexture());
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CrashParticleMaterial(string name, Color color, bool additive)
    {
        string path = CrashAtmosphereAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        ConfigureTransparentMaterial(material, color, additive);
        Texture2D texture = EnsureParticleDiscTexture();
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color, bool additive)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 1f : 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) material.EnableKeyword("_ALPHAPREMULTIPLY_ON"); else material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.enableInstancing = true;
    }

    private static Texture2D EnsureParticleDiscTexture()
    {
        string path = CrashAtmosphereAssets + "/Textures/T_CA_SoftDisc.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true) { name = "T_CA_SoftDisc", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = (x + .5f) / 16f - 1f;
            float dy = (y + .5f) / 16f - 1f;
            float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            alpha = alpha * alpha * (3f - 2f * alpha);
            pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static Mesh StoreCrashAtmosphereMesh(string name, Mesh generated)
    {
        string path = CrashAtmosphereAssets + "/Meshes/" + name + ".asset";
        generated.name = name;
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }
        EditorUtility.CopySerialized(generated, existing);
        Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static string CrashAtmosphereOriginalSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == CrashAtmosphereRoot || root.name == GameViewNightRoot) continue;
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

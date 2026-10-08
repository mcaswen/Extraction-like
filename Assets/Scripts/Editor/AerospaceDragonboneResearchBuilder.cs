using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using Gameplay.Targets.Authoring;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    public const string DragonResearchRoot = "Aerospace_Dragonbone_ResearchDetails_V1";
    public const string DragonResearchAssets = "Assets/Art/Environment/DragonboneResearchDetails_V1";
    public const string DragonResearchFontPath = DragonResearchAssets + "/Fonts/Dragonbone_Chinese_SDF.asset";
    public const string DragonResearchCharacters = "龙骨遗骸观测区取样点远程调查记录 DB-01SENSOR AB0123456789/-";
    private const string DragonResearchPreviews = "UserSettings/ScenePreviews/DragonboneResearchDetails_V1";
    private const string DragonResearchBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_Dragonbone_ResearchDetails_V1.unity";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Dragonbone Research Details V1")]
    public static void BuildDragonboneResearchDetailsV1()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(DragonResearchBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, DragonResearchRoot) != null) throw new InvalidOperationException("Research details already exist; no artist changes will be overwritten.");
        Transform station = FindByName(scene, LayoutRootName);
        Transform arena = FindByName(scene, "tripo_convert_cfc50981-c500-4787-94ae-f41149c942c9");
        if (station == null || arena == null) throw new InvalidOperationException("Original arena and moved station are required.");
        var navBefore = CaptureInvestigationNavigation(scene);
        string originalSignature = DragonOriginalSignature(scene);
        string terrainSignature = InvestigationTerrainSignature(scene);
        var corridors = InvestigationRouteCorridors(scene);
        var points = DragonProtectedPoints(scene);
        var surface = new DragonSurfaceSampler(arena);
        GameObject template = null;
        GameObject installed = null;
        try
        {
            EnsureAssetFolder(DragonResearchAssets + "/Materials");
            EnsureAssetFolder(DragonResearchAssets + "/Fonts");
            EnsureAssetFolder(DragonResearchAssets + "/Prefabs");
            var font = CreateDragonResearchFont();
            var navy = DragonMaterial("Navy", new Color(.055f, .10f, .14f), .18f);
            var metal = DragonMaterial("Weathered_Silver", new Color(.52f, .61f, .63f), .25f);
            var orange = DragonMaterial("Faded_Orange", new Color(.72f, .33f, .13f), .05f);
            var cyan = DragonMaterial("Status_Cyan", new Color(.035f, .36f, .44f), .05f, new Color(.08f, .55f, .62f));
            template = new GameObject(DragonResearchRoot);
            var modifier = template.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
            template.AddComponent<AerospaceResearchLightingOverride>();

            Transform sign = DragonSafePlacement(template.transform, "01_Entry_Research_Sign", new Vector2(275f, 160f), 4.6f, surface, corridors, points);
            sign.localRotation = Quaternion.Euler(0f, -15f, 0f);
            SurveyCube(sign, "Left_Support", new Vector3(-2.2f, 2.25f, 0), new Vector3(.18f, 4.5f, .18f), metal);
            SurveyCube(sign, "Right_Support", new Vector3(2.2f, 2.25f, 0), new Vector3(.18f, 4.5f, .18f), metal);
            SurveyCube(sign, "Research_Panel", new Vector3(0, 3.5f, 0), new Vector3(7.2f, 3.6f, .22f), navy);
            SurveyCube(sign, "Header_Band", new Vector3(0, 5.13f, -.13f), new Vector3(6.95f, .20f, .04f), orange);
            DragonText(sign, "Title_CN", "龙骨遗骸观测区", new Vector3(0, 4.48f, -.14f), new Vector2(6.6f, .8f), 5.6f, font);
            DragonText(sign, "Site_ID", "DB-01 / 远程调查记录", new Vector3(0, 3.72f, -.14f), new Vector2(6.6f, .55f), 3.4f, font);
            DragonText(sign, "Footer_CN", "遗骸取样 / 远程观测", new Vector3(0, 2.05f, -.14f), new Vector2(6.6f, .45f), 3.1f, font);
            // A small code-native skeleton diagram, not another large skeleton model.
            Vector3 diagram = new Vector3(0, 2.86f, -.17f);
            SurveyBeam(sign, "Diagram_Spine", diagram + new Vector3(-1.75f, 0, 0), diagram + new Vector3(1.55f, 0, 0), .055f, cyan);
            for (int i = 0; i < 5; i++)
            {
                float x = -1.15f + i * .48f;
                SurveyBeam(sign, "Diagram_Rib_Upper_" + i, diagram + new Vector3(x, .02f, 0), diagram + new Vector3(x + .17f, .40f, 0), .045f, cyan);
                SurveyBeam(sign, "Diagram_Rib_Lower_" + i, diagram + new Vector3(x, -.02f, 0), diagram + new Vector3(x + .17f, -.40f, 0), .045f, cyan);
            }
            SurveyBeam(sign, "Diagram_Head", diagram + new Vector3(1.45f, 0, 0), diagram + new Vector3(2.05f, .26f, 0), .16f, cyan);
            ValidateSurveyFootprint(sign.gameObject, corridors, points);

            Transform tags = CreateSection(template.transform, "02_Low_Sampling_Tags");
            Vector2[] tagPositions = { new Vector2(278, 184), new Vector2(356, 224), new Vector2(274, 280), new Vector2(326, 300) };
            for (int i = 0; i < tagPositions.Length; i++)
            {
                Transform tag = DragonSafePlacement(tags, "Sample_Tag_" + (i + 1).ToString("00"), tagPositions[i], 1.25f, surface, corridors, points);
                tag.localRotation = Quaternion.Euler(0, -15f, 0);
                SurveyCube(tag, "Ground_Clamp", new Vector3(0, .13f, 0), new Vector3(1.65f, .26f, .8f), metal);
                SurveyCube(tag, "Low_Support", new Vector3(0, .58f, 0), new Vector3(.12f, .9f, .12f), navy);
                Transform plate = CreateSection(tag, "Tilted_Sampling_Plate");
                plate.localPosition = new Vector3(0, 1.1f, 0);
                plate.localRotation = Quaternion.Euler(20, 0, 0);
                SurveyCube(plate, "Number_Plate", Vector3.zero, new Vector3(1.65f, 1.0f, .09f), navy);
                SurveyCube(plate, "Orange_Header", new Vector3(0, .40f, -.055f), new Vector3(1.6f, .12f, .025f), orange);
                DragonText(plate, "Sample_Number", "取样点 " + (i + 1).ToString("00"), new Vector3(0, .05f, -.06f), new Vector2(1.5f, .55f), 2.3f, font);
                DragonText(plate, "Site_ID", "DB-01", new Vector3(0, -.30f, -.06f), new Vector2(1.5f, .25f), 1.6f, font);
                ValidateSurveyFootprint(tag.gameObject, corridors, points);
            }

            Transform probes = CreateSection(template.transform, "03_Fixed_Scanning_Probes_No_AI");
            Vector2[] probePositions = { new Vector2(257, 274), new Vector2(362, 264) };
            for (int i = 0; i < probePositions.Length; i++)
            {
                Transform probe = DragonSafePlacement(probes, "Fixed_Probe_" + (i == 0 ? "A" : "B"), probePositions[i], 2.6f, surface, corridors, points);
                Vector3 aim = new Vector3(307.8f, probe.position.y, 232f) - probe.position;
                probe.rotation = Quaternion.LookRotation(-aim.normalized, Vector3.up);
                for (int j = 0; j < 3; j++)
                {
                    float angle = j * Mathf.PI * 2f / 3f;
                    Vector3 foot = new Vector3(Mathf.Cos(angle) * 1.5f, .08f, Mathf.Sin(angle) * 1.5f);
                    SurveyBeam(probe, "Tripod_Leg_" + j, foot, new Vector3(0, 3.05f, 0), .13f, metal);
                    SurveyCube(probe, "Tripod_Foot_" + j, foot, new Vector3(.40f, .13f, .40f), navy);
                }
                SurveyCube(probe, "Fixed_Scanner_Head", new Vector3(0, 3.5f, 0), new Vector3(1.7f, .9f, 1.25f), metal);
                SurveyCube(probe, "Dark_Lens_Frame", new Vector3(0, 3.5f, -.65f), new Vector3(1.4f, .65f, .10f), navy);
                SurveyCube(probe, "Optical_Status_No_Real_Light", new Vector3(0, 3.5f, -.72f), new Vector3(1.08f, .33f, .025f), cyan);
                DragonText(probe, "Probe_ID", "SENSOR " + (i == 0 ? "A" : "B"), new Vector3(0, 4.12f, -.15f), new Vector2(2.8f, .35f), 1.6f, font);
                ValidateSurveyFootprint(probe.gameObject, corridors, points);
                DragonCableBranch(template.transform, probe.position, i, surface, corridors, points, navy, orange);
            }
            SetSurveyVisualOnly(template);
            ValidateInvestigationVisualRoot(template);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, DragonResearchAssets + "/Prefabs/PFB_Dragonbone_ResearchDetails_V1.prefab", out bool saved);
            if (!saved || prefab == null) throw new IOException("Research prefab could not be saved.");
            Object.DestroyImmediate(template);
            template = null;
            installed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            installed.name = DragonResearchRoot;
            var lighting = installed.GetComponent<AerospaceResearchLightingOverride>();
            string[] lightNames = { "Core_CyanLight", "Sensor_CyanLight", "Approach_WarmLight" };
            Light[] lights = lightNames.Select(n => FindByName(station, n).GetComponent<Light>()).ToArray();
            lighting.Configure(lights, new[] { 90f, 55f, 30f });
            PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
            lighting.Restore();
            if (DragonOriginalSignature(scene) != originalSignature || InvestigationTerrainSignature(scene) != terrainSignature)
                throw new InvalidOperationException("Original objects or terrain changed; formal scene will not be saved.");
            lighting.Apply();
            foreach (Light light in lights) PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            VerifyDragonResearch(scene);
            AssertInvestigationSnapshotEqual(navBefore, CaptureInvestigationNavigation(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save formal scene.");
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode) Selection.activeGameObject = installed;
            Debug.Log("[Dragon Research] PASS: new details only; moved station unchanged; original scene components/terrain identical except three reversible local light intensities. Probes=" + navBefore.Probes + ", vertices=" + navBefore.Vertices + ", SHA256=" + navBefore.GeometryHash + ", backup=" + DragonResearchBackup);
        }
        catch
        {
            if (installed != null) Object.DestroyImmediate(installed);
            throw;
        }
        finally { if (template != null) Object.DestroyImmediate(template); }
    }

    public static void BuildAndRenderDragonboneResearchV1()
    {
        BuildDragonboneResearchDetailsV1();
        RenderDragonboneResearchV1();
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Render Dragonbone Research Details V1")]
    public static void RenderDragonboneResearchV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform root = FindByName(scene, DragonResearchRoot);
        if (root == null) throw new InvalidOperationException("Research details have not been installed.");
        Directory.CreateDirectory(DragonResearchPreviews);
        string folder = Path.GetFullPath(DragonResearchPreviews);
        var cycle = Object.FindObjectOfType<AerospaceDayNightCycle>(true);
        var lightingState = AerospaceLightingState.Capture(cycle != null ? cycle.sunLight : null);
        var fillStates = cycle == null ? new AerospaceFillLightState[0] : cycle.secondaryDirectionalLights.Where(l => l != null).Select(AerospaceFillLightState.Capture).ToArray();
        bool originalActive = root.gameObject.activeSelf;
        float originalPhase = cycle != null ? cycle.CurrentPhase : 0;
        Material previewSky = cycle != null && cycle.skyboxVersion2 != null ? new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave } : null;
        try
        {
            Vector3 close = new Vector3(276f, 15f, 149f), target = new Vector3(279f, 11.5f, 165f);
            root.gameObject.SetActive(true);
            RenderCameraPreview(folder + "/dragon-research-entry.png", close, target, 52f);
            RenderCameraPreview(folder + "/dragon-research-entry-context.png", new Vector3(254f, 28f, 125f), new Vector3(287f, 16f, 166f), 52f);
            RenderCameraPreview(folder + "/dragon-research-wide.png", new Vector3(202.8f, 116f, 107f), new Vector3(307.8f, 29f, 232f), 48f);
            RenderCameraPreview(folder + "/dragon-research-probes.png", new Vector3(225f, 38f, 306f), new Vector3(287f, 13f, 270f), 48f);
            RenderCameraPreview(folder + "/dragon-research-station.png", new Vector3(158f, 41f, 88f), new Vector3(192f, 20f, 136f), 52f);
            root.gameObject.SetActive(false);
            RenderCameraPreview(folder + "/dragon-research-before.png", close, target, 52f);
            RenderCameraPreview(folder + "/dragon-research-station-before.png", new Vector3(158f, 41f, 88f), new Vector3(192f, 20f, 136f), 52f);
            root.gameObject.SetActive(true);
            if (cycle != null && previewSky != null)
            {
                cycle.ApplyPhase(.5f, previewSky);
                RenderCameraPreview(folder + "/dragon-research-night.png", close, target, 52f);
            }
        }
        finally
        {
            root.gameObject.SetActive(originalActive);
            if (cycle != null && previewSky != null) cycle.ApplyPhase(originalPhase, previewSky);
            lightingState.Restore(cycle != null ? cycle.sunLight : null);
            foreach (var state in fillStates) state.Restore();
            if (previewSky != null) Object.DestroyImmediate(previewSky);
        }
        // Preview rendering can add URP components; never save those changes into the formal scene.
        Debug.Log("[Dragon Research] Rendered actual scene previews: " + folder);
        if (Application.isBatchMode)
        {
            Selection.activeObject = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    public static void VerifyDragonResearch(Scene scene)
    {
        Transform root = FindByName(scene, DragonResearchRoot);
        if (root == null) throw new InvalidOperationException("Research root not found.");
        ValidateInvestigationVisualRoot(root.gameObject);
        AssertNoInvestigationNavigationSources(scene, root);
        var corridors = InvestigationRouteCorridors(scene);
        var points = DragonProtectedPoints(scene);
        ValidateSurveyFootprint(FindDirectChild(root, "01_Entry_Research_Sign").gameObject, corridors, points);
        foreach (string section in new[] { "02_Low_Sampling_Tags", "03_Fixed_Scanning_Probes_No_AI" })
            foreach (Transform item in FindDirectChild(root, section)) ValidateSurveyFootprint(item.gameObject, corridors, points);
        foreach (Transform child in root)
            if (child.name.StartsWith("04_Cable_Branch_", StringComparison.Ordinal))
                foreach (Transform part in child) ValidateSurveyFootprint(part.gameObject, corridors, points);
        if (root.GetComponentsInChildren<Light>(true).Length != 0) throw new InvalidOperationException("Research details must not add real lights.");
    }

    private static List<Vector3> DragonProtectedPoints(Scene scene)
    {
        var points = InvestigationInteractionPoints(scene);
        foreach (ResourceClusterAuthoring cluster in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ResourceClusterAuthoring>(true)))
            foreach (var member in cluster.ResourceMembers)
                if (member.EntityObject != null && TryGetBounds(member.EntityObject, out Bounds bounds)) points.Add(bounds.center);
        foreach (var source in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemySourceClusterAuthoring>(true)))
            foreach (Transform spawn in source.SpawnPoints) if (spawn != null) points.Add(spawn.position);
        return points;
    }

    private static Transform DragonSafePlacement(Transform parent, string name, Vector2 desired, float radius, DragonSurfaceSampler surface, List<Vector3[]> corridors, List<Vector3> points)
    {
        for (int ring = 0; ring <= 10; ring++)
            for (int i = 0; i < (ring == 0 ? 1 : 24); i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                Vector2 candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring * 2f;
                if (!SurveyPositionClear(candidate, radius, corridors, points)) continue;
                float y = surface.Height(candidate.x, candidate.y);
                float[] heights = { y, surface.Height(candidate.x - radius * .5f, candidate.y), surface.Height(candidate.x + radius * .5f, candidate.y), surface.Height(candidate.x, candidate.y - radius * .5f), surface.Height(candidate.x, candidate.y + radius * .5f) };
                if (heights.Max() - heights.Min() > 1.2f) continue;
                Transform result = CreateSection(parent, name);
                result.position = new Vector3(candidate.x, heights.Max() + .04f, candidate.y);
                Debug.Log("[Dragon Research] Placed " + name + " at " + Format(result.position));
                return result;
            }
        throw new InvalidOperationException("No safe, level shoulder for " + name);
    }

    private static void DragonCableBranch(Transform parent, Vector3 origin, int index, DragonSurfaceSampler surface, List<Vector3[]> corridors, List<Vector3> points, Material navy, Material orange)
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI * 2f / 24f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3[] samples = new Vector3[9];
            bool safe = true;
            for (int j = 0; j < samples.Length; j++)
            {
                Vector3 p = origin + direction * (2f + j * .5f);
                p.y = surface.Height(p.x, p.z) + .11f;
                samples[j] = p;
                if (!SurveyPositionClear(new Vector2(p.x, p.z), .65f, corridors, points) || (j > 0 && Mathf.Abs(p.y - samples[j - 1].y) > .45f)) safe = false;
            }
            if (!safe) continue;
            Transform branch = CreateSection(parent, "04_Cable_Branch_" + index);
            for (int j = 1; j < samples.Length; j++) SurveyBeam(branch, "Flush_Cable_" + j, samples[j - 1], samples[j], .09f, navy);
            Vector3 end = samples[samples.Length - 1];
            SurveyCube(branch, "Small_Junction_Box", end + Vector3.up * .25f, new Vector3(.7f, .5f, .7f), navy);
            SurveyCube(branch, "Junction_ID_Strip", end + new Vector3(0, .51f, 0), new Vector3(.6f, .025f, .18f), orange);
            return;
        }
        throw new InvalidOperationException("No safe cable shoulder for probe " + index);
    }

    private static Material DragonMaterial(string name, Color color, float metallic, Color emission = default)
    {
        string path = DragonResearchAssets + "/Materials/M_DragonResearch_" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_DragonResearch_" + name };
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", .28f);
        if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static TMP_FontAsset CreateDragonResearchFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DragonResearchFontPath);
        if (existing != null) return existing;
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/text-c.ttf");
        var font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, false);
        font.name = "Dragonbone_Chinese_SDF";
        if (!font.TryAddCharacters(DragonResearchCharacters, out string missing) && !string.IsNullOrEmpty(missing)) throw new InvalidOperationException("Research sign font is missing: " + missing);
        AssetDatabase.CreateAsset(font, DragonResearchFontPath);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        return font;
    }

    private static void DragonText(Transform parent, string name, string value, Vector3 position, Vector2 size, float fontSize, TMP_FontAsset font)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshPro>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.85f, .92f, .9f);
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = size;
        text.transform.localPosition = position;
        text.ForceMeshUpdate();
    }

    private static string DragonOriginalSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == DragonResearchRoot) continue;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(GetPath(child) + "|" + child.localPosition.ToString("F5") + "|" + child.localRotation.ToString("F5") + "|" + child.localScale.ToString("F5") + "|" + child.gameObject.activeSelf + "|" + child.gameObject.layer);
                foreach (Component component in child.GetComponents<Component>()) entries.Add(GetPath(child) + "|" + (component == null ? "missing" : component.GetType().FullName + "|" + EditorJsonUtility.ToJson(component)));
            }
        }
        entries.Sort(StringComparer.Ordinal);
        return string.Join("\n", entries);
    }

    /// <summary>Read-only sampling of the original visible floor, which sits above the terrain.</summary>
    private sealed class DragonSurfaceSampler
    {
        private sealed class Surface { public Bounds bounds; public Vector3[] vertices; public int[] triangles; }
        private readonly List<Surface> surfaces = new List<Surface>();
        public DragonSurfaceSampler(Transform arena)
        {
            foreach (MeshFilter filter in arena.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;
                surfaces.Add(new Surface { bounds = filter.GetComponent<Renderer>().bounds, vertices = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(), triangles = filter.sharedMesh.triangles });
            }
        }
        public float Height(float x, float z)
        {
            float height = GroundHeight(x, z);
            foreach (Surface surface in surfaces)
            {
                if (x < surface.bounds.min.x || x > surface.bounds.max.x || z < surface.bounds.min.z || z > surface.bounds.max.z) continue;
                for (int i = 0; i < surface.triangles.Length; i += 3)
                {
                    Vector3 a = surface.vertices[surface.triangles[i]], b = surface.vertices[surface.triangles[i + 1]], c = surface.vertices[surface.triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    if (Mathf.Abs(normal.y) < .45f) continue;
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < .00001f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                    float w = 1f - u - v;
                    if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                    float y = u * a.y + v * b.y + w * c.y;
                    if (y <= 28f && y > height) height = y;
                }
            }
            return height;
        }
    }
}

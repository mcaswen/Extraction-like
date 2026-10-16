using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Install the visual-only emergency-landed Wenchang vehicle. Never rebakes navigation.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string LandingRootName = "Aerospace_Wenchang_EmergencyLanding_V1";
    public const string LandingAssets = "Assets/Art/Environment/WenchangLanding";
    const string LandingPrefab = LandingAssets + "/Prefabs/PFB_Wenchang_EmergencyLanding.prefab";
    const string LandingPreviewFolder = "UserSettings/ScenePreviews/WenchangLanding";
    const string LandingMasterHash = "ee7a98b5609b1d747bdeb42510375d8978d6e00032dbf9f8bbe4618a49e03c61";

    [MenuItem("Tools/Extraction-like/Aerospace/Wenchang Landing/Build Scene Replacement")]
    public static void BuildWenchangLanding()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, LandingRootName) != null)
            throw new InvalidOperationException("迫降版本已安装。使用对比菜单切换，不重复创建。");
        EnsureSceneBackup("UserSettings/SceneBackups/Scenezl_Final 1_Before_WenchangLanding.unity");
        var cycle = FindDayNight(scene);
        if (cycle == null || Mathf.Abs(cycle.cycleSeconds - 160) > .01f) throw new InvalidOperationException("Expected existing 160-second cycle.");
        var old = LandingLegacyVisuals(scene);
        string originals = LandingOriginalSignature(scene, old.Select(x => x.target).ToArray());
        string terrain = InvestigationTerrainSignature(scene);
        var before = CaptureInvestigationNavigation(scene);
        GameObject root = null;
        try
        {
            foreach (string folder in new[] { "Materials", "Meshes", "Textures", "Prefabs" }) EnsureAssetFolder(LandingAssets + "/" + folder);
            root = new GameObject(LandingRootName);
            var modifier = root.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
            var site = root.AddComponent<AerospaceLandingSite>();
            site.sourceMasterSha256 = LandingMasterHash;
            site.vehicle = CreateSection(root.transform, "01_Wenchang01_Landed_Vehicle");
            site.vehicle.position = new Vector3(290, 0, 17);
            // FBX maps the Blender front to +Z; face the damaged shield toward the follow camera.
            site.vehicle.rotation = Quaternion.Euler(0, 200, 0);
            site.vehicle.localScale = Vector3.one * 1.35f;
            var levels = new LOD[3];
            for (int i = 0; i < 3; i++)
            {
                string path = LandingAssets + "/Models/WC01_Landing_LOD" + i + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new FileNotFoundException(path);
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.addCollider = false; importer.importAnimation = false;
                importer.importCameras = false; importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import; importer.isReadable = true;
                importer.globalScale = 1; importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), site.vehicle);
                model.name = "LOD" + i;
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    string category = renderer.name.Split('_')[1];
                    renderer.sharedMaterial = LandingVehicleMaterial(category);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
                levels[i] = new LOD(i == 0 ? .36f : i == 1 ? .16f : .012f, renderers);
            }
            site.vehicleLods = site.vehicle.gameObject.AddComponent<LODGroup>();
            site.vehicleLods.fadeMode = LODFadeMode.None;
            site.vehicleLods.SetLODs(levels); site.vehicleLods.RecalculateBounds();
            var footAnchors = site.vehicle.Find("LOD0").GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Anchor_Foot_")).OrderBy(t => t.name).ToArray();
            if (footAnchors.Length != 4) throw new InvalidOperationException("Four FBX landing foot anchors are required.");
            // A level landing stance rests on small compressed-earth berms, not modified TerrainData.
            float baseHeight = footAnchors.Max(t => GroundHeight(t.position.x, t.position.z)) + .10f;
            site.vehicle.position += Vector3.up * baseHeight;
            site.groundContactPoints = footAnchors.Select(t => t.position - Vector3.up * (.14f * 1.35f)).ToArray();
            var ground = CreateSection(root.transform, "02_Local_Landing_Scorch_And_Foot_Contact");
            var soil = LandingSimpleMaterial("M_Compacted_Soil", new Color(.20f, .185f, .13f));
            for (int i = 0; i < footAnchors.Length; i++) CreateLandingBerm(ground, site.groundContactPoints[i], i, soil);
            CreateLandingGroundScorch(ground);
            ValidateSurveyFootprint(site.vehicle.gameObject, InvestigationRouteCorridors(scene), InvestigationInteractionPoints(scene));

            var effects = CreateSection(root.transform, "03_Restrained_Grey_Smoke");
            var smokeAnchors = site.vehicle.Find("LOD0").GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Anchor_Smoke_")).OrderBy(t => t.name).ToArray();
            if (smokeAnchors.Length != 2) throw new InvalidOperationException("Two FBX smoke anchors are required.");
            Vector3[] outlets = smokeAnchors.Select(t => {
                Vector3 radial = t.position - site.vehicle.position; radial.y = 0;
                return t.position + radial.normalized * 1.1f;
            }).ToArray();
            site.smoke = outlets.Select((p, i) => CreateLandingSmoke(effects, p, i)).ToArray();
            site.smokeWisps = outlets.Select((p, i) => CreateLandingSmokeWisp(effects, p, i)).ToArray();
            var lighting = CreateSection(root.transform, "04_Low_Inspection_Fill_No_Shadow");
            site.inspectionLights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var lamp = new GameObject("Inspection_Fill_" + i); lamp.transform.SetParent(lighting, false);
                lamp.transform.position = site.vehicle.position + new Vector3(i == 0 ? -4 : 5, 8, -7);
                var light = lamp.AddComponent<Light>(); light.type = LightType.Point;
                light.range = 19; light.color = i == 0 ? new Color(.72f, .84f, 1f) : new Color(1, .78f, .52f);
                light.shadows = LightShadows.None; light.intensity = 0;
                lamp.AddComponent<UniversalAdditionalLightData>(); site.inspectionLights[i] = light;
            }
            var oldSign = old.First(x => x.target.name == "Record_Text").target;
            var sign = Object.Instantiate(oldSign, root.transform); sign.name = "05_Landing_Survey_Record_Text";
            sign.transform.SetPositionAndRotation(oldSign.transform.position, oldSign.transform.rotation);
            sign.transform.localScale = oldSign.transform.lossyScale;
            sign.GetComponent<TMP_Text>().text = "WC-01\nLANDING SITE\nREMOTE SURVEY";
            SetSurveyVisualOnly(root);
            ValidateInvestigationVisualRoot(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LandingPrefab, out bool saved);
            if (!saved) throw new IOException("Landing prefab did not save.");
            Object.DestroyImmediate(root);
            root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            root.name = LandingRootName;
            site = root.GetComponent<AerospaceLandingSite>();
            site.cycle = cycle; site.previousVisuals = old; site.ApplyPhase(cycle.startPhase);
            PrefabUtility.RecordPrefabInstancePropertyModifications(site);
            SetLandingVersion(site, true);
            ValidateWenchangLanding(scene);
            AssertInvestigationSnapshotEqual(before, CaptureInvestigationNavigation(scene));
            if (terrain != InvestigationTerrainSignature(scene) || originals != LandingOriginalSignature(scene, old.Select(x => x.target).ToArray()))
                throw new InvalidOperationException("An unrelated scene object or terrain changed. Refusing to save.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Landing scene did not save.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(LandingPreviewFolder);
            File.WriteAllText(LandingPreviewFolder + "/navigation-verification.json", JsonUtility.ToJson(before, true));
            Debug.Log("WENCHANG_LANDING_INSTALLED Agents=" + before.Agents + " probes=" + before.Probes + " navHash=" + before.GeometryHash + "; unrelated scene and terrain unchanged.");
        }
        catch
        {
            foreach (var prior in old) if (prior.target != null) prior.target.SetActive(prior.active);
            if (root != null) Object.DestroyImmediate(root);
            throw;
        }
    }

    static AerospaceLandingSite.PreviousVisual[] LandingLegacyVisuals(Scene scene)
    {
        var result = new List<AerospaceLandingSite.PreviousVisual>();
        void Add(string root, string path)
        {
            var item = FindByName(scene, root)?.Find(path);
            if (item == null) throw new InvalidOperationException("Missing legacy visual: " + root + "/" + path);
            result.Add(new AerospaceLandingSite.PreviousVisual { target = item.gameObject, active = item.gameObject.activeSelf });
        }
        foreach (string path in new[] {
            "01_Preserved_Wreck_With_Weathering/01_Main_Crashed_Rocket", "01_Preserved_Wreck_With_Weathering/02_Impact_Debris_Field",
            "01_Preserved_Wreck_With_Weathering/04_Scorch_And_Impact_Trail", "01_Preserved_Wreck_With_Weathering/05_Local_Wreck_Readability_Lights",
            "02_Aged_Hull_Surface_Details", "06_Old_Earth_Traces_Under_Returning_Grass", "05_Roadside_Survey_Record/Record_Text" }) Add(InvestigationRootName, path);
        foreach (string path in new[] { "Cooling_Steam_Broken_Engine", "Cooling_Steam_Stage_Joint", "Intermittent_Electrical_Sparks",
            "Soft_Irregular_Vapor_Wisp_01", "Soft_Irregular_Vapor_Wisp_02", "Soft_Irregular_Vapor_Wisp_03", "Soft_Irregular_Vapor_Wisp_04",
            "Photogrammetry_Scan_Sweep", "Fracture_Edge_Contrast_And_Local_Rim" }) Add(CrashAtmosphereRoot, "01_Wreck_Dynamic_Atmosphere/" + path);
        Add(CrashAtmosphereRoot, "03_Impact_Ground_Response"); Add(GameViewNightRoot, "02_Wreck_Top_View_Composition");
        return result.ToArray();
    }

    public static void SetLandingVersion(AerospaceLandingSite site, bool landed)
    {
        foreach (var previous in site.previousVisuals)
            if (previous.target != null)
            {
                previous.target.SetActive(landed ? false : previous.active);
                PrefabUtility.RecordPrefabInstancePropertyModifications(previous.target);
            }
        site.gameObject.SetActive(landed);
        PrefabUtility.RecordPrefabInstancePropertyModifications(site.gameObject);
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Wenchang Landing/Show Upright Landing")]
    static void ShowWenchangLanding() => ToggleLandingMenu(true);
    [MenuItem("Tools/Extraction-like/Aerospace/Wenchang Landing/Compare Previous Horizontal Wreck")]
    static void ComparePreviousWreck() => ToggleLandingMenu(false);
    static void ToggleLandingMenu(bool landed)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
        var site = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AerospaceLandingSite>(true)).Single();
        Undo.RecordObjects(site.previousVisuals.Select(x => (Object)x.target).Append(site.gameObject).ToArray(), "Compare Wenchang landing");
        SetLandingVersion(site, landed); EditorSceneManager.MarkSceneDirty(site.gameObject.scene);
    }

    public static void ValidateWenchangLanding(Scene scene)
    {
        var root = FindByName(scene, LandingRootName);
        if (root == null || !root.gameObject.activeSelf) throw new InvalidOperationException("Active landing root is missing.");
        ValidateInvestigationVisualRoot(root.gameObject); AssertNoInvestigationNavigationSources(scene, root);
        var site = root.GetComponent<AerospaceLandingSite>();
        if (site.cycle == null || site.vehicle == null || site.vehicleLods == null || site.groundContactPoints.Length != 4 || site.previousVisuals.Length != 18)
            throw new InvalidOperationException("Incomplete landing site references.");
        if (site.previousVisuals.Any(x => x.target == null || x.target.activeSelf)) throw new InvalidOperationException("Conflicting legacy wreck remains visible.");
        var lods = site.vehicleLods.GetLODs();
        if (lods.Length != 3 || lods.Any(l => l.renderers.Length != 10)) throw new InvalidOperationException("Expected three ten-renderer LODs.");
        int previous = int.MaxValue;
        foreach (var lod in lods)
        {
            int triangles = lod.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            if (triangles >= previous || triangles > 150000) throw new InvalidOperationException("LOD triangle budget exceeded.");
            previous = triangles;
            foreach (var renderer in lod.renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.colors.Length != mesh.vertexCount) throw new InvalidOperationException("Missing baked vertex surface colors.");
                if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new InvalidOperationException("Landing surface shader failed compilation.");
            }
        }
        TryGetBounds(site.vehicle.gameObject, out Bounds bounds);
        if (bounds.size.y < 30 || bounds.size.y < bounds.size.x * 2) throw new InvalidOperationException("Vehicle is not upright or has wrong FBX scale.");
        if (site.smoke.Length != 2 || site.smoke.Any(p => p == null || p.collision.enabled || p.main.maxParticles > 32 || p.emission.rateOverTime.constant > 3))
            throw new InvalidOperationException("Smoke exceeds restrained visual budget.");
        if (site.inspectionLights.Length != 2 || site.inspectionLights.Any(l => l.shadows != LightShadows.None))
            throw new InvalidOperationException("Landing inspection lighting must be shadowless.");
        ValidateSurveyFootprint(site.vehicle.gameObject, InvestigationRouteCorridors(scene), InvestigationInteractionPoints(scene));
    }

    static Material LandingVehicleMaterial(string key)
    {
        string path = LandingAssets + "/Materials/M_WC01_" + key + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Aerospace/Landing Surface")); AssetDatabase.CreateAsset(material, path); }
        float metallic = key == "Steel" ? .72f : key == "WarmMetal" ? .65f : key == "Trim" || key == "Dark" ? .28f : .08f;
        float smooth = key == "Steel" ? .64f : key == "WarmMetal" ? .53f : key == "Trim" ? .48f : .40f;
        if (key == "Ink" || key.StartsWith("Flag")) { metallic = 0; smooth = .32f; }
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(material); return material;
    }

    static Material LandingSimpleMaterial(string name, Color color)
    {
        string path = LandingAssets + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .08f); EditorUtility.SetDirty(material); return material;
    }

    static void StoreLandingMesh(Transform parent, string name, Mesh mesh, Material material)
    {
        string path = LandingAssets + "/Meshes/" + name + ".asset";
        var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (stored == null) AssetDatabase.CreateAsset(mesh, path);
        else { EditorUtility.CopySerialized(mesh, stored); Object.DestroyImmediate(mesh); mesh = stored; }
        EditorUtility.SetDirty(mesh);
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CreateLandingBerm(Transform parent, Vector3 contact, int index, Material soil)
    {
        var vertices = new List<Vector3>(); var indices = new List<int>(); const int count = 20;
        for (int ring = 0; ring < 2; ring++) for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2 / count; float radius = ring == 0 ? 1.40f : 2.1f + .1f * Mathf.Sin(i * 4.3f);
            float x = contact.x + Mathf.Cos(a) * radius, z = contact.z + Mathf.Sin(a) * radius;
            vertices.Add(new Vector3(x, ring == 0 ? contact.y - .025f : GroundHeight(x, z) + .035f, z));
        }
        vertices.Add(contact - Vector3.up * .025f);
        for (int i = 0; i < count; i++)
        {
            int j = (i + 1) % count;
            indices.AddRange(new[] { count * 2, j, i, i, j, j + count, i, j + count, i + count });
        }
        var mesh = new Mesh { name = "Compacted earth under landing foot" }; mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        StoreLandingMesh(parent, "Landing_Foot_Contact_" + index, mesh, soil);
    }

    static Texture2D LandingCloudTexture()
    {
        string path = LandingAssets + "/Textures/T_Landing_SoftCloud.png";
        if (!File.Exists(path))
        {
            const int size = 128; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                float radial = Mathf.Clamp01(1 - p.sqrMagnitude);
                float noise = .55f * Mathf.PerlinNoise(x * .055f + 5.7f, y * .055f + 2.1f) + .45f * Mathf.PerlinNoise(x * .13f, y * .13f);
                float alpha = Mathf.Pow(radial, 1.8f) * Mathf.SmoothStep(.05f, 1, noise);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material LandingTransparentMaterial(string name, Color color)
    {
        string path = LandingAssets + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Aerospace/Landing Soft Smoke")); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find("Aerospace/Landing Soft Smoke");
        material.SetTexture("_BaseMap", LandingCloudTexture()); material.SetColor("_BaseColor", color);
        material.SetFloat("_VertexColor", name == "M_Landing_Grey_Smoke" ? 1 : 0);
        material.SetFloat("_Motion", name == "M_Landing_Soft_Wisp" ? 1 : 0);
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material); return material;
    }

    static void CreateLandingGroundScorch(Transform parent)
    {
        const int size = 20; var mesh = new Mesh(); var vertices = new Vector3[(size + 1) * (size + 1)]; var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
        for (int z = 0; z <= size; z++) for (int x = 0; x <= size; x++)
        {
            int i = z * (size + 1) + x; float wx = 290 + (x / (float)size - .5f) * 17, wz = 17 + (z / (float)size - .5f) * 17;
            vertices[i] = new Vector3(wx, GroundHeight(wx, wz) + .075f, wz); uv[i] = new Vector2(x / (float)size, z / (float)size);
            if (x < size && z < size) triangles.AddRange(new[] { i, i + size + 1, i + 1, i + 1, i + size + 1, i + size + 2 });
        }
        mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        StoreLandingMesh(parent, "Local_Engine_Scorch_No_Skid_Trail", mesh, LandingTransparentMaterial("M_Landing_Scorch", new Color(.035f, .027f, .021f, .90f)));
    }

    static ParticleSystem CreateLandingSmoke(Transform parent, Vector3 point, int index)
    {
        var go = new GameObject(index == 0 ? "Grey_Smoke_Damaged_Service_Shield" : "Grey_Smoke_Booster_Cooling_Vent"); go.transform.SetParent(parent, false); go.transform.position = point;
        var p = go.AddComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = p.main; main.loop = true; main.playOnAwake = true; main.duration = 8; main.prewarm = true;
        main.maxParticles = index == 0 ? 28 : 22; main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 6.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.30f, .55f); main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(.45f, .46f, .47f, .38f);
        var emission = p.emission; emission.rateOverTime = index == 0 ? 2.4f : 1.6f;
        var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .22f;
        var velocity = p.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = .55f; velocity.y = .65f; velocity.z = -.10f;
        var noise = p.noise; noise.enabled = true; noise.strength = .18f; noise.frequency = .35f; noise.scrollSpeed = .18f;
        var size = p.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 1.6f));
        var color = p.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .17f), new GradientAlphaKey(.48f, .55f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var collision = p.collision; collision.enabled = false; var trigger = p.trigger; trigger.enabled = false;
        var renderer = p.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = LandingTransparentMaterial("M_Landing_Grey_Smoke", Color.white);
        renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false; renderer.sortMode = ParticleSystemSortMode.Distance;
        p.useAutoRandomSeed = false; p.randomSeed = (uint)(27101 + index); return p;
    }

    static Renderer CreateLandingSmokeWisp(Transform parent, Vector3 point, int index)
    {
        // A tiny crossed-card core keeps smoke readable in the editor and steep gameplay view.
        // No depth/shadow pass: it never becomes an outlined solid cloud or gameplay occluder.
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int card = 0; card < 3; card++)
        {
            float angle = card * Mathf.PI / 3; Vector3 right = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            int n = vertices.Count;
            vertices.Add(-right * 1.15f); vertices.Add(right * 1.15f);
            vertices.Add(right * 2.10f + new Vector3(5f, 12.6f, -.6f)); vertices.Add(-right * 1.65f + new Vector3(5f, 12.6f, -.6f));
            uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string name = "Slow_Grey_Wisp_" + index;
        StoreLandingMesh(parent, name, mesh, LandingTransparentMaterial("M_Landing_Soft_Wisp", new Color(.4f, .41f, .41f, .42f)));
        var wisp = parent.Find(name); wisp.position = point - Vector3.up * .6f;
        if (index == 1) wisp.localScale = new Vector3(-.8f, .8f, .8f);
        return wisp.GetComponent<Renderer>();
    }

    static string LandingOriginalSignature(Scene scene, GameObject[] toggleTargets)
    {
        var toggles = new HashSet<GameObject>(toggleTargets); var entries = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == LandingRootName) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(GetPath(t) + "|" + t.gameObject.layer + "|" + (toggles.Contains(t.gameObject) ? "allowed-toggle" : t.gameObject.activeSelf.ToString()));
                foreach (var c in t.GetComponents<Component>()) entries.Add(GetPath(t) + "|" + (c == null ? "missing" : c.GetType().FullName + "|" + EditorJsonUtility.ToJson(c)));
            }
        }
        entries.Sort(StringComparer.Ordinal); return string.Join("\n", entries);
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Wenchang Landing/Render Actual Game Camera")]
    public static void RenderWenchangLanding()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ValidateWenchangLanding(scene);
        var site = FindByName(scene, LandingRootName).GetComponent<AerospaceLandingSite>(); var cycle = site.cycle;
        var lighting = AerospaceLightingState.Capture(cycle.sunLight);
        var fills = cycle.secondaryDirectionalLights.Where(l => l != null).Select(AerospaceFillLightState.Capture).ToArray();
        var sky = new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        Directory.CreateDirectory(LandingPreviewFolder);
        var oldAtmosphere = FindByName(scene, CrashAtmosphereRoot).GetComponent<AerospaceCrashAtmosphere>();
        var night = FindByName(scene, GameViewNightRoot).GetComponent<AerospaceNightGroundLighting>();
        try
        {
            foreach (float phase in new[] { 0f, .5f })
            {
                cycle.ApplyPhase(phase, sky); site.ApplyPhase(phase); oldAtmosphere.ApplyPreview(phase, 0); night.ApplyPhase(phase);
                foreach (var p in site.smoke)
                {
                    p.Simulate(5.5f, true, true, true);
                    Debug.Log("LANDING_SMOKE " + p.name + " count=" + p.particleCount + " anchor=" + p.transform.position + " bounds=" + p.GetComponent<Renderer>().bounds);
                }
                string label = phase == 0 ? "day" : "night";
                RenderActualGameView(scene, LandingPreviewFolder + "/gameview-landing-" + label + ".png", new Vector3(290, GroundHeight(290, 17) + 1.5f, 17));
                RenderActualGameView(scene, LandingPreviewFolder + "/gameview-approach-" + label + ".png", new Vector3(270, GroundHeight(270, 4) + 1.5f, 4));
                if (phase == 0) RenderCameraPreview(LandingPreviewFolder + "/landing-closeup.png", new Vector3(323, 28, -31), site.vehicle.position + Vector3.up * 16, 45);
            }
        }
        finally
        {
            cycle.ApplyPhase(cycle.startPhase, sky); site.ApplyPhase(cycle.startPhase); night.ApplyPhase(cycle.startPhase); oldAtmosphere.ApplyPreview(cycle.startPhase, 0);
            foreach (var p in site.smoke) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            lighting.Restore(cycle.sunLight); foreach (var fill in fills) fill.Restore(); Object.DestroyImmediate(sky);
        }
        Debug.Log("WENCHANG_LANDING_PREVIEWS_COMPLETE " + Path.GetFullPath(LandingPreviewFolder));
        if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    public static void BuildAndRenderWenchangLanding() { BuildWenchangLanding(); RenderWenchangLanding(); }
}

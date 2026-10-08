using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AerospaceSceneLayoutBuilder
{
    private const string CrashRootName = "Aerospace_RocketCrash_V1";
    private const string CrashAssets = "Assets/Art/Environment/AerospaceRocketCrash";
    private const string CrashPrefabPath = CrashAssets + "/Prefabs/PFB_RocketCrash_And_Debris_V1.prefab";
    private const string CrashBackupPath = "UserSettings/SceneBackups/Scenezl_Final 1_Before_RocketCrash_V1.unity";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Rocket Crash V1")]
    public static void BuildRocketCrashV1()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        ExtractionLike.Editor.AerospaceAssetSetup.RefreshForBatchmode();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureSceneBackup(CrashBackupPath);
        EnsureAssetFolder(CrashAssets + "/Meshes");
        EnsureAssetFolder(CrashAssets + "/Materials");
        EnsureAssetFolder(CrashAssets + "/Prefabs");

        Material white = EnsureCrashMaterial("M_Crash_Hull_OffWhite", new Color(0.83f, 0.89f, 0.94f), 0.12f, 0.22f);
        Material orange = EnsureCrashMaterial("M_Crash_Safety_Orange", new Color(1f, 0.32f, 0.055f), 0.08f, 0.20f);
        Material burnt = EnsureCrashMaterial("M_Crash_Burnt_Metal", new Color(0.12f, 0.14f, 0.16f), 0.10f, 0.12f);
        Material interior = EnsureCrashMaterial("M_Crash_Interior_Dark", new Color(0.045f, 0.065f, 0.08f), 0.05f, 0.10f);
        Material scorch = EnsureCrashMaterial("M_Crash_Ground_Scorch", new Color(0.11f, 0.09f, 0.065f), 0f, 0.05f);
        Material[] hullMaterials = { white, orange, burnt, interior };

        Mesh longTube = StoreCrashMesh("Mesh_Wreck_Jagged_MainHull", BuildBrokenTube(4.1f, 18f, 0.42f, 16, 0.3f));
        Mesh upperTube = StoreCrashMesh("Mesh_Wreck_Jagged_UpperStage", BuildBrokenTube(3.85f, 10f, 0.35f, 16, 2.1f));
        Mesh nose = StoreCrashMesh("Mesh_Wreck_HeadCone", BuildCone(3.85f, 10.5f, 16));
        Mesh tornPanel = StoreCrashMesh("Mesh_Wreck_Torn_HullPanel", BuildTornPanel());
        Mesh fin = StoreCrashMesh("Mesh_Wreck_Detached_Fin", BuildTornFin());

        GameObject template = new GameObject(CrashRootName);
        try
        {
            Transform site = CreateSection(template.transform, "01_Main_Crashed_Rocket");
            site.position = new Vector3(290f, 0f, 17f);
            site.rotation = Quaternion.Euler(0f, 80f, 0f);

            GameObject tail = PlaceModel(site, "Tail_Engine_With_Fins", SpaceModels + "/rocket_baseA.fbx", new Vector3(0f, 0f, -19f), 8.2f, new Vector3(90f, 0f, 10f));
            SetCrashPieceOnGround(tail, -0.35f);
            GameObject mainHull = CreateCrashMeshPart(site, "MainHull_Broken_Open", longTube, new Vector3(0f, 4f, -4.5f), new Vector3(-6f, 0f, -5f), hullMaterials);
            SetCrashPieceOnGround(mainHull, -0.4f);
            GameObject upper = CreateCrashMeshPart(site, "UpperStage_Separated", upperTube, new Vector3(-1.6f, 4f, 12f), new Vector3(6f, -12f, 13f), hullMaterials);
            SetCrashPieceOnGround(upper, -0.45f);
            GameObject head = CreateCrashMeshPart(site, "HeadCone_Tipped_Into_Ground", nose, new Vector3(-3.3f, 3f, 18.8f), new Vector3(12f, -15f, -9f), hullMaterials);
            SetCrashPieceOnGround(head, -0.5f);

            Transform booster = CreateSection(site, "Detached_Side_Booster");
            booster.localPosition = new Vector3(9f, 0f, -9f);
            booster.localRotation = Quaternion.Euler(0f, -18f, 0f);
            GameObject boosterHull = CreateCrashMeshPart(booster, "BoosterHull", upperTube, new Vector3(0f, 2f, 0f), new Vector3(0f, 0f, 20f), hullMaterials);
            boosterHull.transform.localScale = new Vector3(0.45f, 0.45f, 1.15f);
            SetCrashPieceOnGround(boosterHull, -0.2f);
            GameObject boosterTip = CreateCrashMeshPart(booster, "BoosterHead", nose, new Vector3(0f, 1.7f, 6f), Vector3.zero, hullMaterials);
            boosterTip.transform.localScale = Vector3.one * 0.45f;
            SetCrashPieceOnGround(boosterTip, -0.12f);

            Transform localDebris = CreateSection(template.transform, "02_Impact_Debris_Field");
            PlaceCrashDebris(localDebris, "Impact_TornFin_A", fin, hullMaterials, 276f, 34f, 0.95f, new Vector3(13f, 20f, -12f));
            PlaceCrashDebris(localDebris, "Impact_TornFin_B", fin, hullMaterials, 304f, 2f, 0.8f, new Vector3(8f, 135f, 18f));
            PlaceCrashDebris(localDebris, "Impact_Panel_A", tornPanel, hullMaterials, 310f, 38f, 1.2f, new Vector3(8f, 45f, 82f));
            PlaceCrashDebris(localDebris, "Impact_Panel_B", tornPanel, hullMaterials, 268f, 4f, 0.85f, new Vector3(15f, 120f, 95f));
            PlaceCrashDebris(localDebris, "Impact_Panel_C", tornPanel, hullMaterials, 322f, 13f, 0.65f, new Vector3(-9f, -60f, 105f));
            PlaceCrashModel(localDebris, "Impact_Stage_Coupling", "rocket_fuelA", 279f, 4f, 5.5f, new Vector3(67f, 32f, 18f));
            PlaceCrashModel(localDebris, "Impact_Broken_Collar", "rocket_finsA", 302f, 34f, 4f, new Vector3(93f, 45f, -8f));

            Transform mapDebris = CreateSection(template.transform, "03_Scattered_Rocket_Parts_Across_Map");
            Transform roadside = CreateSection(mapDebris, "A_Canteen_Road_Shoulder");
            PlaceCrashModel(roadside, "Detached_HeadCone", "rocket_topA", 247f, 42f, 4.8f, new Vector3(82f, -25f, 12f));
            PlaceCrashDebris(roadside, "Torn_Hull_Sheet", tornPanel, hullMaterials, 243f, 46f, 0.6f, new Vector3(5f, 50f, 83f));
            Transform quarters = CreateSection(mapDebris, "B_Crew_Quarters_Approach");
            PlaceCrashModel(quarters, "Fuel_Section", "rocket_fuelA", 240f, -99f, 4.8f, new Vector3(75f, 70f, 8f));
            PlaceCrashDebris(quarters, "Detached_Fin", fin, hullMaterials, 246f, -92f, 0.65f, new Vector3(8f, 100f, 16f));
            Transform dragonApproach = CreateSection(mapDebris, "C_Dragonbone_Research_Approach");
            PlaceCrashModel(dragonApproach, "Engine_Coupling", "rocket_finsA", 240f, 131f, 4.4f, new Vector3(90f, -30f, 15f));
            PlaceCrashDebris(dragonApproach, "Torn_Hull_Sheet", tornPanel, hullMaterials, 236f, 137f, 0.75f, new Vector3(10f, 125f, 87f));
            Transform village = CreateSection(mapDebris, "D_Village_Eastern_Approach");
            PlaceCrashDebris(village, "Detached_Fin", fin, hullMaterials, -113f, -5f, 0.65f, new Vector3(5f, 30f, 4f));
            PlaceCrashModel(village, "Stage_Rim", "rocket_fuelA", -107f, -12f, 3.8f, new Vector3(72f, 110f, -13f));

            Transform marks = CreateSection(template.transform, "04_Scorch_And_Impact_Trail");
            CreateGroundScorch(marks, "Main_Impact_Burn", new Vector2(290f, 17f), 28f, 12f, 80f, 0.7f, scorch);
            CreateGroundScorch(marks, "Tail_Skid_Mark", new Vector2(263f, 11f), 19f, 4f, 80f, 2.4f, scorch);
            CreateGroundScorch(marks, "Booster_Burn", new Vector2(283f, 27f), 9f, 3.5f, 62f, 4.1f, scorch);

            Transform lighting = CreateSection(template.transform, "05_Local_Wreck_Readability_Lights");
            CreatePointLight(lighting, "Wreck_Cool_Fill", new Vector3(289f, GroundHeight(289f, 17f) + 15f, 17f), new Color(0.7f, 0.85f, 1f), 65f, 35f);
            CreatePointLight(lighting, "Wreck_Warm_Residual_Glow", new Vector3(279f, GroundHeight(279f, 17f) + 6f, 17f), new Color(1f, 0.35f, 0.09f), 15f, 13f);

            ValidateRocketCrashRoot(template);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, CrashPrefabPath, out bool saved);
            if (!saved || prefab == null)
            {
                throw new IOException("Failed to save rocket-crash prefab; formal scene left unchanged.");
            }
            Object.DestroyImmediate(template);
            template = null;
            Transform existing = FindByName(scene, CrashRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new IOException("Could not instantiate rocket-crash prefab.");
            }
            instance.name = CrashRootName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Rocket Crash V1] Built main wreck, detached booster, 7 impact parts and 8 map-scattered parts. Backup={CrashBackupPath}");
        }
        finally
        {
            if (template != null)
            {
                Object.DestroyImmediate(template);
            }
        }
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Remove Rocket Crash V1")]
    public static void RemoveRocketCrashV1()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform crash = FindByName(scene, CrashRootName);
        if (crash != null)
        {
            Object.DestroyImmediate(crash.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log("[Rocket Crash V1] Removed only the rocket wreck and scattered parts.");
    }

    public static void BuildAndRenderRocketCrashV1()
    {
        BuildRocketCrashV1();
        RenderRocketCrashV1();
    }

    public static void RenderRocketCrashV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform crash = FindByName(scene, CrashRootName);
        if (crash == null)
        {
            throw new System.InvalidOperationException("No rocket crash layout found.");
        }
        ValidateRocketCrashRoot(crash.gameObject);
        Transform mainWreck = FindDirectChild(crash, "01_Main_Crashed_Rocket");
        if (mainWreck != null && TryGetBounds(mainWreck.gameObject, out Bounds mainBounds))
        {
            Debug.Log($"[Rocket Crash V1 Bounds] mainCenter={Format(mainBounds.center)} mainSize={Format(mainBounds.size)}");
        }
        RenderCameraPreview("/tmp/extraction-rocket-crash-v1-wide.png", new Vector3(387f, 90f, -57f), new Vector3(290f, 8f, 18f), 47f);
        RenderCameraPreview("/tmp/extraction-rocket-crash-v1-close.png", new Vector3(332f, 39f, -19f), new Vector3(291f, 9f, 18f), 48f);
        RenderCameraPreview("/tmp/extraction-rocket-crash-v1-road.png", new Vector3(216f, 47f, 110f), new Vector3(276f, 6f, 26f), 48f);
        RenderCameraPreview("/tmp/extraction-rocket-crash-v1-overview.png", new Vector3(80f, 680f, -590f), new Vector3(30f, 5f, 40f), 48f);
    }

    public static void VerifyRocketCrashRoundTrip()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string baselineObjects = GetNonCrashTransformSignature(scene);
        RemoveRocketCrashV1();
        scene = SceneManager.GetActiveScene();
        if (FindByName(scene, CrashRootName) != null || GetNonCrashTransformSignature(scene) != baselineObjects)
        {
            throw new System.InvalidOperationException("Rocket crash removal affected the existing scene objects.");
        }
        BuildRocketCrashV1();
        scene = SceneManager.GetActiveScene();
        if (GetNonCrashTransformSignature(scene) != baselineObjects)
        {
            throw new System.InvalidOperationException("Rocket crash rebuild affected existing scene object transforms or active states.");
        }
        RenderRocketCrashV1();
        RenderCameraPreview("/tmp/extraction-rocket-debris-map-v1.png", new Vector3(275f, 40f, 7f), new Vector3(245f, 5f, 43f), 45f);
        RenderCameraPreview("/tmp/extraction-rocket-debris-quarters-v1.png", new Vector3(278f, 48f, -71f), new Vector3(243f, 2f, -96f), 43f);
        RenderCameraPreview("/tmp/extraction-rocket-debris-dragon-v1.png", new Vector3(220f, 52f, 106f), new Vector3(239f, 6f, 136f), 44f);
        RenderCameraPreview("/tmp/extraction-rocket-debris-village-v1.png", new Vector3(-80f, 42f, 24f), new Vector3(-110f, 3f, -7f), 44f);
        Debug.Log("[Rocket Crash V1 Roundtrip] Remove/rebuild passed. All other hierarchy transforms, active states and component counts retained.");
    }

    private static string GetNonCrashTransformSignature(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == CrashRootName) continue;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(GetPath(child) + "|" + child.localPosition.ToString("F5") + "|" + child.localRotation.ToString("F5") + "|" + child.localScale.ToString("F5") + "|" + child.gameObject.activeSelf + "|" + child.gameObject.GetComponents<Component>().Length);
            }
        }
        entries.Sort(System.StringComparer.Ordinal);
        return string.Join("\n", entries);
    }

    private static Material EnsureCrashMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = CrashAssets + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new System.InvalidOperationException("URP/Lit is required for the rocket wreck.");
        }
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh StoreCrashMesh(string name, Mesh generated)
    {
        string path = CrashAssets + "/Meshes/" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        generated.name = name;
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

    private static GameObject CreateCrashMeshPart(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 rotation, Material[] materials)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(rotation);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return part;
    }

    private static void SetCrashPieceOnGround(GameObject piece, float embed)
    {
        if (TryGetBounds(piece, out Bounds bounds))
        {
            float ground = GroundHeight(bounds.center.x, bounds.center.z);
            piece.transform.position += Vector3.up * (ground + embed - bounds.min.y);
        }
    }

    private static void PlaceCrashDebris(Transform parent, string name, Mesh mesh, Material[] materials, float x, float z, float scale, Vector3 rotation)
    {
        GameObject part = CreateCrashMeshPart(parent, name, mesh, new Vector3(x, 0f, z), rotation, materials);
        part.transform.localScale = Vector3.one * scale;
        SetCrashPieceOnGround(part, -0.08f);
    }

    private static void PlaceCrashModel(Transform parent, string name, string sourceName, float x, float z, float scale, Vector3 rotation)
    {
        GameObject part = PlaceModel(parent, name, SpaceModels + "/" + sourceName + ".fbx", new Vector3(x, 0f, z), scale, rotation);
        SetCrashPieceOnGround(part, -0.12f);
    }

    private sealed class CrashMeshWriter
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uvs.Add(new Vector2(a.x, a.z)); uvs.Add(new Vector2(b.x, b.z)); uvs.Add(new Vector2(c.x, c.z));
            triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material)
        {
            Triangle(a, b, c, material);
            Triangle(a, c, d, material);
        }

        public Mesh Finish()
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
            {
                mesh.SetTriangles(triangles[i], i);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    private static Vector3 CrashRingPoint(float radius, float angle, float z)
    {
        return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
    }

    private static float BrokenEdge(int index, float seed)
    {
        return Mathf.Sin(index * 2.37f + seed) * 0.68f + Mathf.Cos(index * 1.13f + seed * 2f) * 0.4f;
    }

    private static Mesh BuildBrokenTube(float radius, float length, float thickness, int sides, float seed)
    {
        var writer = new CrashMeshWriter();
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            float a = Mathf.PI * 2f * i / sides;
            float b = Mathf.PI * 2f * next / sides;
            float rearA = -length * 0.5f + BrokenEdge(i, seed);
            float rearB = -length * 0.5f + BrokenEdge(next, seed);
            float frontA = length * 0.5f + BrokenEdge(i, seed + 3f);
            float frontB = length * 0.5f + BrokenEdge(next, seed + 3f);
            float bandRear = -length * 0.27f;
            float bandFront = bandRear + 1.3f;
            float rearBurnEnd = -length * 0.5f + 1.5f;
            float frontBurnStart = length * 0.5f - 2.3f + BrokenEdge(i, seed + 6f) * 0.35f;
            float nextFrontBurnStart = length * 0.5f - 2.3f + BrokenEdge(next, seed + 6f) * 0.35f;
            int hullMaterial = i >= 9 && i <= 12 ? 2 : 0;
            writer.Quad(CrashRingPoint(radius, a, rearA), CrashRingPoint(radius, b, rearB), CrashRingPoint(radius, b, rearBurnEnd), CrashRingPoint(radius, a, rearBurnEnd), i >= 1 && i <= 7 ? 2 : hullMaterial);
            writer.Quad(CrashRingPoint(radius, a, rearBurnEnd), CrashRingPoint(radius, b, rearBurnEnd), CrashRingPoint(radius, b, bandRear), CrashRingPoint(radius, a, bandRear), hullMaterial);
            writer.Quad(CrashRingPoint(radius, a, bandRear), CrashRingPoint(radius, b, bandRear), CrashRingPoint(radius, b, bandFront), CrashRingPoint(radius, a, bandFront), 1);
            writer.Quad(CrashRingPoint(radius, a, bandFront), CrashRingPoint(radius, b, bandFront), CrashRingPoint(radius, b, nextFrontBurnStart), CrashRingPoint(radius, a, frontBurnStart), i == 3 ? 2 : hullMaterial);
            writer.Quad(CrashRingPoint(radius, a, frontBurnStart), CrashRingPoint(radius, b, nextFrontBurnStart), CrashRingPoint(radius, b, frontB), CrashRingPoint(radius, a, frontA), i >= 1 && i <= 8 ? 2 : hullMaterial);
            float inner = radius - thickness;
            writer.Quad(CrashRingPoint(inner, a, rearA), CrashRingPoint(inner, a, frontA), CrashRingPoint(inner, b, frontB), CrashRingPoint(inner, b, rearB), 3);
            writer.Quad(CrashRingPoint(radius, a, rearA), CrashRingPoint(inner, a, rearA), CrashRingPoint(inner, b, rearB), CrashRingPoint(radius, b, rearB), 2);
            writer.Quad(CrashRingPoint(radius, a, frontA), CrashRingPoint(radius, b, frontB), CrashRingPoint(inner, b, frontB), CrashRingPoint(inner, a, frontA), 2);
        }
        return writer.Finish();
    }

    private static Mesh BuildCone(float radius, float length, int sides)
    {
        var writer = new CrashMeshWriter();
        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.PI * 2f * i / sides;
            float b = Mathf.PI * 2f * (i + 1) / sides;
            Vector3 outerA = CrashRingPoint(radius, a, BrokenEdge(i, 1f) * 0.4f);
            Vector3 outerB = CrashRingPoint(radius, b, BrokenEdge((i + 1) % sides, 1f) * 0.4f);
            Vector3 bandA = CrashRingPoint(radius * 0.91f, a, 1f);
            Vector3 bandB = CrashRingPoint(radius * 0.91f, b, 1f);
            writer.Quad(outerA, outerB, bandB, bandA, 1);
            writer.Triangle(bandA, bandB, new Vector3(-0.15f, 0.2f, length), i >= 9 && i <= 12 ? 2 : 0);
            writer.Triangle(outerB, outerA, new Vector3(0f, 0f, 0.05f), 3);
        }
        return writer.Finish();
    }

    private static Mesh BuildTornPanel()
    {
        var writer = new CrashMeshWriter();
        for (int i = 0; i < 5; i++)
        {
            float a = -0.75f + i * 0.3f;
            float b = a + 0.3f;
            Vector3 p0 = CrashRingPoint(4f, a, -2.8f + BrokenEdge(i, 0.4f) * 0.4f);
            Vector3 p1 = CrashRingPoint(4f, b, -2.8f + BrokenEdge(i + 1, 0.4f) * 0.4f);
            Vector3 p2 = CrashRingPoint(4f, b, 2.8f + BrokenEdge(i + 1, 2f) * 0.7f);
            Vector3 p3 = CrashRingPoint(4f, a, 2.8f + BrokenEdge(i, 2f) * 0.7f);
            writer.Quad(p0, p1, p2, p3, i == 2 ? 1 : 0);
            writer.Quad(p3 - Vector3.right * 0.1f, p2 - Vector3.right * 0.1f, p1 - Vector3.right * 0.1f, p0 - Vector3.right * 0.1f, 2);
        }
        return writer.Finish();
    }

    private static Mesh BuildTornFin()
    {
        var writer = new CrashMeshWriter();
        Vector3 a = new Vector3(0f, 0f, -3.8f);
        Vector3 b = new Vector3(5.8f, 0f, -2.6f);
        Vector3 c = new Vector3(3.5f, 0f, 0.1f);
        Vector3 d = new Vector3(0f, 0f, 3.2f);
        Vector3 offset = new Vector3(0f, 0.22f, 0f);
        writer.Quad(a + offset, d + offset, c + offset, b + offset, 1);
        writer.Quad(a, b, c, d, 2);
        writer.Quad(a, a + offset, b + offset, b, 0);
        writer.Quad(b, b + offset, c + offset, c, 2);
        writer.Quad(c, c + offset, d + offset, d, 0);
        writer.Quad(d, d + offset, a + offset, a, 2);
        return writer.Finish();
    }

    private static void CreateGroundScorch(Transform parent, string name, Vector2 center, float lengthRadius, float widthRadius, float yaw, float seed, Material material)
    {
        var writer = new CrashMeshWriter();
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 origin = new Vector3(center.x, GroundHeight(center.x, center.y) + 0.045f, center.y);
        const int segments = 36;
        // Separate concentric strips sample the terrain instead of crossing a hill with a flat triangle.
        for (int ring = 0; ring < 4; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                Vector3[] quad = new Vector3[4];
                int[] indices = { i, i + 1, i + 1, i };
                int[] ringIndices = { ring, ring, ring + 1, ring + 1 };
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    float angle = indices[vertex] * Mathf.PI * 2f / segments;
                    float irregularity = 0.86f + Mathf.Sin(angle * 7f + seed) * 0.07f + Mathf.Cos(angle * 11f + seed) * 0.07f;
                    float fraction = ringIndices[vertex] / 4f;
                    Vector3 delta = rotation * new Vector3(Mathf.Cos(angle) * widthRadius, 0f, Mathf.Sin(angle) * lengthRadius) * (fraction * irregularity);
                    quad[vertex] = new Vector3(center.x + delta.x, GroundHeight(center.x + delta.x, center.y + delta.z) + 0.045f, center.y + delta.z) - origin;
                }
                writer.Quad(quad[0], quad[1], quad[2], quad[3], 0);
                writer.Quad(quad[3], quad[2], quad[1], quad[0], 0);
            }
        }
        Mesh mesh = StoreCrashMesh("Mesh_" + name, writer.Finish());
        CreateCrashMeshPart(parent, name, mesh, origin, Vector3.zero, new[] { material, material, material, material });
    }

    private static void ValidateRocketCrashRoot(GameObject root)
    {
        int missing = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) missing++;
        }
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader") missing++;
            }
        }
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }
        if (missing != 0 || root.GetComponentsInChildren<Collider>(true).Length != 0)
        {
            throw new System.InvalidOperationException($"Rocket crash validation failed: missing references={missing} or unexpected colliders.");
        }
        Debug.Log($"[Rocket Crash V1 Validation] renderers={root.GetComponentsInChildren<Renderer>(true).Length}, triangles assets saved, missing references=0, added colliders=0.");
    }

    public static void InspectRocketCrashSites()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (string modelName in new[] { "rocket_baseA", "rocket_fuelA", "rocket_finsA", "rocket_topA" })
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SpaceModels + "/" + modelName + ".fbx");
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (TryGetBounds(instance, out Bounds bounds))
            {
                Debug.Log($"[Rocket Source] {modelName} center={Format(bounds.center)} size={Format(bounds.size)}");
            }
            Object.DestroyImmediate(instance);
        }
        foreach (Vector2 point in new[] { new Vector2(295f, 28f), new Vector2(305f, 65f), new Vector2(280f, -5f), new Vector2(320f, 100f), new Vector2(110f, 20f) })
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (float x in new[] { -18f, 0f, 18f })
            {
                foreach (float z in new[] { -30f, 0f, 30f })
                {
                    float y = GroundHeight(point.x + x, point.y + z);
                    minimum = Mathf.Min(minimum, y);
                    maximum = Mathf.Max(maximum, y);
                }
            }
            Debug.Log($"[Rocket Site] xz={point} ground={GroundHeight(point.x, point.y):F2} range={minimum:F2}..{maximum:F2}");
        }
        foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
        {
            TerrainData data = terrain.terrainData;
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 point = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                if (point.x > 265f && point.x < 350f && point.z > -40f && point.z < 125f && !data.treePrototypes[tree.prototypeIndex].prefab.name.Contains("Grass"))
                {
                    Debug.Log($"[Rocket Site Prop] {data.treePrototypes[tree.prototypeIndex].prefab.name} pos={Format(point)} scale={tree.widthScale:F2}");
                }
            }
        }
        RenderCameraPreview("/tmp/extraction-rocket-site-before.png", new Vector3(390f, 115f, -85f), new Vector3(290f, 4f, 30f), 48f);
    }
}

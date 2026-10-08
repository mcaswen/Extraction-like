using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class AerospaceSceneLayoutBuilder
{
    private const string RoadsideRootName = "Aerospace_Roadside_SetDress_V1";
    private const string RoadsidePrefabPath = "Assets/Art/External/AerospaceKit_CC0/Prefabs/PFB_Aerospace_Roadside_SetDress_V1.prefab";
    private const string RoadsideBackupPath = "UserSettings/SceneBackups/Scenezl_Final 1_Before_Aerospace_Roadside_V1.unity";

    [MenuItem("Tools/Extraction-like/Aerospace/Build Roadside Layout V1")]
    public static void BuildRoadsideLayoutV1()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        ExtractionLike.Editor.AerospaceAssetSetup.RefreshForBatchmode();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureSceneBackup(RoadsideBackupPath);
        EnsureAssetFolder(Path.GetDirectoryName(RoadsidePrefabPath)?.Replace('\\', '/'));
        Material emissive = EnsureEmissiveMaterial();
        Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/External/AerospaceKit_CC0/Materials/M_Kenney_Dark.mat");
        GameObject template = new GameObject(RoadsideRootName);

        // World-aligned prefab keeps the roadside composition independent of zone transforms.
        Transform relay = CreateGroundSection(template.transform, "01_Canteen_Telemetry_Station", 231f, 14f, 11f, dark);
        PlaceModel(relay, "Telemetry_Deck", SpaceModels + "/platform_large.fbx", Vector3.zero, 5.5f, Vector3.zero);
        PlaceModel(relay, "Telemetry_Terminal", SpaceModels + "/machine_wirelessCable.fbx", new Vector3(-1.8f, 0.55f, -1.8f), 6f, new Vector3(0f, -90f, 0f));
        PlaceModel(relay, "Telemetry_Dish", SpaceModels + "/satelliteDish_detailed.fbx", new Vector3(1.8f, 0.55f, 1.8f), 12f, new Vector3(0f, 140f, 0f));
        PlaceModel(relay, "Telemetry_BackRail_A", SpaceModels + "/rail.fbx", new Vector3(4.8f, 0.55f, -2.5f), 5f, Vector3.zero);
        PlaceModel(relay, "Telemetry_BackRail_B", SpaceModels + "/rail.fbx", new Vector3(4.8f, 0.55f, 2.5f), 5f, Vector3.zero);
        CreateRoadsideBeacon(relay, "Station_Status_Light", new Vector3(-4.5f, 0.55f, -4.5f), emissive, dark);
        CreateBeaconPart(relay, "Deck_Cyan_Edge", new Vector3(0f, 0.58f, -5.25f), new Vector3(9f, 0.06f, 0.18f), emissive);
        CreatePointLight(relay, "Station_WorkLight", new Vector3(0f, 8f, 0f), new Color(0.72f, 0.88f, 1f), 35f, 18f);

        Transform energy = CreateGroundSection(template.transform, "02_Forest_Energy_Node", 187f, 28f, 9f, dark);
        PlaceModel(energy, "Energy_Deck", SpaceModels + "/platform_large.fbx", Vector3.zero, 4.5f, Vector3.zero);
        PlaceModel(energy, "Energy_Generator", SpaceModels + "/machine_generatorLarge.fbx", new Vector3(0.7f, 0.45f, 0f), 5.5f, new Vector3(0f, -90f, 0f));
        PlaceModel(energy, "Energy_Cable", ModularModels + "/cables.fbx", new Vector3(-3f, 0.45f, 0f), 1.5f, new Vector3(0f, 15f, 0f));
        PlaceModel(energy, "Energy_Supply", SpaceModels + "/barrels.fbx", new Vector3(0.5f, 0.45f, 3f), 3.5f, Vector3.zero);
        PlaceModel(energy, "Energy_BackRail", SpaceModels + "/rail.fbx", new Vector3(4f, 0.45f, 0f), 6f, Vector3.zero);
        CreateRoadsideBeacon(energy, "Energy_Status_Light", new Vector3(-3.8f, 0.45f, -3.8f), emissive, dark);
        CreateBeaconPart(energy, "Deck_Cyan_Edge", new Vector3(0f, 0.48f, -4.25f), new Vector3(7f, 0.06f, 0.18f), emissive);
        CreatePointLight(energy, "Energy_WorkLight", new Vector3(0f, 7f, 0f), new Color(0.72f, 0.88f, 1f), 28f, 16f);

        Transform survey = CreateGroundSection(template.transform, "03_Dragonbone_Approach_Relay", 245f, 96f, 8f, dark);
        PlaceModel(survey, "Approach_Relay_Deck", SpaceModels + "/platform_large.fbx", Vector3.zero, 4f, Vector3.zero);
        PlaceModel(survey, "Approach_Relay", SpaceModels + "/machine_wireless.fbx", new Vector3(0f, 0.4f, 0f), 6.5f, new Vector3(0f, -90f, 0f));
        PlaceModel(survey, "Survey_Rover", SpaceModels + "/rover.fbx", new Vector3(-4.5f, 0f, 2.5f), 8f, new Vector3(0f, -25f, 0f));
        CreateRoadsideBeacon(survey, "Survey_Status_Light", new Vector3(-3f, 0.4f, -3f), emissive, dark);
        CreateBeaconPart(survey, "Deck_Cyan_Edge", new Vector3(0f, 0.43f, -3.75f), new Vector3(6f, 0.06f, 0.18f), emissive);
        CreatePointLight(survey, "Relay_WorkLight", new Vector3(0f, 7f, 0f), new Color(0.72f, 0.88f, 1f), 28f, 16f);

        Transform guides = CreateSection(template.transform, "04_Route_Guidance_Beacons");
        // Staggered shoulders: no repeated gates or equipment across the walkable center.
        Vector2[] beaconPoints =
        {
            new Vector2(190f, 12f), new Vector2(221f, 24f),
            new Vector2(192f, 44f), new Vector2(240f, 78f),
            new Vector2(207f, 72f), new Vector2(239f, 118f)
        };
        for (int i = 0; i < beaconPoints.Length; i++)
        {
            Vector2 point = beaconPoints[i];
            CreateRoadsideBeacon(guides, $"Route_Beacon_{i + 1:00}", new Vector3(point.x, GroundHeight(point.x, point.y) + 0.08f, point.y), emissive, dark);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, RoadsidePrefabPath, out bool saved);
        Object.DestroyImmediate(template);
        if (!saved || prefab == null)
        {
            throw new IOException("Failed to save the roadside layout prefab; formal scene not changed.");
        }

        Transform existing = FindByName(scene, RoadsideRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new IOException("Could not instantiate the roadside prefab.");
        }
        instance.name = RoadsideRootName;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ValidateRoadsideScene(scene);
        Debug.Log($"[Aerospace Roadside V1] Built {CountPlacedModels(instance.transform)} models and 9 navigation/status beacons. Backup={RoadsideBackupPath}");
    }

    [MenuItem("Tools/Extraction-like/Aerospace/Remove Roadside Layout V1")]
    public static void RemoveRoadsideLayoutV1()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform layout = FindByName(scene, RoadsideRootName);
        if (layout == null)
        {
            Debug.Log("[Aerospace Roadside V1] No layout found; scene unchanged.");
            return;
        }
        Object.DestroyImmediate(layout.gameObject);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Aerospace Roadside V1] Removed only the roadside layout; dragonbone and gameplay objects retained.");
    }

    public static void RenderRoadsideLayoutPreview()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ValidateRoadsideScene(scene);
        RenderCameraPreview("/tmp/extraction-roadside-v1.png", new Vector3(140f, 95f, 105f), new Vector3(216f, 1f, -5f), 48f);
        RenderCameraPreview("/tmp/extraction-roadside-energy-v1.png", new Vector3(246f, 55f, 64f), new Vector3(200f, 3f, 20f), 46f);
        RenderCameraPreview("/tmp/extraction-roadside-relay-v1.png", new Vector3(289f, 62f, 143f), new Vector3(246f, 16f, 100f), 46f);
        RenderCameraPreview("/tmp/extraction-roadside-plan-v1.png", new Vector3(203f, 210f, 61f), new Vector3(203f, 0f, 50f), 48f);
    }

    public static void BuildAndRenderRoadsideLayoutV1()
    {
        BuildRoadsideLayoutV1();
        RenderRoadsideLayoutPreview();
    }

    public static void VerifyRoadsideRoundTrip()
    {
        RemoveRoadsideLayoutV1();
        Scene scene = SceneManager.GetActiveScene();
        if (FindByName(scene, RoadsideRootName) != null)
        {
            throw new System.InvalidOperationException("Roadside remove test failed.");
        }
        Transform dragonbone = FindByName(scene, LayoutRootName);
        if (dragonbone == null || CountPlacedModels(dragonbone) != 28)
        {
            throw new System.InvalidOperationException("Roadside removal changed the dragonbone layout.");
        }
        BuildAndRenderRoadsideLayoutV1();
        Debug.Log("[Aerospace Roadside V1 Roundtrip] Remove and rebuild passed; final scene contains the roadside V1 and original dragonbone V1.");
    }

    public static void InspectRoadsideGroundCandidates()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Vector2[] candidates = { new Vector2(247f, 84f), new Vector2(252f, 82f), new Vector2(254f, 90f), new Vector2(245f, 96f), new Vector2(257f, 75f), new Vector2(253f, 103f) };
        foreach (Vector2 point in candidates)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (float offsetX in new[] { -4f, 0f, 4f })
            {
                foreach (float offsetZ in new[] { -4f, 0f, 4f })
                {
                    float y = GroundHeight(point.x + offsetX, point.y + offsetZ);
                    minimum = Mathf.Min(minimum, y);
                    maximum = Mathf.Max(maximum, y);
                }
            }
            Debug.Log($"[Roadside Candidate] xz={point} groundRange={minimum:F2}..{maximum:F2} slopeSpan={maximum - minimum:F2}");
        }
        foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
        {
            TerrainData data = terrain.terrainData;
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 position = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                if (position.x > 178f && position.x < 275f && position.z > 0f && position.z < 125f)
                {
                    Debug.Log($"[Roadside Terrain Prop] model={data.treePrototypes[tree.prototypeIndex].prefab.name} position={Format(position)} widthScale={tree.widthScale:F2} heightScale={tree.heightScale:F2}");
                }
            }
        }
    }

    private static Transform CreateGroundSection(Transform parent, string name, float worldX, float worldZ, float footprint, Material foundationMaterial)
    {
        float minimum = GroundHeight(worldX, worldZ);
        float maximum = minimum;
        float half = footprint * 0.5f;
        foreach (float offsetX in new[] { -half, 0f, half })
        {
            foreach (float offsetZ in new[] { -half, 0f, half })
            {
                float height = GroundHeight(worldX + offsetX, worldZ + offsetZ);
                minimum = Mathf.Min(minimum, height);
                maximum = Mathf.Max(maximum, height);
            }
        }
        Transform section = CreateSection(parent, name);
        section.localPosition = new Vector3(worldX, maximum + 0.05f, worldZ);
        if (maximum - minimum > 2.5f)
        {
            throw new System.InvalidOperationException($"Roadside site {name} is too steep for a low equipment pad; choose a flatter shoulder.");
        }
        float foundationDepth = Mathf.Max(0.25f, maximum - minimum + 0.15f);
        CreateBeaconPart(section, "Slope_Adapted_Foundation", new Vector3(0f, -foundationDepth * 0.5f, 0f), new Vector3(footprint, foundationDepth, footprint), foundationMaterial);
        return section;
    }

    private static float GroundHeight(float x, float z)
    {
        foreach (Terrain terrain in Object.FindObjectsOfType<Terrain>())
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (x >= origin.x && x < origin.x + size.x && z >= origin.z && z < origin.z + size.z)
            {
                return terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
            }
        }
        throw new System.InvalidOperationException($"No terrain underneath roadside location ({x}, {z}).");
    }

    private static void CreateRoadsideBeacon(Transform parent, string name, Vector3 position, Material emissive, Material dark)
    {
        Transform beacon = CreateSection(parent, name);
        beacon.localPosition = position;
        PlaceModel(beacon, "Beacon_Base", SpaceModels + "/supports_low.fbx", Vector3.zero, 1.8f, Vector3.zero);
        CreateBeaconPart(beacon, "Bollard", new Vector3(0f, 1.6f, 0f), new Vector3(0.65f, 2.5f, 0.65f), dark);
        CreateBeaconPart(beacon, "Cyan_Header", new Vector3(0f, 2.65f, 0f), new Vector3(0.85f, 0.4f, 0.85f), emissive);
        CreateBeaconPart(beacon, "Cyan_Facing_Strip", new Vector3(0f, 1.65f, -0.335f), new Vector3(0.22f, 1.5f, 0.035f), emissive);
    }

    private static void CreateBeaconPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static void ValidateRoadsideScene(Scene scene)
    {
        int roots = 0;
        Transform roadside = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RoadsideRootName)
            {
                roots++;
                roadside = root.transform;
            }
        }
        if (roots != 1 || roadside == null || CountPlacedModels(roadside) != 22)
        {
            throw new System.InvalidOperationException("Roadside layout validation failed: expected one root and 22 nested models.");
        }
        int missingScripts = 0;
        int missingMaterials = 0;
        int missingMeshes = 0;
        foreach (Transform child in roadside.GetComponentsInChildren<Transform>(true))
        {
            missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        }
        foreach (MeshFilter filter in roadside.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                missingMeshes++;
            }
        }
        foreach (Renderer renderer in roadside.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                {
                    missingMaterials++;
                }
            }
        }
        if (missingScripts != 0 || missingMaterials != 0 || missingMeshes != 0 || roadside.GetComponentsInChildren<Collider>(true).Length != 0 || roadside.GetComponentsInChildren<Light>(true).Length != 3)
        {
            throw new System.InvalidOperationException($"Roadside references/collision validation failed: missingScripts={missingScripts}, missingMaterials={missingMaterials}.");
        }
        Transform dragonbone = FindByName(scene, LayoutRootName);
        if (dragonbone == null || CountPlacedModels(dragonbone) != 28)
        {
            throw new System.InvalidOperationException("The existing dragonbone V1 was not preserved.");
        }
        Debug.Log("[Aerospace Roadside V1 Validation] One root, 22 models, 3 local work lights, 0 missing scripts/materials/meshes, 0 added colliders; dragonbone V1 retained.");
    }
}

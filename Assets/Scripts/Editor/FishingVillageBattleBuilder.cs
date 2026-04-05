using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a fishing-village combat space inside the currently selected map region.
/// </summary>
public static class FishingVillageBattleBuilder
{
    private const float FloorThickness = 0.22f;
    private const float WallHeight = 3.4f;
    private const float WallThickness = 0.18f;
    private const float DeckHeight = 0.18f;

    [MenuItem("Tools/Whitebox/Create Fishing Village Battle From Selection")]
    private static void CreateFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Fishing Village Battle",
                "Please select floor or wall objects inside the highlighted region first.",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_FishingVillageBattle_Selection");
        Undo.RegisterCreatedObjectUndo(root, "Create Fishing Village Battle");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(18f, selectionBounds.size.x * 0.92f),
            0f,
            Mathf.Max(16f, selectionBounds.size.z * 0.92f));

        CreateGround(root.transform, localSize);
        CreatePerimeter(root.transform, localSize);
        CreateHuts(root.transform, localSize);
        CreateDock(root.transform, localSize);
        CreateCombatCover(root.transform, localSize);
        CreateVillageDetails(root.transform, localSize);
        EnsurePerspectiveFadeController();

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static bool TryGetSelectionBounds(out Bounds bounds)
    {
        bounds = default;
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            return false;
        }

        bool initialized = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            if (TryGetObjectBounds(selectedObject, out Bounds objectBounds))
            {
                if (!initialized)
                {
                    bounds = objectBounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(objectBounds);
                }
            }
        }

        return initialized;
    }

    private static bool TryGetObjectBounds(GameObject target, out Bounds bounds)
    {
        Renderer rendererComponent = target.GetComponentInChildren<Renderer>();
        if (rendererComponent != null)
        {
            bounds = rendererComponent.bounds;
            return true;
        }

        Collider colliderComponent = target.GetComponentInChildren<Collider>();
        if (colliderComponent != null)
        {
            bounds = colliderComponent.bounds;
            return true;
        }

        bounds = new Bounds(target.transform.position, Vector3.one);
        return true;
    }

    private static void CreateGround(Transform parent, Vector3 areaSize)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "VillageGround";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        ground.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(ground, new Color(0.66f, 0.63f, 0.58f, 1f));

        GameObject shoreline = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shoreline.name = "ShorelineMud";
        shoreline.transform.SetParent(parent, false);
        shoreline.transform.localPosition = new Vector3(0f, 0.06f, areaSize.z * 0.28f);
        shoreline.transform.localScale = new Vector3(areaSize.x, 0.08f, areaSize.z * 0.36f);
        ApplyColor(shoreline, new Color(0.42f, 0.34f, 0.26f, 1f));

        GameObject openLane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        openLane.name = "OpenCombatLane";
        openLane.transform.SetParent(parent, false);
        openLane.transform.localPosition = new Vector3(0f, 0.12f, -areaSize.z * 0.02f);
        openLane.transform.localScale = new Vector3(areaSize.x * 0.46f, 0.04f, areaSize.z * 0.5f);
        ApplyColor(openLane, new Color(0.52f, 0.5f, 0.48f, 1f));
    }

    private static void CreatePerimeter(Transform parent, Vector3 areaSize)
    {
        Transform perimeterRoot = new GameObject("Perimeter").transform;
        perimeterRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateWall(perimeterRoot, new Vector3(-halfWidth, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.92f), "WestBoundary");
        CreateWall(perimeterRoot, new Vector3(halfWidth, WallHeight * 0.5f, -areaSize.z * 0.08f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.76f), "EastBoundary_Upper");
        CreateWall(perimeterRoot, new Vector3(halfWidth, WallHeight * 0.5f, areaSize.z * 0.34f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.18f), "EastBoundary_Lower");
        CreateWall(perimeterRoot, new Vector3(0f, WallHeight * 0.5f, -halfDepth), new Vector3(areaSize.x * 0.96f, WallThickness > 0f ? WallHeight : WallHeight, WallThickness), "NorthBoundary");

        GameObject southPierEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southPierEdge.name = "SouthPierEdge";
        southPierEdge.transform.SetParent(perimeterRoot, false);
        southPierEdge.transform.localPosition = new Vector3(0f, 0.95f, halfDepth);
        southPierEdge.transform.localScale = new Vector3(areaSize.x * 0.86f, 0.12f, 0.12f);
        ApplyColor(southPierEdge, new Color(0.49f, 0.36f, 0.22f, 1f));
        southPierEdge.AddComponent<PerspectiveFadeWall>();

        GameObject eastGateHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastGateHeader.name = "EastGapHeader";
        eastGateHeader.transform.SetParent(perimeterRoot, false);
        eastGateHeader.transform.localPosition = new Vector3(halfWidth, WallHeight - 0.45f, areaSize.z * 0.14f);
        eastGateHeader.transform.localScale = new Vector3(WallThickness, 0.9f, areaSize.z * 0.16f);
        ApplyColor(eastGateHeader, new Color(0.75f, 0.75f, 0.77f, 1f));
        eastGateHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateHuts(Transform parent, Vector3 areaSize)
    {
        Transform hutsRoot = new GameObject("Huts").transform;
        hutsRoot.SetParent(parent, false);

        CreateFishingHut(hutsRoot, new Vector3(-areaSize.x * 0.28f, 0f, -areaSize.z * 0.22f), new Vector3(areaSize.x * 0.22f, 3.1f, areaSize.z * 0.2f), "Hut_West");
        CreateFishingHut(hutsRoot, new Vector3(areaSize.x * 0.28f, 0f, -areaSize.z * 0.18f), new Vector3(areaSize.x * 0.24f, 3.2f, areaSize.z * 0.22f), "Hut_East");
        CreateFishingHut(hutsRoot, new Vector3(areaSize.x * 0.3f, 0f, areaSize.z * 0.2f), new Vector3(areaSize.x * 0.18f, 2.8f, areaSize.z * 0.18f), "Hut_SouthEast");
    }

    private static void CreateDock(Transform parent, Vector3 areaSize)
    {
        Transform dockRoot = new GameObject("Dock").transform;
        dockRoot.SetParent(parent, false);

        GameObject mainDock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainDock.name = "MainDock";
        mainDock.transform.SetParent(dockRoot, false);
        mainDock.transform.localPosition = new Vector3(-areaSize.x * 0.05f, DeckHeight * 0.5f + 0.1f, areaSize.z * 0.34f);
        mainDock.transform.localScale = new Vector3(areaSize.x * 0.44f, DeckHeight, areaSize.z * 0.18f);
        ApplyColor(mainDock, new Color(0.54f, 0.4f, 0.25f, 1f));

        GameObject sidePier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sidePier.name = "SidePier";
        sidePier.transform.SetParent(dockRoot, false);
        sidePier.transform.localPosition = new Vector3(areaSize.x * 0.22f, DeckHeight * 0.5f + 0.1f, areaSize.z * 0.22f);
        sidePier.transform.localScale = new Vector3(areaSize.x * 0.12f, DeckHeight, areaSize.z * 0.24f);
        ApplyColor(sidePier, new Color(0.54f, 0.4f, 0.25f, 1f));

        CreateRailing(dockRoot, mainDock.transform.localPosition + new Vector3(0f, 0.72f, mainDock.transform.localScale.z * 0.5f - 0.06f), new Vector3(mainDock.transform.localScale.x, 0.82f, 0.1f), "DockRail_Front");
        CreateRailing(dockRoot, mainDock.transform.localPosition + new Vector3(0f, 0.72f, -mainDock.transform.localScale.z * 0.5f + 0.06f), new Vector3(mainDock.transform.localScale.x, 0.82f, 0.1f), "DockRail_Back");
    }

    private static void CreateCombatCover(Transform parent, Vector3 areaSize)
    {
        Transform coverRoot = new GameObject("CombatCover").transform;
        coverRoot.SetParent(parent, false);

        CreateBoatCover(coverRoot, new Vector3(-areaSize.x * 0.2f, 0.62f, areaSize.z * 0.14f), new Vector3(2.8f, 0.8f, 1.2f), 14f, "BoatCover_A");
        CreateBoatCover(coverRoot, new Vector3(areaSize.x * 0.16f, 0.62f, areaSize.z * 0.08f), new Vector3(2.4f, 0.75f, 1.15f), -18f, "BoatCover_B");

        CreateRockCluster(coverRoot, new Vector3(-areaSize.x * 0.08f, 0.52f, -areaSize.z * 0.08f), "RockCluster_A");
        CreateRockCluster(coverRoot, new Vector3(areaSize.x * 0.22f, 0.52f, -areaSize.z * 0.02f), "RockCluster_B");

        CreateCrateStack(coverRoot, new Vector3(-areaSize.x * 0.26f, 0.36f, -areaSize.z * 0.24f), "CrateStack_A");
        CreateCrateStack(coverRoot, new Vector3(areaSize.x * 0.08f, 0.36f, -areaSize.z * 0.24f), "CrateStack_B");
    }

    private static void CreateVillageDetails(Transform parent, Vector3 areaSize)
    {
        Transform detailsRoot = new GameObject("VillageDetails").transform;
        detailsRoot.SetParent(parent, false);

        CreateFishingRack(detailsRoot, new Vector3(-areaSize.x * 0.34f, 0.86f, areaSize.z * 0.1f), "FishRack_A");
        CreateFishingRack(detailsRoot, new Vector3(areaSize.x * 0.34f, 0.86f, areaSize.z * 0.02f), "FishRack_B");
        CreateNetPost(detailsRoot, new Vector3(areaSize.x * 0.02f, 1.1f, areaSize.z * 0.28f), "NetPost_A");
        CreateBarrelCluster(detailsRoot, new Vector3(-areaSize.x * 0.04f, 0.48f, areaSize.z * 0.18f), "Barrels_A");
    }

    private static void CreateFishingHut(Transform parent, Vector3 localPosition, Vector3 footprint, string name)
    {
        Transform hutRoot = new GameObject(name).transform;
        hutRoot.SetParent(parent, false);
        hutRoot.localPosition = localPosition;

        float halfWidth = footprint.x * 0.5f;
        float halfDepth = footprint.z * 0.5f;
        float halfHeight = footprint.y * 0.5f;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(hutRoot, false);
        floor.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        floor.transform.localScale = new Vector3(footprint.x, 0.2f, footprint.z);
        ApplyColor(floor, new Color(0.58f, 0.44f, 0.28f, 1f));

        CreateWall(hutRoot, new Vector3(0f, halfHeight, halfDepth), new Vector3(footprint.x, footprint.y, 0.16f), "Wall_Front");
        CreateWall(hutRoot, new Vector3(0f, halfHeight, -halfDepth), new Vector3(footprint.x, footprint.y, 0.16f), "Wall_Back");
        CreateWall(hutRoot, new Vector3(halfWidth, halfHeight, 0f), new Vector3(0.16f, footprint.y, footprint.z), "Wall_Right");

        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "Wall_Left";
        leftWall.transform.SetParent(hutRoot, false);
        leftWall.transform.localPosition = new Vector3(-halfWidth, halfHeight, -footprint.z * 0.15f);
        leftWall.transform.localScale = new Vector3(0.16f, footprint.y, footprint.z * 0.7f);
        ApplyColor(leftWall, new Color(0.77f, 0.77f, 0.79f, 1f));
        leftWall.AddComponent<PerspectiveFadeWall>();

        GameObject doorwayHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorwayHeader.name = "DoorHeader";
        doorwayHeader.transform.SetParent(hutRoot, false);
        doorwayHeader.transform.localPosition = new Vector3(-halfWidth, footprint.y - 0.38f, footprint.z * 0.22f);
        doorwayHeader.transform.localScale = new Vector3(0.16f, 0.74f, footprint.z * 0.22f);
        ApplyColor(doorwayHeader, new Color(0.77f, 0.77f, 0.79f, 1f));
        doorwayHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateBoatCover(Transform parent, Vector3 localPosition, Vector3 scale, float yRotation, string name)
    {
        GameObject boat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boat.name = name;
        boat.transform.SetParent(parent, false);
        boat.transform.localPosition = localPosition;
        boat.transform.localRotation = Quaternion.Euler(0f, yRotation, 10f);
        boat.transform.localScale = scale;
        ApplyColor(boat, new Color(0.46f, 0.32f, 0.2f, 1f));
    }

    private static void CreateRockCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        Vector3[] offsets =
        {
            new Vector3(-0.65f, 0f, -0.2f),
            new Vector3(0.1f, 0.05f, 0.25f),
            new Vector3(0.58f, -0.04f, -0.28f)
        };

        Vector3[] scales =
        {
            new Vector3(1.15f, 1f, 0.95f),
            new Vector3(0.92f, 1.18f, 1.05f),
            new Vector3(0.78f, 0.92f, 0.76f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            rock.transform.SetParent(clusterRoot, false);
            rock.transform.localPosition = offsets[i];
            rock.transform.localRotation = Quaternion.Euler(0f, i * 22f, 0f);
            rock.transform.localScale = scales[i];
            ApplyColor(rock, new Color(0.35f, 0.37f, 0.39f, 1f));
        }
    }

    private static void CreateCrateStack(Transform parent, Vector3 localPosition, string name)
    {
        Transform stackRoot = new GameObject(name).transform;
        stackRoot.SetParent(parent, false);
        stackRoot.localPosition = localPosition;

        for (int i = 0; i < 4; i++)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = $"Crate_{i}";
            crate.transform.SetParent(stackRoot, false);
            crate.transform.localPosition = new Vector3((i % 2) * 0.72f, (i / 2) * 0.62f, i % 2 == 0 ? 0f : 0.18f);
            crate.transform.localScale = new Vector3(0.66f, 0.66f, 0.66f);
            ApplyColor(crate, new Color(0.55f, 0.41f, 0.26f, 1f));
        }
    }

    private static void CreateFishingRack(Transform parent, Vector3 localPosition, string name)
    {
        Transform rackRoot = new GameObject(name).transform;
        rackRoot.SetParent(parent, false);
        rackRoot.localPosition = localPosition;

        GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.name = "Post_Left";
        leftPost.transform.SetParent(rackRoot, false);
        leftPost.transform.localPosition = new Vector3(-0.72f, 1f, 0f);
        leftPost.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
        ApplyColor(leftPost, new Color(0.58f, 0.42f, 0.26f, 1f));

        GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.name = "Post_Right";
        rightPost.transform.SetParent(rackRoot, false);
        rightPost.transform.localPosition = new Vector3(0.72f, 1f, 0f);
        rightPost.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
        ApplyColor(rightPost, new Color(0.58f, 0.42f, 0.26f, 1f));

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Beam";
        beam.transform.SetParent(rackRoot, false);
        beam.transform.localPosition = new Vector3(0f, 1.82f, 0f);
        beam.transform.localScale = new Vector3(1.68f, 0.12f, 0.12f);
        ApplyColor(beam, new Color(0.58f, 0.42f, 0.26f, 1f));
    }

    private static void CreateNetPost(Transform parent, Vector3 localPosition, string name)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = name;
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPosition;
        post.transform.localScale = new Vector3(0.14f, 2.3f, 0.14f);
        ApplyColor(post, new Color(0.58f, 0.42f, 0.26f, 1f));

        GameObject net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = $"{name}_Net";
        net.transform.SetParent(parent, false);
        net.transform.localPosition = localPosition + new Vector3(0.48f, 1.1f, 0f);
        net.transform.localScale = new Vector3(0.96f, 1.5f, 0.08f);
        ApplyColor(net, new Color(0.68f, 0.74f, 0.78f, 0.85f));
    }

    private static void CreateBarrelCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        for (int i = 0; i < 3; i++)
        {
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = $"Barrel_{i}";
            barrel.transform.SetParent(clusterRoot, false);
            barrel.transform.localPosition = new Vector3(i * 0.6f, 0f, i == 1 ? 0.32f : 0f);
            barrel.transform.localScale = new Vector3(0.34f, 0.48f, 0.34f);
            ApplyColor(barrel, new Color(0.4f, 0.46f, 0.52f, 1f));
        }
    }

    private static void CreateWall(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = scale;
        ApplyColor(wall, new Color(0.78f, 0.78f, 0.8f, 1f));
        wall.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateRailing(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject railing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railing.name = name;
        railing.transform.SetParent(parent, false);
        railing.transform.localPosition = localPosition;
        railing.transform.localScale = scale;
        ApplyColor(railing, new Color(0.58f, 0.58f, 0.6f, 1f));
        railing.AddComponent<PerspectiveFadeWall>();
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer rendererComponent = target.GetComponent<Renderer>();
        if (rendererComponent == null)
        {
            return;
        }

        rendererComponent.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
        {
            color = color
        };
    }

    private static void EnsurePerspectiveFadeController()
    {
        PerspectiveWallFadeController controller = Object.FindObjectOfType<PerspectiveWallFadeController>();
        if (controller != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("PerspectiveWallFadeController");
        Undo.RegisterCreatedObjectUndo(controllerObject, "Create Perspective Wall Fade Controller");
        controller = controllerObject.AddComponent<PerspectiveWallFadeController>();
        controller.OccluderMask = ~0;
    }
}

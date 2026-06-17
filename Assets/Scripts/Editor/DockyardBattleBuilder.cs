using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据当前选区生成码头战斗区域白模
/// </summary>
public static class DockyardBattleBuilder
{
    private const float FloorThickness = 0.22f;
    private const float DeckThickness = 0.18f;
    private const float RailingHeight = 0.88f;

    [MenuItem("Tools/Whitebox/Create Dockyard Battle From Selection")]
    private static void CreateFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Dockyard Battle",
                "Please select floor or wall objects inside the target region first.",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_DockyardBattle");
        Undo.RegisterCreatedObjectUndo(root, "Create Dockyard Battle");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(20f, selectionBounds.size.x * 0.94f),
            0f,
            Mathf.Max(18f, selectionBounds.size.z * 0.94f));

        CreateMainDeck(root.transform, localSize);
        CreateSecondaryPiers(root.transform, localSize);
        CreateLoadingZone(root.transform, localSize);
        CreateCranes(root.transform, localSize);
        CreateCargoCover(root.transform, localSize);
        CreateDockDetails(root.transform, localSize);
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

    private static void CreateMainDeck(Transform parent, Vector3 areaSize)
    {
        GameObject dockFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dockFloor.name = "MainDockFloor";
        dockFloor.transform.SetParent(parent, false);
        dockFloor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        dockFloor.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(dockFloor, new Color(0.56f, 0.43f, 0.28f, 1f));

        GameObject loadingLane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loadingLane.name = "LoadingLane";
        loadingLane.transform.SetParent(parent, false);
        loadingLane.transform.localPosition = new Vector3(0f, 0.14f, -areaSize.z * 0.06f);
        loadingLane.transform.localScale = new Vector3(areaSize.x * 0.38f, 0.04f, areaSize.z * 0.78f);
        ApplyColor(loadingLane, new Color(0.5f, 0.38f, 0.24f, 1f));

        GameObject waterEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waterEdge.name = "WaterEdge";
        waterEdge.transform.SetParent(parent, false);
        waterEdge.transform.localPosition = new Vector3(0f, 0.03f, areaSize.z * 0.34f);
        waterEdge.transform.localScale = new Vector3(areaSize.x, 0.06f, areaSize.z * 0.26f);
        ApplyColor(waterEdge, new Color(0.24f, 0.4f, 0.52f, 1f));
    }

    private static void CreateSecondaryPiers(Transform parent, Vector3 areaSize)
    {
        Transform pierRoot = new GameObject("SecondaryPiers").transform;
        pierRoot.SetParent(parent, false);

        GameObject leftPier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPier.name = "LeftPier";
        leftPier.transform.SetParent(pierRoot, false);
        leftPier.transform.localPosition = new Vector3(-areaSize.x * 0.28f, DeckThickness * 0.5f + 0.02f, areaSize.z * 0.2f);
        leftPier.transform.localScale = new Vector3(areaSize.x * 0.14f, DeckThickness, areaSize.z * 0.38f);
        ApplyColor(leftPier, new Color(0.54f, 0.41f, 0.26f, 1f));

        GameObject rightPier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPier.name = "RightPier";
        rightPier.transform.SetParent(pierRoot, false);
        rightPier.transform.localPosition = new Vector3(areaSize.x * 0.3f, DeckThickness * 0.5f + 0.02f, areaSize.z * 0.16f);
        rightPier.transform.localScale = new Vector3(areaSize.x * 0.12f, DeckThickness, areaSize.z * 0.34f);
        ApplyColor(rightPier, new Color(0.54f, 0.41f, 0.26f, 1f));

        CreateRailing(pierRoot, leftPier.transform.localPosition + new Vector3(0f, RailingHeight, leftPier.transform.localScale.z * 0.5f - 0.06f), new Vector3(leftPier.transform.localScale.x, 0.12f, 0.1f), "LeftPier_Rail");
        CreateRailing(pierRoot, rightPier.transform.localPosition + new Vector3(0f, RailingHeight, rightPier.transform.localScale.z * 0.5f - 0.06f), new Vector3(rightPier.transform.localScale.x, 0.12f, 0.1f), "RightPier_Rail");
    }

    private static void CreateLoadingZone(Transform parent, Vector3 areaSize)
    {
        Transform loadingRoot = new GameObject("LoadingZone").transform;
        loadingRoot.SetParent(parent, false);

        GameObject warehouseFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        warehouseFront.name = "WarehouseFront";
        warehouseFront.transform.SetParent(loadingRoot, false);
        warehouseFront.transform.localPosition = new Vector3(0f, 1.8f, -areaSize.z * 0.36f);
        warehouseFront.transform.localScale = new Vector3(areaSize.x * 0.72f, 3.6f, 0.18f);
        ApplyColor(warehouseFront, new Color(0.76f, 0.77f, 0.79f, 1f));
        warehouseFront.AddComponent<PerspectiveFadeWall>();

        GameObject officeBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        officeBlock.name = "DockOffice";
        officeBlock.transform.SetParent(loadingRoot, false);
        officeBlock.transform.localPosition = new Vector3(-areaSize.x * 0.32f, 1.4f, -areaSize.z * 0.2f);
        officeBlock.transform.localScale = new Vector3(areaSize.x * 0.16f, 2.8f, areaSize.z * 0.16f);
        ApplyColor(officeBlock, new Color(0.72f, 0.73f, 0.76f, 1f));

        GameObject loadingBay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loadingBay.name = "LoadingBay";
        loadingBay.transform.SetParent(loadingRoot, false);
        loadingBay.transform.localPosition = new Vector3(areaSize.x * 0.24f, 0.6f, -areaSize.z * 0.2f);
        loadingBay.transform.localScale = new Vector3(areaSize.x * 0.2f, 1.2f, areaSize.z * 0.18f);
        ApplyColor(loadingBay, new Color(0.64f, 0.66f, 0.68f, 1f));
    }

    private static void CreateCranes(Transform parent, Vector3 areaSize)
    {
        Transform craneRoot = new GameObject("Cranes").transform;
        craneRoot.SetParent(parent, false);

        CreateCrane(craneRoot, new Vector3(-areaSize.x * 0.18f, 0f, areaSize.z * 0.06f), 24f, "Crane_A");
        CreateCrane(craneRoot, new Vector3(areaSize.x * 0.18f, 0f, areaSize.z * 0.02f), -18f, "Crane_B");
    }

    private static void CreateCrane(Transform parent, Vector3 localPosition, float yRotation, string name)
    {
        Transform crane = new GameObject(name).transform;
        crane.SetParent(parent, false);
        crane.localPosition = localPosition;
        crane.localRotation = Quaternion.Euler(0f, yRotation, 0f);

        GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mast.name = "Mast";
        mast.transform.SetParent(crane, false);
        mast.transform.localPosition = new Vector3(0f, 2.6f, 0f);
        mast.transform.localScale = new Vector3(0.4f, 5.2f, 0.4f);
        ApplyColor(mast, new Color(0.52f, 0.56f, 0.6f, 1f));

        GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm.name = "Arm";
        arm.transform.SetParent(crane, false);
        arm.transform.localPosition = new Vector3(1.8f, 4.7f, 0f);
        arm.transform.localScale = new Vector3(4.2f, 0.22f, 0.22f);
        ApplyColor(arm, new Color(0.56f, 0.6f, 0.64f, 1f));

        GameObject hook = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hook.name = "Hook";
        hook.transform.SetParent(crane, false);
        hook.transform.localPosition = new Vector3(3.2f, 3.25f, 0f);
        hook.transform.localScale = new Vector3(0.12f, 2.6f, 0.12f);
        ApplyColor(hook, new Color(0.34f, 0.36f, 0.4f, 1f));
    }

    private static void CreateCargoCover(Transform parent, Vector3 areaSize)
    {
        Transform cargoRoot = new GameObject("CargoCover").transform;
        cargoRoot.SetParent(parent, false);

        CreateContainer(cargoRoot, new Vector3(-areaSize.x * 0.22f, 1.2f, -areaSize.z * 0.02f), new Vector3(3.4f, 2.4f, 1.4f), new Color(0.46f, 0.56f, 0.68f, 1f), "Container_A");
        CreateContainer(cargoRoot, new Vector3(areaSize.x * 0.24f, 1.2f, -areaSize.z * 0.02f), new Vector3(3.4f, 2.4f, 1.4f), new Color(0.64f, 0.48f, 0.32f, 1f), "Container_B");

        CreateCrateStack(cargoRoot, new Vector3(-areaSize.x * 0.06f, 0.36f, areaSize.z * 0.18f), "Crates_A");
        CreateCrateStack(cargoRoot, new Vector3(areaSize.x * 0.1f, 0.36f, areaSize.z * 0.22f), "Crates_B");
        CreatePalletPile(cargoRoot, new Vector3(0f, 0.26f, -areaSize.z * 0.14f), "Pallets_A");
    }

    private static void CreateDockDetails(Transform parent, Vector3 areaSize)
    {
        Transform detailsRoot = new GameObject("DockDetails").transform;
        detailsRoot.SetParent(parent, false);

        CreateBoat(detailsRoot, new Vector3(-areaSize.x * 0.36f, 0.44f, areaSize.z * 0.36f), -16f, "Boat_A");
        CreateBoat(detailsRoot, new Vector3(areaSize.x * 0.3f, 0.44f, areaSize.z * 0.34f), 18f, "Boat_B");

        CreateMooringPosts(detailsRoot, new Vector3(-areaSize.x * 0.22f, 0f, areaSize.z * 0.3f), "Posts_Left");
        CreateMooringPosts(detailsRoot, new Vector3(areaSize.x * 0.18f, 0f, areaSize.z * 0.28f), "Posts_Right");

        CreateBarrelCluster(detailsRoot, new Vector3(0f, 0.48f, areaSize.z * 0.08f), "Barrels_A");
        CreateNetRack(detailsRoot, new Vector3(areaSize.x * 0.34f, 0.8f, -areaSize.z * 0.24f), "NetRack_A");
    }

    private static void CreateContainer(Transform parent, Vector3 localPosition, Vector3 scale, Color color, string name)
    {
        GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);
        container.name = name;
        container.transform.SetParent(parent, false);
        container.transform.localPosition = localPosition;
        container.transform.localScale = scale;
        ApplyColor(container, color);
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
            crate.transform.localPosition = new Vector3((i % 2) * 0.72f, (i / 2) * 0.58f, i % 2 == 0 ? 0f : 0.18f);
            crate.transform.localScale = new Vector3(0.64f, 0.64f, 0.64f);
            ApplyColor(crate, new Color(0.54f, 0.4f, 0.26f, 1f));
        }
    }

    private static void CreatePalletPile(Transform parent, Vector3 localPosition, string name)
    {
        GameObject pallet = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pallet.name = name;
        pallet.transform.SetParent(parent, false);
        pallet.transform.localPosition = localPosition;
        pallet.transform.localScale = new Vector3(2.2f, 0.52f, 1.1f);
        ApplyColor(pallet, new Color(0.52f, 0.38f, 0.24f, 1f));
    }

    private static void CreateBoat(Transform parent, Vector3 localPosition, float yRotation, string name)
    {
        GameObject boat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boat.name = name;
        boat.transform.SetParent(parent, false);
        boat.transform.localPosition = localPosition;
        boat.transform.localRotation = Quaternion.Euler(0f, yRotation, 8f);
        boat.transform.localScale = new Vector3(3.2f, 0.72f, 1.24f);
        ApplyColor(boat, new Color(0.46f, 0.32f, 0.22f, 1f));
    }

    private static void CreateMooringPosts(Transform parent, Vector3 localPosition, string name)
    {
        Transform posts = new GameObject(name).transform;
        posts.SetParent(parent, false);
        posts.localPosition = localPosition;

        for (int i = 0; i < 3; i++)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"Post_{i}";
            post.transform.SetParent(posts, false);
            post.transform.localPosition = new Vector3(i * 0.82f, 0.54f, 0f);
            post.transform.localScale = new Vector3(0.18f, 1.08f, 0.18f);
            ApplyColor(post, new Color(0.44f, 0.3f, 0.18f, 1f));
        }
    }

    private static void CreateBarrelCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform cluster = new GameObject(name).transform;
        cluster.SetParent(parent, false);
        cluster.localPosition = localPosition;

        for (int i = 0; i < 3; i++)
        {
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = $"Barrel_{i}";
            barrel.transform.SetParent(cluster, false);
            barrel.transform.localPosition = new Vector3(i * 0.54f, 0f, i == 1 ? 0.22f : 0f);
            barrel.transform.localScale = new Vector3(0.28f, 0.48f, 0.28f);
            ApplyColor(barrel, new Color(0.42f, 0.46f, 0.5f, 1f));
        }
    }

    private static void CreateNetRack(Transform parent, Vector3 localPosition, string name)
    {
        GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = name;
        rack.transform.SetParent(parent, false);
        rack.transform.localPosition = localPosition;
        rack.transform.localScale = new Vector3(1.6f, 1.5f, 0.5f);
        ApplyColor(rack, new Color(0.6f, 0.64f, 0.68f, 1f));
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

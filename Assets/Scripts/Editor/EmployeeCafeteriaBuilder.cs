using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据当前选区生成员工餐厅战斗空间白模
/// </summary>
public static class EmployeeCafeteriaBuilder
{
    private const float FloorThickness = 0.22f;
    private const float WallHeight = 4f;
    private const float WallThickness = 0.18f;
    private const float CounterHeight = 1.15f;
    private const float CoverHeight = 1.05f;

    [MenuItem("Tools/Whitebox/Create Employee Cafeteria From Selection")]
    private static void CreateFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Employee Cafeteria",
                "Please select floor or wall objects inside the target region first.",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_EmployeeCafeteria");
        Undo.RegisterCreatedObjectUndo(root, "Create Employee Cafeteria");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(20f, selectionBounds.size.x * 0.92f),
            0f,
            Mathf.Max(18f, selectionBounds.size.z * 0.92f));

        CreateFloor(root.transform, localSize);
        CreateOuterShell(root.transform, localSize);
        CreateKitchenAndServiceArea(root.transform, localSize);
        CreateDiningArea(root.transform, localSize);
        CreateCoverAndProps(root.transform, localSize);
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

    private static void CreateFloor(Transform parent, Vector3 areaSize)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "CafeteriaFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(floor, new Color(0.72f, 0.73f, 0.75f, 1f));

        GameObject centerAisle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerAisle.name = "CenterAisle";
        centerAisle.transform.SetParent(parent, false);
        centerAisle.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        centerAisle.transform.localScale = new Vector3(areaSize.x * 0.22f, 0.04f, areaSize.z * 0.88f);
        ApplyColor(centerAisle, new Color(0.56f, 0.58f, 0.62f, 1f));
    }

    private static void CreateOuterShell(Transform parent, Vector3 areaSize)
    {
        Transform shellRoot = new GameObject("OuterShell").transform;
        shellRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "NorthWall");
        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, -halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "SouthWall");
        CreateWall(shellRoot, new Vector3(-halfWidth, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z), "WestWall");
        CreateWall(shellRoot, new Vector3(halfWidth, WallHeight * 0.5f, -areaSize.z * 0.12f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.62f), "EastWall_Upper");
        CreateWall(shellRoot, new Vector3(halfWidth, WallHeight * 0.5f, areaSize.z * 0.3f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.24f), "EastWall_Lower");

        GameObject eastDoorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        eastDoorHeader.name = "EastDoorHeader";
        eastDoorHeader.transform.SetParent(shellRoot, false);
        eastDoorHeader.transform.localPosition = new Vector3(halfWidth, WallHeight - 0.55f, areaSize.z * 0.1f);
        eastDoorHeader.transform.localScale = new Vector3(WallThickness, 0.9f, areaSize.z * 0.18f);
        ApplyColor(eastDoorHeader, new Color(0.82f, 0.82f, 0.84f, 1f));
        eastDoorHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateKitchenAndServiceArea(Transform parent, Vector3 areaSize)
    {
        Transform kitchenRoot = new GameObject("KitchenArea").transform;
        kitchenRoot.SetParent(parent, false);

        float kitchenDepth = areaSize.z * 0.24f;
        float serviceZ = -areaSize.z * 0.24f;

        CreateWall(kitchenRoot, new Vector3(0f, WallHeight * 0.5f, serviceZ), new Vector3(areaSize.x * 0.84f, WallHeight, WallThickness), "ServiceDivider");

        GameObject servingCounter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        servingCounter.name = "ServingCounter";
        servingCounter.transform.SetParent(kitchenRoot, false);
        servingCounter.transform.localPosition = new Vector3(-areaSize.x * 0.08f, CounterHeight * 0.5f, serviceZ + 0.78f);
        servingCounter.transform.localScale = new Vector3(areaSize.x * 0.52f, CounterHeight, 1.1f);
        ApplyColor(servingCounter, new Color(0.64f, 0.66f, 0.69f, 1f));

        GameObject trayReturn = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trayReturn.name = "TrayReturnCounter";
        trayReturn.transform.SetParent(kitchenRoot, false);
        trayReturn.transform.localPosition = new Vector3(areaSize.x * 0.26f, CounterHeight * 0.5f, serviceZ + 1.2f);
        trayReturn.transform.localScale = new Vector3(areaSize.x * 0.18f, CounterHeight, 0.86f);
        ApplyColor(trayReturn, new Color(0.58f, 0.6f, 0.64f, 1f));

        CreateKitchenBench(kitchenRoot, new Vector3(-areaSize.x * 0.28f, 0.7f, -areaSize.z * 0.38f), new Vector3(2.6f, 1.2f, 0.85f), "PrepBench_A");
        CreateKitchenBench(kitchenRoot, new Vector3(0f, 0.7f, -areaSize.z * 0.38f), new Vector3(2.2f, 1.2f, 0.85f), "PrepBench_B");
        CreateStorageRack(kitchenRoot, new Vector3(areaSize.x * 0.3f, 1.2f, -areaSize.z * 0.39f), "StorageRack_A");
        CreateStorageRack(kitchenRoot, new Vector3(areaSize.x * 0.3f, 1.2f, -areaSize.z * 0.28f), "StorageRack_B");
        CreateFridgeBlock(kitchenRoot, new Vector3(-areaSize.x * 0.4f, 1.3f, -areaSize.z * 0.36f), "Fridge_A");
    }

    private static void CreateDiningArea(Transform parent, Vector3 areaSize)
    {
        Transform diningRoot = new GameObject("DiningArea").transform;
        diningRoot.SetParent(parent, false);

        float startZ = areaSize.z * 0.06f;
        float rowSpacing = areaSize.z * 0.15f;

        for (int row = 0; row < 3; row++)
        {
            float z = startZ + row * rowSpacing;
            CreateDiningTableSet(diningRoot, new Vector3(-areaSize.x * 0.2f, 0f, z), $"DiningSet_Left_{row}");
            CreateDiningTableSet(diningRoot, new Vector3(areaSize.x * 0.2f, 0f, z), $"DiningSet_Right_{row}");
        }
    }

    private static void CreateCoverAndProps(Transform parent, Vector3 areaSize)
    {
        Transform propsRoot = new GameObject("CafeteriaProps").transform;
        propsRoot.SetParent(parent, false);

        CreateHalfCoverBlock(propsRoot, new Vector3(0f, CoverHeight * 0.5f, areaSize.z * 0.18f), new Vector3(2.2f, CoverHeight, 0.72f), "SpillBarrier_A");
        CreateHalfCoverBlock(propsRoot, new Vector3(-areaSize.x * 0.34f, CoverHeight * 0.5f, areaSize.z * 0.28f), new Vector3(1.4f, CoverHeight, 1f), "VendingCover_A");
        CreateHalfCoverBlock(propsRoot, new Vector3(areaSize.x * 0.34f, CoverHeight * 0.5f, areaSize.z * 0.28f), new Vector3(1.4f, CoverHeight, 1f), "VendingCover_B");

        CreateVendingMachine(propsRoot, new Vector3(-areaSize.x * 0.4f, 1.15f, areaSize.z * 0.31f), "Vending_A");
        CreateVendingMachine(propsRoot, new Vector3(areaSize.x * 0.4f, 1.15f, areaSize.z * 0.31f), "Vending_B");

        CreateTrayCart(propsRoot, new Vector3(areaSize.x * 0.08f, 0.52f, -areaSize.z * 0.1f), "TrayCart_A");
        CreateTrashBinCluster(propsRoot, new Vector3(-areaSize.x * 0.06f, 0.48f, areaSize.z * 0.34f), "TrashBins_A");
        CreatePlantDivider(propsRoot, new Vector3(0f, 0.6f, areaSize.z * 0.4f), "PlantDivider_A");
    }

    private static void CreateDiningTableSet(Transform parent, Vector3 localPosition, string name)
    {
        Transform setRoot = new GameObject(name).transform;
        setRoot.SetParent(parent, false);
        setRoot.localPosition = localPosition;

        GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.SetParent(setRoot, false);
        table.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        table.transform.localScale = new Vector3(2.2f, 0.14f, 1.05f);
        ApplyColor(table, new Color(0.72f, 0.66f, 0.56f, 1f));

        CreateBenchSeat(setRoot, new Vector3(0f, 0.38f, -0.76f), "Bench_Front");
        CreateBenchSeat(setRoot, new Vector3(0f, 0.38f, 0.76f), "Bench_Back");
    }

    private static void CreateBenchSeat(Transform parent, Vector3 localPosition, string name)
    {
        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = name;
        bench.transform.SetParent(parent, false);
        bench.transform.localPosition = localPosition;
        bench.transform.localScale = new Vector3(2f, 0.12f, 0.36f);
        ApplyColor(bench, new Color(0.54f, 0.44f, 0.3f, 1f));
    }

    private static void CreateKitchenBench(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = name;
        bench.transform.SetParent(parent, false);
        bench.transform.localPosition = localPosition;
        bench.transform.localScale = scale;
        ApplyColor(bench, new Color(0.7f, 0.72f, 0.74f, 1f));
    }

    private static void CreateStorageRack(Transform parent, Vector3 localPosition, string name)
    {
        GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = name;
        rack.transform.SetParent(parent, false);
        rack.transform.localPosition = localPosition;
        rack.transform.localScale = new Vector3(1.15f, 2.4f, 0.52f);
        ApplyColor(rack, new Color(0.56f, 0.58f, 0.62f, 1f));
    }

    private static void CreateFridgeBlock(Transform parent, Vector3 localPosition, string name)
    {
        GameObject fridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fridge.name = name;
        fridge.transform.SetParent(parent, false);
        fridge.transform.localPosition = localPosition;
        fridge.transform.localScale = new Vector3(1.4f, 2.6f, 0.95f);
        ApplyColor(fridge, new Color(0.76f, 0.78f, 0.82f, 1f));
    }

    private static void CreateHalfCoverBlock(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = scale;
        ApplyColor(block, new Color(0.62f, 0.64f, 0.66f, 1f));
    }

    private static void CreateVendingMachine(Transform parent, Vector3 localPosition, string name)
    {
        GameObject vending = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vending.name = name;
        vending.transform.SetParent(parent, false);
        vending.transform.localPosition = localPosition;
        vending.transform.localScale = new Vector3(0.95f, 2.3f, 0.92f);
        ApplyColor(vending, new Color(0.36f, 0.48f, 0.62f, 1f));
    }

    private static void CreateTrayCart(Transform parent, Vector3 localPosition, string name)
    {
        GameObject cart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cart.name = name;
        cart.transform.SetParent(parent, false);
        cart.transform.localPosition = localPosition;
        cart.transform.localScale = new Vector3(1.2f, 0.85f, 0.72f);
        ApplyColor(cart, new Color(0.64f, 0.66f, 0.68f, 1f));
    }

    private static void CreateTrashBinCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        for (int i = 0; i < 3; i++)
        {
            GameObject bin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bin.name = $"Bin_{i}";
            bin.transform.SetParent(clusterRoot, false);
            bin.transform.localPosition = new Vector3(i * 0.52f, 0f, i == 1 ? 0.24f : 0f);
            bin.transform.localScale = new Vector3(0.28f, 0.48f, 0.28f);
            ApplyColor(bin, new Color(0.44f, 0.48f, 0.5f, 1f));
        }
    }

    private static void CreatePlantDivider(Transform parent, Vector3 localPosition, string name)
    {
        GameObject planter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        planter.name = name;
        planter.transform.SetParent(parent, false);
        planter.transform.localPosition = localPosition;
        planter.transform.localScale = new Vector3(2.1f, 0.5f, 0.62f);
        ApplyColor(planter, new Color(0.5f, 0.44f, 0.36f, 1f));
    }

    private static void CreateWall(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localScale = scale;
        ApplyColor(wall, new Color(0.8f, 0.8f, 0.82f, 1f));
        wall.AddComponent<PerspectiveFadeWall>();
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

using UnityEditor;
using UnityEngine;

public static class EmployeeDormitoryBuilder
{
    private const float FloorThickness = 0.22f;
    private const float WallHeight = 3.8f;
    private const float WallThickness = 0.18f;
    private const float BedFrameHeight = 1.75f;
    private const float BedWidth = 1.1f;
    private const float BedLength = 2.2f;

    [MenuItem("Tools/Whitebox/Create Employee Dormitory From Selection")]
    private static void CreateFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Employee Dormitory",
                "Please select floor or wall objects inside the target region first.",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_EmployeeDormitory");
        Undo.RegisterCreatedObjectUndo(root, "Create Employee Dormitory");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(18f, selectionBounds.size.x * 0.92f),
            0f,
            Mathf.Max(14f, selectionBounds.size.z * 0.92f));

        CreateFloor(root.transform, localSize);
        CreateOuterShell(root.transform, localSize);
        CreateDormPartitions(root.transform, localSize);
        CreateBedRows(root.transform, localSize);
        CreateStorageAndWashArea(root.transform, localSize);
        CreateCombatCover(root.transform, localSize);
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
        floor.name = "DormFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(floor, new Color(0.71f, 0.72f, 0.74f, 1f));

        GameObject centerAisle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerAisle.name = "CentralAisle";
        centerAisle.transform.SetParent(parent, false);
        centerAisle.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        centerAisle.transform.localScale = new Vector3(areaSize.x * 0.24f, 0.04f, areaSize.z * 0.9f);
        ApplyColor(centerAisle, new Color(0.56f, 0.58f, 0.61f, 1f));
    }

    private static void CreateOuterShell(Transform parent, Vector3 areaSize)
    {
        Transform shellRoot = new GameObject("OuterShell").transform;
        shellRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "NorthWall");
        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, -halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "SouthWall");
        CreateWall(shellRoot, new Vector3(-halfWidth, WallHeight * 0.5f, -areaSize.z * 0.14f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.64f), "WestWall_Upper");
        CreateWall(shellRoot, new Vector3(-halfWidth, WallHeight * 0.5f, areaSize.z * 0.28f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.26f), "WestWall_Lower");
        CreateWall(shellRoot, new Vector3(halfWidth, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z), "EastWall");

        GameObject westDoorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westDoorHeader.name = "WestDoorHeader";
        westDoorHeader.transform.SetParent(shellRoot, false);
        westDoorHeader.transform.localPosition = new Vector3(-halfWidth, WallHeight - 0.55f, areaSize.z * 0.1f);
        westDoorHeader.transform.localScale = new Vector3(WallThickness, 0.9f, areaSize.z * 0.18f);
        ApplyColor(westDoorHeader, new Color(0.82f, 0.82f, 0.84f, 1f));
        westDoorHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateDormPartitions(Transform parent, Vector3 areaSize)
    {
        Transform partitionRoot = new GameObject("Partitions").transform;
        partitionRoot.SetParent(parent, false);

        CreateWall(partitionRoot, new Vector3(-areaSize.x * 0.22f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.78f), "LeftDormDivider");
        CreateWall(partitionRoot, new Vector3(areaSize.x * 0.22f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.78f), "RightDormDivider");

        CreateWall(partitionRoot, new Vector3(-areaSize.x * 0.33f, WallHeight * 0.5f, areaSize.z * 0.22f), new Vector3(areaSize.x * 0.18f, WallHeight, WallThickness), "LeftRearDivider");
        CreateWall(partitionRoot, new Vector3(areaSize.x * 0.33f, WallHeight * 0.5f, areaSize.z * 0.22f), new Vector3(areaSize.x * 0.18f, WallHeight, WallThickness), "RightRearDivider");

        CreateWall(partitionRoot, new Vector3(0f, WallHeight * 0.5f, -areaSize.z * 0.24f), new Vector3(areaSize.x * 0.32f, WallHeight, WallThickness), "WashAreaDivider");
    }

    private static void CreateBedRows(Transform parent, Vector3 areaSize)
    {
        Transform bedsRoot = new GameObject("BedRows").transform;
        bedsRoot.SetParent(parent, false);

        float[] rowZ = { -areaSize.z * 0.08f, areaSize.z * 0.12f, areaSize.z * 0.32f };
        foreach (float z in rowZ)
        {
            CreateBunkBed(bedsRoot, new Vector3(-areaSize.x * 0.35f, 0f, z), "BunkBed_Left");
            CreateBunkBed(bedsRoot, new Vector3(areaSize.x * 0.35f, 0f, z), "BunkBed_Right");
        }
    }

    private static void CreateStorageAndWashArea(Transform parent, Vector3 areaSize)
    {
        Transform utilRoot = new GameObject("UtilityArea").transform;
        utilRoot.SetParent(parent, false);

        CreateLockerBank(utilRoot, new Vector3(-areaSize.x * 0.08f, 1.2f, -areaSize.z * 0.34f), "Lockers_Left");
        CreateLockerBank(utilRoot, new Vector3(areaSize.x * 0.08f, 1.2f, -areaSize.z * 0.34f), "Lockers_Right");

        CreateWashCounter(utilRoot, new Vector3(-areaSize.x * 0.34f, 0.75f, -areaSize.z * 0.36f), "WashCounter_Left");
        CreateWashCounter(utilRoot, new Vector3(areaSize.x * 0.34f, 0.75f, -areaSize.z * 0.36f), "WashCounter_Right");

        CreateDesk(utilRoot, new Vector3(-areaSize.x * 0.12f, 0.58f, areaSize.z * 0.4f), "Desk_A");
        CreateDesk(utilRoot, new Vector3(areaSize.x * 0.12f, 0.58f, areaSize.z * 0.4f), "Desk_B");
    }

    private static void CreateCombatCover(Transform parent, Vector3 areaSize)
    {
        Transform coverRoot = new GameObject("CombatCover").transform;
        coverRoot.SetParent(parent, false);

        CreateFootlocker(coverRoot, new Vector3(-areaSize.x * 0.08f, 0.42f, areaSize.z * 0.06f), "Footlocker_A");
        CreateFootlocker(coverRoot, new Vector3(areaSize.x * 0.08f, 0.42f, areaSize.z * 0.2f), "Footlocker_B");
        CreateCrateStack(coverRoot, new Vector3(0f, 0.35f, areaSize.z * 0.28f), "SupplyCrates_A");
        CreatePlantStand(coverRoot, new Vector3(0f, 0.6f, -areaSize.z * 0.12f), "PlantStand_A");
    }

    private static void CreateBunkBed(Transform parent, Vector3 localPosition, string name)
    {
        Transform bedRoot = new GameObject(name).transform;
        bedRoot.SetParent(parent, false);
        bedRoot.localPosition = localPosition;

        float legHeight = BedFrameHeight;
        float halfLength = BedLength * 0.5f;
        float halfWidth = BedWidth * 0.5f;

        CreateFramePost(bedRoot, new Vector3(-halfWidth, legHeight * 0.5f, -halfLength), "Post_A");
        CreateFramePost(bedRoot, new Vector3(halfWidth, legHeight * 0.5f, -halfLength), "Post_B");
        CreateFramePost(bedRoot, new Vector3(-halfWidth, legHeight * 0.5f, halfLength), "Post_C");
        CreateFramePost(bedRoot, new Vector3(halfWidth, legHeight * 0.5f, halfLength), "Post_D");

        CreateMattress(bedRoot, new Vector3(0f, 0.42f, 0f), "LowerMattress");
        CreateMattress(bedRoot, new Vector3(0f, 1.32f, 0f), "UpperMattress");

        GameObject ladder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ladder.name = "Ladder";
        ladder.transform.SetParent(bedRoot, false);
        ladder.transform.localPosition = new Vector3(halfWidth + 0.08f, 0.86f, 0f);
        ladder.transform.localScale = new Vector3(0.08f, 1.4f, 0.82f);
        ApplyColor(ladder, new Color(0.55f, 0.45f, 0.34f, 1f));
    }

    private static void CreateFramePost(Transform parent, Vector3 localPosition, string name)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = name;
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPosition;
        post.transform.localScale = new Vector3(0.08f, BedFrameHeight, 0.08f);
        ApplyColor(post, new Color(0.54f, 0.44f, 0.34f, 1f));
    }

    private static void CreateMattress(Transform parent, Vector3 localPosition, string name)
    {
        GameObject mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mattress.name = name;
        mattress.transform.SetParent(parent, false);
        mattress.transform.localPosition = localPosition;
        mattress.transform.localScale = new Vector3(BedWidth, 0.16f, BedLength);
        ApplyColor(mattress, new Color(0.72f, 0.74f, 0.78f, 1f));
    }

    private static void CreateLockerBank(Transform parent, Vector3 localPosition, string name)
    {
        GameObject lockers = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lockers.name = name;
        lockers.transform.SetParent(parent, false);
        lockers.transform.localPosition = localPosition;
        lockers.transform.localScale = new Vector3(1.4f, 2.4f, 0.62f);
        ApplyColor(lockers, new Color(0.58f, 0.62f, 0.67f, 1f));
    }

    private static void CreateWashCounter(Transform parent, Vector3 localPosition, string name)
    {
        GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        counter.name = name;
        counter.transform.SetParent(parent, false);
        counter.transform.localPosition = localPosition;
        counter.transform.localScale = new Vector3(2.2f, 1.15f, 0.78f);
        ApplyColor(counter, new Color(0.68f, 0.7f, 0.73f, 1f));
    }

    private static void CreateDesk(Transform parent, Vector3 localPosition, string name)
    {
        GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = name;
        desk.transform.SetParent(parent, false);
        desk.transform.localPosition = localPosition;
        desk.transform.localScale = new Vector3(1.4f, 0.9f, 0.72f);
        ApplyColor(desk, new Color(0.6f, 0.48f, 0.34f, 1f));
    }

    private static void CreateFootlocker(Transform parent, Vector3 localPosition, string name)
    {
        GameObject locker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        locker.name = name;
        locker.transform.SetParent(parent, false);
        locker.transform.localPosition = localPosition;
        locker.transform.localScale = new Vector3(1.15f, 0.84f, 0.62f);
        ApplyColor(locker, new Color(0.42f, 0.48f, 0.54f, 1f));
    }

    private static void CreateCrateStack(Transform parent, Vector3 localPosition, string name)
    {
        Transform stackRoot = new GameObject(name).transform;
        stackRoot.SetParent(parent, false);
        stackRoot.localPosition = localPosition;

        for (int i = 0; i < 3; i++)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = $"Crate_{i}";
            crate.transform.SetParent(stackRoot, false);
            crate.transform.localPosition = new Vector3((i % 2) * 0.62f, (i / 2) * 0.56f, i == 2 ? 0.24f : 0f);
            crate.transform.localScale = new Vector3(0.58f, 0.58f, 0.58f);
            ApplyColor(crate, new Color(0.56f, 0.42f, 0.28f, 1f));
        }
    }

    private static void CreatePlantStand(Transform parent, Vector3 localPosition, string name)
    {
        GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stand.name = name;
        stand.transform.SetParent(parent, false);
        stand.transform.localPosition = localPosition;
        stand.transform.localScale = new Vector3(1.4f, 1.2f, 0.62f);
        ApplyColor(stand, new Color(0.5f, 0.44f, 0.38f, 1f));
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

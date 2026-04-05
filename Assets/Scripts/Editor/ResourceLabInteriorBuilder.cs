using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据当前选中的区域生成实验室内景白模。
/// 适用于在已搭好的大地图框架中，快速覆盖普通资源区的室内战斗空间。
/// </summary>
public static class ResourceLabInteriorBuilder
{
    private const float FloorThickness = 0.22f;
    private const float WallThickness = 0.2f;
    private const float WallHeight = 4f;
    private const float UpperWalkwayHeight = 2.8f;

    [MenuItem("Tools/Whitebox/Create Resource Lab Interior From Selection")]
    private static void CreateResourceLabInteriorFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Resource Lab Interior",
                "请先在 Scene 中选中红框区域内的地面或墙体物体，再执行该命令。",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_ResourceLabInterior");
        Undo.RegisterCreatedObjectUndo(root, "Create Resource Lab Interior");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(16f, selectionBounds.size.x * 0.9f),
            0f,
            Mathf.Max(14f, selectionBounds.size.z * 0.9f));

        CreateFloor(root.transform, localSize);
        CreateOuterShell(root.transform, localSize);
        CreateInteriorPartitions(root.transform, localSize);
        CreateCombatLane(root.transform, localSize);
        CreateUpperWalkway(root.transform, localSize);
        CreateLabProps(root.transform, localSize);
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
        floor.name = "LabFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(floor, new Color(0.72f, 0.74f, 0.77f, 1f));

        GameObject gridStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gridStrip.name = "LabCenterLane";
        gridStrip.transform.SetParent(parent, false);
        gridStrip.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        gridStrip.transform.localScale = new Vector3(areaSize.x * 0.38f, 0.04f, areaSize.z * 0.7f);
        ApplyColor(gridStrip, new Color(0.52f, 0.58f, 0.66f, 1f));
    }

    private static void CreateOuterShell(Transform parent, Vector3 areaSize)
    {
        Transform shellRoot = new GameObject("OuterShell").transform;
        shellRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "NorthWall");
        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, -halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "SouthWall");
        CreateWall(shellRoot, new Vector3(-halfWidth, WallHeight * 0.5f, -areaSize.z * 0.12f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.56f), "WestWall_Upper");
        CreateWall(shellRoot, new Vector3(-halfWidth, WallHeight * 0.5f, areaSize.z * 0.34f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.2f), "WestWall_Lower");
        CreateWall(shellRoot, new Vector3(halfWidth, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z), "EastWall");

        GameObject westDoorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        westDoorHeader.name = "WestDoorHeader";
        westDoorHeader.transform.SetParent(shellRoot, false);
        westDoorHeader.transform.localPosition = new Vector3(-halfWidth, WallHeight - 0.55f, areaSize.z * 0.12f);
        westDoorHeader.transform.localScale = new Vector3(WallThickness, 0.9f, areaSize.z * 0.24f);
        ApplyColor(westDoorHeader, new Color(0.82f, 0.82f, 0.85f, 1f));
        westDoorHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateInteriorPartitions(Transform parent, Vector3 areaSize)
    {
        Transform partitionRoot = new GameObject("Partitions").transform;
        partitionRoot.SetParent(parent, false);

        CreateWall(partitionRoot, new Vector3(-areaSize.x * 0.2f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.7f), "Partition_Left");
        CreateWall(partitionRoot, new Vector3(areaSize.x * 0.24f, WallHeight * 0.5f, -areaSize.z * 0.12f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.52f), "Partition_Right");
        CreateWall(partitionRoot, new Vector3(0f, WallHeight * 0.5f, areaSize.z * 0.28f), new Vector3(areaSize.x * 0.34f, WallHeight, WallThickness), "Partition_Top");
    }

    private static void CreateCombatLane(Transform parent, Vector3 areaSize)
    {
        GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lane.name = "CombatLane";
        lane.transform.SetParent(parent, false);
        lane.transform.localPosition = new Vector3(0f, 0.18f, -areaSize.z * 0.04f);
        lane.transform.localScale = new Vector3(areaSize.x * 0.42f, 0.05f, areaSize.z * 0.52f);
        ApplyColor(lane, new Color(0.44f, 0.5f, 0.58f, 1f));
    }

    private static void CreateUpperWalkway(Transform parent, Vector3 areaSize)
    {
        GameObject walkway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkway.name = "UpperWalkway";
        walkway.transform.SetParent(parent, false);
        walkway.transform.localPosition = new Vector3(areaSize.x * 0.18f, UpperWalkwayHeight, areaSize.z * 0.28f);
        walkway.transform.localScale = new Vector3(areaSize.x * 0.34f, 0.16f, areaSize.z * 0.18f);
        ApplyColor(walkway, new Color(0.62f, 0.64f, 0.68f, 1f));

        CreateRailing(parent, walkway.transform.localPosition + new Vector3(0f, 0.7f, walkway.transform.localScale.z * 0.5f - 0.06f), new Vector3(walkway.transform.localScale.x, 0.95f, 0.12f), "UpperWalkway_RailFront");
        CreateRailing(parent, walkway.transform.localPosition + new Vector3(0f, 0.7f, -walkway.transform.localScale.z * 0.5f + 0.06f), new Vector3(walkway.transform.localScale.x, 0.95f, 0.12f), "UpperWalkway_RailBack");
    }

    private static void CreateLabProps(Transform parent, Vector3 areaSize)
    {
        Transform propsRoot = new GameObject("LabProps").transform;
        propsRoot.SetParent(parent, false);

        CreateBench(propsRoot, new Vector3(-areaSize.x * 0.33f, 0.72f, areaSize.z * 0.24f), new Vector3(2.6f, 1.25f, 0.9f), "Bench_A");
        CreateBench(propsRoot, new Vector3(-areaSize.x * 0.33f, 0.72f, areaSize.z * 0.05f), new Vector3(2.6f, 1.25f, 0.9f), "Bench_B");
        CreateBench(propsRoot, new Vector3(areaSize.x * 0.34f, 0.72f, -areaSize.z * 0.18f), new Vector3(2.4f, 1.25f, 0.9f), "Bench_C");

        CreateStorage(propsRoot, new Vector3(areaSize.x * 0.36f, 1.25f, areaSize.z * 0.28f), new Vector3(1.1f, 2.5f, 0.55f), "StorageRack_A");
        CreateStorage(propsRoot, new Vector3(areaSize.x * 0.36f, 1.25f, areaSize.z * 0.05f), new Vector3(1.1f, 2.5f, 0.55f), "StorageRack_B");

        CreateTank(propsRoot, new Vector3(areaSize.x * 0.04f, 1.4f, areaSize.z * 0.36f), "Tank_A");
        CreateTank(propsRoot, new Vector3(areaSize.x * 0.14f, 1.4f, areaSize.z * 0.36f), "Tank_B");

        CreateConsole(propsRoot, new Vector3(-areaSize.x * 0.02f, 0.9f, -areaSize.z * 0.38f), "Console_A");
        CreateCrateCluster(propsRoot, new Vector3(areaSize.x * 0.02f, 0.38f, areaSize.z * 0.02f), "Crates_A");
    }

    private static void CreateBench(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = name;
        bench.transform.SetParent(parent, false);
        bench.transform.localPosition = localPosition;
        bench.transform.localScale = scale;
        ApplyColor(bench, new Color(0.74f, 0.76f, 0.8f, 1f));
    }

    private static void CreateStorage(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = name;
        rack.transform.SetParent(parent, false);
        rack.transform.localPosition = localPosition;
        rack.transform.localScale = scale;
        ApplyColor(rack, new Color(0.56f, 0.58f, 0.62f, 1f));
    }

    private static void CreateTank(Transform parent, Vector3 localPosition, string name)
    {
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tank.name = name;
        tank.transform.SetParent(parent, false);
        tank.transform.localPosition = localPosition;
        tank.transform.localScale = new Vector3(0.55f, 1.4f, 0.55f);
        ApplyColor(tank, new Color(0.46f, 0.84f, 1f, 0.92f));
    }

    private static void CreateConsole(Transform parent, Vector3 localPosition, string name)
    {
        GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
        console.name = name;
        console.transform.SetParent(parent, false);
        console.transform.localPosition = localPosition;
        console.transform.localScale = new Vector3(1.3f, 0.95f, 0.85f);
        ApplyColor(console, new Color(0.44f, 0.48f, 0.56f, 1f));
    }

    private static void CreateCrateCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        for (int i = 0; i < 4; i++)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = $"Crate_{i}";
            crate.transform.SetParent(clusterRoot, false);
            crate.transform.localPosition = new Vector3((i % 2) * 0.75f, (i / 2) * 0.65f, i % 2 == 0 ? 0f : 0.2f);
            crate.transform.localScale = new Vector3(0.68f, 0.68f, 0.68f);
            ApplyColor(crate, new Color(0.54f, 0.42f, 0.28f, 1f));
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
        ApplyColor(railing, new Color(0.6f, 0.6f, 0.62f, 1f));
        railing.AddComponent<PerspectiveFadeWall>();
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer rendererComponent = target.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };
        }
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

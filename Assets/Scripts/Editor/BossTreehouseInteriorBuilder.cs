using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据当前场景选中的区域生成首领树屋内景白模
/// 适用于已经在场景中手工调整过墙体和空间分区后的二次覆盖
/// </summary>
public static class BossTreehouseInteriorBuilder
{
    private const float FloorThickness = 0.22f;
    private const float WallThickness = 0.2f;
    private const float WallHeight = 4.2f;
    private const float UpperDeckHeight = 3.1f;
    private const float DeckThickness = 0.18f;

    [MenuItem("Tools/Whitebox/Create Boss Treehouse Interior From Selection")]
    private static void CreateBossTreehouseInteriorFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Boss Treehouse Interior",
                "请先在 Scene 中选中核心区红框内的若干地面或墙体物体，再执行该命令。",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_BossTreehouseInterior");
        Undo.RegisterCreatedObjectUndo(root, "Create Boss Treehouse Interior");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(18f, selectionBounds.size.x * 0.92f),
            0f,
            Mathf.Max(12f, selectionBounds.size.z * 0.92f));

        CreateFloor(root.transform, localSize);
        CreateOuterShell(root.transform, localSize);
        CreateUpperDeck(root.transform, localSize);
        CreateInternalStructures(root.transform, localSize);
        CreateLadder(root.transform, localSize);
        CreateCoverProps(root.transform, localSize);
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
        floor.name = "MainFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        floor.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(floor, new Color(0.56f, 0.43f, 0.29f, 1f));
    }

    private static void CreateOuterShell(Transform parent, Vector3 areaSize)
    {
        Transform shellRoot = new GameObject("OuterShell").transform;
        shellRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "FrontWall");
        CreateWall(shellRoot, new Vector3(0f, WallHeight * 0.5f, -halfDepth), new Vector3(areaSize.x, WallHeight, WallThickness), "BackWall");
        CreateWall(shellRoot, new Vector3(halfWidth, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, areaSize.z), "RightWall");

        GameObject leftWallUpper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWallUpper.name = "LeftWall_Upper";
        leftWallUpper.transform.SetParent(shellRoot, false);
        leftWallUpper.transform.localPosition = new Vector3(-halfWidth, WallHeight * 0.5f, -areaSize.z * 0.22f);
        leftWallUpper.transform.localScale = new Vector3(WallThickness, WallHeight, areaSize.z * 0.56f);
        ApplyColor(leftWallUpper, new Color(0.78f, 0.78f, 0.8f, 1f));
        leftWallUpper.AddComponent<PerspectiveFadeWall>();

        GameObject leftWallLower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWallLower.name = "LeftWall_Lower";
        leftWallLower.transform.SetParent(shellRoot, false);
        leftWallLower.transform.localPosition = new Vector3(-halfWidth, WallHeight * 0.5f, areaSize.z * 0.33f);
        leftWallLower.transform.localScale = new Vector3(WallThickness, WallHeight, areaSize.z * 0.24f);
        ApplyColor(leftWallLower, new Color(0.78f, 0.78f, 0.8f, 1f));
        leftWallLower.AddComponent<PerspectiveFadeWall>();

        GameObject doorwayHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorwayHeader.name = "LeftDoorHeader";
        doorwayHeader.transform.SetParent(shellRoot, false);
        doorwayHeader.transform.localPosition = new Vector3(-halfWidth, WallHeight - 0.55f, areaSize.z * 0.08f);
        doorwayHeader.transform.localScale = new Vector3(WallThickness, 0.9f, areaSize.z * 0.22f);
        ApplyColor(doorwayHeader, new Color(0.78f, 0.78f, 0.8f, 1f));
        doorwayHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateUpperDeck(Transform parent, Vector3 areaSize)
    {
        float upperWidth = areaSize.x * 0.42f;
        float upperDepth = areaSize.z * 0.34f;
        Vector3 upperCenter = new Vector3(areaSize.x * 0.18f, UpperDeckHeight, -areaSize.z * 0.2f);

        GameObject upperDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        upperDeck.name = "UpperDeck";
        upperDeck.transform.SetParent(parent, false);
        upperDeck.transform.localPosition = upperCenter;
        upperDeck.transform.localScale = new Vector3(upperWidth, DeckThickness, upperDepth);
        ApplyColor(upperDeck, new Color(0.6f, 0.48f, 0.32f, 1f));

        CreateRailing(parent, upperCenter + new Vector3(0f, 0.72f, upperDepth * 0.5f - 0.05f), new Vector3(upperWidth, 1f, 0.12f), "UpperDeck_RailingFront");
        CreateRailing(parent, upperCenter + new Vector3(0f, 0.72f, -upperDepth * 0.5f + 0.05f), new Vector3(upperWidth, 1f, 0.12f), "UpperDeck_RailingBack");
        CreateRailing(parent, upperCenter + new Vector3(upperWidth * 0.5f - 0.05f, 0.72f, 0f), new Vector3(0.12f, 1f, upperDepth), "UpperDeck_RailingRight");
    }

    private static void CreateInternalStructures(Transform parent, Vector3 areaSize)
    {
        Transform structureRoot = new GameObject("InteriorStructures").transform;
        structureRoot.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;

        CreateWall(structureRoot, new Vector3(-areaSize.x * 0.1f, WallHeight * 0.5f, areaSize.z * 0.05f), new Vector3(areaSize.x * 0.28f, WallHeight, WallThickness), "RoomDivider_Center");
        CreateWall(structureRoot, new Vector3(areaSize.x * 0.19f, WallHeight * 0.5f, -areaSize.z * 0.08f), new Vector3(WallThickness, WallHeight, areaSize.z * 0.42f), "RoomDivider_Right");

        GameObject sideRoomWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sideRoomWall.name = "SideRoomWall";
        sideRoomWall.transform.SetParent(structureRoot, false);
        sideRoomWall.transform.localPosition = new Vector3(-halfWidth + 2.2f, WallHeight * 0.5f, -areaSize.z * 0.14f);
        sideRoomWall.transform.localScale = new Vector3(areaSize.x * 0.18f, WallHeight, WallThickness);
        ApplyColor(sideRoomWall, new Color(0.78f, 0.78f, 0.8f, 1f));
        sideRoomWall.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateLadder(Transform parent, Vector3 areaSize)
    {
        Transform ladderRoot = new GameObject("InteriorLadder").transform;
        ladderRoot.SetParent(parent, false);
        ladderRoot.localPosition = new Vector3(-areaSize.x * 0.12f, UpperDeckHeight * 0.5f, areaSize.z * 0.18f);

        CreateLadderRail(ladderRoot, -0.28f);
        CreateLadderRail(ladderRoot, 0.28f);

        int rungCount = 6;
        float rungSpacing = UpperDeckHeight / rungCount;
        for (int i = 0; i < rungCount; i++)
        {
            GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rung.name = $"Rung_{i}";
            rung.transform.SetParent(ladderRoot, false);
            rung.transform.localPosition = new Vector3(0f, -UpperDeckHeight * 0.5f + 0.35f + i * rungSpacing, 0f);
            rung.transform.localScale = new Vector3(0.64f, 0.08f, 0.08f);
            ApplyColor(rung, new Color(0.58f, 0.42f, 0.28f, 1f));
        }
    }

    private static void CreateCoverProps(Transform parent, Vector3 areaSize)
    {
        Transform propsRoot = new GameObject("InteriorCoverProps").transform;
        propsRoot.SetParent(parent, false);

        CreateFurniture(propsRoot, new Vector3(-areaSize.x * 0.28f, 0.55f, -areaSize.z * 0.2f), new Vector3(2.4f, 1.1f, 0.8f), "Workbench_A");
        CreateFurniture(propsRoot, new Vector3(areaSize.x * 0.22f, 0.6f, areaSize.z * 0.2f), new Vector3(1.2f, 1.2f, 1.2f), "CrateStack_A");
        CreateFurniture(propsRoot, new Vector3(areaSize.x * 0.28f, 0.55f, -areaSize.z * 0.28f), new Vector3(2.1f, 1.0f, 0.9f), "Cabinet_A");
        CreateFurniture(propsRoot, new Vector3(-areaSize.x * 0.02f, UpperDeckHeight + 0.45f, -areaSize.z * 0.24f), new Vector3(1.8f, 0.9f, 0.75f), "UpperStorage_A");
        CreateFurniture(propsRoot, new Vector3(areaSize.x * 0.27f, UpperDeckHeight + 0.5f, -areaSize.z * 0.06f), new Vector3(1.1f, 1.0f, 1.1f), "UpperCrates_A");
    }

    private static void CreateFurniture(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject furniture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        furniture.name = name;
        furniture.transform.SetParent(parent, false);
        furniture.transform.localPosition = localPosition;
        furniture.transform.localScale = scale;
        ApplyColor(furniture, new Color(0.52f, 0.47f, 0.4f, 1f));
    }

    private static void CreateLadderRail(Transform parent, float xOffset)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = xOffset < 0f ? "Rail_Left" : "Rail_Right";
        rail.transform.SetParent(parent, false);
        rail.transform.localPosition = new Vector3(xOffset, 0f, 0f);
        rail.transform.localScale = new Vector3(0.08f, UpperDeckHeight + 0.3f, 0.08f);
        ApplyColor(rail, new Color(0.58f, 0.42f, 0.28f, 1f));
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

    private static Vector3 GetBossInteriorSpawnPosition()
    {
        if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
        {
            Vector3 selectedPosition = Selection.activeTransform.position;
            return new Vector3(selectedPosition.x, 0f, selectedPosition.z);
        }

        GameObject bossMarker = GameObject.Find("BossMarker");
        if (bossMarker != null)
        {
            Vector3 markerPosition = bossMarker.transform.position;
            return new Vector3(markerPosition.x, 0f, markerPosition.z);
        }

        return Vector3.zero;
    }
}

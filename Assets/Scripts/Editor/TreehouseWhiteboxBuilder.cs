using UnityEditor;
using UnityEngine;

/// <summary>
/// 在当前场景中一键生成树屋白模
/// 生成完成后会自动选中根节点，方便直接拖动到目标位置
/// </summary>
public static class TreehouseWhiteboxBuilder
{
    private const float TrunkHeight = 10f;
    private const float TrunkRadius = 1.2f;
    private const float PlatformHeight = 5.6f;
    private const float PlatformWidth = 8f;
    private const float PlatformDepth = 6f;
    private const float HutWidth = 5.2f;
    private const float HutDepth = 3.6f;
    private const float HutHeight = 2.8f;
    private const float WallThickness = 0.18f;
    private const float IslandCellSize = 6f;

    [MenuItem("Tools/Whitebox/Create Treehouse Whitebox")]
    private static void CreateTreehouseWhitebox()
    {
        Vector3 spawnPosition = GetSpawnPosition();

        GameObject root = new GameObject("Whitebox_Treehouse");
        Undo.RegisterCreatedObjectUndo(root, "Create Treehouse Whitebox");
        root.transform.position = spawnPosition;

        CreateTrunk(root.transform);
        CreateBranchSupports(root.transform);
        CreatePlatform(root.transform);
        CreateHut(root.transform);
        CreateRoof(root.transform);
        CreateLadder(root.transform);
        CreateSmallBridge(root.transform);
        CreateLookoutPost(root.transform);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Whitebox/Create Boss Core Treehouse Whitebox")]
    private static void CreateBossCoreTreehouseWhitebox()
    {
        Vector3 spawnPosition = GetBossCoreSpawnPosition();

        GameObject root = new GameObject("Whitebox_BossCoreTreehouse");
        Undo.RegisterCreatedObjectUndo(root, "Create Boss Core Treehouse Whitebox");
        root.transform.position = spawnPosition;

        CreateBossCoreTrunks(root.transform);
        CreateBossCorePlatforms(root.transform);
        CreateBossCoreMainHut(root.transform);
        CreateBossCoreSideHut(root.transform);
        CreateBossCoreOpenFrame(root.transform);
        CreateBossCoreBridge(root.transform);
        CreateBossCoreStairs(root.transform);
        CreateBossCoreLowerDeck(root.transform);
        CreateBossCoreUpperCatwalk(root.transform);
        CreateBossCoreCover(root.transform);
        CreateBossCoreLookout(root.transform);
        EnsurePerspectiveFadeController();

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Whitebox/Create West Resource Lab Whitebox")]
    private static void CreateWestResourceLabWhitebox()
    {
        Vector3 spawnPosition = GetWestResourceSpawnPosition();

        GameObject root = new GameObject("Whitebox_WestResourceLab");
        Undo.RegisterCreatedObjectUndo(root, "Create West Resource Lab Whitebox");
        root.transform.position = spawnPosition;

        CreateLabBase(root.transform);
        CreateLabOuterShell(root.transform);
        CreateLabInteriorPartitions(root.transform);
        CreateLabCombatSpace(root.transform);
        CreateLabProps(root.transform);
        CreateLabWalkway(root.transform);
        EnsurePerspectiveFadeController();

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Whitebox/Create Fishing Village Battle Whitebox")]
    private static void CreateFishingVillageBattleWhitebox()
    {
        Vector3 spawnPosition = GetFocusedSpawnPosition();

        GameObject root = new GameObject("Whitebox_FishingVillageBattle");
        Undo.RegisterCreatedObjectUndo(root, "Create Fishing Village Battle Whitebox");
        root.transform.position = spawnPosition;

        CreateFishingVillageGround(root.transform);
        CreateFishingVillagePerimeter(root.transform);
        CreateFishingVillageHuts(root.transform);
        CreateFishingVillageDock(root.transform);
        CreateFishingVillageCover(root.transform);
        CreateFishingVillageDetails(root.transform);
        EnsurePerspectiveFadeController();

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static Vector3 GetSpawnPosition()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            return new Vector3(pivot.x, 0f, pivot.z);
        }

        return Vector3.zero;
    }

    private static Vector3 GetFocusedSpawnPosition()
    {
        if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
        {
            Vector3 selectedPosition = Selection.activeTransform.position;
            return new Vector3(selectedPosition.x, 0f, selectedPosition.z);
        }

        return GetSpawnPosition();
    }

    private static Vector3 GetBossCoreSpawnPosition()
    {
        GameObject bossMarker = GameObject.Find("BossMarker");
        if (bossMarker != null)
        {
            Vector3 markerPosition = bossMarker.transform.position;
            return new Vector3(markerPosition.x, 0f, markerPosition.z);
        }

        return GridToIslandWorld(10f, 8f, 0f);
    }

    private static Vector3 GetWestResourceSpawnPosition()
    {
        GameObject westLabel = GameObject.Find("Label_WestUpper");
        if (westLabel != null)
        {
            return new Vector3(westLabel.transform.position.x, 0f, westLabel.transform.position.z - 1.5f);
        }

        return GridToIslandWorld(6f, 7.2f, 0f);
    }

    private static void CreateTrunk(Transform parent)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(parent, false);
        trunk.transform.localPosition = new Vector3(0f, TrunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(TrunkRadius, TrunkHeight * 0.5f, TrunkRadius);
        ApplyColor(trunk, new Color(0.34f, 0.23f, 0.14f, 1f));
    }

    private static void CreateBranchSupports(Transform parent)
    {
        Vector3[] offsets =
        {
            new Vector3(2.4f, PlatformHeight - 0.7f, 1.8f),
            new Vector3(-2.2f, PlatformHeight - 0.4f, -1.4f),
            new Vector3(1.8f, PlatformHeight - 0.2f, -2.1f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            support.name = $"BranchSupport_{i}";
            support.transform.SetParent(parent, false);
            support.transform.localPosition = offsets[i];
            support.transform.localRotation = Quaternion.Euler(25f, i * 55f, 18f);
            support.transform.localScale = new Vector3(0.32f, 2.6f, 0.32f);
            ApplyColor(support, new Color(0.44f, 0.31f, 0.2f, 1f));
        }
    }

    private static void CreatePlatform(Transform parent)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(parent, false);
        platform.transform.localPosition = new Vector3(0f, PlatformHeight, 0f);
        platform.transform.localScale = new Vector3(PlatformWidth, 0.28f, PlatformDepth);
        ApplyColor(platform, new Color(0.63f, 0.63f, 0.63f, 1f));

        CreateRailing(parent, new Vector3(0f, PlatformHeight + 0.7f, PlatformDepth * 0.5f - 0.08f), new Vector3(PlatformWidth, 1.1f, 0.12f), "Railing_Front");
        CreateRailing(parent, new Vector3(0f, PlatformHeight + 0.7f, -PlatformDepth * 0.5f + 0.08f), new Vector3(PlatformWidth, 1.1f, 0.12f), "Railing_Back");
        CreateRailing(parent, new Vector3(PlatformWidth * 0.5f - 0.08f, PlatformHeight + 0.7f, 0f), new Vector3(0.12f, 1.1f, PlatformDepth), "Railing_Right");
        CreateRailing(parent, new Vector3(-PlatformWidth * 0.5f + 0.08f, PlatformHeight + 0.7f, 0f), new Vector3(0.12f, 1.1f, PlatformDepth * 0.45f), "Railing_Left");
    }

    private static void CreateHut(Transform parent)
    {
        Transform hutRoot = new GameObject("Hut").transform;
        hutRoot.SetParent(parent, false);
        hutRoot.localPosition = new Vector3(0.2f, PlatformHeight + 0.14f, 0f);

        CreateWall(hutRoot, new Vector3(0f, HutHeight * 0.5f, HutDepth * 0.5f), new Vector3(HutWidth, HutHeight, WallThickness), "Wall_Front");
        CreateWall(hutRoot, new Vector3(0f, HutHeight * 0.5f, -HutDepth * 0.5f), new Vector3(HutWidth, HutHeight, WallThickness), "Wall_Back");
        CreateWall(hutRoot, new Vector3(HutWidth * 0.5f, HutHeight * 0.5f, 0f), new Vector3(WallThickness, HutHeight, HutDepth), "Wall_Right");

        GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "Wall_Left";
        leftWall.transform.SetParent(hutRoot, false);
        leftWall.transform.localPosition = new Vector3(-HutWidth * 0.5f, HutHeight * 0.5f, -0.7f);
        leftWall.transform.localScale = new Vector3(WallThickness, HutHeight, HutDepth - 1.4f);
        ApplyColor(leftWall, new Color(0.78f, 0.78f, 0.8f, 1f));

        GameObject doorwayHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorwayHeader.name = "DoorwayHeader";
        doorwayHeader.transform.SetParent(hutRoot, false);
        doorwayHeader.transform.localPosition = new Vector3(-HutWidth * 0.5f, HutHeight - 0.35f, 0.95f);
        doorwayHeader.transform.localScale = new Vector3(WallThickness, 0.7f, 1.1f);
        ApplyColor(doorwayHeader, new Color(0.78f, 0.78f, 0.8f, 1f));
    }

    private static void CreateRoof(Transform parent)
    {
        Transform roofRoot = new GameObject("Roof").transform;
        roofRoot.SetParent(parent, false);
        roofRoot.localPosition = new Vector3(0.2f, PlatformHeight + HutHeight + 0.55f, 0f);

        GameObject roofLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofLeft.name = "Roof_Left";
        roofLeft.transform.SetParent(roofRoot, false);
        roofLeft.transform.localPosition = new Vector3(-0.55f, 0f, 0f);
        roofLeft.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        roofLeft.transform.localScale = new Vector3(HutWidth * 0.6f, 0.18f, HutDepth + 1.1f);
        ApplyColor(roofLeft, new Color(0.5f, 0.3f, 0.18f, 1f));

        GameObject roofRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roofRight.name = "Roof_Right";
        roofRight.transform.SetParent(roofRoot, false);
        roofRight.transform.localPosition = new Vector3(0.55f, 0f, 0f);
        roofRight.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
        roofRight.transform.localScale = new Vector3(HutWidth * 0.6f, 0.18f, HutDepth + 1.1f);
        ApplyColor(roofRight, new Color(0.5f, 0.3f, 0.18f, 1f));
    }

    private static void CreateLadder(Transform parent)
    {
        Transform ladderRoot = new GameObject("Ladder").transform;
        ladderRoot.SetParent(parent, false);
        ladderRoot.localPosition = new Vector3(-PlatformWidth * 0.5f - 0.35f, PlatformHeight * 0.5f, 1.15f);

        CreateLadderRail(ladderRoot, -0.25f);
        CreateLadderRail(ladderRoot, 0.25f);

        for (int i = 0; i < 7; i++)
        {
            GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rung.name = $"Rung_{i}";
            rung.transform.SetParent(ladderRoot, false);
            rung.transform.localPosition = new Vector3(0f, -2.4f + i * 0.8f, 0f);
            rung.transform.localScale = new Vector3(0.56f, 0.08f, 0.08f);
            ApplyColor(rung, new Color(0.55f, 0.39f, 0.24f, 1f));
        }
    }

    private static void CreateLadderRail(Transform parent, float xOffset)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = xOffset < 0f ? "Rail_Left" : "Rail_Right";
        rail.transform.SetParent(parent, false);
        rail.transform.localPosition = new Vector3(xOffset, 0f, 0f);
        rail.transform.localScale = new Vector3(0.08f, PlatformHeight + 0.4f, 0.08f);
        ApplyColor(rail, new Color(0.55f, 0.39f, 0.24f, 1f));
    }

    private static void CreateSmallBridge(Transform parent)
    {
        GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "Bridge";
        bridge.transform.SetParent(parent, false);
        bridge.transform.localPosition = new Vector3(PlatformWidth * 0.5f + 2.3f, PlatformHeight - 0.02f, -0.9f);
        bridge.transform.localScale = new Vector3(4.6f, 0.18f, 1.4f);
        ApplyColor(bridge, new Color(0.62f, 0.62f, 0.64f, 1f));

        CreateRailing(parent, bridge.transform.localPosition + new Vector3(0f, 0.65f, 0.62f), new Vector3(4.6f, 1f, 0.12f), "Bridge_Railing_Front");
        CreateRailing(parent, bridge.transform.localPosition + new Vector3(0f, 0.65f, -0.62f), new Vector3(4.6f, 1f, 0.12f), "Bridge_Railing_Back");
    }

    private static void CreateLookoutPost(Transform parent)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "LookoutPost";
        post.transform.SetParent(parent, false);
        post.transform.localPosition = new Vector3(PlatformWidth * 0.5f - 0.4f, PlatformHeight + 1.15f, PlatformDepth * 0.5f - 0.4f);
        post.transform.localScale = new Vector3(0.18f, 2.3f, 0.18f);
        ApplyColor(post, new Color(0.55f, 0.39f, 0.24f, 1f));
    }

    private static void CreateBossCoreTrunks(Transform parent)
    {
        Vector3[] trunkOffsets =
        {
            new Vector3(-2.4f, 5.4f, 0.6f),
            new Vector3(1.9f, 5.0f, -1.1f),
            new Vector3(0.1f, 4.6f, 2.4f)
        };

        float[] heights = { 11.2f, 10.4f, 9.6f };
        float[] radii = { 1.15f, 0.92f, 0.78f };

        for (int i = 0; i < trunkOffsets.Length; i++)
        {
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = $"BossTrunk_{i}";
            trunk.transform.SetParent(parent, false);
            trunk.transform.localPosition = new Vector3(trunkOffsets[i].x, heights[i] * 0.5f, trunkOffsets[i].z);
            trunk.transform.localScale = new Vector3(radii[i], heights[i] * 0.5f, radii[i]);
            ApplyColor(trunk, new Color(0.31f, 0.22f, 0.13f, 1f));
        }
    }

    private static void CreateBossCorePlatforms(Transform parent)
    {
        GameObject mainPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainPlatform.name = "BossMainPlatform";
        mainPlatform.transform.SetParent(parent, false);
        mainPlatform.transform.localPosition = new Vector3(0f, 6.4f, -0.2f);
        mainPlatform.transform.localScale = new Vector3(19.2f, 0.32f, 13.2f);
        ApplyColor(mainPlatform, new Color(0.63f, 0.63f, 0.65f, 1f));

        GameObject sidePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sidePlatform.name = "BossSidePlatform";
        sidePlatform.transform.SetParent(parent, false);
        sidePlatform.transform.localPosition = new Vector3(8.6f, 7.1f, -1.8f);
        sidePlatform.transform.localScale = new Vector3(7.6f, 0.26f, 4.8f);
        ApplyColor(sidePlatform, new Color(0.61f, 0.61f, 0.64f, 1f));

        GameObject rearPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rearPlatform.name = "BossRearPlatform";
        rearPlatform.transform.SetParent(parent, false);
        rearPlatform.transform.localPosition = new Vector3(-8.9f, 5.8f, 1.4f);
        rearPlatform.transform.localScale = new Vector3(6.4f, 0.24f, 4.6f);
        ApplyColor(rearPlatform, new Color(0.58f, 0.58f, 0.61f, 1f));

        GameObject frontPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontPlatform.name = "BossFrontPlatform";
        frontPlatform.transform.SetParent(parent, false);
        frontPlatform.transform.localPosition = new Vector3(0f, 5.2f, 5.0f);
        frontPlatform.transform.localScale = new Vector3(13.4f, 0.24f, 3.4f);
        ApplyColor(frontPlatform, new Color(0.6f, 0.6f, 0.63f, 1f));

        GameObject lowerBattleDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lowerBattleDeck.name = "BossLowerBattleDeck";
        lowerBattleDeck.transform.SetParent(parent, false);
        lowerBattleDeck.transform.localPosition = new Vector3(0f, 3.65f, 0.2f);
        lowerBattleDeck.transform.localScale = new Vector3(16.6f, 0.26f, 6.2f);
        ApplyColor(lowerBattleDeck, new Color(0.56f, 0.56f, 0.59f, 1f));

        CreateRailing(parent, new Vector3(0f, 7.15f, 6.45f), new Vector3(19.2f, 1.35f, 0.12f), "BossRailing_Front");
        CreateRailing(parent, new Vector3(0f, 7.15f, -6.45f), new Vector3(19.2f, 1.35f, 0.12f), "BossRailing_Back");
        CreateRailing(parent, new Vector3(-9.45f, 7.15f, -0.1f), new Vector3(0.12f, 1.35f, 12.6f), "BossRailing_Left");
        CreateRailing(parent, new Vector3(9.45f, 7.15f, -0.1f), new Vector3(0.12f, 1.35f, 12.6f), "BossRailing_Right");
    }

    private static void CreateBossCoreMainHut(Transform parent)
    {
        Transform hutRoot = new GameObject("BossMainHut").transform;
        hutRoot.SetParent(parent, false);
        hutRoot.localPosition = new Vector3(-2.1f, 6.56f, 0.8f);

        CreateWall(hutRoot, new Vector3(0f, 1.8f, 2.6f), new Vector3(7.4f, 3.6f, WallThickness), "MainWall_Front");
        CreateWall(hutRoot, new Vector3(0f, 1.8f, -2.6f), new Vector3(7.4f, 3.6f, WallThickness), "MainWall_Back");
        CreateWall(hutRoot, new Vector3(3.7f, 1.8f, 0f), new Vector3(WallThickness, 3.6f, 5.2f), "MainWall_Right");
        CreateWall(hutRoot, new Vector3(-3.7f, 1.8f, -1.2f), new Vector3(WallThickness, 3.6f, 2.4f), "MainWall_Left");

        GameObject doorwayHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorwayHeader.name = "MainDoorHeader";
        doorwayHeader.transform.SetParent(hutRoot, false);
        doorwayHeader.transform.localPosition = new Vector3(-3.7f, 2.95f, 1.4f);
        doorwayHeader.transform.localScale = new Vector3(WallThickness, 1.3f, 1.7f);
        ApplyColor(doorwayHeader, new Color(0.78f, 0.78f, 0.8f, 1f));

        GameObject supportPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        supportPost.name = "MainSupportPost";
        supportPost.transform.SetParent(hutRoot, false);
        supportPost.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        supportPost.transform.localScale = new Vector3(0.3f, 3.2f, 0.3f);
        ApplyColor(supportPost, new Color(0.45f, 0.32f, 0.2f, 1f));
    }

    private static void CreateBossCoreSideHut(Transform parent)
    {
        Transform hutRoot = new GameObject("BossSideHut").transform;
        hutRoot.SetParent(parent, false);
        hutRoot.localPosition = new Vector3(7.4f, 7.22f, -1.8f);

        CreateWall(hutRoot, new Vector3(0f, 1.4f, 1.6f), new Vector3(4.4f, 2.8f, WallThickness), "SideWall_Front");
        CreateWall(hutRoot, new Vector3(0f, 1.4f, -1.6f), new Vector3(4.4f, 2.8f, WallThickness), "SideWall_Back");
        CreateWall(hutRoot, new Vector3(2.2f, 1.4f, 0f), new Vector3(WallThickness, 2.8f, 3.2f), "SideWall_Right");
        CreateWall(hutRoot, new Vector3(-2.2f, 1.4f, -0.45f), new Vector3(WallThickness, 2.8f, 2.3f), "SideWall_Left");
    }

    private static void CreateBossCoreOpenFrame(Transform parent)
    {
        Transform frameRoot = new GameObject("BossOpenFrame").transform;
        frameRoot.SetParent(parent, false);
        frameRoot.localPosition = new Vector3(0f, 9.6f, 0f);

        Vector3[] postPositions =
        {
            new Vector3(-5.6f, 0f, -4.2f),
            new Vector3(-5.6f, 0f, 4.2f),
            new Vector3(5.6f, 0f, -4.2f),
            new Vector3(5.6f, 0f, 4.2f)
        };

        for (int i = 0; i < postPositions.Length; i++)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = $"FramePost_{i}";
            post.transform.SetParent(frameRoot, false);
            post.transform.localPosition = postPositions[i];
            post.transform.localScale = new Vector3(0.3f, 6.2f, 0.3f);
            ApplyColor(post, new Color(0.46f, 0.33f, 0.22f, 1f));
        }

        GameObject topBeamFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topBeamFront.name = "TopBeam_Front";
        topBeamFront.transform.SetParent(frameRoot, false);
        topBeamFront.transform.localPosition = new Vector3(0f, 3f, 4.2f);
        topBeamFront.transform.localScale = new Vector3(11.6f, 0.18f, 0.18f);
        ApplyColor(topBeamFront, new Color(0.46f, 0.33f, 0.22f, 1f));

        GameObject topBeamBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        topBeamBack.name = "TopBeam_Back";
        topBeamBack.transform.SetParent(frameRoot, false);
        topBeamBack.transform.localPosition = new Vector3(0f, 3f, -4.2f);
        topBeamBack.transform.localScale = new Vector3(11.6f, 0.18f, 0.18f);
        ApplyColor(topBeamBack, new Color(0.46f, 0.33f, 0.22f, 1f));
    }

    private static void CreateBossCoreBridge(Transform parent)
    {
        GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bridge.name = "BossBridge";
        bridge.transform.SetParent(parent, false);
        bridge.transform.localPosition = new Vector3(4.9f, 6.92f, -1.25f);
        bridge.transform.localScale = new Vector3(10.2f, 0.2f, 1.8f);
        ApplyColor(bridge, new Color(0.62f, 0.62f, 0.64f, 1f));

        CreateRailing(parent, bridge.transform.localPosition + new Vector3(0f, 0.8f, 0.82f), new Vector3(10.2f, 1.1f, 0.12f), "BossBridge_Railing_Front");
        CreateRailing(parent, bridge.transform.localPosition + new Vector3(0f, 0.8f, -0.82f), new Vector3(10.2f, 1.1f, 0.12f), "BossBridge_Railing_Back");
    }

    private static void CreateBossCoreStairs(Transform parent)
    {
        Transform stairsRoot = new GameObject("BossStairs").transform;
        stairsRoot.SetParent(parent, false);
        stairsRoot.localPosition = new Vector3(-6.5f, 3.35f, 2.8f);

        for (int i = 0; i < 6; i++)
        {
            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i}";
            step.transform.SetParent(stairsRoot, false);
            step.transform.localPosition = new Vector3(i * 0.48f, i * 0.28f, 0f);
            step.transform.localScale = new Vector3(1.1f, 0.2f, 2.4f);
            ApplyColor(step, new Color(0.66f, 0.66f, 0.68f, 1f));
        }
    }

    private static void CreateBossCoreLowerDeck(Transform parent)
    {
        GameObject lowerDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lowerDeck.name = "BossLowerDeck";
        lowerDeck.transform.SetParent(parent, false);
        lowerDeck.transform.localPosition = new Vector3(0f, 3.6f, 4.2f);
        lowerDeck.transform.localScale = new Vector3(12.8f, 0.24f, 4.2f);
        ApplyColor(lowerDeck, new Color(0.59f, 0.59f, 0.61f, 1f));
    }

    private static void CreateBossCoreUpperCatwalk(Transform parent)
    {
        GameObject catwalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        catwalk.name = "BossUpperCatwalk";
        catwalk.transform.SetParent(parent, false);
        catwalk.transform.localPosition = new Vector3(0f, 9.4f, -5.0f);
        catwalk.transform.localScale = new Vector3(14.8f, 0.2f, 1.6f);
        ApplyColor(catwalk, new Color(0.6f, 0.6f, 0.63f, 1f));

        CreateRailing(parent, catwalk.transform.localPosition + new Vector3(0f, 0.78f, 0.72f), new Vector3(14.8f, 0.95f, 0.12f), "BossCatwalk_Railing_Front");
        CreateRailing(parent, catwalk.transform.localPosition + new Vector3(0f, 0.78f, -0.72f), new Vector3(14.8f, 0.95f, 0.12f), "BossCatwalk_Railing_Back");
    }

    private static void CreateBossCoreCover(Transform parent)
    {
        Transform coverRoot = new GameObject("BossCover").transform;
        coverRoot.SetParent(parent, false);

        Vector3[] coverPositions =
        {
            new Vector3(2.2f, 6.75f, 2.0f),
            new Vector3(-2.4f, 6.75f, -2.0f),
            new Vector3(5.0f, 7.4f, -0.6f)
        };

        Vector3[] coverScales =
        {
            new Vector3(1.5f, 1.1f, 0.9f),
            new Vector3(1.1f, 1.4f, 1.1f),
            new Vector3(0.9f, 1.6f, 0.9f)
        };

        for (int i = 0; i < coverPositions.Length; i++)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = $"Cover_{i}";
            cover.transform.SetParent(coverRoot, false);
            cover.transform.localPosition = coverPositions[i];
            cover.transform.localScale = coverScales[i];
            ApplyColor(cover, new Color(0.48f, 0.49f, 0.52f, 1f));
        }
    }

    private static void CreateBossCoreLookout(Transform parent)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "BossLookoutPost";
        post.transform.SetParent(parent, false);
        post.transform.localPosition = new Vector3(6.1f, 9.1f, 0.8f);
        post.transform.localScale = new Vector3(0.22f, 3.6f, 0.22f);
        ApplyColor(post, new Color(0.48f, 0.33f, 0.2f, 1f));

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beacon.name = "BossBeacon";
        beacon.transform.SetParent(parent, false);
        beacon.transform.localPosition = new Vector3(6.1f, 10.85f, 0.8f);
        beacon.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
        ApplyColor(beacon, new Color(0.95f, 0.82f, 0.35f, 1f));
    }

    private static void CreateLabBase(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "LabFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        floor.transform.localScale = new Vector3(16.5f, 0.22f, 15.5f);
        ApplyColor(floor, new Color(0.72f, 0.74f, 0.76f, 1f));

        GameObject subFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        subFloor.name = "LabSubFloor";
        subFloor.transform.SetParent(parent, false);
        subFloor.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        subFloor.transform.localScale = new Vector3(17f, 0.24f, 16f);
        ApplyColor(subFloor, new Color(0.28f, 0.31f, 0.36f, 1f));
    }

    private static void CreateLabOuterShell(Transform parent)
    {
        Transform shellRoot = new GameObject("OuterShell").transform;
        shellRoot.SetParent(parent, false);

        CreateWall(shellRoot, new Vector3(0f, 1.7f, 7.55f), new Vector3(16.5f, 3.4f, 0.22f), "NorthWall");
        CreateWall(shellRoot, new Vector3(0f, 1.7f, -7.55f), new Vector3(16.5f, 3.4f, 0.22f), "SouthWall");
        CreateWall(shellRoot, new Vector3(-8.15f, 1.7f, 0f), new Vector3(0.22f, 3.4f, 15.1f), "WestWall");
        CreateWall(shellRoot, new Vector3(8.15f, 1.7f, -3.4f), new Vector3(0.22f, 3.4f, 8.3f), "EastWall_North");
        CreateWall(shellRoot, new Vector3(8.15f, 1.7f, 4.6f), new Vector3(0.22f, 3.4f, 5.6f), "EastWall_South");

        GameObject doorHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorHeader.name = "EastDoorHeader";
        doorHeader.transform.SetParent(shellRoot, false);
        doorHeader.transform.localPosition = new Vector3(8.15f, 2.8f, 1.0f);
        doorHeader.transform.localScale = new Vector3(0.22f, 1.2f, 2.2f);
        ApplyColor(doorHeader, new Color(0.84f, 0.84f, 0.86f, 1f));
        doorHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateLabInteriorPartitions(Transform parent)
    {
        Transform partitionRoot = new GameObject("Partitions").transform;
        partitionRoot.SetParent(parent, false);

        CreateWall(partitionRoot, new Vector3(-2.2f, 1.55f, 2.6f), new Vector3(0.18f, 3.1f, 7.6f), "Partition_LeftVertical");
        CreateWall(partitionRoot, new Vector3(2.7f, 1.55f, -2.4f), new Vector3(0.18f, 3.1f, 7.1f), "Partition_RightVertical");
        CreateWall(partitionRoot, new Vector3(-3.9f, 1.55f, -1.5f), new Vector3(5.2f, 3.1f, 0.18f), "Partition_LeftHorizontal");
        CreateWall(partitionRoot, new Vector3(3.9f, 1.55f, 2.1f), new Vector3(4.8f, 3.1f, 0.18f), "Partition_RightHorizontal");
        CreateWall(partitionRoot, new Vector3(0.4f, 1.55f, 5.1f), new Vector3(5.4f, 3.1f, 0.18f), "Partition_NorthInner");
    }

    private static void CreateLabCombatSpace(Transform parent)
    {
        GameObject combatFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        combatFloor.name = "CombatSpace";
        combatFloor.transform.SetParent(parent, false);
        combatFloor.transform.localPosition = new Vector3(0.6f, 0.16f, 0.4f);
        combatFloor.transform.localScale = new Vector3(6.4f, 0.05f, 5.6f);
        ApplyColor(combatFloor, new Color(0.48f, 0.52f, 0.6f, 1f));
    }

    private static void CreateLabProps(Transform parent)
    {
        Transform propsRoot = new GameObject("Props").transform;
        propsRoot.SetParent(parent, false);

        CreateLabBench(propsRoot, new Vector3(-5.4f, 0.75f, 5.4f), new Vector3(2.4f, 1.3f, 0.9f), "Bench_A");
        CreateLabBench(propsRoot, new Vector3(-5.4f, 0.75f, 3.8f), new Vector3(2.4f, 1.3f, 0.9f), "Bench_B");
        CreateLabBench(propsRoot, new Vector3(5.2f, 0.75f, -4.5f), new Vector3(2.2f, 1.3f, 0.9f), "Bench_C");
        CreateLabBench(propsRoot, new Vector3(5.2f, 0.75f, -2.9f), new Vector3(2.2f, 1.3f, 0.9f), "Bench_D");

        CreateStorageRack(propsRoot, new Vector3(-6.0f, 1.2f, -4.8f), "StorageRack_A");
        CreateStorageRack(propsRoot, new Vector3(-6.0f, 1.2f, -2.8f), "StorageRack_B");

        CreateTank(propsRoot, new Vector3(3.8f, 1.35f, 5.4f), "SpecimenTank_A");
        CreateTank(propsRoot, new Vector3(5.8f, 1.35f, 5.4f), "SpecimenTank_B");

        CreateConsole(propsRoot, new Vector3(0.2f, 0.9f, -5.6f), "Console_Center");
        CreateConsole(propsRoot, new Vector3(2.9f, 0.9f, 5.8f), "Console_North");
    }

    private static void CreateLabWalkway(Transform parent)
    {
        GameObject walkway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        walkway.name = "UpperWalkway";
        walkway.transform.SetParent(parent, false);
        walkway.transform.localPosition = new Vector3(-1.2f, 2.4f, -4.2f);
        walkway.transform.localScale = new Vector3(6.4f, 0.18f, 1.4f);
        ApplyColor(walkway, new Color(0.58f, 0.6f, 0.63f, 1f));

        CreateRailing(parent, walkway.transform.localPosition + new Vector3(0f, 0.7f, 0.62f), new Vector3(6.4f, 0.95f, 0.12f), "Walkway_Railing_Front");
        CreateRailing(parent, walkway.transform.localPosition + new Vector3(0f, 0.7f, -0.62f), new Vector3(6.4f, 0.95f, 0.12f), "Walkway_Railing_Back");
    }

    private static void CreateFishingVillageGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "VillageGround";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ground.transform.localScale = new Vector3(18f, 0.18f, 14f);
        ApplyColor(ground, new Color(0.68f, 0.66f, 0.62f, 1f));

        GameObject dampEdge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dampEdge.name = "MudEdge";
        dampEdge.transform.SetParent(parent, false);
        dampEdge.transform.localPosition = new Vector3(0f, -0.04f, 4.7f);
        dampEdge.transform.localScale = new Vector3(18f, 0.12f, 4.6f);
        ApplyColor(dampEdge, new Color(0.4f, 0.35f, 0.28f, 1f));
    }

    private static void CreateFishingVillagePerimeter(Transform parent)
    {
        Transform perimeterRoot = new GameObject("VillagePerimeter").transform;
        perimeterRoot.SetParent(parent, false);

        CreateWall(perimeterRoot, new Vector3(-8.9f, 1.4f, 0f), new Vector3(0.2f, 2.8f, 12.8f), "WestRockWall");
        CreateWall(perimeterRoot, new Vector3(8.9f, 1.4f, -0.4f), new Vector3(0.2f, 2.8f, 11.4f), "EastFenceLine");
        CreateWall(perimeterRoot, new Vector3(0f, 1.4f, -6.9f), new Vector3(17.8f, 2.8f, 0.2f), "NorthBoundary");

        GameObject southFence = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southFence.name = "SouthPierEdge";
        southFence.transform.SetParent(perimeterRoot, false);
        southFence.transform.localPosition = new Vector3(0f, 0.9f, 6.4f);
        southFence.transform.localScale = new Vector3(14.2f, 0.12f, 0.12f);
        ApplyColor(southFence, new Color(0.46f, 0.34f, 0.22f, 1f));
        southFence.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateFishingVillageHuts(Transform parent)
    {
        Transform hutsRoot = new GameObject("VillageHuts").transform;
        hutsRoot.SetParent(parent, false);

        CreateFishingHut(hutsRoot, new Vector3(-5.5f, 0f, -3.4f), new Vector3(4.2f, 3.2f, 3.4f), "Hut_West");
        CreateFishingHut(hutsRoot, new Vector3(5.4f, 0f, -2.6f), new Vector3(4.8f, 3.4f, 3.8f), "Hut_East");
        CreateFishingHut(hutsRoot, new Vector3(6.1f, 0f, 3.6f), new Vector3(3.8f, 2.8f, 3.2f), "Hut_SouthEast");
    }

    private static void CreateFishingVillageDock(Transform parent)
    {
        Transform dockRoot = new GameObject("VillageDock").transform;
        dockRoot.SetParent(parent, false);

        GameObject dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dock.name = "MainDock";
        dock.transform.SetParent(dockRoot, false);
        dock.transform.localPosition = new Vector3(-0.8f, 0.18f, 5.6f);
        dock.transform.localScale = new Vector3(8.6f, 0.18f, 2.2f);
        ApplyColor(dock, new Color(0.52f, 0.4f, 0.26f, 1f));

        CreateRailing(dockRoot, dock.transform.localPosition + new Vector3(0f, 0.72f, 0.95f), new Vector3(8.4f, 0.8f, 0.12f), "DockRail_Front");
        CreateRailing(dockRoot, dock.transform.localPosition + new Vector3(0f, 0.72f, -0.95f), new Vector3(8.4f, 0.8f, 0.12f), "DockRail_Back");

        GameObject sidePier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sidePier.name = "SidePier";
        sidePier.transform.SetParent(dockRoot, false);
        sidePier.transform.localPosition = new Vector3(4.2f, 0.18f, 4.1f);
        sidePier.transform.localScale = new Vector3(1.8f, 0.18f, 3.4f);
        ApplyColor(sidePier, new Color(0.52f, 0.4f, 0.26f, 1f));
    }

    private static void CreateFishingVillageCover(Transform parent)
    {
        Transform coverRoot = new GameObject("VillageCover").transform;
        coverRoot.SetParent(parent, false);

        CreateRockCluster(coverRoot, new Vector3(-2.1f, 0.55f, 0.8f), "RockCluster_A");
        CreateRockCluster(coverRoot, new Vector3(2.8f, 0.55f, 1.4f), "RockCluster_B");
        CreateRockCluster(coverRoot, new Vector3(0.5f, 0.55f, -2.4f), "RockCluster_C");

        CreateBoatCover(coverRoot, new Vector3(-4.8f, 0.55f, 3.2f), "BoatCover_A");
        CreateBoatCover(coverRoot, new Vector3(4.0f, 0.55f, 2.5f), "BoatCover_B");
    }

    private static void CreateFishingVillageDetails(Transform parent)
    {
        Transform detailsRoot = new GameObject("VillageDetails").transform;
        detailsRoot.SetParent(parent, false);

        CreateFishingRack(detailsRoot, new Vector3(-6.2f, 0.75f, 1.8f), "FishRack_A");
        CreateFishingRack(detailsRoot, new Vector3(6.4f, 0.75f, 0.8f), "FishRack_B");

        CreateCrateStack(detailsRoot, new Vector3(-1.6f, 0.45f, -4.8f), "CrateStack_A");
        CreateCrateStack(detailsRoot, new Vector3(3.6f, 0.45f, -4.6f), "CrateStack_B");

        CreateNetPost(detailsRoot, new Vector3(0.8f, 1.2f, 5.0f), "NetPost_A");
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
        floor.transform.localScale = new Vector3(footprint.x, 0.22f, footprint.z);
        ApplyColor(floor, new Color(0.56f, 0.44f, 0.28f, 1f));

        CreateWall(hutRoot, new Vector3(0f, halfHeight, halfDepth), new Vector3(footprint.x, footprint.y, 0.16f), "Wall_Front");
        CreateWall(hutRoot, new Vector3(0f, halfHeight, -halfDepth), new Vector3(footprint.x, footprint.y, 0.16f), "Wall_Back");
        CreateWall(hutRoot, new Vector3(halfWidth, halfHeight, 0f), new Vector3(0.16f, footprint.y, footprint.z), "Wall_Right");
        CreateWall(hutRoot, new Vector3(-halfWidth, halfHeight, -0.55f), new Vector3(0.16f, footprint.y, footprint.z - 1.1f), "Wall_Left");

        GameObject doorwayHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
        doorwayHeader.name = "DoorHeader";
        doorwayHeader.transform.SetParent(hutRoot, false);
        doorwayHeader.transform.localPosition = new Vector3(-halfWidth, footprint.y - 0.38f, 0.6f);
        doorwayHeader.transform.localScale = new Vector3(0.16f, 0.75f, 1.2f);
        ApplyColor(doorwayHeader, new Color(0.72f, 0.72f, 0.74f, 1f));
        doorwayHeader.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateRockCluster(Transform parent, Vector3 localPosition, string name)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        Vector3[] offsets =
        {
            new Vector3(-0.6f, 0f, -0.2f),
            new Vector3(0.15f, 0.1f, 0.25f),
            new Vector3(0.55f, -0.05f, -0.35f)
        };

        Vector3[] scales =
        {
            new Vector3(1.1f, 1.0f, 0.9f),
            new Vector3(0.9f, 1.2f, 1.1f),
            new Vector3(0.8f, 0.9f, 0.75f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            rock.transform.SetParent(clusterRoot, false);
            rock.transform.localPosition = offsets[i];
            rock.transform.localRotation = Quaternion.Euler(0f, i * 26f, 0f);
            rock.transform.localScale = scales[i];
            ApplyColor(rock, new Color(0.34f, 0.36f, 0.38f, 1f));
        }
    }

    private static void CreateBoatCover(Transform parent, Vector3 localPosition, string name)
    {
        GameObject boat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boat.name = name;
        boat.transform.SetParent(parent, false);
        boat.transform.localPosition = localPosition;
        boat.transform.localRotation = Quaternion.Euler(0f, 18f, 9f);
        boat.transform.localScale = new Vector3(2.8f, 0.7f, 1.2f);
        ApplyColor(boat, new Color(0.46f, 0.32f, 0.2f, 1f));
    }

    private static void CreateFishingRack(Transform parent, Vector3 localPosition, string name)
    {
        Transform rackRoot = new GameObject(name).transform;
        rackRoot.SetParent(parent, false);
        rackRoot.localPosition = localPosition;

        GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPost.name = "Post_Left";
        leftPost.transform.SetParent(rackRoot, false);
        leftPost.transform.localPosition = new Vector3(-0.7f, 1f, 0f);
        leftPost.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
        ApplyColor(leftPost, new Color(0.55f, 0.39f, 0.24f, 1f));

        GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPost.name = "Post_Right";
        rightPost.transform.SetParent(rackRoot, false);
        rightPost.transform.localPosition = new Vector3(0.7f, 1f, 0f);
        rightPost.transform.localScale = new Vector3(0.12f, 2f, 0.12f);
        ApplyColor(rightPost, new Color(0.55f, 0.39f, 0.24f, 1f));

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Beam";
        beam.transform.SetParent(rackRoot, false);
        beam.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        beam.transform.localScale = new Vector3(1.6f, 0.12f, 0.12f);
        ApplyColor(beam, new Color(0.55f, 0.39f, 0.24f, 1f));
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
            crate.transform.localPosition = new Vector3((i % 2) * 0.7f, (i / 2) * 0.6f, (i == 2 ? 0.4f : 0f));
            crate.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
            ApplyColor(crate, new Color(0.52f, 0.39f, 0.24f, 1f));
        }
    }

    private static void CreateNetPost(Transform parent, Vector3 localPosition, string name)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = name;
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPosition;
        post.transform.localScale = new Vector3(0.14f, 2.4f, 0.14f);
        ApplyColor(post, new Color(0.55f, 0.39f, 0.24f, 1f));

        GameObject net = GameObject.CreatePrimitive(PrimitiveType.Cube);
        net.name = $"{name}_Net";
        net.transform.SetParent(parent, false);
        net.transform.localPosition = localPosition + new Vector3(0.45f, 1.2f, 0f);
        net.transform.localScale = new Vector3(0.9f, 1.6f, 0.08f);
        ApplyColor(net, new Color(0.66f, 0.74f, 0.78f, 0.85f));
    }

    private static void CreateLabBench(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bench.name = name;
        bench.transform.SetParent(parent, false);
        bench.transform.localPosition = localPosition;
        bench.transform.localScale = scale;
        ApplyColor(bench, new Color(0.72f, 0.74f, 0.78f, 1f));
    }

    private static void CreateStorageRack(Transform parent, Vector3 localPosition, string name)
    {
        GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = name;
        rack.transform.SetParent(parent, false);
        rack.transform.localPosition = localPosition;
        rack.transform.localScale = new Vector3(1.2f, 2.4f, 0.5f);
        ApplyColor(rack, new Color(0.56f, 0.57f, 0.6f, 1f));
    }

    private static void CreateTank(Transform parent, Vector3 localPosition, string name)
    {
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tank.name = name;
        tank.transform.SetParent(parent, false);
        tank.transform.localPosition = localPosition;
        tank.transform.localScale = new Vector3(0.55f, 1.35f, 0.55f);
        ApplyColor(tank, new Color(0.52f, 0.84f, 1f, 0.92f));
    }

    private static void CreateConsole(Transform parent, Vector3 localPosition, string name)
    {
        GameObject console = GameObject.CreatePrimitive(PrimitiveType.Cube);
        console.name = name;
        console.transform.SetParent(parent, false);
        console.transform.localPosition = localPosition;
        console.transform.localScale = new Vector3(1.2f, 1.0f, 0.8f);
        ApplyColor(console, new Color(0.42f, 0.46f, 0.52f, 1f));
    }

    private static Vector3 GridToIslandWorld(float gridX, float gridY, float y)
    {
        return new Vector3((gridX - 9f) * IslandCellSize, y, (gridY - 6f) * IslandCellSize);
    }

    private static void CreateRailing(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject railing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        railing.name = name;
        railing.transform.SetParent(parent, false);
        railing.transform.localPosition = localPosition;
        railing.transform.localScale = scale;
        ApplyColor(railing, new Color(0.56f, 0.56f, 0.58f, 1f));
        railing.AddComponent<PerspectiveFadeWall>();
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

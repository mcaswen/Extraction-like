using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在局内白盒场景中生成并散布魔法知识拾取物的编辑器工具
/// </summary>
public static class RaidMagicKnowledgeScatterTool
{
    private const string DefaultProfilePath = "Assets/Settings/MagicKnowledge/SO_RaidMagicKnowledgeScatterProfile.asset";
    private const string PrefabFolderPath = "Assets/Prefabs/Raid/MagicKnowledge";
    private const string MaterialFolderPath = "Assets/Art/Materials/Raid/MagicKnowledge";

    private sealed class PlacedPickupInfo
    {
        public Vector3 Position;
        public float MinSpacing;
    }

    private readonly struct DefaultKnowledgeDefinition
    {
        public DefaultKnowledgeDefinition(
            string entryId,
            string label,
            string prefabName,
            MagicUnlockType unlockType,
            int runePoints,
            int defaultCount,
            float defaultMinSpacing,
            RaidRegionPurposeMask defaultPurposes,
            Color color,
            KnowledgeVisualStyle visualStyle)
        {
            EntryId = entryId;
            Label = label;
            PrefabName = prefabName;
            UnlockType = unlockType;
            RunePoints = runePoints;
            DefaultCount = defaultCount;
            DefaultMinSpacing = defaultMinSpacing;
            DefaultPurposes = defaultPurposes;
            Color = color;
            VisualStyle = visualStyle;
        }

        public string EntryId { get; }
        public string Label { get; }
        public string PrefabName { get; }
        public MagicUnlockType UnlockType { get; }
        public int RunePoints { get; }
        public int DefaultCount { get; }
        public float DefaultMinSpacing { get; }
        public RaidRegionPurposeMask DefaultPurposes { get; }
        public Color Color { get; }
        public KnowledgeVisualStyle VisualStyle { get; }
    }

    private enum KnowledgeVisualStyle
    {
        Book,
        Tablet,
        Boots,
        Hourglass
    }

    private static readonly DefaultKnowledgeDefinition[] DefaultDefinitions =
    {
        new DefaultKnowledgeDefinition(
            "ice_freeze_book",
            "Ice Freeze Notes",
            "Pfb_MagicKnowledge_IceFreeze",
            MagicUnlockType.IceFreeze,
            1,
            2,
            6f,
            RaidRegionPurposeMask.Resource | RaidRegionPurposeMask.DenseResource,
            new Color(0.56f, 0.82f, 1f, 1f),
            KnowledgeVisualStyle.Book),
        new DefaultKnowledgeDefinition(
            "ice_cone_book",
            "Ice Cone Grimoire",
            "Pfb_MagicKnowledge_IceCone",
            MagicUnlockType.IceCone,
            1,
            2,
            6f,
            RaidRegionPurposeMask.Resource | RaidRegionPurposeMask.DenseResource,
            new Color(0.42f, 0.72f, 1f, 1f),
            KnowledgeVisualStyle.Book),
        new DefaultKnowledgeDefinition(
            "earth_wall_book",
            "Earth Wall Manual",
            "Pfb_MagicKnowledge_EarthWall",
            MagicUnlockType.EarthWall,
            1,
            2,
            7f,
            RaidRegionPurposeMask.Resource | RaidRegionPurposeMask.Boss,
            new Color(0.62f, 0.44f, 0.3f, 1f),
            KnowledgeVisualStyle.Tablet),
        new DefaultKnowledgeDefinition(
            "rune_pattern_tablet",
            "Rune Pattern Tablet",
            "Pfb_MagicKnowledge_RunePattern",
            MagicUnlockType.RunePattern,
            1,
            3,
            5f,
            RaidRegionPurposeMask.Resource | RaidRegionPurposeMask.DenseResource | RaidRegionPurposeMask.Boss,
            new Color(0.9f, 0.86f, 0.58f, 1f),
            KnowledgeVisualStyle.Tablet),
        new DefaultKnowledgeDefinition(
            "traveler_boots_relic",
            "Traveler Boots",
            "Pfb_MagicKnowledge_TravelerBoots",
            MagicUnlockType.TravelerBoots,
            1,
            1,
            9f,
            RaidRegionPurposeMask.DenseResource | RaidRegionPurposeMask.Boss,
            new Color(0.9f, 0.7f, 0.3f, 1f),
            KnowledgeVisualStyle.Boots),
        new DefaultKnowledgeDefinition(
            "time_hourglass_relic",
            "Time Hourglass",
            "Pfb_MagicKnowledge_TimeHourglass",
            MagicUnlockType.TimeHourglass,
            1,
            1,
            10f,
            RaidRegionPurposeMask.Boss,
            new Color(0.26f, 0.96f, 0.7f, 1f),
            KnowledgeVisualStyle.Hourglass),
        new DefaultKnowledgeDefinition(
            "space_hourglass_relic",
            "Space Hourglass",
            "Pfb_MagicKnowledge_SpaceHourglass",
            MagicUnlockType.SpaceHourglass,
            1,
            1,
            10f,
            RaidRegionPurposeMask.Boss | RaidRegionPurposeMask.DenseResource,
            new Color(0.88f, 0.62f, 1f, 1f),
            KnowledgeVisualStyle.Hourglass)
    };

    [MenuItem("Tools/Whitebox/Magic Knowledge/Generate Default Prefabs")]
    private static void GenerateDefaultPrefabsMenu()
    {
        RaidMagicKnowledgeScatterProfile profile = LoadOrCreateProfile();
        EnsureDefaultPrefabsAndProfile(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = profile;
        EditorUtility.DisplayDialog(
            "Magic Knowledge",
            $"Default prefabs are ready at:\n{PrefabFolderPath}\n\nProfile:\n{DefaultProfilePath}",
            "OK");
    }

    [MenuItem("Tools/Whitebox/Magic Knowledge/Scatter To Scene_lyl_IslandWhitebox")]
    private static void ScatterToSceneMenu()
    {
        RaidMagicKnowledgeScatterProfile profile = LoadOrCreateProfile();
        EnsureDefaultPrefabsAndProfile(profile);
        ScatterToScene(profile);
    }

    [MenuItem("Tools/Whitebox/Magic Knowledge/Generate Default Prefabs + Scatter")]
    private static void GenerateAndScatterMenu()
    {
        GenerateAndScatterForSceneLylIslandWhitebox();
    }

    [MenuItem("Tools/Whitebox/Magic Knowledge/Clear Generated Pickups In Scene_lyl_IslandWhitebox")]
    private static void ClearGeneratedPickupsMenu()
    {
        RaidMagicKnowledgeScatterProfile profile = LoadOrCreateProfile();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(profile.TargetScenePath, OpenSceneMode.Single);
        GameObject generatedRoot = GameObject.Find(profile.GeneratedRootName);
        if (generatedRoot != null)
        {
            Undo.DestroyObjectImmediate(generatedRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorUtility.DisplayDialog("Magic Knowledge", "Generated pickups have been cleared.", "OK");
    }

    [MenuItem("Tools/Whitebox/Magic Knowledge/Select Scatter Profile Asset")]
    private static void SelectProfileAssetMenu()
    {
        RaidMagicKnowledgeScatterProfile profile = LoadOrCreateProfile();
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }

    /// <summary>
    /// 编辑器批处理模式入口
    /// 可通过执行方法参数调用魔法知识散布流程
    /// </summary>
    public static void GenerateAndScatterForSceneLylIslandWhitebox()
    {
        RaidMagicKnowledgeScatterProfile profile = LoadOrCreateProfile();
        EnsureDefaultPrefabsAndProfile(profile);
        ScatterToScene(profile);
    }

    private static RaidMagicKnowledgeScatterProfile LoadOrCreateProfile()
    {
        EnsureDirectory("Assets/Settings");
        EnsureDirectory("Assets/Settings/MagicKnowledge");

        RaidMagicKnowledgeScatterProfile profile =
            AssetDatabase.LoadAssetAtPath<RaidMagicKnowledgeScatterProfile>(DefaultProfilePath);
        if (profile != null)
        {
            return profile;
        }

        profile = ScriptableObject.CreateInstance<RaidMagicKnowledgeScatterProfile>();
        profile.TargetScenePath = "Assets/Scenes/Scene_lyl_IslandWhitebox.unity";
        profile.GeneratedRootName = "Generated_MagicKnowledgePickups";
        profile.ClearPreviousGeneratedRoot = true;
        profile.UseFixedSeed = true;
        profile.FixedSeed = 20260409;
        profile.GroundMask = ~0;
        profile.GroundProbeHeight = 40f;
        profile.SpawnHeightOffset = 0.05f;
        profile.RegionEdgePadding = 1f;
        profile.GlobalMinSpacing = 4f;
        profile.PlacementAttemptsPerItem = 64;

        AssetDatabase.CreateAsset(profile, DefaultProfilePath);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void EnsureDefaultPrefabsAndProfile(RaidMagicKnowledgeScatterProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        EnsureDirectory("Assets/Prefabs");
        EnsureDirectory("Assets/Prefabs/Raid");
        EnsureDirectory(PrefabFolderPath);
        EnsureDirectory("Assets/Art");
        EnsureDirectory("Assets/Art/Materials");
        EnsureDirectory("Assets/Art/Materials/Raid");
        EnsureDirectory(MaterialFolderPath);

        if (profile.SpawnEntries == null)
        {
            profile.SpawnEntries = new List<RaidMagicKnowledgeSpawnEntry>();
        }

        for (int i = 0; i < DefaultDefinitions.Length; i++)
        {
            DefaultKnowledgeDefinition definition = DefaultDefinitions[i];
            string prefabPath = $"{PrefabFolderPath}/{definition.PrefabName}.prefab";
            string materialPath = $"{MaterialFolderPath}/M_{definition.PrefabName}.mat";
            GameObject prefab = EnsureKnowledgePrefab(prefabPath, materialPath, definition);

            RaidMagicKnowledgeSpawnEntry spawnEntry = FindSpawnEntryById(profile.SpawnEntries, definition.EntryId);
            if (spawnEntry == null)
            {
                spawnEntry = new RaidMagicKnowledgeSpawnEntry
                {
                    EntryId = definition.EntryId,
                    Label = definition.Label,
                    Count = definition.DefaultCount,
                    MinSpacing = definition.DefaultMinSpacing,
                    AllowedRegionPurposes = definition.DefaultPurposes
                };
                profile.SpawnEntries.Add(spawnEntry);
            }

            if (string.IsNullOrWhiteSpace(spawnEntry.Label))
            {
                spawnEntry.Label = definition.Label;
            }

            if (spawnEntry.Prefab == null)
            {
                spawnEntry.Prefab = prefab;
            }
        }

        EditorUtility.SetDirty(profile);
    }

    private static void ScatterToScene(RaidMagicKnowledgeScatterProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.TargetScenePath) || !File.Exists(profile.TargetScenePath))
        {
            EditorUtility.DisplayDialog(
                "Magic Knowledge",
                $"Target scene does not exist:\n{profile.TargetScenePath}",
                "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(profile.TargetScenePath, OpenSceneMode.Single);
        SceneLylSupportMigrator.EnsureCoreGameplaySupport(scene);

        GameObject generatedRoot = PrepareGeneratedRoot(profile);
        RaidRegionMarker[] markers = UnityEngine.Object.FindObjectsOfType<RaidRegionMarker>(true);
        if (markers == null || markers.Length == 0)
        {
            EditorUtility.DisplayDialog("Magic Knowledge", "No RaidRegionMarker found in this scene.", "OK");
            return;
        }

        System.Random random = profile.UseFixedSeed
            ? new System.Random(profile.FixedSeed)
            : new System.Random(Environment.TickCount);

        List<PlacedPickupInfo> placedPickups = new List<PlacedPickupInfo>();
        int totalSpawned = 0;

        for (int i = 0; i < profile.SpawnEntries.Count; i++)
        {
            RaidMagicKnowledgeSpawnEntry entry = profile.SpawnEntries[i];
            if (entry == null || entry.Prefab == null || entry.Count <= 0)
            {
                continue;
            }

            float entryMinSpacing = Mathf.Max(profile.GlobalMinSpacing, entry.MinSpacing);
            List<RaidRegionMarker> eligibleMarkers = FilterEligibleMarkers(markers, entry.AllowedRegionPurposes);
            if (eligibleMarkers.Count <= 0)
            {
                Debug.LogWarning($"[MagicKnowledgeScatter] Entry '{entry.Label}' has no eligible markers.");
                continue;
            }

            for (int spawnIndex = 0; spawnIndex < entry.Count; spawnIndex++)
            {
                if (!TryFindPlacementPosition(profile, eligibleMarkers, placedPickups, entryMinSpacing, random, out Vector3 position))
                {
                    Debug.LogWarning($"[MagicKnowledgeScatter] Failed to place '{entry.Label}' at index {spawnIndex + 1}.");
                    continue;
                }

                GameObject pickupInstance = PrefabUtility.InstantiatePrefab(entry.Prefab, generatedRoot.transform) as GameObject;
                if (pickupInstance == null)
                {
                    pickupInstance = UnityEngine.Object.Instantiate(entry.Prefab, generatedRoot.transform);
                }

                pickupInstance.name = $"{entry.Label}_{spawnIndex + 1:00}";
                pickupInstance.transform.position = position;
                pickupInstance.transform.rotation = Quaternion.Euler(0f, (float)(random.NextDouble() * 360d), 0f);

                placedPickups.Add(new PlacedPickupInfo
                {
                    Position = position,
                    MinSpacing = entryMinSpacing
                });
                totalSpawned++;
            }
        }

        Selection.activeGameObject = generatedRoot;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "Magic Knowledge",
            $"Scatter complete. Spawned {totalSpawned} pickups in:\n{scene.path}",
            "OK");
    }

    private static GameObject PrepareGeneratedRoot(RaidMagicKnowledgeScatterProfile profile)
    {
        GameObject existingRoot = GameObject.Find(profile.GeneratedRootName);
        if (existingRoot != null && profile.ClearPreviousGeneratedRoot)
        {
            Undo.DestroyObjectImmediate(existingRoot);
            existingRoot = null;
        }

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(profile.GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Magic Knowledge Generated Root");
        return root;
    }

    private static List<RaidRegionMarker> FilterEligibleMarkers(
        RaidRegionMarker[] allMarkers,
        RaidRegionPurposeMask allowedMask)
    {
        List<RaidRegionMarker> eligibleMarkers = new List<RaidRegionMarker>();
        if (allMarkers == null || allMarkers.Length == 0)
        {
            return eligibleMarkers;
        }

        for (int i = 0; i < allMarkers.Length; i++)
        {
            RaidRegionMarker marker = allMarkers[i];
            if (marker == null)
            {
                continue;
            }

            RaidRegionPurposeMask markerMask = ToMask(marker.Purpose);
            if ((allowedMask & markerMask) != 0)
            {
                eligibleMarkers.Add(marker);
            }
        }

        return eligibleMarkers;
    }

    private static bool TryFindPlacementPosition(
        RaidMagicKnowledgeScatterProfile profile,
        List<RaidRegionMarker> markers,
        List<PlacedPickupInfo> placedPickups,
        float minSpacing,
        System.Random random,
        out Vector3 position)
    {
        position = Vector3.zero;
        if (markers == null || markers.Count == 0)
        {
            return false;
        }

        int attempts = Mathf.Max(8, profile.PlacementAttemptsPerItem);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            RaidRegionMarker marker = markers[random.Next(0, markers.Count)];
            if (marker == null)
            {
                continue;
            }

            Bounds bounds = marker.GetWorldBounds();
            float edgePadding = Mathf.Max(profile.RegionEdgePadding, 0f);
            float minX = bounds.min.x + edgePadding;
            float maxX = bounds.max.x - edgePadding;
            float minZ = bounds.min.z + edgePadding;
            float maxZ = bounds.max.z - edgePadding;
            if (minX >= maxX || minZ >= maxZ)
            {
                continue;
            }

            float x = Mathf.Lerp(minX, maxX, (float)random.NextDouble());
            float z = Mathf.Lerp(minZ, maxZ, (float)random.NextDouble());
            Vector3 probeStart = new Vector3(x, bounds.max.y + Mathf.Max(1f, profile.GroundProbeHeight), z);

            Vector3 candidatePosition;
            if (Physics.Raycast(
                    probeStart,
                    Vector3.down,
                    out RaycastHit hitInfo,
                    profile.GroundProbeHeight * 2f,
                    profile.GroundMask))
            {
                candidatePosition = hitInfo.point + Vector3.up * profile.SpawnHeightOffset;
            }
            else
            {
                candidatePosition = new Vector3(x, bounds.min.y + profile.SpawnHeightOffset, z);
            }

            if (!IsFarEnough(candidatePosition, minSpacing, placedPickups))
            {
                continue;
            }

            position = candidatePosition;
            return true;
        }

        return false;
    }

    private static bool IsFarEnough(Vector3 candidatePosition, float minSpacing, List<PlacedPickupInfo> placedPickups)
    {
        if (placedPickups == null || placedPickups.Count <= 0)
        {
            return true;
        }

        for (int i = 0; i < placedPickups.Count; i++)
        {
            PlacedPickupInfo placedInfo = placedPickups[i];
            float requiredDistance = Mathf.Max(minSpacing, placedInfo.MinSpacing);
            if (Vector3.Distance(candidatePosition, placedInfo.Position) < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static RaidRegionPurposeMask ToMask(RaidRegionPurpose purpose)
    {
        switch (purpose)
        {
            case RaidRegionPurpose.Spawn:
                return RaidRegionPurposeMask.Spawn;
            case RaidRegionPurpose.Resource:
                return RaidRegionPurposeMask.Resource;
            case RaidRegionPurpose.DenseResource:
                return RaidRegionPurposeMask.DenseResource;
            case RaidRegionPurpose.Boss:
                return RaidRegionPurposeMask.Boss;
            case RaidRegionPurpose.Extraction:
                return RaidRegionPurposeMask.Extraction;
            case RaidRegionPurpose.Transit:
                return RaidRegionPurposeMask.Transit;
            default:
                return RaidRegionPurposeMask.None;
        }
    }

    private static RaidMagicKnowledgeSpawnEntry FindSpawnEntryById(
        List<RaidMagicKnowledgeSpawnEntry> entries,
        string entryId)
    {
        if (entries == null || string.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            RaidMagicKnowledgeSpawnEntry entry = entries[i];
            if (entry != null && string.Equals(entry.EntryId, entryId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static GameObject EnsureKnowledgePrefab(
        string prefabPath,
        string materialPath,
        DefaultKnowledgeDefinition definition)
    {
        Material material = EnsureMaterial(materialPath, definition.Color);
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject prefabRoot = existingPrefab != null ? PrefabUtility.LoadPrefabContents(prefabPath) : null;
        bool createdNewPrefab = false;

        if (prefabRoot == null)
        {
            createdNewPrefab = true;
            prefabRoot = new GameObject(definition.PrefabName);
        }

        prefabRoot.name = definition.PrefabName;
        prefabRoot.transform.localPosition = Vector3.zero;
        prefabRoot.transform.localRotation = Quaternion.identity;
        prefabRoot.transform.localScale = Vector3.one;
        StripRootVisualComponents(prefabRoot);
        ClearChildren(prefabRoot.transform);

        BuildVisual(prefabRoot.transform, material, definition.VisualStyle);
        EnsureRootCollider(prefabRoot, definition.VisualStyle);
        EnsurePickupComponent(prefabRoot, definition);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        if (createdNewPrefab)
        {
            UnityEngine.Object.DestroyImmediate(prefabRoot);
        }
        else
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        return savedPrefab;
    }

    private static void BuildVisual(Transform root, Material material, KnowledgeVisualStyle visualStyle)
    {
        switch (visualStyle)
        {
            case KnowledgeVisualStyle.Book:
                CreatePrimitiveVisual(
                    root,
                    "Book",
                    PrimitiveType.Cube,
                    new Vector3(0f, 0.08f, 0f),
                    new Vector3(0.85f, 0.16f, 1.15f),
                    material);
                CreatePrimitiveVisual(
                    root,
                    "BookSpine",
                    PrimitiveType.Cube,
                    new Vector3(-0.28f, 0.095f, 0f),
                    new Vector3(0.08f, 0.18f, 1.17f),
                    material);
                break;
            case KnowledgeVisualStyle.Tablet:
                CreatePrimitiveVisual(
                    root,
                    "Tablet",
                    PrimitiveType.Cube,
                    new Vector3(0f, 0.2f, 0f),
                    new Vector3(0.9f, 0.4f, 0.7f),
                    material);
                CreatePrimitiveVisual(
                    root,
                    "TabletCore",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.45f, 0f),
                    new Vector3(0.14f, 0.08f, 0.14f),
                    material);
                break;
            case KnowledgeVisualStyle.Boots:
                CreatePrimitiveVisual(
                    root,
                    "BootLeft",
                    PrimitiveType.Capsule,
                    new Vector3(-0.22f, 0.27f, 0f),
                    new Vector3(0.32f, 0.28f, 0.55f),
                    material);
                CreatePrimitiveVisual(
                    root,
                    "BootRight",
                    PrimitiveType.Capsule,
                    new Vector3(0.22f, 0.27f, 0f),
                    new Vector3(0.32f, 0.28f, 0.55f),
                    material);
                break;
            case KnowledgeVisualStyle.Hourglass:
                CreatePrimitiveVisual(
                    root,
                    "HourglassTop",
                    PrimitiveType.Sphere,
                    new Vector3(0f, 0.62f, 0f),
                    new Vector3(0.48f, 0.36f, 0.48f),
                    material);
                CreatePrimitiveVisual(
                    root,
                    "HourglassBottom",
                    PrimitiveType.Sphere,
                    new Vector3(0f, 0.24f, 0f),
                    new Vector3(0.48f, 0.36f, 0.48f),
                    material);
                CreatePrimitiveVisual(
                    root,
                    "HourglassFrame",
                    PrimitiveType.Cylinder,
                    new Vector3(0f, 0.43f, 0f),
                    new Vector3(0.16f, 0.36f, 0.16f),
                    material);
                break;
        }
    }

    private static void EnsureRootCollider(GameObject rootObject, KnowledgeVisualStyle visualStyle)
    {
        Collider[] existingColliders = rootObject.GetComponents<Collider>();
        for (int i = 0; i < existingColliders.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(existingColliders[i]);
        }

        BoxCollider triggerCollider = rootObject.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        switch (visualStyle)
        {
            case KnowledgeVisualStyle.Book:
                triggerCollider.center = new Vector3(0f, 0.1f, 0f);
                triggerCollider.size = new Vector3(1.05f, 0.3f, 1.35f);
                break;
            case KnowledgeVisualStyle.Tablet:
                triggerCollider.center = new Vector3(0f, 0.28f, 0f);
                triggerCollider.size = new Vector3(1.05f, 0.7f, 0.85f);
                break;
            case KnowledgeVisualStyle.Boots:
                triggerCollider.center = new Vector3(0f, 0.3f, 0f);
                triggerCollider.size = new Vector3(1f, 0.72f, 0.85f);
                break;
            case KnowledgeVisualStyle.Hourglass:
                triggerCollider.center = new Vector3(0f, 0.45f, 0f);
                triggerCollider.size = new Vector3(0.85f, 1.1f, 0.85f);
                break;
        }
    }

    private static void EnsurePickupComponent(GameObject rootObject, DefaultKnowledgeDefinition definition)
    {
        MagicKnowledgePickup[] pickupComponents = rootObject.GetComponents<MagicKnowledgePickup>();
        MagicKnowledgePickup pickupComponent = pickupComponents.Length > 0 ? pickupComponents[0] : null;
        for (int i = 1; i < pickupComponents.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(pickupComponents[i]);
        }

        if (pickupComponent == null)
        {
            pickupComponent = rootObject.AddComponent<MagicKnowledgePickup>();
        }

        pickupComponent.PickupName = definition.Label;
        pickupComponent.Description = $"Study to unlock {definition.Label}.";
        pickupComponent.UnlockType = definition.UnlockType;
        pickupComponent.RunePatternPoints = Mathf.Max(1, definition.RunePoints);
        pickupComponent.ConsumeOnUnlock = true;
        pickupComponent.EnableHighlightPulse = true;
        pickupComponent.HighlightColor = definition.Color;
        pickupComponent.HighlightStrength = 0.35f;
        pickupComponent.HighlightPulseSpeed = 4f;
    }

    private static void CreatePrimitiveVisual(
        Transform parent,
        string name,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Material sharedMaterial)
    {
        GameObject visualObject = GameObject.CreatePrimitive(primitiveType);
        visualObject.name = name;
        visualObject.transform.SetParent(parent, false);
        visualObject.transform.localPosition = localPosition;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = localScale;

        Collider colliderComponent = visualObject.GetComponent<Collider>();
        if (colliderComponent != null)
        {
            UnityEngine.Object.DestroyImmediate(colliderComponent);
        }

        Renderer rendererComponent = visualObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.sharedMaterial = sharedMaterial;
        }
    }

    private static Material EnsureMaterial(string materialPath, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(materialPath)
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static void StripRootVisualComponents(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return;
        }

        MeshRenderer meshRenderer = rootObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            UnityEngine.Object.DestroyImmediate(meshRenderer);
        }

        MeshFilter meshFilter = rootObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            UnityEngine.Object.DestroyImmediate(meshFilter);
        }

        Rigidbody rigidbody = rootObject.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            UnityEngine.Object.DestroyImmediate(rigidbody);
        }
    }

    private static void EnsureDirectory(string assetRelativePath)
    {
        if (AssetDatabase.IsValidFolder(assetRelativePath))
        {
            return;
        }

        string normalized = assetRelativePath.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        if (parts.Length <= 1)
        {
            return;
        }

        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}

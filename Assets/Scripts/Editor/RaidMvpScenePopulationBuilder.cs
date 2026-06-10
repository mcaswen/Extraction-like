using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RaidMvpScenePopulationBuilder
{
    [MenuItem("Tools/Whitebox/Create Raid Region Marker From Selection")]
    private static void CreateRaidRegionMarkerFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds bounds))
        {
            EditorUtility.DisplayDialog(
                "Create Raid Region Marker",
                "Please select floor or wall objects inside a map area first.",
                "OK");
            return;
        }

        string selectionName = Selection.activeGameObject != null ? Selection.activeGameObject.name : "Region";
        GameObject markerObject = new GameObject($"RaidRegion_{selectionName}");
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Raid Region Marker");
        markerObject.transform.position = bounds.center;

        RaidRegionMarker marker = markerObject.AddComponent<RaidRegionMarker>();
        marker.RegionId = selectionName;
        marker.RegionSize = new Vector3(
            Mathf.Max(6f, bounds.size.x),
            Mathf.Max(4f, bounds.size.y + 2f),
            Mathf.Max(6f, bounds.size.z));

        ApplyNameHeuristics(marker, selectionName);

        Selection.activeGameObject = markerObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Whitebox/Populate MVP Scene From Raid Markers")]
    private static void PopulateSceneFromRaidMarkers()
    {
        RaidMvpPopulationProfile profile = ResolvePopulationProfile();
        if (profile == null)
        {
            EditorUtility.DisplayDialog(
                "Populate MVP Scene",
                "Please create or select a RaidMvpPopulationProfile asset first.",
                "OK");
            return;
        }

        RaidRegionMarker[] markers = UnityEngine.Object.FindObjectsOfType<RaidRegionMarker>(true);
        if (markers.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Populate MVP Scene",
                "No RaidRegionMarker was found in the current scene.",
                "OK");
            return;
        }

        SceneLylSupportMigrator.EnsureCoreGameplaySupport(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EnsureRaidFlowControllerExists();
        EnsureRuntimeNavMeshBuilderExists();

        GameObject root = PrepareGeneratedRoot(profile);
        Transform playersRoot = CreateChildRoot(root.transform, "Player");
        Transform startingItemsRoot = CreateChildRoot(root.transform, "StartingItems");
        Transform enemiesRoot = CreateChildRoot(root.transform, "Enemies");
        Transform chestsRoot = CreateChildRoot(root.transform, "Chests");
        Transform extractionRoot = CreateChildRoot(root.transform, "Extraction");

        System.Random random = profile.UseFixedSeed
            ? new System.Random(profile.FixedSeed)
            : new System.Random(Environment.TickCount);

        bool playerSpawned = false;

        Array.Sort(markers, CompareMarkers);
        for (int i = 0; i < markers.Length; i++)
        {
            RaidRegionMarker marker = markers[i];
            if (marker == null)
            {
                continue;
            }

            Bounds bounds = marker.GetWorldBounds();
            if (!playerSpawned && marker.Purpose == RaidRegionPurpose.Spawn)
            {
                if (TrySpawnPlayer(profile, marker, playersRoot, random, out Vector3 spawnPosition))
                {
                    playerSpawned = true;
                    TrySpawnStartingGearDrops(profile, marker, spawnPosition, startingItemsRoot);
                }
            }

            if (marker.Purpose == RaidRegionPurpose.Extraction)
            {
                EnsureExtractionPoint(profile, marker, extractionRoot);
            }

            RaidDensitySpawnRule densityRule = profile.GetDensityRule(marker.Density);
            RaidRegionPrefabPool pool = profile.GetRegionPool(marker.Purpose);

            Vector2Int enemyRange = marker.OverrideSpawnCounts
                ? marker.EnemyCountRangeOverride
                : (densityRule != null ? densityRule.EnemyCountRange : Vector2Int.zero);

            Vector2Int chestRange = marker.OverrideSpawnCounts
                ? marker.ChestCountRangeOverride
                : (densityRule != null ? densityRule.ChestCountRange : Vector2Int.zero);

            if (pool != null)
            {
                Transform regionEnemyRoot = CreateChildRoot(enemiesRoot, $"{marker.RegionId}_Enemies");
                SpawnEntriesInBounds(profile, pool, pool.EnemyPrefabs, enemyRange, bounds, marker.EnemyEdgePadding, profile.DefaultEnemySpacing, regionEnemyRoot, random);

                Transform regionChestRoot = CreateChildRoot(chestsRoot, $"{marker.RegionId}_Chests");
                SpawnEntriesInBounds(profile, pool, pool.ChestPrefabs, chestRange, bounds, marker.ChestEdgePadding, profile.DefaultChestSpacing, regionChestRoot, random);
            }
        }

        if (!playerSpawned && profile.PlayerPrefab != null)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null || markers[i].Purpose != RaidRegionPurpose.Resource)
                {
                    continue;
                }

                if (TrySpawnPlayer(profile, markers[i], playersRoot, random, out Vector3 spawnPosition))
                {
                    playerSpawned = true;
                    TrySpawnStartingGearDrops(profile, markers[i], spawnPosition, startingItemsRoot);
                    break;
                }
            }
        }

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Tools/Whitebox/Clear Generated MVP Scene Population")]
    private static void ClearGeneratedPopulation()
    {
        RaidMvpPopulationProfile profile = ResolvePopulationProfile();
        if (profile == null)
        {
            EditorUtility.DisplayDialog(
                "Clear MVP Scene Population",
                "Please create or select a RaidMvpPopulationProfile asset first.",
                "OK");
            return;
        }

        GameObject existingRoot = GameObject.Find(profile.GeneratedRootName);
        if (existingRoot != null)
        {
            if (Selection.activeGameObject == existingRoot || IsSelectionInside(existingRoot))
            {
                Selection.activeObject = null;
            }

            Undo.DestroyObjectImmediate(existingRoot);
        }
    }

    private static int CompareMarkers(RaidRegionMarker a, RaidRegionMarker b)
    {
        int aPriority = GetMarkerPriority(a != null ? a.Purpose : RaidRegionPurpose.Transit);
        int bPriority = GetMarkerPriority(b != null ? b.Purpose : RaidRegionPurpose.Transit);
        return aPriority.CompareTo(bPriority);
    }

    private static int GetMarkerPriority(RaidRegionPurpose purpose)
    {
        return purpose switch
        {
            RaidRegionPurpose.Spawn => 0,
            RaidRegionPurpose.Extraction => 1,
            RaidRegionPurpose.Resource => 2,
            RaidRegionPurpose.DenseResource => 3,
            RaidRegionPurpose.Boss => 4,
            _ => 5
        };
    }

    private static RaidMvpPopulationProfile ResolvePopulationProfile()
    {
        if (Selection.activeObject is RaidMvpPopulationProfile selectedProfile)
        {
            return selectedProfile;
        }

        string[] profileGuids = AssetDatabase.FindAssets("t:RaidMvpPopulationProfile");
        if (profileGuids.Length == 0)
        {
            return null;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(profileGuids[0]);
        return AssetDatabase.LoadAssetAtPath<RaidMvpPopulationProfile>(assetPath);
    }

    private static GameObject PrepareGeneratedRoot(RaidMvpPopulationProfile profile)
    {
        GameObject existingRoot = GameObject.Find(profile.GeneratedRootName);
        if (existingRoot != null && profile.ClearPreviousGeneratedRoot)
        {
            if (Selection.activeGameObject == existingRoot || IsSelectionInside(existingRoot))
            {
                Selection.activeObject = null;
            }

            Undo.DestroyObjectImmediate(existingRoot);
            existingRoot = null;
        }

        if (existingRoot != null)
        {
            return existingRoot;
        }

        GameObject root = new GameObject(profile.GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Raid MVP Generated Root");
        return root;
    }

    private static Transform CreateChildRoot(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static bool TrySpawnPlayer(RaidMvpPopulationProfile profile, RaidRegionMarker marker, Transform playersRoot, System.Random random, out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;
        if (profile.PlayerPrefab == null || marker == null)
        {
            return false;
        }

        if (!TryFindPlacementPosition(profile, marker.GetWorldBounds(), 1.25f, profile.SpawnHeightOffset, random, new List<Vector3>(), out spawnPosition))
        {
            spawnPosition = marker.GetWorldBounds().center + Vector3.up * profile.SpawnHeightOffset;
        }

        GameObject playerObject = InstantiatePrefab(profile.PlayerPrefab, playersRoot);
        playerObject.name = $"Player_{marker.RegionId}";
        playerObject.transform.position = spawnPosition;
        playerObject.transform.rotation = Quaternion.identity;

        if (playerObject.tag != "Player")
        {
            playerObject.tag = "Player";
        }

        return true;
    }

    private static void TrySpawnStartingGearDrops(RaidMvpPopulationProfile profile, RaidRegionMarker marker, Vector3 spawnPosition, Transform parent)
    {
        if (profile == null || !profile.SpawnStartingGearDrops || marker == null || parent == null)
        {
            return;
        }

        GameObject bagPrefab = profile.StartingBagWorldPrefab != null
            ? profile.StartingBagWorldPrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemPrefabIn3D/World_BigBag.prefab");
        GameObject rigPrefab = profile.StartingRigWorldPrefab != null
            ? profile.StartingRigWorldPrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemPrefabIn3D/World_BigRig.prefab");

        float spacing = Mathf.Max(0.8f, profile.StartingDropSpacing);
        Vector3 rightOffset = new Vector3(spacing, profile.SpawnHeightOffset, 0f);
        Vector3 leftOffset = new Vector3(-spacing, profile.SpawnHeightOffset, 0f);

        if (bagPrefab != null)
        {
            GameObject bagObject = InstantiatePrefab(bagPrefab, parent);
            bagObject.name = "StartingBigBag";
            bagObject.transform.position = spawnPosition + rightOffset;
            bagObject.transform.rotation = Quaternion.identity;
        }

        if (rigPrefab != null)
        {
            GameObject rigObject = InstantiatePrefab(rigPrefab, parent);
            rigObject.name = "StartingBigRig";
            rigObject.transform.position = spawnPosition + leftOffset;
            rigObject.transform.rotation = Quaternion.identity;
        }
    }

    private static void EnsureExtractionPoint(RaidMvpPopulationProfile profile, RaidRegionMarker marker, Transform extractionRoot)
    {
        if (!profile.AutoCreateExtractionPointForExtractionRegion || marker == null)
        {
            return;
        }

        Bounds bounds = marker.GetWorldBounds();
        ExtractionPointController[] extractionPoints = UnityEngine.Object.FindObjectsOfType<ExtractionPointController>(true);
        for (int i = 0; i < extractionPoints.Length; i++)
        {
            if (extractionPoints[i] != null && bounds.Contains(extractionPoints[i].transform.position))
            {
                return;
            }
        }

        GameObject extractionObject = new GameObject($"Extraction_{marker.RegionId}");
        Undo.RegisterCreatedObjectUndo(extractionObject, "Create Extraction Point");
        extractionObject.transform.SetParent(extractionRoot, false);
        extractionObject.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        BoxCollider trigger = extractionObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, profile.ExtractionTriggerSize.y * 0.5f, 0f);
        trigger.size = profile.ExtractionTriggerSize;

        ExtractionPointController extractionPoint = extractionObject.AddComponent<ExtractionPointController>();
        extractionPoint.ExtractionPointName = string.IsNullOrWhiteSpace(marker.RegionId) ? "撤离点" : marker.RegionId;
        extractionPoint.ExtractionDurationSeconds = profile.ExtractionDurationSeconds;

        GameObject markerDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(markerDisc, "Create Extraction Marker");
        markerDisc.name = "Marker";
        markerDisc.transform.SetParent(extractionObject.transform, false);
        markerDisc.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        markerDisc.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);

        Renderer rendererComponent = markerDisc.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.2f, 0.9f, 0.62f, 0.95f)
            };
        }

        Collider markerCollider = markerDisc.GetComponent<Collider>();
        if (markerCollider != null)
        {
            UnityEngine.Object.DestroyImmediate(markerCollider);
        }
    }

    private static void SpawnEntriesInBounds(
        RaidMvpPopulationProfile profile,
        RaidRegionPrefabPool pool,
        List<RaidSpawnPrefabEntry> entries,
        Vector2Int countRange,
        Bounds bounds,
        float edgePadding,
        float defaultSpacing,
        Transform parent,
        System.Random random)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        int minCount = Mathf.Min(countRange.x, countRange.y);
        int maxCount = Mathf.Max(countRange.x, countRange.y);
        if (maxCount <= 0)
        {
            return;
        }

        int targetCount = random.Next(minCount, maxCount + 1);
        List<Vector3> occupiedPositions = new List<Vector3>();
        List<float> occupiedRadii = new List<float>();

        for (int i = 0; i < targetCount; i++)
        {
            RaidSpawnPrefabEntry entry = PickWeightedEntry(entries, random);
            if (entry == null || entry.Prefab == null)
            {
                continue;
            }

            float spacing = entry.MinSpacing > 0f ? entry.MinSpacing : defaultSpacing;
            if (!TryFindPlacementPosition(profile, bounds, edgePadding, profile.SpawnHeightOffset, random, occupiedPositions, occupiedRadii, spacing, out Vector3 position))
            {
                continue;
            }

            position = EnemyGroundingUtility.ReplaceHeightOffsetWithRootGroundOffset(
                entry.Prefab,
                position,
                profile.SpawnHeightOffset);

            GameObject spawnedObject = InstantiatePrefab(entry.Prefab, parent);
            spawnedObject.name = string.IsNullOrWhiteSpace(entry.Label) ? entry.Prefab.name : entry.Label;
            spawnedObject.transform.position = position + entry.PositionOffset;

            Quaternion rotation = Quaternion.Euler(entry.RotationOffset);
            if (entry.RandomizeYaw)
            {
                rotation = Quaternion.Euler(entry.RotationOffset + new Vector3(0f, (float)(random.NextDouble() * 360.0), 0f));
            }

            spawnedObject.transform.rotation = rotation;
            occupiedPositions.Add(spawnedObject.transform.position);
            occupiedRadii.Add(spacing);
        }
    }

    private static RaidSpawnPrefabEntry PickWeightedEntry(List<RaidSpawnPrefabEntry> entries, System.Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Prefab != null)
            {
                totalWeight += Mathf.Max(1, entries[i].Weight);
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = random.Next(0, totalWeight);
        int cumulative = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            RaidSpawnPrefabEntry entry = entries[i];
            if (entry == null || entry.Prefab == null)
            {
                continue;
            }

            cumulative += Mathf.Max(1, entry.Weight);
            if (roll < cumulative)
            {
                return entry;
            }
        }

        return null;
    }

    private static bool TryFindPlacementPosition(
        RaidMvpPopulationProfile profile,
        Bounds bounds,
        float edgePadding,
        float heightOffset,
        System.Random random,
        List<Vector3> occupiedPositions,
        out Vector3 result)
    {
        return TryFindPlacementPosition(profile, bounds, edgePadding, heightOffset, random, occupiedPositions, null, profile.DefaultEnemySpacing, out result);
    }

    private static bool TryFindPlacementPosition(
        RaidMvpPopulationProfile profile,
        Bounds bounds,
        float edgePadding,
        float heightOffset,
        System.Random random,
        List<Vector3> occupiedPositions,
        List<float> occupiedRadii,
        float candidateSpacing,
        out Vector3 result)
    {
        float minX = bounds.min.x + edgePadding;
        float maxX = bounds.max.x - edgePadding;
        float minZ = bounds.min.z + edgePadding;
        float maxZ = bounds.max.z - edgePadding;

        if (minX >= maxX || minZ >= maxZ)
        {
            result = bounds.center;
            return false;
        }

        for (int attempt = 0; attempt < 48; attempt++)
        {
            float x = Mathf.Lerp(minX, maxX, (float)random.NextDouble());
            float z = Mathf.Lerp(minZ, maxZ, (float)random.NextDouble());
            Vector3 candidate = ResolveGroundedPosition(profile, new Vector3(x, bounds.max.y + profile.GroundProbeHeight, z), bounds.min.y + heightOffset);

            if (!IsFarEnough(candidate, occupiedPositions, occupiedRadii, candidateSpacing))
            {
                continue;
            }

            result = candidate;
            return true;
        }

        result = bounds.center + Vector3.up * heightOffset;
        return false;
    }

    private static Vector3 ResolveGroundedPosition(RaidMvpPopulationProfile profile, Vector3 probeStart, float fallbackY)
    {
        if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit hit, profile.GroundProbeHeight * 2f, profile.GroundMask))
        {
            return hit.point + Vector3.up * profile.SpawnHeightOffset;
        }

        return new Vector3(probeStart.x, fallbackY, probeStart.z);
    }

    private static bool IsFarEnough(Vector3 candidate, List<Vector3> occupiedPositions, List<float> occupiedRadii, float candidateSpacing)
    {
        if (occupiedPositions == null || occupiedPositions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            float occupiedRadius = occupiedRadii != null && i < occupiedRadii.Count ? occupiedRadii[i] : candidateSpacing;
            float requiredDistance = Mathf.Max(candidateSpacing, occupiedRadius);
            if (Vector3.Distance(candidate, occupiedPositions[i]) < requiredDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(prefab, parent);
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Spawn {prefab.name}");
        return instance;
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

    private static void ApplyNameHeuristics(RaidRegionMarker marker, string sourceName)
    {
        string lowerName = sourceName.ToLowerInvariant();
        if (lowerName.Contains("boss"))
        {
            marker.Purpose = RaidRegionPurpose.Boss;
            marker.Density = RaidSpawnDensity.Boss;
            return;
        }

        if (lowerName.Contains("extract") || lowerName.Contains("撤离"))
        {
            marker.Purpose = RaidRegionPurpose.Extraction;
            marker.Density = RaidSpawnDensity.None;
            return;
        }

        if (lowerName.Contains("spawn") || lowerName.Contains("出生"))
        {
            marker.Purpose = RaidRegionPurpose.Spawn;
            marker.Density = RaidSpawnDensity.None;
            return;
        }

        if (lowerName.Contains("密集") || lowerName.Contains("dense"))
        {
            marker.Purpose = RaidRegionPurpose.DenseResource;
            marker.Density = RaidSpawnDensity.High;
            return;
        }

        marker.Purpose = RaidRegionPurpose.Resource;
        marker.Density = RaidSpawnDensity.Medium;
    }

    private static bool IsSelectionInside(GameObject root)
    {
        if (root == null || Selection.gameObjects == null)
        {
            return false;
        }

        for (int i = 0; i < Selection.gameObjects.Length; i++)
        {
            GameObject selectedObject = Selection.gameObjects[i];
            if (selectedObject == null)
            {
                continue;
            }

            if (selectedObject == root || selectedObject.transform.IsChildOf(root.transform))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureRaidFlowControllerExists()
    {
        RaidFlowController existingController = UnityEngine.Object.FindObjectOfType<RaidFlowController>();
        if (existingController != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("RaidFlowController");
        Undo.RegisterCreatedObjectUndo(controllerObject, "Create Raid Flow Controller");
        controllerObject.AddComponent<RaidFlowController>();
    }

    private static void EnsureRuntimeNavMeshBuilderExists()
    {
        RuntimeNavMeshSurfaceBuilder existingBuilder = UnityEngine.Object.FindObjectOfType<RuntimeNavMeshSurfaceBuilder>();
        if (existingBuilder != null)
        {
            return;
        }

        GameObject builderObject = new GameObject("RuntimeNavMeshSurfaceBuilder");
        Undo.RegisterCreatedObjectUndo(builderObject, "Create Runtime NavMesh Surface Builder");
        builderObject.AddComponent<RuntimeNavMeshSurfaceBuilder>();
    }
}

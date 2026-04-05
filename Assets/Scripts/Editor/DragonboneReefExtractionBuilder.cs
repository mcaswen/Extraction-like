using UnityEditor;
using UnityEngine;

public static class DragonboneReefExtractionBuilder
{
    private const float FloorThickness = 0.2f;
    private const float CliffHeight = 4.8f;

    [MenuItem("Tools/Whitebox/Create Dragonbone Reef Extraction From Selection")]
    private static void CreateFromSelection()
    {
        if (!TryGetSelectionBounds(out Bounds selectionBounds))
        {
            EditorUtility.DisplayDialog(
                "Create Dragonbone Reef Extraction",
                "Please select floor or wall objects inside the target region first.",
                "OK");
            return;
        }

        GameObject root = new GameObject("Whitebox_DragonboneReefExtraction");
        Undo.RegisterCreatedObjectUndo(root, "Create Dragonbone Reef Extraction");
        root.transform.position = new Vector3(selectionBounds.center.x, selectionBounds.min.y, selectionBounds.center.z);

        Vector3 localSize = new Vector3(
            Mathf.Max(24f, selectionBounds.size.x * 0.95f),
            0f,
            Mathf.Max(18f, selectionBounds.size.z * 0.95f));

        CreateBaseTerrain(root.transform, localSize);
        CreateCliffRing(root.transform, localSize);
        CreateExtractionApproach(root.transform, localSize);
        CreateReefClusters(root.transform, localSize);
        CreateDragonboneRibs(root.transform, localSize);
        CreateExtractionPoint(root.transform, localSize);
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

    private static void CreateBaseTerrain(Transform parent, Vector3 areaSize)
    {
        GameObject shore = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shore.name = "ShorePlatform";
        shore.transform.SetParent(parent, false);
        shore.transform.localPosition = new Vector3(0f, FloorThickness * 0.5f, 0f);
        shore.transform.localScale = new Vector3(areaSize.x, FloorThickness, areaSize.z);
        ApplyColor(shore, new Color(0.46f, 0.44f, 0.4f, 1f));

        GameObject wetShelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wetShelf.name = "WetShelf";
        wetShelf.transform.SetParent(parent, false);
        wetShelf.transform.localPosition = new Vector3(0f, 0.04f, areaSize.z * 0.26f);
        wetShelf.transform.localScale = new Vector3(areaSize.x, 0.08f, areaSize.z * 0.32f);
        ApplyColor(wetShelf, new Color(0.26f, 0.4f, 0.46f, 1f));

        GameObject tidalLane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tidalLane.name = "TidalLane";
        tidalLane.transform.SetParent(parent, false);
        tidalLane.transform.localPosition = new Vector3(-areaSize.x * 0.08f, 0.12f, 0f);
        tidalLane.transform.localScale = new Vector3(areaSize.x * 0.24f, 0.05f, areaSize.z * 0.88f);
        ApplyColor(tidalLane, new Color(0.38f, 0.4f, 0.36f, 1f));
    }

    private static void CreateCliffRing(Transform parent, Vector3 areaSize)
    {
        Transform cliffs = new GameObject("Cliffs").transform;
        cliffs.SetParent(parent, false);

        float halfWidth = areaSize.x * 0.5f;
        float halfDepth = areaSize.z * 0.5f;

        CreateCliff(cliffs, new Vector3(-halfWidth, CliffHeight * 0.5f, -areaSize.z * 0.06f), new Vector3(1.1f, CliffHeight, areaSize.z * 0.9f), "WestCliff");
        CreateCliff(cliffs, new Vector3(halfWidth, CliffHeight * 0.5f, -areaSize.z * 0.02f), new Vector3(1.1f, CliffHeight, areaSize.z * 0.82f), "EastCliff_Upper");
        CreateCliff(cliffs, new Vector3(halfWidth, CliffHeight * 0.5f, areaSize.z * 0.34f), new Vector3(1.1f, CliffHeight, areaSize.z * 0.18f), "EastCliff_Lower");
        CreateCliff(cliffs, new Vector3(0f, CliffHeight * 0.5f, -halfDepth), new Vector3(areaSize.x * 0.9f, CliffHeight, 1.1f), "NorthCliff");

        GameObject southRockLip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        southRockLip.name = "SouthRockLip";
        southRockLip.transform.SetParent(cliffs, false);
        southRockLip.transform.localPosition = new Vector3(0f, 1.2f, halfDepth);
        southRockLip.transform.localScale = new Vector3(areaSize.x * 0.94f, 2.4f, 0.9f);
        ApplyColor(southRockLip, new Color(0.24f, 0.26f, 0.28f, 1f));
        southRockLip.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateExtractionApproach(Transform parent, Vector3 areaSize)
    {
        Transform approach = new GameObject("Approach").transform;
        approach.SetParent(parent, false);

        GameObject narrowPath = GameObject.CreatePrimitive(PrimitiveType.Cube);
        narrowPath.name = "NarrowPath";
        narrowPath.transform.SetParent(approach, false);
        narrowPath.transform.localPosition = new Vector3(areaSize.x * 0.12f, 0.18f, areaSize.z * 0.08f);
        narrowPath.transform.localScale = new Vector3(areaSize.x * 0.18f, 0.18f, areaSize.z * 0.62f);
        ApplyColor(narrowPath, new Color(0.34f, 0.34f, 0.32f, 1f));

        GameObject extractionShelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        extractionShelf.name = "ExtractionShelf";
        extractionShelf.transform.SetParent(approach, false);
        extractionShelf.transform.localPosition = new Vector3(areaSize.x * 0.22f, 0.26f, areaSize.z * 0.3f);
        extractionShelf.transform.localScale = new Vector3(areaSize.x * 0.24f, 0.26f, areaSize.z * 0.2f);
        ApplyColor(extractionShelf, new Color(0.42f, 0.4f, 0.36f, 1f));

        GameObject sideBypass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sideBypass.name = "SideBypass";
        sideBypass.transform.SetParent(approach, false);
        sideBypass.transform.localPosition = new Vector3(-areaSize.x * 0.16f, 0.14f, areaSize.z * 0.16f);
        sideBypass.transform.localScale = new Vector3(areaSize.x * 0.16f, 0.12f, areaSize.z * 0.44f);
        ApplyColor(sideBypass, new Color(0.32f, 0.34f, 0.32f, 1f));
    }

    private static void CreateReefClusters(Transform parent, Vector3 areaSize)
    {
        Transform reefRoot = new GameObject("ReefClusters").transform;
        reefRoot.SetParent(parent, false);

        CreateRockCluster(reefRoot, new Vector3(-areaSize.x * 0.24f, 0.55f, -areaSize.z * 0.04f), "Reef_A", 1.3f);
        CreateRockCluster(reefRoot, new Vector3(areaSize.x * 0.02f, 0.55f, areaSize.z * 0.02f), "Reef_B", 1.1f);
        CreateRockCluster(reefRoot, new Vector3(areaSize.x * 0.28f, 0.55f, areaSize.z * 0.12f), "Reef_C", 1.45f);
        CreateRockCluster(reefRoot, new Vector3(-areaSize.x * 0.06f, 0.55f, areaSize.z * 0.28f), "Reef_D", 1.6f);

        CreateTidePool(reefRoot, new Vector3(-areaSize.x * 0.18f, 0.03f, areaSize.z * 0.32f), new Vector3(2.8f, 0.06f, 1.6f), "TidePool_A");
        CreateTidePool(reefRoot, new Vector3(areaSize.x * 0.1f, 0.03f, areaSize.z * 0.38f), new Vector3(2.2f, 0.06f, 1.3f), "TidePool_B");
    }

    private static void CreateDragonboneRibs(Transform parent, Vector3 areaSize)
    {
        Transform ribs = new GameObject("DragonboneRibs").transform;
        ribs.SetParent(parent, false);
        ribs.localPosition = new Vector3(-areaSize.x * 0.26f, 0.2f, areaSize.z * 0.18f);

        for (int i = 0; i < 5; i++)
        {
            GameObject rib = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rib.name = $"Rib_{i}";
            rib.transform.SetParent(ribs, false);
            rib.transform.localPosition = new Vector3(i * 0.95f, 1.0f, Mathf.Sin(i * 0.7f) * 0.32f);
            rib.transform.localRotation = Quaternion.Euler(0f, 18f, 58f - i * 6f);
            rib.transform.localScale = new Vector3(0.18f, 2.4f, 0.32f);
            ApplyColor(rib, new Color(0.76f, 0.74f, 0.68f, 1f));
        }

        GameObject spine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spine.name = "Spine";
        spine.transform.SetParent(ribs, false);
        spine.transform.localPosition = new Vector3(1.9f, 0.62f, 0f);
        spine.transform.localRotation = Quaternion.Euler(0f, 12f, 8f);
        spine.transform.localScale = new Vector3(4.6f, 0.28f, 0.42f);
        ApplyColor(spine, new Color(0.68f, 0.66f, 0.6f, 1f));
    }

    private static void CreateExtractionPoint(Transform parent, Vector3 areaSize)
    {
        Transform extractionRoot = new GameObject("ExtractionZone").transform;
        extractionRoot.SetParent(parent, false);
        extractionRoot.localPosition = new Vector3(areaSize.x * 0.22f, 0.26f, areaSize.z * 0.3f);

        GameObject extractionMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        extractionMarker.name = "DragonboneReefExtractionPoint";
        extractionMarker.transform.SetParent(extractionRoot, false);
        extractionMarker.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        extractionMarker.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);
        ApplyColor(extractionMarker, new Color(0.24f, 0.78f, 0.48f, 0.95f));

        Collider markerCollider = extractionMarker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Object.DestroyImmediate(markerCollider);
        }

        BoxCollider trigger = extractionRoot.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1f, 0f);
        trigger.size = new Vector3(4.4f, 2.4f, 4.4f);

        ExtractionPointController extractionPoint = extractionRoot.gameObject.AddComponent<ExtractionPointController>();
        extractionPoint.ExtractionPointName = "龙骨礁撤离点";
        extractionPoint.ExtractionDurationSeconds = 4f;

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beacon.name = "ExtractionBeacon";
        beacon.transform.SetParent(extractionRoot, false);
        beacon.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        beacon.transform.localScale = new Vector3(0.18f, 3.6f, 0.18f);
        ApplyColor(beacon, new Color(0.22f, 0.84f, 0.54f, 1f));
    }

    private static void CreateCliff(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cliff.name = name;
        cliff.transform.SetParent(parent, false);
        cliff.transform.localPosition = localPosition;
        cliff.transform.localScale = scale;
        ApplyColor(cliff, new Color(0.22f, 0.24f, 0.26f, 1f));
        cliff.AddComponent<PerspectiveFadeWall>();
    }

    private static void CreateRockCluster(Transform parent, Vector3 localPosition, string name, float scaleMultiplier)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(parent, false);
        clusterRoot.localPosition = localPosition;

        Vector3[] offsets =
        {
            new Vector3(-0.8f, 0f, -0.24f),
            new Vector3(0.18f, 0.08f, 0.32f),
            new Vector3(0.84f, -0.06f, -0.28f)
        };

        Vector3[] scales =
        {
            new Vector3(1.2f, 1f, 1f),
            new Vector3(0.96f, 1.18f, 1.12f),
            new Vector3(0.84f, 0.92f, 0.8f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            rock.transform.SetParent(clusterRoot, false);
            rock.transform.localPosition = offsets[i] * scaleMultiplier;
            rock.transform.localRotation = Quaternion.Euler(0f, i * 24f, i * 8f);
            rock.transform.localScale = scales[i] * scaleMultiplier;
            ApplyColor(rock, new Color(0.28f, 0.3f, 0.32f, 1f));
        }
    }

    private static void CreateTidePool(Transform parent, Vector3 localPosition, Vector3 scale, string name)
    {
        GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pool.name = name;
        pool.transform.SetParent(parent, false);
        pool.transform.localPosition = localPosition;
        pool.transform.localScale = scale;
        ApplyColor(pool, new Color(0.16f, 0.46f, 0.54f, 0.92f));
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

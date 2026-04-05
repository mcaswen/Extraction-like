using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 根据 2026-04-05 的红线草图生成大比例岛图白模场景。
/// 该版本优先保证整体墙体框架正确，便于后续逐区细化。
/// </summary>
public static class IslandRaidWhiteboxSceneBuilder
{
    private const string SceneFolderPath = "Assets/Scenes/Island";
    private const string SceneAssetPath = "Assets/Scenes/Island/Scene_lyl_IslandWhitebox.unity";
    private const float CellSize = 14f;
    private const float FloorThickness = 0.35f;
    private const float WallHeight = 8.5f;
    private const float WallThickness = 0.55f;

    private enum ZoneType
    {
        Corridor,
        Resource,
        DenseResource,
        Boss
    }

    private readonly struct FillRect
    {
        public FillRect(int x, int y, int width, int height, ZoneType zoneType)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ZoneType = zoneType;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public ZoneType ZoneType { get; }
    }

    private readonly struct MarkerDefinition
    {
        public MarkerDefinition(string name, Vector2 cell, Color color, Vector3 scale, string label)
        {
            Name = name;
            Cell = cell;
            Color = color;
            Scale = scale;
            Label = label;
        }

        public string Name { get; }
        public Vector2 Cell { get; }
        public Color Color { get; }
        public Vector3 Scale { get; }
        public string Label { get; }
    }

    [MenuItem("Tools/Whitebox/Generate Large Island Wall Layout Scene")]
    private static void GenerateLargeIslandWallLayoutScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Directory.CreateDirectory(SceneFolderPath);

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        newScene.name = "Scene_lyl_IslandWhitebox";

        Dictionary<Vector2Int, ZoneType> cells = BuildLargeCellMap();
        Transform root = CreateRoot("IslandWallLayoutRoot");

        CreateRaidFlowController(root);
        CreateRuntimeNavMeshBuilder(root);
        CreateWaterBase(root, cells);
        CreateFloorTiles(root, cells);
        CreatePerimeterWalls(root, cells);
        CreateMarkers(root);
        CreateLabels(root);
        CreatePerspectiveFadeController(root);
        PositionCameraForPreview();

        EditorSceneManager.SaveScene(newScene, SceneAssetPath);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Wall Layout Generated",
            $"新的大地图墙体白模已生成并覆盖保存到:\n{SceneAssetPath}",
            "OK");
    }

    private static Dictionary<Vector2Int, ZoneType> BuildLargeCellMap()
    {
        Dictionary<Vector2Int, ZoneType> cells = new Dictionary<Vector2Int, ZoneType>();

        FillRect[] fills =
        {
            new FillRect(0, 5, 8, 6, ZoneType.Resource),
            new FillRect(0, 0, 7, 5, ZoneType.Resource),
            new FillRect(6, 8, 16, 4, ZoneType.Boss),
            new FillRect(8, 2, 6, 6, ZoneType.Resource),
            new FillRect(12, 0, 8, 4, ZoneType.Corridor),
            new FillRect(18, 2, 11, 7, ZoneType.Resource),
            new FillRect(21, 12, 8, 6, ZoneType.DenseResource),
            new FillRect(18, 0, 7, 5, ZoneType.DenseResource),
            new FillRect(21, 9, 2, 3, ZoneType.Corridor),
            new FillRect(20, 6, 2, 3, ZoneType.Corridor),
            new FillRect(14, 8, 4, 2, ZoneType.Corridor),
            new FillRect(7, 4, 3, 2, ZoneType.Corridor),
            new FillRect(6, 10, 2, 2, ZoneType.Corridor),
            new FillRect(26, 3, 4, 2, ZoneType.Corridor)
        };

        foreach (FillRect fill in fills)
        {
            for (int x = fill.X; x < fill.X + fill.Width; x++)
            {
                for (int y = fill.Y; y < fill.Y + fill.Height; y++)
                {
                    cells[new Vector2Int(x, y)] = fill.ZoneType;
                }
            }
        }

        return cells;
    }

    private static void CreateRaidFlowController(Transform root)
    {
        GameObject flowObject = new GameObject("RaidFlowController");
        flowObject.transform.SetParent(root, false);
        RaidFlowController raidFlow = flowObject.AddComponent<RaidFlowController>();
        raidFlow.RequireLootBeforeExtraction = true;
        raidFlow.MissionName = "Large Island Wall Layout";
    }

    private static void CreateWaterBase(Transform root, Dictionary<Vector2Int, ZoneType> cells)
    {
        Bounds bounds = ComputeBounds(cells.Keys);
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "WaterBase";
        water.transform.SetParent(root, false);
        water.transform.position = new Vector3(bounds.center.x, -0.55f, bounds.center.z);
        water.transform.localScale = new Vector3(bounds.size.x + CellSize * 6f, 0.6f, bounds.size.z + CellSize * 6f);
        ApplyColor(water, new Color(0.42f, 0.52f, 0.6f, 1f));
    }

    private static void CreateFloorTiles(Transform root, Dictionary<Vector2Int, ZoneType> cells)
    {
        Transform floorRoot = CreateRoot("FloorTiles", root);
        foreach (KeyValuePair<Vector2Int, ZoneType> pair in cells)
        {
            GameObject floorTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorTile.name = $"Floor_{pair.Key.x}_{pair.Key.y}";
            floorTile.transform.SetParent(floorRoot, false);
            floorTile.transform.position = GridToWorld(pair.Key.x, pair.Key.y, 0f);
            floorTile.transform.localScale = new Vector3(CellSize, FloorThickness, CellSize);
            ApplyColor(floorTile, GetZoneColor(pair.Value));
        }
    }

    private static void CreatePerimeterWalls(Transform root, Dictionary<Vector2Int, ZoneType> cells)
    {
        Transform wallRoot = CreateRoot("Walls", root);
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        foreach (Vector2Int cell in cells.Keys)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int neighbor = cell + directions[i];
                if (cells.ContainsKey(neighbor))
                {
                    continue;
                }

                bool horizontal = directions[i] == Vector2Int.up || directions[i] == Vector2Int.down;
                Vector3 wallScale = horizontal
                    ? new Vector3(CellSize, WallHeight, WallThickness)
                    : new Vector3(WallThickness, WallHeight, CellSize);

                Vector3 offset = directions[i] == Vector2Int.up
                    ? new Vector3(0f, WallHeight * 0.5f, CellSize * 0.5f)
                    : directions[i] == Vector2Int.down
                        ? new Vector3(0f, WallHeight * 0.5f, -CellSize * 0.5f)
                        : directions[i] == Vector2Int.right
                            ? new Vector3(CellSize * 0.5f, WallHeight * 0.5f, 0f)
                            : new Vector3(-CellSize * 0.5f, WallHeight * 0.5f, 0f);

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{cell.x}_{cell.y}_{i}";
                wall.transform.SetParent(wallRoot, false);
                wall.transform.position = GridToWorld(cell.x, cell.y, 0f) + offset;
                wall.transform.localScale = wallScale;
                ApplyColor(wall, new Color(0.21f, 0.23f, 0.28f, 1f));
                wall.AddComponent<PerspectiveFadeWall>();
            }
        }
    }

    private static void CreateMarkers(Transform root)
    {
        Transform markerRoot = CreateRoot("Markers", root);
        MarkerDefinition[] markers =
        {
            new MarkerDefinition("SpawnPoint", new Vector2(3f, 1f), new Color(0.2f, 0.58f, 1f, 1f), new Vector3(4f, 0.35f, 4f), "出生点"),
            new MarkerDefinition("BossMarker", new Vector2(14f, 9.5f), new Color(1f, 0.72f, 0.24f, 1f), new Vector3(5f, 0.4f, 5f), "Boss 区"),
            new MarkerDefinition("Extraction_A", new Vector2(27f, 15f), new Color(0.38f, 1f, 0.48f, 1f), new Vector3(5f, 0.35f, 5f), "撤离点 A"),
            new MarkerDefinition("Extraction_B", new Vector2(29f, 1f), new Color(0.38f, 1f, 0.48f, 1f), new Vector3(5f, 0.35f, 5f), "撤离点 B")
        };

        foreach (MarkerDefinition marker in markers)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = marker.Name;
            markerObject.transform.SetParent(markerRoot, false);
            markerObject.transform.position = GridToWorld(marker.Cell.x, marker.Cell.y, 0.12f);
            markerObject.transform.localScale = marker.Scale;
            ApplyColor(markerObject, marker.Color);

            if (marker.Name.StartsWith("Extraction"))
            {
                BoxCollider trigger = markerObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(CellSize * 1.8f, 2.5f, CellSize * 1.8f);
                ExtractionPointController extractionPoint = markerObject.AddComponent<ExtractionPointController>();
                extractionPoint.ExtractionPointName = marker.Label;
                extractionPoint.ExtractionDurationSeconds = 3.2f;
            }
        }
    }

    private static void CreateLabels(Transform root)
    {
        Transform labelRoot = CreateRoot("Labels", root);
        CreateWorldLabel(labelRoot, "Label_WestUpper", "普通资源区", GridToWorld(3.6f, 9.4f, 3f), 1.1f);
        CreateWorldLabel(labelRoot, "Label_WestLower", "普通资源区", GridToWorld(3.2f, 4.2f, 3f), 1.1f);
        CreateWorldLabel(labelRoot, "Label_CenterLower", "普通资源区", GridToWorld(10.6f, 5.2f, 3f), 1.1f);
        CreateWorldLabel(labelRoot, "Label_East", "普通资源区", GridToWorld(23.2f, 7.6f, 3f), 1.1f);
        CreateWorldLabel(labelRoot, "Label_Boss", "核心区 (Boss)", GridToWorld(14.4f, 10.9f, 3.4f), 1.3f);
        CreateWorldLabel(labelRoot, "Label_TopRightDense", "资源密集区", GridToWorld(24.8f, 16.4f, 3.4f), 1.0f);
        CreateWorldLabel(labelRoot, "Label_BottomRightDense", "资源密集区", GridToWorld(20.6f, 2.0f, 3.4f), 1.0f);
    }

    private static void CreateWorldLabel(Transform parent, string name, string text, Vector3 position, float characterSize)
    {
        GameObject labelObject = new GameObject(name);
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = 40;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.12f, 0.12f, 0.14f, 1f);
    }

    private static void CreatePerspectiveFadeController(Transform root)
    {
        GameObject fadeControllerObject = new GameObject("PerspectiveWallFadeController");
        fadeControllerObject.transform.SetParent(root, false);
        PerspectiveWallFadeController fadeController = fadeControllerObject.AddComponent<PerspectiveWallFadeController>();
        fadeController.OccluderMask = ~0;
    }

    private static void CreateRuntimeNavMeshBuilder(Transform root)
    {
        GameObject navMeshBuilderObject = new GameObject("RuntimeNavMeshSurfaceBuilder");
        navMeshBuilderObject.transform.SetParent(root, false);
        navMeshBuilderObject.AddComponent<RuntimeNavMeshSurfaceBuilder>();
    }

    private static Transform CreateRoot(string name, Transform parent = null)
    {
        GameObject rootObject = new GameObject(name);
        if (parent != null)
        {
            rootObject.transform.SetParent(parent, false);
        }

        return rootObject.transform;
    }

    private static void PositionCameraForPreview()
    {
        Camera sceneCamera = Object.FindObjectOfType<Camera>();
        if (sceneCamera == null)
        {
            return;
        }

        sceneCamera.transform.position = new Vector3(132f, 128f, -78f);
        sceneCamera.transform.rotation = Quaternion.Euler(64f, 0f, 0f);
    }

    private static Bounds ComputeBounds(ICollection<Vector2Int> cells)
    {
        bool initialized = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Vector2Int cell in cells)
        {
            Vector3 world = GridToWorld(cell.x, cell.y, 0f);
            Bounds cellBounds = new Bounds(world, new Vector3(CellSize, 0.2f, CellSize));
            if (!initialized)
            {
                bounds = cellBounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(cellBounds);
            }
        }

        return bounds;
    }

    private static Vector3 GridToWorld(float gridX, float gridY, float y)
    {
        return new Vector3((gridX - 14f) * CellSize, y, (gridY - 8f) * CellSize);
    }

    private static Color GetZoneColor(ZoneType zoneType)
    {
        return zoneType switch
        {
            ZoneType.Corridor => new Color(0.7f, 0.7f, 0.73f, 1f),
            ZoneType.Resource => new Color(0.78f, 0.78f, 0.81f, 1f),
            ZoneType.DenseResource => new Color(0.75f, 0.8f, 0.77f, 1f),
            ZoneType.Boss => new Color(0.56f, 0.58f, 0.62f, 1f),
            _ => new Color(0.76f, 0.76f, 0.78f, 1f)
        };
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
}

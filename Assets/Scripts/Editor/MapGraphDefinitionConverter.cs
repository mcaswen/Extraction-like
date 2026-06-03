using System.IO;
using BoardGame.Config;
using BoardGame.Runtime;
using Gameplay.MapGraph.Config;
using UnityEditor;
using UnityEngine;

public static class MapGraphDefinitionConverter
{
    private const string OutputFolder = "Assets/SO/MapGraph";

    [MenuItem("Tools/Map Graph/Convert Selected BoardGame Map")]
    private static void ConvertSelectedBoardGameMap()
    {
        if (!(Selection.activeObject is SO_BoardGame_MapDefinition boardMapDefinition))
        {
            EditorUtility.DisplayDialog(
                "Convert BoardGame Map",
                "Please select a SO_BoardGame_MapDefinition asset first.",
                "OK");
            return;
        }

        SO_MapGraphDefinition mapGraphDefinition = CreateMapGraphAsset(boardMapDefinition);
        Selection.activeObject = mapGraphDefinition;
        EditorGUIUtility.PingObject(mapGraphDefinition);
    }

    [MenuItem("Tools/Map Graph/Convert Selected BoardGame Map", true)]
    private static bool CanConvertSelectedBoardGameMap()
    {
        return Selection.activeObject is SO_BoardGame_MapDefinition;
    }

    [MenuItem("Tools/Map Graph/Convert All BoardGame Maps")]
    private static void ConvertAllBoardGameMaps()
    {
        string[] guids = AssetDatabase.FindAssets("t:SO_BoardGame_MapDefinition");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Convert BoardGame Maps",
                "No SO_BoardGame_MapDefinition assets were found.",
                "OK");
            return;
        }

        SO_MapGraphDefinition lastCreatedAsset = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            SO_BoardGame_MapDefinition boardMapDefinition =
                AssetDatabase.LoadAssetAtPath<SO_BoardGame_MapDefinition>(assetPath);
            if (boardMapDefinition == null)
                continue;

            lastCreatedAsset = CreateMapGraphAsset(boardMapDefinition);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (lastCreatedAsset != null)
        {
            Selection.activeObject = lastCreatedAsset;
            EditorGUIUtility.PingObject(lastCreatedAsset);
        }
    }

    private static SO_MapGraphDefinition CreateMapGraphAsset(SO_BoardGame_MapDefinition boardMapDefinition)
    {
        EnsureOutputFolder();

        SO_MapGraphDefinition mapGraphDefinition = ScriptableObject.CreateInstance<SO_MapGraphDefinition>();
        PopulateMapGraphDefinition(mapGraphDefinition, boardMapDefinition);

        string safeName = MakeSafeAssetName(boardMapDefinition.name.Replace("SO_BoardGame_MapDefinition", string.Empty));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = boardMapDefinition.name;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{OutputFolder}/SO_MapGraphDefinition{safeName}.asset");
        AssetDatabase.CreateAsset(mapGraphDefinition, assetPath);
        AssetDatabase.SaveAssets();
        return mapGraphDefinition;
    }

    private static void PopulateMapGraphDefinition(
        SO_MapGraphDefinition mapGraphDefinition,
        SO_BoardGame_MapDefinition boardMapDefinition)
    {
        SerializedObject serializedObject = new SerializedObject(mapGraphDefinition);
        serializedObject.FindProperty("_mapId").stringValue = boardMapDefinition.MapId;
        serializedObject.FindProperty("_displayName").stringValue = boardMapDefinition.DisplayName;
        serializedObject.FindProperty("_startNodeId").stringValue = boardMapDefinition.StartNodeId;
        PopulateNodeIconSet(
            serializedObject.FindProperty("_nodeIconSet"),
            boardMapDefinition.NodeIconSet);

        SerializedProperty nodesProperty = serializedObject.FindProperty("_nodes");
        nodesProperty.arraySize = boardMapDefinition.Nodes.Count;
        for (int i = 0; i < boardMapDefinition.Nodes.Count; i++)
        {
            BoardMapNodeDefinition boardNode = boardMapDefinition.Nodes[i];
            SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(i);
            nodeProperty.FindPropertyRelative("_nodeId").stringValue = boardNode.NodeId;
            nodeProperty.FindPropertyRelative("_displayName").stringValue = boardNode.NodeId;
            nodeProperty.FindPropertyRelative("_nodeKind").intValue =
                (int)MapBoardNodeKind(boardNode.NodeType);
            nodeProperty.FindPropertyRelative("_position").vector2Value = boardNode.Position;
            nodeProperty.FindPropertyRelative("_description").stringValue =
                BuildNodeDescription(boardNode);
            nodeProperty.FindPropertyRelative("_iconKind").intValue =
                (int)MapBoardNodeIconKind(boardNode.NodeType);
            nodeProperty.FindPropertyRelative("_resourceTier").intValue =
                (int)MapBoardResourceTier(boardNode.ResourceTier);
            nodeProperty.FindPropertyRelative("_dangerTier").intValue =
                (int)MapBoardDangerTier(boardNode.DangerTier);
            nodeProperty.FindPropertyRelative("_icon").objectReferenceValue =
                boardMapDefinition.NodeIconSet != null
                    ? boardMapDefinition.NodeIconSet.GetIcon(
                        boardNode.NodeType,
                        boardNode.ResourceTier,
                        boardNode.DangerTier)
                    : null;
        }

        SerializedProperty edgesProperty = serializedObject.FindProperty("_edges");
        edgesProperty.arraySize = boardMapDefinition.Edges.Count;
        for (int i = 0; i < boardMapDefinition.Edges.Count; i++)
        {
            BoardMapEdgeDefinition boardEdge = boardMapDefinition.Edges[i];
            SerializedProperty edgeProperty = edgesProperty.GetArrayElementAtIndex(i);
            edgeProperty.FindPropertyRelative("_edgeId").stringValue = boardEdge.EdgeId;
            edgeProperty.FindPropertyRelative("_fromNodeId").stringValue = boardEdge.FromNodeId;
            edgeProperty.FindPropertyRelative("_toNodeId").stringValue = boardEdge.ToNodeId;
            edgeProperty.FindPropertyRelative("_lengthUnits").floatValue = boardEdge.LengthUnits;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mapGraphDefinition);
    }

    private static void PopulateNodeIconSet(
        SerializedProperty iconSetProperty,
        BoardGameNodeIconSet boardNodeIconSet)
    {
        if (iconSetProperty == null || boardNodeIconSet == null)
            return;

        iconSetProperty.FindPropertyRelative("_startIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Start, BoardResourceTier.None, BoardDangerTier.None);
        iconSetProperty.FindPropertyRelative("_resourceLowIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Resource, BoardResourceTier.Low, BoardDangerTier.None);
        iconSetProperty.FindPropertyRelative("_resourceMediumIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Resource, BoardResourceTier.Medium, BoardDangerTier.None);
        iconSetProperty.FindPropertyRelative("_resourceHighIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Resource, BoardResourceTier.High, BoardDangerTier.None);
        iconSetProperty.FindPropertyRelative("_enemyLowIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Low);
        iconSetProperty.FindPropertyRelative("_enemyMediumIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Medium);
        iconSetProperty.FindPropertyRelative("_enemyHighIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.High);
        iconSetProperty.FindPropertyRelative("_bossIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Boss, BoardResourceTier.None, BoardDangerTier.High);
        iconSetProperty.FindPropertyRelative("_extractIcon").objectReferenceValue =
            boardNodeIconSet.GetIcon(BoardNodeType.Extract, BoardResourceTier.None, BoardDangerTier.None);
    }

    private static MapGraphNodeKind MapBoardNodeKind(BoardNodeType nodeType)
    {
        switch (nodeType)
        {
            case BoardNodeType.Start:
                return MapGraphNodeKind.Start;
            case BoardNodeType.Resource:
                return MapGraphNodeKind.Resource;
            case BoardNodeType.Enemy:
            case BoardNodeType.Boss:
                return MapGraphNodeKind.EnemySource;
            case BoardNodeType.Extract:
                return MapGraphNodeKind.Extraction;
            default:
                return MapGraphNodeKind.Custom;
        }
    }

    private static MapGraphNodeIconKind MapBoardNodeIconKind(BoardNodeType nodeType)
    {
        switch (nodeType)
        {
            case BoardNodeType.Start:
                return MapGraphNodeIconKind.Start;
            case BoardNodeType.Resource:
                return MapGraphNodeIconKind.Resource;
            case BoardNodeType.Enemy:
                return MapGraphNodeIconKind.Enemy;
            case BoardNodeType.Boss:
                return MapGraphNodeIconKind.Boss;
            case BoardNodeType.Extract:
                return MapGraphNodeIconKind.Extraction;
            default:
                return MapGraphNodeIconKind.None;
        }
    }

    private static MapGraphResourceTier MapBoardResourceTier(BoardResourceTier resourceTier)
    {
        switch (resourceTier)
        {
            case BoardResourceTier.Low:
                return MapGraphResourceTier.Low;
            case BoardResourceTier.Medium:
                return MapGraphResourceTier.Medium;
            case BoardResourceTier.High:
                return MapGraphResourceTier.High;
            default:
                return MapGraphResourceTier.None;
        }
    }

    private static MapGraphDangerTier MapBoardDangerTier(BoardDangerTier dangerTier)
    {
        switch (dangerTier)
        {
            case BoardDangerTier.Low:
                return MapGraphDangerTier.Low;
            case BoardDangerTier.Medium:
                return MapGraphDangerTier.Medium;
            case BoardDangerTier.High:
                return MapGraphDangerTier.High;
            default:
                return MapGraphDangerTier.None;
        }
    }

    private static string BuildNodeDescription(BoardMapNodeDefinition boardNode)
    {
        string description = boardNode.Description ?? string.Empty;
        string boardTags =
            $"BoardType={boardNode.NodeType}; ResourceTier={boardNode.ResourceTier}; DangerTier={boardNode.DangerTier}";
        return string.IsNullOrWhiteSpace(description)
            ? boardTags
            : $"{description}\n{boardTags}";
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            string parent = Path.GetDirectoryName(OutputFolder)?.Replace("\\", "/");
            string leaf = Path.GetFileName(OutputFolder);
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets/SO", "MapGraph");
            else
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    private static string MakeSafeAssetName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalidChar.ToString(), string.Empty);

        return safe;
    }
}

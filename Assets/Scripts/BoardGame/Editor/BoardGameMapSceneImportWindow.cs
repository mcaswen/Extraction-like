using System;
using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime;
using UnityEditor;
using UnityEngine;

namespace BoardGame.Editor
{
    /// <summary>
    /// 场景节点导入窗口
    /// 把场景中摆放好的节点位置和 ID 写回地图 SO
    /// </summary>
    public sealed class BoardGameMapSceneImportWindow : EditorWindow
    {
        private readonly List<BoardGameSceneNodeMarker> _cachedMarkers =
            new List<BoardGameSceneNodeMarker>();

        private SO_BoardGame_MapDefinition _mapDefinition;
        private Transform _sceneNodeRoot;
        private bool _removeMissingNodes;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/BoardGame/Scene Node Import Tool")]
        private static void OpenWindow()
        {
            GetWindow<BoardGameMapSceneImportWindow>("Map Node Import");
        }

        private void OnEnable()
        {
            RefreshMarkers();
        }

        private void OnHierarchyChange()
        {
            RefreshMarkers();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Node Import", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add BoardGameSceneNodeMarker to scene node objects\n" +
                "Fill NodeId and write positions plus the start node back into the map asset\n" +
                "This import currently syncs NodeId, position and start node id only\n" +
                "Existing node type, tier and description will be preserved",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _mapDefinition = (SO_BoardGame_MapDefinition)EditorGUILayout.ObjectField(
                "Map Asset",
                _mapDefinition,
                typeof(SO_BoardGame_MapDefinition),
                false);
            _sceneNodeRoot = (Transform)EditorGUILayout.ObjectField(
                "Scene Node Root",
                _sceneNodeRoot,
                typeof(Transform),
                true);
            _removeMissingNodes = EditorGUILayout.ToggleLeft("Remove nodes from the asset that are missing in the scene", _removeMissingNodes);

            if (EditorGUI.EndChangeCheck())
            {
                RefreshMarkers();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Scene Nodes"))
                {
                    RefreshMarkers();
                }

                using (new EditorGUI.DisabledScope(_mapDefinition == null))
                {
                    if (GUILayout.Button("Write To Map Asset"))
                    {
                        ImportMarkersToMap();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Nodes Found  {_cachedMarkers.Count}", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (BoardGameSceneNodeMarker marker in _cachedMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                Vector2 position = marker.GetWorldPosition2D();
                string startLabel = marker.IsStartNode ? "Start" : "Node";
                EditorGUILayout.LabelField(
                    $"{marker.NodeId}  {startLabel}  ({position.x:F2}, {position.y:F2})");
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 按根节点或全场景重新收集摆点标记
        /// </summary>
        private void RefreshMarkers()
        {
            _cachedMarkers.Clear();

            BoardGameSceneNodeMarker[] markers = _sceneNodeRoot != null
                ? _sceneNodeRoot.GetComponentsInChildren<BoardGameSceneNodeMarker>(true)
                : FindObjectsByType<BoardGameSceneNodeMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Array.Sort(markers, CompareMarkers);
            _cachedMarkers.AddRange(markers);
        }

        /// <summary>
        /// 校验场景节点数据并写回地图 SO
        /// </summary>
        private void ImportMarkersToMap()
        {
            RefreshMarkers();

            if (_mapDefinition == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "Please assign a map asset first", "OK");
                return;
            }

            if (_cachedMarkers.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Failed", "No BoardGameSceneNodeMarker was found in the current scene", "OK");
                return;
            }

            if (!TryBuildImportEntries(
                    out List<BoardMapSceneNodeImportEntry> importEntries,
                    out string importedStartNodeId,
                    out string errorMessage))
            {
                EditorUtility.DisplayDialog("Import Failed", errorMessage, "OK");
                return;
            }

            Undo.RecordObject(_mapDefinition, "Import Scene Nodes To Map Asset");
            _mapDefinition.ImportSceneNodeLayout(importEntries, _removeMissingNodes, importedStartNodeId);
            EditorUtility.SetDirty(_mapDefinition);
            Selection.activeObject = _mapDefinition;

            string startNodeText = string.IsNullOrEmpty(importedStartNodeId)
                ? "Start node unchanged"
                : $"Start node  {importedStartNodeId}";

            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Nodes written  {importEntries.Count}\n{startNodeText}",
                "OK");
        }

        /// <summary>
        /// 生成写回地图 SO 的导入快照
        /// 同时校验空 ID 重复 ID 和起点数量
        /// </summary>
        private bool TryBuildImportEntries(
            out List<BoardMapSceneNodeImportEntry> importEntries,
            out string importedStartNodeId,
            out string errorMessage)
        {
            importEntries = new List<BoardMapSceneNodeImportEntry>(_cachedMarkers.Count);
            importedStartNodeId = string.Empty;
            errorMessage = string.Empty;

            HashSet<string> importedIds = new HashSet<string>(StringComparer.Ordinal);
            int startNodeCount = 0;

            foreach (BoardGameSceneNodeMarker marker in _cachedMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                string nodeId = marker.NodeId != null ? marker.NodeId.Trim() : string.Empty;

                if (string.IsNullOrEmpty(nodeId))
                {
                    errorMessage = $"A scene node object is missing NodeId  {marker.name}";
                    return false;
                }

                if (!importedIds.Add(nodeId))
                {
                    errorMessage = $"Duplicate NodeId found in scene  {nodeId}";
                    return false;
                }

                if (marker.IsStartNode)
                {
                    startNodeCount += 1;
                    importedStartNodeId = nodeId;
                }

                if (startNodeCount > 1)
                {
                    errorMessage = "Only one start node is allowed in the scene";
                    return false;
                }

                Vector2 position = marker.GetWorldPosition2D();
                importEntries.Add(new BoardMapSceneNodeImportEntry(nodeId, position, marker.IsStartNode));
            }

            return true;
        }

        private static int CompareMarkers(BoardGameSceneNodeMarker first, BoardGameSceneNodeMarker second)
        {
            if (ReferenceEquals(first, second))
            {
                return 0;
            }

            if (first == null)
            {
                return -1;
            }

            if (second == null)
            {
                return 1;
            }

            return string.CompareOrdinal(first.NodeId, second.NodeId);
        }
    }
}

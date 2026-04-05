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
        private readonly List<BoardGameSceneAgentSpawnMarker> _cachedSpawnMarkers =
            new List<BoardGameSceneAgentSpawnMarker>();

        private SO_BoardGame_MapDefinition _mapDefinition;
        private SO_BoardGame_AgentRoster _agentRoster;
        private Transform _sceneNodeRoot;
        private bool _removeMissingNodes;
        private bool _clearMissingAgentSpawnAssignments;
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
                "Add BoardGameSceneAgentSpawnMarker to scene spawn objects when you want to refresh agent starts\n" +
                "Fill NodeId and write positions plus the start node back into the map asset\n" +
                "Assign an agent roster asset to write AgentId start nodes back into the roster\n" +
                "Use the normal write button to sync scene positions only\n" +
                "Use the planner preset button to fill node type, tier and edges by NodeId",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _mapDefinition = (SO_BoardGame_MapDefinition)EditorGUILayout.ObjectField(
                "Map Asset",
                _mapDefinition,
                typeof(SO_BoardGame_MapDefinition),
                false);
            _agentRoster = (SO_BoardGame_AgentRoster)EditorGUILayout.ObjectField(
                "Agent Roster",
                _agentRoster,
                typeof(SO_BoardGame_AgentRoster),
                false);
            _sceneNodeRoot = (Transform)EditorGUILayout.ObjectField(
                "Scene Node Root",
                _sceneNodeRoot,
                typeof(Transform),
                true);
            _removeMissingNodes = EditorGUILayout.ToggleLeft("Remove nodes from the asset that are missing in the scene", _removeMissingNodes);
            _clearMissingAgentSpawnAssignments = EditorGUILayout.ToggleLeft(
                "Clear roster start nodes for agents that are missing spawn markers",
                _clearMissingAgentSpawnAssignments);

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
                    if (GUILayout.Button("Write Positions To Map Asset"))
                    {
                        ImportMarkersToMap(false);
                    }

                    if (GUILayout.Button("Write Planner Preset To Map Asset"))
                    {
                        ImportMarkersToMap(true);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Nodes Found  {_cachedMarkers.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Agent Spawns Found  {_cachedSpawnMarkers.Count}", EditorStyles.boldLabel);

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

            if (_cachedSpawnMarkers.Count > 0)
            {
                EditorGUILayout.Space();
            }

            foreach (BoardGameSceneAgentSpawnMarker spawnMarker in _cachedSpawnMarkers)
            {
                if (spawnMarker == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField($"{spawnMarker.AgentId}  Start -> {spawnMarker.NodeId}");
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 按根节点或全场景重新收集摆点标记
        /// </summary>
        private void RefreshMarkers()
        {
            _cachedMarkers.Clear();
            _cachedSpawnMarkers.Clear();

            BoardGameSceneNodeMarker[] markers = _sceneNodeRoot != null
                ? _sceneNodeRoot.GetComponentsInChildren<BoardGameSceneNodeMarker>(true)
                : FindObjectsByType<BoardGameSceneNodeMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            BoardGameSceneAgentSpawnMarker[] spawnMarkers = _sceneNodeRoot != null
                ? _sceneNodeRoot.GetComponentsInChildren<BoardGameSceneAgentSpawnMarker>(true)
                : FindObjectsByType<BoardGameSceneAgentSpawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Array.Sort(markers, CompareMarkers);
            Array.Sort(spawnMarkers, CompareSpawnMarkers);
            _cachedMarkers.AddRange(markers);
            _cachedSpawnMarkers.AddRange(spawnMarkers);
        }

        /// <summary>
        /// 校验场景节点数据并写回地图 SO
        /// </summary>
        private void ImportMarkersToMap(bool applyPlannerPresetByNodeId)
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

            if (applyPlannerPresetByNodeId &&
                !TryValidatePlannerPreset(importEntries, importedStartNodeId, out errorMessage))
            {
                EditorUtility.DisplayDialog("Import Failed", errorMessage, "OK");
                return;
            }

            if (!TryBuildSpawnImportEntries(importEntries, out List<BoardGameAgentSpawnImportEntry> spawnImportEntries, out errorMessage))
            {
                EditorUtility.DisplayDialog("Import Failed", errorMessage, "OK");
                return;
            }

            Undo.RecordObject(_mapDefinition, "Import Scene Nodes To Map Asset");
            _mapDefinition.ImportSceneNodeLayout(
                importEntries,
                _removeMissingNodes,
                importedStartNodeId,
                applyPlannerPresetByNodeId);
            EditorUtility.SetDirty(_mapDefinition);

            if (_agentRoster != null && (spawnImportEntries.Count > 0 || _clearMissingAgentSpawnAssignments))
            {
                Undo.RecordObject(_agentRoster, "Import Scene Agent Spawns To Agent Roster");
                _agentRoster.ImportSceneSpawnLayout(spawnImportEntries, _clearMissingAgentSpawnAssignments);
                EditorUtility.SetDirty(_agentRoster);
            }

            Selection.activeObject = _mapDefinition;

            string finalStartNodeId = !string.IsNullOrEmpty(importedStartNodeId)
                ? importedStartNodeId
                : applyPlannerPresetByNodeId
                    ? BoardGamePlannerPresetConfig.PlannerMapStartNodeId
                    : string.Empty;

            string startNodeText = string.IsNullOrEmpty(finalStartNodeId)
                ? "Start node unchanged"
                : $"Start node  {finalStartNodeId}";
            string spawnImportText = BuildSpawnImportSummary(spawnImportEntries.Count);

            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Nodes written  {importEntries.Count}\n{startNodeText}\n{spawnImportText}",
                "OK");
        }

        /// <summary>
        /// 校验场景中的节点 ID 是否能完整匹配策划固定表
        /// </summary>
        private static bool TryValidatePlannerPreset(
            IReadOnlyList<BoardMapSceneNodeImportEntry> importEntries,
            string importedStartNodeId,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            HashSet<string> importedNodeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BoardMapSceneNodeImportEntry importEntry in importEntries)
            {
                if (!string.IsNullOrEmpty(importEntry.NodeId))
                {
                    importedNodeIds.Add(importEntry.NodeId);
                }
            }

            HashSet<string> plannerNodeIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BoardPlannerMapNodePresetDefinition plannerNode in BoardGamePlannerPresetConfig.CreatePlannerMapNodePresets())
            {
                plannerNodeIds.Add(plannerNode.NodeId);

                if (!importedNodeIds.Contains(plannerNode.NodeId))
                {
                    errorMessage = $"Planner preset node is missing in scene  {plannerNode.NodeId}";
                    return false;
                }
            }

            foreach (BoardMapSceneNodeImportEntry importEntry in importEntries)
            {
                if (!plannerNodeIds.Contains(importEntry.NodeId))
                {
                    errorMessage = $"Scene node id is not defined in planner preset  {importEntry.NodeId}";
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(importedStartNodeId) &&
                !string.Equals(importedStartNodeId, BoardGamePlannerPresetConfig.PlannerMapStartNodeId, StringComparison.Ordinal))
            {
                errorMessage = $"Planner preset start node must be {BoardGamePlannerPresetConfig.PlannerMapStartNodeId} but scene marked {importedStartNodeId}";
                return false;
            }

            return true;
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

        /// <summary>
        /// 生成写回 roster 的出生位快照
        /// 同时校验 AgentId NodeId 重复和未知 Agent
        /// </summary>
        private bool TryBuildSpawnImportEntries(
            IReadOnlyList<BoardMapSceneNodeImportEntry> importEntries,
            out List<BoardGameAgentSpawnImportEntry> spawnImportEntries,
            out string errorMessage)
        {
            spawnImportEntries = new List<BoardGameAgentSpawnImportEntry>(_cachedSpawnMarkers.Count);
            errorMessage = string.Empty;

            if (_cachedSpawnMarkers.Count == 0)
            {
                return true;
            }

            if (_agentRoster == null)
            {
                return true;
            }

            HashSet<string> validNodeIds = new HashSet<string>(StringComparer.Ordinal);

            if (_mapDefinition != null)
            {
                foreach (BoardMapNodeDefinition nodeDefinition in _mapDefinition.Nodes)
                {
                    if (!string.IsNullOrEmpty(nodeDefinition.NodeId))
                    {
                        validNodeIds.Add(nodeDefinition.NodeId);
                    }
                }
            }

            if (importEntries != null)
            {
                foreach (BoardMapSceneNodeImportEntry importEntry in importEntries)
                {
                    if (!string.IsNullOrEmpty(importEntry.NodeId))
                    {
                        validNodeIds.Add(importEntry.NodeId);
                    }
                }
            }

            HashSet<string> importedAgentIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (BoardGameSceneAgentSpawnMarker spawnMarker in _cachedSpawnMarkers)
            {
                if (spawnMarker == null)
                {
                    continue;
                }

                string agentId = spawnMarker.AgentId != null ? spawnMarker.AgentId.Trim() : string.Empty;
                string nodeId = spawnMarker.NodeId != null ? spawnMarker.NodeId.Trim() : string.Empty;

                if (string.IsNullOrEmpty(agentId))
                {
                    errorMessage = $"A scene agent spawn object is missing AgentId  {spawnMarker.name}";
                    return false;
                }

                if (string.IsNullOrEmpty(nodeId))
                {
                    errorMessage = $"Agent spawn marker is missing NodeId  {spawnMarker.name}";
                    return false;
                }

                if (!importedAgentIds.Add(agentId))
                {
                    errorMessage = $"Duplicate AgentId found in scene spawn markers  {agentId}";
                    return false;
                }

                if (!validNodeIds.Contains(nodeId))
                {
                    errorMessage = $"Spawn marker references a node that is not available in the scene or map asset  {agentId} -> {nodeId}";
                    return false;
                }

                if (!_agentRoster.HasAgentEntry(agentId))
                {
                    errorMessage = $"Spawn marker AgentId is not defined in the assigned roster  {agentId}";
                    return false;
                }

                spawnImportEntries.Add(new BoardGameAgentSpawnImportEntry(agentId, nodeId));
            }

            return true;
        }

        /// <summary>
        /// 生成出生位导入结果摘要
        /// </summary>
        private string BuildSpawnImportSummary(int spawnImportCount)
        {
            if (_agentRoster == null)
            {
                return _cachedSpawnMarkers.Count > 0
                    ? "Agent spawn markers were ignored because no roster asset was assigned"
                    : "Agent spawns unchanged";
            }

            if (spawnImportCount <= 0 && !_clearMissingAgentSpawnAssignments)
            {
                return "Agent spawns unchanged";
            }

            return $"Agent spawns written  {spawnImportCount}";
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

        private static int CompareSpawnMarkers(BoardGameSceneAgentSpawnMarker first, BoardGameSceneAgentSpawnMarker second)
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

            return string.CompareOrdinal(first.AgentId, second.AgentId);
        }
    }
}

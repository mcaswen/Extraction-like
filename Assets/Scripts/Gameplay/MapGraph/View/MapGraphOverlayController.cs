using System.Collections.Generic;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>
    /// 抽象地图 UI 总控
    /// 负责生成 Image 版点边视图，并把 AgentGraphProjectionController 的运行时投影刷新到界面
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapGraphOverlayController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SO_MapGraphDefinition _mapDefinition;
        [SerializeField] private MapGraphBindingAuthoring _bindingAuthoring;
        [SerializeField] private AgentGraphProjectionController _projectionController;

        [Header("UI Roots")]
        [SerializeField] private RectTransform _overlayRoot;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private RectTransform _edgesRoot;
        [SerializeField] private RectTransform _nodesRoot;
        [SerializeField] private RectTransform _agentsRoot;

        [Header("View Prefabs")]
        [SerializeField] private MapGraphNodeView _nodeViewPrefab;
        [SerializeField] private MapGraphEdgeView _edgeViewPrefab;
        [SerializeField] private MapGraphAgentView _agentViewPrefab;

        [Header("Layout")]
        [SerializeField] private bool _createDefaultBackground;
        [SerializeField] private Vector2 _defaultPanelSize = new Vector2(860f, 560f);
        [SerializeField] private Color _backgroundColor = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        [SerializeField] private Vector2 _mapScale = new Vector2(48f, 48f);
        [SerializeField] private Vector2 _mapOffset;
        [SerializeField] private float _edgeWidth = 4f;
        [SerializeField] private Color _edgeColor = new Color(0.38f, 0.48f, 0.58f, 0.58f);
        [SerializeField] private float _nodeAgentClusterOffsetScale = 0.88f;
        [SerializeField] private float _edgeAgentLaneSpacingScale = 0.72f;
        [SerializeField] private float _fallbackAgentClusterRadius = 18f;

        [Header("Input")]
        [SerializeField] private bool _startVisible;
        [SerializeField] private KeyCode _toggleKey = KeyCode.G;

        private readonly Dictionary<string, MapGraphNodeView> _nodeViewsById =
            new Dictionary<string, MapGraphNodeView>();
        private readonly Dictionary<string, MapGraphEdgeView> _edgeViewsById =
            new Dictionary<string, MapGraphEdgeView>();
        private readonly Dictionary<string, MapGraphAgentView> _agentViewsById =
            new Dictionary<string, MapGraphAgentView>();
        private readonly HashSet<string> _targetedNodeIds = new HashSet<string>();
        private readonly HashSet<string> _occupiedNodeIds = new HashSet<string>();
        private readonly HashSet<string> _highlightedEdgeIds = new HashSet<string>();
        private readonly HashSet<string> _visibleAgentIds = new HashSet<string>();
        private readonly List<string> _staleAgentIds = new List<string>();
        private readonly Dictionary<string, Vector2> _agentDisplayPositionsById =
            new Dictionary<string, Vector2>();
        private readonly Dictionary<string, List<MapGraphAgentRuntimeState>> _agentsByClusterKey =
            new Dictionary<string, List<MapGraphAgentRuntimeState>>();
        private readonly Dictionary<string, AgentClusterAnchor> _clusterAnchorsByKey =
            new Dictionary<string, AgentClusterAnchor>();
        private readonly HashSet<string> _loggedIconSpecMismatchNodeIds = new HashSet<string>();

        private CanvasGroup _canvasGroup;
        private MapGraphService _graphService;
        private bool _isInitialized;
        private bool _isVisible;
        private bool _hasLoggedMissingUiReferences;

        private enum AgentClusterLayoutType
        {
            Node,
            Edge,
            World
        }

        private struct AgentClusterAnchor
        {
            public Vector2 Center;
            public float BaseRadius;
            public Vector2 Direction;
            public AgentClusterLayoutType LayoutType;
        }

        private void Awake()
        {
            EnsureRootReferences();
            SetVisible(_startVisible);
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (_toggleKey != KeyCode.None && Input.GetKeyDown(_toggleKey))
                SetVisible(!_isVisible);

            EnsureInitialized();

            if (_projectionController != null)
                _projectionController.Tick(Time.deltaTime);

            if (_isVisible)
                RefreshViews();
        }

        /// <summary>
        /// 重新生成整张图的 UI 视图
        /// </summary>
        [ContextMenu("Rebuild Map Graph Views")]
        public void RebuildViews()
        {
            _isInitialized = false;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            EnsureRootReferences();

            if (_mapDefinition == null && _bindingAuthoring != null)
                _mapDefinition = _bindingAuthoring.MapDefinition;

            if (_mapDefinition == null)
                return;

            if (_isInitialized)
                return;

            if (!HasRequiredUiReferences())
                return;

            _graphService = new MapGraphService(_mapDefinition);
            if (!_graphService.IsValid)
                return;

            if (_projectionController == null)
                _projectionController = GetComponent<AgentGraphProjectionController>();
            if (_projectionController == null)
            {
                Debug.LogWarning(
                    $"{nameof(MapGraphOverlayController)} on {name} is missing {nameof(AgentGraphProjectionController)}. " +
                    "Use the generated UGUI overlay prefab instead of relying on runtime component construction.",
                    this);
                return;
            }

            _projectionController.Initialize(_mapDefinition, _bindingAuthoring);
            BuildMapViews();
            _isInitialized = true;
        }

        private void BuildMapViews()
        {
            ClearRoot(_edgesRoot);
            ClearRoot(_nodesRoot);
            ClearRoot(_agentsRoot);
            _nodeViewsById.Clear();
            _edgeViewsById.Clear();
            _agentViewsById.Clear();
            _loggedIconSpecMismatchNodeIds.Clear();

            IReadOnlyList<MapGraphNodeDefinition> nodes = _mapDefinition.Nodes;
            for (int index = 0; index < nodes.Count; index++)
            {
                MapGraphNodeDefinition node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    continue;

                MapGraphNodeView nodeView = CreateNodeView(_nodesRoot);
                if (nodeView == null)
                    continue;

                nodeView.RectTransform.anchoredPosition = ToAnchoredPosition(node.Position);
                LogNodeIconSpecMismatchIfNeeded(node);
                nodeView.Initialize(node, ResolveNodeColor(node.NodeKind), ResolveNodeIcon(node));
                _nodeViewsById[node.NodeId] = nodeView;
            }

            IReadOnlyList<MapGraphEdgeDefinition> edges = _mapDefinition.Edges;
            for (int index = 0; index < edges.Count; index++)
            {
                MapGraphEdgeDefinition edge = edges[index];
                if (edge == null)
                    continue;

                Vector2 from = ToAnchoredPosition(_graphService.GetNodePosition(edge.FromNodeId));
                Vector2 to = ToAnchoredPosition(_graphService.GetNodePosition(edge.ToNodeId));
                MapGraphEdgeView edgeView = CreateEdgeView(_edgesRoot);
                if (edgeView == null)
                    continue;

                edgeView.Initialize(
                    edge.EdgeId,
                    from,
                    to,
                    _edgeWidth,
                    _edgeColor,
                    GetNodeVisualRadius(edge.FromNodeId),
                    GetNodeVisualRadius(edge.ToNodeId));
                _edgeViewsById[edge.EdgeId] = edgeView;
            }
        }

        // 每帧先汇总运行时状态，再分别刷新边、节点和 Agent，避免各视图重复扫描 Agent 列表
        private void RefreshViews()
        {
            if (_projectionController == null)
                return;

            BuildRuntimeLookupSets(_projectionController.AgentStates);
            RefreshEdgeViews();
            RefreshNodeViews();
            RefreshAgentViews(_projectionController.AgentStates);
        }

        // 预计算目标节点、占用节点和高亮路径，保持 View 层刷新逻辑简单
        private void BuildRuntimeLookupSets(IReadOnlyList<MapGraphAgentRuntimeState> agentStates)
        {
            _targetedNodeIds.Clear();
            _occupiedNodeIds.Clear();
            _highlightedEdgeIds.Clear();

            for (int index = 0; index < agentStates.Count; index++)
            {
                MapGraphAgentRuntimeState state = agentStates[index];
                if (state == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(state.CurrentTargetNodeId))
                    _targetedNodeIds.Add(state.CurrentTargetNodeId);

                if (!string.IsNullOrWhiteSpace(state.CurrentNodeId))
                    _occupiedNodeIds.Add(state.CurrentNodeId);

                AddHighlightedEdgesForAgent(state);
            }
        }

        // 路径高亮不仅包含当前边，也包含剩余路径上的后续边
        private void AddHighlightedEdgesForAgent(MapGraphAgentRuntimeState state)
        {
            if (!string.IsNullOrWhiteSpace(state.CurrentEdgeId))
                _highlightedEdgeIds.Add(state.CurrentEdgeId);

            string currentNodeId = state.IsOnEdge
                ? ResolveEdgeTargetNodeId(state)
                : state.CurrentNodeId;

            for (int index = 0; index < state.RemainingPathNodeIds.Count; index++)
            {
                string nextNodeId = state.RemainingPathNodeIds[index];
                if (string.IsNullOrWhiteSpace(currentNodeId) ||
                    !_graphService.TryGetEdgeBetween(
                        currentNodeId,
                        nextNodeId,
                        out MapGraphEdgeDefinition edgeDefinition))
                {
                    currentNodeId = nextNodeId;
                    continue;
                }

                _highlightedEdgeIds.Add(edgeDefinition.EdgeId);
                currentNodeId = nextNodeId;
            }
        }

        private void RefreshEdgeViews()
        {
            foreach (KeyValuePair<string, MapGraphEdgeView> edgeViewPair in _edgeViewsById)
                edgeViewPair.Value.Refresh(_highlightedEdgeIds.Contains(edgeViewPair.Key));
        }

        private void RefreshNodeViews()
        {
            foreach (KeyValuePair<string, MapGraphNodeView> nodeViewPair in _nodeViewsById)
            {
                string nodeId = nodeViewPair.Key;
                bool hasBeenTouched = false;
                bool hasBeenCompleted = false;

                if (_bindingAuthoring != null &&
                    _bindingAuthoring.TryResolveTargetForNodeId(nodeId, out GameplayTargetAuthoringBase target))
                {
                    hasBeenTouched = target.HasBeenTouched;
                    hasBeenCompleted = target.HasBeenCompleted;
                }

                nodeViewPair.Value.Refresh(new MapGraphNodePresentationState(
                    _targetedNodeIds.Contains(nodeId),
                    _occupiedNodeIds.Contains(nodeId),
                    hasBeenTouched,
                    hasBeenCompleted));
            }
        }

        private void RefreshAgentViews(IReadOnlyList<MapGraphAgentRuntimeState> agentStates)
        {
            BuildAgentDisplayPositions(agentStates);
            _visibleAgentIds.Clear();
            for (int index = 0; index < agentStates.Count; index++)
            {
                MapGraphAgentRuntimeState state = agentStates[index];
                if (state == null || string.IsNullOrWhiteSpace(state.AgentId))
                    continue;

                _visibleAgentIds.Add(state.AgentId);
                if (!_agentViewsById.TryGetValue(state.AgentId, out MapGraphAgentView agentView))
                {
                    agentView = CreateAgentView(_agentsRoot);
                    if (agentView == null)
                        continue;

                    agentView.Initialize(state.AgentId);
                    _agentViewsById[state.AgentId] = agentView;
                }

                Vector2 displayPosition = _agentDisplayPositionsById.TryGetValue(
                    state.AgentId,
                    out Vector2 resolvedPosition)
                    ? resolvedPosition
                    : ToAnchoredPosition(state.GraphPosition);
                agentView.Refresh(state, displayPosition);
            }

            _staleAgentIds.Clear();
            foreach (KeyValuePair<string, MapGraphAgentView> agentViewPair in _agentViewsById)
            {
                if (!_visibleAgentIds.Contains(agentViewPair.Key))
                    _staleAgentIds.Add(agentViewPair.Key);
            }

            for (int index = 0; index < _staleAgentIds.Count; index++)
            {
                string staleAgentId = _staleAgentIds[index];
                if (!_agentViewsById.TryGetValue(staleAgentId, out MapGraphAgentView staleView))
                    continue;

                DestroyViewObject(staleView.gameObject);
                _agentViewsById.Remove(staleAgentId);
            }
        }

        private void BuildAgentDisplayPositions(IReadOnlyList<MapGraphAgentRuntimeState> agentStates)
        {
            _agentDisplayPositionsById.Clear();
            _agentsByClusterKey.Clear();
            _clusterAnchorsByKey.Clear();

            for (int index = 0; index < agentStates.Count; index++)
            {
                MapGraphAgentRuntimeState state = agentStates[index];
                if (state == null || string.IsNullOrWhiteSpace(state.AgentId))
                    continue;

                string clusterKey = BuildAgentClusterKey(state);
                if (!_agentsByClusterKey.TryGetValue(clusterKey, out List<MapGraphAgentRuntimeState> clusteredAgents))
                {
                    clusteredAgents = new List<MapGraphAgentRuntimeState>();
                    _agentsByClusterKey.Add(clusterKey, clusteredAgents);
                    _clusterAnchorsByKey.Add(clusterKey, ResolveAgentClusterAnchor(state));
                }

                clusteredAgents.Add(state);
            }

            foreach (KeyValuePair<string, List<MapGraphAgentRuntimeState>> clusterPair in _agentsByClusterKey)
            {
                List<MapGraphAgentRuntimeState> clusteredAgents = clusterPair.Value;
                clusteredAgents.Sort(CompareAgentClusterOrder);
                AgentClusterAnchor clusterAnchor = _clusterAnchorsByKey[clusterPair.Key];

                for (int index = 0; index < clusteredAgents.Count; index++)
                {
                    MapGraphAgentRuntimeState state = clusteredAgents[index];
                    _agentDisplayPositionsById[state.AgentId] =
                        clusterAnchor.Center + ResolveAgentClusterOffset(clusterAnchor, index, clusteredAgents.Count);
                }
            }
        }

        private static string BuildAgentClusterKey(MapGraphAgentRuntimeState state)
        {
            if (state.IsOnEdge && !string.IsNullOrWhiteSpace(state.CurrentEdgeId))
                return $"edge_{state.CurrentEdgeId}_{state.CurrentEdgeProgress01:0.###}";

            if (!string.IsNullOrWhiteSpace(state.CurrentNodeId))
                return $"node_{state.CurrentNodeId}";

            return $"world_{state.GraphPosition.x:0.###}_{state.GraphPosition.y:0.###}";
        }

        private AgentClusterAnchor ResolveAgentClusterAnchor(MapGraphAgentRuntimeState state)
        {
            if (state.IsOnEdge &&
                !string.IsNullOrWhiteSpace(state.CurrentEdgeId) &&
                _graphService.TryGetEdge(state.CurrentEdgeId, out MapGraphEdgeDefinition edgeDefinition))
            {
                Vector2 fromPosition = ToAnchoredPosition(_graphService.GetNodePosition(edgeDefinition.FromNodeId));
                Vector2 toPosition = ToAnchoredPosition(_graphService.GetNodePosition(edgeDefinition.ToNodeId));
                Vector2 direction = (toPosition - fromPosition).normalized;
                if (direction.sqrMagnitude <= Mathf.Epsilon)
                    direction = Vector2.right;

                float averageNodeRadius =
                    (GetNodeVisualRadius(edgeDefinition.FromNodeId) + GetNodeVisualRadius(edgeDefinition.ToNodeId)) * 0.5f;

                return new AgentClusterAnchor
                {
                    Center = ToAnchoredPosition(
                        _graphService.GetPositionOnEdge(edgeDefinition, state.CurrentEdgeProgress01)),
                    BaseRadius = Mathf.Max(_fallbackAgentClusterRadius, averageNodeRadius),
                    Direction = direction,
                    LayoutType = AgentClusterLayoutType.Edge
                };
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentNodeId) &&
                _nodeViewsById.TryGetValue(state.CurrentNodeId, out MapGraphNodeView nodeView))
            {
                return new AgentClusterAnchor
                {
                    Center = nodeView.RectTransform.anchoredPosition,
                    BaseRadius = GetNodeVisualRadius(state.CurrentNodeId),
                    Direction = Vector2.up,
                    LayoutType = AgentClusterLayoutType.Node
                };
            }

            return new AgentClusterAnchor
            {
                Center = ToAnchoredPosition(state.GraphPosition),
                BaseRadius = _fallbackAgentClusterRadius,
                Direction = Vector2.up,
                LayoutType = AgentClusterLayoutType.World
            };
        }

        private Vector2 ResolveAgentClusterOffset(AgentClusterAnchor clusterAnchor, int index, int count)
        {
            if (count <= 1)
                return Vector2.zero;

            switch (clusterAnchor.LayoutType)
            {
                case AgentClusterLayoutType.Edge:
                    return ResolveEdgeLaneOffset(clusterAnchor, index, count);
                case AgentClusterLayoutType.Node:
                    return ResolveNodeClusterOffset(clusterAnchor.BaseRadius * _nodeAgentClusterOffsetScale, index, count);
                default:
                    return ResolveNodeClusterOffset(_fallbackAgentClusterRadius, index, count);
            }
        }

        private Vector2 ResolveEdgeLaneOffset(AgentClusterAnchor clusterAnchor, int index, int count)
        {
            Vector2 normal = new Vector2(-clusterAnchor.Direction.y, clusterAnchor.Direction.x).normalized;
            if (normal.sqrMagnitude <= Mathf.Epsilon)
                normal = Vector2.up;

            float laneIndex = index - ((count - 1) * 0.5f);
            float laneSpacing = clusterAnchor.BaseRadius * _edgeAgentLaneSpacingScale;
            return normal * (laneIndex * laneSpacing);
        }

        private static Vector2 ResolveNodeClusterOffset(float radius, int index, int count)
        {
            switch (count)
            {
                case 2:
                    return ResolvePolarOffset(index == 0 ? 180f : 0f, radius);
                case 3:
                    return ResolvePolarOffset(90f + (120f * index), radius);
                case 4:
                    return ResolvePolarOffset(135f - (90f * index), radius);
                default:
                    return ResolvePolarOffset(90f + ((360f * index) / count), radius);
            }
        }

        private static Vector2 ResolvePolarOffset(float angleDegrees, float radius)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius);
        }

        private static int CompareAgentClusterOrder(
            MapGraphAgentRuntimeState left,
            MapGraphAgentRuntimeState right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            return string.CompareOrdinal(left.AgentId, right.AgentId);
        }

        private void EnsureRootReferences()
        {
            if (_overlayRoot == null)
                _overlayRoot = GetComponent<RectTransform>();

            if (_canvasGroup == null)
                _canvasGroup = _overlayRoot.GetComponent<CanvasGroup>();

            EnsureDefaultPanelSize();
            EnsureBackgroundImage();
            EnsureChildRoot(ref _edgesRoot, "EdgesRoot");
            EnsureChildRoot(ref _nodesRoot, "NodesRoot");
            EnsureChildRoot(ref _agentsRoot, "AgentsRoot");
            ApplyRootSiblingOrder();
        }

        private void EnsureDefaultPanelSize()
        {
            if (_overlayRoot == null)
                return;

            if (_overlayRoot.anchorMin == _overlayRoot.anchorMax &&
                _overlayRoot.sizeDelta == Vector2.zero)
            {
                _overlayRoot.sizeDelta = _defaultPanelSize;
            }
        }

        private void EnsureBackgroundImage()
        {
            if (!_createDefaultBackground || _overlayRoot == null)
                return;

            if (_backgroundImage == null)
            {
                Transform existingBackground = _overlayRoot.Find("Background");
                if (existingBackground != null)
                    _backgroundImage = existingBackground.GetComponent<Image>();
            }

            if (_backgroundImage == null)
                return;

            RectTransform backgroundRect = _backgroundImage.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            _backgroundImage.color = _backgroundColor;
            _backgroundImage.raycastTarget = true;
        }

        private void EnsureChildRoot(ref RectTransform root, string rootName)
        {
            if (root != null)
                return;

            if (_overlayRoot == null)
                return;

            Transform existingRoot = _overlayRoot.Find(rootName);
            if (existingRoot != null)
                root = existingRoot as RectTransform;
        }

        private void ApplyRootSiblingOrder()
        {
            if (_backgroundImage != null)
                _backgroundImage.transform.SetSiblingIndex(0);

            if (_edgesRoot != null)
                _edgesRoot.SetSiblingIndex(_backgroundImage != null ? 1 : 0);

            if (_nodesRoot != null)
                _nodesRoot.SetSiblingIndex(_backgroundImage != null ? 2 : 1);

            if (_agentsRoot != null)
                _agentsRoot.SetSiblingIndex(_backgroundImage != null ? 3 : 2);
        }

        private MapGraphNodeView CreateNodeView(RectTransform parent)
        {
            if (_nodeViewPrefab == null || parent == null)
                return null;

            return Instantiate(_nodeViewPrefab, parent);
        }

        private MapGraphEdgeView CreateEdgeView(RectTransform parent)
        {
            if (_edgeViewPrefab == null || parent == null)
                return null;

            return Instantiate(_edgeViewPrefab, parent);
        }

        private MapGraphAgentView CreateAgentView(RectTransform parent)
        {
            if (_agentViewPrefab == null || parent == null)
                return null;

            return Instantiate(_agentViewPrefab, parent);
        }

        private bool HasRequiredUiReferences()
        {
            bool hasRequiredReferences =
                _overlayRoot != null &&
                _edgesRoot != null &&
                _nodesRoot != null &&
                _agentsRoot != null &&
                _nodeViewPrefab != null &&
                _edgeViewPrefab != null &&
                _agentViewPrefab != null;

            if (hasRequiredReferences)
                return true;

            if (!_hasLoggedMissingUiReferences)
            {
                Debug.LogWarning(
                    $"{nameof(MapGraphOverlayController)} on {name} is missing UGUI prefab references. " +
                    "Use Tools/Map Graph/Create UGUI Prefabs to generate the overlay and view prefabs.",
                    this);
                _hasLoggedMissingUiReferences = true;
            }

            return false;
        }

        private void SetVisible(bool isVisible)
        {
            _isVisible = isVisible;
            if (_canvasGroup == null)
                return;

            _canvasGroup.alpha = isVisible ? 1f : 0f;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;
        }

        private Vector2 ToAnchoredPosition(Vector2 graphPosition)
        {
            return new Vector2(
                (graphPosition.x * _mapScale.x) + _mapOffset.x,
                (graphPosition.y * _mapScale.y) + _mapOffset.y);
        }

        private string ResolveEdgeTargetNodeId(MapGraphAgentRuntimeState state)
        {
            return state.CurrentEdgeTargetProgress01 >= 0.5f
                ? state.CurrentEdgeToNodeId
                : state.CurrentEdgeFromNodeId;
        }

        private float GetNodeVisualRadius(string nodeId)
        {
            return _nodeViewsById.TryGetValue(nodeId, out MapGraphNodeView nodeView)
                ? nodeView.GetVisualRadius()
                : 20f;
        }

        private Sprite ResolveNodeIcon(MapGraphNodeDefinition node)
        {
            if (node == null)
                return null;

            MapGraphNodeIconSet iconSet = _mapDefinition != null ? _mapDefinition.NodeIconSet : null;
            if (iconSet != null &&
                TryResolveBoundNodeIconSpec(
                    node,
                    out MapGraphNodeIconKind boundIconKind,
                    out MapGraphResourceTier boundResourceTier,
                    out MapGraphDangerTier boundDangerTier))
            {
                Sprite boundIcon = iconSet.GetIcon(boundIconKind, boundResourceTier, boundDangerTier);
                if (boundIcon != null)
                    return boundIcon;
            }

            if (iconSet != null)
            {
                Sprite definitionIcon = iconSet.GetIcon(node.IconKind, node.ResourceTier, node.DangerTier);
                if (definitionIcon != null)
                    return definitionIcon;
            }

            return node.Icon;
        }

        private bool TryResolveBoundNodeIconSpec(
            MapGraphNodeDefinition node,
            out MapGraphNodeIconKind iconKind,
            out MapGraphResourceTier resourceTier,
            out MapGraphDangerTier dangerTier)
        {
            iconKind = node.IconKind;
            resourceTier = node.ResourceTier;
            dangerTier = node.DangerTier;

            if (_bindingAuthoring == null ||
                !_bindingAuthoring.TryResolveTargetForNodeId(
                    node.NodeId,
                    out GameplayTargetAuthoringBase target))
            {
                return false;
            }

            if (target is ResourceClusterAuthoring resourceCluster &&
                resourceCluster.TryResolveResourceTier(out global::SceneResourceTier sceneResourceTier))
            {
                iconKind = MapGraphNodeIconKind.Resource;
                resourceTier = MapSceneResourceTier(sceneResourceTier);
                dangerTier = MapGraphDangerTier.None;
                return true;
            }

            if (target is EnemySourceClusterAuthoring enemySourceCluster)
            {
                iconKind = MapSceneEnemySourceIconKind(enemySourceCluster.IconKind, node.IconKind);
                resourceTier = MapGraphResourceTier.None;
                dangerTier = MapSceneEnemyDangerTier(enemySourceCluster.DangerTier, node.DangerTier);
                return true;
            }

            return false;
        }

        private void LogNodeIconSpecMismatchIfNeeded(MapGraphNodeDefinition node)
        {
            if (node == null ||
                string.IsNullOrWhiteSpace(node.NodeId) ||
                _loggedIconSpecMismatchNodeIds.Contains(node.NodeId) ||
                !TryResolveBoundNodeIconSpec(
                    node,
                    out MapGraphNodeIconKind boundIconKind,
                    out MapGraphResourceTier boundResourceTier,
                    out MapGraphDangerTier boundDangerTier) ||
                !HasNodeIconSpecMismatch(node, boundIconKind, boundResourceTier, boundDangerTier))
            {
                return;
            }

            string targetText = "bound scene target";
            if (_bindingAuthoring != null &&
                _bindingAuthoring.TryResolveTargetForNodeId(
                    node.NodeId,
                    out GameplayTargetAuthoringBase target) &&
                target != null)
            {
                targetText = $"{target.DisplayName} ({target.TargetId})";
            }

            Debug.LogWarning(
                $"Map graph node icon spec mismatch: node={node.NodeId}, target={targetText}, " +
                $"definition=({node.IconKind}, {node.ResourceTier}, {node.DangerTier}), " +
                $"bound=({boundIconKind}, {boundResourceTier}, {boundDangerTier}). " +
                "Runtime icon uses the bound scene target spec.",
                this);
            _loggedIconSpecMismatchNodeIds.Add(node.NodeId);
        }

        private static bool HasNodeIconSpecMismatch(
            MapGraphNodeDefinition node,
            MapGraphNodeIconKind boundIconKind,
            MapGraphResourceTier boundResourceTier,
            MapGraphDangerTier boundDangerTier)
        {
            if (node.IconKind != MapGraphNodeIconKind.None &&
                boundIconKind != MapGraphNodeIconKind.None &&
                node.IconKind != boundIconKind)
            {
                return true;
            }

            if (node.ResourceTier != MapGraphResourceTier.None &&
                boundResourceTier != MapGraphResourceTier.None &&
                node.ResourceTier != boundResourceTier)
            {
                return true;
            }

            return node.DangerTier != MapGraphDangerTier.None &&
                   boundDangerTier != MapGraphDangerTier.None &&
                   node.DangerTier != boundDangerTier;
        }

        private static MapGraphResourceTier MapSceneResourceTier(global::SceneResourceTier resourceTier)
        {
            switch (resourceTier)
            {
                case global::SceneResourceTier.Low:
                    return MapGraphResourceTier.Low;
                case global::SceneResourceTier.Medium:
                    return MapGraphResourceTier.Medium;
                case global::SceneResourceTier.High:
                    return MapGraphResourceTier.High;
                default:
                    return MapGraphResourceTier.None;
            }
        }

        private static MapGraphNodeIconKind MapSceneEnemySourceIconKind(
            SceneEnemySourceIconKind iconKind,
            MapGraphNodeIconKind fallbackIconKind)
        {
            switch (iconKind)
            {
                case SceneEnemySourceIconKind.Enemy:
                    return MapGraphNodeIconKind.Enemy;
                case SceneEnemySourceIconKind.Boss:
                    return MapGraphNodeIconKind.Boss;
                default:
                    if (fallbackIconKind == MapGraphNodeIconKind.Boss)
                        return MapGraphNodeIconKind.Boss;

                    return MapGraphNodeIconKind.Enemy;
            }
        }

        private static MapGraphDangerTier MapSceneEnemyDangerTier(
            SceneEnemyDangerTier dangerTier,
            MapGraphDangerTier fallbackDangerTier)
        {
            switch (dangerTier)
            {
                case SceneEnemyDangerTier.Low:
                    return MapGraphDangerTier.Low;
                case SceneEnemyDangerTier.Medium:
                    return MapGraphDangerTier.Medium;
                case SceneEnemyDangerTier.High:
                    return MapGraphDangerTier.High;
                default:
                    return fallbackDangerTier != MapGraphDangerTier.None
                        ? fallbackDangerTier
                        : MapGraphDangerTier.Low;
            }
        }

        private static Color ResolveNodeColor(MapGraphNodeKind nodeKind)
        {
            switch (nodeKind)
            {
                case MapGraphNodeKind.Start:
                    return new Color(0.33f, 0.72f, 1f, 0.95f);
                case MapGraphNodeKind.Resource:
                    return new Color(0.28f, 0.86f, 0.56f, 0.95f);
                case MapGraphNodeKind.EnemySource:
                    return new Color(0.97f, 0.62f, 0.24f, 0.95f);
                case MapGraphNodeKind.ActiveEnemy:
                    return new Color(0.95f, 0.28f, 0.26f, 0.95f);
                case MapGraphNodeKind.Extraction:
                    return new Color(0.96f, 0.84f, 0.28f, 0.95f);
                default:
                    return new Color(0.68f, 0.72f, 0.8f, 0.95f);
            }
        }

        private static void ClearRoot(RectTransform root)
        {
            if (root == null)
                return;

            for (int index = root.childCount - 1; index >= 0; index--)
                DestroyViewObject(root.GetChild(index).gameObject);
        }

        private static void DestroyViewObject(GameObject viewObject)
        {
            if (viewObject == null)
                return;

            if (Application.isPlaying)
                Destroy(viewObject);
            else
                DestroyImmediate(viewObject);
        }
    }
}

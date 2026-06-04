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
        [SerializeField] private RectTransform _edgesRoot;
        [SerializeField] private RectTransform _nodesRoot;
        [SerializeField] private RectTransform _agentsRoot;

        [Header("View Prefabs")]
        [SerializeField] private MapGraphNodeView _nodeViewPrefab;
        [SerializeField] private MapGraphEdgeView _edgeViewPrefab;
        [SerializeField] private MapGraphAgentView _agentViewPrefab;

        [Header("Layout")]
        [SerializeField] private Vector2 _mapScale = new Vector2(48f, 48f);
        [SerializeField] private Vector2 _mapOffset;
        [SerializeField] private float _edgeWidth = 4f;
        [SerializeField] private Color _edgeColor = new Color(0.38f, 0.48f, 0.58f, 0.58f);

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

        private CanvasGroup _canvasGroup;
        private MapGraphService _graphService;
        private bool _isInitialized;
        private bool _isVisible;

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

            _graphService = new MapGraphService(_mapDefinition);
            if (!_graphService.IsValid)
                return;

            if (_projectionController == null)
                _projectionController = GetComponent<AgentGraphProjectionController>();
            if (_projectionController == null)
                _projectionController = gameObject.AddComponent<AgentGraphProjectionController>();

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

            IReadOnlyList<MapGraphEdgeDefinition> edges = _mapDefinition.Edges;
            for (int index = 0; index < edges.Count; index++)
            {
                MapGraphEdgeDefinition edge = edges[index];
                if (edge == null)
                    continue;

                Vector2 from = ToAnchoredPosition(_graphService.GetNodePosition(edge.FromNodeId));
                Vector2 to = ToAnchoredPosition(_graphService.GetNodePosition(edge.ToNodeId));
                MapGraphEdgeView edgeView = CreateEdgeView(_edgesRoot);
                edgeView.Initialize(edge.EdgeId, from, to, _edgeWidth, _edgeColor);
                _edgeViewsById[edge.EdgeId] = edgeView;
            }

            IReadOnlyList<MapGraphNodeDefinition> nodes = _mapDefinition.Nodes;
            for (int index = 0; index < nodes.Count; index++)
            {
                MapGraphNodeDefinition node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
                    continue;

                MapGraphNodeView nodeView = CreateNodeView(_nodesRoot);
                nodeView.RectTransform.anchoredPosition = ToAnchoredPosition(node.Position);
                nodeView.Initialize(node, ResolveNodeColor(node.NodeKind));
                _nodeViewsById[node.NodeId] = nodeView;
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
                    agentView.Initialize(state.AgentId);
                    _agentViewsById[state.AgentId] = agentView;
                }

                agentView.Refresh(state, ToAnchoredPosition(state.GraphPosition));
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

        private void EnsureRootReferences()
        {
            if (_overlayRoot == null)
                _overlayRoot = GetComponent<RectTransform>();

            if (_canvasGroup == null)
                _canvasGroup = _overlayRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();

            EnsureChildRoot(ref _edgesRoot, "EdgesRoot");
            EnsureChildRoot(ref _nodesRoot, "NodesRoot");
            EnsureChildRoot(ref _agentsRoot, "AgentsRoot");
        }

        private void EnsureChildRoot(ref RectTransform root, string rootName)
        {
            if (root != null)
                return;

            Transform existingRoot = _overlayRoot.Find(rootName);
            if (existingRoot != null)
            {
                root = existingRoot as RectTransform;
                if (root != null)
                    return;
            }

            GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
            rootObject.transform.SetParent(_overlayRoot, false);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
        }

        private MapGraphNodeView CreateNodeView(RectTransform parent)
        {
            if (_nodeViewPrefab != null)
                return Instantiate(_nodeViewPrefab, parent);

            GameObject nodeObject = new GameObject(
                "MapGraphNodeView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MapGraphNodeView));
            nodeObject.transform.SetParent(parent, false);
            return nodeObject.GetComponent<MapGraphNodeView>();
        }

        private MapGraphEdgeView CreateEdgeView(RectTransform parent)
        {
            if (_edgeViewPrefab != null)
                return Instantiate(_edgeViewPrefab, parent);

            GameObject edgeObject = new GameObject(
                "MapGraphEdgeView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MapGraphEdgeView));
            edgeObject.transform.SetParent(parent, false);
            return edgeObject.GetComponent<MapGraphEdgeView>();
        }

        private MapGraphAgentView CreateAgentView(RectTransform parent)
        {
            if (_agentViewPrefab != null)
                return Instantiate(_agentViewPrefab, parent);

            GameObject agentObject = new GameObject(
                "MapGraphAgentView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(MapGraphAgentView));
            agentObject.transform.SetParent(parent, false);
            return agentObject.GetComponent<MapGraphAgentView>();
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

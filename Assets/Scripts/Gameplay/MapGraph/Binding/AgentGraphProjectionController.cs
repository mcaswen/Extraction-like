using System.Collections.Generic;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>
    /// 将实时 Agent 运行状态投影到抽象图上
    /// 数据源来自 AgentRuntimeRegistry 和 Agent 黑板，移动表现只作用于图 UI，不反向驱动真实 Agent
    /// </summary>
    public sealed class AgentGraphProjectionController : MonoBehaviour
    {
        [Header("Binding")]
        [SerializeField] private MapGraphBindingAuthoring _bindingAuthoring;

        [Header("Projection")]
        [SerializeField] private float _graphUnitsPerSecond = 2.6f;
        [SerializeField] private float _worldNodeSnapDistance = 2.5f;
        [SerializeField] private bool _snapToBoundTargetByWorldPosition = true;
        [SerializeField] private bool _driveEdgeProgressByWorldDistance = true;
        [SerializeField] private float _worldEdgeArrivalDistance = 1.2f;
        [SerializeField] private Color _defaultAgentColor = new Color(0.26f, 0.72f, 1f, 1f);

        private readonly MapGraphRuntimeState _runtimeState = new MapGraphRuntimeState();
        private readonly HashSet<string> _activeAgentIds = new HashSet<string>();

        private MapGraphService _graphService;
        private MapGraphPathfindingService _pathfindingService;
        private SO_MapGraphDefinition _mapDefinition;

        /// <summary>
        /// 当前全部 Agent 的图上投影状态
        /// </summary>
        public IReadOnlyList<MapGraphAgentRuntimeState> AgentStates => _runtimeState.AgentStates;

        /// <summary>
        /// 初始化投影控制器
        /// </summary>
        /// <param name="mapDefinition"></param>
        /// <param name="bindingAuthoring"></param>
        public void Initialize(
            SO_MapGraphDefinition mapDefinition,
            MapGraphBindingAuthoring bindingAuthoring)
        {
            _mapDefinition = mapDefinition;
            if (bindingAuthoring != null)
                _bindingAuthoring = bindingAuthoring;

            _graphService = _mapDefinition != null ? new MapGraphService(_mapDefinition) : null;
            _pathfindingService = _graphService != null ? new MapGraphPathfindingService(_graphService) : null;
            _runtimeState.Clear();
        }

        /// <summary>
        /// 推进实时 Agent 到抽象图的投影
        /// </summary>
        /// <param name="deltaTime"></param>
        public void Tick(float deltaTime)
        {
            EnsureInitializedFromBinding();

            if (_graphService == null || !_graphService.IsValid)
            {
                _runtimeState.Clear();
                return;
            }

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null)
            {
                _runtimeState.Clear();
                return;
            }

            _activeAgentIds.Clear();
            IReadOnlyList<AgentRuntimeHandle> handles = registry.RegisteredAgents;
            for (int index = 0; index < handles.Count; index++)
            {
                AgentRuntimeHandle handle = handles[index];
                if (!handle.IsValid || handle.ReadOnly == null)
                    continue;

                string agentId = handle.AgentId.Value;
                _activeAgentIds.Add(agentId);
                MapGraphAgentRuntimeState state = _runtimeState.GetOrCreateAgentState(agentId);

                RefreshAgentIdentity(state, handle.ReadOnly);
                EnsureAgentHasGraphPosition(state, handle.ReadOnly);
                RefreshAgentTargetPath(state, handle.ReadOnly);
                TrySnapAgentToRealtimeNode(state, handle.ReadOnly);
                AdvanceGraphMovement(state, handle.ReadOnly, Mathf.Max(0f, deltaTime));
            }

            _runtimeState.RemoveAgentsExcept(_activeAgentIds);
        }

        private void EnsureInitializedFromBinding()
        {
            if (_mapDefinition != null || _bindingAuthoring == null || _bindingAuthoring.MapDefinition == null)
                return;

            Initialize(_bindingAuthoring.MapDefinition, _bindingAuthoring);
        }

        private void RefreshAgentIdentity(MapGraphAgentRuntimeState state, IAgentReadOnly agent)
        {
            state.DisplayName = string.IsNullOrWhiteSpace(agent.AgentIdValue)
                ? "Agent"
                : agent.AgentIdValue;
            state.AgentColor = ResolveStableAgentColor(agent.AgentIdValue);
        }

        private void EnsureAgentHasGraphPosition(MapGraphAgentRuntimeState state, IAgentReadOnly agent)
        {
            if (state.IsOnEdge || !string.IsNullOrWhiteSpace(state.CurrentNodeId))
                return;

            if (_bindingAuthoring != null &&
                _bindingAuthoring.TryFindNearestBoundNode(
                    agent.Position,
                    Mathf.Max(0f, _worldNodeSnapDistance),
                    out string nearestNodeId))
            {
                SnapAgentToNode(state, nearestNodeId);
                return;
            }

            string startNodeId = _graphService.GetStartNodeId();
            if (!string.IsNullOrWhiteSpace(startNodeId))
                SnapAgentToNode(state, startNodeId);
        }

        private void RefreshAgentTargetPath(MapGraphAgentRuntimeState state, IAgentReadOnly agent)
        {
            if (!TryResolveTargetNode(agent, out string targetNodeId))
                return;

            if (targetNodeId == state.CurrentTargetNodeId && state.IsOnEdge)
                return;

            if (targetNodeId == state.CurrentTargetNodeId && state.RemainingPathNodeIds.Count > 0)
            {
                BeginNextMovementSegment(state);
                return;
            }

            string startNodeId = ResolvePathStartNode(state);
            if (string.IsNullOrWhiteSpace(startNodeId))
                return;

            MapGraphResolvedPathPlan pathPlan = _pathfindingService.ResolveFromNode(startNodeId, targetNodeId);
            if (!pathPlan.IsValid)
                return;

            ApplyPathPlan(state, pathPlan);
        }

        // 目标节点优先来自 Agent 指令，其次才用 NavMesh 目的地做兜底投影
        private bool TryResolveTargetNode(IAgentReadOnly agent, out string nodeId)
        {
            nodeId = string.Empty;

            if (_bindingAuthoring == null || agent == null)
                return false;

            if (TryResolveTargetNodeFromBlackboard(agent, out nodeId))
                return true;

            return TryResolveTargetNodeFromNavMesh(agent, out nodeId);
        }

        // 黑板指令里已经包含业务目标 ID 时，直接复用目标系统的稳定绑定
        private bool TryResolveTargetNodeFromBlackboard(IAgentReadOnly agent, out string nodeId)
        {
            nodeId = string.Empty;
            if (agent.Blackboard == null ||
                !agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest))
            {
                return false;
            }

            AgentTargetRef targetRef = directiveRequest.TargetRef;
            if (targetRef.Kind == AgentTargetKind.Enemy &&
                targetRef.TargetObject != null &&
                TryResolveEnemySourceNodeFromTargetObject(
                    targetRef.TargetObject,
                    targetRef.HasTargetPosition ? targetRef.TargetPosition : agent.Position,
                    out nodeId))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(directiveRequest.TargetId) &&
                _bindingAuthoring.TryGetNodeIdForTargetId(directiveRequest.TargetId, out nodeId))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(targetRef.TargetId) &&
                _bindingAuthoring.TryGetNodeIdForTargetId(targetRef.TargetId, out nodeId))
            {
                return true;
            }

            if (targetRef.TargetObject != null &&
                _bindingAuthoring.TryGetNodeIdForTargetObject(targetRef.TargetObject, out nodeId))
            {
                return true;
            }

            if (targetRef.HasTargetPosition)
            {
                return _bindingAuthoring.TryFindNearestBoundNode(
                    targetRef.TargetPosition,
                    float.MaxValue,
                    out nodeId);
            }

            return false;
        }

        // 没有待处理指令时，用当前导航目的地推断最接近的图节点，保证显示层不中断
        private bool TryResolveTargetNodeFromNavMesh(IAgentReadOnly agent, out string nodeId)
        {
            nodeId = string.Empty;
            NavMeshAgent navMeshAgent = agent.NavMeshAgent;
            if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.hasPath)
                return false;

            return _bindingAuthoring.TryFindNearestBoundNode(
                navMeshAgent.destination,
                float.MaxValue,
                out nodeId);
        }

        // 将寻路结果写入图上状态，不影响真实 Agent 的 NavMesh 路径
        private void ApplyPathPlan(
            MapGraphAgentRuntimeState state,
            MapGraphResolvedPathPlan pathPlan)
        {
            state.CurrentTargetNodeId = pathPlan.TargetNodeId;
            state.RemainingPathNodeIds.Clear();
            state.RemainingPathNodeIds.AddRange(pathPlan.RemainingNodeIds);
            state.ClearEdgeTravel();

            if (state.CurrentNodeId == state.CurrentTargetNodeId)
            {
                state.RemainingPathNodeIds.Clear();
                return;
            }

            BeginNextMovementSegment(state);
        }

        // 把下一段节点路径转换成边上移动状态，复用旧原型里“节点到边”的表现思路
        private bool BeginNextMovementSegment(MapGraphAgentRuntimeState state)
        {
            if (string.IsNullOrWhiteSpace(state.CurrentNodeId) || state.RemainingPathNodeIds.Count == 0)
                return false;

            string nextNodeId = state.RemainingPathNodeIds[0];
            if (!_graphService.TryGetEdgeBetween(
                    state.CurrentNodeId,
                    nextNodeId,
                    out MapGraphEdgeDefinition edgeDefinition))
            {
                return false;
            }

            state.CurrentEdgeId = edgeDefinition.EdgeId;
            state.CurrentEdgeFromNodeId = edgeDefinition.FromNodeId;
            state.CurrentEdgeToNodeId = edgeDefinition.ToNodeId;
            state.CurrentEdgeLengthUnits = edgeDefinition.LengthUnits;

            if (edgeDefinition.FromNodeId == state.CurrentNodeId)
            {
                state.CurrentEdgeProgress01 = 0f;
                state.CurrentEdgeSegmentStartProgress01 = 0f;
                state.CurrentEdgeTargetProgress01 = 1f;
            }
            else
            {
                state.CurrentEdgeProgress01 = 1f;
                state.CurrentEdgeSegmentStartProgress01 = 1f;
                state.CurrentEdgeTargetProgress01 = 0f;
            }

            state.GraphPosition = _graphService.GetPositionOnEdge(
                edgeDefinition,
                state.CurrentEdgeProgress01);
            state.CurrentNodeId = string.Empty;
            return true;
        }

        // UI 棋子沿抽象边推进，只表达图上状态，不参与真实空间移动
        private void AdvanceGraphMovement(
            MapGraphAgentRuntimeState state,
            IAgentReadOnly agent,
            float deltaTime)
        {
            if (!state.IsOnEdge)
                return;

            if (_driveEdgeProgressByWorldDistance &&
                TryAdvanceGraphMovementByWorldDistance(state, agent))
            {
                return;
            }

            AdvanceGraphMovementByTime(state, deltaTime);
        }

        // 用真实世界中 Agent 到下一节点的距离换算图边进度，让抽象图位置跟 NavMesh 结果同步
        private bool TryAdvanceGraphMovementByWorldDistance(
            MapGraphAgentRuntimeState state,
            IAgentReadOnly agent)
        {
            if (agent == null || _bindingAuthoring == null)
                return false;

            if (!_graphService.TryGetEdge(state.CurrentEdgeId, out MapGraphEdgeDefinition edgeDefinition))
                return false;

            string startNodeId = ResolveCurrentEdgeStartNodeId(state);
            string targetNodeId = ResolveCurrentEdgeTargetNodeId(state);
            if (string.IsNullOrWhiteSpace(startNodeId) || string.IsNullOrWhiteSpace(targetNodeId))
                return false;

            if (!_bindingAuthoring.TryGetWorldPositionForNodeId(startNodeId, out Vector3 startWorldPosition) ||
                !_bindingAuthoring.TryGetWorldPositionForNodeId(targetNodeId, out Vector3 targetWorldPosition))
            {
                return false;
            }

            float totalDistance = GetPlanarDistance(startWorldPosition, targetWorldPosition);
            if (totalDistance <= Mathf.Epsilon)
                return false;

            float distanceToTarget = GetPlanarDistance(agent.Position, targetWorldPosition);
            float progressTowardTarget = Mathf.Clamp01(1f - (distanceToTarget / totalDistance));
            state.CurrentEdgeProgress01 = Mathf.Lerp(
                state.CurrentEdgeSegmentStartProgress01,
                state.CurrentEdgeTargetProgress01,
                progressTowardTarget);
            state.GraphPosition = _graphService.GetPositionOnEdge(edgeDefinition, state.CurrentEdgeProgress01);

            if (distanceToTarget <= Mathf.Max(0.05f, _worldEdgeArrivalDistance) ||
                progressTowardTarget >= 0.999f)
            {
                CompleteEdgeArrival(state, edgeDefinition);
            }

            return true;
        }

        // 找不到节点世界坐标时，保留旧的匀速图上推进兜底
        private void AdvanceGraphMovementByTime(MapGraphAgentRuntimeState state, float deltaTime)
        {
            if (!state.IsOnEdge)
                return;

            if (!_graphService.TryGetEdge(state.CurrentEdgeId, out MapGraphEdgeDefinition edgeDefinition))
            {
                state.ClearEdgeTravel();
                return;
            }

            float edgeLength = Mathf.Max(0.1f, edgeDefinition.LengthUnits);
            float direction = Mathf.Sign(state.CurrentEdgeTargetProgress01 - state.CurrentEdgeProgress01);
            float deltaProgress = _graphUnitsPerSecond * deltaTime / edgeLength;
            state.CurrentEdgeProgress01 = Mathf.Clamp01(
                state.CurrentEdgeProgress01 + direction * deltaProgress);
            state.GraphPosition = _graphService.GetPositionOnEdge(edgeDefinition, state.CurrentEdgeProgress01);

            bool reachedTarget = direction >= 0f
                ? state.CurrentEdgeProgress01 >= state.CurrentEdgeTargetProgress01
                : state.CurrentEdgeProgress01 <= state.CurrentEdgeTargetProgress01;

            if (!reachedTarget)
                return;

            CompleteEdgeArrival(state, edgeDefinition);
        }

        private void CompleteEdgeArrival(
            MapGraphAgentRuntimeState state,
            MapGraphEdgeDefinition edgeDefinition)
        {
            string arrivedNodeId = state.CurrentEdgeTargetProgress01 >= 0.5f
                ? edgeDefinition.ToNodeId
                : edgeDefinition.FromNodeId;
            string previousNodeId = arrivedNodeId == edgeDefinition.FromNodeId
                ? edgeDefinition.ToNodeId
                : edgeDefinition.FromNodeId;

            state.PreviousNodeId = previousNodeId;
            state.CurrentNodeId = arrivedNodeId;
            state.GraphPosition = _graphService.GetNodePosition(arrivedNodeId);
            state.ClearEdgeTravel();

            if (state.RemainingPathNodeIds.Count > 0 && state.RemainingPathNodeIds[0] == arrivedNodeId)
                state.RemainingPathNodeIds.RemoveAt(0);

            if (state.RemainingPathNodeIds.Count > 0)
                BeginNextMovementSegment(state);
        }

        // 真实 Agent 已经靠近某个绑定目标时，把图上位置吸附到对应节点，避免 UI 与实际结果长期漂移
        private void TrySnapAgentToRealtimeNode(MapGraphAgentRuntimeState state, IAgentReadOnly agent)
        {
            if (!_snapToBoundTargetByWorldPosition || _bindingAuthoring == null)
                return;

            if (!_bindingAuthoring.TryFindNearestBoundNode(
                    agent.Position,
                    Mathf.Max(0f, _worldNodeSnapDistance),
                    out string snappedNodeId))
            {
                return;
            }

            if (state.IsOnEdge &&
                snappedNodeId != state.CurrentTargetNodeId &&
                snappedNodeId != ResolveCurrentEdgeTargetNodeId(state))
            {
                return;
            }

            SnapAgentToNode(state, snappedNodeId);
        }

        private void SnapAgentToNode(MapGraphAgentRuntimeState state, string nodeId)
        {
            if (!_graphService.TryGetNode(nodeId, out _))
                return;

            state.CurrentNodeId = nodeId;
            state.GraphPosition = _graphService.GetNodePosition(nodeId);
            state.ClearEdgeTravel();

            if (state.RemainingPathNodeIds.Count > 0 && state.RemainingPathNodeIds[0] == nodeId)
                state.RemainingPathNodeIds.RemoveAt(0);
        }

        private string ResolvePathStartNode(MapGraphAgentRuntimeState state)
        {
            if (!state.IsOnEdge)
                return state.CurrentNodeId;

            return state.CurrentEdgeProgress01 >= 0.5f
                ? state.CurrentEdgeToNodeId
                : state.CurrentEdgeFromNodeId;
        }

        private static string ResolveCurrentEdgeTargetNodeId(MapGraphAgentRuntimeState state)
        {
            return state.CurrentEdgeTargetProgress01 >= 0.5f
                ? state.CurrentEdgeToNodeId
                : state.CurrentEdgeFromNodeId;
        }

        private static string ResolveCurrentEdgeStartNodeId(MapGraphAgentRuntimeState state)
        {
            return state.CurrentEdgeSegmentStartProgress01 >= 0.5f
                ? state.CurrentEdgeToNodeId
                : state.CurrentEdgeFromNodeId;
        }

        private bool TryResolveEnemySourceNodeFromTargetObject(
            GameObject targetObject,
            Vector3 lookupPosition,
            out string nodeId)
        {
            nodeId = string.Empty;
            if (targetObject == null || _bindingAuthoring == null)
                return false;

            GameplayTargetRegistry registry = GameplayTargetRegistry.ActiveInstance;
            if (registry == null)
                return false;

            if (TryGetComponentFromTargetObject(targetObject, out global::EnemyHealthController enemy) &&
                registry.TryFindEnemySourceTargetIdByEnemy(enemy, out string sourceTargetId) &&
                _bindingAuthoring.TryGetNodeIdForTargetId(sourceTargetId, out nodeId))
            {
                return true;
            }

            if (TryGetComponentFromTargetObject(targetObject, out ActiveEnemyClusterAuthoring activeCluster) &&
                registry.TryFindEnemySourceClusterByActiveEnemyCluster(
                    activeCluster,
                    lookupPosition,
                    out EnemySourceClusterAuthoring sourceCluster) &&
                _bindingAuthoring.TryGetNodeIdForTargetId(sourceCluster.TargetId, out nodeId))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetComponentFromTargetObject<TComponent>(
            GameObject targetObject,
            out TComponent component)
            where TComponent : Component
        {
            component = null;
            if (targetObject == null)
                return false;

            component = targetObject.GetComponent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInParent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInChildren<TComponent>();
            return component != null;
        }

        private static float GetPlanarDistance(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return Mathf.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        }

        private Color ResolveStableAgentColor(string agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return _defaultAgentColor;

            int hash = 17;
            for (int index = 0; index < agentId.Length; index++)
                hash = (hash * 31) + agentId[index];

            float hue = Mathf.Abs(hash % 360) / 360f;
            Color color = Color.HSVToRGB(hue, 0.64f, 0.96f);
            color.a = 1f;
            return color;
        }
    }
}

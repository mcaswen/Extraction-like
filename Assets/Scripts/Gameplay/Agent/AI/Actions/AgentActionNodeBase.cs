using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// Agent 行为树执行节点基类
    /// 集中处理 Agent 上下文、目标解析、移动和指令清理等通用逻辑
    /// </summary>
    public abstract class AgentActionNodeBase : ActionNode
    {
        internal const float NavMeshDestinationRefreshInterval = 0.1f;
        internal const float NavMeshTargetSampleRadius = 4f;
        private const float NavMeshStopFromMaxSpeedDuration = 0.5f;

        private float _lastNavMeshDestinationSetTime = -999f;
        private bool _hasLastNavMeshDestination;

        /// <summary>
        /// 创建 Agent 行为节点基类
        /// </summary>
        /// <param name="nodeName"></param>
        protected AgentActionNodeBase(string nodeName)
            : base(nodeName)
        {
        }

        /// <summary>
        /// 从行为树上下文中取得当前 Agent 只读接口
        /// </summary>
        /// <param name="context"></param>
        /// <param name="agent"></param>
        /// <returns></returns>
        protected bool TryGetAgent(BehaviorTreeContext context, out IAgentReadOnly agent)
        {
            // UserContext 由 AgentBrainController 注入，节点不反查场景对象
            agent = context.UserContext as IAgentReadOnly;
            return agent != null && agent.CachedTransform != null && !agent.IsDead;
        }

        /// <summary>
        /// 从黑板中读取指定类型的待处理指令
        /// 类型不匹配时不消费指令，避免节点误处理其他目标
        /// </summary>
        /// <param name="context"></param>
        /// <param name="directiveType"></param>
        /// <param name="directiveRequest"></param>
        /// <returns></returns>
        protected bool TryGetDirective(
            BehaviorTreeContext context,
            AgentDirectiveType directiveType,
            out AgentDirectiveRequest directiveRequest)
        {
            if (!context.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out directiveRequest))
            {
                return false;
            }

            // 不消费类型不匹配的指令，避免节点误处理其他目标
            return directiveRequest.DirectiveType == directiveType;
        }

        /// <summary>
        /// 将 AgentTargetRef 解析为可移动的世界坐标
        /// 具体对象优先使用实时 Transform，抽象点使用保存的位置
        /// </summary>
        /// <param name="targetRef"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        protected bool TryResolveTargetPosition(AgentTargetRef targetRef, out Vector3 targetPosition)
        {
            if (!targetRef.IsValid)
            {
                targetPosition = default;
                return false;
            }

            if (targetRef.TargetObject != null)
            {
                if (targetRef.TargetObject.TryGetComponent(
                        out GameplayTargetClusterAuthoringBase clusterTarget))
                {
                    targetPosition = clusterTarget.CenterPosition;
                    return true;
                }

                // 具体对象优先，抽象点位可能是旧快照
                targetPosition = targetRef.TargetObject.transform.position;
                return true;
            }

            if (targetRef.HasTargetPosition)
            {
                targetPosition = targetRef.TargetPosition;
                return true;
            }

            targetPosition = default;
            return false;
        }

        /// <summary>
        /// 解析交互类目标的停靠点
        /// 优先取目标碰撞体上离 Agent 最近的位置，避免 Agent 挤向箱子或掉落物中心
        /// </summary>
        /// <param name="targetRef"></param>
        /// <param name="agentPosition"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        protected bool TryResolveInteractionTargetPosition(
            AgentTargetRef targetRef,
            Vector3 agentPosition,
            out Vector3 targetPosition)
        {
            if (!TryResolveTargetPosition(targetRef, out targetPosition))
                return false;

            GameObject targetObject = targetRef.TargetObject;
            if (targetObject == null)
                return true;

            if (targetObject.TryGetComponent(out GameplayTargetClusterAuthoringBase _))
                return true;

            Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
            if (colliders == null || colliders.Length <= 0)
                return true;

            bool hasClosestPoint = false;
            Vector3 closestTargetPoint = targetPosition;
            float closestDistanceSqr = float.MaxValue;

            for (int index = 0; index < colliders.Length; index++)
            {
                Collider targetCollider = colliders[index];
                if (targetCollider == null ||
                    !targetCollider.enabled ||
                    !targetCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 closestPoint = targetCollider.ClosestPoint(agentPosition);
                float distanceSqr = GetPlanarDistanceSqr(agentPosition, closestPoint);
                if (distanceSqr >= closestDistanceSqr)
                    continue;

                hasClosestPoint = true;
                closestTargetPoint = closestPoint;
                closestDistanceSqr = distanceSqr;
            }

            if (hasClosestPoint)
                targetPosition = closestTargetPoint;

            return true;
        }

        /// <summary>
        /// 解析资源目标的实际停靠点
        /// 资源群不再使用群中心，直接选群内离 Agent 最近的未完成资源。
        /// </summary>
        /// <param name="targetRef"></param>
        /// <param name="agentPosition"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        protected bool TryResolveResourceNavigationTargetPosition(
            AgentTargetRef targetRef,
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            out Vector3 targetPosition)
        {
            GameObject targetObject = targetRef.TargetObject;
            if (targetObject != null &&
                targetObject.TryGetComponent(out ResourceClusterAuthoring resourceCluster) &&
                resourceCluster.TryGetNearestReachableIncompleteResource(agentPosition, navMeshAgent, out GameObject resourceObject) &&
                resourceObject != null)
            {
                AgentTargetRef resourceTargetRef = AgentTargetRef.FromConcreteObject(
                    AgentTargetKind.Resource,
                    resourceObject,
                    resourceObject.name);

                return TryResolveInteractionTargetPosition(
                    resourceTargetRef,
                    agentPosition,
                    out targetPosition);
            }

            return TryResolveInteractionTargetPosition(
                targetRef,
                agentPosition,
                out targetPosition);
        }

        /// <summary>
        /// 从目标引用中查找指定组件
        /// 会兼容组件挂在父级或子级表现物体上的情况
        /// </summary>
        /// <typeparam name="TComponent"></typeparam>
        /// <param name="targetRef"></param>
        /// <param name="component"></param>
        /// <returns></returns>
        protected bool TryGetTargetComponent<TComponent>(
            AgentTargetRef targetRef,
            out TComponent component)
            where TComponent : Component
        {
            GameObject targetObject = targetRef.TargetObject;
            if (targetObject == null)
            {
                component = null;
                return false;
            }

            // 目标组件可能挂在碰撞体父级或表现子物体上
            component = targetObject.GetComponent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInParent<TComponent>();
            if (component != null)
                return true;

            component = targetObject.GetComponentInChildren<TComponent>();
            return component != null;
        }

        /// <summary>
        /// 从黑板读取 float 配置值，不存在时使用默认值
        /// </summary>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        protected float GetFloat(
            BehaviorTreeContext context,
            BlackboardKey key,
            float defaultValue)
        {
            return context.Blackboard.TryGetValue(key, out float value) ? value : defaultValue;
        }

        /// <summary>
        /// 向黑板写入当前节点产生的事实
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected void SetFact<TValue>(
            BehaviorTreeContext context,
            BlackboardKey key,
            TValue value)
        {
            context.Blackboard.SetValue(key, value, context.TimeSeconds);
        }

        /// <summary>
        /// 清理当前黑板中的待处理指令
        /// </summary>
        /// <param name="context"></param>
        protected void ClearPendingDirective(BehaviorTreeContext context)
        {
            // 行为完成后同时清标记和值，避免状态机继续吃旧指令
            context.Blackboard.SetValue(
                AgentBlackboardKeys.HasPendingDirective,
                false,
                context.TimeSeconds);

            context.Blackboard.RemoveValue(
                AgentBlackboardKeys.PendingDirectiveRequest,
                context.TimeSeconds);
        }

        /// <summary>
        /// 驱动 Agent 靠近目标位置
        /// 优先使用 NavMesh，导航不可用时退回直线移动兜底
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="targetPosition"></param>
        /// <param name="stoppingDistance"></param>
        /// <param name="moveSpeed"></param>
        /// <param name="deltaTime"></param>
        /// <returns></returns>
        protected bool MoveAgentTowards(
            IAgentReadOnly agent,
            Vector3 targetPosition,
            float stoppingDistance,
            float moveSpeed,
            float deltaTime)
        {
            if (TryMoveAgentWithNavMesh(
                    agent,
                    targetPosition,
                    stoppingDistance,
                    moveSpeed,
                    out bool hasReachedByNavMesh))
            {
                return hasReachedByNavMesh;
            }

            return MoveAgentDirectly(
                agent,
                targetPosition,
                stoppingDistance,
                moveSpeed,
                deltaTime);
        }

        /// <summary>
        /// 立即停止 Agent 移动
        /// 清掉 NavMesh 路径和残余速度，避免等待交互阶段继续滑动
        /// </summary>
        /// <param name="agent"></param>
        protected void StopAgentMovement(IAgentReadOnly agent)
        {
            StopNavMeshAgent(agent?.NavMeshAgent);
            ResetNavMeshDestinationCache();
        }

        private bool TryMoveAgentWithNavMesh(
            IAgentReadOnly agent,
            Vector3 targetPosition,
            float stoppingDistance,
            float moveSpeed,
            out bool hasReached)
        {
            hasReached = false;

            NavMeshAgent navMeshAgent = agent.NavMeshAgent;
            if (navMeshAgent == null || !navMeshAgent.enabled)
                return false;

            if (!EnsureNavMeshAgentReady(navMeshAgent, agent.CachedTransform.position, stoppingDistance))
                return false;

            ConfigureNavMeshAgent(navMeshAgent, stoppingDistance, moveSpeed);

            if (!TrySampleNavMeshTarget(
                    navMeshAgent,
                    targetPosition,
                    stoppingDistance,
                    out Vector3 sampledTargetPosition))
            {
                global::RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
                StopNavMeshAgent(navMeshAgent);
                return true;
            }

            if (IsWithinWorldStoppingDistance(
                    agent.CachedTransform.position,
                    sampledTargetPosition,
                    stoppingDistance))
            {
                StopNavMeshAgent(navMeshAgent);
                hasReached = true;
                return true;
            }

            navMeshAgent.isStopped = false;
            if (ShouldRefreshNavMeshDestination())
            {
                if (!navMeshAgent.SetDestination(sampledTargetPosition))
                {
                    global::RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
                    return true;
                }

                _lastNavMeshDestinationSetTime = Time.time;
                _hasLastNavMeshDestination = true;
                return true;
            }

            hasReached = HasReachedNavMeshDestination(
                navMeshAgent,
                sampledTargetPosition,
                stoppingDistance);
            if (hasReached)
            {
                StopNavMeshAgent(navMeshAgent);
                ResetNavMeshDestinationCache();
            }

            return true;
        }

        private bool ShouldRefreshNavMeshDestination()
        {
            if (!_hasLastNavMeshDestination)
                return true;

            return Time.time - _lastNavMeshDestinationSetTime >= NavMeshDestinationRefreshInterval;
        }

        private void ResetNavMeshDestinationCache()
        {
            _hasLastNavMeshDestination = false;
            _lastNavMeshDestinationSetTime = -999f;
        }

        private static bool EnsureNavMeshAgentReady(
            NavMeshAgent navMeshAgent,
            Vector3 currentPosition,
            float stoppingDistance)
        {
            if (navMeshAgent.isOnNavMesh)
                return true;

            float sampleRadius = Mathf.Max(2f, stoppingDistance, navMeshAgent.radius * 2f);
            if (NavMesh.SamplePosition(currentPosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(hit.position);
                return navMeshAgent.isOnNavMesh;
            }

            // 运行时白盒场景可能稍晚生成 NavMesh，请求重建后本帧保留兜底移动
            global::RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
            return false;
        }

        private static void ConfigureNavMeshAgent(
            NavMeshAgent navMeshAgent,
            float stoppingDistance,
            float moveSpeed)
        {
            float speed = Mathf.Max(0f, moveSpeed);
            navMeshAgent.speed = speed;
            navMeshAgent.acceleration = Mathf.Max(
                navMeshAgent.acceleration,
                speed / NavMeshStopFromMaxSpeedDuration);
            navMeshAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        }

        private static bool TrySampleNavMeshTarget(
            NavMeshAgent navMeshAgent,
            Vector3 targetPosition,
            float stoppingDistance,
            out Vector3 sampledTargetPosition)
        {
            float sampleRadius = Mathf.Max(
                NavMeshTargetSampleRadius,
                stoppingDistance,
                navMeshAgent.height,
                navMeshAgent.radius * 4f);
            if (NavMesh.SamplePosition(
                    targetPosition,
                    out NavMeshHit hit,
                    sampleRadius,
                    navMeshAgent.areaMask))
            {
                sampledTargetPosition = hit.position;
                return true;
            }

            sampledTargetPosition = default;
            return false;
        }

        private static bool HasReachedNavMeshDestination(
            NavMeshAgent navMeshAgent,
            Vector3 sampledTargetPosition,
            float stoppingDistance)
        {
            if (navMeshAgent.pathPending)
                return false;

            float effectiveStoppingDistance = Mathf.Max(
                navMeshAgent.stoppingDistance,
                stoppingDistance);

            if (navMeshAgent.hasPath)
            {
                if (navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
                    return false;

                float remainingDistance = navMeshAgent.remainingDistance;
                if (!float.IsNaN(remainingDistance) &&
                    !float.IsInfinity(remainingDistance))
                {
                    return remainingDistance <= effectiveStoppingDistance;
                }
            }

            return IsWithinWorldStoppingDistance(
                navMeshAgent.transform.position,
                sampledTargetPosition,
                effectiveStoppingDistance);
        }

        private static void StopNavMeshAgent(NavMeshAgent navMeshAgent)
        {
            if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
                return;

            navMeshAgent.isStopped = true;
            navMeshAgent.velocity = Vector3.zero;
            if (navMeshAgent.hasPath)
                navMeshAgent.ResetPath();
        }

        private static bool MoveAgentDirectly(
            IAgentReadOnly agent,
            Vector3 targetPosition,
            float stoppingDistance,
            float moveSpeed,
            float deltaTime)
        {
            Transform agentTransform = agent.CachedTransform;
            Vector3 currentPosition = agentTransform.position;
            // NavMesh 尚未准备好时保留直线兜底，避免 MVP 场景启动瞬间卡死
            Vector3 planarTargetPosition = new Vector3(
                targetPosition.x,
                currentPosition.y,
                targetPosition.z);

            Vector3 offset = planarTargetPosition - currentPosition;
            // 到达判定使用平方距离，避免每帧开方
            float stoppingDistanceSqr = Mathf.Max(0f, stoppingDistance) * Mathf.Max(0f, stoppingDistance);
            if (offset.sqrMagnitude <= stoppingDistanceSqr)
                return true;

            float stepDistance = Mathf.Max(0f, moveSpeed) * Mathf.Max(0f, deltaTime);
            if (stepDistance <= 0f)
                return false;

            agentTransform.position = Vector3.MoveTowards(
                currentPosition,
                planarTargetPosition,
                stepDistance);

            if (offset.sqrMagnitude > 0.0001f)
            {
                // 保持朝向目标，方便后续接射击或动画表现
                agentTransform.rotation = Quaternion.LookRotation(
                    offset.normalized,
                    Vector3.up);
            }

            return false;
        }

        private static bool IsWithinPlanarStoppingDistance(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float stoppingDistance)
        {
            Vector3 planarTargetPosition = new Vector3(
                targetPosition.x,
                currentPosition.y,
                targetPosition.z);
            Vector3 offset = planarTargetPosition - currentPosition;
            float stoppingDistanceSqr = Mathf.Max(0f, stoppingDistance) * Mathf.Max(0f, stoppingDistance);
            return offset.sqrMagnitude <= stoppingDistanceSqr;
        }

        private static bool IsWithinWorldStoppingDistance(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float stoppingDistance)
        {
            Vector3 offset = targetPosition - currentPosition;
            float stoppingDistanceSqr = Mathf.Max(0f, stoppingDistance) * Mathf.Max(0f, stoppingDistance);
            return offset.sqrMagnitude <= stoppingDistanceSqr;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        /// <summary>
        /// 构造缺失指定指令时的失败结果
        /// </summary>
        /// <param name="directiveType"></param>
        /// <returns></returns>
        protected BehaviorNodeResult FailMissingDirective(AgentDirectiveType directiveType)
        {
            return Fail(
                BehaviorFailureCode.MissingBlackboardValue,
                $"Missing pending directive [{directiveType}]");
        }
    }
}

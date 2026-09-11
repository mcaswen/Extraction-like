using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Navigation;
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
        protected readonly AgentResourceNavigationResolver ResourceNavigation = new AgentResourceNavigationResolver();
        internal const float NavMeshDestinationRefreshInterval = 0.1f;
        internal const float NavMeshTargetSampleRadius = 0.5f;
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
                targetObject.TryGetComponent(out ResourceClusterAuthoring resourceCluster))
            {
                if (ResourceNavigation.TryResolve(resourceCluster,
                        agentPosition,
                        navMeshAgent,
                        out GameObject resourceObject,
                        out Vector3 resourceNavigationPosition) &&
                    resourceObject != null)
                {
                    targetPosition = resourceNavigationPosition;
                    return true;
                }

                targetPosition = default;
                return false;
            }

            return TryResolveInteractionTargetPosition(
                targetRef,
                agentPosition,
                out targetPosition);
        }

        protected bool TryResolveExtractionNavigationTargetPosition(
            AgentTargetRef targetRef,
            Vector3 agentPosition,
            out Vector3 targetPosition)
        {
            GameObject targetObject = targetRef.TargetObject;
            if (targetObject != null &&
                targetObject.TryGetComponent(out ExtractionClusterAuthoring extractionCluster))
            {
                if (extractionCluster.TryGetNearestExtractionPoint(
                        agentPosition,
                        out global::ExtractionPointController extractionPoint) &&
                    extractionPoint != null)
                {
                    targetPosition = extractionPoint.transform.position;
                    return true;
                }

                targetPosition = default;
                return false;
            }

            if (targetObject != null &&
                TryGetTargetComponent(targetRef, out global::ExtractionPointController directExtractionPoint))
            {
                targetPosition = directExtractionPoint.transform.position;
                return true;
            }

            return TryResolveTargetPosition(targetRef, out targetPosition);
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
            if (context.UserContext is IAgentCommandReceiver receiver &&
                context.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest, out AgentDirectiveRequest request))
                receiver.FinishDirective(request.CommandId);
        }

        protected void FailPendingDirective(BehaviorTreeContext context, AgentDirectiveFailure failure)
        {
            if (context.UserContext is IAgentCommandReceiver receiver &&
                context.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest, out AgentDirectiveRequest request))
                receiver.FinishDirective(request.CommandId, failure);
        }

        protected bool MoveAgentTowards(IAgentReadOnly agent, Vector3 targetPosition, float stoppingDistance, float moveSpeed, float deltaTime)
        {
            return agent is IAgentCommandReceiver receiver &&
                receiver.MoveDirective(targetPosition, stoppingDistance, moveSpeed).Status == AgentNavigationStatus.Arrived;
        }

        protected void StopAgentMovement(IAgentReadOnly agent)
        {
            if (agent is IAgentCommandReceiver receiver) receiver.StopDirectiveMovement();
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

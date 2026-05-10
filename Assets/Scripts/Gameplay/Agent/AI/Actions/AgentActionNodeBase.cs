using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.AI.Actions
{
    /// <summary>
    /// Agent 行为树执行节点基类
    /// 集中处理 Agent 上下文、目标解析、移动和指令清理等通用逻辑
    /// </summary>
    public abstract class AgentActionNodeBase : ActionNode
    {
        protected AgentActionNodeBase(string nodeName)
            : base(nodeName)
        {
        }

        protected bool TryGetAgent(BehaviorTreeContext context, out IAgentReadOnly agent)
        {
            // UserContext 由 AgentBrainController 注入，节点不反查场景对象
            agent = context.UserContext as IAgentReadOnly;
            return agent != null && agent.CachedTransform != null && !agent.IsDead;
        }

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

        protected bool TryResolveTargetPosition(AgentTargetRef targetRef, out Vector3 targetPosition)
        {
            if (!targetRef.IsValid)
            {
                targetPosition = default;
                return false;
            }

            if (targetRef.TargetObject != null)
            {
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

        protected float GetFloat(
            BehaviorTreeContext context,
            BlackboardKey key,
            float defaultValue)
        {
            return context.Blackboard.TryGetValue(key, out float value) ? value : defaultValue;
        }

        protected void SetFact<TValue>(
            BehaviorTreeContext context,
            BlackboardKey key,
            TValue value)
        {
            context.Blackboard.SetValue(key, value, context.TimeSeconds);
        }

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

        protected bool MoveAgentTowards(
            IAgentReadOnly agent,
            Vector3 targetPosition,
            float stoppingDistance,
            float moveSpeed,
            float deltaTime)
        {
            Transform agentTransform = agent.CachedTransform;
            Vector3 currentPosition = agentTransform.position;
            // MVP 阶段使用平面直线移动，后续可在这里替换为 NavMesh 或 Motor
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

        protected BehaviorNodeResult FailMissingDirective(AgentDirectiveType directiveType)
        {
            return Fail(
                BehaviorFailureCode.MissingBlackboardValue,
                $"Missing pending directive [{directiveType}]");
        }
    }
}

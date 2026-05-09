using UnityEngine;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent干预请求数据结构
    /// DirectiveType 表示“做什么”，TargetRef 表示“对谁/哪里做”。
    /// </summary>
    public readonly struct AgentDirectiveRequest
    {
        public AgentId TargetAgentId { get; } // 指令目标 Agent
        public AgentDirectiveType DirectiveType { get; } // 干预请求类型
        public AgentTargetRef TargetRef { get; } // 指令目标引用

        public GameObject TargetObject => TargetRef.TargetObject; // 兼容旧调用：具体目标对象
        public Vector3 TargetPosition => TargetRef.TargetPosition; // 兼容旧调用：目标位置
        public bool HasTargetPosition => TargetRef.HasTargetPosition; // 兼容旧调用：是否包含有效位置
        public string TargetId => TargetRef.TargetId;
        public string PayloadId { get; }
        public string CommandId { get; }
        public int Priority { get; }

        public static AgentDirectiveRequest SearchConcreteResource(
            GameObject resourceObject,
            string targetId = "",
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            return new AgentDirectiveRequest(
                AgentDirectiveType.Search,
                AgentTargetRef.FromConcreteObject(AgentTargetKind.Resource, resourceObject, targetId),
                targetId,
                targetAgentId,
                commandId,
                priority);
        }

        public static AgentDirectiveRequest EngageConcreteEnemy(
            GameObject enemyObject,
            string targetId = "",
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            return new AgentDirectiveRequest(
                AgentDirectiveType.Engage,
                AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, enemyObject, targetId),
                targetId,
                targetAgentId,
                commandId,
                priority);
        }

        public static AgentDirectiveRequest SearchAbstractResourcePoint(
            string targetId,
            Vector3 targetPosition,
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            return new AgentDirectiveRequest(
                AgentDirectiveType.Search,
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Resource, targetId, targetPosition),
                targetId,
                targetAgentId,
                commandId,
                priority);
        }

        public static AgentDirectiveRequest EngageAbstractEnemyPoint(
            string targetId,
            Vector3 targetPosition,
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            return new AgentDirectiveRequest(
                AgentDirectiveType.Engage,
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Enemy, targetId, targetPosition),
                targetId,
                targetAgentId,
                commandId,
                priority);
        }

        public AgentDirectiveRequest(
            AgentDirectiveType directiveType,
            AgentTargetRef targetRef,
            string payloadId = "",
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            TargetAgentId = targetAgentId;
            DirectiveType = directiveType;
            TargetRef = targetRef;
            PayloadId = payloadId ?? string.Empty;
            CommandId = commandId ?? string.Empty;
            Priority = priority;
        }

        public AgentDirectiveRequest(
            AgentDirectiveType directiveType,
            GameObject targetObject = null,
            Vector3 targetPosition = default,
            bool hasTargetPosition = false,
            string payloadId = "",
            AgentId targetAgentId = default(AgentId),
            string commandId = "",
            int priority = 0)
        {
            TargetAgentId = targetAgentId;
            DirectiveType = directiveType;
            TargetRef = CreateTargetRefFromLegacyArguments(
                directiveType,
                targetObject,
                targetPosition,
                hasTargetPosition,
                payloadId);
            PayloadId = payloadId ?? string.Empty;
            CommandId = commandId ?? string.Empty;
            Priority = priority;
        }

        public AgentDirectiveRequest WithTargetAgentId(AgentId targetAgentId)
        {
            return new AgentDirectiveRequest(
                DirectiveType,
                TargetRef,
                PayloadId,
                targetAgentId,
                CommandId,
                Priority);
        }

        public AgentDirectiveRequest WithTargetRef(AgentTargetRef targetRef)
        {
            return new AgentDirectiveRequest(
                DirectiveType,
                targetRef,
                PayloadId,
                TargetAgentId,
                CommandId,
                Priority);
        }

        private static AgentTargetRef CreateTargetRefFromLegacyArguments(
            AgentDirectiveType directiveType,
            GameObject targetObject,
            Vector3 targetPosition,
            bool hasTargetPosition,
            string payloadId)
        {
            AgentTargetKind targetKind = InferTargetKind(directiveType);

            if (targetObject != null)
            {
                return AgentTargetRef.FromConcreteObject(targetKind, targetObject, payloadId);
            }

            if (hasTargetPosition || !string.IsNullOrEmpty(payloadId))
            {
                return AgentTargetRef.FromAbstractPoint(
                    targetKind,
                    payloadId,
                    targetPosition,
                    hasTargetPosition);
            }

            return AgentTargetRef.None;
        }

        private static AgentTargetKind InferTargetKind(AgentDirectiveType directiveType)
        {
            switch (directiveType)
            {
                case AgentDirectiveType.Search:
                    return AgentTargetKind.Resource;
                case AgentDirectiveType.Engage:
                    return AgentTargetKind.Enemy;
                case AgentDirectiveType.MoveTo:
                    return AgentTargetKind.Location;
                case AgentDirectiveType.Extract:
                    return AgentTargetKind.Extraction;
                default:
                    return AgentTargetKind.None;
            }
        }
    }
}

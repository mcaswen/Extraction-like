using UnityEngine;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent干预请求数据结构
    /// DirectiveType 表示“做什么”，TargetRef 表示“对谁/哪里做”
    /// </summary>
    public readonly struct AgentDirectiveRequest
    {
        /// <summary>
        /// 指令要投递到的目标 Agent
        /// 为空时由路由器选择默认 Agent
        /// </summary>
        public AgentId TargetAgentId { get; }

        /// <summary>
        /// 干预请求类型，表示 Agent 要做什么
        /// </summary>
        public AgentDirectiveType DirectiveType { get; }

        /// <summary>
        /// 指令目标引用，表示 Agent 要对谁或哪里执行
        /// </summary>
        public AgentTargetRef TargetRef { get; }

        /// <summary>
        /// 兼容旧调用的具体目标对象
        /// </summary>
        public GameObject TargetObject => TargetRef.TargetObject;

        /// <summary>
        /// 兼容旧调用的目标位置
        /// </summary>
        public Vector3 TargetPosition => TargetRef.TargetPosition;

        /// <summary>
        /// 兼容旧调用的位置有效标记
        /// </summary>
        public bool HasTargetPosition => TargetRef.HasTargetPosition;

        /// <summary>
        /// 目标业务 ID
        /// </summary>
        public string TargetId => TargetRef.TargetId;

        /// <summary>
        /// 指令携带的兼容载荷 ID
        /// </summary>
        public string PayloadId { get; }

        /// <summary>
        /// 外部系统生成的命令 ID
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// 指令优先级，数值越高越应被优先处理
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 创建搜索具体资源对象的指令
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <param name="targetId"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 创建接战具体敌人对象的指令
        /// </summary>
        /// <param name="enemyObject"></param>
        /// <param name="targetId"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 创建搜索抽象资源点的指令
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="targetPosition"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 创建接战抽象敌人点的指令
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="targetPosition"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 使用目标引用创建 Agent 干预请求
        /// </summary>
        /// <param name="directiveType"></param>
        /// <param name="targetRef"></param>
        /// <param name="payloadId"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
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

        /// <summary>
        /// 使用旧版目标参数创建 Agent 干预请求
        /// 内部会转换为 AgentTargetRef 以兼容新旧调用
        /// </summary>
        /// <param name="directiveType"></param>
        /// <param name="targetObject"></param>
        /// <param name="targetPosition"></param>
        /// <param name="hasTargetPosition"></param>
        /// <param name="payloadId"></param>
        /// <param name="targetAgentId"></param>
        /// <param name="commandId"></param>
        /// <param name="priority"></param>
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

        /// <summary>
        /// 返回替换目标 AgentId 后的新请求
        /// 保持结构体不可变，避免路由时修改原始请求
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 返回替换目标引用后的新请求
        /// </summary>
        /// <param name="targetRef"></param>
        /// <returns></returns>
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

using UnityEngine;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent干预请求数据结构
    /// 当前阶段用于承载干预目标，不解释具体业务含义
    /// </summary>
    public readonly struct AgentDirectiveRequest
    {
        public AgentId TargetAgentId { get; } // 指令目标 Agent
        public AgentDirectiveType DirectiveType { get; } // 干预请求类型
        public GameObject TargetObject { get; } // 目标对象（如敌人、资源点等）
        public Vector3 TargetPosition { get; } // 目标位置（如区域中心点等）
        public bool HasTargetPosition { get; } // 是否包含有效的目标位置
        public string PayloadId { get; }
        public string CommandId { get; }
        public int Priority { get; }

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
            TargetObject = targetObject;
            TargetPosition = targetPosition;
            HasTargetPosition = hasTargetPosition;
            PayloadId = payloadId ?? string.Empty;
            CommandId = commandId ?? string.Empty;
            Priority = priority;
        }

        public AgentDirectiveRequest WithTargetAgentId(AgentId targetAgentId)
        {
            return new AgentDirectiveRequest(
                DirectiveType,
                TargetObject,
                TargetPosition,
                HasTargetPosition,
                PayloadId,
                targetAgentId,
                CommandId,
                Priority);
        }
    }
}

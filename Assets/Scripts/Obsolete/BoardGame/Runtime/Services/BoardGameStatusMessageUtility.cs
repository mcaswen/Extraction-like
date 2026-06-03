using BoardGame.Runtime.State;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 多 Agent 全局状态提示格式化工具
    /// 统一给提示补足触发者或节点上下文，避免多 AI 同时行动时文案失焦
    /// </summary>
    internal static class BoardGameStatusMessageUtility
    {
        /// <summary>
        /// 生成系统级提示
        /// </summary>
        public static string System(string message)
        {
            return string.IsNullOrEmpty(message) ? string.Empty : $"System: {message}";
        }

        /// <summary>
        /// 生成带 Agent 归属的提示
        /// </summary>
        public static string Agent(BoardAgentState agentState, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string agentLabel = GetAgentLabel(agentState);
            return string.IsNullOrEmpty(agentLabel) ? message : $"{agentLabel}: {message}";
        }

        /// <summary>
        /// 生成带节点上下文的提示
        /// </summary>
        public static string Node(BoardNodeRuntimeState nodeState, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string nodeLabel = GetNodeLabel(nodeState);
            return string.IsNullOrEmpty(nodeLabel) ? message : $"{nodeLabel}: {message}";
        }

        /// <summary>
        /// 生成同时带 Agent 和节点归属的提示
        /// </summary>
        public static string AgentAtNode(BoardAgentState agentState, BoardNodeRuntimeState nodeState, string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            string agentLabel = GetAgentLabel(agentState);
            string nodeLabel = GetNodeLabel(nodeState);

            if (string.IsNullOrEmpty(agentLabel) && string.IsNullOrEmpty(nodeLabel))
            {
                return message;
            }

            if (string.IsNullOrEmpty(agentLabel))
            {
                return $"{nodeLabel}: {message}";
            }

            if (string.IsNullOrEmpty(nodeLabel))
            {
                return $"{agentLabel}: {message}";
            }

            return $"{agentLabel} @ {nodeLabel}: {message}";
        }

        private static string GetAgentLabel(BoardAgentState agentState)
        {
            return agentState != null && !string.IsNullOrEmpty(agentState.DisplayName)
                ? agentState.DisplayName
                : string.Empty;
        }

        private static string GetNodeLabel(BoardNodeRuntimeState nodeState)
        {
            return nodeState != null && !string.IsNullOrEmpty(nodeState.NodeId)
                ? nodeState.NodeId
                : string.Empty;
        }
    }
}

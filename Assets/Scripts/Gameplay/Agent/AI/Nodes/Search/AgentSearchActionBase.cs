using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.Memory;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 搜索子树动作的公共基类：持有搜索记忆
    /// </summary>
    public abstract class AgentSearchActionBase : AgentActionNodeBase
    {
        protected const string InvalidAgent = "Agent context is invalid";

        protected readonly AgentSearchMemory Memory;

        protected AgentSearchActionBase(string nodeName, string displayName, AgentSearchMemory memory)
            : base(nodeName)
        {
            Memory = memory;
            DisplayName = displayName;
        }
    }
}

using Gameplay.Agent.AI.States;
using Gameplay.Agent.AI.Trees;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Factories
{
    /// <summary>
    /// Agent Brain 宏状态实例创建工厂
    /// 负责创建各状态对象，并为可执行宏状态挂接对应行为树
    /// </summary>
    public sealed class AgentBrainStateFactory
    {
        /// <summary>
        /// 创建 Agent Brain 根状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateRootState()
        {
            return new AgentBrainState(
                AgentMacroStateId.None,
                "AgentRoot");
        }

        /// <summary>
        /// 创建 Raid 宏状态容器
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateRaidState()
        {
            return new AgentBrainState(
                AgentMacroStateId.None,
                "Raid");
        }

        /// <summary>
        /// 创建探索状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateExploreState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Explore,
                "Explore",
                AgentLegacyBrainTrees.Explore());
        }

        public AgentBrainState CreateNavigateState()
            => new AgentBrainState(AgentMacroStateId.Navigate, "Navigate", AgentBrainTrees.Navigate());

        public AgentBrainState CreateCombatState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Combat,
                "Combat",
                AgentLegacyBrainTrees.Combat());
        }

        public AgentBrainState CreateInvestigateEnemySourceState()
        {
            return new AgentBrainState(
                AgentMacroStateId.InvestigateEnemySource,
                "InvestigateEnemySource",
                AgentBrainTrees.InvestigateEnemySource());
        }

        public AgentBrainState CreateSearchResourceState()
        {
            return new AgentBrainState(
                AgentMacroStateId.SearchResource,
                "SearchResource",
                AgentLegacyBrainTrees.SearchResource(AgentMacroStateId.SearchResource));
        }

        public AgentBrainState CreateInteractLootState()
        {
            return new AgentBrainState(
                AgentMacroStateId.InteractLoot,
                "InteractLoot",
                AgentLegacyBrainTrees.SearchResource(AgentMacroStateId.InteractLoot));
        }

        public AgentBrainState CreateExtractionState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Extraction,
                "Extraction",
                AgentLegacyBrainTrees.Extraction());
        }
    }
}

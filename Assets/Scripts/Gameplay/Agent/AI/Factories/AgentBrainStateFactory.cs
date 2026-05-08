using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.States;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Factories
{
    /// <summary>
    /// Agent Brain 宏状态实例创建工厂
    /// 负责创建各状态对象，并为其挂上当前阶段最小占位行为树
    /// </summary>
    public sealed class AgentBrainStateFactory
    {
        public AgentBrainState CreateRootState()
        {
            return new AgentBrainState(
                AgentMacroStateId.None,
                "AgentRoot");
        }

        public AgentBrainState CreateRaidState()
        {
            return new AgentBrainState(
                AgentMacroStateId.None,
                "Raid");
        }

        public AgentBrainState CreateExploreState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Explore,
                "Explore",
                BuildMaintainStateTree(AgentMacroStateId.Explore));
        }

        public AgentBrainState CreateCombatState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Combat,
                "Combat",
                BuildMaintainStateTree(AgentMacroStateId.Combat));
        }

        public AgentBrainState CreateSearchResourceState()
        {
            return new AgentBrainState(
                AgentMacroStateId.SearchResource,
                "SearchResource",
                BuildMaintainStateTree(AgentMacroStateId.SearchResource));
        }

        public AgentBrainState CreateInteractLootState()
        {
            return new AgentBrainState(
                AgentMacroStateId.InteractLoot,
                "InteractLoot",
                BuildMaintainStateTree(AgentMacroStateId.InteractLoot));
        }

        public AgentBrainState CreateExtractionState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Extraction,
                "Extraction",
                BuildMaintainStateTree(AgentMacroStateId.Extraction));
        }

        /// <summary>
        /// 当前阶段每个宏状态先绑定一棵最小占位树
        /// 这样做的目的不是“逻辑完整”，而是验证状态机与行为树已经真正接通
        /// 后续状态内具体行为节点补齐后，只需要替换这里返回的树构造方式
        /// </summary>
        /// <param name="macroStateId"></param>
        /// <returns></returns>
        private static BehaviorTreeType BuildMaintainStateTree(AgentMacroStateId macroStateId)
        {
            return new BehaviorTreeType(
                $"{macroStateId}Tree",
                new MaintainMacroStateActionNode(
                    $"{macroStateId}_MaintainMacroState",
                    macroStateId));
        }
    }
}
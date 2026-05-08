using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;
using Gameplay.Player.AI.Actions;
using Gameplay.Player.AI.States;
using Gameplay.Player.Data;

namespace Gameplay.Player.AI.Factories
{
    /// <summary>
    /// 主角 Brain 宏状态实例创建工厂
    /// 负责创建各状态对象，并为其挂上当前阶段最小占位行为树
    /// </summary>
    public sealed class PlayerBrainStateFactory
    {
        public PlayerBrainState CreateRootState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.None,
                "PlayerRoot");
        }

        public PlayerBrainState CreateRaidState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.None,
                "Raid");
        }

        public PlayerBrainState CreateExploreState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.Explore,
                "Explore",
                BuildMaintainStateTree(PlayerMacroStateId.Explore));
        }

        public PlayerBrainState CreateCombatState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.Combat,
                "Combat",
                BuildMaintainStateTree(PlayerMacroStateId.Combat));
        }

        public PlayerBrainState CreateSearchResourceState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.SearchResource,
                "SearchResource",
                BuildMaintainStateTree(PlayerMacroStateId.SearchResource));
        }

        public PlayerBrainState CreateInteractLootState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.InteractLoot,
                "InteractLoot",
                BuildMaintainStateTree(PlayerMacroStateId.InteractLoot));
        }

        public PlayerBrainState CreateExtractionState()
        {
            return new PlayerBrainState(
                PlayerMacroStateId.Extraction,
                "Extraction",
                BuildMaintainStateTree(PlayerMacroStateId.Extraction));
        }

        /// <summary>
        /// 当前阶段每个宏状态先绑定一棵最小占位树
        /// 这样做的目的不是“逻辑完整”，而是验证状态机与行为树已经真正接通
        /// 后续状态内具体行为节点补齐后，只需要替换这里返回的树构造方式
        /// </summary>
        /// <param name="macroStateId"></param>
        /// <returns></returns>
        private static BehaviorTreeType BuildMaintainStateTree(PlayerMacroStateId macroStateId)
        {
            return new BehaviorTreeType(
                $"{macroStateId}Tree",
                new MaintainMacroStateActionNode(
                    $"{macroStateId}_MaintainMacroState",
                    macroStateId));
        }
    }
}
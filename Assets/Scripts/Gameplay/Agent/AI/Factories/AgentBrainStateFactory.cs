using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;
using Core.BehaviorTree.Nodes.Composites;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.AI.States;
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
                BuildMaintainStateTree(AgentMacroStateId.Explore));
        }

        /// <summary>
        /// 创建战斗状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateCombatState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Combat,
                "Combat",
                BuildCombatTree());
        }

        /// <summary>
        /// 创建敌人来源侦查状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateInvestigateEnemySourceState()
        {
            return new AgentBrainState(
                AgentMacroStateId.InvestigateEnemySource,
                "InvestigateEnemySource",
                BuildInvestigateEnemySourceTree());
        }

        /// <summary>
        /// 创建资源搜索状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateSearchResourceState()
        {
            return new AgentBrainState(
                AgentMacroStateId.SearchResource,
                "SearchResource",
                BuildSearchResourceTree(AgentMacroStateId.SearchResource));
        }

        /// <summary>
        /// 创建战利品交互状态
        /// 当前复用资源搜索行为树
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateInteractLootState()
        {
            return new AgentBrainState(
                AgentMacroStateId.InteractLoot,
                "InteractLoot",
                BuildSearchResourceTree(AgentMacroStateId.InteractLoot));
        }

        /// <summary>
        /// 创建撤离状态
        /// </summary>
        /// <returns></returns>
        public AgentBrainState CreateExtractionState()
        {
            return new AgentBrainState(
                AgentMacroStateId.Extraction,
                "Extraction",
                BuildExtractionTree());
        }

        /// <summary>
        /// 构建只维持宏状态标记的保底行为树
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

        private static BehaviorTreeType BuildCombatTree()
        {
            // 战斗树先统一靠近目标，再由攻击节点处理射程内的持续攻击
            return new BehaviorTreeType(
                "CombatTree",
                new SequenceNode(
                    "Combat_EngageSequence",
                    new MoveToTargetActionNode(
                        "Combat_MoveToEnemy",
                        AgentDirectiveType.Engage,
                        AgentTargetKind.Enemy,
                        AgentBlackboardKeys.AttackRange,
                        6f),
                    new EngageEnemyActionNode("Combat_EngageEnemy")));
        }

        private static BehaviorTreeType BuildInvestigateEnemySourceTree()
        {
            // 敌人来源点只负责靠近侦查，真正接战必须等活跃敌人群出现
            return new BehaviorTreeType(
                "InvestigateEnemySourceTree",
                new MoveToTargetActionNode(
                    "InvestigateEnemySource_MoveToSource",
                    AgentDirectiveType.MoveTo,
                    AgentTargetKind.EnemySource,
                    AgentBlackboardKeys.MoveStoppingDistance,
                    2f));
        }

        private static BehaviorTreeType BuildSearchResourceTree(AgentMacroStateId macroStateId)
        {
            // 搜索和开箱共用同一条执行链，宏状态差异只保留在状态名上
            return new BehaviorTreeType(
                $"{macroStateId}Tree",
                new SequenceNode(
                    $"{macroStateId}_SearchSequence",
                    new MoveToTargetActionNode(
                        $"{macroStateId}_MoveToResource",
                        AgentDirectiveType.Search,
                        AgentTargetKind.Resource,
                        AgentBlackboardKeys.InteractionDistance,
                        1.5f),
                    new SearchResourceActionNode($"{macroStateId}_SearchResource")));
        }

        private static BehaviorTreeType BuildExtractionTree()
        {
            // 撤离树先到达范围，再让撤离节点持续桥接 RaidFlowController 的计时状态
            return new BehaviorTreeType(
                "ExtractionTree",
                new SequenceNode(
                    "Extraction_Sequence",
                    new MoveToTargetActionNode(
                        "Extraction_MoveToPoint",
                        AgentDirectiveType.Extract,
                        AgentTargetKind.Extraction,
                        AgentBlackboardKeys.InteractionDistance,
                        1.5f),
                    new ExtractActionNode("Extraction_Extract")));
        }
    }
}

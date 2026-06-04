using Core.StateMachine.Runtime;
using Gameplay.Agent.AI.States;
using Gameplay.Agent.AI.Actions; // 引入行为树动作节点命名空间
using Core.BehaviorTree.Nodes.Composites; // 引入 SequenceNode
using Gameplay.Agent.Data;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace Gameplay.Agent.AI.Factories
{
    /// <summary>
    /// Agent Brain 状态机装配工厂
    /// 负责将状态实例、层级关系和转移规则组装为完整的分层状态机
    /// </summary>
    public sealed class AgentBrainStateMachineFactory
    {
        private readonly AgentBrainStateFactory _stateFactory;
        private readonly AgentBrainTransitionRules _transitionRules;

        public AgentBrainStateMachineFactory(
            AgentBrainStateFactory stateFactory,
            AgentBrainTransitionRules transitionRules)
        {
            _stateFactory = stateFactory;
            _transitionRules = transitionRules;
        }

        /// <summary>
        /// 构建Agent Brain 的 MVP 宏状态机并注入玩家高级干预移动链路
        /// </summary>
        /// <param name="stateMachineContext"></param>
        /// <returns></returns>
        public HierarchicalStateMachine Build(StateMachineContext stateMachineContext)
        {
            AgentBrainState rootState = _stateFactory.CreateRootState();
            AgentBrainState raidState = _stateFactory.CreateRaidState();

            AgentBrainState exploreState = _stateFactory.CreateExploreState();
            AgentBrainState combatState = _stateFactory.CreateCombatState();
            AgentBrainState searchResourceState = _stateFactory.CreateSearchResourceState();
            AgentBrainState interactLootState = _stateFactory.CreateInteractLootState();
            AgentBrainState extractionState = _stateFactory.CreateExtractionState();

            // ====================================================
            // 💡 [核心组装]：动态为“玩家干预状态”构建一棵符合架构的专属行为树
            // ====================================================
            BehaviorTreeType directiveTree = new BehaviorTreeType(
                "DirectiveTree",
                new SequenceNode(
                    "Directive_Sequence",
                    new MoveToTargetActionNode(
                        "Directive_MoveToClusterCenter",
                        AgentDirectiveType.MoveTo,              // 动作：移动到
                        AgentTargetKind.Location,               // 语义：位置点
                        AgentBlackboardKeys.MoveStoppingDistance,// 停止距离的黑板Key
                        2.0f                                    // 保底超时时间
                    )
                )
            );

            // 实例化干预状态对象，直接套用 Explore 标记或者 None 标记即可，不影响内部行为树执行
            AgentBrainState directiveState = new AgentBrainState(
                AgentMacroStateId.Explore,
                "PlayerDirective",
                directiveTree
            );
            // ====================================================

            // 建立状态层级
            rootState.AddChild(raidState, true);

            raidState.AddChild(exploreState, true);
            raidState.AddChild(combatState);
            raidState.AddChild(searchResourceState);
            raidState.AddChild(interactLootState);
            raidState.AddChild(extractionState);

            // 将我们动态组装好的干预状态塞进宏状态机
            raidState.AddChild(directiveState);

            // ====================================================
            // 💡 [打断判定]：所有自主行为一旦检测到玩家点击范围群，无条件打断（优先级200）
            // ====================================================
            exploreState.AddTransition(new StateTransition("ExploreToDirective", directiveState, _transitionRules.CanEnterDirective, 200));
            combatState.AddTransition(new StateTransition("CombatToDirective", directiveState, _transitionRules.CanEnterDirective, 200));
            searchResourceState.AddTransition(new StateTransition("SearchResourceToDirective", directiveState, _transitionRules.CanEnterDirective, 200));
            interactLootState.AddTransition(new StateTransition("InteractLootToDirective", directiveState, _transitionRules.CanEnterDirective, 200));
            extractionState.AddTransition(new StateTransition("ExtractionToDirective", directiveState, _transitionRules.CanEnterDirective, 200));

            // ====================================================
            // 💡 [重置回归]：当点击生成的临时指令在黑板中被执行/清除时，退回自主探索
            // ====================================================
            directiveState.AddTransition(new StateTransition(
                "DirectiveToExplore",
                exploreState,
                ctx => !_transitionRules.CanEnterDirective(ctx),
                100));

            // Explore 转移
            exploreState.AddTransition(new StateTransition("ExploreToCombat", combatState, _transitionRules.CanEnterCombat, 100));
            exploreState.AddTransition(new StateTransition("ExploreToSearchResource", searchResourceState, _transitionRules.CanEnterSearchResource, 90));
            exploreState.AddTransition(new StateTransition("ExploreToExtraction", extractionState, _transitionRules.CanEnterExtraction, 80));

            // Combat 转移
            combatState.AddTransition(new StateTransition("CombatToExtraction", extractionState, _transitionRules.CanEnterExtraction, 100));
            combatState.AddTransition(new StateTransition("CombatToExplore", exploreState, _transitionRules.CanLeaveCombatToExplore, 80));

            // SearchResource 转移
            searchResourceState.AddTransition(new StateTransition("SearchResourceToCombat", combatState, _transitionRules.CanEnterCombat, 100));
            searchResourceState.AddTransition(new StateTransition("SearchResourceToInteractLoot", interactLootState, _transitionRules.CanEnterInteractLoot, 90));
            searchResourceState.AddTransition(new StateTransition("SearchResourceToExtraction", extractionState, _transitionRules.CanEnterExtraction, 80));
            searchResourceState.AddTransition(new StateTransition("SearchResourceToExplore", exploreState, _transitionRules.CanLeaveSearchResourceToExplore, 70));

            // InteractLoot 转移
            interactLootState.AddTransition(new StateTransition("InteractLootToCombat", combatState, _transitionRules.CanEnterCombat, 100));
            interactLootState.AddTransition(new StateTransition("InteractLootToExtraction", extractionState, _transitionRules.CanEnterExtraction, 90));
            interactLootState.AddTransition(new StateTransition("InteractLootToExplore", exploreState, _transitionRules.CanLeaveInteractLootToExplore, 80));

            // Extraction 转移
            extractionState.AddTransition(new StateTransition("ExtractionToCombat", combatState, _transitionRules.CanExtractionBeInterruptedByCombat, 100));
            extractionState.AddTransition(new StateTransition("ExtractionToExplore", exploreState, _transitionRules.CanLeaveExtractionToExplore, 80));

            return new HierarchicalStateMachine(rootState, stateMachineContext);
        }
    }
}
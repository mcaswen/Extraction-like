using Core.StateMachine.Runtime;
using Gameplay.Agent.AI.States;

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

        /// <summary>
        /// 创建 Agent Brain 状态机装配工厂
        /// </summary>
        /// <param name="stateFactory"></param>
        /// <param name="transitionRules"></param>
        public AgentBrainStateMachineFactory(
            AgentBrainStateFactory stateFactory,
            AgentBrainTransitionRules transitionRules)
        {
            _stateFactory = stateFactory;
            _transitionRules = transitionRules;
        }

        /// <summary>
        /// 构建Agent Brain 的 MVP 宏状态机
        /// 当前版本处理 Explore / Combat / InvestigateEnemySource / SearchResource / InteractLoot / Extraction 六个宏状态
        /// </summary>
        /// <param name="stateMachineContext"></param>
        /// <returns></returns>
        public HierarchicalStateMachine Build(StateMachineContext stateMachineContext)
        {
            AgentBrainState rootState = _stateFactory.CreateRootState();
            AgentBrainState raidState = _stateFactory.CreateRaidState();

            AgentBrainState exploreState = _stateFactory.CreateExploreState();
            AgentBrainState combatState = _stateFactory.CreateCombatState();
            AgentBrainState investigateEnemySourceState = _stateFactory.CreateInvestigateEnemySourceState();
            AgentBrainState searchResourceState = _stateFactory.CreateSearchResourceState();
            AgentBrainState interactLootState = _stateFactory.CreateInteractLootState();
            AgentBrainState extractionState = _stateFactory.CreateExtractionState();

            // 先建立状态层级
            // 当前宏状态都挂在 Raid 之下，方便后续继续向下细拆子状态
            rootState.AddChild(raidState, true);

            raidState.AddChild(exploreState, true);
            raidState.AddChild(combatState);
            raidState.AddChild(investigateEnemySourceState);
            raidState.AddChild(searchResourceState);
            raidState.AddChild(interactLootState);
            raidState.AddChild(extractionState);

            // Explore 转移
            exploreState.AddTransition(new StateTransition(
                "ExploreToCombat",
                combatState,
                _transitionRules.CanEnterCombat,
                100));

            exploreState.AddTransition(new StateTransition(
                "ExploreToSearchResource",
                searchResourceState,
                _transitionRules.CanEnterSearchResource,
                90));

            exploreState.AddTransition(new StateTransition(
                "ExploreToInvestigateEnemySource",
                investigateEnemySourceState,
                _transitionRules.CanEnterInvestigateEnemySource,
                95));

            exploreState.AddTransition(new StateTransition(
                "ExploreToExtraction",
                extractionState,
                _transitionRules.CanEnterExtraction,
                80));

            // Combat 转移
            combatState.AddTransition(new StateTransition(
                "CombatToExtraction",
                extractionState,
                _transitionRules.CanEnterExtraction,
                100));

            combatState.AddTransition(new StateTransition(
                "CombatToExplore",
                exploreState,
                _transitionRules.CanLeaveCombatToExplore,
                80));

            combatState.AddTransition(new StateTransition(
                "CombatToInvestigateEnemySource",
                investigateEnemySourceState,
                _transitionRules.CanEnterInvestigateEnemySource,
                75));

            // InvestigateEnemySource 转移
            investigateEnemySourceState.AddTransition(new StateTransition(
                "InvestigateEnemySourceToCombat",
                combatState,
                _transitionRules.CanEnterCombat,
                100));

            investigateEnemySourceState.AddTransition(new StateTransition(
                "InvestigateEnemySourceToSearchResource",
                searchResourceState,
                _transitionRules.CanEnterSearchResource,
                90));

            investigateEnemySourceState.AddTransition(new StateTransition(
                "InvestigateEnemySourceToExtraction",
                extractionState,
                _transitionRules.CanEnterExtraction,
                80));

            investigateEnemySourceState.AddTransition(new StateTransition(
                "InvestigateEnemySourceToExplore",
                exploreState,
                _transitionRules.CanLeaveInvestigateEnemySourceToExplore,
                70));

            // SearchResource 转移
            searchResourceState.AddTransition(new StateTransition(
                "SearchResourceToCombat",
                combatState,
                _transitionRules.CanEnterCombat,
                100));

            searchResourceState.AddTransition(new StateTransition(
                "SearchResourceToInteractLoot",
                interactLootState,
                _transitionRules.CanEnterInteractLoot,
                90));

            searchResourceState.AddTransition(new StateTransition(
                "SearchResourceToExtraction",
                extractionState,
                _transitionRules.CanEnterExtraction,
                80));

            searchResourceState.AddTransition(new StateTransition(
                "SearchResourceToExplore",
                exploreState,
                _transitionRules.CanLeaveSearchResourceToExplore,
                70));

            // InteractLoot 转移
            interactLootState.AddTransition(new StateTransition(
                "InteractLootToCombat",
                combatState,
                _transitionRules.CanEnterCombat,
                100));

            interactLootState.AddTransition(new StateTransition(
                "InteractLootToExtraction",
                extractionState,
                _transitionRules.CanEnterExtraction,
                90));

            interactLootState.AddTransition(new StateTransition(
                "InteractLootToExplore",
                exploreState,
                _transitionRules.CanLeaveInteractLootToExplore,
                80));

            // Extraction 转移
            extractionState.AddTransition(new StateTransition(
                "ExtractionToCombat",
                combatState,
                _transitionRules.CanExtractionBeInterruptedByCombat,
                100));

            extractionState.AddTransition(new StateTransition(
                "ExtractionToInvestigateEnemySource",
                investigateEnemySourceState,
                _transitionRules.CanEnterInvestigateEnemySource,
                90));

            extractionState.AddTransition(new StateTransition(
                "ExtractionToExplore",
                exploreState,
                _transitionRules.CanLeaveExtractionToExplore,
                80));

            return new HierarchicalStateMachine(rootState, stateMachineContext);
        }
    }
}

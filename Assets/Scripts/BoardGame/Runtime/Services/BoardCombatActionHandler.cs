using System.Collections.Generic;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 普通敌人和 Boss 战斗动作处理器
    /// </summary>
    internal sealed class BoardCombatActionHandler : IBoardNodeActionHandler
    {
        private readonly bool _isBoss;

        public BoardCombatActionHandler(bool isBoss)
        {
            _isBoss = isBoss;
        }

        public BoardNodeType SupportedNodeType => _isBoss ? BoardNodeType.Boss : BoardNodeType.Enemy;
        public BoardActionType SupportedActionType => _isBoss ? BoardActionType.FightingBoss : BoardActionType.FightingEnemy;

        public bool TryBegin(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            if (_isBoss)
            {
                if (!nodeState.HasUndefeatedBoss())
                {
                    return false;
                }

                nodeState.BossState = BoardBossStateType.Engaged;
            }
            else
            {
                if (!nodeState.HasUnclearedEnemy())
                {
                    return false;
                }

                nodeState.EnemyState = BoardEnemyStateType.Engaged;
            }

            EnterCombatState(
                agentState,
                nodeState,
                context.RuleSet.CombatRules.TickIntervalSeconds,
                sessionState.SimulationStepIndex);
            sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                agentState,
                nodeState,
                _isBoss ? "Started boss fight" : "Started combat");
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            List<BoardAgentState> participantAgents = CollectLivingParticipantsOnNode(sessionState, nodeState.NodeId, true);

            if (participantAgents.Count == 0)
            {
                return;
            }

            SyncParticipantsToCombat(
                participantAgents,
                nodeState,
                context.RuleSet.CombatRules.TickIntervalSeconds,
                sessionState.SimulationStepIndex);

            if (!IsCombatCoordinator(sessionState, agentState, nodeState.NodeId))
            {
                return;
            }

            if (nodeState.LastCombatProcessedStep != sessionState.SimulationStepIndex)
            {
                nodeState.LastCombatProcessedStep = sessionState.SimulationStepIndex;
                nodeState.CombatTickAccumulatorSeconds += deltaTime;
            }

            while (nodeState.CombatTickAccumulatorSeconds >= context.RuleSet.CombatRules.TickIntervalSeconds)
            {
                nodeState.CombatTickAccumulatorSeconds -= context.RuleSet.CombatRules.TickIntervalSeconds;
                participantAgents = CollectLivingParticipantsOnNode(sessionState, nodeState.NodeId, false);

                if (participantAgents.Count == 0)
                {
                    return;
                }

                SyncParticipantsToCombat(
                    participantAgents,
                    nodeState,
                    context.RuleSet.CombatRules.TickIntervalSeconds,
                    sessionState.SimulationStepIndex);
                BoardCombatTickResult result = context.CombatResolutionService.ResolveCombatTick(participantAgents, nodeState, _isBoss);
                SyncParticipantsToCombat(
                    participantAgents,
                    nodeState,
                    context.RuleSet.CombatRules.TickIntervalSeconds,
                    sessionState.SimulationStepIndex);

                if (!result.EncounterDefeated)
                {
                    string downedSummary = result.DownedAgentCount > 0
                        ? $", {result.DownedAgentCount} AI were downed"
                        : string.Empty;
                    sessionState.StatusMessage = BoardGameStatusMessageUtility.Node(
                        nodeState,
                        $"Combat ongoing {result.ActiveParticipantCount} AI dealt {result.TotalDamageDealt} damage and took {result.TotalDamageTaken} damage{downedSummary}");
                    continue;
                }

                nodeState.CombatTickAccumulatorSeconds = 0f;
                List<BoardAgentState> survivingParticipantAgents = CollectLivingParticipantsOnNode(sessionState, nodeState.NodeId, true);
                string encounterExperienceSummary = GrantEncounterExperienceToSurvivors(
                    context,
                    sessionState,
                    survivingParticipantAgents,
                    nodeState);
                List<BoardItemInstance> generatedItems = context.LootResolutionService.GenerateEncounterLoot(nodeState, _isBoss);
                BoardAgentState lootInteractionOwner = ResolveLootInteractionOwner(sessionState, survivingParticipantAgents, agentState);
                BoardExperienceGrantResult itemExperienceResult = context.ProgressionService.GrantExperienceFromItems(
                    sessionState,
                    lootInteractionOwner,
                    generatedItems);

                if (generatedItems.Count == 0)
                {
                    ResetParticipantsAfterEncounterClear(context, survivingParticipantAgents);
                    sessionState.StatusMessage = BoardGameStatusMessageUtility.Node(
                        nodeState,
                        _isBoss
                            ? $"Boss defeated{encounterExperienceSummary}"
                            : $"Enemy cleared{encounterExperienceSummary}");
                    return;
                }

                context.LootResolutionService.PrepareNodeLootContainer(nodeState, generatedItems, context.BagLayoutSettings);
                ResetNonLootOwnerParticipantsAfterEncounterClear(context, survivingParticipantAgents, lootInteractionOwner);
                sessionState.ActiveInteractionAgentId = lootInteractionOwner != null ? lootInteractionOwner.AgentId : string.Empty;
                sessionState.ActiveLootNodeId = nodeState.NodeId;
                sessionState.IsLootInteractionOpen = false;
                sessionState.FocusedAgentId = lootInteractionOwner != null ? lootInteractionOwner.AgentId : sessionState.FocusedAgentId;

                if (lootInteractionOwner != null)
                {
                    lootInteractionOwner.CurrentActionType = BoardActionType.Searching;
                    lootInteractionOwner.CurrentActionDuration = 1f;
                    lootInteractionOwner.CurrentActionAccumulatorSeconds = 0f;
                    lootInteractionOwner.CurrentActionProgress = nodeState.GetLootRevealProgress01();
                    lootInteractionOwner.CombatJoinStepIndex = -1;
                }

                sessionState.StatusMessage = BoardGameStatusMessageUtility.AgentAtNode(
                    lootInteractionOwner,
                    nodeState,
                    _isBoss
                        ? $"Boss defeated, press F to search the loot{encounterExperienceSummary}{itemExperienceResult.Summary}"
                        : $"Enemy cleared, press F to search the loot{encounterExperienceSummary}{itemExperienceResult.Summary}");
                return;
            }
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState)
        {
            if (_isBoss)
            {
                return;
            }

            if (HasOtherLivingParticipantOnNode(sessionState, nodeState.NodeId, agentState.AgentId))
            {
                return;
            }

            nodeState.EnemyState = nodeState.EnemyCurrentHealth < nodeState.EnemyMaxHealth
                ? BoardEnemyStateType.Damaged
                : BoardEnemyStateType.Disengaged;
        }

        // 共享战斗里所有同节点的存活 Agent 都要切到同一个战斗动作，避免中途进战时状态不同步
        private void SyncParticipantsToCombat(
            IReadOnlyList<BoardAgentState> participantAgents,
            BoardNodeRuntimeState nodeState,
            float tickIntervalSeconds,
            int currentSimulationStep)
        {
            for (int index = 0; index < participantAgents.Count; index++)
            {
                EnterCombatState(participantAgents[index], nodeState, tickIntervalSeconds, currentSimulationStep);
            }
        }

        private void EnterCombatState(
            BoardAgentState agentState,
            BoardNodeRuntimeState nodeState,
            float tickIntervalSeconds,
            int currentSimulationStep)
        {
            if (agentState == null || !agentState.IsAlive || agentState.HasExtracted)
            {
                return;
            }

            if (agentState.CurrentActionType != SupportedActionType)
            {
                agentState.CombatJoinStepIndex = currentSimulationStep;
            }

            agentState.CurrentActionType = SupportedActionType;
            agentState.CurrentActionDuration = tickIntervalSeconds;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = GetActionProgress(nodeState);
        }

        /// <summary>
        /// 共享战斗每帧只允许节点上的第一个存活 Agent 推进一步
        /// 其余 Agent 只同步表现，避免同一节点在一帧内被重复结算
        /// </summary>
        private static bool IsCombatCoordinator(
            BoardGameSessionState sessionState,
            BoardAgentState currentAgentState,
            string nodeId)
        {
            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState == null ||
                    !agentState.IsAlive ||
                    agentState.HasExtracted ||
                    agentState.CombatJoinStepIndex == sessionState.SimulationStepIndex ||
                    agentState.CurrentNodeId != nodeId)
                {
                    continue;
                }

                return agentState.AgentId == currentAgentState.AgentId;
            }

            return false;
        }

        private static List<BoardAgentState> CollectLivingParticipantsOnNode(
            BoardGameSessionState sessionState,
            string nodeId,
            bool includeFreshJoiners)
        {
            List<BoardAgentState> participantAgents = new List<BoardAgentState>();

            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState == null ||
                    !agentState.IsAlive ||
                    agentState.HasExtracted ||
                    (!includeFreshJoiners && agentState.CombatJoinStepIndex == sessionState.SimulationStepIndex) ||
                    agentState.CurrentNodeId != nodeId)
                {
                    continue;
                }

                participantAgents.Add(agentState);
            }

            return participantAgents;
        }

        private static bool HasOtherLivingParticipantOnNode(
            BoardGameSessionState sessionState,
            string nodeId,
            string excludedAgentId)
        {
            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState == null ||
                    !agentState.IsAlive ||
                    agentState.HasExtracted ||
                    agentState.AgentId == excludedAgentId ||
                    agentState.CurrentNodeId != nodeId)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 战斗经验只发给这场共享战斗里仍然存活的参与者
        /// </summary>
        private string GrantEncounterExperienceToSurvivors(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            IReadOnlyList<BoardAgentState> survivingParticipantAgents,
            BoardNodeRuntimeState nodeState)
        {
            int grantedAgentCount = 0;
            int experienceValue = 0;
            int levelUpAgentCount = 0;

            if (survivingParticipantAgents != null)
            {
                for (int index = 0; index < survivingParticipantAgents.Count; index++)
                {
                    BoardAgentState participantAgent = survivingParticipantAgents[index];
                    BoardExperienceGrantResult experienceResult = context.ProgressionService.GrantExperienceFromEncounter(
                        sessionState,
                        participantAgent,
                        nodeState,
                        _isBoss);

                    if (experienceResult.ExperienceGained <= 0)
                    {
                        continue;
                    }

                    grantedAgentCount++;
                    experienceValue = experienceResult.ExperienceGained;

                    if (experienceResult.LevelsGained > 0 || experienceResult.TriggeredLevelUpChoice)
                    {
                        levelUpAgentCount++;
                    }
                }
            }

            if (grantedAgentCount <= 0 || experienceValue <= 0)
            {
                return string.Empty;
            }

            string summary = $" {grantedAgentCount} AI gained {experienceValue} XP";

            if (levelUpAgentCount > 0)
            {
                summary += ". Choose an upgrade";
            }

            return summary;
        }

        private static BoardAgentState ResolveLootInteractionOwner(
            BoardGameSessionState sessionState,
            IReadOnlyList<BoardAgentState> survivingParticipantAgents,
            BoardAgentState fallbackAgentState)
        {
            BoardAgentState focusedAgentState = sessionState.GetFocusedAgentState();

            if (focusedAgentState != null &&
                focusedAgentState.IsAlive &&
                focusedAgentState.CurrentNodeId == fallbackAgentState.CurrentNodeId)
            {
                return focusedAgentState;
            }

            if (fallbackAgentState != null && fallbackAgentState.IsAlive)
            {
                return fallbackAgentState;
            }

            if (survivingParticipantAgents == null || survivingParticipantAgents.Count == 0)
            {
                return null;
            }

            return survivingParticipantAgents[0];
        }

        private static void ResetParticipantsAfterEncounterClear(
            BoardNodeActionHandlerContext context,
            IReadOnlyList<BoardAgentState> participantAgents)
        {
            if (participantAgents == null)
            {
                return;
            }

            for (int index = 0; index < participantAgents.Count; index++)
            {
                BoardNodeActionHandlerUtility.ResetCurrentTarget(context, participantAgents[index]);
            }
        }

        private static void ResetNonLootOwnerParticipantsAfterEncounterClear(
            BoardNodeActionHandlerContext context,
            IReadOnlyList<BoardAgentState> participantAgents,
            BoardAgentState lootInteractionOwner)
        {
            if (participantAgents == null)
            {
                return;
            }

            for (int index = 0; index < participantAgents.Count; index++)
            {
                BoardAgentState participantAgent = participantAgents[index];

                if (lootInteractionOwner != null && participantAgent.AgentId == lootInteractionOwner.AgentId)
                {
                    continue;
                }

                BoardNodeActionHandlerUtility.ResetCurrentTarget(context, participantAgent);
            }
        }

        private float GetActionProgress(BoardNodeRuntimeState nodeState)
        {
            int currentHealth = _isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int maxHealth = Mathf.Max(1, _isBoss ? nodeState.BossMaxHealth : nodeState.EnemyMaxHealth);
            return 1f - (float)currentHealth / maxHealth;
        }
    }
}

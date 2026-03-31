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

            BoardAgentState agentState = sessionState.AgentState;
            agentState.CurrentActionType = SupportedActionType;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionAccumulatorSeconds = 0f;
            agentState.CurrentActionProgress = GetActionProgress(nodeState);
            sessionState.StatusMessage = _isBoss
                ? $"Started boss fight at {nodeState.NodeId}"
                : $"Started engaging {nodeState.NodeId}";
            return true;
        }

        public void Tick(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState,
            float deltaTime)
        {
            BoardAgentState agentState = sessionState.AgentState;
            agentState.CurrentActionAccumulatorSeconds += deltaTime;
            agentState.CurrentActionDuration = 1f;
            agentState.CurrentActionProgress = GetActionProgress(nodeState);

            while (agentState.CurrentActionAccumulatorSeconds >= context.RuleSet.CombatRules.TickIntervalSeconds)
            {
                agentState.CurrentActionAccumulatorSeconds -= context.RuleSet.CombatRules.TickIntervalSeconds;
                BoardCombatTickResult result = context.CombatResolutionService.ResolveCombatTick(agentState, nodeState, _isBoss);

                if (result.AgentDefeated)
                {
                    return;
                }

                if (!result.EncounterDefeated)
                {
                    sessionState.StatusMessage = $"Combat ongoing  AI dealt {result.DamageDealt} damage and took {result.DamageTaken} damage";
                    continue;
                }

                BoardExperienceGrantResult experienceResult = context.ProgressionService.GrantExperienceFromEncounter(sessionState, nodeState, _isBoss);
                List<BoardItemInstance> generatedItems = context.LootResolutionService.GenerateEncounterLoot(nodeState, _isBoss);
                BoardAutoCollectResult collectResult = context.LootResolutionService.AutoCollect(agentState.InventoryState, generatedItems);
                BoardNodeActionHandlerUtility.FinishCurrentTarget(
                    context,
                    sessionState,
                    _isBoss
                        ? $"Boss defeated{collectResult.Summary}{experienceResult.Summary}"
                        : $"Enemy cleared{collectResult.Summary}{experienceResult.Summary}");
                return;
            }
        }

        public void FinalizeForRedirect(
            BoardNodeActionHandlerContext context,
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState)
        {
            if (_isBoss)
            {
                return;
            }

            nodeState.EnemyState = nodeState.EnemyCurrentHealth < nodeState.EnemyMaxHealth
                ? BoardEnemyStateType.Damaged
                : BoardEnemyStateType.Disengaged;
        }

        private float GetActionProgress(BoardNodeRuntimeState nodeState)
        {
            int currentHealth = _isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int maxHealth = Mathf.Max(1, _isBoss ? nodeState.BossMaxHealth : nodeState.EnemyMaxHealth);
            return 1f - (float)currentHealth / maxHealth;
        }
    }
}

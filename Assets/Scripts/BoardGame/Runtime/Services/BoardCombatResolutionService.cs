using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 战斗初始化与单次战斗 tick 结算服务
    /// </summary>
    public sealed class BoardCombatResolutionService
    {
        private readonly Dictionary<BoardDangerTier, BoardEnemyStatDefinition> _enemyDefinitionsByTier =
            new Dictionary<BoardDangerTier, BoardEnemyStatDefinition>();

        private readonly SO_BoardGame_RuleSet _ruleSet;

        public BoardCombatResolutionService(SO_BoardGame_RuleSet ruleSet)
        {
            _ruleSet = ruleSet;

            // 将敌人模板按危险等级建索引，运行时可 O(1) 查到对应原型
            foreach (BoardEnemyStatDefinition definition in ruleSet.CombatRules.EnemyDefinitions)
            {
                _enemyDefinitionsByTier[definition.DangerTier] = definition;
            }
        }

        /// <summary>
        /// 根据配置模板初始化敌人点或 Boss 点的战斗属性
        /// </summary>
        public void InitializeNodeCombatState(BoardNodeRuntimeState nodeState)
        {
            if (nodeState.NodeType == BoardNodeType.Enemy)
            {
                BoardEnemyStatDefinition definition = _enemyDefinitionsByTier.TryGetValue(nodeState.DangerTier, out BoardEnemyStatDefinition value)
                    ? value
                    : _enemyDefinitionsByTier[BoardDangerTier.Low];

                nodeState.EnemyMaxHealth = definition.MaxHealth;
                nodeState.EnemyCurrentHealth = definition.MaxHealth;
                nodeState.EnemyAttack = definition.Attack;
                nodeState.EnemyDefense = definition.Defense;
                nodeState.EnemyState = BoardEnemyStateType.Unengaged;
                return;
            }

            if (nodeState.NodeType == BoardNodeType.Boss)
            {
                BoardBossStatDefinition definition = _ruleSet.CombatRules.BossDefinition;
                nodeState.BossMaxHealth = definition.MaxHealth;
                nodeState.BossCurrentHealth = definition.MaxHealth;
                nodeState.BossAttack = definition.Attack;
                nodeState.BossDefense = definition.Defense;
                nodeState.BossState = BoardBossStateType.Untriggered;
            }
        }

        /// <summary>
        /// 结算一次共享战斗 tick
        /// 节点内所有存活 Agent 共同输出伤害，敌方若未死则同时反击所有在场 Agent
        /// </summary>
        public BoardCombatTickResult ResolveCombatTick(
            IReadOnlyList<BoardAgentState> participantAgents,
            BoardNodeRuntimeState nodeState,
            bool isBoss)
        {
            int defenderDefense = isBoss ? nodeState.BossDefense : nodeState.EnemyDefense;
            int defenderHealth = isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int totalDamageDealt = 0;
            int activeParticipantCount = 0;

            if (participantAgents != null)
            {
                for (int index = 0; index < participantAgents.Count; index++)
                {
                    BoardAgentState participantAgent = participantAgents[index];

                    if (participantAgent == null || !participantAgent.IsAlive)
                    {
                        continue;
                    }

                    totalDamageDealt += Mathf.Max(1, participantAgent.Attack - defenderDefense);
                    activeParticipantCount++;
                }
            }

            defenderHealth = Mathf.Max(0, defenderHealth - totalDamageDealt);

            if (isBoss)
            {
                nodeState.BossCurrentHealth = defenderHealth;
            }
            else
            {
                nodeState.EnemyCurrentHealth = defenderHealth;
            }

            bool defenderDefeated = defenderHealth <= 0;
            int totalDamageTaken = 0;
            int downedAgentCount = 0;

            if (!defenderDefeated && participantAgents != null)
            {
                int enemyAttack = isBoss ? nodeState.BossAttack : nodeState.EnemyAttack;

                for (int index = 0; index < participantAgents.Count; index++)
                {
                    BoardAgentState participantAgent = participantAgents[index];

                    if (participantAgent == null || !participantAgent.IsAlive)
                    {
                        continue;
                    }

                    int damageTaken = Mathf.Max(1, enemyAttack - participantAgent.Defense);
                    participantAgent.CurrentHealth = Mathf.Max(0, participantAgent.CurrentHealth - damageTaken);
                    totalDamageTaken += damageTaken;

                    if (!participantAgent.IsAlive)
                    {
                        downedAgentCount++;
                    }
                }
            }

            if (isBoss)
            {
                nodeState.BossState = defenderDefeated ? BoardBossStateType.Defeated : BoardBossStateType.Engaged;
            }
            else
            {
                nodeState.EnemyState = defenderDefeated ? BoardEnemyStateType.Cleared : BoardEnemyStateType.Engaged;
            }

            return new BoardCombatTickResult(
                totalDamageDealt,
                totalDamageTaken,
                defenderDefeated,
                downedAgentCount,
                activeParticipantCount);
        }
    }

    /// <summary>
    /// 单次战斗 tick 的结算结果
    /// </summary>
    public sealed class BoardCombatTickResult
    {
        public BoardCombatTickResult(
            int totalDamageDealt,
            int totalDamageTaken,
            bool encounterDefeated,
            int downedAgentCount,
            int activeParticipantCount)
        {
            TotalDamageDealt = totalDamageDealt;
            TotalDamageTaken = totalDamageTaken;
            EncounterDefeated = encounterDefeated;
            DownedAgentCount = Mathf.Max(0, downedAgentCount);
            ActiveParticipantCount = Mathf.Max(0, activeParticipantCount);
        }

        public int TotalDamageDealt { get; }
        public int TotalDamageTaken { get; }
        public bool EncounterDefeated { get; }
        public int DownedAgentCount { get; }
        public int ActiveParticipantCount { get; }
    }
}

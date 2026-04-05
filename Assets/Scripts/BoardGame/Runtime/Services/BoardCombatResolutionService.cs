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
        /// 结算一次战斗 tick
        /// 规则为“AI 先手，若敌人未死则敌人反击”
        /// </summary>
        public BoardCombatTickResult ResolveCombatTick(BoardAgentState agentState, BoardNodeRuntimeState nodeState, bool isBoss)
        {
            int defenderDefense = isBoss ? nodeState.BossDefense : nodeState.EnemyDefense;
            int defenderHealth = isBoss ? nodeState.BossCurrentHealth : nodeState.EnemyCurrentHealth;
            int attackerDamage = Mathf.Max(1, agentState.Attack - defenderDefense);

            // 先处理 AI 对敌方的伤害
            defenderHealth = Mathf.Max(0, defenderHealth - attackerDamage);

            if (isBoss)
            {
                nodeState.BossCurrentHealth = defenderHealth;
            }
            else
            {
                nodeState.EnemyCurrentHealth = defenderHealth;
            }

            bool defenderDefeated = defenderHealth <= 0;
            int agentDamageTaken = 0;

            if (!defenderDefeated)
            {
                // 只有敌方存活时，才会触发反击
                int enemyAttack = isBoss ? nodeState.BossAttack : nodeState.EnemyAttack;
                agentDamageTaken = Mathf.Max(1, enemyAttack - agentState.Defense);
                agentState.CurrentHealth = Mathf.Max(0, agentState.CurrentHealth - agentDamageTaken);
            }

            bool agentDefeated = agentState.CurrentHealth <= 0;

            if (isBoss)
            {
                nodeState.BossState = defenderDefeated ? BoardBossStateType.Defeated : BoardBossStateType.Engaged;
            }
            else
            {
                nodeState.EnemyState = defenderDefeated ? BoardEnemyStateType.Cleared : BoardEnemyStateType.Engaged;
            }

            return new BoardCombatTickResult(attackerDamage, agentDamageTaken, defenderDefeated, agentDefeated);
        }
    }

    /// <summary>
    /// 单次战斗 tick 的结算结果
    /// </summary>
    public sealed class BoardCombatTickResult
    {
        public BoardCombatTickResult(int damageDealt, int damageTaken, bool encounterDefeated, bool agentDefeated)
        {
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
            EncounterDefeated = encounterDefeated;
            AgentDefeated = agentDefeated;
        }

        public int DamageDealt { get; }
        public int DamageTaken { get; }
        public bool EncounterDefeated { get; }
        public bool AgentDefeated { get; }
    }
}

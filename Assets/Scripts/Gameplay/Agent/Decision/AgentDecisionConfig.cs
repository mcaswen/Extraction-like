using UnityEngine;

namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 目标决策配置
    /// 当前只保留单一决策性格，用风险公式和可调权重控制目标选择
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_DecisionConfig",
        menuName = "SO/Agent/DecisionConfig")]
    public sealed class AgentDecisionConfig : ScriptableObject
    {
        [Header("风险公式")]
        [SerializeField] private float _riskScale = 100f;
        [SerializeField] private float _defaultDefense = 0f;
        [SerializeField] private float _enemyPathInfluenceRadius = 8f;
        [SerializeField] private float _enemyLevel2MaxHealth = 140f;
        [SerializeField] private float _enemyLevel3MaxHealth = 260f;
        [SerializeField] private float _enemyLevel4MaxHealth = 520f;

        [Header("风险阈值")]
        [SerializeField] private float _resourceRiskThreshold = 2.2f;
        [SerializeField] private float _enemySourceRiskThreshold = 2.8f;
        [SerializeField] private float _combatRiskThreshold = 3.6f;
        [SerializeField] private float _extractionRiskThreshold = 999f;

        [Header("生命约束")]
        [SerializeField, Range(0f, 1f)] private float _minCombatHealthRatio = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _lowHealthExtractionRatio = 0.35f;

        [Header("评分权重")]
        [SerializeField] private float _resourceBaseScore = 85f;
        [SerializeField] private float _enemySourceBaseScore = 62f;
        [SerializeField] private float _combatBaseScore = 72f;
        [SerializeField] private float _extractionBaseScore = 36f;
        [SerializeField] private float _riskPenaltyWeight = 24f;
        [SerializeField] private float _distancePenaltyWeight = 0.08f;
        [SerializeField] private float _currentTargetBonus = 12f;
        [SerializeField] private float _lowHealthExtractionBonus = 72f;

        public float RiskScale => Mathf.Max(0f, _riskScale);
        public float DefaultDefense => Mathf.Max(0f, _defaultDefense);
        public float EnemyPathInfluenceRadius => Mathf.Max(0f, _enemyPathInfluenceRadius);
        public float ResourceRiskThreshold => Mathf.Max(0f, _resourceRiskThreshold);
        public float EnemySourceRiskThreshold => Mathf.Max(0f, _enemySourceRiskThreshold);
        public float CombatRiskThreshold => Mathf.Max(0f, _combatRiskThreshold);
        public float ExtractionRiskThreshold => Mathf.Max(0f, _extractionRiskThreshold);
        public float MinCombatHealthRatio => Mathf.Clamp01(_minCombatHealthRatio);
        public float LowHealthExtractionRatio => Mathf.Clamp01(_lowHealthExtractionRatio);
        public float ResourceBaseScore => _resourceBaseScore;
        public float EnemySourceBaseScore => _enemySourceBaseScore;
        public float CombatBaseScore => _combatBaseScore;
        public float ExtractionBaseScore => _extractionBaseScore;
        public float RiskPenaltyWeight => Mathf.Max(0f, _riskPenaltyWeight);
        public float DistancePenaltyWeight => Mathf.Max(0f, _distancePenaltyWeight);
        public float CurrentTargetBonus => _currentTargetBonus;
        public float LowHealthExtractionBonus => Mathf.Max(0f, _lowHealthExtractionBonus);

        /// <summary>
        /// 根据敌人最大生命值估算策划公式中的敌人等级 L
        /// </summary>
        /// <param name="enemyMaxHealth"></param>
        /// <returns></returns>
        public int EstimateEnemyLevel(float enemyMaxHealth)
        {
            float maxHealth = Mathf.Max(1f, enemyMaxHealth);
            if (maxHealth >= Mathf.Max(1f, _enemyLevel4MaxHealth))
                return 4;

            if (maxHealth >= Mathf.Max(1f, _enemyLevel3MaxHealth))
                return 3;

            if (maxHealth >= Mathf.Max(1f, _enemyLevel2MaxHealth))
                return 2;

            return 1;
        }

        /// <summary>
        /// 返回指定候选类型可接受的风险阈值
        /// </summary>
        /// <param name="targetKind"></param>
        /// <returns></returns>
        public float GetRiskThreshold(AgentDecisionTargetKind targetKind)
        {
            switch (targetKind)
            {
                case AgentDecisionTargetKind.Resource:
                    return ResourceRiskThreshold;
                case AgentDecisionTargetKind.EnemySource:
                    return EnemySourceRiskThreshold;
                case AgentDecisionTargetKind.ActiveEnemy:
                    return CombatRiskThreshold;
                case AgentDecisionTargetKind.Extraction:
                    return ExtractionRiskThreshold;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 返回指定候选类型的基础收益分
        /// </summary>
        /// <param name="targetKind"></param>
        /// <returns></returns>
        public float GetBaseScore(AgentDecisionTargetKind targetKind)
        {
            switch (targetKind)
            {
                case AgentDecisionTargetKind.Resource:
                    return ResourceBaseScore;
                case AgentDecisionTargetKind.EnemySource:
                    return EnemySourceBaseScore;
                case AgentDecisionTargetKind.ActiveEnemy:
                    return CombatBaseScore;
                case AgentDecisionTargetKind.Extraction:
                    return ExtractionBaseScore;
                default:
                    return 0f;
            }
        }

        private void OnValidate()
        {
            _riskScale = Mathf.Max(0f, _riskScale);
            _defaultDefense = Mathf.Max(0f, _defaultDefense);
            _enemyPathInfluenceRadius = Mathf.Max(0f, _enemyPathInfluenceRadius);
            _enemyLevel2MaxHealth = Mathf.Max(1f, _enemyLevel2MaxHealth);
            _enemyLevel3MaxHealth = Mathf.Max(_enemyLevel2MaxHealth, _enemyLevel3MaxHealth);
            _enemyLevel4MaxHealth = Mathf.Max(_enemyLevel3MaxHealth, _enemyLevel4MaxHealth);
            _resourceRiskThreshold = Mathf.Max(0f, _resourceRiskThreshold);
            _enemySourceRiskThreshold = Mathf.Max(0f, _enemySourceRiskThreshold);
            _combatRiskThreshold = Mathf.Max(0f, _combatRiskThreshold);
            _extractionRiskThreshold = Mathf.Max(0f, _extractionRiskThreshold);
            _riskPenaltyWeight = Mathf.Max(0f, _riskPenaltyWeight);
            _distancePenaltyWeight = Mathf.Max(0f, _distancePenaltyWeight);
            _lowHealthExtractionBonus = Mathf.Max(0f, _lowHealthExtractionBonus);
        }
    }
}

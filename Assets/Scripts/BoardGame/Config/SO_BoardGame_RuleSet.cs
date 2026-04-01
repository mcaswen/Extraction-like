using System;
using System.Collections.Generic;
using BoardGame.Runtime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoardGame.Config
{
    /// <summary>
    /// 原型规则与数值配置
    /// 将角色、移动、搜索、战斗、撤离等关键参数集中为 ScriptableObject
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_BoardGame_RuleSet",
        menuName = "BoardGame/Rule Set")]
    public sealed class SO_BoardGame_RuleSet : ScriptableObject
    {
        [SerializeField] private string _ruleSetId = "sample_rule_set"; // 规则集唯一 ID
        [SerializeField] private BoardAgentStatDefinition _agentStats = new BoardAgentStatDefinition(); // 角色基础属性
        [SerializeField] private BoardAutonomousRuleDefinition _autonomousRules = new BoardAutonomousRuleDefinition(); // AI 默认决策规则
        [SerializeField] private BoardMovementRuleDefinition _movementRules = new BoardMovementRuleDefinition(); // 沿边移动规则
        [SerializeField] private BoardSearchDurationDefinition _searchDurations = new BoardSearchDurationDefinition(); // 各档资源点搜索时长
        [SerializeField] private BoardCombatRuleDefinition _combatRules = new BoardCombatRuleDefinition(); // 战斗 tick 与敌人模板
        [SerializeField] private BoardExtractRuleDefinition _extractRules = new BoardExtractRuleDefinition(); // 撤离规则
        [SerializeField] private BoardProgressionRuleDefinition _progressionRules = new BoardProgressionRuleDefinition(); // 经验与升级规则

        public string RuleSetId => _ruleSetId;
        public BoardAgentStatDefinition AgentStats => _agentStats;
        public BoardAutonomousRuleDefinition AutonomousRules => _autonomousRules;
        public BoardMovementRuleDefinition MovementRules => _movementRules;
        public BoardSearchDurationDefinition SearchDurations => _searchDurations;
        public BoardCombatRuleDefinition CombatRules => _combatRules;
        public BoardExtractRuleDefinition ExtractRules => _extractRules;
        public BoardProgressionRuleDefinition ProgressionRules => _progressionRules;

        /// <summary>
        /// 按当前策划预设写入规则 SO
        /// </summary>
        public void ApplyPlannerPreset()
        {
            _ruleSetId = BoardGamePlannerPresetConfig.RuleSetId;
            _agentStats = BoardGamePlannerPresetConfig.CreateAgentStats();
            _autonomousRules = BoardGamePlannerPresetConfig.CreateAutonomousRules();
            _movementRules = BoardGamePlannerPresetConfig.CreateMovementRules();
            _searchDurations = BoardGamePlannerPresetConfig.CreateSearchDurations();
            _combatRules = BoardGamePlannerPresetConfig.CreateCombatRules();
            _extractRules = BoardGamePlannerPresetConfig.CreateExtractRules();
            _progressionRules = BoardGamePlannerPresetConfig.CreateProgressionRules();
            MarkDirty();
        }

        [ContextMenu("Apply Planner Rule Preset")]
        private void ApplyPlannerPresetFromContextMenu()
        {
            ApplyPlannerPreset();
        }

        [ContextMenu("Fill Sample Rules")]
        private void FillWithSampleRules()
        {
            ApplyPlannerPreset();
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// 角色基础面板配置
    /// </summary>
    [Serializable]
    public sealed class BoardAgentStatDefinition
    {
        [SerializeField] private int _maxHealth = 100; // 最大生命值
        [SerializeField] private int _attack = 7; // 基础攻击力
        [SerializeField] private int _defense; // 基础防御力
        [SerializeField] private int _startingHealingPotionCount = 1; // 开局自带血瓶数量

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = Mathf.Max(1, value);
        }

        public int Attack
        {
            get => _attack;
            set => _attack = Mathf.Max(1, value);
        }

        public int Defense
        {
            get => _defense;
            set => _defense = Mathf.Max(0, value);
        }

        public int StartingHealingPotionCount
        {
            get => _startingHealingPotionCount;
            set => _startingHealingPotionCount = Mathf.Max(0, value);
        }
    }

    /// <summary>
    /// AI 默认行为规则
    /// </summary>
    [Serializable]
    public sealed class BoardAutonomousRuleDefinition
    {
        [SerializeField] private int _lowHealthRetreatThreshold = 35; // 预留字段 当前笨 AI 默认决策未使用
        [SerializeField] private float _reevaluateIntervalSeconds = 1.5f; // 默认逻辑重新评估目标的时间间隔

        public int LowHealthRetreatThreshold
        {
            get => _lowHealthRetreatThreshold;
            set => _lowHealthRetreatThreshold = Mathf.Max(1, value);
        }

        public float ReevaluateIntervalSeconds
        {
            get => _reevaluateIntervalSeconds;
            set => _reevaluateIntervalSeconds = Mathf.Max(0.1f, value);
        }
    }

    /// <summary>
    /// 移动换算规则
    /// </summary>
    [Serializable]
    public sealed class BoardMovementRuleDefinition
    {
        [SerializeField] private float _secondsPerLengthUnit = 1f; // 每单位边长度对应的移动秒数

        public float SecondsPerLengthUnit
        {
            get => _secondsPerLengthUnit;
            set => _secondsPerLengthUnit = Mathf.Max(0.1f, value);
        }
    }

    /// <summary>
    /// 资源搜索时长配置
    /// </summary>
    [Serializable]
    public sealed class BoardSearchDurationDefinition
    {
        [SerializeField] private float _lowTierSeconds = 3f; // 低级资源点搜索总时长
        [SerializeField] private float _mediumTierSeconds = 5f; // 中级资源点搜索总时长
        [SerializeField] private float _highTierSeconds = 7f; // 高级资源点搜索总时长

        public float LowTierSeconds
        {
            get => _lowTierSeconds;
            set => _lowTierSeconds = Mathf.Max(0.1f, value);
        }

        public float MediumTierSeconds
        {
            get => _mediumTierSeconds;
            set => _mediumTierSeconds = Mathf.Max(0.1f, value);
        }

        public float HighTierSeconds
        {
            get => _highTierSeconds;
            set => _highTierSeconds = Mathf.Max(0.1f, value);
        }
    }

    /// <summary>
    /// 战斗规则与敌人模板配置
    /// </summary>
    [Serializable]
    public sealed class BoardCombatRuleDefinition
    {
        [SerializeField] private float _tickIntervalSeconds = 0.3f; // 战斗结算 tick 间隔
        [SerializeField] private List<BoardEnemyStatDefinition> _enemyDefinitions = new List<BoardEnemyStatDefinition>(); // 普通敌人模板列表
        [SerializeField] private BoardBossStatDefinition _bossDefinition = new BoardBossStatDefinition(24, 7, 0, 1); // Boss 模板

        public float TickIntervalSeconds
        {
            get => _tickIntervalSeconds;
            set => _tickIntervalSeconds = Mathf.Max(0.1f, value);
        }

        public List<BoardEnemyStatDefinition> EnemyDefinitions
        {
            get => _enemyDefinitions;
            set => _enemyDefinitions = value ?? new List<BoardEnemyStatDefinition>();
        }

        public BoardBossStatDefinition BossDefinition
        {
            get => _bossDefinition;
            set => _bossDefinition = value ?? new BoardBossStatDefinition(24, 7, 0, 1);
        }
    }

    /// <summary>
    /// 普通敌人模板定义
    /// </summary>
    [Serializable]
    public sealed class BoardEnemyStatDefinition
    {
        [SerializeField] private BoardDangerTier _dangerTier = BoardDangerTier.Low; // 对应危险等级
        [SerializeField] private int _maxHealth = 10; // 最大生命值
        [SerializeField] private int _attack = 3; // 攻击力
        [SerializeField] private int _defenseMin; // 防御随机下限
        [SerializeField] private int _defenseMax = 1; // 防御随机上限

        public BoardEnemyStatDefinition(BoardDangerTier dangerTier, int maxHealth, int attack, int defenseMin, int defenseMax)
        {
            _dangerTier = dangerTier;
            _maxHealth = maxHealth;
            _attack = attack;
            _defenseMin = defenseMin;
            _defenseMax = defenseMax;
        }

        public BoardDangerTier DangerTier => _dangerTier;
        public int MaxHealth => _maxHealth;
        public int Attack => _attack;
        public int DefenseMin => _defenseMin;
        public int DefenseMax => _defenseMax;
    }

    /// <summary>
    /// Boss 模板定义
    /// </summary>
    [Serializable]
    public sealed class BoardBossStatDefinition
    {
        [SerializeField] private int _maxHealth = 24; // 最大生命值
        [SerializeField] private int _attack = 7; // 攻击力
        [SerializeField] private int _defenseMin; // 防御随机下限
        [SerializeField] private int _defenseMax = 1; // 防御随机上限

        public BoardBossStatDefinition(int maxHealth, int attack, int defenseMin, int defenseMax)
        {
            _maxHealth = maxHealth;
            _attack = attack;
            _defenseMin = defenseMin;
            _defenseMax = defenseMax;
        }

        public int MaxHealth => _maxHealth;
        public int Attack => _attack;
        public int DefenseMin => _defenseMin;
        public int DefenseMax => _defenseMax;
    }

    /// <summary>
    /// 撤离行为配置
    /// </summary>
    [Serializable]
    public sealed class BoardExtractRuleDefinition
    {
        [SerializeField] private float _durationSeconds = 4f; // 撤离总时长
        [SerializeField] private bool _canInterrupt = true; // 撤离过程是否允许被玩家打断
        [SerializeField] private bool _preserveProgressOnInterrupt = true; // 被打断后是否保留撤离进度

        public float DurationSeconds
        {
            get => _durationSeconds;
            set => _durationSeconds = Mathf.Max(0.1f, value);
        }

        public bool CanInterrupt
        {
            get => _canInterrupt;
            set => _canInterrupt = value;
        }

        public bool PreserveProgressOnInterrupt
        {
            get => _preserveProgressOnInterrupt;
            set => _preserveProgressOnInterrupt = value;
        }
    }

    /// <summary>
    /// 经验与升级规则
    /// </summary>
    [Serializable]
    public sealed class BoardProgressionRuleDefinition
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private int _startingLevel = 1;
        [SerializeField] private int _startingRequiredExperience = 50;
        [SerializeField] private int _requiredExperienceGrowthPerLevel = 25;
        [SerializeField] private int _choicesPerLevel = 3;
        [SerializeField] private int _bossExperienceValue = 120;
        [SerializeField] private List<BoardEncounterExperienceDefinition> _encounterExperienceDefinitions =
            new List<BoardEncounterExperienceDefinition>();
        [SerializeField] private List<BoardLevelUpBuffDefinition> _buffDefinitions =
            new List<BoardLevelUpBuffDefinition>();

        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public int StartingLevel
        {
            get => _startingLevel;
            set => _startingLevel = Mathf.Max(1, value);
        }

        public int StartingRequiredExperience
        {
            get => _startingRequiredExperience;
            set => _startingRequiredExperience = Mathf.Max(1, value);
        }

        public int RequiredExperienceGrowthPerLevel
        {
            get => _requiredExperienceGrowthPerLevel;
            set => _requiredExperienceGrowthPerLevel = Mathf.Max(1, value);
        }

        public int ChoicesPerLevel
        {
            get => _choicesPerLevel;
            set => _choicesPerLevel = Mathf.Max(1, value);
        }

        public int BossExperienceValue
        {
            get => _bossExperienceValue;
            set => _bossExperienceValue = Mathf.Max(0, value);
        }

        public List<BoardEncounterExperienceDefinition> EncounterExperienceDefinitions
        {
            get => _encounterExperienceDefinitions;
            set => _encounterExperienceDefinitions = value ?? new List<BoardEncounterExperienceDefinition>();
        }

        public List<BoardLevelUpBuffDefinition> BuffDefinitions
        {
            get => _buffDefinitions;
            set => _buffDefinitions = value ?? new List<BoardLevelUpBuffDefinition>();
        }
    }

    /// <summary>
    /// 敌人与 Boss 的经验值配置
    /// </summary>
    [Serializable]
    public sealed class BoardEncounterExperienceDefinition
    {
        [SerializeField] private bool _isBoss;
        [SerializeField] private BoardDangerTier _dangerTier = BoardDangerTier.Low;
        [SerializeField] private int _experienceValue = 10;

        public BoardEncounterExperienceDefinition(bool isBoss, BoardDangerTier dangerTier, int experienceValue)
        {
            _isBoss = isBoss;
            _dangerTier = dangerTier;
            _experienceValue = Mathf.Max(0, experienceValue);
        }

        public bool IsBoss => _isBoss;
        public BoardDangerTier DangerTier => _dangerTier;
        public int ExperienceValue => _experienceValue;
    }

    /// <summary>
    /// 升级增益值范围配置
    /// </summary>
    [Serializable]
    public sealed class BoardLevelUpBuffDefinition
    {
        [SerializeField] private BoardLevelUpBuffType _buffType = BoardLevelUpBuffType.AttackFlat;
        [SerializeField] private int _minValue = 1;
        [SerializeField] private int _maxValue = 3;

        public BoardLevelUpBuffDefinition(BoardLevelUpBuffType buffType, int minValue, int maxValue)
        {
            _buffType = buffType;
            _minValue = Mathf.Max(1, minValue);
            _maxValue = Mathf.Max(_minValue, maxValue);
        }

        public BoardLevelUpBuffType BuffType => _buffType;
        public int MinValue => _minValue;
        public int MaxValue => _maxValue;
    }
}

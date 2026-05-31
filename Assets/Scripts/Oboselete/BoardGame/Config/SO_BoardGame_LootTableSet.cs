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
    /// 掉落、道具与战利品配置
    /// 负责资源点物资表、敌人掉落表和血瓶定义
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_BoardGame_LootTableSet",
        menuName = "BoardGame/Loot Table")]
    public sealed class SO_BoardGame_LootTableSet : ScriptableObject
    {
        [SerializeField] private string _lootSetId = "sample_loot_set";
        [SerializeField] private List<BoardItemDefinition> _itemDefinitions = new List<BoardItemDefinition>();
        [SerializeField] private List<BoardResourceLootPoolDefinition> _resourcePools = new List<BoardResourceLootPoolDefinition>();
        [SerializeField] private List<BoardEncounterLootPoolDefinition> _encounterPools = new List<BoardEncounterLootPoolDefinition>();

        public string LootSetId => _lootSetId;
        public IReadOnlyList<BoardItemDefinition> ItemDefinitions => _itemDefinitions;
        public IReadOnlyList<BoardResourceLootPoolDefinition> ResourcePools => _resourcePools;
        public IReadOnlyList<BoardEncounterLootPoolDefinition> EncounterPools => _encounterPools;

        /// <summary>
        /// 按当前策划预设写入掉落 SO
        /// </summary>
        public void ApplyPlannerPreset()
        {
            _lootSetId = BoardGamePlannerPresetConfig.LootSetId;
            _itemDefinitions = BoardGamePlannerPresetConfig.CreateItemDefinitions();
            _resourcePools = BoardGamePlannerPresetConfig.CreateResourcePools();
            _encounterPools = BoardGamePlannerPresetConfig.CreateEncounterPools();
            MarkDirty();
        }

        [ContextMenu("Apply Planner Loot Preset")]
        private void ApplyPlannerPresetFromContextMenu()
        {
            ApplyPlannerPreset();
        }

        [ContextMenu("Fill Sample Loot")]
        private void FillWithSampleLoot()
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
    /// 单个物品模板定义
    /// </summary>
    [Serializable]
    public sealed class BoardItemDefinition
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [SerializeField] private BoardItemCategory _itemCategory;
        [SerializeField] private BoardItemRarity _itemRarity;
        [SerializeField] private int _minValue;
        [SerializeField] private int _maxValue;
        [SerializeField] private float _capacityCost;
        [SerializeField] private float _revealDurationSeconds;
        [SerializeField] private int _experienceValue;
        [SerializeField] private BoardConsumableType _consumableType;
        [SerializeField] private int _consumeValue;

        public BoardItemDefinition(
            string itemId,
            string displayName,
            BoardItemCategory itemCategory,
            BoardItemRarity itemRarity,
            int minValue,
            int maxValue,
            float capacityCost,
            float revealDurationSeconds,
            int experienceValue = 0,
            BoardConsumableType consumableType = BoardConsumableType.None,
            int consumeValue = 0)
        {
            _itemId = itemId;
            _displayName = displayName;
            _itemCategory = itemCategory;
            _itemRarity = itemRarity;
            _minValue = minValue;
            _maxValue = maxValue;
            _capacityCost = Mathf.Max(0f, capacityCost);
            _revealDurationSeconds = Mathf.Max(0.01f, revealDurationSeconds);
            _experienceValue = Mathf.Max(0, experienceValue);
            _consumableType = consumableType;
            _consumeValue = consumeValue;
        }

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public BoardItemCategory ItemCategory => _itemCategory;
        public BoardItemRarity ItemRarity => _itemRarity;
        public int MinValue => _minValue;
        public int MaxValue => _maxValue;
        public float CapacityCost => _capacityCost > 0f
            ? _capacityCost
            : GetDefaultCapacityCost(_itemRarity);
        public float RevealDurationSeconds => _revealDurationSeconds > 0f
            ? _revealDurationSeconds
            : GetDefaultRevealDurationSeconds(_itemRarity);
        public int ExperienceValue => _experienceValue;
        public BoardConsumableType ConsumableType => _consumableType;
        public int ConsumeValue => _consumeValue;

        /// <summary>
        /// 获取关闭背包系统时使用的默认数字容量
        /// </summary>
        /// <param name="rarity"></param>
        /// <returns></returns>
        public static float GetDefaultCapacityCost(BoardItemRarity rarity)
        {
            return rarity switch
            {
                BoardItemRarity.Common => 0.1f,
                BoardItemRarity.Uncommon => 0.5f,
                BoardItemRarity.Rare => 0.5f,
                BoardItemRarity.Epic => 1f,
                BoardItemRarity.Legendary => 2f,
                _ => 0.1f
            };
        }

        /// <summary>
        /// 获取物资揭露流程使用的默认时长
        /// </summary>
        /// <param name="rarity"></param>
        /// <returns></returns>
        public static float GetDefaultRevealDurationSeconds(BoardItemRarity rarity)
        {
            return rarity switch
            {
                BoardItemRarity.Common => 0.45f,
                BoardItemRarity.Uncommon => 0.75f,
                BoardItemRarity.Rare => 1.1f,
                BoardItemRarity.Epic => 1.55f,
                BoardItemRarity.Legendary => 2.1f,
                _ => 0.45f
            };
        }
    }

    /// <summary>
    /// 带权重的物品引用
    /// </summary>
    [Serializable]
    public sealed class BoardWeightedItemReference
    {
        [SerializeField] private string _itemId;
        [SerializeField] private float _weight = 1f;

        public BoardWeightedItemReference(string itemId, float weight)
        {
            _itemId = itemId;
            _weight = weight;
        }

        public string ItemId => _itemId;
        public float Weight => _weight;
    }

    /// <summary>
    /// 资源点掉落池定义
    /// </summary>
    [Serializable]
    public sealed class BoardResourceLootPoolDefinition
    {
        [SerializeField] private BoardResourceTier _resourceTier = BoardResourceTier.Low;
        [SerializeField] private int _minRollCount = 1;
        [SerializeField] private int _maxRollCount = 1;
        [SerializeField] private List<BoardWeightedItemReference> _entries = new List<BoardWeightedItemReference>();

        public BoardResourceLootPoolDefinition(
            BoardResourceTier resourceTier,
            int minRollCount,
            int maxRollCount,
            List<BoardWeightedItemReference> entries)
        {
            _resourceTier = resourceTier;
            _minRollCount = minRollCount;
            _maxRollCount = maxRollCount;
            _entries = entries ?? new List<BoardWeightedItemReference>();
        }

        public BoardResourceTier ResourceTier => _resourceTier;
        public int MinRollCount => _minRollCount;
        public int MaxRollCount => _maxRollCount;
        public IReadOnlyList<BoardWeightedItemReference> Entries => _entries;
    }

    /// <summary>
    /// 敌人和 Boss 掉落池定义
    /// </summary>
    [Serializable]
    public sealed class BoardEncounterLootPoolDefinition
    {
        [SerializeField] private bool _isBoss;
        [SerializeField] private BoardDangerTier _dangerTier = BoardDangerTier.Low;
        [SerializeField] private int _minRollCount = 1;
        [SerializeField] private int _maxRollCount = 1;
        [SerializeField] private float _healingPotionChance = 0.1f;
        [SerializeField] private List<BoardWeightedItemReference> _entries = new List<BoardWeightedItemReference>();

        public BoardEncounterLootPoolDefinition(
            bool isBoss,
            BoardDangerTier dangerTier,
            int minRollCount,
            int maxRollCount,
            float healingPotionChance,
            List<BoardWeightedItemReference> entries)
        {
            _isBoss = isBoss;
            _dangerTier = dangerTier;
            _minRollCount = minRollCount;
            _maxRollCount = maxRollCount;
            _healingPotionChance = healingPotionChance;
            _entries = entries ?? new List<BoardWeightedItemReference>();
        }

        public bool IsBoss => _isBoss;
        public BoardDangerTier DangerTier => _dangerTier;
        public int MinRollCount => _minRollCount;
        public int MaxRollCount => _maxRollCount;
        public float HealingPotionChance => _healingPotionChance;
        public IReadOnlyList<BoardWeightedItemReference> Entries => _entries;
    }
}

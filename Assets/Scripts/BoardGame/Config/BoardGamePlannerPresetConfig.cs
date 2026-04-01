using System.Collections.Generic;
using BoardGame.Runtime;

namespace BoardGame.Config
{
    /// <summary>
    /// 当前策划数值预设
    /// 负责把策划表中的掉落与战斗数据整理成可写入 SO 的代码配置
    /// </summary>
    public static class BoardGamePlannerPresetConfig
    {
        // 规则集唯一 ID
        public const string RuleSetId = "planner_rule_set_v1";

        // 掉落表唯一 ID
        public const string LootSetId = "planner_loot_set_v1";

        // 当前策划固定地图预设的起点节点 ID
        public const string PlannerMapStartNodeId = "1";

        // 各品质策划期望价值
        // 绿色 20 蓝色 60 紫色 100 金色 400 红色 1500
        // 这些值已经隐含在对应的价值区间里

        // 当前原型中未由策划表直接给出的撤离默认参数
        public const float DefaultExtractDurationSeconds = 4f;
        public const bool DefaultExtractCanInterrupt = true;
        public const bool DefaultExtractPreserveProgressOnInterrupt = true;

        /// <summary>
        /// 生成角色基础属性
        /// </summary>
        public static BoardAgentStatDefinition CreateAgentStats()
        {
            return new BoardAgentStatDefinition
            {
                MaxHealth = 100,
                Attack = 7,
                Defense = 0,
                StartingHealingPotionCount = 1
            };
        }

        /// <summary>
        /// 生成 AI 默认决策规则
        /// 低血阈值字段当前仅保留配置位，默认决策未使用
        /// </summary>
        public static BoardAutonomousRuleDefinition CreateAutonomousRules()
        {
            return new BoardAutonomousRuleDefinition
            {
                LowHealthRetreatThreshold = 35,
                ReevaluateIntervalSeconds = 1.5f
            };
        }

        /// <summary>
        /// 生成移动换算规则
        /// 每单位长度耗时当前沿用原型值 1 05 秒
        /// </summary>
        public static BoardMovementRuleDefinition CreateMovementRules()
        {
            return new BoardMovementRuleDefinition
            {
                SecondsPerLengthUnit = 1.05f
            };
        }

        /// <summary>
        /// 生成资源搜索时长配置
        /// 低中高档搜索时长当前沿用原型值 3 5 7 秒
        /// </summary>
        public static BoardSearchDurationDefinition CreateSearchDurations()
        {
            return new BoardSearchDurationDefinition
            {
                LowTierSeconds = 3f,
                MediumTierSeconds = 5f,
                HighTierSeconds = 7f
            };
        }

        /// <summary>
        /// 生成战斗规则与敌人模板
        /// </summary>
        public static BoardCombatRuleDefinition CreateCombatRules()
        {
            return new BoardCombatRuleDefinition
            {
                TickIntervalSeconds = 1f,
                EnemyDefinitions = new List<BoardEnemyStatDefinition>
                {
                    new BoardEnemyStatDefinition(BoardDangerTier.Low, 13, 4, 1, 2),
                    new BoardEnemyStatDefinition(BoardDangerTier.Medium, 12, 6, 0, 1),
                    new BoardEnemyStatDefinition(BoardDangerTier.High, 20, 5, 0, 0)
                },
                BossDefinition = new BoardBossStatDefinition(24, 7, 0, 1)
            };
        }

        /// <summary>
        /// 生成撤离规则
        /// 当前三项沿用原型默认值
        /// </summary>
        public static BoardExtractRuleDefinition CreateExtractRules()
        {
            return new BoardExtractRuleDefinition
            {
                DurationSeconds = DefaultExtractDurationSeconds,
                CanInterrupt = DefaultExtractCanInterrupt,
                PreserveProgressOnInterrupt = DefaultExtractPreserveProgressOnInterrupt
            };
        }

        /// <summary>
        /// 生成经验与升级规则
        /// </summary>
        public static BoardProgressionRuleDefinition CreateProgressionRules()
        {
            return new BoardProgressionRuleDefinition
            {
                Enabled = true,
                StartingLevel = 1,
                StartingRequiredExperience = 50,
                RequiredExperienceGrowthPerLevel = 25,
                ChoicesPerLevel = 3,
                BossExperienceValue = 120,
                EncounterExperienceDefinitions = new List<BoardEncounterExperienceDefinition>
                {
                    new BoardEncounterExperienceDefinition(false, BoardDangerTier.Low, 20),
                    new BoardEncounterExperienceDefinition(false, BoardDangerTier.Medium, 35),
                    new BoardEncounterExperienceDefinition(false, BoardDangerTier.High, 55)
                },
                BuffDefinitions = new List<BoardLevelUpBuffDefinition>
                {
                    new BoardLevelUpBuffDefinition(BoardLevelUpBuffType.AttackFlat, 1, 3),
                    new BoardLevelUpBuffDefinition(BoardLevelUpBuffType.DefenseFlat, 1, 2),
                    new BoardLevelUpBuffDefinition(BoardLevelUpBuffType.MaxHealthFlat, 8, 18)
                }
            };
        }

        /// <summary>
        /// 生成全部物品模板
        /// </summary>
        public static List<BoardItemDefinition> CreateItemDefinitions()
        {
            return new List<BoardItemDefinition>
            {
                new BoardItemDefinition("loot_green", "Green Loot", BoardItemCategory.Loot, BoardItemRarity.Common, 10, 30, 0.45f, 6),
                new BoardItemDefinition("loot_blue", "Blue Loot", BoardItemCategory.Loot, BoardItemRarity.Uncommon, 40, 80, 0.75f, 12),
                new BoardItemDefinition("loot_purple", "Purple Loot", BoardItemCategory.Loot, BoardItemRarity.Rare, 80, 120, 1.1f, 18),
                new BoardItemDefinition("loot_gold", "Gold Loot", BoardItemCategory.Loot, BoardItemRarity.Epic, 300, 500, 1.55f, 30),
                new BoardItemDefinition("loot_red", "Red Loot", BoardItemCategory.Loot, BoardItemRarity.Legendary, 1000, 2000, 2.1f, 50),
                // 血瓶的结算价值按策划口径放在中级物资之上 高级物资之下
                new BoardItemDefinition("healing_potion", "Healing Potion", BoardItemCategory.Consumable, BoardItemRarity.Uncommon, 150, 250, 0.75f, 16, BoardConsumableType.HealingPotion, 10)
            };
        }

        /// <summary>
        /// 生成资源点掉落池
        /// </summary>
        public static List<BoardResourceLootPoolDefinition> CreateResourcePools()
        {
            return new List<BoardResourceLootPoolDefinition>
            {
                new BoardResourceLootPoolDefinition(BoardResourceTier.Low, 1, 3, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_green", 45f),
                    new BoardWeightedItemReference("loot_blue", 35f),
                    new BoardWeightedItemReference("loot_purple", 20f)
                }),
                new BoardResourceLootPoolDefinition(BoardResourceTier.Medium, 2, 4, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_green", 10f),
                    new BoardWeightedItemReference("loot_blue", 40f),
                    new BoardWeightedItemReference("loot_purple", 35f),
                    new BoardWeightedItemReference("loot_gold", 15f)
                }),
                new BoardResourceLootPoolDefinition(BoardResourceTier.High, 3, 5, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_blue", 30f),
                    new BoardWeightedItemReference("loot_purple", 30f),
                    new BoardWeightedItemReference("loot_gold", 25f),
                    new BoardWeightedItemReference("loot_red", 15f)
                })
            };
        }

        /// <summary>
        /// 生成敌人和 Boss 掉落池
        /// 小怪掉血瓶概率 10
        /// Boss 掉血瓶概率 30
        /// </summary>
        public static List<BoardEncounterLootPoolDefinition> CreateEncounterPools()
        {
            return new List<BoardEncounterLootPoolDefinition>
            {
                new BoardEncounterLootPoolDefinition(false, BoardDangerTier.Low, 1, 1, 0.1f, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_green", 65f),
                    new BoardWeightedItemReference("loot_blue", 35f)
                }),
                new BoardEncounterLootPoolDefinition(false, BoardDangerTier.Medium, 1, 1, 0.1f, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_blue", 60f),
                    new BoardWeightedItemReference("loot_purple", 40f)
                }),
                new BoardEncounterLootPoolDefinition(false, BoardDangerTier.High, 1, 2, 0.1f, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_purple", 55f),
                    new BoardWeightedItemReference("loot_gold", 45f)
                }),
                new BoardEncounterLootPoolDefinition(true, BoardDangerTier.High, 2, 3, 0.3f, new List<BoardWeightedItemReference>
                {
                    new BoardWeightedItemReference("loot_gold", 60f),
                    new BoardWeightedItemReference("loot_red", 40f)
                })
            };
        }

        /// <summary>
        /// 生成策划固定地图的节点元数据
        /// 位置由场景摆点导入 这里只定义类型与等级
        /// </summary>
        public static List<BoardPlannerMapNodePresetDefinition> CreatePlannerMapNodePresets()
        {
            return new List<BoardPlannerMapNodePresetDefinition>
            {
                new BoardPlannerMapNodePresetDefinition("1", BoardNodeType.Start),
                new BoardPlannerMapNodePresetDefinition("2", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Low),
                new BoardPlannerMapNodePresetDefinition("3", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Medium),
                new BoardPlannerMapNodePresetDefinition("4", BoardNodeType.Resource, BoardResourceTier.Medium),
                new BoardPlannerMapNodePresetDefinition("5", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Low),
                new BoardPlannerMapNodePresetDefinition("6", BoardNodeType.Boss, BoardResourceTier.None, BoardDangerTier.High),
                new BoardPlannerMapNodePresetDefinition("7", BoardNodeType.Resource, BoardResourceTier.High),
                new BoardPlannerMapNodePresetDefinition("8", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Low),
                new BoardPlannerMapNodePresetDefinition("9", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Low),
                new BoardPlannerMapNodePresetDefinition("10", BoardNodeType.Resource, BoardResourceTier.Low),
                new BoardPlannerMapNodePresetDefinition("11", BoardNodeType.Enemy, BoardResourceTier.None, BoardDangerTier.Medium),
                new BoardPlannerMapNodePresetDefinition("12", BoardNodeType.Resource, BoardResourceTier.Medium),
                new BoardPlannerMapNodePresetDefinition("13", BoardNodeType.Boss, BoardResourceTier.None, BoardDangerTier.High),
                new BoardPlannerMapNodePresetDefinition("14", BoardNodeType.Resource, BoardResourceTier.Low),
                new BoardPlannerMapNodePresetDefinition("15", BoardNodeType.Resource, BoardResourceTier.High),
                new BoardPlannerMapNodePresetDefinition("0", BoardNodeType.Extract)
            };
        }

        /// <summary>
        /// 生成策划固定地图的连边定义
        /// </summary>
        public static List<BoardMapEdgeDefinition> CreatePlannerMapEdgePresets()
        {
            return new List<BoardMapEdgeDefinition>
            {
                new BoardMapEdgeDefinition("01", "1", "2", 2f),
                new BoardMapEdgeDefinition("02", "2", "3", 2f),
                new BoardMapEdgeDefinition("03", "3", "5", 2f),
                new BoardMapEdgeDefinition("04", "5", "4", 2f),
                new BoardMapEdgeDefinition("05", "5", "6", 2f),
                new BoardMapEdgeDefinition("06", "6", "7", 2f),
                new BoardMapEdgeDefinition("07", "1", "8", 2f),
                new BoardMapEdgeDefinition("08", "3", "9", 2f),
                new BoardMapEdgeDefinition("09", "8", "10", 2f),
                new BoardMapEdgeDefinition("10", "8", "11", 2f),
                new BoardMapEdgeDefinition("11", "11", "0", 3f),
                new BoardMapEdgeDefinition("12", "9", "0", 3f),
                new BoardMapEdgeDefinition("13", "9", "12", 3f),
                new BoardMapEdgeDefinition("14", "11", "14", 2f),
                new BoardMapEdgeDefinition("15", "10", "13", 2f),
                new BoardMapEdgeDefinition("16", "13", "15", 2f)
            };
        }
    }

    /// <summary>
    /// 策划固定地图节点预设
    /// 按节点 ID 提供类型与等级
    /// </summary>
    public readonly struct BoardPlannerMapNodePresetDefinition
    {
        public BoardPlannerMapNodePresetDefinition(
            string nodeId,
            BoardNodeType nodeType,
            BoardResourceTier resourceTier = BoardResourceTier.None,
            BoardDangerTier dangerTier = BoardDangerTier.None,
            string description = "")
        {
            NodeId = nodeId;
            NodeType = nodeType;
            ResourceTier = resourceTier;
            DangerTier = dangerTier;
            Description = description;
        }

        public string NodeId { get; }
        public BoardNodeType NodeType { get; }
        public BoardResourceTier ResourceTier { get; }
        public BoardDangerTier DangerTier { get; }
        public string Description { get; }
    }
}

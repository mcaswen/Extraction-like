using UnityEngine;

namespace BoardGame.Runtime
{
    /// <summary>
    /// 原型共用枚举与轻量级辅助方法
    /// </summary>
    public static class BoardGameTypes
    {
        /// <summary>
        /// 获取动作类型的中文标签
        /// </summary>
        public static string GetActionLabel(BoardActionType actionType)
        {
            switch (actionType)
            {
                case BoardActionType.Idle:
                    return "Idle";
                case BoardActionType.Moving:
                    return "Moving";
                case BoardActionType.Searching:
                    return "Searching";
                case BoardActionType.FightingEnemy:
                    return "Engaging";
                case BoardActionType.FightingBoss:
                    return "Boss Fight";
                case BoardActionType.Extracting:
                    return "Extracting";
                case BoardActionType.Completed:
                    return "Completed";
                case BoardActionType.Downed:
                    return "Downed";
                default:
                    return actionType.ToString();
            }
        }

        /// <summary>
        /// 获取节点类型的中文标签
        /// </summary>
        public static string GetNodeTypeLabel(BoardNodeType nodeType)
        {
            switch (nodeType)
            {
                case BoardNodeType.Start:
                    return "Start";
                case BoardNodeType.Resource:
                    return "Resource";
                case BoardNodeType.Enemy:
                    return "Enemy";
                case BoardNodeType.Boss:
                    return "Boss";
                case BoardNodeType.Extract:
                    return "Extract";
                default:
                    return nodeType.ToString();
            }
        }

        /// <summary>
        /// 获取危险等级的中文标签
        /// </summary>
        public static string GetDangerLabel(BoardDangerTier dangerTier)
        {
            switch (dangerTier)
            {
                case BoardDangerTier.None:
                    return "None";
                case BoardDangerTier.Low:
                    return "Low";
                case BoardDangerTier.Medium:
                    return "Medium";
                case BoardDangerTier.High:
                    return "High";
                default:
                    return dangerTier.ToString();
            }
        }

        /// <summary>
        /// 获取资源等级的中文标签
        /// </summary>
        public static string GetResourceTierLabel(BoardResourceTier resourceTier)
        {
            switch (resourceTier)
            {
                case BoardResourceTier.None:
                    return "None";
                case BoardResourceTier.Low:
                    return "Low";
                case BoardResourceTier.Medium:
                    return "Medium";
                case BoardResourceTier.High:
                    return "High";
                default:
                    return resourceTier.ToString();
            }
        }

        /// <summary>
        /// 获取资源点状态的中文标签
        /// </summary>
        public static string GetResourceStateLabel(BoardResourceStateType stateType)
        {
            switch (stateType)
            {
                case BoardResourceStateType.Unsearched:
                    return "Unsearched";
                case BoardResourceStateType.Searching:
                    return "Searching";
                case BoardResourceStateType.PartiallySearched:
                    return "Partial";
                case BoardResourceStateType.SearchCompleted:
                    return "Search Complete";
                case BoardResourceStateType.Looted:
                    return "Looted";
                default:
                    return stateType.ToString();
            }
        }

        /// <summary>
        /// 获取普通敌人点状态的中文标签
        /// </summary>
        public static string GetEnemyStateLabel(BoardEnemyStateType stateType)
        {
            switch (stateType)
            {
                case BoardEnemyStateType.Unengaged:
                    return "Unengaged";
                case BoardEnemyStateType.Engaged:
                    return "Engaged";
                case BoardEnemyStateType.Disengaged:
                    return "Disengaged";
                case BoardEnemyStateType.Damaged:
                    return "Damaged";
                case BoardEnemyStateType.Cleared:
                    return "Cleared";
                default:
                    return stateType.ToString();
            }
        }

        /// <summary>
        /// 获取 Boss 点状态的中文标签
        /// </summary>
        public static string GetBossStateLabel(BoardBossStateType stateType)
        {
            switch (stateType)
            {
                case BoardBossStateType.Untriggered:
                    return "Untriggered";
                case BoardBossStateType.Engaged:
                    return "Engaged";
                case BoardBossStateType.Defeated:
                    return "Defeated";
                default:
                    return stateType.ToString();
            }
        }

        /// <summary>
        /// 获取撤离点状态的中文标签
        /// </summary>
        public static string GetExtractStateLabel(BoardExtractStateType stateType)
        {
            switch (stateType)
            {
                case BoardExtractStateType.Available:
                    return "Available";
                case BoardExtractStateType.Extracting:
                    return "Extracting";
                case BoardExtractStateType.Extracted:
                    return "Extracted";
                default:
                    return stateType.ToString();
            }
        }

        /// <summary>
        /// 获取节点颜色，用于地图表现层区分类型和等级
        /// </summary>
        public static Color GetNodeColor(BoardNodeType nodeType, BoardResourceTier resourceTier, BoardDangerTier dangerTier)
        {
            switch (nodeType)
            {
                case BoardNodeType.Start:
                    return new Color(0.9f, 0.9f, 0.9f, 1f);
                case BoardNodeType.Resource:
                    switch (resourceTier)
                    {
                        case BoardResourceTier.Low:
                            return new Color(0.42f, 0.73f, 0.95f, 1f);
                        case BoardResourceTier.Medium:
                            return new Color(0.2f, 0.46f, 0.87f, 1f);
                        case BoardResourceTier.High:
                            return new Color(0.57f, 0.35f, 0.85f, 1f);
                        default:
                            return new Color(0.55f, 0.72f, 0.95f, 1f);
                    }
                case BoardNodeType.Enemy:
                    switch (dangerTier)
                    {
                        case BoardDangerTier.Low:
                            return new Color(0.95f, 0.72f, 0.32f, 1f);
                        case BoardDangerTier.Medium:
                            return new Color(0.95f, 0.47f, 0.18f, 1f);
                        case BoardDangerTier.High:
                            return new Color(0.84f, 0.21f, 0.16f, 1f);
                        default:
                            return new Color(0.84f, 0.35f, 0.16f, 1f);
                    }
                case BoardNodeType.Boss:
                    return new Color(0.72f, 0.12f, 0.12f, 1f);
                case BoardNodeType.Extract:
                    return new Color(0.18f, 0.76f, 0.36f, 1f);
                default:
                    return Color.white;
            }
        }
    }

    /// <summary>
    /// 地图节点类型
    /// </summary>
    public enum BoardNodeType
    {
        Start = 0,
        Resource = 1,
        Enemy = 2,
        Boss = 3,
        Extract = 4
    }

    /// <summary>
    /// 资源点资源等级
    /// </summary>
    public enum BoardResourceTier
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 敌人危险等级
    /// </summary>
    public enum BoardDangerTier
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 角色当前动作类型
    /// </summary>
    public enum BoardActionType
    {
        Idle = 0,
        Moving = 1,
        Searching = 2,
        FightingEnemy = 3,
        FightingBoss = 4,
        Extracting = 5,
        Completed = 6,
        Downed = 7
    }

    /// <summary>
    /// 资源点过程状态
    /// </summary>
    public enum BoardResourceStateType
    {
        Unsearched = 0,
        Searching = 1,
        PartiallySearched = 2,
        SearchCompleted = 3,
        Looted = 4
    }

    /// <summary>
    /// 普通敌人点过程状态
    /// </summary>
    public enum BoardEnemyStateType
    {
        Unengaged = 0,
        Engaged = 1,
        Disengaged = 2,
        Damaged = 3,
        Cleared = 4
    }

    /// <summary>
    /// Boss 点过程状态
    /// </summary>
    public enum BoardBossStateType
    {
        Untriggered = 0,
        Engaged = 1,
        Defeated = 2
    }

    /// <summary>
    /// 撤离点过程状态
    /// </summary>
    public enum BoardExtractStateType
    {
        Available = 0,
        Extracting = 1,
        Extracted = 2
    }

    /// <summary>
    /// 物品大类
    /// </summary>
    public enum BoardItemCategory
    {
        Loot = 0,
        Consumable = 1
    }

    /// <summary>
    /// 物品稀有度
    /// </summary>
    public enum BoardItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// 可消耗道具类型
    /// </summary>
    public enum BoardConsumableType
    {
        None = 0,
        HealingPotion = 1
    }

    /// <summary>
    /// 当前目标来源，用于区分 AI 默认意图和玩家改写意图
    /// </summary>
    public enum BoardIntentSource
    {
        Autonomous = 0,
        PlayerRedirect = 1
    }

    /// <summary>
    /// 对局最终结果
    /// </summary>
    public enum BoardSessionOutcome
    {
        None = 0,
        Success = 1,
        Failure = 2
    }
}

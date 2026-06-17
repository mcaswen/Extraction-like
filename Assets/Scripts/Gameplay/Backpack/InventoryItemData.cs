using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 物品主类型
/// </summary>
public enum ItemType
{
    Weapon,
    Ammo,
    Medical,
    Rig,
    Bag,
    Junk,
    Equipment
}

public enum EquipmentSlotKind
{
    None,
    Head,
    Body,
    Face,
    Headphone,
    Totem
}


/// <summary>
/// 物品稀有度
/// 用于决定搜索耗时和表现优先级
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum TotemQuality
{
    None,
    Green,
    Blue,
    Gold
}

public enum TotemModifierType
{
    MaxHealth,
    MoveSpeed,
    AttackRange,
    TargetDiscoveryRange,
    NormalAttackDamage,
    IceSkillDamage,
    EarthSkillDamage
}

/// <summary>
/// A single percentage-based totem modifier. Use 0.1 for +10%, -0.05 for -5%.
/// </summary>
[System.Serializable]
public sealed class TotemStatModifier
{
    public TotemModifierType Type;
    public float Percent;
}

/// <summary>
/// Runtime total of all equipped totem modifiers.
/// </summary>
[System.Serializable]
public struct TotemModifierSet
{
    public float MaxHealthPercent;
    public float MoveSpeedPercent;
    public float AttackRangePercent;
    public float TargetDiscoveryRangePercent;
    public float NormalAttackDamagePercent;
    public float IceSkillDamagePercent;
    public float EarthSkillDamagePercent;

    public bool HasAny =>
        !Mathf.Approximately(MaxHealthPercent, 0f) ||
        !Mathf.Approximately(MoveSpeedPercent, 0f) ||
        !Mathf.Approximately(AttackRangePercent, 0f) ||
        !Mathf.Approximately(TargetDiscoveryRangePercent, 0f) ||
        !Mathf.Approximately(NormalAttackDamagePercent, 0f) ||
        !Mathf.Approximately(IceSkillDamagePercent, 0f) ||
        !Mathf.Approximately(EarthSkillDamagePercent, 0f);

    public void AddItem(InventoryItemData itemData)
    {
        if (itemData == null ||
            itemData.EquipmentKind != EquipmentSlotKind.Totem ||
            itemData.TotemModifiers == null)
        {
            return;
        }

        foreach (TotemStatModifier modifier in itemData.TotemModifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            Add(modifier.Type, modifier.Percent);
        }
    }

    public void Add(TotemModifierType type, float percent)
    {
        switch (type)
        {
            case TotemModifierType.MaxHealth:
                MaxHealthPercent += percent;
                break;
            case TotemModifierType.MoveSpeed:
                MoveSpeedPercent += percent;
                break;
            case TotemModifierType.AttackRange:
                AttackRangePercent += percent;
                break;
            case TotemModifierType.TargetDiscoveryRange:
                TargetDiscoveryRangePercent += percent;
                break;
            case TotemModifierType.NormalAttackDamage:
                NormalAttackDamagePercent += percent;
                break;
            case TotemModifierType.IceSkillDamage:
                IceSkillDamagePercent += percent;
                break;
            case TotemModifierType.EarthSkillDamage:
                EarthSkillDamagePercent += percent;
                break;
        }
    }

    public float GetMultiplier(float percent)
    {
        return Mathf.Max(0f, 1f + percent);
    }

    public float ApplyMoveSpeed(float baseValue)
    {
        return Mathf.Max(0f, baseValue * GetMultiplier(MoveSpeedPercent));
    }

    public float ApplyAttackRange(float baseValue)
    {
        return Mathf.Max(0f, baseValue * GetMultiplier(AttackRangePercent));
    }

    public float ApplyTargetDiscoveryRange(float baseValue)
    {
        return Mathf.Max(0f, baseValue * GetMultiplier(TargetDiscoveryRangePercent));
    }

    public float ApplyNormalAttackDamage(float baseValue)
    {
        return Mathf.Max(0f, baseValue * GetMultiplier(NormalAttackDamagePercent));
    }

    public int ApplyMaxHealth(int baseValue)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, baseValue) * GetMultiplier(MaxHealthPercent)));
    }

    public float GetSkillDamageMultiplier(Gameplay.Agent.Combat.AgentCombatElementType element)
    {
        switch (element)
        {
            case Gameplay.Agent.Combat.AgentCombatElementType.Ice:
                return GetMultiplier(IceSkillDamagePercent);
            case Gameplay.Agent.Combat.AgentCombatElementType.Earth:
                return GetMultiplier(EarthSkillDamagePercent);
            default:
                return 1f;
        }
    }
}

/// <summary>
/// Optional gameplay unlocks granted when the item is brought out.
/// </summary>
public enum MagicUnlockType
{
    None,
    IceFreeze,
    IceCone,
    EarthWall,
    RunePattern,
    TravelerBoots,
    TimeHourglass,
    SpaceHourglass
}

/// <summary>
/// 物品静态配置
/// ScriptableObject 只承载配置，不承载运行时状态
/// </summary>
[CreateAssetMenu(fileName = "SO_Bag_NewItemData", menuName = "HardcoreInventory/ItemData")]
public class InventoryItemData : ScriptableObject
{
    [Header("Container")]
    public int ContainerColumns;
    public int ContainerRows;
    public List<Vector2Int> BlockedCells = new List<Vector2Int>();

    [Header("Identity")]
    public string ItemID;
    public string ItemName;
    public Sprite ItemIcon;
    public Sprite ItemBackgroundSprite;

    [Header("Runtime Availability")]
    public bool IncludeInRuntimeDatabase = true;
    public bool IncludeInTotemShop = true;

    [Header("Category")]
    public ItemType Type = ItemType.Junk;
    public ItemRarity Rarity = ItemRarity.Common;
    public EquipmentSlotKind EquipmentKind = EquipmentSlotKind.None;


    [Header("Shape")]
    [Range(1, 10)]
    public int Width = 1;

    [Range(1, 10)]
    public int Height = 1;

    [Header("Stack")]
    public bool IsStackable;
    public int MaxStack = 1;

    [Header("World")]
    public GameObject WorldPrefab;

    [Header("Economy")]
    [Min(0)]
    public int SellPrice;

    [Header("Load")]
    [InspectorName("负重")]
    [Min(0f)]
    public float CarryWeight = 1f;

    [Header("Search")]
    public bool RequiresSearchInLootContainer = true;
    public float SearchDurationOverride = -1f;

    [Header("Magic Unlock")]
    public MagicUnlockType MagicUnlock = MagicUnlockType.None;
    [Min(1)]
    public int RunePatternPoints = 1;

    [Header("Totem")]
    public TotemQuality TotemQuality = TotemQuality.None;
    public List<TotemStatModifier> TotemModifiers = new List<TotemStatModifier>();

    /// <summary>
    /// 获取该物品在战利品容器中的默认搜索时长
    /// 如果配置里手动覆盖了时长，则优先使用覆盖值；
    /// 否则按稀有度基础时长叠加体积惩罚计算
    /// </summary>
    /// <returns>搜索该物品所需的默认秒数</returns>
    public float GetSearchDurationSeconds()
    {
        if (SearchDurationOverride >= 0f)
        {
            return SearchDurationOverride;
        }

        float rarityDuration = Rarity switch
        {
            ItemRarity.Common => 0.45f,
            ItemRarity.Uncommon => 0.75f,
            ItemRarity.Rare => 1.1f,
            ItemRarity.Epic => 1.55f,
            ItemRarity.Legendary => 2.1f,
            _ => 0.45f
        };

        float sizePenalty = Mathf.Max(0f, (Width * Height - 1) * 0.12f);
        return rarityDuration + sizePenalty;
    }

    public bool IsTotemEquipment()
    {
        return Type == ItemType.Equipment && EquipmentKind == EquipmentSlotKind.Totem;
    }

    public string GetTotemEffectSummary()
    {
        if (!IsTotemEquipment() || TotemModifiers == null || TotemModifiers.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < TotemModifiers.Count; i++)
        {
            TotemStatModifier modifier = TotemModifiers[i];
            if (modifier == null || Mathf.Approximately(modifier.Percent, 0f))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(GetTotemModifierLabel(modifier.Type));
            builder.Append(' ');
            builder.Append(FormatPercent(modifier.Percent));
        }

        return builder.ToString();
    }

    public static string GetTotemQualityLabel(TotemQuality quality)
    {
        switch (quality)
        {
            case TotemQuality.Green:
                return "绿色";
            case TotemQuality.Blue:
                return "蓝色";
            case TotemQuality.Gold:
                return "金色";
            case TotemQuality.None:
            default:
                return string.Empty;
        }
    }

    public static string GetTotemModifierLabel(TotemModifierType type)
    {
        switch (type)
        {
            case TotemModifierType.MaxHealth:
                return "最大生命";
            case TotemModifierType.MoveSpeed:
                return "移动速度";
            case TotemModifierType.AttackRange:
                return "射程";
            case TotemModifierType.TargetDiscoveryRange:
                return "视野距离";
            case TotemModifierType.NormalAttackDamage:
                return "法杖伤害";
            case TotemModifierType.IceSkillDamage:
                return "冰属性技能伤害";
            case TotemModifierType.EarthSkillDamage:
                return "土属性技能伤害";
            default:
                return type.ToString();
        }
    }

    public static string FormatPercent(float percent)
    {
        string sign = percent >= 0f ? "+" : string.Empty;
        return $"{sign}{percent * 100f:0.#}%";
    }
}

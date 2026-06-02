using System.Collections.Generic;
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
}

using UnityEngine;

// 【PRD】：定义物品类型枚举
public enum ItemType { Weapon, Ammo, Medical, Rig, Bag, Junk }
[CreateAssetMenu(fileName = "NewItemData", menuName = "HardcoreInventory/ItemData")]
public class InventoryItemData : ScriptableObject
{
    public string ItemID;
    public string ItemName;
    public Sprite ItemIcon;

    [Header("核心类别")]
    public ItemType Type = ItemType.Junk; // 物品类型

    [Header("形态标准")][Range(1, 10)] public int Width = 1;
    [Range(1, 10)] public int Height = 1;

    [Header("堆叠机制")]
    public bool IsStackable = false;
    public int MaxStack = 1;

    [Header("3D 实体配置")]
    public GameObject WorldPrefab; // 掉落在 3D 世界时的预制体模型
}
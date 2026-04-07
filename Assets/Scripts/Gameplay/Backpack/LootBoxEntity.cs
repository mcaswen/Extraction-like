using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 宝箱随机生成配置
/// </summary>
[System.Serializable]
public class LootGenerationEntry
{
    public InventoryItemData ItemData;
    public int Weight = 1;
    public int MinAmount = 1;
    public int MaxAmount = 1;

    [Range(0f, 1f)]
    public float SpawnChance = 1f;
}

/// <summary>
/// 场景中的可交互容器实体
/// 支持在生成时预计算战利品列表，并提前完成二维装箱
/// </summary>
public class LootBoxEntity : MonoBehaviour, IInteractableContainer, IInteractable
{
    private const bool EnableLootBoxDebug = true;
    private static readonly Color ChestWhite = new Color(0.96f, 0.96f, 0.98f, 1f);

    [Header("Loot Box")]
    public string BoxName = "军用物资箱";
    public InventoryItemData FirstTimeLootItem;
    public int FirstTimeLootAmount = 30;
    public int ContainerColumns = 6;
    public int ContainerRows = 6;
    public List<Vector2Int> BlockedCells = new List<Vector2Int>();

    [Header("Pre-Calculated Spawning")]
    public bool PrecalculateLootOnSpawn = true;
    public int MinLootRollCount = 3;
    public int MaxLootRollCount = 8;
    public List<LootGenerationEntry> LootTable = new List<LootGenerationEntry>();

    [SerializeField]
    private List<ContainerItemSaveData> _savedItems = new List<ContainerItemSaveData>();

    [SerializeField]
    private List<ContainerCellStateSaveData> _savedCellStates = new List<ContainerCellStateSaveData>();

    private bool _isFirstTimeOpen = true;
    private bool _hasPrecalculatedLoot;

    private void Awake()
    {
        WhiteboxCharacterVisualUtility.ApplySolidColor(gameObject, ChestWhite);

        if (PrecalculateLootOnSpawn)
        {
            PrecalculateLootIfNeeded();
        }
    }

    /// <summary>
    /// 获取容器名称
    /// </summary>
    /// <returns>当前容器名称</returns>
    public string GetContainerName()
    {
        return BoxName;
    }

    /// <summary>
    /// 获取当前保存的战利品快照
    /// </summary>
    /// <returns>容器中的物品快照副本</returns>
    public List<ContainerItemSaveData> GetSavedItems()
    {
        PrecalculateLootIfNeeded();
        return CloneSaveDataList(_savedItems);
    }

    /// <summary>
    /// 仅覆盖容器中的物品快照，保留当前格子状态快照
    /// </summary>
    /// <param name="items">新的物品快照列表</param>
    public void SaveItems(List<ContainerItemSaveData> items)
    {
        SaveRuntimeState(items, _savedCellStates);
    }

    /// <summary>
    /// 获取主交互提示文本
    /// </summary>
    /// <returns>展示给玩家的交互文案</returns>
    public string GetPromptText()
    {
        return HasUnsearchedItems()
            ? $"[F] 搜索 {BoxName}"
            : $"[F] 打开 {BoxName}";
    }

    /// <summary>
    /// 执行主交互逻辑，打开该容器的战利品界面
    /// </summary>
    public void Interact()
    {
        if (InventoryScreenController.Instance == null || InventoryScreenController.Instance.IsInventoryOpen)
        {
            LogDebug($"Interact blocked. InventoryController={(InventoryScreenController.Instance != null)} IsInventoryOpen={(InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen)}");
            return;
        }

        LogDebug("Interact accepted. Opening loot box UI.");
        InventoryScreenController.Instance.OpenLootBox(this);
    }

    /// <summary>
    /// 构建一轮可交给共享背包界面的运行时会话
    /// </summary>
    public InventoryScreenSessionContext CreateInventorySessionContext()
    {
        return new InventoryScreenSessionContext
        {
            DisplayName = BoxName,
            ExternalContainerName = BoxName,
            ExternalColumns = ContainerColumns,
            ExternalRows = ContainerRows,
            ExternalBlockedCells = GetBlockedCells(),
            ExternalItems = GetSavedItems(),
            ExternalCellStates = GetSavedCellStates(),
            BeforeOpen = PrecalculateLootIfNeeded,
            OnClose = result =>
            {
                if (result == null)
                {
                    return;
                }

                SaveRuntimeState(result.ExternalItems, result.ExternalCellStates);
            }
        };
    }

    /// <summary>
    /// 保存完整的战利品运行时状态
    /// </summary>
    /// <param name="items">要保存的物品快照</param>
    /// <param name="cellStates">要保存的格子状态快照</param>
    public void SaveRuntimeState(List<ContainerItemSaveData> items, List<ContainerCellStateSaveData> cellStates)
    {
        _savedItems = CloneSaveDataList(items);
        _savedCellStates = CloneCellStateList(cellStates);
        _hasPrecalculatedLoot = true;
        _isFirstTimeOpen = false;
        Debug.Log($"[{BoxName}] Saved {_savedItems.Count} items.");
    }

    /// <summary>
    /// 获取当前保存的格子状态快照
    /// </summary>
    /// <returns>容器中的格子状态快照副本</returns>
    public List<ContainerCellStateSaveData> GetSavedCellStates()
    {
        PrecalculateLootIfNeeded();
        return CloneCellStateList(_savedCellStates);
    }

    /// <summary>
    /// 获取配置上的阻塞格列表副本
    /// </summary>
    /// <returns>阻塞格坐标列表副本</returns>
    public List<Vector2Int> GetBlockedCells()
    {
        return BlockedCells != null ? new List<Vector2Int>(BlockedCells) : new List<Vector2Int>();
    }

    /// <summary>
    /// 如有需要则预生成战利品并完成一次自动装箱
    /// 只会执行一次，后续读取都复用缓存结果
    /// </summary>
    public void PrecalculateLootIfNeeded()
    {
        if (_hasPrecalculatedLoot)
        {
            return;
        }

        List<ContainerItemSaveData> rolledLoot = GenerateLootCandidates();
        _savedItems = InventoryAutoSortService.BuildPackedLayout(
            ContainerColumns,
            ContainerRows,
            BlockedCells,
            rolledLoot);
        _savedCellStates = new List<ContainerCellStateSaveData>();
        _hasPrecalculatedLoot = true;
        _isFirstTimeOpen = false;
    }

    // 生成本次容器应包含的战利品候选列表
    private List<ContainerItemSaveData> GenerateLootCandidates()
    {
        List<ContainerItemSaveData> generatedLoot = new List<ContainerItemSaveData>();

        if (_isFirstTimeOpen && FirstTimeLootItem != null)
        {
            generatedLoot.Add(CreateGeneratedLoot(FirstTimeLootItem, FirstTimeLootAmount));
        }

        if (LootTable != null && LootTable.Count > 0)
        {
            int rollCount = Random.Range(MinLootRollCount, MaxLootRollCount + 1);
            for (int i = 0; i < rollCount; i++)
            {
                LootGenerationEntry entry = RollLootEntry();
                if (entry == null || entry.ItemData == null)
                {
                    continue;
                }

                int amount = entry.ItemData.IsStackable
                    ? Random.Range(Mathf.Max(1, entry.MinAmount), Mathf.Max(entry.MinAmount, entry.MaxAmount) + 1)
                    : 1;
                generatedLoot.Add(CreateGeneratedLoot(entry.ItemData, amount));
            }
        }

        return generatedLoot;
    }

    // 根据权重和概率从掉落表里选出一次实际掉落项
    private LootGenerationEntry RollLootEntry()
    {
        List<LootGenerationEntry> candidates = new List<LootGenerationEntry>();
        int totalWeight = 0;

        foreach (LootGenerationEntry entry in LootTable)
        {
            if (entry == null || entry.ItemData == null || entry.Weight <= 0)
            {
                continue;
            }

            if (Random.value > entry.SpawnChance)
            {
                continue;
            }

            candidates.Add(entry);
            totalWeight += entry.Weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0)
        {
            return null;
        }

        int randomWeight = Random.Range(0, totalWeight);
        int currentWeight = 0;
        foreach (LootGenerationEntry entry in candidates)
        {
            currentWeight += entry.Weight;
            if (randomWeight < currentWeight)
            {
                return entry;
            }
        }

        return candidates[candidates.Count - 1];
    }

    // 把配置项转换为可直接进入容器的运行时快照，并附带搜索状态
    private static ContainerItemSaveData CreateGeneratedLoot(InventoryItemData itemData, int amount)
    {
        bool requiresSearch = itemData != null && itemData.RequiresSearchInLootContainer;
        float searchDuration = itemData != null ? itemData.GetSearchDurationSeconds() : 0f;

        return new ContainerItemSaveData
        {
            ItemData = itemData,
            Amount = amount,
            X = 0,
            Y = 0,
            IsRotated = false,
            RequiresSearch = requiresSearch,
            IsSearched = !requiresSearch,
            SearchProgressSeconds = 0f,
            SearchDurationSeconds = requiresSearch ? searchDuration : 0f,
            InternalItems = new List<ContainerItemSaveData>(),
            InternalCellStates = new List<ContainerCellStateSaveData>()
        };
    }

    // 判断当前容器内是否仍有未搜索完成的物品，用于切换“搜索/打开”提示文案
    private bool HasUnsearchedItems()
    {
        PrecalculateLootIfNeeded();
        foreach (ContainerItemSaveData item in _savedItems)
        {
            if (item != null && item.RequiresSearch && !item.IsSearched)
            {
                return true;
            }
        }

        return false;
    }

    private void LogDebug(string message)
    {
        if (!EnableLootBoxDebug)
        {
            return;
        }

        Debug.Log($"[LootBoxDebug:{BoxName}] {message}");
    }

    // 深拷贝物品快照列表，防止外部直接改写容器内部缓存
    private static List<ContainerItemSaveData> CloneSaveDataList(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> clone = new List<ContainerItemSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerItemSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }

    // 深拷贝格子状态快照列表
    private static List<ContainerCellStateSaveData> CloneCellStateList(List<ContainerCellStateSaveData> source)
    {
        List<ContainerCellStateSaveData> clone = new List<ContainerCellStateSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerCellStateSaveData item in source)
        {
            if (item != null)
            {
                clone.Add(item.DeepCopy());
            }
        }

        return clone;
    }
}

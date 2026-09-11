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
/// 场景资源箱对应桌游资源点的资源等级
/// </summary>
public enum SceneResourceTier
{
    Low,
    Medium,
    High
}

/// <summary>
/// 场景资源箱的桌游式搜索状态
/// </summary>
public enum SceneResourceStateType
{
    Unsearched,
    Searching,
    PartiallySearched,
    SearchCompleted,
    Looted
}

/// <summary>
/// 场景中的可交互容器实体
/// 统一在第一次读取时懒生成战利品列表，并完成二维装箱
/// </summary>
public class LootBoxEntity : MonoBehaviour, IInteractableContainer, IInteractable
{
    private const string DefaultBoxName = "军用物资箱";
    private const int DefaultContainerColumns = 6;
    private const int DefaultContainerRows = 6;
    private const string DefaultResourceLootRuleSetResourcesPath = "Loot/SO_SceneResourceLootRuleSet";

    private static SceneResourceLootRuleSet _defaultResourceLootRuleSet;

    [Header("Loot Box")]
    public string BoxName = DefaultBoxName;
    public InventoryItemData FirstTimeLootItem;
    public int FirstTimeLootAmount = 30;
    public int ContainerColumns = DefaultContainerColumns;
    public int ContainerRows = DefaultContainerRows;
    public List<Vector2Int> BlockedCells = new List<Vector2Int>();

    [Header("Lazy Loot Generation")]
    public int MinLootRollCount = 3;
    public int MaxLootRollCount = 8;
    public bool GenerateEachLootTableEntryOnce;
    public List<LootGenerationEntry> LootTable = new List<LootGenerationEntry>();

    [Header("Board Game Resource Rules")]
    public bool UseBoardGameResourceRules;
    public SceneResourceLootRuleSet ResourceLootRuleSet;
    [HideInInspector]
    public SceneResourceTier ResourceTier = SceneResourceTier.Low;
    public SceneResourceStateType ResourceState = SceneResourceStateType.Unsearched;
    public float LowTierSearchSeconds = 3f;
    public float MediumTierSearchSeconds = 5f;
    public float HighTierSearchSeconds = 7f;

    [SerializeField]
    private List<ContainerItemSaveData> _savedItems = new List<ContainerItemSaveData>();

    [SerializeField]
    private List<ContainerCellStateSaveData> _savedCellStates = new List<ContainerCellStateSaveData>();

    private bool _isFirstTimeOpen = true;
    private bool _hasGeneratedLoot;
    private bool _hasAwardedOpenExperience;

    public bool IsBoardGameResourcePoint => UseBoardGameResourceRules;
    public bool IsResourcePointLooted => UseBoardGameResourceRules && ResourceState == SceneResourceStateType.Looted;
    public float SearchRequiredSeconds => ResolveSearchDurationSeconds();

    private string ResolvedBoxName => string.IsNullOrWhiteSpace(BoxName) ? DefaultBoxName : BoxName.Trim();
    private int ResolvedContainerColumns => ContainerColumns > 0 ? ContainerColumns : DefaultContainerColumns;
    private int ResolvedContainerRows => ContainerRows > 0 ? ContainerRows : DefaultContainerRows;

    private void Reset()
    {
#if UNITY_EDITOR
        ApplyDefaultConfiguration();
#endif
    }

    private void OnValidate()
    {
        ApplyDefaultConfiguration();
    }

    private void ApplyDefaultConfiguration()
    {
        if (string.IsNullOrWhiteSpace(BoxName))
            BoxName = DefaultBoxName;

        if (ContainerColumns <= 0)
            ContainerColumns = DefaultContainerColumns;

        if (ContainerRows <= 0)
            ContainerRows = DefaultContainerRows;

        BlockedCells ??= new List<Vector2Int>();
    }

    /// <summary>
    /// 获取容器名称
    /// </summary>
    /// <returns>当前容器名称</returns>
    public string GetContainerName()
    {
        return ResolvedBoxName;
    }

    /// <summary>
    /// 获取当前保存的战利品快照
    /// </summary>
    /// <returns>容器中的物品快照副本</returns>
    public List<ContainerItemSaveData> GetSavedItems()
    {
        EnsureLootGeneratedIfNeeded();
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
        if (UseBoardGameResourceRules)
        {
            RefreshResourcePointState();
        }

        if (IsResourcePointLooted)
        {
            return $"[F] 已搜刮 {ResolvedBoxName}";
        }

        return HasUnsearchedItems()
            ? $"[F] 搜索 {ResolvedBoxName}"
            : $"[F] 打开 {ResolvedBoxName}";
    }

    /// <summary>
    /// 执行主交互逻辑，打开该容器的战利品界面
    /// </summary>
    public void Interact()
    {
        if (UseBoardGameResourceRules)
        {
            RefreshResourcePointState();
        }

        if (InventoryScreenController.Instance == null ||
            InventoryScreenController.Instance.IsInventoryOpen ||
            IsResourcePointLooted)
        {
            return;
        }

        MarkResourceSearchStarted();
        AwardOpenExperienceIfNeeded();
        InventoryScreenController.Instance.OpenLootBox(this);
    }

    /// <summary>
    /// 构建一轮可交给共享背包界面的运行时会话
    /// </summary>
    public InventoryScreenSessionContext CreateInventorySessionContext()
    {
        return new InventoryScreenSessionContext
        {
            SourceObject = gameObject,
            DisplayName = ResolvedBoxName,
            ExternalContainerName = ResolvedBoxName,
            ExternalColumns = ResolvedContainerColumns,
            ExternalRows = ResolvedContainerRows,
            ExternalBlockedCells = GetBlockedCells(),
            ExternalItems = GetSavedItems(),
            ExternalCellStates = GetSavedCellStates(),
            BeforeOpen = EnsureLootGeneratedIfNeeded,
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
        _hasGeneratedLoot = true;
        _isFirstTimeOpen = false;
        RefreshResourceStateFromSavedItems();
        Debug.Log($"[{ResolvedBoxName}] Saved {_savedItems.Count} items.");
    }

    /// <summary>
    /// 获取当前保存的格子状态快照
    /// </summary>
    /// <returns>容器中的格子状态快照副本</returns>
    public List<ContainerCellStateSaveData> GetSavedCellStates()
    {
        EnsureLootGeneratedIfNeeded();
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
    /// 如有需要则懒生成战利品并完成一次自动装箱
    /// 只会执行一次，后续读取都复用缓存结果
    /// </summary>
    public void EnsureLootGeneratedIfNeeded()
    {
        if (_hasGeneratedLoot)
        {
            return;
        }

        List<ContainerItemSaveData> rolledLoot = GenerateLootCandidates();
        _savedItems = InventoryAutoSortService.BuildPackedLayout(
            ResolvedContainerColumns,
            ResolvedContainerRows,
            GetBlockedCells(),
            rolledLoot);
        _savedCellStates = new List<ContainerCellStateSaveData>();
        _hasGeneratedLoot = true;
        _isFirstTimeOpen = false;
        RefreshResourceStateFromSavedItems();
    }

    /// <summary>
    /// 查询该资源箱是否仍可作为资源点被搜索或打开
    /// </summary>
    /// <returns>是否仍有待处理资源</returns>
    public bool CanBeSearchedAsResourcePoint()
    {
        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        EnsureLootGeneratedIfNeeded();

        if (!UseBoardGameResourceRules)
        {
            return _savedItems.Count > 0;
        }

        return ResourceState != SceneResourceStateType.Looted && _savedItems.Count > 0;
    }

    /// <summary>
    /// 刷新资源点状态；供目标系统在背包关闭后同步箱子是否已经被清空
    /// </summary>
    public void RefreshResourcePointState()
    {
        EnsureLootGeneratedIfNeeded();
        RefreshResourceStateFromSavedItems();
    }

    /// <summary>
    /// 将资源点强制标为已搜刮
    /// </summary>
    public void MarkResourcePointLooted()
    {
        if (!UseBoardGameResourceRules)
        {
            return;
        }

        ResourceState = SceneResourceStateType.Looted;
    }

    /// <summary>
    /// 由资源群写入当前箱子的 fallback 等级
    /// 实际等级仍优先读取所属 ResourceClusterAuthoring
    /// </summary>
    /// <param name="resourceTier"></param>
    public void ApplyResourceClusterTier(SceneResourceTier resourceTier)
    {
        UseBoardGameResourceRules = true;
        ResourceTier = resourceTier;
    }

    /// <summary>
    /// 供经验系统读取该箱子当前使用的资源等级。
    /// </summary>
    public SceneResourceTier ResolveExperienceResourceTier()
    {
        return ResolveEffectiveResourceTier();
    }

    private void AwardOpenExperienceIfNeeded()
    {
        if (_hasAwardedOpenExperience)
            return;

        if (!TryGetFocusedAgentProgression(
                out Gameplay.Agent.Progression.AgentLevelProgressionController progressionController))
        {
            return;
        }

        int awardedExperience = progressionController.NotifyLootBoxOpened(this);
        if (awardedExperience > 0)
            _hasAwardedOpenExperience = true;
    }

    private static bool TryGetFocusedAgentProgression(
        out Gameplay.Agent.Progression.AgentLevelProgressionController progressionController)
    {
        progressionController = null;
        Gameplay.Agent.Runtime.AgentRuntimeRegistry registry =
            Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance;
        if (registry == null || !registry.TryGetFocusedHandle(out Gameplay.Agent.Runtime.AgentRuntimeHandle handle))
            return false;

        if (!handle.IsAlive || handle.CachedTransform == null)
            return false;

        progressionController =
            handle.CachedTransform.GetComponent<Gameplay.Agent.Progression.AgentLevelProgressionController>();
        return progressionController != null;
    }

    // 生成本次容器应包含的战利品候选列表
    private List<ContainerItemSaveData> GenerateLootCandidates()
    {
        if (UseBoardGameResourceRules)
        {
            return GenerateBoardGameResourceLootCandidates();
        }

        List<ContainerItemSaveData> generatedLoot = new List<ContainerItemSaveData>();

        if (_isFirstTimeOpen && FirstTimeLootItem != null)
        {
            generatedLoot.Add(CreateGeneratedLoot(FirstTimeLootItem, FirstTimeLootAmount));
        }

        if (LootTable != null && LootTable.Count > 0)
        {
            if (GenerateEachLootTableEntryOnce)
            {
                AddEachLootTableEntryOnce(generatedLoot);
                return generatedLoot;
            }

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

    // 按旧桌游资源点规则生成箱内候选物资：不同资源等级使用不同 roll 数和稀有度权重
    private List<ContainerItemSaveData> GenerateBoardGameResourceLootCandidates()
    {
        List<ContainerItemSaveData> generatedLoot = new List<ContainerItemSaveData>();
        int rollCount = ResolveBoardGameRollCount();
        IReadOnlyList<LootGenerationEntry> lootTable = ResolveBoardGameLootTable();

        for (int i = 0; i < rollCount; i++)
        {
            LootGenerationEntry entry = RollBoardGameResourceEntry(lootTable);
            if (entry == null || entry.ItemData == null)
            {
                continue;
            }

            int amount = entry.ItemData.IsStackable
                ? Random.Range(Mathf.Max(1, entry.MinAmount), Mathf.Max(entry.MinAmount, entry.MaxAmount) + 1)
                : 1;
            generatedLoot.Add(CreateGeneratedLoot(entry.ItemData, amount));
        }

        return generatedLoot;
    }

    // Adds every configured entry once for deterministic test boxes.
    private void AddEachLootTableEntryOnce(List<ContainerItemSaveData> generatedLoot)
    {
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

            int amount = entry.ItemData.IsStackable
                ? Random.Range(Mathf.Max(1, entry.MinAmount), Mathf.Max(entry.MinAmount, entry.MaxAmount) + 1)
                : 1;
            generatedLoot.Add(CreateGeneratedLoot(entry.ItemData, amount));
        }
    }

    // 根据权重和概率从掉落表里选出一次实际掉落项
    private LootGenerationEntry RollLootEntry()
    {
        return RollLootEntry(LootTable);
    }

    private LootGenerationEntry RollLootEntry(IReadOnlyList<LootGenerationEntry> lootTable)
    {
        List<LootGenerationEntry> candidates = new List<LootGenerationEntry>();
        int totalWeight = 0;

        if (lootTable == null)
        {
            return null;
        }

        foreach (LootGenerationEntry entry in lootTable)
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

    // 先按资源等级抽稀有度，再从资源点规则表里抽同稀有度的实际物品
    private LootGenerationEntry RollBoardGameResourceEntry(IReadOnlyList<LootGenerationEntry> lootTable)
    {
        if (lootTable == null || lootTable.Count <= 0)
        {
            return null;
        }

        if (!TryRollBoardGameRarity(lootTable, out ItemRarity rarity))
        {
            return RollLootEntry(lootTable);
        }

        return RollLootEntryByRarity(lootTable, rarity) ?? RollLootEntry(lootTable);
    }

    private bool TryRollBoardGameRarity(IReadOnlyList<LootGenerationEntry> lootTable, out ItemRarity rarity)
    {
        SceneResourceTier effectiveResourceTier = ResolveEffectiveResourceTier();
        SceneResourceLootRuleSet ruleSet = ResolveResourceLootRuleSet();
        if (ruleSet != null)
        {
            return ruleSet.TryRollRarity(
                effectiveResourceTier,
                candidateRarity => HasLootEntryForRarity(lootTable, candidateRarity),
                out rarity);
        }

        rarity = ItemRarity.Common;
        float commonWeight = 0f;
        float uncommonWeight = 0f;
        float rareWeight = 0f;
        float epicWeight = 0f;
        float legendaryWeight = 0f;

        switch (effectiveResourceTier)
        {
            case SceneResourceTier.Low:
                commonWeight = 45f;
                uncommonWeight = 35f;
                rareWeight = 20f;
                break;
            case SceneResourceTier.Medium:
                commonWeight = 10f;
                uncommonWeight = 40f;
                rareWeight = 35f;
                epicWeight = 15f;
                break;
            case SceneResourceTier.High:
                uncommonWeight = 30f;
                rareWeight = 30f;
                epicWeight = 25f;
                legendaryWeight = 15f;
                break;
        }

        return TryPickAvailableRarity(
            lootTable,
            commonWeight,
            uncommonWeight,
            rareWeight,
            epicWeight,
            legendaryWeight,
            out rarity);
    }

    private bool TryPickAvailableRarity(
        IReadOnlyList<LootGenerationEntry> lootTable,
        float commonWeight,
        float uncommonWeight,
        float rareWeight,
        float epicWeight,
        float legendaryWeight,
        out ItemRarity rarity)
    {
        rarity = ItemRarity.Common;
        float totalWeight = 0f;
        totalWeight += HasLootEntryForRarity(lootTable, ItemRarity.Common) ? Mathf.Max(0f, commonWeight) : 0f;
        totalWeight += HasLootEntryForRarity(lootTable, ItemRarity.Uncommon) ? Mathf.Max(0f, uncommonWeight) : 0f;
        totalWeight += HasLootEntryForRarity(lootTable, ItemRarity.Rare) ? Mathf.Max(0f, rareWeight) : 0f;
        totalWeight += HasLootEntryForRarity(lootTable, ItemRarity.Epic) ? Mathf.Max(0f, epicWeight) : 0f;
        totalWeight += HasLootEntryForRarity(lootTable, ItemRarity.Legendary) ? Mathf.Max(0f, legendaryWeight) : 0f;

        if (totalWeight <= Mathf.Epsilon)
        {
            return false;
        }

        float roll = Random.Range(0f, totalWeight);
        float cursor = 0f;

        if (TryAdvanceRarityRoll(lootTable, ItemRarity.Common, commonWeight, roll, ref cursor, out rarity) ||
            TryAdvanceRarityRoll(lootTable, ItemRarity.Uncommon, uncommonWeight, roll, ref cursor, out rarity) ||
            TryAdvanceRarityRoll(lootTable, ItemRarity.Rare, rareWeight, roll, ref cursor, out rarity) ||
            TryAdvanceRarityRoll(lootTable, ItemRarity.Epic, epicWeight, roll, ref cursor, out rarity) ||
            TryAdvanceRarityRoll(lootTable, ItemRarity.Legendary, legendaryWeight, roll, ref cursor, out rarity))
        {
            return true;
        }

        return false;
    }

    private bool TryAdvanceRarityRoll(
        IReadOnlyList<LootGenerationEntry> lootTable,
        ItemRarity candidateRarity,
        float weight,
        float roll,
        ref float cursor,
        out ItemRarity rarity)
    {
        rarity = ItemRarity.Common;
        if (weight <= 0f || !HasLootEntryForRarity(lootTable, candidateRarity))
        {
            return false;
        }

        cursor += weight;
        if (roll > cursor)
        {
            return false;
        }

        rarity = candidateRarity;
        return true;
    }

    private bool HasLootEntryForRarity(IReadOnlyList<LootGenerationEntry> lootTable, ItemRarity rarity)
    {
        if (lootTable == null)
        {
            return false;
        }

        foreach (LootGenerationEntry entry in lootTable)
        {
            if (entry != null &&
                entry.ItemData != null &&
                entry.Weight > 0 &&
                entry.ItemData.Rarity == rarity)
            {
                return true;
            }
        }

        return false;
    }

    private LootGenerationEntry RollLootEntryByRarity(IReadOnlyList<LootGenerationEntry> lootTable, ItemRarity rarity)
    {
        List<LootGenerationEntry> candidates = new List<LootGenerationEntry>();
        int totalWeight = 0;

        if (lootTable == null)
        {
            return null;
        }

        foreach (LootGenerationEntry entry in lootTable)
        {
            if (entry == null ||
                entry.ItemData == null ||
                entry.Weight <= 0 ||
                entry.ItemData.Rarity != rarity)
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

    private int ResolveBoardGameRollCount()
    {
        SceneResourceTier effectiveResourceTier = ResolveEffectiveResourceTier();
        SceneResourceLootRuleSet ruleSet = ResolveResourceLootRuleSet();
        if (ruleSet != null)
        {
            return ruleSet.ResolveRollCount(effectiveResourceTier);
        }

        switch (effectiveResourceTier)
        {
            case SceneResourceTier.Low:
                return Random.Range(1, 4);
            case SceneResourceTier.Medium:
                return Random.Range(2, 5);
            case SceneResourceTier.High:
                return Random.Range(3, 6);
            default:
                return Random.Range(1, 4);
        }
    }

    private IReadOnlyList<LootGenerationEntry> ResolveBoardGameLootTable()
    {
        SceneResourceLootRuleSet ruleSet = ResolveResourceLootRuleSet();
        if (ruleSet != null && ruleSet.HasLootTable)
        {
            return ruleSet.LootTable;
        }

        return LootTable;
    }

    private SceneResourceLootRuleSet ResolveResourceLootRuleSet()
    {
        if (ResourceLootRuleSet != null)
        {
            return ResourceLootRuleSet;
        }

        if (_defaultResourceLootRuleSet == null)
        {
            _defaultResourceLootRuleSet = Resources.Load<SceneResourceLootRuleSet>(DefaultResourceLootRuleSetResourcesPath);
        }

        return _defaultResourceLootRuleSet;
    }

    private float ResolveSearchDurationSeconds()
    {
        switch (ResolveEffectiveResourceTier())
        {
            case SceneResourceTier.Low:
                return Mathf.Max(0.1f, LowTierSearchSeconds);
            case SceneResourceTier.Medium:
                return Mathf.Max(0.1f, MediumTierSearchSeconds);
            case SceneResourceTier.High:
                return Mathf.Max(0.1f, HighTierSearchSeconds);
            default:
                return Mathf.Max(0.1f, LowTierSearchSeconds);
        }
    }

    private SceneResourceTier ResolveEffectiveResourceTier()
    {
        Gameplay.Targets.Runtime.GameplayTargetRegistry registry =
            Gameplay.Targets.Runtime.GameplayTargetRegistry.ActiveInstance;
        if (registry != null &&
            registry.TryFindResourceClusterByEntity(
                gameObject,
                out Gameplay.Targets.Authoring.ResourceClusterAuthoring resourceCluster))
        {
            return resourceCluster.ResourceTier;
        }

        return ResourceTier;
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
        EnsureLootGeneratedIfNeeded();
        foreach (ContainerItemSaveData item in _savedItems)
        {
            if (item != null && item.RequiresSearch && !item.IsSearched)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkResourceSearchStarted()
    {
        if (!UseBoardGameResourceRules || ResourceState == SceneResourceStateType.Looted)
        {
            return;
        }

        ResourceState = HasUnsearchedItems()
            ? SceneResourceStateType.Searching
            : SceneResourceStateType.SearchCompleted;
    }

    private void RefreshResourceStateFromSavedItems()
    {
        if (!UseBoardGameResourceRules)
        {
            return;
        }

        if (_savedItems == null || _savedItems.Count <= 0)
        {
            ResourceState = SceneResourceStateType.Looted;
            return;
        }

        bool hasUnsearchedItem = false;
        bool hasSearchProgress = false;

        foreach (ContainerItemSaveData item in _savedItems)
        {
            if (item == null || !item.RequiresSearch || item.IsSearched)
            {
                continue;
            }

            hasUnsearchedItem = true;
            if (item.SearchProgressSeconds > 0f)
            {
                hasSearchProgress = true;
            }
        }

        if (!hasUnsearchedItem)
        {
            ResourceState = SceneResourceStateType.SearchCompleted;
            return;
        }

        ResourceState = hasSearchProgress
            ? SceneResourceStateType.PartiallySearched
            : SceneResourceStateType.Unsearched;
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

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persistent per-character warehouse storage.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStorageService : MonoBehaviour
{
    private const string SaveFileName = "player_storage.json";
    private const int DefaultColumns = 6;
    private const int DefaultRows = 10;
    private const int DefaultPageCount = 10;

    public static PlayerStorageService Instance { get; private set; }

    [Header("Storage")]
    public int Columns = DefaultColumns;
    public int Rows = DefaultRows;
    public int MinimumPageCount = DefaultPageCount;
    public string FallbackAgentId = "default_player";

    [NonSerialized] private StorageSaveFile _saveFile = new StorageSaveFile();
    [NonSerialized] private PlayerStorageSaveRecord _activePlayer;
    [NonSerialized] private string _activeAgentId;
    [NonSerialized] private bool _hasLoaded;

    public string ActiveAgentId => string.IsNullOrWhiteSpace(_activeAgentId) ? FallbackAgentId : _activeAgentId;
    public int PageCount => _activePlayer?.Pages != null ? _activePlayer.Pages.Count : Mathf.Max(1, MinimumPageCount);
    public int PageCapacity => Mathf.Max(1, Columns) * Mathf.Max(1, Rows);

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Configure(int columns, int rows, int minimumPageCount)
    {
        Columns = Mathf.Max(1, columns);
        Rows = Mathf.Max(1, rows);
        MinimumPageCount = Mathf.Max(1, minimumPageCount);

        if (_activePlayer != null)
        {
            NormalizePages(_activePlayer);
        }
    }

    public void LoadForAgent(string agentId)
    {
        _activeAgentId = string.IsNullOrWhiteSpace(agentId) ? FallbackAgentId : agentId.Trim();
        LoadSaveFileIfNeeded();
        _activePlayer = GetOrCreatePlayerRecord(_activeAgentId);
        NormalizePages(_activePlayer);
    }

    public List<ContainerItemSaveData> GetPageItems(int pageIndex)
    {
        StoragePageSaveRecord page = GetOrCreatePage(pageIndex);
        return ConvertRecordsToRuntimeItems(page.Items);
    }

    public List<ContainerCellStateSaveData> GetPageCellStates(int pageIndex)
    {
        StoragePageSaveRecord page = GetOrCreatePage(pageIndex);
        return CloneCellStateList(page.CellStates);
    }

    public void SetPageRuntimeState(
        int pageIndex,
        List<ContainerItemSaveData> items,
        List<ContainerCellStateSaveData> cellStates)
    {
        StoragePageSaveRecord page = GetOrCreatePage(pageIndex);
        page.Items = ConvertRuntimeItemsToRecords(SanitizeStorageItems(items));
        page.CellStates = CloneCellStateList(cellStates);
        NormalizePages(_activePlayer);
    }

    public bool TryAppendItemsToAgentStorage(
        string agentId,
        List<ContainerItemSaveData> items,
        out int appendedItemCount,
        out int appendedTotalValue)
    {
        appendedItemCount = 0;
        appendedTotalValue = 0;

        LoadForAgent(string.IsNullOrWhiteSpace(agentId) ? FallbackAgentId : agentId.Trim());
        EnsurePageCount(MinimumPageCount);

        List<ContainerItemSaveData> sanitizedItems = SanitizeStorageItems(items);
        for (int i = 0; i < sanitizedItems.Count; i++)
        {
            ContainerItemSaveData item = sanitizedItems[i];
            string itemId = string.IsNullOrWhiteSpace(item.ItemData.ItemID) ? item.ItemData.name : item.ItemData.ItemID.Trim();
            if (!InventoryItemDatabase.TryResolve(itemId, out InventoryItemData definition) || definition != item.ItemData)
            {
                Debug.LogWarning($"[PlayerStorageService] Cannot persist item '{itemId}': runtime definition is missing or ambiguous.", this);
                return false;
            }
            if (!CanFitOnEmptyStoragePage(item))
            {
                Debug.LogWarning(
                    $"[PlayerStorageService] Cannot append item '{item.ItemData.ItemName}' because it is larger than a storage page.",
                    this);
                return false;
            }
        }

        for (int i = 0; i < sanitizedItems.Count; i++)
        {
            if (!TryAppendItemToAnyPage(sanitizedItems[i], out ContainerItemSaveData placedItem))
            {
                return false;
            }

            int amount = Mathf.Max(1, placedItem.Amount);
            appendedItemCount += amount;
            appendedTotalValue += Mathf.Max(0, placedItem.ItemData.SellPrice) * amount;
        }

        EnsureTrailingBlankPage();
        Save();
        return true;
    }

    public bool PageHasItems(int pageIndex)
    {
        StoragePageSaveRecord page = GetOrCreatePage(pageIndex);
        return page.Items != null && page.Items.Count > 0;
    }

    public int CountOccupiedCells(int pageIndex)
    {
        return CountOccupiedCells(GetPageItems(pageIndex));
    }

    public bool EnsureTrailingBlankPage()
    {
        EnsurePageCount(MinimumPageCount);
        if (_activePlayer == null || _activePlayer.Pages == null || _activePlayer.Pages.Count == 0)
        {
            return false;
        }

        StoragePageSaveRecord lastPage = _activePlayer.Pages[_activePlayer.Pages.Count - 1];
        if (lastPage.Items == null || lastPage.Items.Count == 0)
        {
            return false;
        }

        _activePlayer.Pages.Add(CreateEmptyPage(_activePlayer.Pages.Count));
        return true;
    }

    public void EnsurePageCount(int pageCount)
    {
        if (_activePlayer == null)
        {
            LoadForAgent(FallbackAgentId);
        }

        while (_activePlayer.Pages.Count < Mathf.Max(1, pageCount))
        {
            _activePlayer.Pages.Add(CreateEmptyPage(_activePlayer.Pages.Count));
        }
    }

    public void Save()
    {
        if (_activePlayer != null)
        {
            NormalizePages(_activePlayer);
        }

        string directory = Path.GetDirectoryName(SaveFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(_saveFile, true);
        File.WriteAllText(SaveFilePath, json);
    }

    public List<ContainerItemSaveData> SanitizeStorageItems(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> sanitized = new List<ContainerItemSaveData>();
        if (source == null)
        {
            return sanitized;
        }

        foreach (ContainerItemSaveData item in source)
        {
            ContainerItemSaveData copy = SanitizeStorageItem(item);
            if (copy != null)
            {
                sanitized.Add(copy);
            }
        }

        return sanitized;
    }

    private bool TryAppendItemToAnyPage(ContainerItemSaveData item, out ContainerItemSaveData placedItem)
    {
        placedItem = null;
        if (item == null || item.ItemData == null)
        {
            return false;
        }

        for (int pageIndex = 0; ; pageIndex++)
        {
            EnsurePageCount(pageIndex + 1);
            StoragePageSaveRecord page = GetOrCreatePage(pageIndex);
            if (!TryPlaceItemOnPage(page, item, out placedItem))
            {
                continue;
            }

            StorageItemSaveRecord record = ConvertRuntimeItemToRecord(placedItem);
            if (record == null)
            {
                return false;
            }

            page.Items.Add(record);
            return true;
        }
    }

    private bool TryPlaceItemOnPage(
        StoragePageSaveRecord page,
        ContainerItemSaveData item,
        out ContainerItemSaveData placedItem)
    {
        placedItem = null;
        if (page == null || item == null || item.ItemData == null)
        {
            return false;
        }

        InventoryGridModel model = BuildPageLayoutModel(page);
        if (model == null) return false;
        if (!model.FindFirstAvailableSpace(
                Mathf.Max(1, item.ItemData.Width),
                Mathf.Max(1, item.ItemData.Height),
                out Vector2Int position,
                out bool needsRotation))
        {
            return false;
        }

        placedItem = item.DeepCopy();
        placedItem.X = position.x;
        placedItem.Y = position.y;
        placedItem.IsRotated = needsRotation;
        placedItem.InternalItems = new List<ContainerItemSaveData>();
        placedItem.InternalCellStates = new List<ContainerCellStateSaveData>();
        return true;
    }

    private InventoryGridModel BuildPageLayoutModel(StoragePageSaveRecord page)
    {
        InventoryGridModel model = new InventoryGridModel();
        model.Configure(Columns, Rows, new List<Vector2Int>(), true);
        model.ApplyRuntimeCellStates(page.CellStates);

        foreach (StorageItemSaveRecord record in page.Items)
        {
            ContainerItemSaveData existingItem = ConvertRecordToRuntimeItem(record);
            if (existingItem == null || existingItem.ItemData == null)
            {
                // 缺少定义时无法确定占格，不得把未知物品所在页面当作空闲空间。
                return null;
            }

            int width = existingItem.IsRotated ? existingItem.ItemData.Height : existingItem.ItemData.Width;
            int height = existingItem.IsRotated ? existingItem.ItemData.Width : existingItem.ItemData.Height;
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            if (!model.IsSpaceAvailable(existingItem.X, existingItem.Y, width, height)) return null;
            model.PlaceItem(null, existingItem.X, existingItem.Y, width, height, existingItem.IsRotated);
        }

        return model;
    }

    private bool CanFitOnEmptyStoragePage(ContainerItemSaveData item)
    {
        if (item == null || item.ItemData == null)
        {
            return false;
        }

        int itemWidth = Mathf.Max(1, item.ItemData.Width);
        int itemHeight = Mathf.Max(1, item.ItemData.Height);
        int columns = Mathf.Max(1, Columns);
        int rows = Mathf.Max(1, Rows);
        return (itemWidth <= columns && itemHeight <= rows) ||
               (itemHeight <= columns && itemWidth <= rows);
    }

    private void LoadSaveFileIfNeeded()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        if (!File.Exists(SaveFilePath))
        {
            _saveFile = new StorageSaveFile();
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            _saveFile = JsonUtility.FromJson<StorageSaveFile>(json) ?? new StorageSaveFile();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PlayerStorageService] Failed to load storage save. A new save will be used. {exception.Message}", this);
            _saveFile = new StorageSaveFile();
        }

        _saveFile.Players ??= new List<PlayerStorageSaveRecord>();
    }

    private PlayerStorageSaveRecord GetOrCreatePlayerRecord(string agentId)
    {
        _saveFile.Players ??= new List<PlayerStorageSaveRecord>();
        foreach (PlayerStorageSaveRecord player in _saveFile.Players)
        {
            if (player != null && string.Equals(player.AgentId, agentId, StringComparison.Ordinal))
            {
                player.Pages ??= new List<StoragePageSaveRecord>();
                return player;
            }
        }

        PlayerStorageSaveRecord newPlayer = new PlayerStorageSaveRecord
        {
            AgentId = agentId,
            Pages = new List<StoragePageSaveRecord>()
        };
        _saveFile.Players.Add(newPlayer);
        return newPlayer;
    }

    private StoragePageSaveRecord GetOrCreatePage(int pageIndex)
    {
        if (_activePlayer == null)
        {
            LoadForAgent(FallbackAgentId);
        }

        int normalizedIndex = Mathf.Max(0, pageIndex);
        EnsurePageCount(normalizedIndex + 1);
        StoragePageSaveRecord page = _activePlayer.Pages[normalizedIndex];
        page.PageIndex = normalizedIndex;
        page.Items ??= new List<StorageItemSaveRecord>();
        page.CellStates ??= new List<ContainerCellStateSaveData>();
        return page;
    }

    private void NormalizePages(PlayerStorageSaveRecord player)
    {
        if (player == null)
        {
            return;
        }

        player.Pages ??= new List<StoragePageSaveRecord>();
        Dictionary<int, StoragePageSaveRecord> pagesByIndex = new Dictionary<int, StoragePageSaveRecord>();
        int highestIndex = -1;
        foreach (StoragePageSaveRecord page in player.Pages)
        {
            if (page == null)
            {
                continue;
            }

            int pageIndex = Mathf.Max(0, page.PageIndex);
            page.PageIndex = pageIndex;
            page.Items ??= new List<StorageItemSaveRecord>();
            page.CellStates ??= new List<ContainerCellStateSaveData>();
            pagesByIndex[pageIndex] = page;
            highestIndex = Mathf.Max(highestIndex, pageIndex);
        }

        int targetCount = Mathf.Max(MinimumPageCount, highestIndex + 1);
        player.Pages.Clear();
        for (int i = 0; i < targetCount; i++)
        {
            player.Pages.Add(pagesByIndex.TryGetValue(i, out StoragePageSaveRecord page)
                ? page
                : CreateEmptyPage(i));
        }
    }

    private static StoragePageSaveRecord CreateEmptyPage(int pageIndex)
    {
        return new StoragePageSaveRecord
        {
            PageIndex = pageIndex,
            Items = new List<StorageItemSaveRecord>(),
            CellStates = new List<ContainerCellStateSaveData>()
        };
    }

    private static ContainerItemSaveData SanitizeStorageItem(ContainerItemSaveData source)
    {
        if (source == null || source.ItemData == null)
        {
            return null;
        }

        ContainerItemSaveData copy = source.DeepCopy();
        copy.RequiresSearch = false;
        copy.IsSearched = true;
        copy.SearchProgressSeconds = 0f;
        copy.SearchDurationSeconds = 0f;
        copy.InternalItems = SanitizeStorageItemsStatic(copy.InternalItems);
        copy.InternalCellStates = CloneCellStateList(copy.InternalCellStates);
        return copy;
    }

    private static List<ContainerItemSaveData> SanitizeStorageItemsStatic(List<ContainerItemSaveData> source)
    {
        List<ContainerItemSaveData> sanitized = new List<ContainerItemSaveData>();
        if (source == null)
        {
            return sanitized;
        }

        foreach (ContainerItemSaveData item in source)
        {
            ContainerItemSaveData copy = SanitizeStorageItem(item);
            if (copy != null)
            {
                sanitized.Add(copy);
            }
        }

        return sanitized;
    }

    private static List<StorageItemSaveRecord> ConvertRuntimeItemsToRecords(List<ContainerItemSaveData> items)
    {
        List<StorageItemSaveRecord> records = new List<StorageItemSaveRecord>();
        if (items == null)
        {
            return records;
        }

        foreach (ContainerItemSaveData item in items)
        {
            StorageItemSaveRecord record = ConvertRuntimeItemToRecord(item);
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static StorageItemSaveRecord ConvertRuntimeItemToRecord(ContainerItemSaveData item)
    {
        if (item == null || item.ItemData == null)
        {
            return null;
        }

        string itemId = !string.IsNullOrWhiteSpace(item.ItemData.ItemID)
            ? item.ItemData.ItemID.Trim()
            : item.ItemData.name;

        return new StorageItemSaveRecord
        {
            RuntimeItemId = item.RuntimeItemId,
            ItemID = itemId,
            Amount = item.Amount,
            X = item.X,
            Y = item.Y,
            IsRotated = item.IsRotated
        };
    }

    private static List<ContainerItemSaveData> ConvertRecordsToRuntimeItems(List<StorageItemSaveRecord> records)
    {
        List<ContainerItemSaveData> items = new List<ContainerItemSaveData>();
        if (records == null)
        {
            return items;
        }

        foreach (StorageItemSaveRecord record in records)
        {
            ContainerItemSaveData item = ConvertRecordToRuntimeItem(record);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static ContainerItemSaveData ConvertRecordToRuntimeItem(StorageItemSaveRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.ItemID))
        {
            return null;
        }

        if (!InventoryItemDatabase.TryResolve(record.ItemID, out InventoryItemData itemData))
        {
            Debug.LogWarning($"[PlayerStorageService] Missing InventoryItemData for storage item id '{record.ItemID}'.");
            return null;
        }

        return new ContainerItemSaveData
        {
            RuntimeItemId = record.RuntimeItemId,
            ItemData = itemData,
            Amount = Mathf.Max(1, record.Amount),
            X = record.X,
            Y = record.Y,
            IsRotated = record.IsRotated,
            RequiresSearch = false,
            IsSearched = true,
            SearchProgressSeconds = 0f,
            SearchDurationSeconds = 0f,
            InternalItems = new List<ContainerItemSaveData>(),
            InternalCellStates = new List<ContainerCellStateSaveData>()
        };
    }

    private static int CountOccupiedCells(List<ContainerItemSaveData> items)
    {
        int occupiedCells = 0;
        if (items == null)
        {
            return occupiedCells;
        }

        foreach (ContainerItemSaveData item in items)
        {
            if (item?.ItemData == null)
            {
                continue;
            }

            int width = item.IsRotated ? item.ItemData.Height : item.ItemData.Width;
            int height = item.IsRotated ? item.ItemData.Width : item.ItemData.Height;
            occupiedCells += Mathf.Max(1, width) * Mathf.Max(1, height);
        }

        return occupiedCells;
    }

    private static List<ContainerCellStateSaveData> CloneCellStateList(List<ContainerCellStateSaveData> source)
    {
        List<ContainerCellStateSaveData> clone = new List<ContainerCellStateSaveData>();
        if (source == null)
        {
            return clone;
        }

        foreach (ContainerCellStateSaveData state in source)
        {
            if (state != null)
            {
                clone.Add(state.DeepCopy());
            }
        }

        return clone;
    }

    [Serializable]
    private sealed class StorageSaveFile
    {
        public int Version = 1;
        public List<PlayerStorageSaveRecord> Players = new List<PlayerStorageSaveRecord>();
    }

    [Serializable]
    private sealed class PlayerStorageSaveRecord
    {
        public string AgentId;
        public List<StoragePageSaveRecord> Pages = new List<StoragePageSaveRecord>();
    }

    [Serializable]
    private sealed class StoragePageSaveRecord
    {
        public int PageIndex;
        public List<StorageItemSaveRecord> Items = new List<StorageItemSaveRecord>();
        public List<ContainerCellStateSaveData> CellStates = new List<ContainerCellStateSaveData>();
    }

    [Serializable]
    private sealed class StorageItemSaveRecord
    {
        public string RuntimeItemId;
        public string ItemID;
        public int Amount;
        public int X;
        public int Y;
        public bool IsRotated;
    }
}

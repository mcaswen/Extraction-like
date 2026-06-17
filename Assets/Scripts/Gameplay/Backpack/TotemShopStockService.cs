using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TotemShopStockService : MonoBehaviour
{
    private const string SaveFileName = "totem_shop_stock.json";

    [Header("Stock")]
    public int Columns = 4;
    public int Rows = 4;
    [Min(1)] public int RefreshIntervalMinutes = 30;
    public string FallbackAgentId = "default_player";

    [NonSerialized] private TotemShopStockSaveFile _saveFile = new TotemShopStockSaveFile();
    [NonSerialized] private PlayerTotemShopStockRecord _activePlayer;
    [NonSerialized] private string _activeAgentId;
    [NonSerialized] private bool _hasLoaded;

    public string ActiveAgentId => string.IsNullOrWhiteSpace(_activeAgentId) ? FallbackAgentId : _activeAgentId;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public void Configure(int columns, int rows, int refreshIntervalMinutes)
    {
        Columns = Mathf.Max(1, columns);
        Rows = Mathf.Max(1, rows);
        RefreshIntervalMinutes = Mathf.Max(1, refreshIntervalMinutes);
    }

    public void LoadForAgent(string agentId)
    {
        _activeAgentId = string.IsNullOrWhiteSpace(agentId) ? FallbackAgentId : agentId.Trim();
        LoadSaveFileIfNeeded();
        _activePlayer = GetOrCreatePlayerRecord(_activeAgentId);
        _activePlayer.StockItems ??= new List<TotemShopStockItemRecord>();
    }

    public bool EnsureStockCurrent(IReadOnlyList<TotemShopPoolEntry> candidates)
    {
        EnsureActivePlayer();

        long nowTicks = DateTime.UtcNow.Ticks;
        bool needsRefresh =
            _activePlayer.NextRefreshUtcTicks <= nowTicks ||
            _activePlayer.StockItems == null ||
            _activePlayer.StockItems.Count == 0;

        if (!needsRefresh)
        {
            return false;
        }

        GenerateNewStock(candidates);
        Save();
        return true;
    }

    public float GetSecondsUntilRefresh()
    {
        EnsureActivePlayer();
        long remainingTicks = _activePlayer.NextRefreshUtcTicks - DateTime.UtcNow.Ticks;
        if (remainingTicks <= 0)
        {
            return 0f;
        }

        return (float)TimeSpan.FromTicks(remainingTicks).TotalSeconds;
    }

    public List<TotemShopStockItemRuntime> GetRuntimeStockItems()
    {
        EnsureActivePlayer();

        List<TotemShopStockItemRuntime> result = new List<TotemShopStockItemRuntime>();
        if (_activePlayer.StockItems == null)
        {
            return result;
        }

        foreach (TotemShopStockItemRecord record in _activePlayer.StockItems)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.ItemID))
            {
                continue;
            }

            if (!InventoryItemDatabase.TryResolve(record.ItemID, out InventoryItemData itemData))
            {
                Debug.LogWarning($"[TotemShopStockService] Missing item data for shop item id '{record.ItemID}'.", this);
                continue;
            }

            result.Add(new TotemShopStockItemRuntime
            {
                StockItemId = record.StockItemId,
                ItemData = itemData,
                BuyPrice = Mathf.Max(0, record.BuyPrice),
                X = record.X,
                Y = record.Y,
                IsRotated = record.IsRotated,
                IsSold = record.IsSold
            });
        }

        return result;
    }

    public bool MarkSold(string stockItemId)
    {
        EnsureActivePlayer();
        if (string.IsNullOrWhiteSpace(stockItemId) || _activePlayer.StockItems == null)
        {
            return false;
        }

        foreach (TotemShopStockItemRecord item in _activePlayer.StockItems)
        {
            if (item != null && string.Equals(item.StockItemId, stockItemId, StringComparison.Ordinal))
            {
                if (item.IsSold)
                {
                    return false;
                }

                item.IsSold = true;
                return true;
            }
        }

        return false;
    }

    public void Save()
    {
        EnsureActivePlayer();

        string directory = Path.GetDirectoryName(SaveFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(_saveFile, true);
        File.WriteAllText(SaveFilePath, json);
    }

    private void GenerateNewStock(IReadOnlyList<TotemShopPoolEntry> candidates)
    {
        _activePlayer.StockItems = new List<TotemShopStockItemRecord>();

        List<TotemShopPoolEntry> validCandidates = BuildValidCandidates(candidates);
        if (validCandidates.Count == 0)
        {
            _activePlayer.NextRefreshUtcTicks = DateTime.UtcNow.AddMinutes(RefreshIntervalMinutes).Ticks;
            return;
        }

        InventoryGridModel model = new InventoryGridModel();
        model.Configure(Columns, Rows, new List<Vector2Int>(), true);

        int seed = DateTime.UtcNow.Ticks.GetHashCode() ^ ActiveAgentId.GetHashCode();
        System.Random random = new System.Random(seed);
        int guard = Mathf.Max(16, Columns * Rows * 8);
        while (guard-- > 0 && CanAnyCandidateFit(model, validCandidates))
        {
            TotemShopPoolEntry entry = PickWeightedCandidate(validCandidates, random);
            if (entry == null || entry.ItemData == null)
            {
                break;
            }

            if (!model.FindFirstAvailableSpace(
                    Mathf.Max(1, entry.ItemData.Width),
                    Mathf.Max(1, entry.ItemData.Height),
                    out Vector2Int position,
                    out bool needsRotation))
            {
                continue;
            }

            int width = needsRotation ? entry.ItemData.Height : entry.ItemData.Width;
            int height = needsRotation ? entry.ItemData.Width : entry.ItemData.Height;
            model.PlaceItem(null, position.x, position.y, width, height, needsRotation);

            _activePlayer.StockItems.Add(new TotemShopStockItemRecord
            {
                StockItemId = Guid.NewGuid().ToString("N"),
                ItemID = ResolveItemId(entry.ItemData),
                BuyPrice = ShopEconomyPriceUtility.ResolveBuyPrice(entry, this),
                X = position.x,
                Y = position.y,
                IsRotated = needsRotation,
                IsSold = false
            });
        }

        _activePlayer.NextRefreshUtcTicks = DateTime.UtcNow.AddMinutes(RefreshIntervalMinutes).Ticks;
    }

    private static List<TotemShopPoolEntry> BuildValidCandidates(IReadOnlyList<TotemShopPoolEntry> candidates)
    {
        List<TotemShopPoolEntry> valid = new List<TotemShopPoolEntry>();
        if (candidates == null)
        {
            return valid;
        }

        foreach (TotemShopPoolEntry candidate in candidates)
        {
            if (candidate?.ItemData == null)
            {
                continue;
            }

            if (candidate.ItemData.EquipmentKind != EquipmentSlotKind.Totem)
            {
                continue;
            }

            valid.Add(candidate);
        }

        return valid;
    }

    private static bool CanAnyCandidateFit(InventoryGridModel model, List<TotemShopPoolEntry> candidates)
    {
        if (model == null || candidates == null)
        {
            return false;
        }

        foreach (TotemShopPoolEntry candidate in candidates)
        {
            if (candidate?.ItemData == null)
            {
                continue;
            }

            if (model.FindFirstAvailableSpace(
                    Mathf.Max(1, candidate.ItemData.Width),
                    Mathf.Max(1, candidate.ItemData.Height),
                    out _,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    private static TotemShopPoolEntry PickWeightedCandidate(List<TotemShopPoolEntry> candidates, System.Random random)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        foreach (TotemShopPoolEntry candidate in candidates)
        {
            totalWeight += Mathf.Max(0.001f, candidate.Weight);
        }

        double roll = random.NextDouble() * totalWeight;
        float cumulative = 0f;
        foreach (TotemShopPoolEntry candidate in candidates)
        {
            cumulative += Mathf.Max(0.001f, candidate.Weight);
            if (roll <= cumulative)
            {
                return candidate;
            }
        }

        return candidates[candidates.Count - 1];
    }

    private static string ResolveItemId(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(itemData.ItemID) ? itemData.ItemID.Trim() : itemData.name;
    }

    private void EnsureActivePlayer()
    {
        if (_activePlayer == null)
        {
            LoadForAgent(FallbackAgentId);
        }
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
            _saveFile = new TotemShopStockSaveFile();
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            _saveFile = JsonUtility.FromJson<TotemShopStockSaveFile>(json) ?? new TotemShopStockSaveFile();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[TotemShopStockService] Failed to load stock save. A new save will be used. {exception.Message}", this);
            _saveFile = new TotemShopStockSaveFile();
        }

        _saveFile.Players ??= new List<PlayerTotemShopStockRecord>();
    }

    private PlayerTotemShopStockRecord GetOrCreatePlayerRecord(string agentId)
    {
        _saveFile.Players ??= new List<PlayerTotemShopStockRecord>();
        foreach (PlayerTotemShopStockRecord player in _saveFile.Players)
        {
            if (player != null && string.Equals(player.AgentId, agentId, StringComparison.Ordinal))
            {
                player.StockItems ??= new List<TotemShopStockItemRecord>();
                return player;
            }
        }

        PlayerTotemShopStockRecord newPlayer = new PlayerTotemShopStockRecord
        {
            AgentId = agentId,
            NextRefreshUtcTicks = 0,
            StockItems = new List<TotemShopStockItemRecord>()
        };
        _saveFile.Players.Add(newPlayer);
        return newPlayer;
    }

    [Serializable]
    private sealed class TotemShopStockSaveFile
    {
        public int Version = 1;
        public List<PlayerTotemShopStockRecord> Players = new List<PlayerTotemShopStockRecord>();
    }

    [Serializable]
    private sealed class PlayerTotemShopStockRecord
    {
        public string AgentId;
        public long NextRefreshUtcTicks;
        public List<TotemShopStockItemRecord> StockItems = new List<TotemShopStockItemRecord>();
    }

    [Serializable]
    private sealed class TotemShopStockItemRecord
    {
        public string StockItemId;
        public string ItemID;
        public int BuyPrice;
        public int X;
        public int Y;
        public bool IsRotated;
        public bool IsSold;
    }
}

public sealed class TotemShopStockItemRuntime
{
    public string StockItemId;
    public InventoryItemData ItemData;
    public int BuyPrice;
    public int X;
    public int Y;
    public bool IsRotated;
    public bool IsSold;
}


using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEconomyService : MonoBehaviour
{
    private const string SaveFileName = "player_economy.json";

    public static PlayerEconomyService Instance { get; private set; }

    [Header("Economy")]
    [Min(0)] public int DefaultGold = 1000;
    public string FallbackAgentId = "default_player";

    [NonSerialized] private EconomySaveFile _saveFile = new EconomySaveFile();
    [NonSerialized] private PlayerEconomySaveRecord _activePlayer;
    [NonSerialized] private string _activeAgentId;
    [NonSerialized] private bool _hasLoaded;

    public event Action<int> GoldChanged;

    public string ActiveAgentId => string.IsNullOrWhiteSpace(_activeAgentId) ? FallbackAgentId : _activeAgentId;
    public int Gold => Mathf.Max(0, _activePlayer != null ? _activePlayer.Gold : DefaultGold);

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

    public void LoadForAgent(string agentId)
    {
        _activeAgentId = string.IsNullOrWhiteSpace(agentId) ? FallbackAgentId : agentId.Trim();
        LoadSaveFileIfNeeded();
        _activePlayer = GetOrCreatePlayerRecord(_activeAgentId);
        _activePlayer.Gold = Mathf.Max(0, _activePlayer.Gold);
        GoldChanged?.Invoke(Gold);
    }

    public void AddGold(int amount)
    {
        EnsureActivePlayer();
        if (amount <= 0)
        {
            return;
        }

        _activePlayer.Gold = Mathf.Max(0, _activePlayer.Gold + amount);
        GoldChanged?.Invoke(_activePlayer.Gold);
    }

    public bool CanSpend(int amount)
    {
        EnsureActivePlayer();
        return amount >= 0 && Gold >= amount;
    }

    public bool TrySpend(int amount)
    {
        EnsureActivePlayer();
        if (amount < 0 || _activePlayer.Gold < amount)
        {
            return false;
        }

        _activePlayer.Gold -= amount;
        GoldChanged?.Invoke(_activePlayer.Gold);
        return true;
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
            _saveFile = new EconomySaveFile();
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            _saveFile = JsonUtility.FromJson<EconomySaveFile>(json) ?? new EconomySaveFile();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[PlayerEconomyService] Failed to load economy save. A new save will be used. {exception.Message}", this);
            _saveFile = new EconomySaveFile();
        }

        _saveFile.Players ??= new List<PlayerEconomySaveRecord>();
    }

    private PlayerEconomySaveRecord GetOrCreatePlayerRecord(string agentId)
    {
        _saveFile.Players ??= new List<PlayerEconomySaveRecord>();
        foreach (PlayerEconomySaveRecord player in _saveFile.Players)
        {
            if (player != null && string.Equals(player.AgentId, agentId, StringComparison.Ordinal))
            {
                return player;
            }
        }

        PlayerEconomySaveRecord newPlayer = new PlayerEconomySaveRecord
        {
            AgentId = agentId,
            Gold = Mathf.Max(0, DefaultGold)
        };
        _saveFile.Players.Add(newPlayer);
        return newPlayer;
    }

    [Serializable]
    private sealed class EconomySaveFile
    {
        public int Version = 1;
        public List<PlayerEconomySaveRecord> Players = new List<PlayerEconomySaveRecord>();
    }

    [Serializable]
    private sealed class PlayerEconomySaveRecord
    {
        public string AgentId;
        public int Gold;
    }
}


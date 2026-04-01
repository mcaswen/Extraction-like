using System;
using UnityEngine;

namespace BoardGame.Runtime
{
    /// <summary>
    /// BoardGame 对接背包网格时使用的布局配置�
    /// </summary>
    [Serializable]
    public sealed class BoardGameBagLayoutSettings
    {
        [SerializeField] private bool _enableBagSystem = false;
        [SerializeField] private int _playerInventoryColumns = 6;
        [SerializeField] private int _playerInventoryRows = 4;
        [SerializeField] private int _lootContainerRows = 3;
        [SerializeField] private int _minimumLootContainerColumns = 3;

        public bool EnableBagSystem
        {
            get => _enableBagSystem;
            set => _enableBagSystem = value;
        }

        public int PlayerInventoryColumns
        {
            get => Mathf.Max(1, _playerInventoryColumns);
            set => _playerInventoryColumns = Mathf.Max(1, value);
        }

        public int PlayerInventoryRows
        {
            get => Mathf.Max(1, _playerInventoryRows);
            set => _playerInventoryRows = Mathf.Max(1, value);
        }

        public int LootContainerRows
        {
            get => Mathf.Max(1, _lootContainerRows);
            set => _lootContainerRows = Mathf.Max(1, value);
        }

        public int MinimumLootContainerColumns
        {
            get => Mathf.Max(1, _minimumLootContainerColumns);
            set => _minimumLootContainerColumns = Mathf.Max(1, value);
        }

        public int ResolveLootContainerColumns(int itemCount)
        {
            int rows = LootContainerRows;
            int requiredColumns = itemCount <= 0
                ? MinimumLootContainerColumns
                : Mathf.CeilToInt(itemCount / (float)rows);
            return Mathf.Max(MinimumLootContainerColumns, requiredColumns);
        }
    }
}

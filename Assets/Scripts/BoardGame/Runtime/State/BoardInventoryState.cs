using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 角色背包运行时状态
    /// </summary>
    [Serializable]
    public sealed class BoardInventoryState
    {
        // 当前已持有的全部物品实例
        [SerializeField] private List<BoardItemInstance> _items = new List<BoardItemInstance>();

        public List<BoardItemInstance> Items => _items;
        public int TotalValue => _items.Sum(item => item.Value);

        /// <summary>
        /// 获取当前背包里的全部消耗品实例
        /// </summary>
        public IEnumerable<BoardItemInstance> GetConsumables()
        {
            return _items.Where(item => item.ItemCategory == BoardItemCategory.Consumable);
        }
    }
}

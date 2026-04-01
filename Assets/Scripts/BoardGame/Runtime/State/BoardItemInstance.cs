using System;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 运行时物品实例，既可挂在节点奖励上，也可放入角色背包
    /// </summary>
    [Serializable]
    public sealed class BoardItemInstance
    {
        // 运行时实例唯一 ID
        [SerializeField] private string _instanceId;
        // 物品模板 ID
        [SerializeField] private string _itemId;
        // 物品显示名称
        [SerializeField] private string _displayName;
        // 物品大类
        [SerializeField] private BoardItemCategory _itemCategory;
        // 物品稀有度
        [SerializeField] private BoardItemRarity _itemRarity;
        // 物品价值
        [SerializeField] private int _value;
        // 消耗品类型
        [SerializeField] private BoardConsumableType _consumableType;
        // 消耗品使用数值
        [SerializeField] private int _consumeValue;

        public BoardItemInstance(
            string itemId,
            string displayName,
            BoardItemCategory itemCategory,
            BoardItemRarity itemRarity,
            int value,
            BoardConsumableType consumableType = BoardConsumableType.None,
            int consumeValue = 0)
        {
            _instanceId = Guid.NewGuid().ToString("N");
            _itemId = itemId;
            _displayName = displayName;
            _itemCategory = itemCategory;
            _itemRarity = itemRarity;
            _value = value;
            _consumableType = consumableType;
            _consumeValue = Mathf.Max(0, consumeValue);
        }

        public string InstanceId => _instanceId;
        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public BoardItemCategory ItemCategory => _itemCategory;
        public BoardItemRarity ItemRarity => _itemRarity;
        public int Value => _value;
        public BoardConsumableType ConsumableType => _consumableType;
        public int ConsumeValue => _consumeValue;

        /// <summary>
        /// 复制一个新的物品实例，用于运行时掉落生成
        /// </summary>
        public BoardItemInstance Clone()
        {
            return new BoardItemInstance(
                _itemId,
                _displayName,
                _itemCategory,
                _itemRarity,
                _value,
                _consumableType,
                _consumeValue);
        }
    }
}

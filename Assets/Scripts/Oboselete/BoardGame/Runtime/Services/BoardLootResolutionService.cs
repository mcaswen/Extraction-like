using System.Collections.Generic;
using System.Linq;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 掉落生成、自动收取和道具使用服务
    /// </summary>
    public sealed class BoardLootResolutionService
    {
        private readonly Dictionary<string, BoardItemDefinition> _itemDefinitionsById =
            new Dictionary<string, BoardItemDefinition>();

        private readonly Dictionary<BoardResourceTier, BoardResourceLootPoolDefinition> _resourcePoolsByTier =
            new Dictionary<BoardResourceTier, BoardResourceLootPoolDefinition>();

        private readonly List<BoardEncounterLootPoolDefinition> _encounterPools =
            new List<BoardEncounterLootPoolDefinition>();

        public BoardLootResolutionService(SO_BoardGame_LootTableSet lootTableSet)
        {
            // 物品模板按 ID 建索引，便于掉落池快速引用
            foreach (BoardItemDefinition itemDefinition in lootTableSet.ItemDefinitions)
            {
                _itemDefinitionsById[itemDefinition.ItemId] = itemDefinition;
            }

            // 资源点掉落池按资源等级建索引
            foreach (BoardResourceLootPoolDefinition resourcePool in lootTableSet.ResourcePools)
            {
                _resourcePoolsByTier[resourcePool.ResourceTier] = resourcePool;
            }

            _encounterPools.AddRange(lootTableSet.EncounterPools);
        }

        /// <summary>
        /// 生成开局自带道具
        /// 当前仅用于注入初始血瓶
        /// </summary>
        public List<BoardItemInstance> CreateStartingItems(int startingHealingPotionCount)
        {
            List<BoardItemInstance> startingItems = new List<BoardItemInstance>();

            if (!_itemDefinitionsById.TryGetValue("healing_potion", out BoardItemDefinition potionDefinition))
            {
                return startingItems;
            }

            for (int index = 0; index < startingHealingPotionCount; index++)
            {
                startingItems.Add(CreateItemInstance(potionDefinition));
            }

            return startingItems;
        }

        /// <summary>
        /// 资源点搜索完成后生成掉落
        /// 同一个资源点只会生成一次掉落结果
        /// </summary>
        public List<BoardItemInstance> GenerateResourceLoot(BoardNodeRuntimeState nodeState)
        {
            if (nodeState.HasGeneratedResourceLoot)
            {
                return nodeState.GeneratedResourceItems;
            }

            if (!_resourcePoolsByTier.TryGetValue(nodeState.ResourceTier, out BoardResourceLootPoolDefinition resourcePool))
            {
                nodeState.HasGeneratedResourceLoot = true;
                return nodeState.GeneratedResourceItems;
            }

            int rollCount = Random.Range(resourcePool.MinRollCount, resourcePool.MaxRollCount + 1);

            for (int index = 0; index < rollCount; index++)
            {
                BoardItemDefinition pickedItem = PickWeightedItem(resourcePool.Entries);

                if (pickedItem != null)
                {
                    nodeState.GeneratedResourceItems.Add(CreateItemInstance(pickedItem));
                }
            }

            nodeState.HasGeneratedResourceLoot = true;
            return nodeState.GeneratedResourceItems;
        }

        /// <summary>
        /// 普通敌人或 Boss 被击败后生成掉落
        /// </summary>
        public List<BoardItemInstance> GenerateEncounterLoot(BoardNodeRuntimeState nodeState, bool isBoss)
        {
            List<BoardItemInstance> generatedItems = new List<BoardItemInstance>();
            BoardEncounterLootPoolDefinition encounterPool = FindEncounterPool(isBoss, nodeState.DangerTier);

            if (encounterPool == null)
            {
                return generatedItems;
            }

            int rollCount = Random.Range(encounterPool.MinRollCount, encounterPool.MaxRollCount + 1);

            for (int index = 0; index < rollCount; index++)
            {
                BoardItemDefinition pickedItem = PickWeightedItem(encounterPool.Entries);

                if (pickedItem != null)
                {
                    generatedItems.Add(CreateItemInstance(pickedItem));
                }
            }

            if (_itemDefinitionsById.TryGetValue("healing_potion", out BoardItemDefinition potionDefinition) &&
                Random.value <= encounterPool.HealingPotionChance)
            {
                generatedItems.Add(CreateItemInstance(potionDefinition));
            }

            return generatedItems;
        }

        /// <summary>
        /// 将一批掉落转成节点可持久化的战利品容器内容，按生成顺序从左到右摆放
        /// </summary>
        public void PrepareNodeLootContainer(
            BoardNodeRuntimeState nodeState,
            IReadOnlyList<BoardItemInstance> generatedItems,
            BoardGameBagLayoutSettings layoutSettings)
        {
            float preservedSearchRequiredSeconds = nodeState.SearchRequiredSeconds;
            float preservedSearchProgressSeconds = nodeState.SearchProgressSeconds;
            nodeState.ResetLootContainer();

            if (generatedItems == null || generatedItems.Count == 0)
            {
                return;
            }

            int rows = layoutSettings != null ? layoutSettings.LootContainerRows : 3;
            int columns = layoutSettings != null
                ? layoutSettings.ResolveLootContainerColumns(generatedItems.Count)
                : Mathf.Max(3, Mathf.CeilToInt(generatedItems.Count / 3f));

            nodeState.LootContainerColumns = columns;
            nodeState.LootContainerRows = rows;
            nodeState.LootTotalItemCount = generatedItems.Count;
            nodeState.LootRevealedItemCount = 0;

            for (int index = 0; index < generatedItems.Count; index++)
            {
                BoardItemInstance itemInstance = generatedItems[index];

                if (itemInstance == null)
                {
                    continue;
                }

                int x = index % columns;
                int y = index / columns;
                float revealDuration = ResolveRevealDurationSeconds(itemInstance);

                // reveal 顺序直接跟随生成顺序，方便搜索流程和关闭回写时稳定复原
                nodeState.LootContainerItems.Add(new BoardLootContainerItemState(
                    itemInstance,
                    x,
                    y,
                    index,
                    revealDuration));
            }

            if (layoutSettings != null && layoutSettings.EnableBagSystem)
            {
                nodeState.SyncSearchProgressFromLootReveal();
            }
            else
            {
                nodeState.SearchRequiredSeconds = Mathf.Max(0.1f, preservedSearchRequiredSeconds);
                nodeState.SearchProgressSeconds = Mathf.Clamp(
                    preservedSearchProgressSeconds,
                    0f,
                    nodeState.SearchRequiredSeconds);
            }
        }

        /// <summary>
        /// 关闭背包系统时，按旧的数字容量规则自动收取掉落
        /// 空间不足时会按单位容量价值替换低收益物品
        /// </summary>
        public BoardAutoCollectResult AutoCollect(BoardInventoryState inventoryState, IEnumerable<BoardItemInstance> sourceItems)
        {
            List<BoardItemInstance> addedItems = new List<BoardItemInstance>();
            List<BoardItemInstance> droppedItems = new List<BoardItemInstance>();
            List<BoardItemInstance> replacedItems = new List<BoardItemInstance>();

            if (inventoryState == null || sourceItems == null)
            {
                return new BoardAutoCollectResult(addedItems, droppedItems, replacedItems, string.Empty);
            }

            foreach (BoardItemInstance sourceItem in sourceItems
                         .Where(item => item != null)
                         .OrderByDescending(item => item.UnitValue))
            {
                // 自动收取按单位容量价值从高到低处理，尽量保证有限容量先装进更值钱的物品
                BoardItemInstance itemToAdd = sourceItem.Clone();

                if (TryAddItemWithReplacement(inventoryState, itemToAdd, out List<BoardItemInstance> removedItems))
                {
                    addedItems.Add(itemToAdd);
                    replacedItems.AddRange(removedItems);
                }
                else
                {
                    droppedItems.Add(sourceItem);
                }
            }

            string summary = $"Auto collected {addedItems.Count} item(s), dropped {droppedItems.Count} item(s)";
            return new BoardAutoCollectResult(addedItems, droppedItems, replacedItems, summary);
        }

        /// <summary>
        /// 使用一个背包内的可消耗物品
        /// </summary>
        public bool TryConsumeItem(BoardAgentState agentState, string instanceId, out string message)
        {
            BoardItemInstance itemInstance = agentState.InventoryState.Items.FirstOrDefault(item => item.InstanceId == instanceId);

            if (itemInstance == null)
            {
                message = "No usable item was found";
                return false;
            }

            if (itemInstance.ItemCategory != BoardItemCategory.Consumable || itemInstance.ConsumableType == BoardConsumableType.None)
            {
                message = "This item is not usable";
                return false;
            }

            if (itemInstance.ConsumableType == BoardConsumableType.HealingPotion)
            {
                int previousHealth = agentState.CurrentHealth;
                agentState.CurrentHealth = Mathf.Min(agentState.MaxHealth, agentState.CurrentHealth + itemInstance.ConsumeValue);
                agentState.InventoryState.Items.Remove(itemInstance);
                message = $"Used {itemInstance.DisplayName}, HP restored from {previousHealth} to {agentState.CurrentHealth}";
                return true;
            }

            message = "This consumable is not implemented yet";
            return false;
        }

        /// <summary>
        /// 查找敌人或 Boss 对应的掉落池
        /// </summary>
        private BoardEncounterLootPoolDefinition FindEncounterPool(bool isBoss, BoardDangerTier dangerTier)
        {
            return _encounterPools.FirstOrDefault(pool => pool.IsBoss == isBoss && pool.DangerTier == dangerTier);
        }

        /// <summary>
        /// 在一个带权重的候选列表中随机选出一个物品模板
        /// </summary>
        private BoardItemDefinition PickWeightedItem(IReadOnlyList<BoardWeightedItemReference> entries)
        {
            float totalWeight = 0f;

            foreach (BoardWeightedItemReference entry in entries)
            {
                totalWeight += Mathf.Max(0f, entry.Weight);
            }

            if (totalWeight <= Mathf.Epsilon)
            {
                return null;
            }

            float roll = Random.Range(0f, totalWeight);
            float cursor = 0f;

            foreach (BoardWeightedItemReference entry in entries)
            {
                cursor += Mathf.Max(0f, entry.Weight);

                if (roll > cursor)
                {
                    continue;
                }

                return _itemDefinitionsById.TryGetValue(entry.ItemId, out BoardItemDefinition itemDefinition)
                    ? itemDefinition
                    : null;
            }

            return null;
        }

        /// <summary>
        /// 由物品模板生成具体的运行时实例
        /// </summary>
        private static BoardItemInstance CreateItemInstance(BoardItemDefinition itemDefinition)
        {
            int value = Random.Range(itemDefinition.MinValue, itemDefinition.MaxValue + 1);
            return new BoardItemInstance(
                itemDefinition.ItemId,
                itemDefinition.DisplayName,
                itemDefinition.ItemCategory,
                itemDefinition.ItemRarity,
                value,
                itemDefinition.CapacityCost,
                itemDefinition.ConsumableType,
                itemDefinition.ConsumeValue);
        }

        /// <summary>
        /// 解析单个物品的揭露时长，优先读取物品配置，缺失时退回稀有度默认值
        /// </summary>
        private float ResolveRevealDurationSeconds(BoardItemInstance itemInstance)
        {
            if (itemInstance != null &&
                !string.IsNullOrEmpty(itemInstance.ItemId) &&
                _itemDefinitionsById.TryGetValue(itemInstance.ItemId, out BoardItemDefinition itemDefinition) &&
                itemDefinition != null)
            {
                return itemDefinition.RevealDurationSeconds;
            }

            return BoardItemDefinition.GetDefaultRevealDurationSeconds(
                itemInstance != null ? itemInstance.ItemRarity : BoardItemRarity.Common);
        }

        /// <summary>
        /// 尝试将单个物品加入背包
        /// 若空间不足，则移除单位容量价值更低的旧物品来腾位置
        /// </summary>
        private static bool TryAddItemWithReplacement(
            BoardInventoryState inventoryState,
            BoardItemInstance itemToAdd,
            out List<BoardItemInstance> replacedItems)
        {
            replacedItems = new List<BoardItemInstance>();

            if (inventoryState == null || itemToAdd == null)
            {
                return false;
            }

            if (inventoryState.UsedCapacity + itemToAdd.CapacityCost <= inventoryState.MaxCapacity + 0.001f)
            {
                inventoryState.Items.Add(itemToAdd);
                return true;
            }

            List<BoardItemInstance> sortedItems = inventoryState.Items
                .Where(item => item != null)
                .OrderBy(item => item.UnitValue)
                .ThenBy(item => item.Value)
                .ToList();

            foreach (BoardItemInstance existingItem in sortedItems)
            {
                // 一旦轮到的旧物品已经不比新物品差，就说明继续替换也不会让收益更高
                if (existingItem.UnitValue >= itemToAdd.UnitValue)
                {
                    break;
                }

                replacedItems.Add(existingItem);
                inventoryState.Items.Remove(existingItem);

                if (inventoryState.UsedCapacity + itemToAdd.CapacityCost <= inventoryState.MaxCapacity + 0.001f)
                {
                    inventoryState.Items.Add(itemToAdd);
                    return true;
                }
            }

            foreach (BoardItemInstance replacedItem in replacedItems)
            {
                inventoryState.Items.Add(replacedItem);
            }

            replacedItems.Clear();
            return false;
        }
    }

    /// <summary>
    /// 自动收取结果快照
    /// 记录本次加入、丢弃和替换的物品集合以及摘要文本
    /// </summary>
    public sealed class BoardAutoCollectResult
    {
        /// <summary>
        /// 创建一份自动收取结果
        /// </summary>
        /// <param name="addedItems"></param>
        /// <param name="droppedItems"></param>
        /// <param name="replacedItems"></param>
        /// <param name="summary"></param>
        public BoardAutoCollectResult(
            List<BoardItemInstance> addedItems,
            List<BoardItemInstance> droppedItems,
            List<BoardItemInstance> replacedItems,
            string summary)
        {
            AddedItems = addedItems ?? new List<BoardItemInstance>();
            DroppedItems = droppedItems ?? new List<BoardItemInstance>();
            ReplacedItems = replacedItems ?? new List<BoardItemInstance>();
            Summary = summary ?? string.Empty;
        }

        public List<BoardItemInstance> AddedItems { get; }
        public List<BoardItemInstance> DroppedItems { get; }
        public List<BoardItemInstance> ReplacedItems { get; }
        public string Summary { get; }
    }
}

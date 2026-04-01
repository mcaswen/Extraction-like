using System.Collections.Generic;
using System.Linq;
using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 掉落生成和道具使用服务
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
        /// 将一批掉落转成节点可持久化的战利品容器内容，按生成顺序从左到右摆放。
        /// </summary>
        public void PrepareNodeLootContainer(
            BoardNodeRuntimeState nodeState,
            IReadOnlyList<BoardItemInstance> generatedItems,
            BoardGameBagLayoutSettings layoutSettings)
        {
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
                nodeState.LootContainerItems.Add(new BoardLootContainerItemState(
                    itemInstance,
                    x,
                    y,
                    index,
                    GetRevealDurationSeconds(itemInstance.ItemRarity)));
            }

            nodeState.SyncSearchProgressFromLootReveal();
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
                itemDefinition.ConsumableType,
                itemDefinition.ConsumeValue);
        }

        private static float GetRevealDurationSeconds(BoardItemRarity rarity)
        {
            switch (rarity)
            {
                case BoardItemRarity.Common:
                    return 0.45f;
                case BoardItemRarity.Uncommon:
                    return 0.75f;
                case BoardItemRarity.Rare:
                    return 1.1f;
                case BoardItemRarity.Epic:
                    return 1.55f;
                case BoardItemRarity.Legendary:
                    return 2.1f;
                default:
                    return 0.45f;
            }
        }
    }

}

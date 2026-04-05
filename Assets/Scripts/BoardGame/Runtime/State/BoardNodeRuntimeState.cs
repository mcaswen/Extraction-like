using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 单个节点的运行时状态，记录搜索、战斗、撤离等过程数据
    /// </summary>
    [Serializable]
    public sealed class BoardNodeRuntimeState
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private string _description;
        [SerializeField] private BoardNodeType _nodeType;
        [SerializeField] private BoardResourceTier _resourceTier;
        [SerializeField] private BoardDangerTier _dangerTier;

        [SerializeField] private BoardResourceStateType _resourceState = BoardResourceStateType.Unsearched;
        [SerializeField] private float _searchProgressSeconds;
        [SerializeField] private float _searchRequiredSeconds;
        [SerializeField] private bool _hasGeneratedResourceLoot;
        [SerializeField] private List<BoardItemInstance> _generatedResourceItems = new List<BoardItemInstance>();
        [SerializeField] private List<BoardLootContainerItemState> _lootContainerItems = new List<BoardLootContainerItemState>();
        [SerializeField] private int _lootContainerColumns = 3;
        [SerializeField] private int _lootContainerRows = 3;
        [SerializeField] private int _lootTotalItemCount;
        [SerializeField] private int _lootRevealedItemCount;

        [SerializeField] private BoardEnemyStateType _enemyState = BoardEnemyStateType.Unengaged;
        [SerializeField] private int _enemyCurrentHealth;
        [SerializeField] private int _enemyMaxHealth;
        [SerializeField] private int _enemyAttack;
        [SerializeField] private int _enemyDefense;

        [SerializeField] private BoardBossStateType _bossState = BoardBossStateType.Untriggered;
        [SerializeField] private int _bossCurrentHealth;
        [SerializeField] private int _bossMaxHealth;
        [SerializeField] private int _bossAttack;
        [SerializeField] private int _bossDefense;

        [SerializeField] private BoardExtractStateType _extractState = BoardExtractStateType.Available;
        [SerializeField] private float _extractProgressSeconds;
        [SerializeField] private float _extractRequiredSeconds;

        public BoardNodeRuntimeState(
            string nodeId,
            string description,
            BoardNodeType nodeType,
            BoardResourceTier resourceTier,
            BoardDangerTier dangerTier)
        {
            _nodeId = nodeId;
            _description = description;
            _nodeType = nodeType;
            _resourceTier = resourceTier;
            _dangerTier = dangerTier;
        }

        public string NodeId => _nodeId;
        public string Description => _description;
        public BoardNodeType NodeType => _nodeType;
        public BoardResourceTier ResourceTier => _resourceTier;
        public BoardDangerTier DangerTier => _dangerTier;

        public BoardResourceStateType ResourceState
        {
            get => _resourceState;
            set => _resourceState = value;
        }

        public float SearchProgressSeconds
        {
            get => _searchProgressSeconds;
            set => _searchProgressSeconds = Mathf.Max(0f, value);
        }

        public float SearchRequiredSeconds
        {
            get => _searchRequiredSeconds;
            set => _searchRequiredSeconds = Mathf.Max(0.1f, value);
        }

        public bool HasGeneratedResourceLoot
        {
            get => _hasGeneratedResourceLoot;
            set => _hasGeneratedResourceLoot = value;
        }

        public List<BoardItemInstance> GeneratedResourceItems => _generatedResourceItems;
        public List<BoardLootContainerItemState> LootContainerItems => _lootContainerItems;

        public int LootContainerColumns
        {
            get => Mathf.Max(1, _lootContainerColumns);
            set => _lootContainerColumns = Mathf.Max(1, value);
        }

        public int LootContainerRows
        {
            get => Mathf.Max(1, _lootContainerRows);
            set => _lootContainerRows = Mathf.Max(1, value);
        }

        public int LootTotalItemCount
        {
            get => Mathf.Max(0, _lootTotalItemCount);
            set => _lootTotalItemCount = Mathf.Max(0, value);
        }

        public int LootRevealedItemCount
        {
            get => Mathf.Clamp(_lootRevealedItemCount, 0, LootTotalItemCount);
            set => _lootRevealedItemCount = Mathf.Clamp(value, 0, LootTotalItemCount);
        }

        public BoardEnemyStateType EnemyState
        {
            get => _enemyState;
            set => _enemyState = value;
        }

        public int EnemyCurrentHealth
        {
            get => _enemyCurrentHealth;
            set => _enemyCurrentHealth = Mathf.Max(0, value);
        }

        public int EnemyMaxHealth
        {
            get => _enemyMaxHealth;
            set => _enemyMaxHealth = Mathf.Max(0, value);
        }

        public int EnemyAttack
        {
            get => _enemyAttack;
            set => _enemyAttack = Mathf.Max(0, value);
        }

        public int EnemyDefense
        {
            get => _enemyDefense;
            set => _enemyDefense = Mathf.Max(0, value);
        }

        public BoardBossStateType BossState
        {
            get => _bossState;
            set => _bossState = value;
        }

        public int BossCurrentHealth
        {
            get => _bossCurrentHealth;
            set => _bossCurrentHealth = Mathf.Max(0, value);
        }

        public int BossMaxHealth
        {
            get => _bossMaxHealth;
            set => _bossMaxHealth = Mathf.Max(0, value);
        }

        public int BossAttack
        {
            get => _bossAttack;
            set => _bossAttack = Mathf.Max(0, value);
        }

        public int BossDefense
        {
            get => _bossDefense;
            set => _bossDefense = Mathf.Max(0, value);
        }

        public BoardExtractStateType ExtractState
        {
            get => _extractState;
            set => _extractState = value;
        }

        public float ExtractProgressSeconds
        {
            get => _extractProgressSeconds;
            set => _extractProgressSeconds = Mathf.Max(0f, value);
        }

        public float ExtractRequiredSeconds
        {
            get => _extractRequiredSeconds;
            set => _extractRequiredSeconds = Mathf.Max(0.1f, value);
        }

        /// <summary>
        /// 当前资源点是否仍有未完成的搜索过程
        /// </summary>
        public bool HasUnfinishedSearch()
        {
            return _nodeType == BoardNodeType.Resource && _resourceState != BoardResourceStateType.Looted;
        }

        public bool HasPendingLootContainer()
        {
            return _lootTotalItemCount > 0;
        }

        /// <summary>
        /// 查询容器里是否还有可被玩家带走的剩余战利品
        /// </summary>
        public bool HasRemainingLootItems()
        {
            return _lootContainerItems.Count > 0;
        }

        /// <summary>
        /// 查询当前容器里是否还存在未揭露物品
        /// </summary>
        public bool HasHiddenLootItems()
        {
            foreach (BoardLootContainerItemState itemState in _lootContainerItems)
            {
                if (itemState != null && !itemState.IsRevealed)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsLootRevealComplete()
        {
            return _lootTotalItemCount <= 0 || LootRevealedItemCount >= _lootTotalItemCount;
        }

        public float GetLootRevealProgress01()
        {
            return _lootTotalItemCount <= 0
                ? 1f
                : Mathf.Clamp01((float)LootRevealedItemCount / _lootTotalItemCount);
        }

        /// <summary>
        /// 获取当前 loot 搜查的连续进度。
        /// 除了已完整揭示的物品外，还会把当前正在揭示的那一件物品的部分进度计入。
        /// </summary>
        /// <summary>
        /// 获取当前 loot 搜查的连续进度。
        /// 除了已完整揭示的物品外，还会把当前正在揭示的那一件物品的部分进度计入。
        /// </summary>
        public float GetLootRevealProgressWithPartial01()
        {
            if (_lootTotalItemCount <= 0)
            {
                return 1f;
            }

            float progress = LootRevealedItemCount;
            BoardLootContainerItemState currentHiddenItem = null;
            int bestRevealSequence = int.MaxValue;

            foreach (BoardLootContainerItemState itemState in _lootContainerItems)
            {
                if (itemState == null || itemState.IsRevealed || itemState.RevealSequenceIndex >= bestRevealSequence)
                {
                    continue;
                }

                bestRevealSequence = itemState.RevealSequenceIndex;
                currentHiddenItem = itemState;
            }

            if (currentHiddenItem != null)
            {
                progress += currentHiddenItem.RevealProgress01;
            }

            return Mathf.Clamp01(progress / _lootTotalItemCount);
        }

        /// <summary>
        /// 清空节点上的 loot 容器，并把搜索进度同步回空状态
        /// </summary>
        public void ResetLootContainer()
        {
            _lootContainerItems.Clear();
            _lootContainerColumns = 3;
            _lootContainerRows = 3;
            _lootTotalItemCount = 0;
            _lootRevealedItemCount = 0;
            SyncSearchProgressFromLootReveal();
        }

        /// <summary>
        /// 用 reveal 进度反推资源点的搜索进度
        /// 让节点表现和 loot 面板始终引用同一套完成度
        /// </summary>
        public void SyncSearchProgressFromLootReveal()
        {
            if (_nodeType != BoardNodeType.Resource)
            {
                return;
            }

            // 资源点仍沿用搜索进度条表现，所以这里把每件 loot 抽象成一段统一的 reveal 进度
            _searchRequiredSeconds = Mathf.Max(1f, _lootTotalItemCount);
            _searchProgressSeconds = Mathf.Clamp(_lootRevealedItemCount, 0f, _searchRequiredSeconds);
        }

        /// <summary>
        /// 当前普通敌人点是否尚未被清除
        /// </summary>
        public bool HasUnclearedEnemy()
        {
            return _nodeType == BoardNodeType.Enemy && _enemyState != BoardEnemyStateType.Cleared;
        }

        /// <summary>
        /// 当前 Boss 点是否尚未被击败
        /// </summary>
        public bool HasUndefeatedBoss()
        {
            return _nodeType == BoardNodeType.Boss && _bossState != BoardBossStateType.Defeated;
        }

        /// <summary>
        /// 查询该节点当前是否存在“做到一半”的过程
        /// </summary>
        public bool IsPartiallyProcessed()
        {
            switch (_nodeType)
            {
                case BoardNodeType.Resource:
                    return _searchProgressSeconds > 0f && _resourceState != BoardResourceStateType.Looted;
                case BoardNodeType.Enemy:
                    return _enemyCurrentHealth > 0 && _enemyCurrentHealth < _enemyMaxHealth;
                case BoardNodeType.Boss:
                    return _bossCurrentHealth > 0 && _bossCurrentHealth < _bossMaxHealth;
                case BoardNodeType.Extract:
                    return _extractProgressSeconds > 0f && _extractState != BoardExtractStateType.Extracted;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取节点当前状态的中文标签
        /// </summary>
        public string GetStatusLabel()
        {
            switch (_nodeType)
            {
                case BoardNodeType.Resource:
                    return BoardGameTypes.GetResourceStateLabel(_resourceState);
                case BoardNodeType.Enemy:
                    return BoardGameTypes.GetEnemyStateLabel(_enemyState);
                case BoardNodeType.Boss:
                    return BoardGameTypes.GetBossStateLabel(_bossState);
                case BoardNodeType.Extract:
                    return BoardGameTypes.GetExtractStateLabel(_extractState);
                default:
                    return "Idle";
            }
        }
    }
}

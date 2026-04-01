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
        // 节点唯一 ID
        [SerializeField] private string _nodeId;
        // 节点补充说明
        [SerializeField] private string _description;
        // 节点主类型
        [SerializeField] private BoardNodeType _nodeType;
        // 资源等级，仅资源点使用
        [SerializeField] private BoardResourceTier _resourceTier;
        // 危险等级，仅敌人点和 Boss 点使用
        [SerializeField] private BoardDangerTier _dangerTier;

        // 资源点当前状态
        [SerializeField] private BoardResourceStateType _resourceState = BoardResourceStateType.Unsearched;
        // 资源点已累计搜索时长
        [SerializeField] private float _searchProgressSeconds;
        // 资源点搜索总时长
        [SerializeField] private float _searchRequiredSeconds;
        // 是否已经生成过资源奖励实例
        [SerializeField] private bool _hasGeneratedResourceLoot;
        // 资源点已生成的掉落实例缓存
        [SerializeField] private List<BoardItemInstance> _generatedResourceItems = new List<BoardItemInstance>();
        // 节点当前战利品容器内剩余物品
        [SerializeField] private List<BoardLootContainerItemState> _lootContainerItems = new List<BoardLootContainerItemState>();
        // 战利品容器列数
        [SerializeField] private int _lootContainerColumns = 3;
        // 战利品容器行数
        [SerializeField] private int _lootContainerRows = 3;
        // 该节点本次总共生成的战利品件数
        [SerializeField] private int _lootTotalItemCount;
        // 已揭露的战利品件数
        [SerializeField] private int _lootRevealedItemCount;

        // 普通敌人点当前状态
        [SerializeField] private BoardEnemyStateType _enemyState = BoardEnemyStateType.Unengaged;
        // 普通敌人当前生命值
        [SerializeField] private int _enemyCurrentHealth;
        // 普通敌人最大生命值
        [SerializeField] private int _enemyMaxHealth;
        // 普通敌人攻击力
        [SerializeField] private int _enemyAttack;
        // 普通敌人防御力
        [SerializeField] private int _enemyDefense;

        // Boss 点当前状态
        [SerializeField] private BoardBossStateType _bossState = BoardBossStateType.Untriggered;
        // Boss 当前生命值
        [SerializeField] private int _bossCurrentHealth;
        // Boss 最大生命值
        [SerializeField] private int _bossMaxHealth;
        // Boss 攻击力
        [SerializeField] private int _bossAttack;
        // Boss 防御力
        [SerializeField] private int _bossDefense;

        // 撤离点当前状态
        [SerializeField] private BoardExtractStateType _extractState = BoardExtractStateType.Available;
        // 撤离已累计进度
        [SerializeField] private float _extractProgressSeconds;
        // 撤离总时长
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

        public bool HasRemainingLootItems()
        {
            return _lootContainerItems.Count > 0;
        }

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

        public void ResetLootContainer()
        {
            _lootContainerItems.Clear();
            _lootContainerColumns = 3;
            _lootContainerRows = 3;
            _lootTotalItemCount = 0;
            _lootRevealedItemCount = 0;
            SyncSearchProgressFromLootReveal();
        }

        public void SyncSearchProgressFromLootReveal()
        {
            if (_nodeType != BoardNodeType.Resource)
            {
                return;
            }

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

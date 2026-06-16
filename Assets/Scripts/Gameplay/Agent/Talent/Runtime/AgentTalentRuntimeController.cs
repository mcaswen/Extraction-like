using System.Collections.Generic;
using Gameplay.Agent.Combat;
using UnityEngine;

namespace Gameplay.Agent.Talent
{
    /// <summary>
    /// Agent 局内天赋运行时状态。
    /// 负责记录已解锁节点，并把节点效果转换为战斗属性、技能修正和被动效果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentTalentRuntimeController : MonoBehaviour
    {
        private const string EarthStoneWallSkillId = "earth_stone_wall";
        private const string EarthQuakeFieldSkillId = "earth_quake_field";
        private const string IceFrostAssaultSkillId = "ice_frost_assault";
        private const string IceWinterfallSkillId = "ice_winterfall";

        private const float FlatStatBonus = 1f;
        private const float ExperienceGainBonus = 0.05f;
        private const float StoneWallDurationBonusSeconds = 3f;
        private const float QuakeDamageMultiplierBonus = 0.2f;
        private const float FrostAssaultSlowDurationBonusSeconds = 3f;
        private const float WinterfallDamageMultiplierBonus = 0.5f;
        private const float EarthShieldDefenseMultiplier = 2f;
        private const float EarthShieldDurationSeconds = 5f;
        private const float EarthShieldCooldownSeconds = 120f;
        private const float IceKillAttackBonusPerStack = 0.1f;
        private const int IceKillAttackMaxStacks = 5;

        private static readonly AgentTalentNodeId[] EarthDesignNodes =
        {
            AgentTalentNodeId.EarthAttack1,
            AgentTalentNodeId.EarthHealth1,
            AgentTalentNodeId.EarthStoneWallDuration,
            AgentTalentNodeId.EarthDefense1,
            AgentTalentNodeId.EarthExperienceGain1,
            AgentTalentNodeId.EarthQuakeDamagePerSecond,
            AgentTalentNodeId.EarthAttack2,
            AgentTalentNodeId.EarthReactiveShield
        };

        private static readonly AgentTalentNodeId[] IceDesignNodes =
        {
            AgentTalentNodeId.IceAttack1,
            AgentTalentNodeId.IceMaxHealth1,
            AgentTalentNodeId.IceFrostAssaultSlowDuration,
            AgentTalentNodeId.IceDefense1,
            AgentTalentNodeId.IceExperienceGain1,
            AgentTalentNodeId.IceWinterfallDamage,
            AgentTalentNodeId.IceAttack2,
            AgentTalentNodeId.IceKillAttackStack
        };

        private static readonly AgentTalentNodeId[] EmptyPrerequisites = new AgentTalentNodeId[0];
        private static readonly AgentTalentNodeId[] EarthRootPrerequisites =
        {
            AgentTalentNodeId.EarthAttack1
        };
        private static readonly AgentTalentNodeId[] EarthStoneWallPrerequisites =
        {
            AgentTalentNodeId.EarthHealth1
        };
        private static readonly AgentTalentNodeId[] EarthDefensePrerequisites =
        {
            AgentTalentNodeId.EarthStoneWallDuration
        };
        private static readonly AgentTalentNodeId[] EarthQuakePrerequisites =
        {
            AgentTalentNodeId.EarthExperienceGain1
        };
        private static readonly AgentTalentNodeId[] EarthAttack2Prerequisites =
        {
            AgentTalentNodeId.EarthQuakeDamagePerSecond
        };
        private static readonly AgentTalentNodeId[] EarthFinalPrerequisites =
        {
            AgentTalentNodeId.EarthDefense1,
            AgentTalentNodeId.EarthAttack2
        };
        private static readonly AgentTalentNodeId[] IceRootPrerequisites =
        {
            AgentTalentNodeId.IceAttack1
        };
        private static readonly AgentTalentNodeId[] IceFrostPrerequisites =
        {
            AgentTalentNodeId.IceMaxHealth1
        };
        private static readonly AgentTalentNodeId[] IceDefensePrerequisites =
        {
            AgentTalentNodeId.IceFrostAssaultSlowDuration
        };
        private static readonly AgentTalentNodeId[] IceWinterfallPrerequisites =
        {
            AgentTalentNodeId.IceExperienceGain1
        };
        private static readonly AgentTalentNodeId[] IceAttack2Prerequisites =
        {
            AgentTalentNodeId.IceWinterfallDamage
        };
        private static readonly AgentTalentNodeId[] IceFinalPrerequisites =
        {
            AgentTalentNodeId.IceDefense1,
            AgentTalentNodeId.IceAttack2
        };

        [Header("Initial Unlocks")]
        [SerializeField] private AgentTalentNodeId[] _initialUnlockedNodes;
        [SerializeField] private bool _unlockAllEarthOnAwake;
        [SerializeField] private bool _unlockAllIceOnAwake;
        [SerializeField, Min(0)] private int _availableTalentPoints;

        private readonly HashSet<AgentTalentNodeId> _unlockedNodes = new HashSet<AgentTalentNodeId>();
        private readonly HashSet<int> _creditedEnemyInstanceIds = new HashSet<int>();

        private int _iceKillAttackStacks;
        private float _earthShieldValue;
        private double _earthShieldExpiresAt;
        private double _nextEarthShieldReadyAt;
        private bool _hasAppliedInitialUnlocks;
        private bool _isApplyingInitialUnlocks;

        /// <summary>
        /// 当前已解锁节点集合。
        /// </summary>
        public IReadOnlyCollection<AgentTalentNodeId> UnlockedNodes
        {
            get
            {
                EnsureInitialUnlocksApplied();
                return _unlockedNodes;
            }
        }

        /// <summary>
        /// 冰系击杀攻击力叠层数。
        /// </summary>
        public int IceKillAttackStacks
        {
            get
            {
                EnsureInitialUnlocksApplied();
                return _iceKillAttackStacks;
            }
        }

        /// <summary>
        /// 当前可用于解锁天赋节点的未消耗点数。
        /// </summary>
        public int AvailableTalentPoints
        {
            get
            {
                EnsureInitialUnlocksApplied();
                return Mathf.Max(0, _availableTalentPoints);
            }
        }

        /// <summary>
        /// 当前是否至少有一个可消耗天赋点。
        /// </summary>
        public bool HasAvailableTalentPoints => AvailableTalentPoints > 0;

        /// <summary>
        /// 当前土系受击护盾剩余吸收量。
        /// </summary>
        public float ActiveEarthShieldValue
        {
            get
            {
                EnsureInitialUnlocksApplied();
                ExpireEarthShield(Time.timeAsDouble);
                return _earthShieldValue;
            }
        }

        /// <summary>
        /// 当前经验获取倍率。
        /// </summary>
        public float ExperienceGainMultiplier
        {
            get
            {
                EnsureInitialUnlocksApplied();
                int bonusCount = CountUnlocked(
                    AgentTalentNodeId.EarthExperienceGain1,
                    AgentTalentNodeId.IceExperienceGain1);
                return 1f + bonusCount * ExperienceGainBonus;
            }
        }

        private void Awake()
        {
            EnsureInitialUnlocksApplied();
        }

        /// <summary>
        /// 确保序列化的初始解锁已经写入运行时状态。
        /// 可由同物体上的其他组件在 Awake 中安全调用。
        /// </summary>
        public void EnsureInitialUnlocksApplied()
        {
            if (_hasAppliedInitialUnlocks)
                return;

            _hasAppliedInitialUnlocks = true;
            ApplyInitialUnlocks();
        }

        /// <summary>
        /// 直接解锁单个天赋节点。
        /// 该方法不检查前置节点，适合存档读入或调试。
        /// </summary>
        /// <param name="nodeId">要解锁的节点 ID。</param>
        /// <returns>本次调用是否新增了解锁节点。</returns>
        public bool UnlockNode(AgentTalentNodeId nodeId)
        {
            EnsureInitialUnlocksApplied();
            bool didUnlock = _unlockedNodes.Add(nodeId);
            if (didUnlock && Application.isPlaying && !_isApplyingInitialUnlocks)
            {
                global::AgentSfxEmitter sfxEmitter = GetComponent<global::AgentSfxEmitter>();
                if (sfxEmitter != null)
                    sfxEmitter.PlayUpgrade();
                else
                    global::GameSfxPlayer.PlayAiUpgrade(transform.position);
            }

            return didUnlock;
        }

        /// <summary>
        /// 在满足前置条件时解锁单个天赋节点。
        /// </summary>
        /// <param name="nodeId">要解锁的节点 ID。</param>
        /// <returns>成功解锁时返回 true。</returns>
        public bool TryUnlockNode(AgentTalentNodeId nodeId)
        {
            if (!HasUnlockedPrerequisites(nodeId) || _availableTalentPoints <= 0)
                return false;

            if (!UnlockNode(nodeId))
                return false;

            _availableTalentPoints = Mathf.Max(0, _availableTalentPoints - 1);
            return true;
        }

        /// <summary>
        /// 查询指定节点当前是否满足解锁条件。
        /// </summary>
        /// <param name="nodeId">要查询的节点 ID。</param>
        /// <returns>节点未解锁且所有前置节点已解锁时返回 true。</returns>
        public bool CanUnlockNode(AgentTalentNodeId nodeId)
        {
            return HasUnlockedPrerequisites(nodeId) && AvailableTalentPoints > 0;
        }

        /// <summary>
        /// 查询指定节点是否满足前置节点条件，不检查可用天赋点。
        /// </summary>
        /// <param name="nodeId">要查询的节点 ID。</param>
        /// <returns>前置条件已满足时返回 true。</returns>
        public bool HasUnlockedPrerequisites(AgentTalentNodeId nodeId)
        {
            EnsureInitialUnlocksApplied();
            if (_unlockedNodes.Contains(nodeId))
                return false;

            IReadOnlyList<AgentTalentNodeId> prerequisites = GetPrerequisites(nodeId);
            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (!_unlockedNodes.Contains(prerequisites[i]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 增加可用天赋点。升级系统在 Agent 升级时调用。
        /// </summary>
        /// <param name="points">新增点数。</param>
        public void AddTalentPoints(int points)
        {
            EnsureInitialUnlocksApplied();
            if (points <= 0)
                return;

            _availableTalentPoints += points;
        }

        /// <summary>
        /// 获取指定节点在策划树上的前置节点。
        /// </summary>
        /// <param name="nodeId">要查询的节点 ID。</param>
        /// <returns>前置节点 ID 列表。</returns>
        public IReadOnlyList<AgentTalentNodeId> GetPrerequisites(AgentTalentNodeId nodeId)
        {
            switch (nodeId)
            {
                case AgentTalentNodeId.EarthHealth1:
                case AgentTalentNodeId.EarthExperienceGain1:
                    return EarthRootPrerequisites;
                case AgentTalentNodeId.EarthStoneWallDuration:
                    return EarthStoneWallPrerequisites;
                case AgentTalentNodeId.EarthDefense1:
                    return EarthDefensePrerequisites;
                case AgentTalentNodeId.EarthQuakeDamagePerSecond:
                    return EarthQuakePrerequisites;
                case AgentTalentNodeId.EarthAttack2:
                    return EarthAttack2Prerequisites;
                case AgentTalentNodeId.EarthReactiveShield:
                    return EarthFinalPrerequisites;
                case AgentTalentNodeId.IceMaxHealth1:
                case AgentTalentNodeId.IceExperienceGain1:
                    return IceRootPrerequisites;
                case AgentTalentNodeId.IceFrostAssaultSlowDuration:
                    return IceFrostPrerequisites;
                case AgentTalentNodeId.IceDefense1:
                    return IceDefensePrerequisites;
                case AgentTalentNodeId.IceWinterfallDamage:
                    return IceWinterfallPrerequisites;
                case AgentTalentNodeId.IceAttack2:
                    return IceAttack2Prerequisites;
                case AgentTalentNodeId.IceKillAttackStack:
                    return IceFinalPrerequisites;
                default:
                    return EmptyPrerequisites;
            }
        }

        /// <summary>
        /// 批量解锁天赋节点。
        /// </summary>
        /// <param name="nodeIds">要解锁的节点 ID 列表。</param>
        public void UnlockNodes(IEnumerable<AgentTalentNodeId> nodeIds)
        {
            EnsureInitialUnlocksApplied();
            if (nodeIds == null)
                return;

            foreach (AgentTalentNodeId nodeId in nodeIds)
                UnlockNode(nodeId);
        }

        /// <summary>
        /// 设置指定节点的解锁状态。
        /// </summary>
        /// <param name="nodeId">要修改的节点 ID。</param>
        /// <param name="isUnlocked">是否解锁。</param>
        public void SetNodeUnlocked(AgentTalentNodeId nodeId, bool isUnlocked)
        {
            EnsureInitialUnlocksApplied();
            if (isUnlocked)
                _unlockedNodes.Add(nodeId);
            else
                _unlockedNodes.Remove(nodeId);
        }

        /// <summary>
        /// 查询指定节点是否已解锁。
        /// </summary>
        /// <param name="nodeId">要查询的节点 ID。</param>
        /// <returns>节点已解锁时返回 true。</returns>
        public bool IsUnlocked(AgentTalentNodeId nodeId)
        {
            EnsureInitialUnlocksApplied();
            return _unlockedNodes.Contains(nodeId);
        }

        /// <summary>
        /// 清空所有天赋节点和运行时被动状态。
        /// </summary>
        public void ClearUnlocks()
        {
            EnsureInitialUnlocksApplied();
            _unlockedNodes.Clear();
            _creditedEnemyInstanceIds.Clear();
            _availableTalentPoints = 0;
            _iceKillAttackStacks = 0;
            _earthShieldValue = 0f;
            _earthShieldExpiresAt = 0d;
            _nextEarthShieldReadyAt = 0d;
        }

        /// <summary>
        /// 按当前策划图解锁全部土系节点。
        /// </summary>
        public void UnlockAllEarthDesignNodes()
        {
            UnlockNodes(EarthDesignNodes);
        }

        /// <summary>
        /// 按当前策划图解锁全部冰系节点。
        /// </summary>
        public void UnlockAllIceDesignNodes()
        {
            UnlockNodes(IceDesignNodes);
        }

        /// <summary>
        /// 将天赋中的属性节点应用到战斗属性快照。
        /// </summary>
        /// <param name="baseStats">基础战斗属性。</param>
        /// <returns>应用天赋后的战斗属性。</returns>
        public AgentCombatRuntimeStats ApplyStatModifiers(AgentCombatRuntimeStats baseStats)
        {
            EnsureInitialUnlocksApplied();
            float attack = baseStats.Attack + CountUnlocked(
                AgentTalentNodeId.EarthAttack1,
                AgentTalentNodeId.EarthAttack2,
                AgentTalentNodeId.IceAttack1,
                AgentTalentNodeId.IceAttack2) * FlatStatBonus;
            float defense = baseStats.Defense + CountUnlocked(
                AgentTalentNodeId.EarthDefense1,
                AgentTalentNodeId.IceDefense1) * FlatStatBonus;
            int maxHealth = ApplyMaxHealthModifier(baseStats.MaxHealth);

            if (_unlockedNodes.Contains(AgentTalentNodeId.IceKillAttackStack) && _iceKillAttackStacks > 0)
            {
                float multiplier = 1f + _iceKillAttackStacks * IceKillAttackBonusPerStack;
                attack *= multiplier;
            }

            return new AgentCombatRuntimeStats(maxHealth, attack, defense);
        }

        /// <summary>
        /// 将天赋中的生命节点应用到最大生命值。
        /// </summary>
        /// <param name="baseMaxHealth">基础最大生命值。</param>
        /// <returns>应用天赋后的最大生命值。</returns>
        public int ApplyMaxHealthModifier(int baseMaxHealth)
        {
            EnsureInitialUnlocksApplied();
            int bonusCount = CountUnlocked(
                AgentTalentNodeId.EarthHealth1,
                AgentTalentNodeId.IceMaxHealth1);
            return Mathf.Max(1, baseMaxHealth + Mathf.RoundToInt(bonusCount * FlatStatBonus));
        }

        /// <summary>
        /// 根据技能 ID 创建本次释放使用的技能修正。
        /// </summary>
        /// <param name="skillId">技能配置上的稳定 ID。</param>
        /// <returns>技能伤害、持续时间或状态时长修正。</returns>
        public AgentCombatSkillModifiers CreateSkillModifiers(string skillId)
        {
            EnsureInitialUnlocksApplied();
            float damageMultiplier = 1f;
            float durationBonusSeconds = 0f;
            float slowDurationBonusSeconds = 0f;

            if (MatchesSkillId(skillId, EarthStoneWallSkillId) &&
                _unlockedNodes.Contains(AgentTalentNodeId.EarthStoneWallDuration))
            {
                durationBonusSeconds += StoneWallDurationBonusSeconds;
            }

            if (MatchesSkillId(skillId, EarthQuakeFieldSkillId) &&
                _unlockedNodes.Contains(AgentTalentNodeId.EarthQuakeDamagePerSecond))
            {
                damageMultiplier += QuakeDamageMultiplierBonus;
            }

            if (MatchesSkillId(skillId, IceFrostAssaultSkillId) &&
                _unlockedNodes.Contains(AgentTalentNodeId.IceFrostAssaultSlowDuration))
            {
                slowDurationBonusSeconds += FrostAssaultSlowDurationBonusSeconds;
            }

            if (MatchesSkillId(skillId, IceWinterfallSkillId) &&
                _unlockedNodes.Contains(AgentTalentNodeId.IceWinterfallDamage))
            {
                damageMultiplier += WinterfallDamageMultiplierBonus;
            }

            return new AgentCombatSkillModifiers(
                damageMultiplier,
                durationBonusSeconds,
                slowDurationBonusSeconds);
        }

        /// <summary>
        /// 应用土系受击护盾被动，并返回护盾吸收后的剩余伤害。
        /// </summary>
        /// <param name="incomingDamage">本次即将承受的伤害。</param>
        /// <param name="currentDefense">当前防御力，用于计算护盾量。</param>
        /// <param name="timeSeconds">当前游戏时间。</param>
        /// <returns>护盾吸收后的剩余伤害。</returns>
        public float AbsorbIncomingDamage(float incomingDamage, float currentDefense, double timeSeconds)
        {
            EnsureInitialUnlocksApplied();
            float remainingDamage = Mathf.Max(0f, incomingDamage);
            if (remainingDamage <= 0f || !_unlockedNodes.Contains(AgentTalentNodeId.EarthReactiveShield))
                return remainingDamage;

            ExpireEarthShield(timeSeconds);

            if (_earthShieldValue <= 0f && timeSeconds >= _nextEarthShieldReadyAt)
                ActivateEarthShield(currentDefense, timeSeconds);

            if (_earthShieldValue <= 0f)
                return remainingDamage;

            float absorbedDamage = Mathf.Min(_earthShieldValue, remainingDamage);
            _earthShieldValue -= absorbedDamage;
            remainingDamage -= absorbedDamage;
            return remainingDamage;
        }

        /// <summary>
        /// 通知天赋系统 Agent 击杀了一个敌人。
        /// </summary>
        /// <param name="enemyHealthController">被击杀的敌人生命组件。</param>
        public void NotifyEnemyDefeated(global::EnemyHealthController enemyHealthController)
        {
            EnsureInitialUnlocksApplied();
            if (!_unlockedNodes.Contains(AgentTalentNodeId.IceKillAttackStack) || enemyHealthController == null)
                return;

            int enemyInstanceId = enemyHealthController.GetInstanceID();
            if (!_creditedEnemyInstanceIds.Add(enemyInstanceId))
                return;

            _iceKillAttackStacks = Mathf.Min(IceKillAttackMaxStacks, _iceKillAttackStacks + 1);
        }

        private void ApplyInitialUnlocks()
        {
            _isApplyingInitialUnlocks = true;
            UnlockNodes(_initialUnlockedNodes);

            if (_unlockAllEarthOnAwake)
                UnlockAllEarthDesignNodes();

            if (_unlockAllIceOnAwake)
                UnlockAllIceDesignNodes();

            _isApplyingInitialUnlocks = false;
        }

        private int CountUnlocked(params AgentTalentNodeId[] nodeIds)
        {
            int count = 0;
            for (int i = 0; i < nodeIds.Length; i++)
            {
                if (_unlockedNodes.Contains(nodeIds[i]))
                    count++;
            }

            return count;
        }

        private void ActivateEarthShield(float currentDefense, double timeSeconds)
        {
            float shieldAmount = Mathf.Max(0f, currentDefense) * EarthShieldDefenseMultiplier;
            if (shieldAmount <= 0f)
                return;

            _earthShieldValue = shieldAmount;
            _earthShieldExpiresAt = timeSeconds + EarthShieldDurationSeconds;
            _nextEarthShieldReadyAt = timeSeconds + EarthShieldCooldownSeconds;
        }

        private void ExpireEarthShield(double timeSeconds)
        {
            if (_earthShieldValue <= 0f)
                return;

            if (timeSeconds < _earthShieldExpiresAt)
                return;

            _earthShieldValue = 0f;
            _earthShieldExpiresAt = 0d;
        }

        private static bool MatchesSkillId(string skillId, string expectedSkillId)
        {
            return string.Equals(skillId, expectedSkillId, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System;
using System.Collections.Generic;
using Gameplay.Agent.Talent;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AgentTalentRuntimeController))]
    public sealed class AgentLevelProgressionController : MonoBehaviour
    {
        [Serializable]
        private sealed class SourceAwardCounter
        {
            [SerializeField] private AgentExperienceSourceKind _sourceKind;
            [SerializeField, Min(0)] private int _awardedExperience;

            public SourceAwardCounter()
            {
            }

            public SourceAwardCounter(AgentExperienceSourceKind sourceKind)
            {
                _sourceKind = sourceKind;
            }

            public AgentExperienceSourceKind SourceKind => _sourceKind;
            public int AwardedExperience
            {
                get => Mathf.Max(0, _awardedExperience);
                set => _awardedExperience = Mathf.Max(0, value);
            }
        }

        [Header("Config")]
        [SerializeField] private AgentLevelProgressionConfig _progressionConfig;

        [Header("Runtime State")]
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _currentExperience;
        [SerializeField] private List<SourceAwardCounter> _sourceAwardCounters = new List<SourceAwardCounter>();

        [Header("Debug")]
        [SerializeField] private bool _logExperienceAwards;

        private AgentTalentRuntimeController _talentController;

        public int Level => Mathf.Clamp(_level, 1, MaxLevel);
        public int CurrentExperience => Mathf.Max(0, _currentExperience);
        public int MaxLevel => Config != null ? Config.MaxLevel : 1;
        public bool IsMaxLevel => Level >= MaxLevel;
        public int RequiredExperienceToNextLevel =>
            IsMaxLevel || Config == null ? 0 : Config.ResolveExperienceToNextLevel(Level);
        public float ExperienceRatio =>
            RequiredExperienceToNextLevel > 0
                ? Mathf.Clamp01(CurrentExperience / (float)RequiredExperienceToNextLevel)
                : 1f;

        public event Action<AgentLevelProgressionController, int, AgentExperienceSourceKind> ExperienceAwarded;
        public event Action<AgentLevelProgressionController, int, int> LeveledUp;

        private AgentLevelProgressionConfig Config
        {
            get
            {
                if (_progressionConfig == null)
                    _progressionConfig = Resources.Load<AgentLevelProgressionConfig>(
                        AgentLevelProgressionConfig.DefaultResourcesPath);

                return _progressionConfig;
            }
        }

        private void Awake()
        {
            CacheComponents();
            ClampRuntimeState();
        }

        private void OnValidate()
        {
            ClampRuntimeState();
        }

        public int NotifyEnemyDefeated(global::EnemyHealthController enemy)
        {
            return TryResolveEnemySourceKind(enemy, out AgentExperienceSourceKind sourceKind)
                ? AddExperience(sourceKind, enemy)
                : 0;
        }

        public int NotifyLootBoxOpened(global::LootBoxEntity lootBox)
        {
            return TryResolveLootBoxSourceKind(lootBox, out AgentExperienceSourceKind sourceKind)
                ? AddExperience(sourceKind, lootBox)
                : 0;
        }

        public int AddExperience(AgentExperienceSourceKind sourceKind, UnityEngine.Object contextObject = null)
        {
            AgentLevelProgressionConfig config = Config;
            if (config == null || IsMaxLevel)
                return 0;

            int baseAmount = config.ResolveExperienceAmount(sourceKind);
            if (baseAmount <= 0)
                return 0;

            CacheComponents();
            float gainMultiplier = _talentController != null ? _talentController.ExperienceGainMultiplier : 1f;
            int requestedAmount = Mathf.Max(0, Mathf.RoundToInt(baseAmount * Mathf.Max(0f, gainMultiplier)));
            int clampedAmount = ClampBySourceCap(config, sourceKind, requestedAmount);
            if (clampedAmount <= 0)
                return 0;

            GetOrCreateCounter(sourceKind).AwardedExperience += clampedAmount;
            _currentExperience += clampedAmount;
            ExperienceAwarded?.Invoke(this, clampedAmount, sourceKind);

            if (_logExperienceAwards)
            {
                Debug.Log(
                    $"[AgentXP] {name} +{clampedAmount} XP from {sourceKind}, level={Level}, xp={CurrentExperience}/{RequiredExperienceToNextLevel}.",
                    contextObject != null ? contextObject : this);
            }

            ResolveLevelUps();
            return clampedAmount;
        }

        public int GetAwardedExperienceForSource(AgentExperienceSourceKind sourceKind)
        {
            SourceAwardCounter counter = FindCounter(sourceKind);
            return counter != null ? counter.AwardedExperience : 0;
        }

        private void ResolveLevelUps()
        {
            AgentLevelProgressionConfig config = Config;
            if (config == null)
                return;

            while (!IsMaxLevel)
            {
                int requiredExperience = config.ResolveExperienceToNextLevel(Level);
                if (requiredExperience <= 0 || _currentExperience < requiredExperience)
                    break;

                _currentExperience -= requiredExperience;
                _level = Mathf.Min(MaxLevel, _level + 1);

                int grantedTalentPoints = config.ResolveTalentPointsOnReachLevel(Level);
                if (grantedTalentPoints > 0)
                {
                    CacheComponents();
                    _talentController?.AddTalentPoints(grantedTalentPoints);
                }

                LeveledUp?.Invoke(this, Level, grantedTalentPoints);

                if (_logExperienceAwards)
                {
                    Debug.Log(
                        $"[AgentXP] {name} reached level {Level}, talentPoints+={grantedTalentPoints}.",
                        this);
                }
            }

            if (IsMaxLevel)
                _currentExperience = 0;
        }

        private int ClampBySourceCap(
            AgentLevelProgressionConfig config,
            AgentExperienceSourceKind sourceKind,
            int requestedAmount)
        {
            int cap = config.ResolveExperienceCap(sourceKind);
            if (cap <= 0)
                return requestedAmount;

            int alreadyAwarded = GetAwardedExperienceForSource(sourceKind);
            int remaining = Mathf.Max(0, cap - alreadyAwarded);
            return Mathf.Min(requestedAmount, remaining);
        }

        private SourceAwardCounter GetOrCreateCounter(AgentExperienceSourceKind sourceKind)
        {
            SourceAwardCounter counter = FindCounter(sourceKind);
            if (counter != null)
                return counter;

            counter = new SourceAwardCounter(sourceKind);
            _sourceAwardCounters.Add(counter);
            return counter;
        }

        private SourceAwardCounter FindCounter(AgentExperienceSourceKind sourceKind)
        {
            _sourceAwardCounters ??= new List<SourceAwardCounter>();
            for (int i = 0; i < _sourceAwardCounters.Count; i++)
            {
                SourceAwardCounter counter = _sourceAwardCounters[i];
                if (counter != null && counter.SourceKind == sourceKind)
                    return counter;
            }

            return null;
        }

        private void CacheComponents()
        {
            if (_talentController == null)
                _talentController = GetComponent<AgentTalentRuntimeController>();
        }

        private void ClampRuntimeState()
        {
            _level = Mathf.Max(1, _level);
            _currentExperience = Mathf.Max(0, _currentExperience);
            _sourceAwardCounters ??= new List<SourceAwardCounter>();

            for (int i = _sourceAwardCounters.Count - 1; i >= 0; i--)
            {
                if (_sourceAwardCounters[i] == null)
                    _sourceAwardCounters.RemoveAt(i);
                else
                    _sourceAwardCounters[i].AwardedExperience = _sourceAwardCounters[i].AwardedExperience;
            }
        }

        private static bool TryResolveEnemySourceKind(
            global::EnemyHealthController enemy,
            out AgentExperienceSourceKind sourceKind)
        {
            if (enemy != null && enemy.GetComponentInChildren<global::HunterBossBehaviorController>(true) != null)
            {
                sourceKind = AgentExperienceSourceKind.BossEnemy;
                return true;
            }

            SceneEnemyDangerTier dangerTier = SceneEnemyDangerTier.Low;
            GameplayTargetRegistry registry = GameplayTargetRegistry.ActiveInstance;
            if (registry != null &&
                registry.TryFindEnemySourceClusterByEnemy(enemy, out EnemySourceClusterAuthoring sourceCluster) &&
                sourceCluster != null)
            {
                dangerTier = sourceCluster.DangerTier;
            }

            switch (dangerTier)
            {
                case SceneEnemyDangerTier.Medium:
                    sourceKind = AgentExperienceSourceKind.MediumDifficultyEnemy;
                    return true;
                case SceneEnemyDangerTier.High:
                    sourceKind = AgentExperienceSourceKind.HighDifficultyEnemy;
                    return true;
                default:
                    sourceKind = AgentExperienceSourceKind.LowDifficultyEnemy;
                    return true;
            }
        }

        private static bool TryResolveLootBoxSourceKind(
            global::LootBoxEntity lootBox,
            out AgentExperienceSourceKind sourceKind)
        {
            sourceKind = AgentExperienceSourceKind.LowTierLootBox;
            if (lootBox == null)
                return false;

            switch (lootBox.ResolveExperienceResourceTier())
            {
                case global::SceneResourceTier.Medium:
                    sourceKind = AgentExperienceSourceKind.MediumTierLootBox;
                    return true;
                case global::SceneResourceTier.High:
                    sourceKind = AgentExperienceSourceKind.HighTierLootBox;
                    return true;
                default:
                    sourceKind = AgentExperienceSourceKind.LowTierLootBox;
                    return true;
            }
        }
    }
}

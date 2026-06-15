using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Progression
{
    public enum AgentExperienceSourceKind
    {
        LowDifficultyEnemy,
        MediumDifficultyEnemy,
        HighDifficultyEnemy,
        BossEnemy,
        LowTierLootBox,
        MediumTierLootBox,
        HighTierLootBox
    }

    [Serializable]
    public sealed class AgentExperienceSourceRule
    {
        [SerializeField] private AgentExperienceSourceKind _sourceKind;
        [SerializeField, Min(0)] private int _experienceAmount;
        [SerializeField, Min(0)] private int _experienceCap;

        public AgentExperienceSourceRule()
        {
        }

        public AgentExperienceSourceRule(
            AgentExperienceSourceKind sourceKind,
            int experienceAmount,
            int experienceCap)
        {
            _sourceKind = sourceKind;
            _experienceAmount = Mathf.Max(0, experienceAmount);
            _experienceCap = Mathf.Max(0, experienceCap);
        }

        public AgentExperienceSourceKind SourceKind => _sourceKind;
        public int ExperienceAmount => Mathf.Max(0, _experienceAmount);
        public int ExperienceCap => Mathf.Max(0, _experienceCap);

        public void Validate()
        {
            _experienceAmount = Mathf.Max(0, _experienceAmount);
            _experienceCap = Mathf.Max(0, _experienceCap);
        }
    }

    [Serializable]
    public sealed class AgentLevelProgressionRule
    {
        [SerializeField, Min(1)] private int _level = 1;
        [SerializeField, Min(0)] private int _experienceToNextLevel;
        [SerializeField, Min(0)] private int _talentPointsGrantedOnReachLevel;

        public AgentLevelProgressionRule()
        {
        }

        public AgentLevelProgressionRule(
            int level,
            int experienceToNextLevel,
            int talentPointsGrantedOnReachLevel)
        {
            _level = Mathf.Max(1, level);
            _experienceToNextLevel = Mathf.Max(0, experienceToNextLevel);
            _talentPointsGrantedOnReachLevel = Mathf.Max(0, talentPointsGrantedOnReachLevel);
        }

        public int Level => Mathf.Max(1, _level);
        public int ExperienceToNextLevel => Mathf.Max(0, _experienceToNextLevel);
        public int TalentPointsGrantedOnReachLevel => Mathf.Max(0, _talentPointsGrantedOnReachLevel);

        public void Validate()
        {
            _level = Mathf.Max(1, _level);
            _experienceToNextLevel = Mathf.Max(0, _experienceToNextLevel);
            _talentPointsGrantedOnReachLevel = Mathf.Max(0, _talentPointsGrantedOnReachLevel);
        }
    }

    [CreateAssetMenu(
        fileName = "SO_AgentLevelProgressionConfig",
        menuName = "SO/Agent/Progression/Level Progression Config")]
    public sealed class AgentLevelProgressionConfig : ScriptableObject
    {
        public const string DefaultResourcesPath = "Agent/Progression/SO_AgentLevelProgressionConfig";

        [Header("Experience Sources")]
        [SerializeField] private List<AgentExperienceSourceRule> _experienceSourceRules =
            new List<AgentExperienceSourceRule>
            {
                new AgentExperienceSourceRule(AgentExperienceSourceKind.LowDifficultyEnemy, 10, 100),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.MediumDifficultyEnemy, 15, 125),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.HighDifficultyEnemy, 25, 150),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.BossEnemy, 40, 120),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.LowTierLootBox, 5, 50),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.MediumTierLootBox, 10, 80),
                new AgentExperienceSourceRule(AgentExperienceSourceKind.HighTierLootBox, 15, 90)
            };

        [Header("Levels")]
        [SerializeField] private List<AgentLevelProgressionRule> _levelRules =
            new List<AgentLevelProgressionRule>
            {
                new AgentLevelProgressionRule(1, 10, 0),
                new AgentLevelProgressionRule(2, 15, 2),
                new AgentLevelProgressionRule(3, 25, 2),
                new AgentLevelProgressionRule(4, 40, 2),
                new AgentLevelProgressionRule(5, 60, 2),
                new AgentLevelProgressionRule(6, 80, 2),
                new AgentLevelProgressionRule(7, 100, 2),
                new AgentLevelProgressionRule(8, 125, 2),
                new AgentLevelProgressionRule(9, 150, 2),
                new AgentLevelProgressionRule(10, 0, 1)
            };

        public IReadOnlyList<AgentExperienceSourceRule> ExperienceSourceRules => _experienceSourceRules;
        public IReadOnlyList<AgentLevelProgressionRule> LevelRules => _levelRules;

        public int MaxLevel
        {
            get
            {
                int maxLevel = 1;
                for (int i = 0; i < _levelRules.Count; i++)
                {
                    if (_levelRules[i] != null)
                        maxLevel = Mathf.Max(maxLevel, _levelRules[i].Level);
                }

                return maxLevel;
            }
        }

        public int ResolveExperienceAmount(AgentExperienceSourceKind sourceKind)
        {
            AgentExperienceSourceRule rule = FindSourceRule(sourceKind);
            return rule != null ? rule.ExperienceAmount : 0;
        }

        public int ResolveExperienceCap(AgentExperienceSourceKind sourceKind)
        {
            AgentExperienceSourceRule rule = FindSourceRule(sourceKind);
            return rule != null ? rule.ExperienceCap : 0;
        }

        public int ResolveExperienceToNextLevel(int level)
        {
            AgentLevelProgressionRule rule = FindLevelRule(level);
            return rule != null ? rule.ExperienceToNextLevel : 0;
        }

        public int ResolveTalentPointsOnReachLevel(int level)
        {
            AgentLevelProgressionRule rule = FindLevelRule(level);
            return rule != null ? rule.TalentPointsGrantedOnReachLevel : 0;
        }

        private void OnValidate()
        {
            _experienceSourceRules ??= new List<AgentExperienceSourceRule>();
            _levelRules ??= new List<AgentLevelProgressionRule>();

            for (int i = 0; i < _experienceSourceRules.Count; i++)
                _experienceSourceRules[i]?.Validate();

            for (int i = 0; i < _levelRules.Count; i++)
                _levelRules[i]?.Validate();
        }

        private AgentExperienceSourceRule FindSourceRule(AgentExperienceSourceKind sourceKind)
        {
            for (int i = 0; i < _experienceSourceRules.Count; i++)
            {
                AgentExperienceSourceRule rule = _experienceSourceRules[i];
                if (rule != null && rule.SourceKind == sourceKind)
                    return rule;
            }

            return null;
        }

        private AgentLevelProgressionRule FindLevelRule(int level)
        {
            for (int i = 0; i < _levelRules.Count; i++)
            {
                AgentLevelProgressionRule rule = _levelRules[i];
                if (rule != null && rule.Level == level)
                    return rule;
            }

            return null;
        }
    }
}

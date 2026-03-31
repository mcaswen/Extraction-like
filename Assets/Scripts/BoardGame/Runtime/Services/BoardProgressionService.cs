using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime.State;
using UnityEngine;

namespace BoardGame.Runtime.Services
{
    /// <summary>
    /// 经验值、升级与增益选择服务
    /// </summary>
    public sealed class BoardProgressionService
    {
        private readonly Dictionary<string, int> _itemExperienceById =
            new Dictionary<string, int>();

        private readonly Dictionary<BoardDangerTier, int> _encounterExperienceByDangerTier =
            new Dictionary<BoardDangerTier, int>();

        private readonly SO_BoardGame_RuleSet _ruleSet;
        private bool IsEnabled => _ruleSet != null && _ruleSet.ProgressionRules.Enabled;

        public BoardProgressionService(SO_BoardGame_RuleSet ruleSet, SO_BoardGame_LootTableSet lootTableSet)
        {
            _ruleSet = ruleSet;

            foreach (BoardItemDefinition itemDefinition in lootTableSet.ItemDefinitions)
            {
                _itemExperienceById[itemDefinition.ItemId] = Mathf.Max(0, itemDefinition.ExperienceValue);
            }

            foreach (BoardEncounterExperienceDefinition encounterDefinition in ruleSet.ProgressionRules.EncounterExperienceDefinitions)
            {
                if (encounterDefinition.IsBoss)
                {
                    continue;
                }

                _encounterExperienceByDangerTier[encounterDefinition.DangerTier] = Mathf.Max(0, encounterDefinition.ExperienceValue);
            }
        }

        public BoardExperienceGrantResult GrantExperienceFromItems(
            BoardGameSessionState sessionState,
            IEnumerable<BoardItemInstance> generatedItems)
        {
            if (!IsEnabled)
            {
                return new BoardExperienceGrantResult(0);
            }

            int totalExperience = 0;

            if (generatedItems != null)
            {
                foreach (BoardItemInstance itemInstance in generatedItems)
                {
                    if (itemInstance == null)
                    {
                        continue;
                    }

                    if (_itemExperienceById.TryGetValue(itemInstance.ItemId, out int experienceValue))
                    {
                        totalExperience += Mathf.Max(0, experienceValue);
                    }
                }
            }

            return GrantExperience(sessionState, totalExperience);
        }

        public BoardExperienceGrantResult GrantExperienceFromEncounter(
            BoardGameSessionState sessionState,
            BoardNodeRuntimeState nodeState,
            bool isBoss)
        {
            if (!IsEnabled)
            {
                return new BoardExperienceGrantResult(0);
            }

            int experienceValue = 0;

            if (isBoss)
            {
                experienceValue = Mathf.Max(0, _ruleSet.ProgressionRules.BossExperienceValue);
            }
            else if (nodeState != null)
            {
                _encounterExperienceByDangerTier.TryGetValue(nodeState.DangerTier, out experienceValue);
            }

            return GrantExperience(sessionState, experienceValue);
        }

        public bool TryApplyLevelUpChoice(BoardGameSessionState sessionState, int choiceIndex, out string message)
        {
            message = string.Empty;

            if (!IsEnabled)
            {
                message = "Level-up progression is currently disabled";
                return false;
            }

            if (sessionState == null)
            {
                message = "No active session was found";
                return false;
            }

            if (!sessionState.IsAwaitingLevelUpChoice)
            {
                message = "No level-up choice is pending";
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= sessionState.PendingLevelUpChoices.Count)
            {
                message = "The selected level-up choice is invalid";
                return false;
            }

            BoardLevelUpChoice choice = sessionState.PendingLevelUpChoices[choiceIndex];
            ApplyChoiceToAgent(sessionState.AgentState, choice);
            sessionState.PendingLevelUpChoices.Clear();
            sessionState.PendingLevelUpCount = Mathf.Max(0, sessionState.PendingLevelUpCount - 1);

            if (sessionState.PendingLevelUpCount > 0 && TryRollPendingChoices(sessionState))
            {
                message = $"Applied {choice.DisplayLabel}. Choose another upgrade.";
                sessionState.StatusMessage = message;
                return true;
            }

            sessionState.IsAwaitingLevelUpChoice = false;
            message = $"Applied {choice.DisplayLabel}.";
            sessionState.StatusMessage = message;
            return true;
        }

        private BoardExperienceGrantResult GrantExperience(BoardGameSessionState sessionState, int experienceValue)
        {
            BoardExperienceGrantResult result = new BoardExperienceGrantResult(experienceValue);

            if (sessionState == null || experienceValue <= 0)
            {
                return result;
            }

            BoardAgentState agentState = sessionState.AgentState;
            agentState.CurrentExperience += experienceValue;

            while (agentState.CurrentExperience >= agentState.RequiredExperienceToNextLevel &&
                   agentState.RequiredExperienceToNextLevel > 0)
            {
                agentState.CurrentExperience -= agentState.RequiredExperienceToNextLevel;
                agentState.Level += 1;
                agentState.RequiredExperienceToNextLevel += _ruleSet.ProgressionRules.RequiredExperienceGrowthPerLevel;
                sessionState.PendingLevelUpCount += 1;
                result.LevelsGained += 1;
            }

            if (sessionState.PendingLevelUpCount > 0 && !sessionState.IsAwaitingLevelUpChoice)
            {
                result.TriggeredLevelUpChoice = TryRollPendingChoices(sessionState);
            }

            return result;
        }

        private bool TryRollPendingChoices(BoardGameSessionState sessionState)
        {
            sessionState.PendingLevelUpChoices.Clear();
            List<BoardLevelUpBuffDefinition> buffDefinitions = _ruleSet.ProgressionRules.BuffDefinitions;

            if (buffDefinitions == null || buffDefinitions.Count == 0)
            {
                sessionState.IsAwaitingLevelUpChoice = false;
                sessionState.PendingLevelUpCount = 0;
                sessionState.StatusMessage = "Level up was reached, but no upgrade choices are configured";
                return false;
            }

            List<BoardLevelUpBuffDefinition> remainingDefinitions = new List<BoardLevelUpBuffDefinition>(buffDefinitions);

            for (int index = 0; index < _ruleSet.ProgressionRules.ChoicesPerLevel; index++)
            {
                if (remainingDefinitions.Count == 0)
                {
                    remainingDefinitions.AddRange(buffDefinitions);
                }

                int pickedIndex = Random.Range(0, remainingDefinitions.Count);
                BoardLevelUpBuffDefinition pickedDefinition = remainingDefinitions[pickedIndex];
                remainingDefinitions.RemoveAt(pickedIndex);

                int rolledValue = Random.Range(
                    Mathf.Max(1, pickedDefinition.MinValue),
                    Mathf.Max(pickedDefinition.MinValue, pickedDefinition.MaxValue) + 1);

                sessionState.PendingLevelUpChoices.Add(new BoardLevelUpChoice(pickedDefinition.BuffType, rolledValue));
            }

            sessionState.IsAwaitingLevelUpChoice = sessionState.PendingLevelUpChoices.Count > 0;
            return sessionState.IsAwaitingLevelUpChoice;
        }

        private static void ApplyChoiceToAgent(BoardAgentState agentState, BoardLevelUpChoice choice)
        {
            switch (choice.BuffType)
            {
                case BoardLevelUpBuffType.AttackFlat:
                    agentState.Attack += choice.Value;
                    break;
                case BoardLevelUpBuffType.DefenseFlat:
                    agentState.Defense += choice.Value;
                    break;
                case BoardLevelUpBuffType.MaxHealthFlat:
                    agentState.MaxHealth += choice.Value;
                    agentState.CurrentHealth = Mathf.Min(agentState.MaxHealth, agentState.CurrentHealth + choice.Value);
                    break;
            }
        }
    }

    /// <summary>
    /// 单次经验结算结果
    /// </summary>
    public sealed class BoardExperienceGrantResult
    {
        public BoardExperienceGrantResult(int experienceGained)
        {
            ExperienceGained = Mathf.Max(0, experienceGained);
        }

        public int ExperienceGained { get; }
        public int LevelsGained { get; set; }
        public bool TriggeredLevelUpChoice { get; set; }

        public string Summary
        {
            get
            {
                if (ExperienceGained <= 0)
                {
                    return string.Empty;
                }

                string summary = $" Gained {ExperienceGained} XP";

                if (LevelsGained > 0)
                {
                    summary += LevelsGained == 1
                        ? " and leveled up"
                        : $" and leveled up {LevelsGained} times";
                }

                if (TriggeredLevelUpChoice)
                {
                    summary += ". Choose an upgrade";
                }

                return summary;
            }
        }
    }
}

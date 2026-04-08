using System.Collections.Generic;
using BoardGame.Config;
using BoardGame.Runtime;
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
        private readonly List<BoardLevelUpBuffType> _configuredChoiceTypes =
            new List<BoardLevelUpBuffType>();

        private readonly SO_BoardGame_RuleSet _ruleSet;
        private readonly bool _isFeatureEnabled;
        private bool IsEnabled => _ruleSet != null && _isFeatureEnabled;

        public BoardProgressionService(
            SO_BoardGame_RuleSet ruleSet,
            SO_BoardGame_LootTableSet lootTableSet,
            bool isFeatureEnabled)
        {
            _ruleSet = ruleSet;
            _isFeatureEnabled = isFeatureEnabled;

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

        public void SetConfiguredChoiceTypes(IReadOnlyList<BoardLevelUpBuffType> configuredChoiceTypes)
        {
            _configuredChoiceTypes.Clear();

            if (configuredChoiceTypes == null)
            {
                return;
            }

            foreach (BoardLevelUpBuffType configuredChoiceType in configuredChoiceTypes)
            {
                _configuredChoiceTypes.Add(configuredChoiceType);
            }
        }

        public BoardExperienceGrantResult GrantExperienceFromItems(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
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

            return GrantExperience(sessionState, agentState, totalExperience);
        }

        public BoardExperienceGrantResult GrantExperienceFromEncounter(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
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

            return GrantExperience(sessionState, agentState, experienceValue);
        }

        public bool TryApplyLevelUpChoice(BoardGameSessionState sessionState, int choiceIndex, out string message)
        {
            message = string.Empty;

            if (!IsEnabled)
            {
                message = BoardGameStatusMessageUtility.System("Level-up progression is currently disabled");
                return false;
            }

            if (sessionState == null)
            {
                message = BoardGameStatusMessageUtility.System("No active session was found");
                return false;
            }

            if (!sessionState.IsAwaitingLevelUpChoice)
            {
                message = BoardGameStatusMessageUtility.System("No level-up choice is pending");
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= sessionState.PendingLevelUpChoices.Count)
            {
                message = BoardGameStatusMessageUtility.System("The selected level-up choice is invalid");
                return false;
            }

            BoardAgentState activeAgentState = sessionState.GetActiveLevelUpAgentState();

            if (activeAgentState == null)
            {
                message = BoardGameStatusMessageUtility.System("No active level-up agent was found");
                return false;
            }

            BoardLevelUpChoice choice = sessionState.PendingLevelUpChoices[choiceIndex];
            ApplyChoiceToAgent(activeAgentState, choice);
            sessionState.PendingLevelUpChoices.Clear();
            activeAgentState.PendingLevelUpCount = Mathf.Max(0, activeAgentState.PendingLevelUpCount - 1);

            if (activeAgentState.PendingLevelUpCount > 0 && TryRollPendingChoices(sessionState, activeAgentState))
            {
                message = BoardGameStatusMessageUtility.Agent(
                    activeAgentState,
                    $"Applied {choice.DisplayLabel}. Choose another upgrade");
                sessionState.StatusMessage = message;
                return true;
            }

            sessionState.ActiveLevelUpAgentId = string.Empty;
            sessionState.PendingLevelUpChoices.Clear();

            BoardAgentState nextPendingAgentState = ResolvePendingLevelUpAgent(sessionState, null);

            if (nextPendingAgentState != null)
            {
                message = BoardGameStatusMessageUtility.Agent(
                    activeAgentState,
                    $"Applied {choice.DisplayLabel}. Another AI has a pending upgrade");
                sessionState.StatusMessage = message;
                return true;
            }

            message = BoardGameStatusMessageUtility.Agent(activeAgentState, $"Applied {choice.DisplayLabel}");
            sessionState.StatusMessage = message;
            return true;
        }

        public void SyncFocusedPendingChoices(BoardGameSessionState sessionState)
        {
            if (!IsEnabled || sessionState == null)
            {
                return;
            }

            if (sessionState.IsAwaitingLevelUpChoice)
            {
                return;
            }

            BoardAgentState focusedAgentState = sessionState.GetFocusedAgentState();

            if (focusedAgentState == null || focusedAgentState.PendingLevelUpCount <= 0)
            {
                return;
            }

            if (TryRollPendingChoices(sessionState, focusedAgentState))
            {
                sessionState.StatusMessage = BoardGameStatusMessageUtility.Agent(
                    focusedAgentState,
                    "Choose an upgrade");
            }
        }

        private BoardExperienceGrantResult GrantExperience(
            BoardGameSessionState sessionState,
            BoardAgentState agentState,
            int experienceValue)
        {
            BoardExperienceGrantResult result = new BoardExperienceGrantResult(experienceValue);

            if (sessionState == null || agentState == null || experienceValue <= 0)
            {
                return result;
            }

            agentState.CurrentExperience += experienceValue;

            while (agentState.CurrentExperience >= agentState.RequiredExperienceToNextLevel &&
                   agentState.RequiredExperienceToNextLevel > 0)
            {
                agentState.CurrentExperience -= agentState.RequiredExperienceToNextLevel;
                agentState.Level += 1;
                agentState.RequiredExperienceToNextLevel += _ruleSet.ProgressionRules.RequiredExperienceGrowthPerLevel;
                agentState.PendingLevelUpCount += 1;
                result.LevelsGained += 1;
            }

            if (agentState.PendingLevelUpCount > 0 &&
                !sessionState.IsAwaitingLevelUpChoice &&
                sessionState.GetFocusedAgentState()?.AgentId == agentState.AgentId)
            {
                result.TriggeredLevelUpChoice = TryRollPendingChoices(sessionState, agentState);
            }

            return result;
        }

        private bool TryRollPendingChoices(BoardGameSessionState sessionState, BoardAgentState preferredAgentState)
        {
            sessionState.PendingLevelUpChoices.Clear();
            List<BoardLevelUpBuffDefinition> buffDefinitions = _ruleSet.ProgressionRules.BuffDefinitions;
            BoardAgentState agentState = ResolvePendingLevelUpAgent(sessionState, preferredAgentState);

            if (agentState == null)
            {
                sessionState.ActiveLevelUpAgentId = string.Empty;
                return false;
            }

            if (buffDefinitions == null || buffDefinitions.Count == 0)
            {
                agentState.PendingLevelUpCount = 0;
                sessionState.ActiveLevelUpAgentId = string.Empty;
                sessionState.StatusMessage = BoardGameStatusMessageUtility.Agent(
                    agentState,
                    "Level up was reached, but no upgrade choices are configured");
                return false;
            }

            sessionState.ActiveLevelUpAgentId = agentState.AgentId;

            if (_configuredChoiceTypes.Count > 0)
            {
                foreach (BoardLevelUpBuffType configuredChoiceType in _configuredChoiceTypes)
                {
                    if (TryRollConfiguredChoice(configuredChoiceType, buffDefinitions, out BoardLevelUpChoice configuredChoice))
                    {
                        sessionState.PendingLevelUpChoices.Add(configuredChoice);
                    }
                }

                return sessionState.PendingLevelUpChoices.Count > 0;
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

            return sessionState.PendingLevelUpChoices.Count > 0;
        }

        private static bool TryRollConfiguredChoice(
            BoardLevelUpBuffType configuredChoiceType,
            IReadOnlyList<BoardLevelUpBuffDefinition> buffDefinitions,
            out BoardLevelUpChoice choice)
        {
            choice = null;

            if (buffDefinitions == null || buffDefinitions.Count == 0)
            {
                return false;
            }

            List<BoardLevelUpBuffDefinition> matchingDefinitions = new List<BoardLevelUpBuffDefinition>();

            foreach (BoardLevelUpBuffDefinition buffDefinition in buffDefinitions)
            {
                if (buffDefinition != null && buffDefinition.BuffType == configuredChoiceType)
                {
                    matchingDefinitions.Add(buffDefinition);
                }
            }

            if (matchingDefinitions.Count == 0)
            {
                return false;
            }

            BoardLevelUpBuffDefinition pickedDefinition = matchingDefinitions[Random.Range(0, matchingDefinitions.Count)];
            int rolledValue = Random.Range(
                Mathf.Max(1, pickedDefinition.MinValue),
                Mathf.Max(pickedDefinition.MinValue, pickedDefinition.MaxValue) + 1);
            choice = new BoardLevelUpChoice(pickedDefinition.BuffType, rolledValue);
            return true;
        }

        /// <summary>
        /// 优先激活本次刚升级的 Agent
        /// 若它没有待处理升级，则顺序寻找下一个待处理 Agent
        /// </summary>
        private static BoardAgentState ResolvePendingLevelUpAgent(
            BoardGameSessionState sessionState,
            BoardAgentState preferredAgentState)
        {
            if (preferredAgentState != null && preferredAgentState.PendingLevelUpCount > 0)
            {
                return preferredAgentState;
            }

            foreach (BoardAgentState agentState in sessionState.AgentStates)
            {
                if (agentState != null &&
                    agentState.IsAlive &&
                    !agentState.HasExtracted &&
                    agentState.PendingLevelUpCount > 0)
                {
                    return agentState;
                }
            }

            return null;
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

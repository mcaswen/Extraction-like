using System.Collections.Generic;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using BoardGame.Views;
using TMPro;
using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// Upgrades panel controller.
    /// Uses only scene-wired UI references and does not create runtime UI.
    /// </summary>
    public sealed class BoardGameLevelUpController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private TMP_Text _hintText;
        [SerializeField] private List<BoardGameLevelUpOptionView> _optionViews = new List<BoardGameLevelUpOptionView>();

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameProgressionController _progressionController;

        public void Bind(
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameProgressionController progressionController)
        {
            _runtimeQueryController = runtimeQueryController;
            _progressionController = progressionController;

            if (_runtimeQueryController == null)
            {
                return;
            }

            _runtimeQueryController.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            if (_runtimeQueryController == null || _progressionController == null)
            {
                return;
            }

            if (!_runtimeQueryController.IsProgressionEnabled || !HasConfiguredUi())
            {
                ClearOptions();
                SetVisible(false);
                return;
            }

            BoardGameSessionState sessionState = _runtimeQueryController.SessionState;
            BoardAgentState activeLevelUpAgentState = _runtimeQueryController.GetActiveLevelUpAgentState();
            bool isVisible = activeLevelUpAgentState != null &&
                             sessionState.IsAwaitingLevelUpChoice &&
                             sessionState.PendingLevelUpChoices.Count > 0;
            SetVisible(isVisible);

            if (!isVisible)
            {
                ClearOptions();
                return;
            }

            if (_hintText != null)
            {
                _hintText.text = "Press 1 / 2 / 3 or click an option";
            }

            for (int index = 0; index < _optionViews.Count; index++)
            {
                BoardGameLevelUpOptionView optionView = _optionViews[index];

                if (optionView == null)
                {
                    continue;
                }

                if (index >= sessionState.PendingLevelUpChoices.Count)
                {
                    optionView.Clear();
                    continue;
                }

                BoardLevelUpChoice choice = sessionState.PendingLevelUpChoices[index];
                int capturedIndex = index;
                BoardLevelUpOptionPresentation optionPresentation = BuildOptionPresentation(choice);
                optionView.Bind(
                    optionPresentation.Description,
                    () => _progressionController.TryApplyLevelUpChoice(capturedIndex));
            }
        }

        private void ClearOptions()
        {
            foreach (BoardGameLevelUpOptionView optionView in _optionViews)
            {
                optionView?.Clear();
            }
        }

        private void SetVisible(bool isVisible)
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(isVisible);
            }
        }

        private bool HasConfiguredUi()
        {
            return _panelRoot != null && _optionViews.Count > 0;
        }

        private static BoardLevelUpOptionPresentation BuildOptionPresentation(BoardLevelUpChoice choice)
        {
            if (choice.BuffType == BoardLevelUpBuffType.AttackFlat)
            {
                return new BoardLevelUpOptionPresentation($"Attack +{choice.Value}");
            }

            if (choice.BuffType == BoardLevelUpBuffType.DefenseFlat)
            {
                return new BoardLevelUpOptionPresentation($"Defense +{choice.Value}");
            }

            if (choice.BuffType == BoardLevelUpBuffType.MaxHealthFlat)
            {
                return new BoardLevelUpOptionPresentation($"Max HP +{choice.Value}");
            }

            return new BoardLevelUpOptionPresentation(choice.DisplayLabel);
        }
    }

    internal readonly struct BoardLevelUpOptionPresentation
    {
        public BoardLevelUpOptionPresentation(string description)
        {
            Description = description;
        }

        public string Description { get; }
    }
}

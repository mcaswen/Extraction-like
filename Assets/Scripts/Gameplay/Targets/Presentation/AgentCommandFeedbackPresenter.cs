using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Targets.Presentation
{
    public sealed class AgentCommandFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private Text _text;
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Min(0.01f)] private float _fadeIn = 0.15f;
        [SerializeField, Min(0.01f)] private float _hold = 1.5f;
        [SerializeField, Min(0.01f)] private float _fadeOut = 0.25f;
        private readonly Queue<AgentDirectiveResult> _pending = new Queue<AgentDirectiveResult>();
        private string _lastKey;
        private float _elapsed;
        private bool _showing;
        public string CurrentText => _text != null ? _text.text : string.Empty;
        public float Alpha => _group != null ? _group.alpha : 0f;
        private void OnEnable()
        {
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            AgentDirectiveFeedbackChannel.Published += OnResult;
        }
        private void OnDisable()
        {
            AgentDirectiveFeedbackChannel.Published -= OnResult;
            _pending.Clear(); _showing = false; _lastKey = null;
        }
        private void OnResult(AgentDirectiveResult result)
        {
            bool manual = AgentManualDirectiveLock.IsManualDirective(result.Request);
            bool missingRequest = string.IsNullOrEmpty(result.Request.CommandId);
            if ((!manual && !missingRequest) ||
                (result.Stage != AgentDirectiveStage.Accepted && result.Stage != AgentDirectiveStage.Rejected && result.Stage != AgentDirectiveStage.Failed)) return;
            string key = result.Request.CommandId + ":" + result.Stage + ":" + result.Reason;
            if (key == _lastKey) return;
            _lastKey = key;
            // Keep feedback bounded during rapid clicking, preserving the most recent outcome.
            if (_pending.Count >= 4) _pending.Dequeue();
            _pending.Enqueue(result);
        }
        private void Update()
        {
            if (!_showing && _pending.Count > 0)
            {
                var result = _pending.Dequeue();
                _text.text = AgentCommandFeedbackText.Format(result);
                _text.color = result.Accepted ? new Color(0.75f, 1f, 0.82f) : new Color(1f, 0.7f, 0.65f);
                _elapsed = 0f; _showing = true;
            }
            if (!_showing) return;
            _elapsed += Time.unscaledDeltaTime;
            _group.alpha = _elapsed < _fadeIn ? Mathf.Clamp01(_elapsed / _fadeIn) :
                _elapsed < _fadeIn + _hold ? 1f : 1f - Mathf.Clamp01((_elapsed - _fadeIn - _hold) / _fadeOut);
            if (_elapsed >= _fadeIn + _hold + _fadeOut) { _showing = false; _group.alpha = 0f; if (_pending.Count == 0) _lastKey = null; }
        }
    }
}

using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Targets.Presentation
{
    public sealed class AgentCommandFeedbackPresenter : MonoBehaviour
    {
        private readonly struct Message
        {
            public readonly string Text;
            public readonly bool Success;
            public Message(string text,bool success) { Text=text; Success=success; }
        }
        [SerializeField] private Text _text;
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Min(0.01f)] private float _fadeIn = 0.15f;
        [SerializeField, Min(0.01f)] private float _hold = 1.5f;
        [SerializeField, Min(0.01f)] private float _fadeOut = 0.25f;
        private readonly Queue<Message> _pending = new Queue<Message>();
        private readonly Dictionary<AgentId,AgentPawnRoot> _agents = new Dictionary<AgentId,AgentPawnRoot>();
        private AgentRuntimeRegistry _registry;
        private AgentCommandRouter _router;
        private string _lastKey;
        private float _elapsed;
        private bool _showing;
        public string CurrentText => _text != null ? _text.text : string.Empty;
        public float Alpha => _group != null ? _group.alpha : 0f;
        private void OnEnable()
        {
            _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
            AgentDirectiveFeedbackChannel.Published += OnResult;
            AttachRegistry(AgentRuntimeRegistry.GetOrCreate());
            AttachRouter(AgentCommandRouter.GetOrCreate());
        }
        private void OnDisable()
        {
            AgentDirectiveFeedbackChannel.Published -= OnResult;
            AttachRegistry(null); AttachRouter(null);
            _pending.Clear(); _showing = false; _lastKey = null;
            if (_group != null) _group.alpha=0;
        }
        private void AttachRegistry(AgentRuntimeRegistry registry)
        {
            if (_registry == registry) return;
            if (_registry != null) {
                _registry.AgentRegistered-=Subscribe;
                _registry.AgentUnregistered-=Unsubscribe;
            }
            foreach(var pawn in _agents.Values) if(pawn!=null) pawn.RouteResultPublished-=OnRouteResult;
            _agents.Clear(); _registry=registry;
            if (_registry == null) return;
            _registry.AgentRegistered+=Subscribe;
            _registry.AgentUnregistered+=Unsubscribe;
            foreach(var handle in _registry.RegisteredAgents) Subscribe(handle);
        }
        private void AttachRouter(AgentCommandRouter router)
        {
            if (_router == router) return;
            if (_router != null) _router.RouteRejectedBeforeDispatch-=OnRouteResult;
            _router=router;
            if (_router != null) _router.RouteRejectedBeforeDispatch+=OnRouteResult;
        }
        private void Subscribe(AgentRuntimeHandle handle)
        {
            if (handle.PawnRoot==null) return;
            if(_agents.TryGetValue(handle.AgentId,out var previous)) {
                if(previous==handle.PawnRoot) return;
                if(previous!=null) previous.RouteResultPublished-=OnRouteResult;
            }
            _agents[handle.AgentId]=handle.PawnRoot;
            handle.PawnRoot.RouteResultPublished+=OnRouteResult;
        }
        private void Unsubscribe(AgentRuntimeHandle handle)
        {
            if(!_agents.TryGetValue(handle.AgentId,out var pawn) || pawn!=handle.PawnRoot) return;
            if(pawn!=null) pawn.RouteResultPublished-=OnRouteResult;
            _agents.Remove(handle.AgentId);
        }
        private void OnRouteResult(AgentRouteResult result)
        {
            if(result.Request.Source!=AgentRouteSource.Player || result.IsReplan ||
                (result.Stage!=AgentRouteStage.Accepted && result.Stage!=AgentRouteStage.Rejected && result.Stage!=AgentRouteStage.Failed)) return;
            Enqueue("root:"+result.Request.TargetAgentId+":"+result.Request.RequestId+":"+result.Stage+":"+result.Reason,
                AgentCommandFeedbackText.Format(result),result.Accepted);
        }
        private void OnResult(AgentDirectiveResult result)
        {
            if(result.Request.RouteContext.IsValid) return;
            bool manual = AgentManualDirectiveLock.IsManualDirective(result.Request);
            bool missingRequest = string.IsNullOrEmpty(result.Request.CommandId);
            if ((!manual && !missingRequest) ||
                (result.Stage != AgentDirectiveStage.Accepted && result.Stage != AgentDirectiveStage.Rejected && result.Stage != AgentDirectiveStage.Failed)) return;
            Enqueue("directive:"+result.Request.TargetAgentId+":"+result.Request.CommandId+":"+result.Stage+":"+result.Reason,
                AgentCommandFeedbackText.Format(result),result.Accepted);
        }
        private void Enqueue(string key,string text,bool success)
        {
            if(key==_lastKey) return;
            _lastKey=key;
            if(_pending.Count>=4) _pending.Dequeue();
            _pending.Enqueue(new Message(text,success));
        }
        private void Update()
        {
            if(_registry!=AgentRuntimeRegistry.ActiveInstance) AttachRegistry(AgentRuntimeRegistry.ActiveInstance);
            if(_router!=AgentCommandRouter.ActiveInstance) AttachRouter(AgentCommandRouter.ActiveInstance);
            if (!_showing && _pending.Count > 0)
            {
                var message = _pending.Dequeue();
                _text.text = message.Text;
                _text.color = message.Success ? new Color(0.75f, 1f, 0.82f) : new Color(1f, 0.7f, 0.65f);
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

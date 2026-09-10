using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Targeting
{
    /// <summary>Short-lived failed execution memory; manual commands never consult this cache.</summary>
    public sealed class AgentTargetFailureMemory
    {
        private const float RetryDelaySeconds = 3f;
        private const float ChangedPositionSqr = 0.25f;
        private const int Capacity = 64;
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _listening;

        private sealed class Entry
        {
            public Transform Agent;
            public GameObject Target;
            public Vector3 AgentPosition;
            public Vector3 Destination;
            public float ExpiresAt;
        }

        public void StartObserving()
        {
            if (_listening) return;
            AgentDirectiveFeedbackChannel.Published += OnDirectiveResult;
            _listening = true;
        }

        public void StopObserving()
        {
            if (_listening) AgentDirectiveFeedbackChannel.Published -= OnDirectiveResult;
            _listening = false;
            _entries.Clear();
        }

        public bool IsDeferred(IAgentReadOnly agent, GameObject cluster, GameObject member, Vector3 destination)
        {
            Prune();
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                if (entry.Agent != agent.CachedTransform || (entry.Target != cluster && entry.Target != member)) continue;
                if ((entry.AgentPosition - agent.Position).sqrMagnitude > ChangedPositionSqr ||
                    (entry.Destination - destination).sqrMagnitude > ChangedPositionSqr)
                {
                    _entries.RemoveAt(i);
                    continue;
                }
                return true;
            }
            return false;
        }

        private void OnDirectiveResult(AgentDirectiveResult result)
        {
            if (result.Stage != AgentDirectiveStage.Failed || result.Request.TargetObject == null) return;
            if (result.Reason != AgentDirectiveFailure.NoProgress && result.Reason != AgentDirectiveFailure.Unreachable &&
                result.Reason != AgentDirectiveFailure.NavigationNotReady && result.Reason != AgentDirectiveFailure.LostSight &&
                result.Reason != AgentDirectiveFailure.AttackUnavailable) return;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry == null || !registry.Query.TryGetAgent(result.Request.TargetAgentId, out var handle)) return;
            IAgentReadOnly agent = handle.ReadOnly;
            if (agent == null || agent.IsDead || agent.CachedTransform == null) return;
            Vector3 destination = result.Request.TargetObject.transform.position;
            if (AgentDirectiveValidationService.TryResolveDestination(agent, result.Request, out Vector3 resolved)) destination = resolved;
            Prune();
            _entries.RemoveAll(entry => entry.Agent == agent.CachedTransform && entry.Target == result.Request.TargetObject);
            if (_entries.Count >= Capacity) _entries.RemoveAt(0);
            _entries.Add(new Entry { Agent = agent.CachedTransform, Target = result.Request.TargetObject,
                AgentPosition = agent.Position, Destination = destination, ExpiresAt = Time.time + RetryDelaySeconds });
        }

        private void Prune() => _entries.RemoveAll(entry => entry.Agent == null || entry.Target == null ||
            !entry.Agent.gameObject.activeInHierarchy || !entry.Target.activeInHierarchy || Time.time >= entry.ExpiresAt);
    }
}

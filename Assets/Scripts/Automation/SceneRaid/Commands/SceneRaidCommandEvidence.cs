#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>记录一次同步发令边界，之后仅按 CommandId 关联；不决定目标或整局验收。</summary>
    public sealed class SceneRaidCommandEvidence : IDisposable
    {
        [Serializable] public sealed class Feedback
        {
            public long eventSequence;
            public string attemptId, agent, commandId, targetId, target, stage, reason;
        }
        [Serializable] public sealed class Attempt
        {
            public int schemaVersion = 1, frame;
            public string scenarioId, stepId, attemptId, requestedAgent, route, cluster, commandId, resolvedAgent, directive, targetId, target, focusBefore, focusAfter;
            public bool accepted;
            public long outcomeSequence;
            public double gameSeconds, wallSeconds, submitMilliseconds;
            public SceneRaidReadModel.Snapshot before, after;
            public SceneRaidClusterCatalog.Snapshot selection;
            public Feedback[] synchronousFeedback;
        }
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly SceneRaidObserver _observer;
        private readonly SceneRaidReadModel _model;
        private readonly SceneRaidIdentityMap _identity;
        private readonly string _scenarioId;
        private readonly StreamWriter _attemptFile;
        private readonly Dictionary<string, Attempt> _byCommand = new Dictionary<string, Attempt>(StringComparer.Ordinal);
        private readonly Dictionary<string, Attempt> _byStep = new Dictionary<string, Attempt>(StringComparer.Ordinal);
        private readonly Dictionary<string, Feedback> _latest = new Dictionary<string, Feedback>(StringComparer.Ordinal);
        private readonly List<Feedback> _duringCall = new List<Feedback>();
        private readonly List<ProgressProbe> _probes = new List<ProgressProbe>();
        private sealed class ProgressProbe
        {
            public Attempt attempt;
            public Gameplay.Agent.Core.AgentPawnRoot pawn;
            public EnemyHealthController enemy;
            public AgentDirectiveRequest request;
            public Vector3 position;
            public int health = -1;
            public float enemyHealth = -1;
            public string active;
            public bool captured, ended;
        }
        [Serializable] public sealed class Progress
        {
            public string attemptId, commandId, agent, activeCommand, suspendedCommand, state, resource;
            public Vector3 position;
            public int health;
            public float enemyHealth;
            public bool actorGone, targetGone, visible;
        }
        private bool _inCall, _disposed;
        public int AttemptCount => _byStep.Count;

        public SceneRaidCommandEvidence(string output, string scenarioId, SceneRaidEvidenceWriter writer, SceneRaidObserver observer,
            SceneRaidReadModel model, SceneRaidIdentityMap identity)
        {
            _scenarioId = scenarioId; _writer = writer; _observer = observer; _model = model; _identity = identity;
            _attemptFile = new StreamWriter(Path.Combine(output, "command-attempts.jsonl"), false, new System.Text.UTF8Encoding(false));
            _observer.DirectiveObserved += Observe;
        }
        public Attempt FindStep(string id) => !string.IsNullOrEmpty(id) && _byStep.TryGetValue(id, out var attempt) ? attempt : null;
        public Feedback Latest(string commandId) => !string.IsNullOrEmpty(commandId) && _latest.TryGetValue(commandId, out var value) ? value : null;

        public Attempt Submit(SceneRaidCommandScenario.Step step, GameplayTargetClusterAuthoringBase cluster, SceneRaidClusterCatalog.Snapshot selection)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SceneRaidCommandEvidence));
            if (_inCall || _byStep.ContainsKey(step.id)) throw new InvalidOperationException("Duplicate or recursive command attempt.");
            var attempt = new Attempt { scenarioId = _scenarioId, stepId = step.id, attemptId = _scenarioId + ":" + step.id,
                requestedAgent = step.agent, route = step.route, frame = Time.frameCount, gameSeconds = Time.timeAsDouble,
                wallSeconds = _writer.WallSeconds, cluster = _identity.Get(cluster), selection = selection, before = _model.Capture(),
                focusBefore = AgentRuntimeRegistry.ActiveInstance?.FocusedAgentId.Value };
            _byStep.Add(step.id, attempt);
            _duringCall.Clear(); _inCall = true;
            AgentDirectiveRequest request;
            long start = Stopwatch.GetTimestamp();
            try
            {
                attempt.accepted = new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster,
                    step.route == "Focused" ? "" : step.agent, out request);
            }
            finally
            {
                attempt.submitMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                _inCall = false;
            }
            attempt.commandId = request.CommandId; attempt.resolvedAgent = request.TargetAgentId.Value;
            // 正式枚举含序列化兼容别名，ToString 不保证返回 Engage 等脚本语义名称。
            attempt.directive = request.DirectiveType == AgentDirectiveType.Search ? "Search" :
                request.DirectiveType == AgentDirectiveType.Engage ? "Engage" :
                request.DirectiveType == AgentDirectiveType.Extract ? "Extract" : "None";
            attempt.targetId = request.TargetId; attempt.target = _identity.Get(request.TargetObject);
            var outcomes = _duringCall.Where(x => (x.stage == "Accepted" || x.stage == "Rejected") &&
                (string.IsNullOrEmpty(request.CommandId) ? string.IsNullOrEmpty(x.commandId) : x.commandId == request.CommandId)).ToArray();
            if (outcomes.Length != 1) throw new InvalidOperationException("Command attempt has no unique synchronous outcome.");
            attempt.outcomeSequence = outcomes[0].eventSequence;
            if (!string.IsNullOrEmpty(request.CommandId)) _byCommand.Add(request.CommandId, attempt);
            foreach (var feedback in _duringCall)
            {
                if (feedback.eventSequence == attempt.outcomeSequence) feedback.attemptId = attempt.attemptId;
                Record(feedback);
            }
            attempt.synchronousFeedback = _duringCall.ToArray();
            attempt.after = _model.Capture();
            attempt.focusAfter = AgentRuntimeRegistry.ActiveInstance?.FocusedAgentId.Value;
            if (attempt.accepted && AgentRuntimeRegistry.ActiveInstance.TryGetHandle(request.TargetAgentId, out var handle))
                _probes.Add(new ProgressProbe { attempt = attempt, pawn = handle.PawnRoot, request = request,
                    enemy = request.DirectiveType == AgentDirectiveType.Engage && request.TargetObject != null
                        ? request.TargetObject.GetComponentInParent<EnemyHealthController>() : null });
            ObserveProgress();
            _attemptFile.WriteLine(JsonUtility.ToJson(attempt)); _attemptFile.Flush();
            _writer.Add("command.attempt", JsonUtility.ToJson(new AttemptLink { attemptId = attempt.attemptId, stepId = step.id,
                commandId = request.CommandId, outcomeSequence = attempt.outcomeSequence }));
            return attempt;
        }
        [Serializable] private sealed class AttemptLink { public string attemptId, stepId, commandId; public long outcomeSequence; }
        private void Observe(AgentDirectiveResult value, long sequence)
        {
            var request = value.Request;
            string target = _identity.Get(request.TargetObject);
            if (string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(request.CommandId) && _byCommand.TryGetValue(request.CommandId, out var attempt))
                target = attempt.target;
            var row = new Feedback { eventSequence = sequence, agent = request.TargetAgentId.Value, commandId = request.CommandId,
                targetId = request.TargetId, target = target, stage = value.Stage.ToString(), reason = value.Reason.ToString() };
            if (_inCall) _duringCall.Add(row);
            else if (!string.IsNullOrEmpty(request.CommandId) && _byCommand.ContainsKey(request.CommandId)) Record(row);
        }
        private void Record(Feedback row)
        {
            if (!string.IsNullOrEmpty(row.commandId) && _byCommand.TryGetValue(row.commandId, out var owner))
            { row.attemptId = owner.attemptId; _latest[row.commandId] = row; }
            _writer.Add("command.feedback", JsonUtility.ToJson(row));
            if (!_inCall) ObserveProgress();
        }
        public void ObserveProgress()
        {
            if (_disposed) return;
            foreach (var probe in _probes)
            {
                if (probe.ended) continue;
                var pawn = probe.pawn;
                bool gone = pawn == null;
                string active = gone ? "" : pawn.DirectiveLifecycle.Active?.CommandId;
                Vector3 position = gone ? probe.position : pawn.Position;
                int health = gone ? probe.health : pawn.CurrentHealth;
                // 纯托管生命字段在 Unity 销毁原生对象后仍可读取，避免漏掉同帧死亡的最后一次掉血。
                float enemyHealth = ReferenceEquals(probe.enemy, null) ? -1 : probe.enemy.GetCurrentHealthRatio() * probe.enemy.MaxHealth;
                string stage = Latest(probe.attempt.commandId)?.stage;
                bool ended = gone || stage == "Completed" || stage == "Cancelled" || stage == "Failed";
                if (!probe.captured || ended || (position - probe.position).sqrMagnitude >= 1 || probe.health != health ||
                    probe.enemyHealth != enemyHealth || probe.active != active)
                {
                    var resource = _observer.LatestResource(probe.attempt.resolvedAgent);
                    _writer.Add("command.progress", JsonUtility.ToJson(new Progress { attemptId = probe.attempt.attemptId,
                        commandId = probe.attempt.commandId, agent = probe.attempt.resolvedAgent, position = position,
                        health = health, enemyHealth = enemyHealth, actorGone = gone, targetGone = probe.request.TargetObject == null,
                        activeCommand = active, suspendedCommand = gone ? "" : pawn.DirectiveLifecycle.SuspendedExtraction?.CommandId,
                        state = gone ? "Gone" : pawn.CurrentMacroStateId.ToString(),
                        resource = resource.HasValue && resource.Value.CommandId == probe.request.CommandId ? _identity.Get(resource.Value.Resource) : "",
                        visible = !gone && active == probe.request.CommandId && pawn.Blackboard.GetValueOrDefault<bool>(AgentBlackboardKeys.HasVisibleEnemy) }));
                    probe.position = position; probe.health = health; probe.enemyHealth = enemyHealth; probe.active = active; probe.captured = true;
                }
                probe.ended = ended;
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _observer.DirectiveObserved -= Observe; _attemptFile.Dispose();
        }
    }
}
#endif

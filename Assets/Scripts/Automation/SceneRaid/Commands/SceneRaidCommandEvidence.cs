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
            public string scenarioId, stepId, attemptId, requestedAgent, route, cluster, commandId, resolvedAgent, directive, targetId, target;
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
                wallSeconds = _writer.WallSeconds, cluster = _identity.Get(cluster), selection = selection, before = _model.Capture() };
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
            attempt.directive = request.DirectiveType.ToString(); attempt.targetId = request.TargetId; attempt.target = _identity.Get(request.TargetObject);
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
            _attemptFile.WriteLine(JsonUtility.ToJson(attempt)); _attemptFile.Flush();
            _writer.Add("command.attempt", JsonUtility.ToJson(new AttemptLink { attemptId = attempt.attemptId, stepId = step.id,
                commandId = request.CommandId, outcomeSequence = attempt.outcomeSequence }));
            return attempt;
        }
        [Serializable] private sealed class AttemptLink { public string attemptId, stepId, commandId; public long outcomeSequence; }
        private void Observe(AgentDirectiveResult value, long sequence)
        {
            var request = value.Request;
            var row = new Feedback { eventSequence = sequence, agent = request.TargetAgentId.Value, commandId = request.CommandId,
                targetId = request.TargetId, target = _identity.Get(request.TargetObject), stage = value.Stage.ToString(), reason = value.Reason.ToString() };
            if (_inCall) _duringCall.Add(row);
            else if (!string.IsNullOrEmpty(request.CommandId) && _byCommand.ContainsKey(request.CommandId)) Record(row);
        }
        private void Record(Feedback row)
        {
            if (!string.IsNullOrEmpty(row.commandId) && _byCommand.TryGetValue(row.commandId, out var owner))
            { row.attemptId = owner.attemptId; _latest[row.commandId] = row; }
            _writer.Add("command.feedback", JsonUtility.ToJson(row));
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _observer.DirectiveObserved -= Observe; _attemptFile.Dispose();
        }
    }
}
#endif

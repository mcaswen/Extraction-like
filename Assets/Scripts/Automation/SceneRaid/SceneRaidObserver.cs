#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidObserver : IDisposable
    {
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly SceneRaidIdentityMap _identity;
        private readonly Dictionary<string, int> _logCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _commandTargets = new Dictionary<string, string>();
        private readonly Dictionary<string, AgentResourceInteractionEvent> _resourceFacts = new Dictionary<string, AgentResourceInteractionEvent>();
        [Serializable] private sealed class ResourceRecord
        { public string agent, commandId, resource, stage; public Vector3 navigationPosition; }
        [Serializable] private sealed class DirectiveRecord
        { public string agent, commandId, directive, targetId, target, stage, reason; }
        [Serializable] private sealed class DirectiveProbe
        { public string stage, reason, failureOrigin; public bool hasNavigation; public SceneRaidReadModel.AgentState agent; public SceneRaidNavigationEvidence.Record navigation; }
        public Func<AgentDirectiveRequest, SceneRaidReadModel.AgentState> CaptureDirective { get; set; }
        public Func<AgentDirectiveRequest, SceneRaidNavigationEvidence.Record> CaptureNavigation { get; set; }
        public event Action<AgentDirectiveResult, long> DirectiveObserved;
        public string ProbeFailure { get; private set; }
        public readonly HashSet<string> ObservedAgents = new HashSet<string>();
        public int Errors { get; private set; }
        public int Warnings { get; private set; }
        public SceneRaidObserver(SceneRaidEvidenceWriter writer, SceneRaidIdentityMap identity)
        {
            _writer = writer; _identity = identity;
            Application.logMessageReceived += Log;
            AgentDirectiveFeedbackChannel.Published += Directive;
            AgentResourceInteractionChannel.Published += Resource;
        }
        public AgentResourceInteractionEvent? LatestResource(string agentId) =>
            _resourceFacts.TryGetValue(agentId, out var fact) ? fact : (AgentResourceInteractionEvent?)null;
        private void Resource(AgentResourceInteractionEvent value)
        {
            if (value.Stage == AgentResourceInteractionStage.Left || value.Stage == AgentResourceInteractionStage.Completed)
                _resourceFacts.Remove(value.AgentId);
            else _resourceFacts[value.AgentId] = value;
            _writer.Add("resource." + value.Stage, JsonUtility.ToJson(new ResourceRecord
            {
                agent = value.AgentId, commandId = value.CommandId, resource = _identity.Get(value.Resource),
                stage = value.Stage.ToString(), navigationPosition = value.NavigationPosition
            }));
        }
        private void Log(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors++;
            if (type == LogType.Warning) Warnings++;
            string key = type + ":" + condition;
            _logCounts.TryGetValue(key, out int count);
            _logCounts[key] = count + 1;
            // 原始 Editor.log 保留全部；结构化流记录前 5 条和每 100 次的计数，避免重复刷盘。
            if (count < 5 || (count + 1) % 100 == 0)
                _writer.Add("log." + type, "count=" + (count + 1) + " " + condition + "\n" + stack);
        }
        private void Directive(AgentDirectiveResult result)
        {
            var request = result.Request;
            string key = request.TargetAgentId.Value + ":" + request.CommandId;
            string target = _identity.Get(request.TargetObject);
            if (!string.IsNullOrEmpty(target)) _commandTargets[key] = target;
            else if (_commandTargets.TryGetValue(key, out var saved)) target = saved;
            _writer.Add("directive." + result.Stage, JsonUtility.ToJson(new DirectiveRecord
            {
                agent = request.TargetAgentId.Value, commandId = request.CommandId, directive = request.DirectiveType.ToString(),
                targetId = request.TargetId, target = target, stage = result.Stage.ToString(), reason = result.Reason.ToString()
            }));
            try { DirectiveObserved?.Invoke(result, _writer.Count); }
            catch (Exception exception)
            {
                ProbeFailure = exception.ToString();
                _writer.Add("diagnostic.failed", ProbeFailure);
            }
            if (CaptureDirective != null && (result.Stage == AgentDirectiveStage.Failed || result.Stage == AgentDirectiveStage.Rejected ||
                (result.Stage == AgentDirectiveStage.Accepted && request.DirectiveType == AgentDirectiveType.Engage)))
            {
                try
                {
                    var navigation = result.Stage == AgentDirectiveStage.Failed || result.Stage == AgentDirectiveStage.Rejected
                        ? CaptureNavigation?.Invoke(request) : null;
                    _writer.Add("diagnostic.directive", JsonUtility.ToJson(new DirectiveProbe
                    { stage = result.Stage.ToString(), reason = result.Reason.ToString(), agent = CaptureDirective(request),
                        failureOrigin = result.Stage == AgentDirectiveStage.Failed || result.Stage == AgentDirectiveStage.Rejected
                            ? new System.Diagnostics.StackTrace(1, false).ToString() : "",
                        hasNavigation = navigation != null, navigation = navigation }));
                }
                catch (Exception exception)
                {
                    ProbeFailure = exception.ToString();
                    _writer.Add("diagnostic.failed", ProbeFailure);
                }
            }
            if (result.Stage == AgentDirectiveStage.Completed || result.Stage == AgentDirectiveStage.Cancelled ||
                result.Stage == AgentDirectiveStage.Failed || result.Stage == AgentDirectiveStage.Rejected) _commandTargets.Remove(key);
        }
        public void Snapshot(SceneRaidReadModel.Snapshot state)
        {
            foreach (var agent in state.agents) ObservedAgents.Add(agent.id);
            _writer.Add("snapshot", JsonUtility.ToJson(state));
        }
        public void Dispose()
        {
            Application.logMessageReceived -= Log;
            AgentDirectiveFeedbackChannel.Published -= Directive;
            AgentResourceInteractionChannel.Published -= Resource;
        }
    }
}
#endif

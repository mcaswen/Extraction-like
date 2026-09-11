#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using Gameplay.Agent.Commands;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidObserver : IDisposable
    {
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly SceneRaidIdentityMap _identity;
        private readonly Dictionary<string, int> _logCounts = new Dictionary<string, int>();
        public readonly HashSet<string> ObservedAgents = new HashSet<string>();
        public int Errors { get; private set; }
        public int Warnings { get; private set; }
        public SceneRaidObserver(SceneRaidEvidenceWriter writer, SceneRaidIdentityMap identity)
        {
            _writer = writer; _identity = identity;
            Application.logMessageReceived += Log;
            AgentDirectiveFeedbackChannel.Published += Directive;
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
            _writer.Add("directive." + result.Stage,
                "agent=" + request.TargetAgentId.Value + " command=" + request.CommandId +
                " type=" + request.DirectiveType + " targetId=" + request.TargetId +
                " target=" + _identity.Get(request.TargetObject) + " reason=" + result.Reason);
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
        }
    }
}
#endif

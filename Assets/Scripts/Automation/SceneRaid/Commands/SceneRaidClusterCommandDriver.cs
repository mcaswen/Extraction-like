#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    /// <summary>有限步骤编排，等待真实事实后只提交一次；库存和游戏状态仍由各自正式入口负责。</summary>
    public sealed class SceneRaidClusterCommandDriver
    {
        [Serializable] public sealed class StepResult
        {
            public string stepId, status, reason, agent, cluster, attemptId;
            public int frame;
            public double gameSeconds, wallSeconds;
        }
        [Serializable] public sealed class Result
        {
            public int schemaVersion = 1;
            public string scenarioId;
            public bool stopped;
            public List<StepResult> steps = new List<StepResult>();
        }
        private readonly SceneRaidCommandScenario _scenario;
        private readonly SceneRaidClusterCatalog _catalog;
        private readonly SceneRaidCommandEvidence _evidence;
        private readonly SceneRaidInventoryDriver _inventory;
        private readonly SceneRaidEvidenceWriter _writer;
        private readonly string _output;
        private readonly Dictionary<string, GameplayTargetClusterAuthoringBase> _targets = new Dictionary<string, GameplayTargetClusterAuthoringBase>();
        private int _index;
        private double _stepGameStart = -1, _stepWallStart;
        public Result Results { get; }
        public bool Stopped => Results.stopped;

        public SceneRaidClusterCommandDriver(string output, SceneRaidCommandScenario scenario, SceneRaidClusterCatalog catalog,
            SceneRaidCommandEvidence evidence, SceneRaidInventoryDriver inventory, SceneRaidEvidenceWriter writer, SceneRaidIdentityMap identity)
        {
            scenario.Validate(); _scenario = scenario; _catalog = catalog; _evidence = evidence; _inventory = inventory;
            _writer = writer; _output = output;
            Results = new Result { scenarioId = scenario.id };
        }

        // 返回 true 表示本帧已占用测试动作，调用方不要再让背包驱动改变焦点。
        public bool Tick()
        {
            if (Stopped) return false;
            if (_stepGameStart < 0) { _stepGameStart = Time.timeAsDouble; _stepWallStart = _writer.WallSeconds; }
            var step = _scenario.steps[_index];
            var registry = AgentRuntimeRegistry.ActiveInstance;
            string actor = step.agent == "Focused" ? registry?.FocusedAgentId.Value : step.agent;
            if (registry == null || !registry.TryGetHandle(actor, out var handle) || !handle.IsAlive)
            {
                if (_writer.WallSeconds - _stepWallStart >= 10) Advance(step, "COVERAGE_MISSING", "AgentUnavailable", actor, null);
                return false;
            }
            if (Time.timeAsDouble - _stepGameStart >= step.gameDeadline || _writer.WallSeconds - _stepWallStart >= step.wallDeadline)
            { Advance(step, "COVERAGE_MISSING", "PreconditionDeadline:" + step.gate.kind, actor, null); return false; }
            if (!AgentNavigationQuery.IsReady(handle.PawnRoot.NavMeshAgent) || !GateReady(step, actor, handle)) return false;
            if (!string.IsNullOrEmpty(step.focusAgent))
            {
                if (!registry.TrySetFocusedAgent(step.focusAgent)) throw new InvalidOperationException("Formal focus change failed.");
                _writer.Add("command.focus", JsonUtility.ToJson(new Focus { stepId = step.id, agent = step.focusAgent }));
            }
            if (step.agent == "Focused") actor = registry.FocusedAgentId.Value;
            var snapshot = _catalog.Capture();
            GameplayTargetClusterAuthoringBase target;
            if (!string.IsNullOrEmpty(step.target.sameAsStep)) _targets.TryGetValue(step.target.sameAsStep, out target);
            else
            {
                var excluded = _evidence.FindStep(step.target.excludeStep)?.cluster;
                var selected = SceneRaidClusterCatalog.Select(snapshot, step.target, actor, excluded);
                target = selected != null ? _catalog.Resolve(selected.identity) : null;
            }
            if (target == null) { Advance(step, "COVERAGE_MISSING", "NoCandidate", actor, null); return true; }
            if (step.accepted && (!target.isActiveAndEnabled || target.HasBeenCompleted))
            { Advance(step, "COVERAGE_MISSING", "TargetNoLongerAvailable", actor, null); return true; }
            _targets.Add(step.id, target);
            var attempt = _evidence.Submit(step, target, snapshot);
            Advance(step, "SUBMITTED", "AwaitIndependentActionAndTerminalValidation", actor, attempt);
            return true;
        }
        [Serializable] private sealed class Focus { public string stepId, agent; }

        private bool GateReady(SceneRaidCommandScenario.Step step, string actor, AgentRuntimeHandle handle)
        {
            var gate = step.gate;
            var previous = _evidence.FindStep(gate.referenceStep);
            var pawn = handle.PawnRoot;
            var active = pawn.DirectiveLifecycle.Active;
            switch (gate.kind)
            {
                case "Ready": return true;
                case "Retaliating": return active.HasValue && AgentManualDirectiveLock.IsCombatDamageDirective(active.Value) &&
                    pawn.DirectiveLifecycle.SuspendedExtraction.HasValue;
                case "InventoryOpen":
                    var screen = InventoryScreenController.Instance;
                    return screen != null && screen.IsInventoryOpen && screen.ActiveInventoryAgentId == actor &&
                        screen.ActiveSessionContext?.SourceObject != null && !screen.UsesCustomPlayerInventory;
                case "Moving":
                    if (previous == null || active?.CommandId != previous.commandId) return false;
                    var before = previous.before.agents.FirstOrDefault(x => x.id == actor);
                    if (before == null) return false;
                    Vector3 delta = pawn.Position - before.position; delta.y = 0;
                    return delta.sqrMagnitude > 1 && pawn.NavMeshAgent.hasPath;
                case "InventoryClosed": return previous != null && _inventory.HasCompletedSession(previous.commandId);
                case "CombatCompleted": return previous != null && previous.directive == "Engage" && _evidence.Latest(previous.commandId)?.stage == "Completed";
                case "Completed": return previous != null && _evidence.Latest(previous.commandId)?.stage == "Completed";
                case "Near":
                    if (previous == null || active?.CommandId != previous.commandId || active.Value.TargetObject == null) return false;
                    Vector3 distance = active.Value.TargetObject.transform.position - pawn.Position; distance.y = 0;
                    float range = pawn.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.InteractionDistance);
                    return SceneRaidClusterCatalog.ClassifyDistance(distance.magnitude, pawn.TargetDiscoveryRange,
                        Mathf.Max(AgentNavigationQuery.ArrivalTolerance, range)) == "Near";
                default: throw new InvalidOperationException("Unknown command gate.");
            }
        }

        private void Advance(SceneRaidCommandScenario.Step step, string status, string reason, string agent, SceneRaidCommandEvidence.Attempt attempt)
        {
            var row = new StepResult { stepId = step.id, status = status, reason = reason, agent = agent,
                cluster = attempt?.cluster, attemptId = attempt?.attemptId, frame = Time.frameCount,
                gameSeconds = Time.timeAsDouble, wallSeconds = _writer.WallSeconds };
            Results.steps.Add(row); _writer.Add("command.step", JsonUtility.ToJson(row));
            _index++; _stepGameStart = -1;
            if (_index == _scenario.steps.Length) Results.stopped = true;
            Save();
        }
        public void Stop(string reason)
        {
            while (_index < _scenario.steps.Length)
                Advance(_scenario.steps[_index], "COVERAGE_MISSING", reason, _scenario.steps[_index].agent, null);
            Results.stopped = true; Save();
        }
        private void Save() => File.WriteAllText(Path.Combine(_output, "command-steps.json"), JsonUtility.ToJson(Results, true));
    }
}
#endif

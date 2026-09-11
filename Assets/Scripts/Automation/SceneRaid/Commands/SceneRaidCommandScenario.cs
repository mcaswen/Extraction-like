#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;

namespace AnomalySearch.Automation.SceneRaid.Commands
{
    [Serializable]
    public sealed class SceneRaidCommandScenario
    {
        public int schemaVersion = 1;
        public string id;
        public Step[] steps;

        [Serializable] public sealed class Step
        {
            public string id, agent, route = "Explicit", focusAgent = "";
            public Selector target = new Selector();
            public Gate gate = new Gate();
            public bool accepted = true;
            public string reason = "None";
            public float gameDeadline = 180, wallDeadline = 120;
        }
        [Serializable] public sealed class Selector
        {
            public string kind, distance = "Any", sameAsStep = "", excludeStep = "";
            public bool singleton;
        }
        [Serializable] public sealed class Gate
        {
            public string kind = "Ready", referenceStep = "";
        }

        public void Validate()
        {
            if (schemaVersion != 1 || string.IsNullOrWhiteSpace(id) || steps == null || steps.Length == 0 || steps.Length > 32)
                throw new InvalidOperationException("Invalid command scenario header or step count.");
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.id) || known.Contains(step.id) ||
                    (step.agent != "1" && step.agent != "2") ||
                    (step.route != "Explicit" && step.route != "Focused") ||
                    (step.focusAgent != "" && step.focusAgent != "1" && step.focusAgent != "2") ||
                    step.target == null || step.gate == null ||
                    !In(step.target.kind, "Resource", "ActiveEnemy", "Extraction") ||
                    !In(step.target.distance, "Any", "Near", "Far") ||
                    !In(step.gate.kind, "Ready", "Moving", "InventoryClosed", "CombatCompleted", "InventoryOpen", "Retaliating", "Near", "Completed") ||
                    !FinitePositive(step.gameDeadline) || !FinitePositive(step.wallDeadline) || step.wallDeadline > 600 ||
                    (step.accepted ? step.reason != "None" : !In(step.reason, "TargetCompleted", "InvalidTarget", "Unreachable", "NoAgent", "AgentUnavailable")))
                    throw new InvalidOperationException("Invalid command step: " + step?.id);
                foreach (string reference in new[] { step.target.sameAsStep, step.target.excludeStep, step.gate.referenceStep })
                    if (!string.IsNullOrEmpty(reference) && !known.Contains(reference))
                        throw new InvalidOperationException("Command step references a missing or future step: " + reference);
                if (step.gate.kind != "Ready" && step.gate.kind != "Retaliating" && step.gate.kind != "InventoryOpen" &&
                    string.IsNullOrEmpty(step.gate.referenceStep))
                    throw new InvalidOperationException("Command gate requires an earlier step.");
                if (!string.IsNullOrEmpty(step.target.sameAsStep) && !string.IsNullOrEmpty(step.target.excludeStep))
                    throw new InvalidOperationException("Conflicting target references.");
                known.Add(step.id);
            }
        }
        private static bool FinitePositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
        private static bool In(string value, params string[] allowed) => Array.IndexOf(allowed, value) >= 0;
    }
}
#endif

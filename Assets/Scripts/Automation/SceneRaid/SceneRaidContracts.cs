#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System.Collections.Generic;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    // 软诊断不接管玩法。等待背包无路径时不进入移动停滞检查。
    public sealed class SceneRaidContracts
    {
        private sealed class Progress
        {
            public string command;
            public double since;
            public Vector3 anchor;
            public bool reported;
        }
        private readonly Dictionary<string, Progress> _progress = new Dictionary<string, Progress>();
        public void Observe(SceneRaidReadModel.Snapshot snapshot, SceneRaidEvidenceWriter writer)
        {
            foreach (var agent in snapshot.agents)
            {
                if (!_progress.TryGetValue(agent.id, out var progress))
                { progress = new Progress(); _progress.Add(agent.id, progress); }
                if (progress.command != agent.commandId || !agent.hasPath || agent.stopped ||
                    (agent.resource != null && agent.resource.phase == "WaitingForInventory") ||
                    snapshot.timeScale <= 0 || agent.health <= 0 || Vector3.Distance(agent.position, progress.anchor) >= 1f)
                {
                    progress.command = agent.commandId; progress.since = Time.timeAsDouble;
                    progress.anchor = agent.position; progress.reported = false;
                    continue;
                }
                if (!progress.reported && Time.timeAsDouble - progress.since >= 10)
                {
                    progress.reported = true;
                    writer.Add("contract.movementStagnationSuspected", JsonUtility.ToJson(agent));
                }
            }
        }
    }
}
#endif

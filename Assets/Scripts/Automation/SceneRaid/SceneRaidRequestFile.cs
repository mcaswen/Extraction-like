#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.IO;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    // 启动器只写请求，Editor 只写完成标记，避免旧轮收尾覆盖下一轮请求。
    public static class SceneRaidRequestFile
    {
        [Serializable] private sealed class Completion { public string runId; }

        public static string ReadShared(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().TrimStart('\uFEFF');
        }

        public static SceneRaidScenarioConfig ReadPending(string path)
        {
            var request = JsonUtility.FromJson<SceneRaidScenarioConfig>(ReadShared(path));
            if (request == null || string.IsNullOrEmpty(request.runId))
                throw new InvalidOperationException("SceneRaid request has no runId.");
            if (!request.enabled) return null;
            string completedPath = path + ".completed.json";
            if (File.Exists(completedPath))
            {
                var completed = JsonUtility.FromJson<Completion>(ReadShared(completedPath));
                if (completed == null || string.IsNullOrEmpty(completed.runId))
                    throw new InvalidOperationException("Invalid SceneRaid completion marker.");
                if (completed.runId == request.runId) return null;
            }
            return request;
        }

        public static void Complete(string path, string runId)
        {
            if (string.IsNullOrEmpty(runId)) throw new ArgumentException("Completion requires a runId.", nameof(runId));
            string completedPath = path + ".completed.json";
            File.WriteAllText(completedPath + ".tmp", JsonUtility.ToJson(new Completion { runId = runId }));
            if (File.Exists(completedPath)) File.Replace(completedPath + ".tmp", completedPath, null);
            else File.Move(completedPath + ".tmp", completedPath);
        }
    }
}
#endif

using System;
using System.IO;
using UnityEngine;

namespace AgentReproduction.Infrastructure
{
    [Serializable]
    public sealed class TestRunContext
    {
        public string outputPath;
        public string runId;
        public string group;
        public int repeat = 1;
        public int seed = 731;
        public bool graphics;
        public string faultProbe;

        public static TestRunContext Load()
        {
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".agent-repro-run.json");
            if (!File.Exists(path))
                throw new InvalidOperationException("Use tools/agent-repro/Invoke-AgentRepro.ps1 to run isolated tests.");
            return JsonUtility.FromJson<TestRunContext>(File.ReadAllText(path).TrimStart('\uFEFF'));
        }
    }
}

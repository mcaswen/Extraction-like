using System;
using System.Collections;
using System.IO;
using AgentReproduction.Infrastructure;
using AnomalySearch.Automation.SceneRaid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidRequestFileTests : ReproductionTestFixture
    {
        private static string Request(string runId, bool enabled = true)
        {
            string directory = Path.Combine(TestRunContext.Load().outputPath, "request-probes", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "request.json");
            File.WriteAllText(path, JsonUtility.ToJson(new SceneRaidScenarioConfig { runId = runId, enabled = enabled }));
            return path;
        }

        [UnityTest]
        public IEnumerator OccupiedRequestCanBeRetriedWithoutChangingItsContents()
        {
            string path = Request("next");
            string before = File.ReadAllText(path);
            using (var occupied = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => SceneRaidRequestFile.ReadPending(path));
            Assert.That(SceneRaidRequestFile.ReadPending(path).runId, Is.EqualTo("next"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviousCompletionCannotOverwriteNewRequest()
        {
            string path = Request("next");
            string before = File.ReadAllText(path);
            // 即使新请求暂时不可读，上一轮完成也只写独立标记。
            using (var occupied = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                SceneRaidRequestFile.Complete(path, "previous");
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(SceneRaidRequestFile.ReadPending(path).runId, Is.EqualTo("next"));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedRequestDoesNotRunAgainAfterReload()
        {
            string path = Request("completed");
            SceneRaidRequestFile.Complete(path, "completed");
            Assert.That(SceneRaidRequestFile.ReadPending(path), Is.Null);
            Assert.That(SceneRaidRequestFile.ReadPending(path), Is.Null);
            File.WriteAllText(path + ".next", JsonUtility.ToJson(new SceneRaidScenarioConfig { runId = "next" }));
            File.Replace(path + ".next", path, null);
            Assert.That(SceneRaidRequestFile.ReadPending(path).runId, Is.EqualTo("next"));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledRequestRemainsInactive()
        {
            string path = Request("disabled", false);
            Assert.That(SceneRaidRequestFile.ReadPending(path), Is.Null);
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidRequestOrCompletionNeverCountsAsConsumed()
        {
            string path = Request("");
            Assert.Throws<InvalidOperationException>(() => SceneRaidRequestFile.ReadPending(path));
            File.WriteAllText(path, JsonUtility.ToJson(new SceneRaidScenarioConfig { runId = "valid" }));
            File.WriteAllText(path + ".completed.json", "{}");
            Assert.Throws<InvalidOperationException>(() => SceneRaidRequestFile.ReadPending(path));
            ContractCompleted = true;
            yield return null;
        }
    }
}

using System.Collections;
using AgentReproduction.Infrastructure;
using AnomalySearch.Automation.SceneRaid;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidFrameSamplerTests : ReproductionTestFixture
    {
        public static int[] ShortBudgets = { 60, 120 };
        [UnityTest]
        public IEnumerator LongRunRetainsMoreThanLegacyCapacity()
        {
            using (var sampler = new SceneRaidFrameSampler())
            {
                // Deliberate same-frame calls test storage only, never claim rendered FPS.
                for (int i = 0; i < 160001; i++) sampler.Sample();
                Assert.That(sampler.Overflow, Is.False);
                Assert.That(sampler.Count, Is.EqualTo(160001));
            }
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiniteBudgetStillRejectsAnExtraFrame([ValueSource(nameof(ShortBudgets))] int seconds)
        {
            using (var sampler = new SceneRaidFrameSampler(seconds))
            {
                int expected = seconds == 60 ? 100000 : 120000;
                Assert.That(sampler.FrameCapacity, Is.EqualTo(expected));
                for (int i = 0; i < expected; i++) sampler.Sample();
                Assert.That(sampler.Overflow, Is.False);
                sampler.Sample();
                Assert.That(sampler.Overflow, Is.True);
                Assert.That(sampler.Count, Is.EqualTo(expected));
            }
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidDeadlineCannotStartSampling()
        {
            foreach (float seconds in new[] { 0f, -1f, 601f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                {
                    using var sampler = new SceneRaidFrameSampler(seconds);
                });
            ContractCompleted = true;
            yield return null;
        }
    }
}

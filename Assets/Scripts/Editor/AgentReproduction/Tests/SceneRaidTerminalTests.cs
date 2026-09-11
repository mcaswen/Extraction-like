using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidTerminalTests : ReproductionTestFixture
    {
        public static string[] Orders = { "DeathThenExtraction", "ExtractionThenDeath", "BothSameFrame", "BothSequential" };
        [UnityTest]
        public IEnumerator AllAgentsResolvedAlwaysProducesTerminalState([ValueSource(nameof(Orders))] string order)
        {
            TestNavMeshBuilder.Flat(World);
            var a = AgentFactory.Create(World, "A", Vector3.zero, 0, false, false);
            var b = AgentFactory.Create(World, "B", new Vector3(5, 0, 0), 0, false, false);
            var cluster = TargetFactory.Extraction(World, new Vector3(30, 0, 0));
            var point = cluster.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>();
            point.ExtractionDurationSeconds = 0.05f;
            var raid = World.Root("Terminal raid").AddComponent<RaidFlowController>();
            var model = new SceneRaidReadModel(new SceneRaidIdentityMap(), null);
            yield return null;
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, model.Capture().requiredAgents);
            if (order == "DeathThenExtraction")
            {
                a.TakeCombatDamage(1000000, a.Position, Vector3.forward, null);
                Assert.That(a.IsDead, Is.True);
                Assert.That(raid.IsInputLocked, Is.False, "The living teammate can still extract.");
                raid.SetAgentInsideExtractionPoint("B", point, true);
                yield return RuntimeWait.Until(() => b == null, "surviving teammate extraction", 3);
            }
            else
            {
                raid.SetAgentInsideExtractionPoint("A", point, true);
                if (order == "BothSameFrame") raid.SetAgentInsideExtractionPoint("B", point, true);
                yield return RuntimeWait.Until(() => a == null, "first extraction", 3);
                if (order == "ExtractionThenDeath") b.TakeCombatDamage(1000000, b.Position, Vector3.forward, null);
                else if (order == "BothSequential") raid.SetAgentInsideExtractionPoint("B", point, true);
                if (order != "ExtractionThenDeath") yield return RuntimeWait.Until(() => b == null, "second extraction", 3);
            }
            var state = model.Capture();
            bool bothSurvived = order.StartsWith("Both");
            Assert.That(raid.IsInputLocked, Is.True, "No living unextracted agent must not leave the raid running forever.");
            Assert.That(state.missionCompleted, Is.EqualTo(bothSurvived));
            Assert.That(state.missionFailed, Is.EqualTo(!bothSurvived));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(state.extractedAgents.Length, Is.EqualTo(bothSurvived ? 2 : 1));
            CollectionAssert.AreEquivalent(state.extractedAgents, state.settledAgents);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, state.requiredAgents);
            ContractCompleted = true;
        }
    }
}

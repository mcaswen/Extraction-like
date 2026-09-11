using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    [Explicit("P3h diagnostic: retaliation policy awaits confirmation; alternating sources reproduce the current failure.")]
    public sealed class SceneRaidRetaliationProgressTests : ReproductionTestFixture
    {
        public static bool[] Alternating = { false, true };
        [UnityTest]
        public IEnumerator RepeatedDamageAllowsRealRetaliationProgress([ValueSource(nameof(Alternating))] bool alternating)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Retaliation progress", Vector3.zero, 8, false, false);
            var first = EnemyFactory.Passive(World, new Vector3(14, 0, 0));
            var second = EnemyFactory.Passive(World, new Vector3(-14, 0, 0));
            var exit = TargetFactory.Extraction(World, new Vector3(0, 0, -30));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit, agent.AgentIdValue, out AgentDirectiveRequest extraction), Is.True);
            yield return null;
            float firstHealth = first.GetCurrentHealthRatio(), secondHealth = second.GetCurrentHealthRatio();
            float began = Time.time, nextDamage = Time.time, minimumX = agent.Position.x, maximumX = agent.Position.x;
            int hits = 0;
            var commands = new HashSet<string>();
            while (Time.time - began < 8 && first.GetCurrentHealthRatio() >= firstHealth && second.GetCurrentHealthRatio() >= secondHealth)
            {
                if (Time.time >= nextDamage)
                {
                    var attacker = alternating && hits % 2 == 1 ? second : first;
                    Assert.That(agent.TakeCombatDamage(10, agent.Position, attacker.transform.position - agent.Position, attacker.gameObject), Is.GreaterThan(0));
                    hits++; nextDamage += 0.25f;
                }
                if (agent.DirectiveLifecycle.Active.HasValue) commands.Add(agent.DirectiveLifecycle.Active.Value.CommandId);
                minimumX = Mathf.Min(minimumX, agent.Position.x); maximumX = Mathf.Max(maximumX, agent.Position.x);
                yield return null;
            }
            CaseArtifactWriter.Trace("retaliation-progress", JsonUtility.ToJson(new Evidence
            {
                alternating = alternating, damageEvents = hits, distinctCommands = commands.Count,
                minimumX = minimumX, maximumX = maximumX, elapsed = Time.time - began,
                firstHealthBefore = firstHealth, firstHealthAfter = first.GetCurrentHealthRatio(),
                secondHealthBefore = secondHealth, secondHealthAfter = second.GetCurrentHealthRatio()
            }));
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.Value.CommandId, Is.EqualTo(extraction.CommandId));
            Assert.That(first.GetCurrentHealthRatio() < firstHealth || second.GetCurrentHealthRatio() < secondHealth, Is.True,
                "Repeated effective damage must still allow pursuit to reach real projectile damage within the fixture window.");
            ContractCompleted = true;
        }
        [System.Serializable] private sealed class Evidence
        {
            public bool alternating;
            public int damageEvents, distinctCommands;
            public float minimumX, maximumX, elapsed, firstHealthBefore, firstHealthAfter, secondHealthBefore, secondHealthAfter;
        }
    }
}

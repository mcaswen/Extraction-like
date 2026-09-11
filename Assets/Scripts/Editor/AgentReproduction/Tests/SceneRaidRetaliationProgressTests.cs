using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
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
            Assert.That(commands.Count, Is.EqualTo(1), "Later attackers must not reset an active retaliation.");
            Assert.That(second.GetCurrentHealthRatio(), Is.EqualTo(secondHealth));
            Object.Destroy(first.gameObject);
            yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == extraction.CommandId, "resume original extraction");
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue, Is.False);
            Assert.That(second.IsAlive, Is.True, "Retaliation must not queue every previous damage source before resuming.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator InvalidTargetBeforeNextDamageResumesThenInterruptsOriginalExtraction([ValueSource(nameof(Alternating))] bool destroyed)
        {
            var agent = CreateStationary(out var extraction);
            var first = EnemyFactory.Passive(World, new Vector3(14, 0, 0));
            var second = EnemyFactory.Passive(World, new Vector3(-14, 0, 0));
            Hit(agent, first);
            string oldCombat = agent.DirectiveLifecycle.Active.Value.CommandId;
            var stages = new List<string>();
            System.Action<AgentDirectiveResult> observe = value =>
            {
                if (value.Request.CommandId == oldCombat || value.Request.CommandId == extraction.CommandId)
                    stages.Add(value.Request.CommandId + ":" + value.Stage);
            };
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                if (destroyed) Object.DestroyImmediate(first.gameObject); else first.gameObject.SetActive(false);
                Hit(agent, second); // Deliberately before the next lifecycle Tick.
                CollectionAssert.AreEqual(new[] { oldCombat + ":Completed", extraction.CommandId + ":Resumed", extraction.CommandId + ":Suspended" }, stages);
                Assert.That(agent.DirectiveLifecycle.Active.Value.TargetObject, Is.EqualTo(second.gameObject));
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.Value.CommandId, Is.EqualTo(extraction.CommandId));
                Assert.That(agent.FinishDirective(oldCombat), Is.False);
                second.gameObject.SetActive(false);
                yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == extraction.CommandId, "second retaliation resumes original extraction");
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator ManualEnemyCommandOverridesRetaliationAndDiscardsOldExtraction()
        {
            var agent = CreateStationary(out _);
            var first = EnemyFactory.Passive(World, new Vector3(14, 0, 0));
            var second = EnemyFactory.Passive(World, new Vector3(-14, 0, 0));
            Hit(agent, first); Hit(agent, second);
            string oldCombat = agent.DirectiveLifecycle.Active.Value.CommandId;
            var manual = AgentDirectiveRequest.EngageConcreteEnemy(second.gameObject, "second", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("second"), AgentManualDirectiveLock.ManualDirectivePriority);
            Assert.That(agent.TrySubmitDirective(manual).Accepted, Is.True);
            Assert.That(agent.DirectiveLifecycle.Active.Value.CommandId, Is.EqualTo(manual.CommandId));
            Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue, Is.False);
            Assert.That(agent.FinishDirective(oldCombat), Is.False);
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OtherDamageDoesNotRestartLostSightWindow()
        {
            var agent = CreateStationary(out var extraction);
            var first = EnemyFactory.Passive(World, new Vector3(14, 0, 0));
            var second = EnemyFactory.Passive(World, new Vector3(-14, 0, 0));
            World.Cube("Sight blocker", new Vector3(7, 5, 0), new Vector3(1, 20, 20));
            Physics.SyncTransforms();
            Hit(agent, first);
            string originalCombat = agent.DirectiveLifecycle.Active.Value.CommandId;
            AgentDirectiveResult? end = null;
            System.Action<AgentDirectiveResult> observe = value =>
            {
                if (value.Request.CommandId == originalCombat && value.Stage == AgentDirectiveStage.Failed) end = value;
            };
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                float start = Time.time, nextDamage = Time.time;
                yield return RuntimeWait.Until(() =>
                {
                    if (agent.DirectiveLifecycle.Active?.CommandId == extraction.CommandId) return true;
                    if (Time.time >= nextDamage) { Hit(agent, second); nextDamage += 0.25f; }
                    return false;
                }, "bounded retaliation despite repeated other damage", 8);
                Assert.That(end.HasValue, Is.True);
                Assert.That(end.Value.Reason, Is.EqualTo(AgentDirectiveFailure.LostSight));
                Assert.That(Time.time - start, Is.LessThan(agent.CombatLostSightTimeout + 1));
                Assert.That(first.GetCurrentHealthRatio(), Is.EqualTo(1));
                Assert.That(second.GetCurrentHealthRatio(), Is.EqualTo(1));
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue, Is.False);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        private AgentPawnRoot CreateStationary(out AgentDirectiveRequest extraction)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Retaliation boundary", Vector3.zero, 0, false, false);
            var exit = TargetFactory.Extraction(World, new Vector3(0, 0, -30));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit, agent.AgentIdValue, out extraction), Is.True);
            return agent;
        }
        private static void Hit(AgentPawnRoot agent, EnemyHealthController source) =>
            Assert.That(agent.TakeCombatDamage(10, agent.Position, source.transform.position - agent.Position, source.gameObject), Is.GreaterThan(0));
        [System.Serializable] private sealed class Evidence
        {
            public bool alternating;
            public int damageEvents, distinctCommands;
            public float minimumX, maximumX, elapsed, firstHealthBefore, firstHealthAfter, secondHealthBefore, secondHealthAfter;
        }
    }
}

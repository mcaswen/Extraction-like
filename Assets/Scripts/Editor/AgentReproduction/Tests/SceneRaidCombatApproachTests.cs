using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidCombatApproachTests : ReproductionTestFixture
    {
        public static string[] Layouts = { "Visible", "VisibleChild", "Damage", "Wall", "TooFar", "MuzzleOutOfRange" };
        [UnityTest]
        public IEnumerator DisconnectedEnemyRequiresReachableFiringPosition([ValueSource(nameof(Layouts))] string layout)
        {
            float enemyX = layout == "TooFar" ? 25 : 14;
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20)),
                new Bounds(new Vector3(enemyX, 0.9f, 0), new Vector3(4, 0.2f, 4)));
            var agent = AgentFactory.Create(World, "Approach", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(enemyX, 1, 0));
            if (layout == "Wall") World.Cube("Firing obstruction", new Vector3(11, 5, 0), new Vector3(0.4f, 12, 30));
            if (layout == "MuzzleOutOfRange")
            {
                var muzzle = World.Root("High muzzle").transform;
                muzzle.SetParent(agent.transform, false); muzzle.localPosition = Vector3.up * 20;
                RuntimeFixtureAccess.Configure(agent.GetComponent<AgentCombatShooter>(), "_firePoint", muzzle);
            }
            GameObject binding = enemy.gameObject;
            if (layout == "VisibleChild")
            {
                binding = World.Root("Bound child"); binding.transform.SetParent(enemy.transform, false);
            }
            yield return null; Physics.SyncTransforms();
            float initialHeight = agent.Position.y;
            var path = new NavMeshPath();
            Assert.That(agent.NavMeshAgent.CalculatePath(AgentCombatNavigationTarget.Resolve(enemy), path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathPartial), "Fixture must have disconnected navigation surfaces.");
            float initialHealth = enemy.GetCurrentHealthRatio();
            var request = AgentDirectiveRequest.EngageConcreteEnemy(binding, "island", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("island"), 1000);
            AgentDirectiveRequest extraction = default;
            if (layout == "Damage")
            {
                var exit = TargetFactory.Extraction(World, new Vector3(-5, 0, 0));
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit, agent.AgentIdValue, out extraction), Is.True);
                Assert.That(agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject), Is.GreaterThan(0));
                request = agent.DirectiveLifecycle.Active.Value;
            }
            var result = layout == "Damage" ? new AgentDirectiveResult(request, AgentDirectiveStage.Accepted) : agent.TrySubmitDirective(request);
            bool expected = layout.StartsWith("Visible") || layout == "Damage";
            Assert.That(result.Accepted, Is.EqualTo(expected), "Accept only a reachable position that can actually fire.");
            if (expected)
            {
                yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < initialHealth,
                    "move along our complete path and damage the disconnected target", 8);
                Assert.That(agent.Position.x, Is.GreaterThan(3));
                Assert.That(agent.Position.x, Is.LessThan(10), "Cannot walk across the navigation gap.");
                Assert.That(agent.Position.y, Is.EqualTo(initialHeight).Within(0.1f), "Keep the authored scale/baseOffset above our original floor.");
                if (layout == "Damage")
                {
                    Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.Value.CommandId, Is.EqualTo(extraction.CommandId));
                    Object.Destroy(enemy.gameObject);
                    yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active.HasValue &&
                        agent.DirectiveLifecycle.Active.Value.CommandId == extraction.CommandId, "resume original extraction after retaliation", 3);
                }
            }
            else Assert.That(result.Reason, Is.EqualTo(AgentDirectiveFailure.Unreachable));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ApproachQueriesAreReadOnlyAndBuffersDoNotCrossTargets()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20)),
                new Bounds(new Vector3(14, 0.9f, 0), new Vector3(4, 0.2f, 4)),
                new Bounds(new Vector3(25, 0.9f, 0), new Vector3(4, 0.2f, 4)));
            var agent = AgentFactory.Create(World, "Read only", Vector3.zero, 0, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(14, 1, 0));
            var far = EnemyFactory.Passive(World, new Vector3(25, 1, 0));
            yield return null; Physics.SyncTransforms();
            var shooter = agent.GetComponent<AgentCombatShooter>();
            shooter.CanShootAt(far, 8);
            var shotResult = shooter.LastShotResult; var shotFailure = shooter.LastShotFailure;
            Vector3 position = agent.Position; Quaternion rotation = agent.transform.rotation;
            var first = new AgentCombatApproachQuery.Buffer(); var second = new AgentCombatApproachQuery.Buffer();
            Assert.That(AgentCombatApproachQuery.TryResolve(agent, enemy, 8, first, out var destination), Is.True);
            long firstCount = first.CalculationCount;
            Assert.That(AgentCombatApproachQuery.TryResolve(agent, far, 8, second, out _), Is.False);
            Assert.That(first.CalculationCount, Is.EqualTo(firstCount));
            Assert.That(AgentCombatApproachQuery.TryResolve(agent, enemy, 8, first, out var again), Is.True);
            Assert.That(again, Is.EqualTo(destination));
            Assert.That(agent.Position, Is.EqualTo(position)); Assert.That(agent.transform.rotation, Is.EqualTo(rotation));
            Assert.That(agent.NavMeshAgent.hasPath, Is.False); Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
            Assert.That(shooter.LastShotResult, Is.EqualTo(shotResult)); Assert.That(shooter.LastShotFailure, Is.EqualTo(shotFailure));
            World.Cube("Dynamic obstruction", new Vector3(11, 5, 0), new Vector3(0.4f, 12, 30)); Physics.SyncTransforms();
            Assert.That(AgentCombatApproachQuery.TryResolve(agent, enemy, 8, first, out _), Is.False, "Previously reachable firing position must not bypass a new wall.");
            ContractCompleted = true;
        }
    }
}

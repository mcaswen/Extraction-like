using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Input;
using Gameplay.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Gameplay.Agent.Core;

namespace AgentReproduction.Tests
{
    public sealed class ElevatedCombatApproachTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static bool[] Damage = { false, true };
        public static string[] Obstructions = { "Wall", "MuzzleTooHigh", "TargetTooHigh" };

        [TearDown]
        public void StopActorsBeforeNavigationCleanup()
        {
            foreach (var nav in UnityEngine.Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator NavigationRebuildingStillAllowsBoundedRetaliation([ValueSource(nameof(Speeds))] int speed)
        { yield return NavigationRecovery(speed, false); }

        [UnityTest]
        public IEnumerator FixedNavigationWaitKeepsTheAcceptedRetaliation([ValueSource(nameof(Speeds))] int speed)
        { yield return NavigationRecovery(speed, true); }

        private IEnumerator NavigationRecovery(int speed, bool fixedWindow)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(16, 5, 0));
            yield return null;
            Time.timeScale = speed;
            agent.NavMeshAgent.enabled = false;
            agent.DirectiveLifecycle.ResultPublished += result =>
            {
                var nav = agent.NavMeshAgent;
                CaseArtifactWriter.Trace("navigation-rebuild-result", result.Stage + ":" + result.Reason +
                    "; game=" + Time.time + "; command=" + result.Request.CommandId + "; transform=" + agent.Position +
                    "; next=" + (nav.enabled ? nav.nextPosition.ToString() : "disabled") + "; offset=" + nav.baseOffset +
                    "; scale=" + agent.transform.lossyScale);
                if (result.Stage == AgentDirectiveStage.Failed && nav.enabled)
                {
                    Vector3 floor = nav.nextPosition - Vector3.up * nav.baseOffset * agent.transform.lossyScale.y;
                    Vector3 target = new Vector3(16, floor.y, 0);
                    var path = new NavMeshPath(); bool native = nav.CalculatePath(target, path);
                    CaseArtifactWriter.Trace("navigation-rebuild-query", "native=" + native + "; status=" + path.status + "; corners=" + path.corners.Length);
                    bool shared = NavMesh.CalculatePath(floor, target, new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask }, path);
                    var probe = new AgentCombatApproachQuery.Buffer();
                    bool approach = AgentCombatApproachQuery.TryResolve(agent, enemy, 8, probe, out var candidate);
                    CaseArtifactWriter.Trace("navigation-rebuild-query", "static=" + shared + "; status=" + path.status + "; approach=" + approach +
                        "; candidate=" + candidate + "; calculations=" + probe.CalculationCount);
                    var shooter = agent.GetComponent<AgentCombatShooter>();
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    Vector3 muzzle = (Vector3)typeof(AgentCombatShooter).GetMethod("ResolveFirePosition", flags).Invoke(shooter, null);
                    Vector3 aim = Gameplay.Perception.CombatAimPointResolver.Resolve(enemy.transform);
                    Vector3 bodyAim = Gameplay.Perception.CombatAimPointResolver.Resolve(agent.transform);
                    Vector3 delta = target - floor;
                    CaseArtifactWriter.Trace("navigation-rebuild-firing", "configured=" + shooter.IsConfigured + "; enemyAlive=" + enemy.IsAlive +
                        "; body=" + bodyAim + "; muzzle=" + muzzle + "; aim=" + aim +
                        "; predictedBody=" + Gameplay.Perception.TargetVisibilityQuery.Check(agent.transform, bodyAim + delta, enemy.transform, 8) +
                        "; predictedMuzzle=" + Gameplay.Perception.TargetVisibilityQuery.Check(agent.transform, muzzle + delta, enemy.transform, 8) +
                        "; radius=" + typeof(AgentCombatShooter).GetMethod("ResolveProjectileRadius", flags).Invoke(shooter, null));
                }
            };
            Assert.That(agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject), Is.GreaterThan(0));
            Assert.That(AgentManualDirectiveLock.IsCombatDamageDirective(agent.DirectiveLifecycle.Active.Value), Is.True);
            string command = agent.DirectiveLifecycle.Active.Value.CommandId;
            if (fixedWindow)
            {
                double restoreAt = Time.timeAsDouble + .2;
                yield return RuntimeWait.Until(() => Time.timeAsDouble >= restoreAt, "固定导航准备观察窗口", 3);
            }
            else yield return null;
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(command), "导航暂未准备好不能立即丢弃已接受反击");
            agent.NavMeshAgent.enabled = true;
            float before = enemy.GetCurrentHealthRatio();
            double nextProbe = 0;
            try
            {
                yield return RuntimeWait.Until(() =>
                {
                    if (Time.realtimeSinceStartupAsDouble >= nextProbe)
                    {
                        nextProbe = Time.realtimeSinceStartupAsDouble + .5;
                        var nav = agent.NavMeshAgent;
                        CaseArtifactWriter.Trace("navigation-rebuild-progress", "position=" + agent.Position + "; game=" + Time.time +
                            "; scale=" + Time.timeScale + "; command=" + agent.DirectiveLifecycle.Active?.CommandId +
                            "; health=" + enemy.GetCurrentHealthRatio() + "; destination=" + nav.destination +
                            "; velocity=" + nav.velocity + "; hasPath=" + nav.hasPath + "; pending=" + nav.pathPending +
                            "; shot=" + agent.GetComponent<AgentCombatShooter>().LastShotResult);
                    }
                    return enemy.GetCurrentHealthRatio() < before;
                }, "navigation recovery continues same retaliation", 8);
            }
            finally
            {
                var nav = agent.NavMeshAgent;
                CaseArtifactWriter.Trace("navigation-rebuild-terminal", "position=" + agent.Position + "; time=" + Time.time +
                    "; scale=" + Time.timeScale + "; command=" + agent.DirectiveLifecycle.Active?.CommandId +
                    "; enemyHealth=" + enemy.GetCurrentHealthRatio() + "; ready=" + nav.isOnNavMesh + "; destination=" + nav.destination);
            }
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(command));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RecordedSceneSentinelCanBeDamagedFromReachableGround()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            yield return null; yield return null; yield return null;
            var pawn = UnityEngine.Object.FindObjectsOfType<AgentPawnRoot>().Single(x => x.AgentIdValue == "1");
            Vector3 targetPosition = new Vector3(-210.81366f, 7.765434f, -76.50351f);
            var enemies = UnityEngine.Object.FindObjectsOfType<EnemyHealthController>(true);
            var enemy = enemies.Where(x => x.name.Contains("AnchorSentinel"))
                .OrderBy(x => (x.transform.position - targetPosition).sqrMagnitude).First();
            foreach (var other in enemies) if (other != enemy) other.gameObject.SetActive(false);
            foreach (var other in UnityEngine.Object.FindObjectsOfType<AgentPawnRoot>())
                if (other != pawn) other.gameObject.SetActive(false);
            enemy.gameObject.SetActive(true);
            enemy.GetComponent<AnchorSentinelBehaviorController>().enabled = false;
            enemy.transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(0, 283.1047f, 0));
            pawn.enabled = false;
            Vector3 start = new Vector3(-226.62259f, 3.0317955f, -72.82329f);
            var nav = pawn.NavMeshAgent;
            Assert.That(nav.Warp(start - Vector3.up * nav.baseOffset * pawn.transform.lossyScale.y), Is.True);
            nav.nextPosition = start;
            Physics.SyncTransforms();
            Time.timeScale = 4;
            var buffer = new AgentCombatApproachQuery.Buffer();
            var motor = new AgentNavigationMotor(nav, 2, 3);
            var shooter = pawn.GetComponent<AgentCombatShooter>();
            float before = enemy.GetCurrentHealthRatio();
            bool fired = false;
            double deadline = Time.timeAsDouble + 25;
            while (Time.timeAsDouble < deadline && enemy.GetCurrentHealthRatio() >= before)
            {
                if (TargetVisibilityQuery.Check(pawn.transform, CombatAimPointResolver.Resolve(pawn.transform), enemy.transform, 8) == TargetVisibilityResult.Visible &&
                    shooter.CanShootAt(enemy, 8))
                {
                    motor.Stop();
                    if (!fired) fired = shooter.TryShootAt(enemy, 25);
                }
                else
                {
                    bool resolved = AgentCombatApproachQuery.TryResolve(pawn, enemy, 8, buffer, out var destination);
                    CaseArtifactWriter.Trace("recorded-sentinel", "position=" + pawn.Position + "; resolved=" + resolved + "; destination=" + destination);
                    Assert.That(resolved, Is.True, "Recorded sentinel needs a reachable, unobstructed firing position.");
                    Assert.That(motor.Move("recorded-sentinel", destination, 0.1f, 8).Failed, Is.False);
                }
                yield return null;
            }
            Assert.That(fired, Is.True);
            Assert.That(enemy.GetCurrentHealthRatio(), Is.LessThan(before), "A real projectile must damage the recorded scene target.");
            CaseArtifactWriter.Trace("recorded-sentinel-shot", "position=" + pawn.Position + "; health=" + enemy.GetCurrentHealthRatio());
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator UnmappedElevatedEnemyCanBeApproachedAndShot(
            [ValueSource(nameof(Damage))] bool damage, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(16, 5, 0));
            var cluster = TargetFactory.Enemies(World, enemy);
            var exit = TargetFactory.Extraction(World, new Vector3(-20, 0, 0));
            yield return null;
            Physics.SyncTransforms();
            Time.timeScale = speed;
            Assert.That(AgentNavigationQuery.Check(agent.NavMeshAgent, AgentCombatNavigationTarget.Resolve(enemy), 0).Failed, Is.True,
                "The enemy itself must have no sampled navigation destination.");
            float before = enemy.GetCurrentHealthRatio();
            var results = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = results.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                var dispatcher = new AgentTargetCommandDispatcher();
                string extraction = null;
                if (damage)
                {
                    Assert.That(dispatcher.TrySubmitClusterCommand(exit, "1", out var request), Is.True);
                    extraction = request.CommandId;
                    Assert.That(agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject), Is.GreaterThan(0));
                    Assert.That(AgentManualDirectiveLock.IsCombatDamageDirective(agent.DirectiveLifecycle.Active.Value), Is.True);
                }
                else Assert.That(dispatcher.TrySubmitClusterCommand(cluster, "1", out _), Is.True);
                yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < before ||
                    results.Any(x => x.Stage == AgentDirectiveStage.Failed), "elevated enemy actual projectile damage", 8);
                Assert.That(results.Any(x => x.Stage == AgentDirectiveStage.Failed), Is.False);
                Assert.That(enemy.GetCurrentHealthRatio(), Is.LessThan(before));
                Assert.That(agent.Position.x, Is.GreaterThan(3));
                if (damage)
                {
                    Assert.That(agent.DirectiveLifecycle.SuspendedExtraction?.CommandId, Is.EqualTo(extraction));
                    UnityEngine.Object.Destroy(enemy.gameObject);
                    yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == extraction, "elevated retaliation resume", 3);
                }
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator ImpossibleAttackerKeepsExtractionThenBecomesEligible([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(16, 30, 0));
            var exit = TargetFactory.Extraction(World, new Vector3(-20, 0, 0));
            yield return null;
            Time.timeScale = speed;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit, "1", out var extraction), Is.True);
            var results = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = results.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject), Is.GreaterThan(0));
                    Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(extraction.CommandId));
                    yield return null;
                }
                Assert.That(results, Is.Empty, "An impossible automatic candidate must never replace the valid task or create formal failure spam.");
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction.HasValue, Is.False);
                yield return RuntimeWait.Until(() => agent.Position.x < -1, "unchanged extraction progress", 3);
                enemy.transform.position = new Vector3(16, 5, 0);
                Physics.SyncTransforms();
                float before = enemy.GetCurrentHealthRatio();
                Assert.That(agent.TakeCombatDamage(10, agent.Position, Vector3.left, enemy.gameObject), Is.GreaterThan(0));
                Assert.That(AgentManualDirectiveLock.IsCombatDamageDirective(agent.DirectiveLifecycle.Active.Value), Is.True);
                yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < before, "newly feasible attacker is not blacklisted", 8);
                Assert.That(agent.DirectiveLifecycle.SuspendedExtraction?.CommandId, Is.EqualTo(extraction.CommandId));
                UnityEngine.Object.Destroy(enemy.gameObject);
                yield return RuntimeWait.Until(() => agent.DirectiveLifecycle.Active?.CommandId == extraction.CommandId, "resume unchanged extraction", 3);
                Assert.That(results.Any(x => x.Stage == AgentDirectiveStage.Failed || x.Stage == AgentDirectiveStage.Rejected), Is.False);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator ProjectedCandidatesRespectOcclusionAndRange([ValueSource(nameof(Obstructions))] string obstruction)
        {
            TestNavMeshBuilder.Flat(World, 20);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(16, obstruction == "TargetTooHigh" ? 30 : 3, 0));
            if (obstruction == "Wall") World.Cube("Firing obstruction", new Vector3(11, 8, 0), new Vector3(0.4f, 20, 40));
            var shooter = agent.GetComponent<AgentCombatShooter>();
            if (obstruction == "MuzzleTooHigh")
            {
                var muzzle = World.Root("Elevated muzzle").transform;
                muzzle.SetParent(agent.transform, false); muzzle.localPosition = Vector3.up * 30;
                RuntimeFixtureAccess.Configure(shooter, "_firePoint", muzzle);
            }
            yield return null;
            Physics.SyncTransforms();
            var position = agent.Position;
            var rotation = agent.transform.rotation;
            var priorResult = shooter.LastShotResult;
            Assert.That(AgentCombatApproachQuery.TryResolve(agent, enemy, 8, new AgentCombatApproachQuery.Buffer(), out _), Is.False);
            Assert.That(agent.Position, Is.EqualTo(position));
            Assert.That(agent.transform.rotation, Is.EqualTo(rotation));
            Assert.That(agent.NavMeshAgent.hasPath, Is.False);
            Assert.That(shooter.LastShotResult, Is.EqualTo(priorResult));
            ContractCompleted = true;
        }
    }
}

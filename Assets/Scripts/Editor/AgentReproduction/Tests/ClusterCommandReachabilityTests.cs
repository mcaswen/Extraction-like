using System;
using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.SO;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ClusterCommandReachabilityTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static bool[] Availability = { true, false };
        public static string[] ExitKinds = { "Disconnected", "Disabled", "Completed" };

        [UnityTest]
        public IEnumerator DistantManualEnemyReachesFirstObservationAndDealsDamage([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 200);
            var agent = AgentFactory.Create(World, "Far", Vector3.zero, 8, false, false);
            var config = RuntimeFixtureAccess.Read<AgentPawnConfig>(agent, "_pawnConfig");
            RuntimeFixtureAccess.Configure(config, "_targetDiscoveryRange", 20f);
            var enemy = EnemyFactory.Passive(World, new Vector3(80, 0, 0));
            var cluster = TargetFactory.Enemies(World, enemy);
            yield return null;
            Assert.That(agent.Blackboard.GetValueOrDefault<float>(AgentBlackboardKeys.AttackRange), Is.LessThan(20));
            Time.timeScale = speed;
            var stages = new List<string>();
            string command = null;
            Action<AgentDirectiveResult> observe = r => { if (r.Request.CommandId == command) stages.Add(r.Stage + ":" + r.Reason); };
            AgentDirectiveFeedbackChannel.Published += observe;
            float initial = enemy.GetCurrentHealthRatio(), started = Time.time;
            try
            {
                Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent.AgentIdValue, out var request), Is.True);
                command = request.CommandId;
                yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < initial || !agent.DirectiveLifecycle.Active.HasValue,
                    "distant manual approach", 25);
                CaseArtifactWriter.Trace("far-command", "speed=" + speed + "; elapsed=" + (Time.time - started) +
                    "; position=" + agent.Position + "; stages=" + string.Join(",", stages));
                Assert.That(enemy.GetCurrentHealthRatio(), Is.LessThan(initial), "Initial approach must not expire before first observation.");
                Assert.That(stages.Exists(x => x.StartsWith("Failed:")), Is.False);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator EnemyClusterScansPastUnreachableNearestMember([ValueSource(nameof(Availability))] bool hasReachable)
        {
            BuildDisconnectedLayout();
            var agent = AgentFactory.Create(World, "Members", Vector3.zero);
            var nearest = EnemyFactory.Passive(World, new Vector3(20, 0, 0));
            var farther = EnemyFactory.Passive(World, new Vector3(-30, 0, 0));
            var cluster = hasReachable ? TargetFactory.Enemies(World, nearest, farther) : TargetFactory.Enemies(World, nearest);
            yield return null;
            bool accepted = new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent.AgentIdValue, out var request);
            Assert.That(accepted, Is.EqualTo(hasReachable));
            if (hasReachable) Assert.That(request.TargetObject, Is.SameAs(farther.gameObject));
            else Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ExtractionClusterSkipsNonExecutableNearestPoint([ValueSource(nameof(ExitKinds))] string kind)
        {
            BuildDisconnectedLayout();
            var agent = AgentFactory.Create(World, "Exits", Vector3.zero);
            var cluster = TargetFactory.Extraction(World, kind == "Disconnected" ? new Vector3(20, 0, 0) : new Vector3(-4, 0, 0));
            var fartherCluster = TargetFactory.Extraction(World, new Vector3(-30, 0, 0));
            var near = cluster.ExtractionMembers[0]; var far = fartherCluster.ExtractionMembers[0];
            RuntimeFixtureAccess.Configure(cluster, "_extractionMembers", new List<GameplayTargetEntityMember> { near, far });
            if (kind == "Disabled") near.EntityObject.GetComponent<ExtractionPointController>().enabled = false;
            if (kind == "Completed") near.MarkCompleted();
            yield return null;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent.AgentIdValue, out var request), Is.True);
            Assert.That(request.TargetObject, Is.SameAs(far.EntityObject));
            ContractCompleted = true;
        }

        private void BuildDisconnectedLayout()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(-20, -0.1f, 0), new Vector3(50, 0.2f, 30)),
                new Bounds(new Vector3(20, -0.1f, 0), new Vector3(8, 0.2f, 8)));
            World.Cube("Near island line of sight wall", new Vector3(10, 5, 0), new Vector3(2, 10, 20));
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator ObservedManualEnemyStillUsesLostSightTimeout([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 200);
            var agent = AgentFactory.Create(World, "Seen", Vector3.zero, 8, false, false);
            RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(agent, "_pawnConfig"), "_targetDiscoveryRange", 20f);
            var enemy = EnemyFactory.Passive(World, new Vector3(12, 0, 0));
            var cluster = TargetFactory.Enemies(World, enemy);
            yield return null;
            Time.timeScale = speed;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent.AgentIdValue, out var request), Is.True);
            yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < 1, "actual observation and shot", 10);
            AgentDirectiveResult? failed = null;
            Action<AgentDirectiveResult> observe = r => { if (r.Request.CommandId == request.CommandId && r.Stage == AgentDirectiveStage.Failed) failed = r; };
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                // 仅构造中的外部目标位移，整局脚本没有 Transform 写入。
                enemy.transform.position = new Vector3(80, 0, 0); Physics.SyncTransforms();
                float lostAt = Time.time;
                yield return RuntimeWait.Until(() => failed.HasValue, "observed enemy lost sight", 6);
                Assert.That(failed.Value.Reason, Is.EqualTo(AgentDirectiveFailure.LostSight));
                Assert.That(Time.time - lostAt, Is.InRange(agent.CombatLostSightTimeout - 0.05f, agent.CombatLostSightTimeout + 0.5f));
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }

        [UnityTest]
        public IEnumerator MovingUnobservedEnemyCannotRenewInitialApproachForever()
        {
            TestNavMeshBuilder.Flat(World, 1000);
            var agent = AgentFactory.Create(World, "Bounded", Vector3.zero, 8, false, false);
            RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(agent, "_pawnConfig"), "_targetDiscoveryRange", 20f);
            var enemy = EnemyFactory.Passive(World, new Vector3(80, 0, 0));
            var cluster = TargetFactory.Enemies(World, enemy);
            yield return null;
            Time.timeScale = 4;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(cluster, agent.AgentIdValue, out var request), Is.True);
            AgentDirectiveResult? failed = null;
            Action<AgentDirectiveResult> observe = r => { if (r.Request.CommandId == request.CommandId && r.Stage == AgentDirectiveStage.Failed) failed = r; };
            AgentDirectiveFeedbackChannel.Published += observe;
            float began = Time.time;
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            try
            {
                while (!failed.HasValue)
                {
                    Assert.That(DateTime.UtcNow, Is.LessThan(deadline));
                    enemy.transform.position = new Vector3(agent.Position.x + 80, 0, 0);
                    yield return null;
                }
                CaseArtifactWriter.Trace("bounded-initial-approach", "seconds=" + (Time.time - began) + "; position=" + agent.Position);
                Assert.That(failed.Value.Reason, Is.EqualTo(AgentDirectiveFailure.LostSight));
                Assert.That(Time.time - began, Is.InRange(18f, 25f));
                Assert.That(agent.Position.x, Is.GreaterThan(80), "The timeout must remain finite despite continuous progress.");
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }
    }
}

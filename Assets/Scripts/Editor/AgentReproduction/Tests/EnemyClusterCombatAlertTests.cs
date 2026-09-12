using System;
using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class EnemyClusterCombatAlertTests : ReproductionTestFixture
    {
        public static string[] MobileKinds = { "Base", "Ranged", "ModernStrander", "AncientStrander", "TidalAberration", "HunterBoss" };
        public static int[] Speeds = { 1, 4 };
        public static int[] CostMembers = { 8, 32 };
        public static string[] Boundaries = { "Shield", "Lethal", "NoAttacker", "Zero", "Disabled", "Dead", "RuntimeMember", "AlreadyFighting" };
        private static string State(Component enemy) => RuntimeFixtureAccess.Read<object>(enemy, "CurrentState").ToString();
        private static void Probe(Component enemy, Transform target)
        {
            var nav = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            string movement = nav == null ? "none" : $"ready={nav.isOnNavMesh}; stopped={nav.isStopped}; speed={nav.speed}; velocity={nav.velocity}; pending={nav.pathPending}; status={nav.pathStatus}; destination={nav.destination}; remaining={nav.remainingDistance}";
            CaseArtifactWriter.Trace("ally-probe", $"type={enemy.GetType().Name}; state={State(enemy)}; position={enemy.transform.position}; target={target.position}; distance={Vector3.Distance(enemy.transform.position, target.position)}; {movement}");
        }
        private static EnemyDamageContext Hit(Component attacker, EnemyHealthController victim) => EnemyDamageContext.FromAttacker(
            attacker.transform, victim.transform.position, attacker.transform.position,
            victim.transform.position - attacker.transform.position, EnemyDamageSourceType.Projectile);

        [UnityTest]
        public IEnumerator SameClusterMobileMemberJoinsAndMoves(
            [ValueSource(nameof(MobileKinds))] string kind, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Attacker", Vector3.zero, 0, false, false);
            var victim = EnemyFactory.Passive(World, new Vector3(15, 0, -8));
            var ally = EnemyFactory.Formal(World, kind, new Vector3(35, 0, 4));
            var outsider = EnemyFactory.Formal(World, "Base", new Vector3(35, 0, -10));
            TargetFactory.Enemies(World, victim, ally.GetComponent<EnemyHealthController>(), ally.GetComponent<EnemyHealthController>());
            TargetFactory.Enemies(World, outsider.GetComponent<EnemyHealthController>());
            yield return null;
            Time.timeScale = speed;
            Assert.That(State(ally), Is.EqualTo(kind == "HunterBoss" ? "Idle" : "Patrol"));
            var health = ally.GetComponent<EnemyHealthController>();
            float healthBefore = health.GetCurrentHealthRatio();
            float before = Vector3.Distance(ally.transform.position, agent.Position);
            victim.TakeDamage(1, Hit(agent, victim));
            Assert.That(State(ally), Is.EqualTo("Chase"), "An idle member must respond even outside its own discovery range.");
            Assert.That(RuntimeFixtureAccess.Read<Transform>(ally, "PlayerTransform"), Is.SameAs(agent.transform));
            Assert.That(RuntimeFixtureAccess.Read<ICombatDamageReceiver>(ally, "_combatDamageReceiver").DamageRootTransform, Is.SameAs(agent.transform));
            Assert.That(State(outsider), Is.EqualTo("Patrol"), "No other cluster may be alerted.");
            Assert.That(health.GetCurrentHealthRatio(), Is.EqualTo(healthBefore), "A group alert is not damage to the ally.");
            string timer = kind == "HunterBoss" ? "_groupAlertChaseEndTime" : "_directDamageForcedChaseEndTime";
            float firstWindow = RuntimeFixtureAccess.Read<float>(ally, timer);
            float nextProbe = 0;
            yield return RuntimeWait.Until(() =>
            {
                if (Time.realtimeSinceStartup >= nextProbe) { Probe(ally, agent.transform); nextProbe = Time.realtimeSinceStartup + 0.5f; }
                return Vector3.Distance(ally.transform.position, agent.Position) < before - 0.5f;
            }, "ally actual pursuit", 5);
            victim.TakeDamage(1, Hit(agent, victim));
            Assert.That(RuntimeFixtureAccess.Read<float>(ally, timer), Is.EqualTo(firstWindow), "Repeated allied hits must not restart chase or an attack.");
            CaseArtifactWriter.Trace("group-pursuit", $"kind={kind}; speed={speed}; position={ally.transform.position}; distanceBefore={before}; distanceNow={Vector3.Distance(ally.transform.position, agent.Position)}; state={State(ally)}");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator GroupAlertHonorsMembershipDamageAndExistingCombat([ValueSource(nameof(Boundaries))] string boundary)
        {
            TestNavMeshBuilder.Flat(World, 160);
            var agent = AgentFactory.Create(World, "Attacker", Vector3.zero, 0, false, false);
            var victim = EnemyFactory.Passive(World, new Vector3(15, 0, -8));
            var ally = EnemyFactory.Formal(World, "Base", new Vector3(35, 0, 4));
            var health = ally.GetComponent<EnemyHealthController>();
            var cluster = TargetFactory.Enemies(World, victim);
            if (boundary == "RuntimeMember") cluster.RegisterSpawnedEnemy(health, "Source");
            else cluster.RegisterSceneEnemy(health);
            yield return null;
            if (boundary == "Shield") victim.AddShield(100);
            if (boundary == "Disabled") ((Behaviour)ally).enabled = false;
            if (boundary == "Dead") health.TakeDamage(100000);
            Transform existing = null;
            if (boundary == "AlreadyFighting")
            {
                var other = AgentFactory.Create(World, "Other", new Vector3(30, 0, 10), 0, false, false);
                health.TakeDamage(1, Hit(other, health));
                existing = other.transform;
                Assert.That(State(ally), Is.EqualTo("Chase"));
            }
            victim.TakeDamage(boundary == "Zero" ? 0 : boundary == "Lethal" ? 100000 : 1,
                boundary == "NoAttacker" ? EnemyDamageContext.Empty : Hit(agent, victim));
            if (boundary == "NoAttacker" || boundary == "Zero" || boundary == "Disabled")
                Assert.That(State(ally), Is.EqualTo("Patrol"));
            else if (boundary == "Dead") Assert.That(health.IsAlive, Is.False);
            else
            {
                Assert.That(State(ally), Is.EqualTo("Chase"));
                Assert.That(RuntimeFixtureAccess.Read<Transform>(ally, "PlayerTransform"), Is.SameAs(existing != null ? existing : agent.transform));
            }
            if (boundary == "Shield") Assert.That(victim.GetCurrentHealthRatio(), Is.EqualTo(1));
            if (boundary == "Lethal") Assert.That(victim.IsAlive, Is.False);
            CaseArtifactWriter.Trace("group-boundary", boundary);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator AlertedRangedMemberCannotDamageThroughWallThenAttacksWhenClear()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Attacker", Vector3.zero, 0, false, false);
            var victim = EnemyFactory.Passive(World, new Vector3(-10, 0, 0));
            var ally = EnemyFactory.Formal(World, "Ranged", new Vector3(0, 0, 8));
            var wall = World.Cube("Sight blocker", new Vector3(0, 3, 5), new Vector3(20, 8, 1));
            TargetFactory.Enemies(World, victim, ally.GetComponent<EnemyHealthController>());
            yield return null;
            // 先让正式 NavMeshAgent 应用 Prefab 的 baseOffset，再固定位置做墙体对照。
            var navigation = ally.GetComponent<UnityEngine.AI.NavMeshAgent>();
            navigation.updatePosition = false;
            navigation.updateRotation = false;
            Assert.That(((RangedEnemyBehaviorController)ally).FirePoint.position.y, Is.GreaterThan(0.5f));
            Time.timeScale = 4;
            Physics.SyncTransforms();
            Assert.That(State(ally), Is.EqualTo("Patrol"));
            victim.TakeDamage(1, Hit(agent, victim));
            Assert.That(State(ally), Is.EqualTo("Chase"));
            float health = agent.CurrentHealth;
            double until = Time.timeAsDouble + 1;
            while (Time.timeAsDouble < until) yield return null;
            Assert.That(agent.CurrentHealth, Is.EqualTo(health), "Sharing awareness must not bypass walls.");
            UnityEngine.Object.Destroy(wall);
            float nextProbe = 0;
            yield return RuntimeWait.Until(() =>
            {
                if (Time.realtimeSinceStartup >= nextProbe) { Probe(ally, agent.transform); nextProbe = Time.realtimeSinceStartup + 0.5f; }
                return agent.CurrentHealth < health;
            }, "ally actual ranged hit after line of sight clears", 8);
            CaseArtifactWriter.Trace("group-actual-hit", $"healthBefore={health}; healthNow={agent.CurrentHealth}; allyPosition={ally.transform.position}");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator SentinelBindsAlertAttackerWithoutMovingOrIgnoringRange()
        {
            TestNavMeshBuilder.Flat(World, 160);
            var decoy = AgentFactory.Create(World, "Near", new Vector3(0, 0, 20), 0, false, false);
            var attacker = AgentFactory.Create(World, "Far", new Vector3(35, 0, 0), 0, false, false);
            var victim = EnemyFactory.Passive(World, new Vector3(15, 0, -8));
            var sentinel = EnemyFactory.Formal(World, "AnchorSentinel", Vector3.zero);
            TargetFactory.Enemies(World, victim, sentinel.GetComponent<EnemyHealthController>());
            yield return null;
            Assert.That(State(sentinel), Is.EqualTo("Dormant"));
            Assert.That(RuntimeFixtureAccess.Read<Transform>(sentinel, "PlayerTransform"), Is.SameAs(decoy.transform));
            Vector3 position = sentinel.transform.position;
            float health = attacker.CurrentHealth;
            victim.TakeDamage(1, Hit(attacker, victim));
            Assert.That(RuntimeFixtureAccess.Read<Transform>(sentinel, "PlayerTransform"), Is.SameAs(attacker.transform));
            Assert.That(RuntimeFixtureAccess.Read<ICombatDamageReceiver>(sentinel, "_combatDamageReceiver").DamageRootTransform, Is.SameAs(attacker.transform));
            Time.timeScale = 4;
            double until = Time.timeAsDouble + 2;
            while (Time.timeAsDouble < until) yield return null;
            Assert.That(sentinel.transform.position, Is.EqualTo(position));
            Assert.That(attacker.CurrentHealth, Is.EqualTo(health));
            Assert.That(State(sentinel), Is.EqualTo("Dormant"));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RepeatedGroupHitsHaveBoundedCost([ValueSource(nameof(CostMembers))] int members)
        {
            TestNavMeshBuilder.Flat(World, 240);
            var attacker = AgentFactory.Create(World, "Cost", Vector3.zero, 0, false, false);
            var victim = EnemyFactory.Passive(World, new Vector3(20, 0, -10));
            var cluster = TargetFactory.Enemies(World, victim);
            for (int i = 0; i < members; i++)
                cluster.RegisterSceneEnemy(EnemyFactory.Formal(World, "Base", new Vector3(30 + i % 8 * 3, 0, 10 + i / 8 * 3))
                    .GetComponent<EnemyHealthController>());
            yield return null;
            var context = Hit(attacker, victim);
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            victim.TakeDamage(1, context);
            double coldMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            for (int i = 0; i < 8; i++) victim.TakeDamage(1, context);
            using var allocations = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal,
                "GC.Alloc", 4096, Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < 100; i++) victim.TakeDamage(1, context);
            double meanMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency / 100;
            allocations.Stop();
            Assert.That(allocations.Valid, Is.True);
            Assert.That(allocations.Count, Is.LessThan(4096), "A saturated recorder is incomplete evidence.");
            CaseArtifactWriter.Trace("group-cost", $"members={members}; firstHitMs={coldMs:F6}; repeatedMeanMs={meanMs:F6}; hits=100; gcAllocationEvents={allocations.Count}");
            Assert.That(meanMs, Is.LessThan(1), "A repeated group-hit event must cost less than 1ms on this test machine.");
            Assert.That(allocations.Count, Is.Zero, "Warm repeated hits must reuse member and receiver lists.");
            ContractCompleted = true;
        }
    }
}

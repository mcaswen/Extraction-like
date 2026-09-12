using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class EnemyPhysicsOwnershipTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };

        [UnityTest]
        public IEnumerator BossPhysicsBodyStaysOnItsDrivenRoot([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World, 160);
            // 场景导航可以来自已烘焙几何；不能依赖刚体重力把脚本驱动的身体压在地面上。
            foreach (var collider in UnityEngine.Object.FindObjectsOfType<Collider>())
                if (collider.name == "Test ground") collider.enabled = false;
            var attacker = AgentFactory.Create(World, "Attacker", new Vector3(35, 0, 0), 0, false, false);
            var boss = EnemyFactory.Formal(World, "HunterBoss", Vector3.zero);
            var victim = EnemyFactory.Passive(World, new Vector3(20, 0, -8));
            TargetFactory.Enemies(World, victim, boss.GetComponent<EnemyHealthController>());
            yield return null;
            Time.timeScale = speed;
            victim.TakeDamage(1, EnemyDamageContext.FromAttacker(attacker.transform, victim.transform.position,
                attacker.Position, Vector3.left, EnemyDamageSourceType.Projectile));
            Vector3 start = boss.transform.position;
            var body = boss.GetComponent<Collider>();
            var rigidbody = boss.GetComponent<Rigidbody>();
            double until = Time.timeAsDouble + 6;
            float greatestSeparation = 0;
            while (Time.timeAsDouble < until)
            {
                yield return null;
                greatestSeparation = Mathf.Max(greatestSeparation, Vector3.Distance(body.bounds.center, boss.transform.position));
            }
            CaseArtifactWriter.Trace("boss-physics", $"speed={speed}; kinematic={rigidbody.isKinematic}; gravity={rigidbody.useGravity}; start={start}; root={boss.transform.position}; body={body.bounds.center}; rigidbodyPosition={rigidbody.position}; maxSeparation={greatestSeparation}");
            Assert.That(Vector3.Distance(start, boss.transform.position), Is.GreaterThan(1), "The real alert must drive pursuit.");
            Assert.That(greatestSeparation, Is.LessThan(0.5f), "A moving root cannot leave its damage collider behind or below it.");
            Assert.That(rigidbody.isKinematic, Is.True, "The authored body must have one movement owner.");
            Assert.That(rigidbody.useGravity, Is.False);
            ContractCompleted = true;
        }
    }
}

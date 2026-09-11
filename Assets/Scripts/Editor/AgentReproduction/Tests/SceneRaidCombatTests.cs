using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidCombatTests : ReproductionTestFixture
    {
        public static bool[] Platforms = { false, true };
        [UnityTest]
        public IEnumerator RaisedRootUsesFeetForPursuitButCannotCrossDisconnectedFloor([ValueSource(nameof(Platforms))] bool disconnected)
        {
            if (disconnected)
                TestNavMeshBuilder.Build(World, new Bounds(new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12)),
                    new Bounds(new Vector3(15, 5.9f, 0), new Vector3(8, 0.2f, 8)));
            else TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Pursuit", Vector3.zero, 8, false, false);
            var enemy = EnemyFactory.Passive(World, new Vector3(15, disconnected ? 9 : 3, 0));
            var body = enemy.GetComponent<CapsuleCollider>(); body.center = Vector3.zero; body.height = 6;
            yield return null; Physics.SyncTransforms();
            var result = agent.TrySubmitDirective(new AgentDirectiveRequest(AgentDirectiveType.Engage,
                AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, enemy.gameObject, "raised-root"), "raised-root", agent.AgentId,
                AgentManualDirectiveLock.CreateCommandId("raised-root"), 1000));
            if (disconnected)
            {
                Assert.That(result.Accepted, Is.False);
                Assert.That(result.Reason, Is.EqualTo(AgentDirectiveFailure.Unreachable));
            }
            else
            {
                Assert.That(result.Accepted, Is.True, "A raised body root is not a raised floor.");
                float health = enemy.GetCurrentHealthRatio();
                yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < health, "real pursuit and projectile damage", 8);
                Assert.That(agent.Position.x, Is.GreaterThan(3));
            }
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CorrosionSplashStartsWithoutChangingDurationWhilePlaying()
        {
            TestNavMeshBuilder.Flat(World);
            var vfx = World.Root("Corrosion visual").AddComponent<ZombieTentacleCorrosionVfx>();
            RuntimeFixtureAccess.Configure(vfx, "_autoAttackNearbyTarget", false);
            vfx.PlaySlimePoolVisual(Vector3.zero, 2, 3);
            yield return null;
            var particles = Object.FindObjectsOfType<ParticleSystem>();
            var splash = System.Array.Find(particles, x => x.name == "SlimeSplashParticles");
            Assert.That(splash, Is.Not.Null);
            Assert.That(splash.isPlaying, Is.True);
            Assert.That(splash.main.duration, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
            ContractCompleted = true;
        }
    }
}

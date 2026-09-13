using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Data;
using Gameplay.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ProjectileClearanceTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };

        [UnityTest]
        public IEnumerator RayClearButPhysicalBulletBlockedIsNotAFiringPosition()
        {
            TestNavMeshBuilder.Flat(World);
            var pawn = AgentFactory.Create(World, "edge", Vector3.zero, 0, false, false);
            pawn.enabled = false;
            var enemy = EnemyFactory.Passive(World, new Vector3(0, 0, 6), 200);
            yield return null;
            var shooter = pawn.GetComponent<AgentCombatShooter>();
            var muzzle = World.Root("Fixed muzzle").transform;
            muzzle.position = CombatAimPointResolver.Resolve(enemy.transform) + Vector3.back * 6;
            RuntimeFixtureAccess.Configure(shooter, "_firePoint", muzzle);
            var cover = World.Cube("Sphere clips this edge", muzzle.position + new Vector3(.58f, 0, 3), Vector3.one);
            Physics.SyncTransforms();
            var aim = CombatAimPointResolver.Resolve(enemy.transform);
            Assert.That(TargetVisibilityQuery.Check(pawn.transform, muzzle.position, enemy.transform, 10), Is.EqualTo(TargetVisibilityResult.Visible));
            Assert.That(ProjectileSweepQuery.TryFirstHit(null, pawn.transform, muzzle.position, aim, .1f, out var hit), Is.True);
            Assert.That(hit.gameObject, Is.EqualTo(cover));
            CaseArtifactWriter.Trace("edge-proof", "ray=Visible; radius=.1; firstHit=" + hit.name);
            Assert.That(shooter.CanShootFrom(enemy, 10, pawn.Position, pawn.transform.rotation), Is.False);
            Assert.That(shooter.TryShootAt(enemy, 50), Is.False);
            Object.Destroy(cover); yield return null; Physics.SyncTransforms();
            Assert.That(shooter.TryShootAt(enemy, 50), Is.True);
            yield return RuntimeWait.Until(() => enemy.GetCurrentHealthRatio() < 1, "移除实体后同一弹体实际伤害", 4);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator WideOverhangEngageFindsPhysicalProjectileClearance([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var pawn = AgentFactory.Create(World, "wide-cover", new Vector3(.067f, 0, -.008f), 8, false, false);
            var enemy = EnemyFactory.Passive(World, Vector3.zero, 200);
            var body = enemy.GetComponent<CapsuleCollider>(); body.center = Vector3.up * 3; body.height = 6; body.radius = 1.5f;
            var shooter = pawn.GetComponent<AgentCombatShooter>();
            RuntimeFixtureAccess.Configure(shooter, "_firePoint", null);
            World.Cube("Wide overhead solid", new Vector3(0, 6.7f, 0), new Vector3(5, 1, 5));
            yield return null; yield return null; Physics.SyncTransforms(); Time.timeScale = speed;
            pawn.TrySubmitDirective(new AgentDirectiveRequest(AgentDirectiveType.Engage,
                AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy, enemy.gameObject),
                commandId: "ManualTargetClick_WideOverhang", priority: 1000));
            double next = 0;
            yield return RuntimeWait.Until(() =>
            {
                if (Time.realtimeSinceStartupAsDouble >= next)
                {
                    next = Time.realtimeSinceStartupAsDouble + .25;
                    var fire = pawn.transform.TransformPoint(new Vector3(0, 1.2f, .6f));
                    var aim = CombatAimPointResolver.Resolve(enemy.transform);
                    bool blocked = ProjectileSweepQuery.TryFirstHit(null, pawn.transform, fire, aim, .1f, out var first);
                    CaseArtifactWriter.Trace("wide-cover", "position=" + pawn.Position + "; muzzle=" + fire +
                        "; ray=" + TargetVisibilityQuery.Check(pawn.transform, fire, enemy.transform, 8) +
                        "; sphereFirst=" + (blocked ? first.name : "none") + "; health=" + enemy.GetCurrentHealthRatio() +
                        "; active=" + pawn.DirectiveLifecycle.Active?.CommandId);
                }
                return enemy.GetCurrentHealthRatio() < 1;
            }, "5 米悬顶外的真实弹丸伤害", 12);
            ContractCompleted = true;
        }
    }
}

using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Perception;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ReachableCombatApproachTests:ReproductionTestFixture
    {
        public static int[] Speeds={1,4};
        private AgentPawnRoot _pawn;
        private EnemyHealthController _enemy;
        private IEnumerator Setup(bool fullyOverlapped)
        {
            TestNavMeshBuilder.Flat(World);
            _pawn=AgentFactory.Create(World,"2",new Vector3(.067f,0,-.008f),8,false,false);
            _enemy=EnemyFactory.Passive(World,Vector3.zero,200);
            var body=_enemy.GetComponent<CapsuleCollider>();body.center=Vector3.up*3;body.height=6;body.radius=1.5f;
            if(fullyOverlapped)
            {
                var adjacent=EnemyFactory.Passive(World,new Vector3(.9f,0,-1.1059f));
                body=adjacent.GetComponent<CapsuleCollider>();body.center=Vector3.up*3;body.height=6;body.radius=1.5f;
            }
            else
            {
                // 实体悬顶位于可行走身体上方，只阻挡当前枪口，侧向存在真实射击位置。
                RuntimeFixtureAccess.Configure(_pawn.GetComponent<AgentCombatShooter>(),"_firePoint",null);
                World.Cube("Overhanging muzzle cover",new Vector3(0,6.7f,1.8f),Vector3.one);
            }
            yield return null;yield return null;Physics.SyncTransforms();
        }
        [UnityTest]public IEnumerator FullyEnclosedAimPointRejectsEveryFiringCandidate()
        {
            yield return Setup(true);var before=_pawn.Position;
            Assert.That(TargetVisibilityQuery.Check(_pawn.transform,CombatAimPointResolver.Resolve(_pawn.transform),_enemy.transform,8),Is.EqualTo(TargetVisibilityResult.Occluded));
            Assert.That(AgentCombatApproachQuery.TryResolve(_pawn,_enemy,8,new AgentCombatApproachQuery.Buffer(),out _),Is.False);
            Assert.That(_pawn.Position,Is.EqualTo(before));ContractCompleted=true;
        }
        [UnityTest]public IEnumerator ReachableButOccludedCenterDoesNotBypassFiringCandidateValidation()
        {
            yield return Setup(false);
            var before=_pawn.Position;var nav=_pawn.NavMeshAgent;
            Assert.That(AgentCombatApproachQuery.TryResolve(_pawn,_enemy,8,new AgentCombatApproachQuery.Buffer(),out var destination),Is.True);
            Assert.That(new Vector2(destination.x,destination.z).magnitude,Is.GreaterThan(.5f));
            var ground=nav.nextPosition-Vector3.up*nav.baseOffset*Mathf.Abs(_pawn.transform.lossyScale.y);
            var prospective=_pawn.Position+destination-ground;
            var body=CombatAimPointResolver.Resolve(_pawn.transform)+destination-ground;
            var facing=_enemy.transform.position-prospective;facing.y=0;
            var rotation=Quaternion.LookRotation(facing,Vector3.up);
            CaseArtifactWriter.Trace("candidate","origin="+before+"; destination="+destination+"; prospective="+prospective);
            Assert.That(TargetVisibilityQuery.Check(_pawn.transform,body,_enemy.transform,8),Is.EqualTo(TargetVisibilityResult.Visible));
            Assert.That(_pawn.GetComponent<AgentCombatShooter>().CanShootFrom(_enemy,8,prospective,rotation),Is.True);
            Assert.That(_pawn.Position,Is.EqualTo(before));ContractCompleted=true;
        }
        [UnityTest]public IEnumerator RealEngageRepositionsFromMuzzleCoverAndDamagesTarget([ValueSource(nameof(Speeds))]int speed)
        {
            yield return Setup(false);Time.timeScale=speed;var before=_pawn.Position;
            Assert.That(_pawn.GetComponent<AgentCombatShooter>().CanShootAt(_enemy,8),Is.False);
            var target=AgentTargetRef.FromConcreteObject(AgentTargetKind.Enemy,_enemy.gameObject);
            _pawn.TrySubmitDirective(new AgentDirectiveRequest(AgentDirectiveType.Engage,target,commandId:"ManualTargetClick_OccludedSentinel",priority:1000));
            double next=0;
            yield return RuntimeWait.Until(()=>
            {
                if(Time.realtimeSinceStartupAsDouble>=next)
                {
                    next=Time.realtimeSinceStartupAsDouble+.25;
                    CaseArtifactWriter.Trace("approach.tick","position="+_pawn.Position+"; destination="+_pawn.NavMeshAgent.destination+
                        "; velocity="+_pawn.NavMeshAgent.velocity+"; active="+_pawn.DirectiveLifecycle.Active?.CommandId+
                        "; health="+_enemy.GetCurrentHealthRatio()+"; result="+_pawn.GetComponent<AgentCombatShooter>().LastShotResult);
                }
                return _enemy.GetCurrentHealthRatio()<1;
            },"正式换位后弹丸伤害",12);
            // 实际转向和小幅移动已可脱离局部遮挡，不要求继续走完整个候选距离。
            Assert.That(Vector3.Distance(before,_pawn.Position),Is.GreaterThan(.05f));
            CaseArtifactWriter.Trace("damaged","position="+_pawn.Position+"; health="+_enemy.GetCurrentHealthRatio()+"; scale="+Time.timeScale);
            ContractCompleted=true;
        }
    }
}

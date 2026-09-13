using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class NavigationProgressTests:ReproductionTestFixture
    {
        public static int[] Speeds={1,4};
        [UnityTest]public IEnumerator SidewaysOscillationCannotRenewTheProgressDeadline([ValueSource(nameof(Speeds))]int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var pawn=AgentFactory.Create(World,"jitter",Vector3.zero,0,false,false);pawn.enabled=false;Time.timeScale=speed;
            var nav=pawn.NavMeshAgent;var motor=new AgentNavigationMotor(nav,2,3);
            double deadline=Time.timeAsDouble+7,next=0;float previous=0;
            var status=AgentNavigationStatus.Moving;
            while(Time.timeAsDouble<deadline&&status!=AgentNavigationStatus.Stalled)
            {
                if(Time.timeAsDouble>=next)
                {
                    next=Time.timeAsDouble+.1;float position=previous<=0?.1f:-.1f;
                    nav.Move(Vector3.right*(position-previous));previous=position;
                }
                status=motor.Move("jitter",Vector3.right*20,0,0).Status;
                Assert.That(Mathf.Abs(pawn.Position.x),Is.LessThan(.25f),"构造只能模拟局部抖动");
                yield return null;
            }
            CaseArtifactWriter.Trace("jitter-terminal",status+"; position="+pawn.Position+"; queries="+motor.PathCalculationCount);
            Assert.That(status,Is.EqualTo(AgentNavigationStatus.Stalled));ContractCompleted=true;
        }
        [UnityTest]public IEnumerator ARealDetourCanMoveAwayFromTheGoalWithoutFalseStalling()
        {
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(-10,-.1f,5),new Vector3(4,.2f,24)),
                new Bounds(new Vector3(0,-.1f,15),new Vector3(24,.2f,4)),
                new Bounds(new Vector3(10,-.1f,5),new Vector3(4,.2f,24)));
            var pawn=AgentFactory.Create(World,"detour",Vector3.left*10,0,false,false);pawn.enabled=false;Time.timeScale=4;
            var motor=new AgentNavigationMotor(pawn.NavMeshAgent,2,1);bool movedAway=false;
            var goal=Vector3.right*10;var status=AgentNavigationStatus.Moving;
            yield return RuntimeWait.Until(()=>
            {
                var delta=pawn.Position-goal;movedAway|=new Vector2(delta.x,delta.z).magnitude>20.5f;
                status=motor.Move("detour",goal,0,8).Status;
                return status==AgentNavigationStatus.Arrived||status==AgentNavigationStatus.Stalled;
            },"真实 U 形绕行",15);
            Assert.That(movedAway,Is.True);Assert.That(status,Is.EqualTo(AgentNavigationStatus.Arrived));ContractCompleted=true;
        }
        [UnityTest]public IEnumerator MovingDestinationRebasesTheSameCommandsProgressBudget()
        {
            TestNavMeshBuilder.Flat(World);var pawn=AgentFactory.Create(World,"moving-goal",Vector3.zero,0,false,false);
            // 使用正式的 3 秒无进展窗口，给原生导航反向转身和减速留出正常时间。
            // 新目标让剩余路程增加约 30 米，未重建基线仍会超过此窗口。
            pawn.enabled=false;Time.timeScale=4;var motor=new AgentNavigationMotor(pawn.NavMeshAgent,2,3);
            bool changed=false;var status=AgentNavigationStatus.Moving;
            yield return RuntimeWait.Until(()=>
            {
                changed|=pawn.Position.x>2;
                status=motor.Move("same-command",Vector3.right*(changed?-35:10),0,4).Status;
                return changed&&status==AgentNavigationStatus.Arrived||status==AgentNavigationStatus.Stalled;
            },"同一指令的目标反向移动",15);
            CaseArtifactWriter.Trace("moving-goal-terminal",status+"; position="+pawn.Position+"; queries="+motor.PathCalculationCount);
            Assert.That(changed,Is.True);Assert.That(status,Is.EqualTo(AgentNavigationStatus.Arrived));
            Assert.That(pawn.Position.x,Is.EqualTo(-35).Within(.4));ContractCompleted=true;
        }
    }
}

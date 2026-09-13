using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandOccupiedAnchorTests:ReproductionTestFixture
    {
        public static int[] Speeds={1,4};
        [UnityTest]public IEnumerator EnemyOccupyingAnchorIsClearedBeforePhysicalVisitAndRouteContinuation([ValueSource(nameof(Speeds))]int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var pawn=AgentFactory.Create(World,"2",Vector3.left*8,8,false,false);
            var enemy=EnemyFactory.Passive(World,Vector3.zero,40);
            var body=enemy.GetComponent<CapsuleCollider>();body.center=Vector3.up*3;body.height=6;body.radius=1.5f;
            // 固定敌人的避让体有真实原生占位，死亡时随敌人物体一起释放。
            var obstacle=enemy.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Capsule;
            obstacle.center=Vector3.up*3;obstacle.radius=1.5f;obstacle.height=6;obstacle.carving=false;
            var second=EnemyFactory.Passive(World,Vector3.forward*9,40);
            var cluster=TargetFactory.Enemies(World,enemy,second);
            var cleared=EnemyFactory.Passive(World,Vector3.right*16);var goal=TargetFactory.Enemies(World,cleared);Object.DestroyImmediate(cleared.gameObject);
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{cluster,goal},new[]{Vector3.zero,Vector3.right*16},MapRouteFactory.Chain(2));
            var installer=World.Root("Occupied anchor installer").AddComponent<RaidMapCommandInstaller>();installer.Configure(binding);
            Time.timeScale=speed;var engagements=new List<Vector3>();
            void Observe(AgentDirectiveResult r)
            {if(r.Accepted&&r.Request.DirectiveType==AgentDirectiveType.Engage)engagements.Add(pawn.Position);}
            pawn.DirectiveLifecycle.ResultPublished+=Observe;
            pawn.TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,pawn.AgentId));
            bool visited=false,physicallyVisited=false;double next=0;
            yield return RuntimeWait.Until(()=>
            {
                var root=pawn.RouteSnapshot;
                if(root.StepIndex==0&&new Vector2(pawn.Position.x,pawn.Position.z).magnitude<.4f)physicallyVisited=true;
                if(root.StepIndex==1&&!visited)
                {
                    visited=true;Assert.That(cluster.CountLivingEnemies(),Is.Zero);
                    Assert.That(physicallyVisited,Is.True,"清群和实际锚点访问都必须完成");
                }
                if(Time.realtimeSinceStartupAsDouble>=next)
                {next=Time.realtimeSinceStartupAsDouble+1;CaseArtifactWriter.Trace("occupied-anchor","position="+pawn.Position+"; step="+root.StepIndex+"; phase="+root.CurrentStep.Phase+"; alive="+cluster.CountLivingEnemies());}
                return root.Stage==AgentRouteStage.Completed||root.Stage==AgentRouteStage.Failed;
            },"先清除锚点上的敌人，访问锚点后继续路线",18);
            Assert.That(pawn.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Completed),pawn.RouteSnapshot.Failure.ToString());
            Assert.That(visited,Is.True);Assert.That(engagements.Count,Is.GreaterThanOrEqualTo(2));
            Assert.That(new Vector2(engagements[0].x,engagements[0].z).magnitude,Is.GreaterThan(.4f));
            Assert.That(pawn.Position.x,Is.EqualTo(16).Within(.4f));
            pawn.DirectiveLifecycle.ResultPublished-=Observe;ContractCompleted=true;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using Gameplay.Targets.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandFeedbackTests : ReproductionTestFixture
    {
        private AgentCommandFeedbackPresenter Hud()
        {
            AgentCommandFeedbackInstaller.EnsureInstalled(); var presenter=Object.FindObjectOfType<AgentCommandFeedbackPresenter>();
            Assert.That(presenter,Is.Not.Null);
            RuntimeFixtureAccess.Configure(presenter,"_fadeIn",0.03f); RuntimeFixtureAccess.Configure(presenter,"_hold",0.1f); RuntimeFixtureAccess.Configure(presenter,"_fadeOut",0.03f);
            return presenter;
        }
        private static int Pending(AgentCommandFeedbackPresenter presenter) => ((ICollection)RuntimeFixtureAccess.Read<object>(presenter,"_pending")).Count;
        private static IEnumerator TimePasses(double seconds)
        { double until=Time.realtimeSinceStartupAsDouble+seconds; yield return RuntimeWait.Until(()=>Time.realtimeSinceStartupAsDouble>=until,"等待提示阶段",seconds+2); }
        private ActiveEnemyClusterAuthoring Cleared(Vector3 position)
        { var enemy=EnemyFactory.Passive(World,position); var cluster=TargetFactory.Enemies(World,enemy); Object.DestroyImmediate(enemy.gameObject); return cluster; }
        private MapGraphBindingAuthoring Bind(AgentPawnRoot pawn)
        {
            var binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{Cleared(Vector3.forward*3),Cleared(Vector3.right*12+Vector3.forward*3)},
                new[]{Vector3.zero,Vector3.right*12},MapRouteFactory.Chain(2));
            pawn.ConfigureRoutes(MapRouteFactory.Environment(binding,pawn)); return binding;
        }
        [UnityTest] public IEnumerator PlanningIsSilentAcceptedShowsOnceAndEveryChildRemainsSilent()
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var binding=Bind(pawn); var hud=Hud();
            var environment=MapRouteFactory.Environment(binding,pawn);
            pawn.ConfigureRoutes(new AgentRouteEnvironment(environment.Graph,environment.GraphRevision,2,environment.Costs,environment.Targets,environment.Profile,false));
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,pawn.AgentId,"feedback-root"));
            yield return TimePasses(0.2); Assert.That(hud.Alpha,Is.Zero); Assert.That(Pending(hud),Is.Zero);
            pawn.ConfigureRoutes(environment);
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"根接受提示"); Assert.That(hud.CurrentText,Is.EqualTo("指令下达成功"));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.HasRoute&&pawn.RouteSnapshot.Stage==AgentRouteStage.Completed,"两群实际走完");
            yield return TimePasses(0.25); Assert.That(hud.Alpha,Is.Zero); Assert.That(Pending(hud),Is.Zero);
            Assert.That(hud.GetComponentInChildren<CanvasGroup>().blocksRaycasts,Is.False); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RootRejectionShowsReasonAndKeepsTheAcceptedRoute()
        {
            TestNavMeshBuilder.Flat(World); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,0,false,false); Bind(pawn); var hud=Hud();
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,pawn.AgentId,"old-root"));
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"首次成功提示"); yield return RuntimeWait.Until(()=>hud.Alpha==0,"首次提示淡出");
            var rejected=AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("missing",AgentRouteSource.Player,pawn.AgentId,"bad-root"));
            Assert.That(rejected.Stage,Is.EqualTo(AgentRouteStage.Rejected));
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"根失败提示"); Assert.That(hud.CurrentText,Is.EqualTo("指令下达失败：目标不存在"));
            Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo("old-root")); Assert.That(Pending(hud),Is.Zero); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator MissingAgentAndLateRegistrationUseOneFeedbackSubscription()
        {
            TestNavMeshBuilder.Flat(World); var hud=Hud();
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player));
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"无角色拒绝仍有提示"); Assert.That(hud.CurrentText,Is.EqualTo("指令下达失败：未选择可用角色"));
            yield return RuntimeWait.Until(()=>hud.Alpha==0,"失败淡出");
            var pawn=AgentFactory.Create(World,"1",Vector3.left*3,0,false,false); var binding=Bind(pawn);
            new AgentTargetCommandDispatcher().TrySubmitClusterRoute((GameplayTargetClusterAuthoringBase)binding.TargetBindings[1].DirectTarget);
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"迟到角色的成功反馈"); Assert.That(hud.CurrentText,Is.EqualTo("指令下达成功"));
            Assert.That(Pending(hud),Is.Zero); yield return RuntimeWait.Until(()=>hud.Alpha==0,"成功淡出");
            pawn.enabled=false; Assert.That(RuntimeFixtureAccess.Read<IDictionary>(hud,"_agents").Count,Is.Zero);
            pawn.enabled=true; AgentRuntimeRegistry.ActiveInstance.Register(pawn);
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,pawn.AgentId,"again"));
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"重注册仍只接一次"); Assert.That(Pending(hud),Is.Zero); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator AutonomousRoutesAndDamageInterruptsDoNotShowPlayerCommandFeedback()
        {
            TestNavMeshBuilder.Flat(World); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,0,false,false); Bind(pawn); var hud=Hud();
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Autonomous,pawn.AgentId));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"自主路线接受");
            var enemy=EnemyFactory.Passive(World,pawn.Position+Vector3.forward*10); pawn.TakeCombatDamage(1,pawn.Position,Vector3.left,enemy.gameObject);
            yield return TimePasses(0.2); Assert.That(hud.Alpha,Is.Zero); Assert.That(Pending(hud),Is.Zero); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator FailedReplanShowsOneRootFailureInsteadOfChildMessages()
        {
            TestNavMeshBuilder.Flat(World); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,0,false,false); var binding=Bind(pawn); var hud=Hud();
            var installer=World.Root("Feedback installer").AddComponent<RaidMapCommandInstaller>(); installer.Configure(binding);
            AgentCommandRouter.GetOrCreate().TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,pawn.AgentId));
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"根首次接受"); yield return RuntimeWait.Until(()=>hud.Alpha==0,"接受提示淡出");
            ((GameplayTargetClusterAuthoringBase)binding.TargetBindings[1].DirectTarget).enabled=false;
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.Stage==AgentRouteStage.Failed,"当前路径无法继续");
            yield return RuntimeWait.Until(()=>hud.Alpha>0.9,"最终根失败提示");
            Assert.That(hud.CurrentText,Does.StartWith("指令下达失败：")); Assert.That(Pending(hud),Is.Zero);
            yield return RuntimeWait.Until(()=>hud.Alpha==0,"根失败提示淡出"); yield return TimePasses(0.2);
            Assert.That(hud.Alpha,Is.Zero); Assert.That(Pending(hud),Is.Zero); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RootMessagesUseCorrectChineseAndDescribePlayerMeaning()
        {
            var request=new AgentRouteRequest("n0",AgentRouteSource.Player);
            Assert.That(AgentCommandFeedbackText.Format(new AgentRouteResult(request,1,AgentRouteStage.Accepted)),Is.EqualTo("指令下达成功"));
            Assert.That(AgentCommandFeedbackText.Format(new AgentRouteResult(request,1,AgentRouteStage.Rejected,AgentRouteFailure.Disconnected)),Is.EqualTo("指令下达失败：没有可通行的群路线"));
            Assert.That(AgentCommandFeedbackText.Format(new AgentRouteResult(request,1,AgentRouteStage.Failed,AgentRouteFailure.CapacityExtraction)),Does.Contain("背包已满"));
            ContractCompleted=true; yield break;
        }
    }
}

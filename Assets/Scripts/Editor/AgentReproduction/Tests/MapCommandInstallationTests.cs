using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandInstallationTests : ReproductionTestFixture
    {
        private MapGraphBindingAuthoring Build(int count=8)
        {
            TestNavMeshBuilder.Flat(World,200);
            var clusters=new GameplayTargetClusterAuthoringBase[count]; var anchors=new Vector3[count];
            for(int i=0;i<count;i++) { anchors[i]=Vector3.right*i*10; clusters[i]=TargetFactory.Resources(World,anchors[i]); }
            return MapRouteFactory.Bind(World,clusters,anchors,MapRouteFactory.Chain(count));
        }
        private RaidMapCommandInstaller Install(MapGraphBindingAuthoring binding)
        { var result=World.Root("Installer").AddComponent<RaidMapCommandInstaller>(); result.Configure(binding); return result; }
        private static AgentRouteEnvironment Environment(RaidMapCommandInstaller installer,AgentPawnRoot pawn) =>
            installer.Environments.GetEnvironment(AgentNavigationProfile.FromAgent(pawn.NavMeshAgent));

        [UnityTest] public IEnumerator NoCommandBindingLeavesLegacySceneUninstalled()
        {
            TestNavMeshBuilder.Flat(World); var pawn=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            Assert.That(RaidMapCommandInstaller.InstallForScene(pawn.gameObject.scene)==null,Is.True);
            yield return null; Assert.That(pawn.RouteSnapshot.IsInstalled,Is.False);
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator ExistingAndLateAgentsShareOneBudgetAndUnusedProfilesAreRemoved()
        {
            var binding=Build(); var first=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false);
            var installer=Install(binding); Assert.That(installer.InstalledAgentCount,Is.EqualTo(1));
            Assert.That(installer.Environments.CalculationCount,Is.EqualTo(4));
            Assert.That(first.RouteSnapshot.IsInstalled,Is.True);
            var second=AgentFactory.Create(World,"2",Vector3.left*6,8,false,false);
            installer.TickInstallation(); Assert.That(installer.InstalledAgentCount,Is.EqualTo(2));
            Assert.That(installer.Environments.CalculationCount,Is.EqualTo(8)); Assert.That(installer.Environments.ProfileCount,Is.EqualTo(1));
            Assert.That(Environment(installer,first),Is.SameAs(Environment(installer,second)));
            yield return RuntimeWait.Until(()=>Environment(installer,first).IsReady,"共享边预算完成");
            Assert.That(installer.Environments.CalculationCount,Is.EqualTo(14));
            second.NavMeshAgent.SetAreaCost(3,2);
            yield return RuntimeWait.Until(()=>installer.Environments.ProfileCount==2,"配置变更建立独立成本");
            first.gameObject.SetActive(false); installer.TickInstallation();
            Assert.That(installer.InstalledAgentCount,Is.EqualTo(1)); Assert.That(installer.Environments.ProfileCount,Is.EqualTo(1));
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RegistryEventsObserveFinalRosterAndDoNotDuplicateRegistration()
        {
            TestNavMeshBuilder.Flat(World); var registry=AgentRuntimeRegistry.GetOrCreate();
            int added=0,removed=0;
            registry.AgentRegistered+=handle=>{added++; Assert.That(registry.TryGetHandle(handle.AgentId,out var found),Is.True); Assert.That(found.PawnRoot,Is.SameAs(handle.PawnRoot));};
            registry.AgentUnregistered+=handle=>{removed++; Assert.That(registry.TryGetHandle(handle.AgentId,out _),Is.False);};
            var first=AgentFactory.Create(World,"1",Vector3.zero,8,false,false);
            var second=AgentFactory.Create(World,"2",Vector3.right*4,8,false,false);
            registry.Register(first); Assert.That(added,Is.EqualTo(2)); Assert.That(registry.FocusedAgentId,Is.EqualTo(first.AgentId));
            first.gameObject.SetActive(false); Assert.That(removed,Is.EqualTo(1)); Assert.That(registry.FocusedAgentId,Is.EqualTo(second.AgentId));
            registry.Unregister(first); Assert.That(removed,Is.EqualTo(1));
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator DisablingInstallerCancelsOwnedRouteAndReenableCreatesUsableController()
        {
            var binding=Build(); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var installer=Install(binding);
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"安装就绪");
            pawn.TrySubmitRoute(new AgentRouteRequest("n7",AgentRouteSource.Player,pawn.AgentId,"before-disable"));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"根路线接受");
            int cancelled=0; pawn.RouteResultPublished+=r=>{if(r.Stage==AgentRouteStage.Cancelled)cancelled++;};
            installer.enabled=false; Assert.That(cancelled,Is.EqualTo(1)); Assert.That(pawn.RouteSnapshot.IsInstalled,Is.False);
            Assert.That(pawn.DirectiveLifecycle.Active.HasValue,Is.False);
            installer.enabled=true;
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"重新安装就绪");
            Assert.That(pawn.TrySubmitRoute(new AgentRouteRequest("n7",AgentRouteSource.Player,pawn.AgentId,"after-disable")).Stage,Is.EqualTo(AgentRouteStage.Planning));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"重新安装可执行");
            Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo("after-disable")); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator OldInstallerCannotReleaseAReplacementEnvironment()
        {
            var binding=Build(); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var installer=Install(binding);
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"安装就绪");
            var replacement=MapRouteFactory.Environment(binding,pawn); pawn.ConfigureRoutes(replacement);
            installer.enabled=false; Assert.That(pawn.RouteSnapshot.IsInstalled,Is.True);
            Assert.That(pawn.TrySubmitRoute(new AgentRouteRequest("n7",AgentRouteSource.Player,pawn.AgentId,"replacement-owner")).Stage,Is.EqualTo(AgentRouteStage.Planning));
            yield return RuntimeWait.Until(()=>pawn.RouteSnapshot.IsActive,"新所有者保留可执行路线");
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator DisabledBindingRemainsExplicitCommandModeAndCanRecover()
        {
            var binding=Build(3); var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var installer=Install(binding);
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"初次就绪");
            binding.enabled=false; installer.TickInstallation();
            Assert.That(pawn.RouteSnapshot.IsInstalled,Is.True);
            Assert.That(pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,pawn.AgentId)).Reason,Is.EqualTo(AgentRouteFailure.MapUnavailable));
            binding.enabled=true; installer.TickInstallation();
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"绑定恢复");
            Assert.That(pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,pawn.AgentId)).Stage,Is.EqualTo(AgentRouteStage.Planning));
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator PendingInitialNavigationAndActualRebuildInvalidateWithoutPerFrameFingerprint()
        {
            var binding=Build(3); var holder=World.Root("Navigation owner",false);
            var builder=holder.AddComponent<RuntimeNavMeshSurfaceBuilder>(); builder.InitialBuildDelay=100; holder.SetActive(true);
            var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var installer=Install(binding);
            Assert.That(builder.HasPendingBuild,Is.True); Assert.That(Environment(installer,pawn).NavigationReady,Is.False);
            installer.TickInstallation(); Assert.That(installer.Environments.CalculationCount,Is.Zero);
            builder.BuildNow(); installer.TickInstallation();
            Assert.That(builder.NavigationRevision,Is.EqualTo(1)); Assert.That(builder.HasPendingBuild,Is.False);
            Assert.That(Environment(installer,pawn).IsReady,Is.True); Assert.That(installer.FingerprintCaptureCount,Is.EqualTo(2));
            long queries=installer.Environments.CalculationCount;
            for(int i=0;i<20;i++) installer.TickInstallation();
            Assert.That(installer.FingerprintCaptureCount,Is.EqualTo(2)); Assert.That(installer.Environments.CalculationCount,Is.EqualTo(queries));
            builder.BuildNow(); installer.TickInstallation();
            Assert.That(installer.FingerprintCaptureCount,Is.EqualTo(3)); Assert.That(installer.Environments.CalculationCount-queries,Is.EqualTo(4));
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator FormalSceneLoadInstallsItsSavedBindingForBothAgents()
        {
            var operation=EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return RuntimeWait.Until(()=>operation.isDone,"正式场景载入",60);
            var installers=Object.FindObjectsOfType<RaidMapCommandInstaller>(); Assert.That(installers.Length,Is.EqualTo(1));
            var installer=installers[0];
            yield return RuntimeWait.Until(()=>installer.InstalledAgentCount==2 && installer.Environments.PendingEdgeCount==0,"两名角色的正式成本完成",30);
            Assert.That(installer.Binding.MapDefinition.Nodes.Count,Is.EqualTo(28));
            foreach(var handle in AgentRuntimeRegistry.ActiveInstance.RegisteredAgents)
                Assert.That(handle.ReadOnly.RouteSnapshot.IsInstalled,Is.True);
            Assert.That(RaidMapCommandInstaller.InstallForScene(installer.gameObject.scene),Is.SameAs(installer));
            CaseArtifactWriter.Trace("formal-install",$"agents={installer.InstalledAgentCount};profiles={installer.Environments.ProfileCount};queries={installer.Environments.CalculationCount};fingerprints={installer.FingerprintCaptureCount}");
            // 先正式卸载场景，再由基类清理测试 NavMesh，不能让活着的敌人在已移除的导航上再跑一帧。
            var formalScene=installer.gameObject.scene;
            var empty=SceneManager.CreateScene("After formal map installation"); SceneManager.SetActiveScene(empty);
            var unload=SceneManager.UnloadSceneAsync(formalScene);
            yield return RuntimeWait.Until(()=>unload.isDone,"正式场景卸载",30);
            Assert.That(installer==null,Is.True);
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator InterruptedInitialNavigationBuildResumesAfterEnable()
        {
            var binding=Build(3); var holder=World.Root("Delayed navigation",false);
            var builder=holder.AddComponent<RuntimeNavMeshSurfaceBuilder>(); builder.InitialBuildDelay=100; holder.SetActive(true);
            var pawn=AgentFactory.Create(World,"1",Vector3.left*3,8,false,false); var installer=Install(binding);
            yield return null; Assert.That(builder.HasPendingBuild,Is.True);
            holder.SetActive(false); builder.InitialBuildDelay=0.05f; holder.SetActive(true);
            yield return RuntimeWait.Until(()=>builder.NavigationRevision==1,"恢复被停止的初次烘焙");
            yield return RuntimeWait.Until(()=>Environment(installer,pawn).IsReady,"恢复后成本可用");
            Assert.That(builder.HasPendingBuild,Is.False); Assert.That(builder.IsBuilding,Is.False);
            ContractCompleted=true;
        }
    }
}

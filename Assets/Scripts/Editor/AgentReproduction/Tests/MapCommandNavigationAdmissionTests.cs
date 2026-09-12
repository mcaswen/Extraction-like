using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandNavigationAdmissionTests:ReproductionTestFixture
    {
        [UnityTest] public IEnumerator DeadAgentAtAnchorDoesNotBlockTheSurvivorsRealRoute()
        {
            TestNavMeshBuilder.Flat(World);Time.timeScale=4;
            var fallen=AgentFactory.Create(World,"1",Vector3.zero,4,false,false);
            var survivor=AgentFactory.Create(World,"2",Vector3.back*8,4,false,false);
            var points=new[]{Vector3.zero,Vector3.right*16};
            var clusters=points.Select(p=>{var enemy=EnemyFactory.Passive(World,p+Vector3.forward*3);
                var cluster=TargetFactory.Enemies(World,enemy);Object.DestroyImmediate(enemy.gameObject);return (GameplayTargetClusterAuthoringBase)cluster;}).ToArray();
            var binding=MapRouteFactory.Bind(World,clusters,points,MapRouteFactory.Chain(2));
            var installer=World.Root("Death occupancy installer").AddComponent<RaidMapCommandInstaller>();installer.Configure(binding);
            fallen.TakeCombatDamage(1000000,fallen.Position,Vector3.forward,null);
            Assert.That(fallen.IsDead,Is.True);Vector3 bodyPosition=fallen.Position;
            survivor.TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,survivor.AgentId));
            yield return RuntimeWait.Until(()=>survivor.RouteSnapshot.Stage==AgentRouteStage.Completed||survivor.RouteSnapshot.Stage==AgentRouteStage.Failed,
                "幸存角色通过死亡队友占用的锚点",15);
            AgentReproduction.Reporting.CaseArtifactWriter.Trace("death-navigation",JsonUtility.ToJson(
                AnomalySearch.Automation.SceneRaid.Commands.SceneRaidRouteEvidence.Capture(survivor))+"; fallenNavEnabled="+fallen.NavMeshAgent.enabled);
            Assert.That(survivor.RouteSnapshot.Stage,Is.EqualTo(AgentRouteStage.Completed),survivor.RouteSnapshot.Failure.ToString());
            Assert.That(survivor.Position.x,Is.EqualTo(16).Within(.4));
            Assert.That(fallen.Position,Is.EqualTo(bodyPosition));Assert.That(fallen.gameObject.activeInHierarchy,Is.True);
            Assert.That(fallen.NavMeshAgent.enabled,Is.False);
            Assert.That(survivor.NavMeshAgent.enabled,Is.True);ContractCompleted=true;
        }
        [UnityTest] public IEnumerator SimultaneousAgentsPassTheSharedEntryAndReachTheirOwnDestinations()
        {
            TestNavMeshBuilder.Flat(World);Time.timeScale=4;
            var first=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            var second=AgentFactory.Create(World,"2",Vector3.left*3+Vector3.forward*4,4,false,false);
            int original=first.NavMeshAgent.avoidancePriority;Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(original));
            var positions=new[]{Vector3.zero,Vector3.right*16,Vector3.right*32};
            var clusters=positions.Select(p=>{var enemy=EnemyFactory.Passive(World,p+Vector3.forward*3);
                var cluster=TargetFactory.Enemies(World,enemy);Object.DestroyImmediate(enemy.gameObject);return (GameplayTargetClusterAuthoringBase)cluster;}).ToArray();
            var binding=MapRouteFactory.Bind(World,clusters,positions,MapRouteFactory.Chain(3));
            var installer=World.Root("Formal navigation installation").AddComponent<RaidMapCommandInstaller>();installer.Configure(binding);
            Assert.That(first.NavMeshAgent.avoidancePriority,Is.Not.EqualTo(second.NavMeshAgent.avoidancePriority));
            first.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player,first.AgentId));
            second.TrySubmitRoute(new AgentRouteRequest("n1",AgentRouteSource.Player,second.AgentId));
            yield return RuntimeWait.Until(()=>first.RouteSnapshot.Stage==AgentRouteStage.Completed&&second.RouteSnapshot.Stage==AgentRouteStage.Completed,"双角色通过共同锚点，到达各自终点",15);
            Assert.That(first.Position.x,Is.EqualTo(32).Within(.4));Assert.That(second.Position.x,Is.EqualTo(16).Within(.4));
            installer.enabled=false;Assert.That(first.NavMeshAgent.avoidancePriority,Is.EqualTo(original));Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(original));
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator AssignmentIsStableAndReservesExplicitPriorities()
        {
            TestNavMeshBuilder.Flat(World);
            var first=AgentFactory.Create(World,"1",Vector3.left*4,0,false,false);
            var second=AgentFactory.Create(World,"2",Vector3.zero,0,false,false);
            var third=AgentFactory.Create(World,"3",Vector3.right*4,0,false,false);
            first.NavMeshAgent.avoidancePriority=50;second.NavMeshAgent.avoidancePriority=50;third.NavMeshAgent.avoidancePriority=51;
            var assignment=new AgentNavigationAvoidanceAssignment();
            var entries=new[]{new AgentNavigationAvoidanceAssignment.AgentEntry("2",second.NavMeshAgent),new AgentNavigationAvoidanceAssignment.AgentEntry("3",third.NavMeshAgent),new AgentNavigationAvoidanceAssignment.AgentEntry("1",first.NavMeshAgent)};
            assignment.Synchronize(entries);
            Assert.That(first.NavMeshAgent.avoidancePriority,Is.EqualTo(50));Assert.That(third.NavMeshAgent.avoidancePriority,Is.EqualTo(51));Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(49));
            assignment.Synchronize(entries.Reverse().ToArray());Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(49));
            assignment.Synchronize(new[]{entries[0]});Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(50));
            assignment.Clear();ContractCompleted=true;yield break;
        }
        [UnityTest] public IEnumerator ReleaseDoesNotOverwriteExternalChangesOrReplacementNavigation()
        {
            TestNavMeshBuilder.Flat(World);
            var first=AgentFactory.Create(World,"1",Vector3.left*4,0,false,false);
            var second=AgentFactory.Create(World,"2",Vector3.zero,0,false,false);
            var assignment=new AgentNavigationAvoidanceAssignment();
            assignment.Synchronize(new[]{new AgentNavigationAvoidanceAssignment.AgentEntry("1",first.NavMeshAgent),new AgentNavigationAvoidanceAssignment.AgentEntry("2",second.NavMeshAgent)});
            second.NavMeshAgent.avoidancePriority=12;assignment.Clear();Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(12));
            assignment.Synchronize(new[]{new AgentNavigationAvoidanceAssignment.AgentEntry("1",first.NavMeshAgent)});
            assignment.Synchronize(new[]{new AgentNavigationAvoidanceAssignment.AgentEntry("1",second.NavMeshAgent)});
            Assert.That(assignment.Count,Is.EqualTo(1));Assert.That(first.NavMeshAgent.avoidancePriority,Is.EqualTo(50));
            assignment.Clear();Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(12));ContractCompleted=true;yield break;
        }
        [UnityTest] public IEnumerator AnOldInstallerCannotRestoreAReplacementOwnersIdenticalValue()
        {
            TestNavMeshBuilder.Flat(World);
            var first=AgentFactory.Create(World,"1",Vector3.left*4,0,false,false);var second=AgentFactory.Create(World,"2",Vector3.zero,0,false,false);
            var entries=new[]{new AgentNavigationAvoidanceAssignment.AgentEntry("1",first.NavMeshAgent),new AgentNavigationAvoidanceAssignment.AgentEntry("2",second.NavMeshAgent)};
            var old=new AgentNavigationAvoidanceAssignment();var current=new AgentNavigationAvoidanceAssignment();
            old.Synchronize(entries);current.Synchronize(entries);
            Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(51));old.Clear();
            Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(51));current.Clear();
            Assert.That(first.NavMeshAgent.avoidancePriority,Is.EqualTo(50));Assert.That(second.NavMeshAgent.avoidancePriority,Is.EqualTo(50));
            ContractCompleted=true;yield break;
        }
    }
}

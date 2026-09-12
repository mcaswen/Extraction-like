using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Core;
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
    public sealed class MapCommandRouteEvidenceTests:ReproductionTestFixture
    {
        [Serializable] private sealed class Row { public string kind,detail; }
        private AgentPawnRoot _pawn;
        private RaidMapCommandInstaller _installer;
        private SceneRaidEvidenceWriter _writer;
        private SceneRaidRouteEvidence _evidence;
        private string _folder;
        private void Setup()
        {
            TestNavMeshBuilder.Flat(World);
            _folder=Path.Combine(TestRunContext.Load().outputPath,"route-probes",TestContext.CurrentContext.Test.Name);
            _writer=new SceneRaidEvidenceWriter(_folder);_evidence=new SceneRaidRouteEvidence(_writer);
            _pawn=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            var points=new[]{Vector3.zero,Vector3.right*8,Vector3.right*16};
            var clusters=points.Select(x=>{var enemy=EnemyFactory.Passive(World,x+Vector3.forward*3);
                var cluster=TargetFactory.Enemies(World,enemy);Object.DestroyImmediate(enemy.gameObject);return (GameplayTargetClusterAuthoringBase)cluster;}).ToArray();
            var binding=MapRouteFactory.Bind(World,clusters,points,MapRouteFactory.Chain(3));
            _installer=World.Root("Installer").AddComponent<RaidMapCommandInstaller>();_installer.Configure(binding);
            Time.timeScale=4;
            // 验证器在正式安装前订阅 Registry，已创建角色也能补订阅。
            _evidence.Tick(true);
        }
        private IEnumerator Wait(Func<bool> condition,string reason)
        {yield return RuntimeWait.Until(()=>{_evidence.Tick();return condition();},reason);_evidence.Tick(true);}
        [UnityTest] public IEnumerator JournalContainsActualRootSequenceTransitionsAndTerminalResult()
        {
            Setup();_pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player));
            yield return Wait(()=>_pawn.RouteSnapshot.Stage==AgentRouteStage.Completed,"探针记录完整真实路线");
            _evidence.Dispose();_writer.Dispose();_writer=null;
            var rows=File.ReadAllLines(Path.Combine(_folder,"events.jsonl")).Select(JsonUtility.FromJson<Row>).ToArray();
            var states=rows.Where(x=>x.kind=="route.state").Select(x=>JsonUtility.FromJson<SceneRaidRouteEvidence.RootRecord>(x.detail)).ToArray();
            Assert.That(states.Where(x=>x.active).Select(x=>x.cursor).Distinct(),Is.EquivalentTo(new[]{0,1,2}));
            Assert.That(states.Last().stage,Is.EqualTo("Completed"));
            Assert.That(states.Last().nodes,Is.EqualTo(new[]{"n0","n1","n2"}));
            Assert.That(states.Last().position,Is.EqualTo(_pawn.Position));
            Assert.That(rows.Any(x=>x.kind=="route.result"&&x.detail.Contains("\"stage\":\"Completed\"")),Is.True);
            Assert.That(rows.Any(x=>x.kind=="route.graph"),Is.True);ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RepeatedCaptureDoesNotSubmitMoveAdvanceRootOrQueryNavigation()
        {
            Setup();_pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player));
            yield return Wait(()=>_pawn.RouteSnapshot.StepIndex==1,"进入真实边");
            yield return null;_installer.Presentation.TickPresentation();
            var before=_pawn.RouteSnapshot;var position=_pawn.Position;var destination=_pawn.NavMeshAgent.destination;
            var motor=RuntimeFixtureAccess.Read<AgentNavigationMotor>(_pawn,"_navigationMotor");
            long native=motor.PathCalculationCount,edges=_installer.Environments.CalculationCount,
                distances=_installer.Presentation.Projection.GetDistanceCalculationCount("1");
            int results=_evidence.ResultCount;
            for(int i=0;i<50;i++){_evidence.Tick(true);SceneRaidRouteEvidence.Capture(_pawn);SceneRaidRouteEvidence.CaptureDisplay(_installer.Presentation);}
            Assert.That(_pawn.RouteSnapshot.Request.RequestId,Is.EqualTo(before.Request.RequestId));
            Assert.That(_pawn.RouteSnapshot.StepIndex,Is.EqualTo(before.StepIndex));Assert.That(_pawn.Position,Is.EqualTo(position));
            Assert.That(_pawn.NavMeshAgent.destination,Is.EqualTo(destination));Assert.That(motor.PathCalculationCount,Is.EqualTo(native));
            Assert.That(_installer.Environments.CalculationCount,Is.EqualTo(edges));
            Assert.That(_installer.Presentation.Projection.GetDistanceCalculationCount("1"),Is.EqualTo(distances));
            Assert.That(_evidence.ResultCount,Is.EqualTo(results));ContractCompleted=true;
        }
        [UnityTest] public IEnumerator DisposalStopsSubscriptionsAndSnapshotsKeepTheirOwnNodeArray()
        {
            Setup();_pawn.TrySubmitRoute(new AgentRouteRequest("n2",AgentRouteSource.Player));
            yield return Wait(()=>_pawn.RouteSnapshot.IsActive,"接受真实根");
            var captured=SceneRaidRouteEvidence.Capture(_pawn);captured.nodes[0]="modified-copy";
            Assert.That(_pawn.RouteSnapshot.NodeIds[0],Is.EqualTo("n0"));int count=_evidence.ResultCount;
            _evidence.Dispose();_pawn.TrySubmitRoute(new AgentRouteRequest("missing-node",AgentRouteSource.Player));
            _evidence.Tick(true);Assert.That(_evidence.ResultCount,Is.EqualTo(count));ContractCompleted=true;
        }
        [UnityTearDown] public IEnumerator ReleaseProbe()
        {_evidence?.Dispose();_writer?.Dispose();_evidence=null;_writer=null;yield break;}
    }
}

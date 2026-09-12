using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Raid;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandRouteScenarioTests:ReproductionTestFixture
    {
        private SceneRaidEvidenceWriter _writer;
        [Serializable]private sealed class Row{public string kind,detail;}
        [UnityTest]public IEnumerator RouteScenarioRejectsWrongVersionHashAndUnboundedOperations()
        {
            var scenario=new SceneRaidRouteScenario{id="MR02"};scenario.Validate();
            string json=JsonUtility.ToJson(scenario);
            var config=new SceneRaidScenarioConfig{schemaVersion=3,mode="ManualRoutes",scenarioJson=json,scenarioSha256=SceneRaidScenarioConfig.HashScenario(json)};
            Assert.That(config.ParseRouteScenario().agent,Is.EqualTo("2"));
            config.schemaVersion=2;Assert.Throws<InvalidOperationException>(()=>config.ParseRouteScenario());
            config.schemaVersion=3;config.scenarioSha256="wrong";Assert.Throws<InvalidOperationException>(()=>config.ParseRouteScenario());
            scenario.wallDeadline=float.PositiveInfinity;Assert.Throws<InvalidOperationException>(()=>scenario.Validate());
            scenario.wallDeadline=60;scenario.operations=new[]{"Near","FarCrossZone","InvalidPreserves","Near"};Assert.Throws<InvalidOperationException>(()=>scenario.Validate());
            ContractCompleted=true;yield break;
        }
        [UnityTest]public IEnumerator FiniteScriptUsesRealMovementReplacementAndPreservesTheOtherAgent()
        {
            TestNavMeshBuilder.Flat(World);
            var first=AgentFactory.Create(World,"1",new Vector3(-20,0,-20),4,false,false);
            var pawn=AgentFactory.Create(World,"2",Vector3.left*5,4,false,false);
            var anchors=new[]{Vector3.right*10,Vector3.right*20,Vector3.right*35};
            var clusters=anchors.Select(x=>{var e=EnemyFactory.Passive(World,x+Vector3.forward*3);var c=TargetFactory.Enemies(World,e);
                Object.DestroyImmediate(e.gameObject);return (GameplayTargetClusterAuthoringBase)c;}).ToArray();
            var binding=MapRouteFactory.Bind(World,clusters,anchors,MapRouteFactory.Chain(3));var definition=binding.MapDefinition;
            definition.ApplyCommandData(definition.MapId,definition.DisplayName,"",new[]{
                new MapGraphZoneDefinition("near","近区",new Rect(0,-100,300,200),new Vector2(60,20),isSynthetic:true),
                new MapGraphZoneDefinition("far","远区",new Rect(400,-100,300,200),new Vector2(60,20),isSynthetic:true)},
                new[]{new MapGraphNodeDefinition("n0",MapGraphNodeKind.ActiveEnemy,Vector2.zero,zoneId:"near"),
                    new MapGraphNodeDefinition("n1",MapGraphNodeKind.ActiveEnemy,Vector2.right*100,zoneId:"near"),
                    new MapGraphNodeDefinition("n2",MapGraphNodeKind.ActiveEnemy,Vector2.zero,zoneId:"far")},definition.Edges,definition.LayoutConstraints,new MapGraphNavigationBakeData());
            binding.Configure(definition,binding.TargetBindings,new[]{new MapGraphZoneBinding("near",null,isSynthetic:true),new MapGraphZoneBinding("far",null,isSynthetic:true)});
            var installer=World.Root("Installer").AddComponent<RaidMapCommandInstaller>();installer.Configure(binding);Time.timeScale=4;
            string folder=Path.Combine(TestRunContext.Load().outputPath,"route-script");_writer=new SceneRaidEvidenceWriter(folder);
            using var evidence=new SceneRaidRouteEvidence(_writer);
            var driver=new SceneRaidRouteCommandDriver(new SceneRaidRouteScenario{id="MR02",farMinimum=30},_writer);
            yield return null;yield return null;
            var otherPosition=first.Position;
            yield return RuntimeWait.Until(()=>{driver.Tick();evidence.Tick();return driver.IsDone;},"有限近远无效指令",60);
            Assert.That(driver.SubmittedCount,Is.EqualTo(3));
            string request=pawn.RouteSnapshot.Request.RequestId;
            for(int i=0;i<50;i++)driver.Tick();Assert.That(driver.SubmittedCount,Is.EqualTo(3));
            yield return RuntimeWait.Until(()=>{evidence.Tick();return pawn.RouteSnapshot.Stage==AgentRouteStage.Completed;},"真正到达远群终点",60);
            Assert.That(pawn.RouteSnapshot.Request.RequestId,Is.EqualTo(request));
            Assert.That(pawn.Position.x,Is.EqualTo(35).Within(.4));
            Assert.That(first.Position,Is.EqualTo(otherPosition));Assert.That(first.RouteSnapshot.HoldsPlayerRoute,Is.False);
            evidence.Tick(true);evidence.Dispose();_writer.Dispose();_writer=null;
            var rows=File.ReadAllLines(Path.Combine(folder,"events.jsonl")).Select(JsonUtility.FromJson<Row>).ToArray();
            var submitted=rows.Where(x=>x.kind=="routeScript.submitted").Select(x=>JsonUtility.FromJson<SceneRaidRouteCommandDriver.Submission>(x.detail)).ToArray();
            Assert.That(submitted.Select(x=>x.operation),Is.EqualTo(new[]{"Near","FarCrossZone","InvalidPreserves"}));
            Assert.That(submitted[2].stage,Is.EqualTo("Rejected"));Assert.That(submitted[2].reason,Is.EqualTo("MissingTarget"));
            Assert.That(submitted[2].before.requestId,Is.EqualTo(submitted[2].after.requestId));
            Assert.That(submitted[2].before.version,Is.EqualTo(submitted[2].after.version));
            Assert.That(rows.Any(x=>x.kind=="routeScript.progress"),Is.True);ContractCompleted=true;
        }
        [UnityTest]public IEnumerator StopIsIdempotentAndCannotSubmitWithoutPrerequisites()
        {
            string folder=Path.Combine(TestRunContext.Load().outputPath,"route-script-stop");_writer=new SceneRaidEvidenceWriter(folder);
            var driver=new SceneRaidRouteCommandDriver(new SceneRaidRouteScenario{id="MR02"},_writer);
            driver.Tick();driver.Stop();driver.Stop();for(int i=0;i<50;i++)driver.Tick();
            Assert.That(driver.SubmittedCount,Is.Zero);Assert.That(driver.IsDone,Is.True);ContractCompleted=true;yield break;
        }
        [UnityTearDown]public IEnumerator ReleaseWriter(){_writer?.Dispose();_writer=null;yield break;}
    }
}

using System;
using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Routes;
using Gameplay.Agent.SO;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Runtime;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphRouteProjectionTests : ReproductionTestFixture
    {
        private AgentPawnRoot _pawn;
        private AgentGraphProjectionController _projection;
        private MapGraphBindingAuthoring _binding;
        private void Setup(bool resourceEnd=false)
        {
            TestNavMeshBuilder.Flat(World); Time.timeScale=4;
            _pawn=AgentFactory.Create(World,"1",Vector3.left*3,4,false,false);
            var positions=new[]{Vector3.zero,Vector3.right*14,Vector3.right*28};
            var clusters=positions.Select(x=>{var enemy=EnemyFactory.Passive(World,x+Vector3.forward*3);
                var cluster=TargetFactory.Enemies(World,enemy); Object.DestroyImmediate(enemy.gameObject); return (GameplayTargetClusterAuthoringBase)cluster;}).ToArray();
            if(resourceEnd) clusters[2]=TargetFactory.Resources(World,Vector3.right*30);
            _binding=MapRouteFactory.Bind(World,clusters,positions,MapRouteFactory.Chain(3));
            _pawn.ConfigureRoutes(MapRouteFactory.Environment(_binding,_pawn));
            _projection=World.Root("Real route projection").AddComponent<AgentGraphProjectionController>();
            _projection.Initialize(_binding.MapDefinition,_binding);
        }
        private void Send(string target,string id) => _pawn.TrySubmitRoute(new AgentRouteRequest(target,AgentRouteSource.Player,_pawn.AgentId,id));
        private void SetSpeed(float speed) => RuntimeFixtureAccess.Configure(RuntimeFixtureAccess.Read<AgentPawnConfig>(_pawn,"_pawnConfig"),"_moveSpeed",speed);
        private MapGraphAgentRuntimeState State { get { _projection.Tick(0); return _projection.AgentStates.Single(x=>x.AgentId=="1"); } }
        private IEnumerator Observe(double seconds,Action assertion=null)
        {
            double end=Time.realtimeSinceStartupAsDouble+seconds;
            while(Time.realtimeSinceStartupAsDouble<end) { _projection.Tick(0); assertion?.Invoke(); yield return null; }
        }
        private IEnumerator OnFirstEdge()
        {
            Send("n2","outgoing");
            yield return RuntimeWait.Until(()=>{_projection.Tick(0); return _pawn.RouteSnapshot.StepIndex==1&&_pawn.Position.x>3&&_projection.AgentStates[0].IsOnEdge;},"实际进入第一条边");
            Assert.That(Time.timeScale,Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator ProjectionReadsTheExecutedSequenceAndStationaryTimeCannotAdvanceIt()
        {
            Setup(); yield return OnFirstEdge(); SetSpeed(0);
            yield return RuntimeWait.Until(()=>_pawn.NavMeshAgent.speed==0&&_pawn.NavMeshAgent.velocity.sqrMagnitude<0.0001f,"真实导航停止");
            yield return Observe(0.1); var state=State; float progress=state.CurrentEdgeProgress01;
            int cursor=_pawn.RouteSnapshot.StepIndex; Vector3 world=_pawn.Position;
            yield return Observe(0.35,()=>{
                string probe="world="+_pawn.Position.ToString("R")+"; start="+world.ToString("R")+"; speed="+_pawn.NavMeshAgent.speed+"; velocity="+_pawn.NavMeshAgent.velocity.ToString("R")+
                    "; remaining="+_pawn.NavMeshAgent.remainingDistance+"; baseline="+State.BaselineDistance+"; sampled="+State.RemainingDistance;
                Assert.That(Vector3.Distance(world,_pawn.Position),Is.LessThan(0.01),probe);
                Assert.That(State.CurrentEdgeProgress01,Is.EqualTo(progress).Within(0.01),probe);
                Assert.That(State.StepIndex,Is.EqualTo(cursor));
            });
            Assert.That(Vector3.Distance(world,_pawn.Position),Is.LessThan(0.01));
            CollectionAssert.AreEqual(_pawn.RouteSnapshot.NodeIds.Skip(cursor).ToArray(),State.RemainingPathNodeIds);
            Assert.That(State.RootRequestId,Is.EqualTo("outgoing"));
            Assert.That(State.CurrentEdgeFromNodeId,Is.EqualTo("n0")); Assert.That(State.CurrentEdgeToNodeId,Is.EqualTo("n1"));
            SetSpeed(4); yield return Observe(0.25);
            Assert.That(State.CurrentEdgeProgress01,Is.GreaterThan(progress)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator NativeDistanceIsBorrowedWithoutAdditionalQueryOrDestinationMutation()
        {
            Setup(); yield return OnFirstEdge(); SetSpeed(0); yield return Observe(0.1);
            var sampler=new MapGraphRouteDistanceSampler(); var original=_pawn.NavMeshAgent.destination;
            for(int i=0;i<20;i++)
            {
                var sample=sampler.Sample(_pawn,_pawn.RouteSnapshot,Time.realtimeSinceStartupAsDouble+i*0.05);
                Assert.That(sample.Source,Is.EqualTo(MapGraphDistanceSource.NativePath),
                    "native="+_pawn.NavMeshAgent.destination.ToString("R")+"; anchor="+_pawn.RouteSnapshot.CurrentStep.Anchor.ToString("R")+"; status="+_pawn.NavMeshAgent.pathStatus+"; hasPath="+_pawn.NavMeshAgent.hasPath);
                Assert.That(sample.RemainingDistance,Is.EqualTo(_pawn.NavMeshAgent.remainingDistance));
            }
            Assert.That(sampler.CalculationCount,Is.Zero); Assert.That(_pawn.NavMeshAgent.destination,Is.EqualTo(original));
            ContractCompleted=true;
        }
        [UnityTest] public IEnumerator RetaliationMeasuresTheOriginalAnchorWithBoundedReadOnlyQueries()
        {
            Setup(); yield return OnFirstEdge(); var anchor=_pawn.RouteSnapshot.CurrentStep.Anchor;
            var enemy=EnemyFactory.Passive(World,_pawn.Position+Vector3.forward*12);
            _pawn.TakeCombatDamage(10,_pawn.Position,Vector3.left,enemy.gameObject);
            yield return RuntimeWait.Until(()=>_pawn.RouteSnapshot.CurrentStep.IsRetaliating,"真实反击开始");
            long before=_projection.GetDistanceCalculationCount("1");
            yield return Observe(0.8,()=>{
                var route=_pawn.RouteSnapshot;
                Assert.That(route.Request.RequestId,Is.EqualTo("outgoing"));
                var destination=_pawn.NavMeshAgent.destination; _projection.Tick(0);
                Assert.That(_pawn.NavMeshAgent.destination,Is.EqualTo(destination));
                if(_projection.TryGetDistanceSample("1",out var sample)) Assert.That(sample.Anchor,Is.EqualTo(anchor));
                Assert.That(State.CurrentEdgeId,Is.EqualTo("e0"));
            });
            long queries=_projection.GetDistanceCalculationCount("1")-before;
            Assert.That(queries,Is.InRange(1L,4L)); Assert.That(State.IsRetaliating,Is.True);
            Object.DestroyImmediate(enemy.gameObject);
            yield return RuntimeWait.Until(()=>!_pawn.RouteSnapshot.CurrentStep.IsRetaliating,"反击结束恢复");
            yield return Observe(0.1); Assert.That(State.RootRequestId,Is.EqualTo("outgoing")); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator MidEdgeReversalRetainsThePhysicalEntryEdgeAcrossRepeatedCommands()
        {
            Setup(); yield return OnFirstEdge(); SetSpeed(0);
            yield return RuntimeWait.Until(()=>_pawn.NavMeshAgent.speed==0&&_pawn.NavMeshAgent.velocity.sqrMagnitude<0.0001f,"改令前导航停止");
            yield return Observe(0.1); float location=State.GraphPosition.x;
            Send("n0","reverse");
            yield return RuntimeWait.Until(()=>_pawn.RouteSnapshot.Request.RequestId=="reverse","折返接受");
            Assert.That(_pawn.RouteSnapshot.EntryFromNodeId,Is.EqualTo("n1"));
            Assert.That(_pawn.RouteSnapshot.PreviousNodeId,Is.EqualTo("n1"));
            CollectionAssert.AreEqual(new[]{"n0"},_pawn.RouteSnapshot.NodeIds);
            yield return Observe(0.06); Assert.That(State.GraphPosition.x,Is.EqualTo(location).Within(0.02));
            Send("n2","forward-again");
            yield return RuntimeWait.Until(()=>_pawn.RouteSnapshot.Request.RequestId=="forward-again","同边再次改令");
            Assert.That(new[]{"n0","n1"},Does.Contain(_pawn.RouteSnapshot.NodeIds[0]));
            Assert.That(_pawn.RouteSnapshot.EntryFromNodeId,Is.Not.Empty);
            yield return Observe(0.06); Assert.That(State.GraphPosition.x,Is.EqualTo(location).Within(0.02)); ContractCompleted=true;
        }
        [UnityTest] public IEnumerator ProcessingAndNavigationLossCannotInventAnotherEdgeOrAdvanceTheCursor()
        {
            Setup(true); yield return OnFirstEdge();
            _pawn.NavMeshAgent.enabled=false; var previous=State.CurrentEdgeProgress01;
            yield return Observe(0.2); Assert.That(State.HasValidDistance,Is.False);
            Assert.That(State.CurrentEdgeProgress01,Is.EqualTo(previous));
            _pawn.NavMeshAgent.enabled=true;
            yield return RuntimeWait.Until(()=>_pawn.RouteSnapshot.CurrentStep.Phase==AgentClusterStepPhase.WaitingForInventory,"实际资源处理等待",15);
            yield return Observe(0.06); Assert.That(State.IsOnEdge,Is.False); Assert.That(State.CurrentNodeId,Is.EqualTo("n2"));
            Assert.That(State.DisplayMode,Is.EqualTo(MapGraphAgentDisplayMode.Waiting));
            Assert.That(State.RemainingPathNodeIds.Count,Is.EqualTo(1));
            Object.DestroyImmediate(_pawn.gameObject); yield return Observe(0.06); Assert.That(_projection.AgentStates,Is.Empty); ContractCompleted=true;
        }
    }
}

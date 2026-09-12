using System.Collections;
using AgentReproduction.Infrastructure;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphPositionProjectionTests : ReproductionTestFixture
    {
        private static float Sample(MapGraphAgentPositionProjection p,float d,bool valid=true,long version=1,string segment="root:1:1",string edge="edge",string from="a",string to="b",float tolerance=2)
            => p.Update(segment,edge,from,to,valid,d,tolerance,version);
        [UnityTest] public IEnumerator FrozenDistanceAndToleranceProduceMeasuredProgressAndRetreat()
        {
            var p=new MapGraphAgentPositionProjection(); Assert.That(Sample(p,100),Is.Zero);
            Assert.That(Sample(p,51),Is.EqualTo(0.5f).Within(0.0001));
            Assert.That(Sample(p,75.5f,tolerance:10),Is.EqualTo(0.25f).Within(0.0001));
            Assert.That(p.BaselineDistance,Is.EqualTo(100)); Assert.That(p.BaselineTolerance,Is.EqualTo(2));
            Assert.That(Sample(p,2),Is.EqualTo(1)); ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator InvalidNonfiniteAndPendingDistanceFreezeUntilRecoveryBaseline()
        {
            var p=new MapGraphAgentPositionProjection(); Sample(p,100); Sample(p,51);
            foreach(float d in new[]{float.NaN,float.PositiveInfinity,-1f}) Assert.That(Sample(p,d),Is.EqualTo(0.5f));
            for(int i=0;i<50;i++) Assert.That(Sample(p,20,false),Is.EqualTo(0.5f));
            Assert.That(p.HasValidDistance,Is.False); Assert.That(Sample(p,80),Is.EqualTo(0.5f));
            Assert.That(Sample(p,41),Is.EqualTo(0.75f).Within(0.0001)); ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator SameEdgeReplacementAndDirectionReversalKeepTheSamePosition()
        {
            var p=new MapGraphAgentPositionProjection(); Sample(p,102); Sample(p,62);
            Assert.That(Sample(p,62,segment:"new"),Is.EqualTo(0.4f).Within(0.0001));
            Assert.That(Sample(p,42,segment:"reverse",from:"b",to:"a"),Is.EqualTo(0.6f).Within(0.0001));
            var location=MapGraphAgentPositionProjection.PositionOnSegment(new Vector2(100,0),Vector2.zero,p.Progress01);
            Assert.That(location.x,Is.EqualTo(40).Within(0.001)); Assert.That(location.y,Is.Zero);
            Assert.That(Sample(p,22,segment:"reverse",from:"b",to:"a"),Is.EqualTo(0.8f).Within(0.0001));
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator PathRevisionCalibratesOnceAndNewEdgeStartsFromItsPort()
        {
            var p=new MapGraphAgentPositionProjection(); Sample(p,102); Sample(p,52);
            Assert.That(Sample(p,202,version:2),Is.EqualTo(0.5f));
            Assert.That(Sample(p,102,version:2),Is.EqualTo(0.75f));
            Assert.That(Sample(p,100,version:3,edge:"next",from:"b",to:"c"),Is.Zero);
            ContractCompleted=true; yield break;
        }
        [UnityTest] public IEnumerator ZeroNearToleranceAndInvalidToleranceStayFinite()
        {
            foreach(float distance in new[]{0f,1f,2f,2.00001f})
            {
                var p=new MapGraphAgentPositionProjection(); float value=Sample(p,distance);
                Assert.That(float.IsNaN(value)||float.IsInfinity(value),Is.False);
                Assert.That(Sample(p,0),Is.EqualTo(1)); Assert.That(Sample(p,100,tolerance:float.NaN),Is.EqualTo(1));
            }
            ContractCompleted=true; yield break;
        }
    }
}

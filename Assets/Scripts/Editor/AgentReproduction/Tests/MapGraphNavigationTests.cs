using System;
using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Navigation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphNavigationTests : ReproductionTestFixture
    {
        private AgentNavigationProfile Profile(int mask = NavMesh.AllAreas, float radius = 1, float height = 0.5f)
            => new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID, mask, radius, height);

        [UnityTest]
        public IEnumerator StaticAndLiveQueriesAgreeWithoutChangingMovement()
        {
            TestNavMeshBuilder.Flat(World);
            var pawn = AgentFactory.Create(World, "NavigationSnapshot", Vector3.zero);
            pawn.enabled = false;
            yield return null;
            var nav = pawn.NavMeshAgent;
            Assert.That(nav.SetDestination(Vector3.right * 8), Is.True);
            yield return RuntimeWait.Until(() => !nav.pathPending, "existing native path", 2);
            nav.isStopped = true;
            Vector3 before = nav.nextPosition, destination = nav.destination, end = nav.pathEndPosition;
            var live = AgentNavigationSegmentQuery.Calculate(nav, Vector3.forward * 12, new AgentNavigationSegmentQuery.Buffer());
            var frozen = AgentNavigationSegmentQuery.Calculate(AgentNavigationProfile.FromAgent(nav), live.Origin,
                Vector3.forward * 12, new AgentNavigationSegmentQuery.Buffer());
            Assert.That(live.IsComplete && frozen.IsComplete, Is.True);
            Assert.That(frozen.Length, Is.EqualTo(live.Length).Within(0.05f));
            Assert.That(nav.nextPosition, Is.EqualTo(before));
            Assert.That(nav.destination, Is.EqualTo(destination));
            Assert.That(nav.pathEndPosition, Is.EqualTo(end));
            Assert.That(nav.isStopped && nav.hasPath, Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator AreaCostsChooseDetoursButReportPhysicalLength()
        {
            BuildCostArea();
            Vector3 start = Vector3.left * 12, end = Vector3.right * 12;
            var go = World.Root("Cost profile agent"); go.transform.position = start;
            var nav = go.AddComponent<NavMeshAgent>();
            Assert.That(nav.isOnNavMesh, Is.True);
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            var direct = AgentNavigationSegmentQuery.Calculate(Profile(), start, end, buffer);
            nav.SetAreaCost(3, 100);
            var profile = AgentNavigationProfile.FromAgent(nav);
            var costly = AgentNavigationSegmentQuery.Calculate(profile, start, end, buffer);
            var live = AgentNavigationSegmentQuery.Calculate(nav, end, new AgentNavigationSegmentQuery.Buffer());
            Assert.That(direct.IsComplete && costly.IsComplete && live.IsComplete, Is.True);
            Assert.That(direct.Length, Is.EqualTo(24).Within(0.1f));
            Assert.That(costly.Length, Is.GreaterThan(direct.Length + 1));
            Assert.That(costly.Length, Is.LessThan(50), "Report physical corner length, not the weighted area sum.");
            Assert.That(costly.Length, Is.EqualTo(live.Length).Within(0.1f));
            nav.SetAreaCost(3, 1);
            var unchanged = AgentNavigationSegmentQuery.Calculate(profile, start, end, buffer);
            Assert.That(unchanged.Length, Is.EqualTo(costly.Length).Within(0.01f));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator AreaMasksConstrainSamplingAndPaths()
        {
            BuildCostArea();
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            var detour = AgentNavigationSegmentQuery.Calculate(Profile(1), Vector3.left * 12, Vector3.right * 12, buffer);
            Assert.That(detour.IsComplete, Is.True);
            Assert.That(detour.Length, Is.GreaterThan(25));
            var forbidden = AgentNavigationSegmentQuery.Calculate(Profile(1), Vector3.left * 12, Vector3.zero, buffer);
            Assert.That(forbidden.IsComplete, Is.False);
            Assert.That(forbidden.Failure, Is.EqualTo("SampleMissing"));
            Assert.That(AgentNavigationSegmentQuery.Calculate(Profile(0), Vector3.left * 12, Vector3.right * 12, buffer).Failure,
                Is.EqualTo("OriginSampleMissing"));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator DisconnectedIslandsNeverProduceACompleteSegment()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12)),
                new Bounds(new Vector3(30, -0.1f, 0), new Vector3(12, 0.2f, 12)));
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            var result = AgentNavigationSegmentQuery.Calculate(Profile(), Vector3.zero, Vector3.right * 30, buffer);
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Failure, Is.EqualTo("PathPartial"));
            Assert.That(result.Length, Is.EqualTo(float.PositiveInfinity));
            Assert.That(buffer.CalculationCount, Is.EqualTo(1));
            Assert.That(buffer.LastCornerCount, Is.Zero);
            Assert.That(AgentNavigationSegmentQuery.Calculate(Profile(), Vector3.zero, Vector3.right * 3, buffer).IsComplete, Is.True);
            Assert.That(buffer.LastFailure, Is.Null);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator SamplingRejectsWrongFloorsInvalidCoordinatesAndMissingEndpoints()
        {
            TestNavMeshBuilder.Flat(World, 20);
            var profile = Profile(radius: 4, height: 0.5f);
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            Assert.That(AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.up * 2, buffer).Failure,
                Is.EqualTo("SampleHeightMismatch"));
            Assert.That(AgentNavigationSegmentQuery.Calculate(profile, Vector3.up * 2, Vector3.zero, buffer).Failure,
                Is.EqualTo("OriginSampleHeightMismatch"));
            Assert.That(AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 100, buffer).Failure,
                Is.EqualTo("SampleMissing"));
            Assert.That(AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, new Vector3(float.NaN, 0, 0), buffer).Failure,
                Is.EqualTo("SampleInvalid"));
            Assert.That(buffer.CalculationCount, Is.Zero, "Invalid samples must never invoke a path calculation.");
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator SlopeDistanceIncludesHeightAndArrivalUsesGroundWithBaseOffset()
        {
            Vector3 target = TestNavMeshBuilder.Ramp(World);
            var result = AgentNavigationSegmentQuery.Calculate(Profile(), Vector3.zero, target, new AgentNavigationSegmentQuery.Buffer());
            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.Length, Is.GreaterThan(Vector2.Distance(new Vector2(result.Origin.x, result.Origin.z),
                new Vector2(result.Destination.x, result.Destination.z)) + 0.02f));
            var pawn = AgentFactory.Create(World, "Offset", target);
            pawn.enabled = false;
            yield return null;
            var live = AgentNavigationSegmentQuery.Calculate(pawn.NavMeshAgent, target, new AgentNavigationSegmentQuery.Buffer());
            Assert.That(live.IsComplete, Is.True);
            Assert.That(pawn.NavMeshAgent.baseOffset, Is.GreaterThan(0));
            Assert.That(AgentNavigationQuery.Check(pawn.NavMeshAgent, live.Origin, 0).Status, Is.EqualTo(AgentNavigationStatus.Arrived));
            pawn.NavMeshAgent.enabled = false;
            Assert.That(AgentNavigationQuery.Check(pawn.NavMeshAgent, target, 0).Status, Is.EqualTo(AgentNavigationStatus.NotReady));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator BuffersAndResultValuesDoNotAliasOtherQueries()
        {
            TestNavMeshBuilder.Flat(World);
            var first = new AgentNavigationSegmentQuery.Buffer();
            var second = new AgentNavigationSegmentQuery.Buffer();
            var profile = Profile();
            var ten = AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 10, first);
            var three = AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 3, second);
            Assert.That(ten.Length, Is.EqualTo(10).Within(0.05f));
            Assert.That(three.Length, Is.EqualTo(3).Within(0.05f));
            AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 1000, second);
            Assert.That(first.LastFailure, Is.Null);
            var zero = AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.zero, first);
            Assert.That(zero.IsComplete, Is.True);
            Assert.That(zero.Length, Is.EqualTo(0).Within(0.01f));
            Assert.That(ten.Length, Is.EqualTo(10).Within(0.05f));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ProfileCopiesCostsAndRejectsInvalidInputs()
        {
            var costs = new float[32]; for (int i = 0; i < 32; i++) costs[i] = 1;
            costs[3] = 7;
            var profile = new AgentNavigationProfile(0, -1, 1, 0.5f, costs);
            costs[3] = 99;
            Assert.That(profile.GetAreaCost(3), Is.EqualTo(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AgentNavigationProfile(0, -1, float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AgentNavigationProfile(0, -1, 1, -1));
            Assert.Throws<ArgumentException>(() => new AgentNavigationProfile(0, -1, 1, 1, new float[1]));
            costs[3] = float.PositiveInfinity;
            Assert.Throws<ArgumentOutOfRangeException>(() => new AgentNavigationProfile(0, -1, 1, 1, costs));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ReusedSegmentQueriesAllocateNothingAfterWarmup()
        {
            TestNavMeshBuilder.Flat(World);
            var profile = Profile();
            var buffer = new AgentNavigationSegmentQuery.Buffer();
            for (int i = 0; i < 10; i++) AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 10, buffer);
            using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1024,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 100; i++) AgentNavigationSegmentQuery.Calculate(profile, Vector3.zero, Vector3.right * 10, buffer);
            allocations.Stop();
            Assert.That(allocations.Valid, Is.True);
            Assert.That(allocations.Count, Is.Zero);
            Assert.That(buffer.CalculationCount, Is.EqualTo(110));
            ContractCompleted = true;
            yield break;
        }

        private void BuildCostArea()
        {
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(40, 0.2f, 30),
                    transform = Matrix4x4.TRS(Vector3.down * 0.1f, Quaternion.identity, Vector3.one), area = 0 },
                new NavMeshBuildSource { shape = NavMeshBuildSourceShape.ModifierBox, size = new Vector3(10, 4, 10),
                    transform = Matrix4x4.identity, area = 3 }
            };
            var data = World.Own(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                new Bounds(Vector3.zero, new Vector3(45, 10, 35)), Vector3.zero, Quaternion.identity));
            Assert.That(data, Is.Not.Null);
            NavMesh.AddNavMeshData(data);
        }
    }
}

using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Targets.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class TargetApproachOccupancyTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator ScaledBodyOccupancyTracksMovementAndHeight()
        {
            TestNavMeshBuilder.Flat(World);
            var requester = Actor("requester", new Vector3(-12, 0, 0));
            var other = Actor("other", Vector3.zero);
            yield return null;
            Physics.SyncTransforms();
            var query = new GameplayTargetApproachOccupancy();
            Assert.That(query.IsClear(requester, new Vector3(2.9f, 0, 0)), Is.False, "Scaled bodies need their full diameter.");
            Assert.That(query.IsClear(requester, new Vector3(3.5f, 0, 0)), Is.True);
            Assert.That(query.IsClear(requester, new Vector3(0, 12, 0)), Is.True, "A different floor must not be rejected by planar distance alone.");
            Assert.That(other.Warp(new Vector3(10, 0, 0)), Is.True);
            Physics.SyncTransforms();
            Assert.That(query.IsClear(requester, Vector3.zero), Is.True, "Occupancy is live, not permanently cached.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator OwnBodyStaticGeometryAndDisabledNavigationDoNotOccupy()
        {
            TestNavMeshBuilder.Flat(World);
            var requester = Actor("requester", Vector3.zero);
            var staticBody = World.Root("ordinary geometry").AddComponent<BoxCollider>();
            staticBody.size = Vector3.one * 2f;
            var query = new GameplayTargetApproachOccupancy();
            yield return null;
            Physics.SyncTransforms();
            Assert.That(query.IsClear(requester, Vector3.zero), Is.True);
            var other = Actor("disabled navigation", new Vector3(1, 0, 0));
            other.enabled = false;
            Physics.SyncTransforms();
            Assert.That(query.IsClear(requester, Vector3.zero), Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator FullOverlapBufferExpandsAndThenReusesStorage()
        {
            TestNavMeshBuilder.Flat(World);
            var requester = Actor("requester", new Vector3(-12, 0, 0));
            for (int i = 0; i < 40; i++)
            {
                var body = World.Root("static overlap " + i).AddComponent<BoxCollider>();
                body.transform.position = new Vector3(0, 2, 0);
            }
            var occupant = Actor("occupant", Vector3.zero);
            var query = new GameplayTargetApproachOccupancy();
            yield return null;
            Physics.SyncTransforms();
            Assert.That(query.IsClear(requester, Vector3.zero), Is.False);
            var buffer = RuntimeFixtureAccess.Read<Collider[]>(query, "_overlaps");
            Assert.That(buffer.Length, Is.GreaterThan(40));
            for (int i = 0; i < 20; i++) Assert.That(query.IsClear(requester, Vector3.zero), Is.False);
            Assert.That(RuntimeFixtureAccess.Read<Collider[]>(query, "_overlaps"), Is.SameAs(buffer));
            occupant.gameObject.SetActive(false);
            Physics.SyncTransforms();
            Assert.That(query.IsClear(requester, Vector3.zero), Is.True);
            ContractCompleted = true;
        }

        private NavMeshAgent Actor(string name, Vector3 position)
        {
            var root = World.Root(name);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * 3f;
            var body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.5f; body.height = 2f; body.center = Vector3.up;
            var nav = root.AddComponent<NavMeshAgent>();
            nav.radius = 0.5f; nav.height = 2f;
            Assert.That(nav.isOnNavMesh, Is.True);
            return nav;
        }
    }
}

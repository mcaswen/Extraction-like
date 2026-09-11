using System.Collections;
using System.Linq;
using System.Text;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using NUnit.Framework;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Targets.Authoring;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidResourceNavigationTests : ReproductionTestFixture
    {
        public static float[] Offsets = { 0f, 1f, 3f, 6f };

        [TearDown]
        public void StopActorsBeforeNavigationCleanup()
        {
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator RecordedLaboratoryApproachUsesExecutablePath()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            yield return null;
            yield return null;
            yield return null;
            var pawn = Object.FindObjectsOfType<AgentPawnRoot>().Single(x => x.AgentIdValue == "2");
            foreach (var other in Object.FindObjectsOfType<AgentPawnRoot>())
                if (other != pawn) other.gameObject.SetActive(false);
            foreach (var enemy in Object.FindObjectsOfType<EnemyHealthController>()) enemy.gameObject.SetActive(false);
            pawn.enabled = false;
            var nav = pawn.NavMeshAgent;
            Vector3 start = new Vector3(-430.34558f, 3.00834f, 197.98416f);
            Vector3 target = new Vector3(-370.77042f, 0.00834f, 197.92053f);
            Assert.That(nav.Warp(start - Vector3.up * nav.baseOffset * pawn.transform.lossyScale.y), Is.True);
            nav.nextPosition = start;
            Time.timeScale = 4;
            var motor = new AgentNavigationMotor(nav, 2, 3);
            double deadline = Time.timeAsDouble + 30;
            bool arrived = false;
            while (Time.timeAsDouble < deadline)
            {
                var query = AgentNavigationQuery.Check(nav, target, 0);
                var result = motor.Move("recorded-laboratory", target, 0, 12);
                CaseArtifactWriter.Trace("laboratory-path", "position=" + pawn.Position + "; query=" + query.Status +
                    "; move=" + result.Status + "; hasPath=" + nav.hasPath + "; path=" + nav.pathStatus);
                Assert.That(result.Failed, Is.False, "A recorded reachable target must not be rejected while following its path.");
                if (result.Status == AgentNavigationStatus.Arrived) { arrived = true; break; }
                yield return null;
            }
            Assert.That(arrived, Is.True, "The recorded laboratory path must actually reach its endpoint.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RecordedScenePositionStillResolvesAReachableResource()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            yield return null;
            yield return null;
            yield return null;
            var pawn = Object.FindObjectsOfType<AgentPawnRoot>().Single(x => x.AgentIdValue == "1");
            var cluster = Object.FindObjectsOfType<ResourceClusterAuthoring>().Single(x =>
                x.name == "ResourceCluster_B" && x.transform.parent.name == "Zone-员工食堂");
            var nav = pawn.NavMeshAgent;
            Vector3 recorded = new Vector3(217.9441528f, 3.3416824f, -37.3266182f);
            Assert.That(nav.Warp(recorded - Vector3.up * nav.baseOffset * pawn.transform.lossyScale.y), Is.True);
            // Warp expects the simulation position; restore the observed height without changing the bound polygon.
            nav.nextPosition = recorded;
            // 日志中队友已经取空第 3、4 个箱子，此时需要走向较远的剩余成员。
            cluster.MarkResourceCompleted(cluster.ResourceMembers[2].EntityObject);
            cluster.MarkResourceCompleted(cluster.ResourceMembers[3].EntityObject);
            var path = new NavMeshPath();
            Vector3 target = new Vector3(173.730957f, 0.0083389f, -79.435486f);
            Assert.That(nav.CalculatePath(target, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            var details = new StringBuilder("next=" + nav.nextPosition + "; boundCorner=" + path.corners[0] + "\n");
            cluster.AppendNavigationDebugSnapshot(details, recorded, nav);
            CaseArtifactWriter.Trace("recorded-scene-origin", details.ToString());
            Assert.That(cluster.TryGetNearestReachableIncompleteResource(recorded, nav, out _, out _), Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator VisualBaseOffsetDoesNotInvalidateReachableResource([ValueSource(nameof(Offsets))] float offset)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "Offset", Vector3.zero);
            agent.NavMeshAgent.baseOffset = offset;
            var cluster = TargetFactory.Resources(World, new Vector3(12, 0, 0));
            yield return null;
            var direct = new NavMeshPath();
            Assert.That(agent.NavMeshAgent.CalculatePath(new Vector3(12, 0, 0), direct), Is.True);
            Assert.That(direct.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            CaseArtifactWriter.Trace("origin", "offset=" + offset + "; next=" + agent.NavMeshAgent.nextPosition + "; corner=" + direct.corners[0]);
            Assert.That(cluster.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _, out _), Is.True,
                "Resource selection must use the same bound navigation origin as the actual agent.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator VisualOriginNearDisconnectedUpperFloorUsesBoundLowerFloor()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(80, 0.2f, 80)),
                new Bounds(new Vector3(0, 2.9f, 0), new Vector3(6, 0.2f, 6)));
            var agent = AgentFactory.Create(World, "Stacked floors", Vector3.zero);
            agent.NavMeshAgent.baseOffset = 3f;
            var cluster = TargetFactory.Resources(World, new Vector3(12, 0, 0));
            yield return null;
            var direct = new NavMeshPath();
            Assert.That(agent.NavMeshAgent.CalculatePath(new Vector3(12, 0, 0), direct), Is.True);
            Assert.That(direct.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(direct.corners[0].y, Is.LessThan(1f), "Agent is bound to the lower floor.");
            Assert.That(agent.NavMeshAgent.nextPosition.y, Is.GreaterThan(2f));
            CaseArtifactWriter.Trace("origin", "next=" + agent.NavMeshAgent.nextPosition + "; corner=" + direct.corners[0]);
            Assert.That(cluster.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _, out _), Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator ResourceOnDisconnectedIslandStillRemainsUnreachable()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12)),
                new Bounds(new Vector3(30, -0.1f, 0), new Vector3(12, 0.2f, 12)));
            var agent = AgentFactory.Create(World, "Disconnected", Vector3.zero);
            agent.NavMeshAgent.baseOffset = 3f;
            var cluster = TargetFactory.Resources(World, new Vector3(30, 0, 0));
            yield return null;
            var direct = new NavMeshPath();
            agent.NavMeshAgent.CalculatePath(new Vector3(30, 0, 0), direct);
            Assert.That(direct.status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(cluster.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _, out _), Is.False);
            ContractCompleted = true;
        }
    }
}

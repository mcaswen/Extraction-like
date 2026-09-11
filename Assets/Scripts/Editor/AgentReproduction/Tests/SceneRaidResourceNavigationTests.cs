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
        public static float[] LaboratoryStartX = { -430.34558f, -444f };
        public static int[] LaboratoryOrigins = { 0, 1, 2 };
        public static float[] LaboratoryCaptureSteps = { 0, 0.008f, 0.01f, 0.0125f, 0.01666667f };
        public static int[] RecoveryInterventions = { 1, 2 };

        [TearDown]
        public void StopActorsBeforeNavigationCleanup()
        {
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator RecordedLaboratoryApproachUsesExecutablePath([ValueSource(nameof(LaboratoryStartX))] float startX)
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
            Vector3 start = new Vector3(startX, 3.00834f, 197.98416f);
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
                    "; move=" + result.Status + "; hasPath=" + nav.hasPath + "; link=" + nav.isOnOffMeshLink + "; path=" + nav.pathStatus);
                Assert.That(result.Failed, Is.False, "A recorded reachable target must not be rejected while following its path.");
                if (result.Status == AgentNavigationStatus.Arrived) { arrived = true; break; }
                yield return null;
            }
            Assert.That(arrived, Is.True, "The recorded laboratory path must actually reach its endpoint.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator RecordedLaboratoryResourceResolvesWhileMoving(
            [ValueSource(nameof(LaboratoryOrigins))] int origin, [ValueSource(nameof(LaboratoryCaptureSteps))] float captureStep)
        {
            yield return RunLaboratoryMovement(origin, captureStep, 0);
        }

        [UnityTest]
        public IEnumerator LaboratoryRecoveryHandlesPauseAndReplacement([ValueSource(nameof(RecoveryInterventions))] int intervention)
        {
            yield return RunLaboratoryMovement(1, 0.01666667f, intervention);
        }

        private IEnumerator RunLaboratoryMovement(int origin, float captureStep, int intervention)
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
            var cluster = Object.FindObjectsOfType<ResourceClusterAuthoring>().Single(x => x.name == "ResourceCluster_B" && x.transform.parent.name == "Zone-实验室");
            var member = cluster.ResourceMembers.Single(x => x.EntityObject.transform.GetSiblingIndex() == 2);
            foreach (var other in cluster.ResourceMembers)
                if (other != member) cluster.MarkResourceCompleted(other.EntityObject);
            var nav = pawn.NavMeshAgent;
            Vector3 start = origin == 1 ? new Vector3(-423.68872f, 3.00834f, 214.31749f)
                : new Vector3(origin == 2 ? -430.492157f : -444f, 3.00834f, 197.98416f);
            Assert.That(nav.Warp(start - Vector3.up * nav.baseOffset * pawn.transform.lossyScale.y), Is.True);
            nav.nextPosition = start;
            Time.timeScale = 4;
            float previousCaptureStep = Time.captureDeltaTime;
            Time.captureDeltaTime = captureStep;
            try
            {
                var resolver = new AgentResourceNavigationResolver();
                var motor = new AgentNavigationMotor(nav, 2, 3);
                double deadline = Time.timeAsDouble + 30;
                bool arrived = false;
                int retries = 0;
                bool intervened = false;
                string command = "laboratory-resource";
                Vector3 replacement = new Vector3(-444, 0.00834f, 197.98416f);
                Assert.That(NavMesh.SamplePosition(replacement, out var replacementHit, 1,
                    new NavMeshQueryFilter { agentTypeID = nav.agentTypeID, areaMask = nav.areaMask }), Is.True);
                replacement = replacementHit.position;
                while (Time.timeAsDouble < deadline)
                {
                    Assert.That(resolver.TryResolve(cluster, pawn.Position, nav, out var resource, out var target), Is.True);
                    Assert.That(resource, Is.EqualTo(member.EntityObject));
                    if (command == "replacement") target = replacement;
                    var result = motor.Move(command, target, 0, 12);
                    if (result.Status == AgentNavigationStatus.NotReady) retries++;
                    CaseArtifactWriter.Trace("laboratory-resource", "position=" + pawn.Position + "; target=" + target + "; move=" + result.Status + "; dt=" + Time.deltaTime);
                    Assert.That(result.Failed, Is.False);
                    if (!intervened && intervention != 0 && result.Status == AgentNavigationStatus.NotReady)
                    {
                        intervened = true;
                        if (intervention == 1)
                        {
                            Vector3 pausedPosition = pawn.Position;
                            double pausedTime = Time.timeAsDouble;
                            Time.timeScale = 0;
                            for (int frame = 0; frame < 10; frame++)
                            {
                                yield return null;
                                Assert.That(motor.Move(command, target, 0, 12).Status, Is.EqualTo(AgentNavigationStatus.NotReady));
                                Assert.That(Vector3.Distance(pausedPosition, pawn.Position), Is.LessThan(0.001f));
                                Assert.That(Time.timeAsDouble, Is.EqualTo(pausedTime));
                            }
                            Time.timeScale = 4;
                        }
                        else
                        {
                            command = "replacement";
                            Assert.That(motor.Move(command, replacement, 0, 12).Failed, Is.False);
                            Assert.That(Vector3.Distance(nav.destination, replacement), Is.LessThan(0.05f));
                        }
                    }
                    if (result.Status == AgentNavigationStatus.Arrived) { arrived = true; break; }
                    yield return null;
                }
                Assert.That(arrived, Is.True);
                if (intervention != 0) Assert.That(intervened, Is.True, "The real native rejection must occur before testing the intervention.");
                CaseArtifactWriter.Trace("assignment-recovery", "retries=" + retries + "; arrived=" + arrived);
                ContractCompleted = true;
            }
            finally { Time.captureDeltaTime = previousCaptureStep; }
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

using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidResourceApproachTests : ReproductionTestFixture
    {
        public static float[] Speeds = { 1f, 4f };
        public static string[] Actors = { "1", "2", "Both" };
        private AgentPawnRoot[] _pawns;

        [TearDown]
        public void StopActorsBeforeNavigationCleanup()
        {
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator RecordedBoxApproachesArriveWithoutStalling(
            [ValueSource(nameof(Actors))] string actors, [ValueSource(nameof(Speeds))] float speed)
        {
            yield return LoadSceneAndIsolateMovement();
            var pawns = _pawns;
            Vector3[] positions = {
                new Vector3(243.5979614f, 3.0100021f, -51.7829589f),
                new Vector3(237.9779358f, 3.0083389f, -53.8705635f)
            };
            Vector3[] targets = {
                new Vector3(242.6446533f, 0.0083389f, -52.6825180f),
                new Vector3(238.2513733f, 0.0083389f, -53.7690048f)
            };
            var motors = new AgentNavigationMotor[2];
            var statuses = new[] { AgentNavigationStatus.Moving, AgentNavigationStatus.Moving };
            for (int i = 0; i < pawns.Length; i++)
            {
                var pawn = pawns[i];
                if (actors != "Both" && actors != pawn.AgentIdValue)
                { pawn.gameObject.SetActive(false); statuses[i] = AgentNavigationStatus.Arrived; continue; }
                var nav = pawn.NavMeshAgent;
                Assert.That(nav.Warp(positions[i] - Vector3.up * nav.baseOffset * pawn.transform.lossyScale.y), Is.True);
                nav.nextPosition = positions[i];
                nav.ResetPath();
                nav.velocity = Vector3.zero;
                motors[i] = new AgentNavigationMotor(nav, 2f, 3f);
                CaseArtifactWriter.Trace("configuration", pawn.AgentIdValue + "; radius=" + nav.radius +
                    "; scale=" + pawn.transform.lossyScale + "; position=" + nav.nextPosition + "; target=" + targets[i]);
            }
            Time.timeScale = speed;
            float deadline = Time.time + 6f;
            float wallDeadline = Time.realtimeSinceStartup + 12f;
            while (Time.time < deadline && Time.realtimeSinceStartup < wallDeadline && statuses.Any(x => x == AgentNavigationStatus.Moving))
            {
                for (int i = 0; i < motors.Length; i++)
                    if (motors[i] != null && statuses[i] == AgentNavigationStatus.Moving)
                        statuses[i] = motors[i].Move("box-approach-" + i, targets[i], 0f, i == 0 ? 8f : 12f).Status;
                yield return null;
            }
            for (int i = 0; i < motors.Length; i++)
            {
                if (motors[i] == null) continue;
                CaseArtifactWriter.Trace("arrival", pawns[i].AgentIdValue + "; status=" + statuses[i] +
                    "; position=" + pawns[i].NavMeshAgent.nextPosition + "; velocity=" + pawns[i].NavMeshAgent.velocity);
            }
            Assert.That(statuses, Is.All.EqualTo(AgentNavigationStatus.Arrived), "Recorded reachable approaches must actually arrive.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator TwoAgentsChooseReachableUnoccupiedApproaches([ValueSource(nameof(Speeds))] float speed)
        {
            yield return LoadSceneAndIsolateMovement();
            Vector3 boxPosition = new Vector3(255.0428314f, 2.9549866f, -160.1343384f);
            var cluster = Object.FindObjectsOfType<ResourceClusterAuthoring>().Single(c => c.ResourceMembers.Any(m =>
                m?.EntityObject != null && (m.EntityObject.transform.position - boxPosition).sqrMagnitude < 0.01f));
            var box = cluster.ResourceMembers.Single(m => m?.EntityObject != null &&
                (m.EntityObject.transform.position - boxPosition).sqrMagnitude < 0.01f).EntityObject;
            Vector3[] positions = {
                new Vector3(256.0929565f, 3.0083389f, -153.2946167f),
                new Vector3(259.006897f, 3.0083389f, -154.0072784f)
            };
            var motors = new AgentNavigationMotor[2];
            var resolvers = new[] { new AgentResourceNavigationResolver(), new AgentResourceNavigationResolver() };
            var statuses = new[] { AgentNavigationStatus.Moving, AgentNavigationStatus.Moving };
            var destinations = new Vector3[2];
            for (int i = 0; i < _pawns.Length; i++)
            {
                var nav = _pawns[i].NavMeshAgent;
                Assert.That(nav.Warp(positions[i] - Vector3.up * nav.baseOffset * _pawns[i].transform.lossyScale.y), Is.True);
                nav.nextPosition = positions[i]; nav.ResetPath(); nav.velocity = Vector3.zero;
                motors[i] = new AgentNavigationMotor(nav, 2f, 3f);
            }
            Physics.SyncTransforms();
            Time.timeScale = speed;
            float deadline = Time.time + 12f, wallDeadline = Time.realtimeSinceStartup + 18f;
            float stableSince = -1f;
            while (Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
            {
                for (int i = 0; i < _pawns.Length; i++)
                {
                    Assert.That(resolvers[i].TryResolve(cluster, _pawns[i].Position, _pawns[i].NavMeshAgent,
                        out var resource, out destinations[i]), Is.True);
                    Assert.That(resource, Is.SameAs(box), "The fixture must reach the recorded box, not escape to another member.");
                    statuses[i] = motors[i].Move("shared-box-" + i, destinations[i], 0f, i == 0 ? 8f : 12f).Status;
                }
                yield return null;
                if (statuses.Any(s => s != AgentNavigationStatus.Moving && s != AgentNavigationStatus.Arrived)) break;
                bool bothArrived = statuses.All(s => s == AgentNavigationStatus.Arrived);
                for (int i = 0; i < _pawns.Length && bothArrived; i++)
                    bothArrived &= AgentNavigationQuery.Check(_pawns[i].NavMeshAgent, destinations[i], 0f).Status == AgentNavigationStatus.Arrived;
                stableSince = bothArrived ? (stableSince < 0f ? Time.time : stableSince) : -1f;
                if (stableSince >= 0f && Time.time - stableSince >= 0.25f) break;
            }
            for (int i = 0; i < _pawns.Length; i++)
                CaseArtifactWriter.Trace("shared-box-arrival", _pawns[i].AgentIdValue + "; status=" + statuses[i] +
                    "; position=" + _pawns[i].Position + "; destination=" + destinations[i]);
            Assert.That(statuses, Is.All.EqualTo(AgentNavigationStatus.Arrived));
            Assert.That(stableSince, Is.GreaterThanOrEqualTo(0f));
            Assert.That(Time.time - stableSince, Is.GreaterThanOrEqualTo(0.25f), "Both arrivals must stay valid while the action continues ticking.");
            for (int i = 0; i < _pawns.Length; i++)
                Assert.That(AgentNavigationQuery.Check(_pawns[i].NavMeshAgent, destinations[i], 0f).Status,
                    Is.EqualTo(AgentNavigationStatus.Arrived), "The first arrival must remain valid when the second agent stops.");
            Assert.That(Vector3.Distance(_pawns[0].Position, _pawns[1].Position), Is.GreaterThan(2.8f));
            ContractCompleted = true;
        }

        private IEnumerator LoadSceneAndIsolateMovement()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            foreach (var discovery in Object.FindObjectsOfType<AgentTargetDiscoveryController>()) discovery.enabled = false;
            _pawns = Object.FindObjectsOfType<AgentPawnRoot>().OrderBy(x => x.AgentIdValue).ToArray();
            Assert.That(_pawns.Length, Is.EqualTo(2));
            foreach (var pawn in _pawns) pawn.enabled = false;
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>())
                if (!_pawns.Any(p => p.NavMeshAgent == nav)) nav.gameObject.SetActive(false);
        }
    }
}

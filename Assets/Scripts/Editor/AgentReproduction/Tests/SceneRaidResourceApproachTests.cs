using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Runtime;
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

        [TearDown]
        public void StopActorsBeforeNavigationCleanup()
        {
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>()) nav.gameObject.SetActive(false);
        }

        [UnityTest]
        public IEnumerator RecordedBoxApproachesArriveWithoutStalling(
            [ValueSource(nameof(Actors))] string actors, [ValueSource(nameof(Speeds))] float speed)
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
            foreach (var discovery in Object.FindObjectsOfType<AgentTargetDiscoveryController>()) discovery.enabled = false;
            var pawns = Object.FindObjectsOfType<AgentPawnRoot>().OrderBy(x => x.AgentIdValue).ToArray();
            Assert.That(pawns.Length, Is.EqualTo(2));
            // Disable gameplay decisions in this movement fixture; NavMesh simulation remains real.
            foreach (var pawn in pawns) pawn.enabled = false;
            foreach (var nav in Object.FindObjectsOfType<NavMeshAgent>())
                if (!pawns.Any(p => p.NavMeshAgent == nav)) nav.gameObject.SetActive(false);
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
            while (Time.time < deadline && statuses.Any(x => x == AgentNavigationStatus.Moving))
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
    }
}

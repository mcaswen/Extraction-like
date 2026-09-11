using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidEnemyPrefabConfigurationTests : ReproductionTestFixture
    {
        private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";

        [TearDown]
        public void StopSceneActorsBeforeFixtureRemovesNavigation()
        {
            // The generic micro-fixture removes NavMesh data before leaving Play Mode.
            // Stop this full scene's actors first so they cannot tick against that removed mesh.
            foreach (var agent in Object.FindObjectsOfType<UnityEngine.AI.NavMeshAgent>())
                agent.gameObject.SetActive(false);
        }

        private static IEnumerator LoadScene()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone) yield return null;
            // Nested spawners in the red fixture start one frame after their parents.
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneEnemySlotsResolveToPawnsInsteadOfSpawnPoints()
        {
            yield return LoadScene();
            var invalid = new List<string>();
            int slots = 0;
            foreach (var source in Object.FindObjectsOfType<EnemySourceClusterAuthoring>())
                foreach (var point in source.SpawnPoints)
                {
                    if (point == null || !point.gameObject.activeInHierarchy) continue;
                    slots++;
                    var spawn = point.GetComponent<EnemySpawnPoint>();
                    GameObject prefab = source.TryResolveEnemyPrefabForSpawnPoint(point, out var configured) ? configured : spawn?.EnemyPrefab;
                    if (prefab == null || prefab.GetComponentInChildren<EnemyHealthController>(true) == null ||
                        prefab.GetComponentInChildren<EnemySpawnPoint>(true) != null)
                        invalid.Add(SceneRaidIdentityMap.HierarchyPath(point) + " => " + AssetDatabase.GetAssetPath(prefab));
                }
            CaseArtifactWriter.Trace("enemy-prefab-slots", "slots=" + slots + "; invalid=" + invalid.Count + "\n" + string.Join("\n", invalid));
            Assert.That(slots, Is.EqualTo(30), "The original scene's authored enemy count must stay intact.");
            Assert.That(invalid, Is.Empty, "Enemy slots must reference actual pawns, not another spawner.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator EverySpawnedSceneEnemyBelongsToItsSourceCluster()
        {
            yield return LoadScene();
            var registry = GameplayTargetRegistry.ActiveInstance;
            var enemies = Object.FindObjectsOfType<EnemyHealthController>().Where(enemy => enemy.IsAlive).ToArray();
            var unregistered = new List<string>();
            foreach (var enemy in enemies)
                if (registry == null || !registry.TryFindEnemyClusterByEnemy(enemy, out _) ||
                    !registry.TryFindEnemySourceClusterByEnemy(enemy, out _))
                    unregistered.Add(SceneRaidIdentityMap.HierarchyPath(enemy.transform));
            int spawnPoints = Object.FindObjectsOfType<EnemySpawnPoint>().Length;
            CaseArtifactWriter.Trace("enemy-registration", "spawnPoints=" + spawnPoints + "; enemies=" + enemies.Length +
                "; unregistered=" + unregistered.Count + "\n" + string.Join("\n", unregistered));
            Assert.That(enemies.Length, Is.EqualTo(30));
            Assert.That(spawnPoints, Is.EqualTo(30), "No extra detached spawners may be created by the authored scene.");
            Assert.That(unregistered, Is.Empty);
            ContractCompleted = true;
        }
    }
}

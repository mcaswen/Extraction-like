using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.MapGraph.Binding;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AgentReproduction.Tests
{
    /// <summary>实际发布场景中的定点交战反例，只改变测试运行态，不保存场景。</summary>
    public sealed class MapCommandEnemyProcessingTests : ReproductionTestFixture
    {
        private const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        private const string GroupPath = "Zone-龙骨礁/EnemySourceCluster_A";
        private const string RangePath = "Zone-龙骨礁/tripo_convert_cfc50981-c500-4787-94ae-f41149c942c9/Torus/Range";
        [Serializable] private sealed class Row { public string enemy, enabledFailure, disabledFailure; public Vector3 aim; }
        [Serializable] private sealed class Evidence
        { public bool originallyEnabled, enabledResolves, disabledResolves; public Vector3 pawn, boxCenter, boxSize; public Row[] candidates; }

        [UnityTest] public IEnumerator FormalDragonboneRangeCannotBlockEnemyProcessing()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return RuntimeWait.Until(() => load.isDone, "龙骨礁交战场景载入", 60);
            var source = GameObject.Find(GroupPath).GetComponent<EnemySourceClusterAuthoring>();
            yield return RuntimeWait.Until(() => source.ConfiguredActiveEnemyCluster != null && source.ConfiguredActiveEnemyCluster.CountLivingEnemies() == 2,
                "实际来源群的两个敌人出生", 20);
            Time.timeScale = 0;
            var pawn = Object.FindObjectsOfType<AgentPawnRoot>().Single(x => x.AgentIdValue == "1");
            var binding = Object.FindObjectOfType<MapGraphBindingAuthoring>();
            string node = binding.TargetBindings.Single(x => x.DirectTarget == source).NodeId;
            Assert.That(binding.TryGetNavigationAnchor(node, out var anchor), Is.True);
            float bodyOffset = pawn.NavMeshAgent.baseOffset * Mathf.Abs(pawn.transform.lossyScale.y);
            Assert.That(pawn.NavMeshAgent.Warp(anchor + Vector3.up * bodyOffset), Is.True);
            Assert.That(pawn.Position.y - anchor.y, Is.EqualTo(bodyOffset).Within(.01f));
            Physics.SyncTransforms();
            var box = GameObject.Find(RangePath).GetComponent<BoxCollider>();
            Assert.That(box.GetComponents<Component>().Length, Is.EqualTo(2), "只修正无渲染/行为的范围辅助对象");
            Assert.That(box.transform.parent.GetComponent<MeshCollider>().enabled, Is.True, "真实 Torus 碰撞保持");
            var enemies = new List<global::EnemyHealthController>(); source.ConfiguredActiveEnemyCluster.CopyAliveEnemiesTo(enemies);
            var resolver = new MapGraphRouteTargetResolver(binding);
            var evidence = new Evidence { originallyEnabled = box.enabled, pawn = pawn.Position,
                candidates = enemies.Select(x => new Row { enemy = x.name, aim = CombatAimPointResolver.Resolve(x.transform) }).ToArray() };
            try
            {
                box.enabled = true; Physics.SyncTransforms();
                evidence.boxCenter = box.bounds.center; evidence.boxSize = box.bounds.size;
                evidence.enabledResolves = resolver.TryCreateProcessingDirective(node, pawn.AgentId, "range-enabled", 1000, out _, out _);
                for (int i = 0; i < enemies.Count; i++)
                    evidence.candidates[i].enabledFailure = AgentDirectiveValidationService.Validate(pawn,
                        AgentDirectiveRequest.EngageConcreteEnemy(enemies[i].gameObject, "range-probe", pawn.AgentId, "range-probe", 1000)).ToString();
                box.enabled = false; Physics.SyncTransforms();
                evidence.disabledResolves = resolver.TryCreateProcessingDirective(node, pawn.AgentId, "range-disabled", 1000, out _, out _);
                for (int i = 0; i < enemies.Count; i++)
                    evidence.candidates[i].disabledFailure = AgentDirectiveValidationService.Validate(pawn,
                        AgentDirectiveRequest.EngageConcreteEnemy(enemies[i].gameObject, "range-probe", pawn.AgentId, "range-probe", 1000)).ToString();
                File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath, "dragonbone-processing.json"), JsonUtility.ToJson(evidence, true));
                Assert.That(evidence.enabledResolves, Is.False, "旧碰撞盒应稳定复现拒绝");
                Assert.That(evidence.disabledResolves, Is.True, "只关闭辅助碰撞盒就应恢复正式处理指令");
                Assert.That(evidence.originallyEnabled, Is.False, "保存场景不能重新启用阻挡交战的辅助盒");
            }
            finally { box.enabled = evidence.originallyEnabled; Time.timeScale = 1; }
            ContractCompleted = true;
        }
        [UnityTearDown] public IEnumerator UnloadFormalScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath); if (!scene.IsValid() || !scene.isLoaded) yield break;
            var empty = SceneManager.CreateScene("Enemy processing cleanup"); SceneManager.SetActiveScene(empty);
            var unload = SceneManager.UnloadSceneAsync(scene);
            yield return RuntimeWait.Until(() => unload.isDone, "卸载龙骨礁交战场景", 30);
        }
    }
}

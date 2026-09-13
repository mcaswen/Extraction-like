using System;
using System.Collections;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Raid;
using Gameplay.Perception;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandRuntimeCacheTests:ReproductionTestFixture
    {
        private const string ScenePath="Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        [Serializable]private sealed class Evidence{public string saved,installed,current;public long installedQueries;public MapGraphNavigationProfileData bakedProfile;public Profile[] agents;}
        [Serializable]private sealed class Profile{public string id,reason;public int pending;public MapGraphNavigationProfileData profile;}
        [Serializable]private sealed class SpawnRecord{public string source,enemy;public Vector3 position,aim;public string[] enclosing;}
        [Serializable]private sealed class SpawnEvidence{public SpawnRecord[] spawns;}
        [UnityTest]public IEnumerator FormalStaticSentinelsDoNotEncloseAnotherAimPoint()
        {
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return RuntimeWait.Until(()=>load.isDone,"静止敌人场景载入",60);
            yield return RuntimeWait.Until(()=>Object.FindObjectsOfType<EnemySpawnPoint>().Any(x=>x.SpawnedEnemy!=null),"正式生成",20);
            yield return null;Physics.SyncTransforms();
            var sources=Object.FindObjectsOfType<EnemySpawnPoint>().Where(x=>x.SpawnedEnemy!=null&&x.SpawnedEnemy.GetComponent<AnchorSentinelBehaviorController>()!=null).ToArray();
            var rows=sources.Select(x=>new SpawnRecord{source=Hierarchy(x.transform),enemy=x.SpawnedEnemy.name,position=x.SpawnedEnemy.transform.position,
                aim=CombatAimPointResolver.Resolve(x.SpawnedEnemy.transform),enclosing=sources.Where(y=>y!=x&&y.SpawnedEnemy.GetComponentsInChildren<Collider>()
                    .Any(c=>c.enabled&&!c.isTrigger&&(c.ClosestPoint(CombatAimPointResolver.Resolve(x.SpawnedEnemy.transform))-CombatAimPointResolver.Resolve(x.SpawnedEnemy.transform)).sqrMagnitude<.0001f))
                    .Select(y=>y.SpawnedEnemy.name).ToArray()}).ToArray();
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath,"static-sentinels.json"),JsonUtility.ToJson(new SpawnEvidence{spawns=rows},true));
            Assert.That(rows.Length,Is.GreaterThan(1));Assert.That(rows.All(x=>x.enclosing.Length==0),Is.True,"静止哨兵瞄准点被相邻身体完全包住");ContractCompleted=true;
        }
        private static string Hierarchy(Transform item)=>item.parent==null?item.name:Hierarchy(item.parent)+"/"+item.name;
        [UnityTest]public IEnumerator FormalPlayModeReusesSavedCostsWithTheActualNavigationAndProfiles()
        {
            var load=EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return RuntimeWait.Until(()=>load.isDone,"缓存正式场景载入",60);Time.timeScale=4;
            yield return RuntimeWait.Until(()=>Object.FindObjectOfType<RaidMapCommandInstaller>()?.InstalledAgentCount==2,"正式安装",20);
            var installer=Object.FindObjectOfType<RaidMapCommandInstaller>();var binding=installer.Binding;var definition=binding.MapDefinition;
            string installed=RuntimeFixtureAccess.Read<string>(installer,"_navigationFingerprint");
            var rows=Object.FindObjectsOfType<AgentPawnRoot>().Select(p=>
            {
                var profile=AgentNavigationProfile.FromAgent(p.NavMeshAgent);
                var costs=new MapGraphNavigationCostService(definition,binding,profile,"","",installed);
                return new Profile{id=p.AgentIdValue,pending=costs.PendingEdgeCount,reason=costs.LastInvalidationReason,profile=MapGraphNavigationCostService.CaptureProfile(profile)};
            }).ToArray();
            var record=new Evidence{saved=definition.NavigationBake.RuntimeNavigationFingerprint,installed=installed,
                current=MapGraphNavigationFingerprint.Capture(SceneManager.GetActiveScene()),installedQueries=installer.Environments.CalculationCount,
                bakedProfile=definition.NavigationBake.Profile,agents=rows};
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath,"runtime-cache.json"),JsonUtility.ToJson(record,true));
            Assert.That(rows.All(x=>x.pending==0),Is.True,JsonUtility.ToJson(record));
            Assert.That(installer.Environments.CalculationCount,Is.Zero);ContractCompleted=true;
        }
        [UnityTearDown]public IEnumerator UnloadFormalScene()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)yield break;
            var empty=SceneManager.CreateScene("Cache cleanup");SceneManager.SetActiveScene(empty);var unload=SceneManager.UnloadSceneAsync(scene);
            yield return RuntimeWait.Until(()=>unload.isDone,"卸载缓存场景",30);
        }
    }
}

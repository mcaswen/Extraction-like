using System.Collections;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Infrastructure
{
    public abstract class ReproductionTestFixture
    {
        protected TestWorldBuilder World;
        protected bool ContractCompleted;
        [UnitySetUp]
        public IEnumerator SetUpWorld()
        {
            TestRunContext.Load();
            // UTF restarts the setup enumerator after the domain reload.
            if (!Application.isPlaying)
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Test Audio Listener").AddComponent<AudioListener>();
                yield return new EnterPlayMode(false);
            }
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            Random.InitState(TestRunContext.Load().seed);
            World=new TestWorldBuilder();
            ContractCompleted=false;
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"), "Save isolation is required.");
            Assert.That(Application.productName, Does.StartWith("AgentRepro_"));
            CaseArtifactWriter.Trace("setup", Application.unityVersion + " | " + Application.persistentDataPath);
        }

        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            if (!Application.isPlaying) yield break;
            CaseArtifactWriter.Complete("COMPLETED");
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            NavMesh.RemoveAllNavMeshData();
            World?.Dispose();
            bool completed=ContractCompleted;
            bool passed=TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed;
            yield return new ExitPlayMode();
            if (passed) Assert.That(completed, Is.True, "The scenario did not reach its final contract checkpoint.");
        }
    }
}

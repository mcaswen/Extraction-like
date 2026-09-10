using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Targets.Input;
using Gameplay.Targets.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class CommandFeedbackTests : ReproductionTestFixture
    {
        [UnityTest]
        public IEnumerator FormalHudReportsAcceptedAndRejectedCommandsAndFadesWhilePaused()
        {
            TestNavMeshBuilder.Flat(World);
            var agent=AgentFactory.Create(World,"Feedback",Vector3.zero);
            AgentCommandFeedbackInstaller.EnsureInstalled();
            var presenter=Object.FindObjectOfType<AgentCommandFeedbackPresenter>();
            Assert.That(presenter,Is.Not.Null);
            var exit=TargetFactory.Extraction(World,new Vector3(10,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(exit,agent.AgentIdValue,out _),Is.True);
            Time.timeScale=0f;
            yield return RuntimeWait.Until(()=>presenter.Alpha>0.95f,"success fade in");
            Assert.That(presenter.CurrentText,Is.EqualTo("指令下达成功"));
            Assert.That(presenter.GetComponentInChildren<CanvasGroup>().blocksRaycasts,Is.False);
            yield return RuntimeWait.Until(()=>presenter.Alpha<=0f,"success fade out");
            var invalid=TargetFactory.Extraction(World,new Vector3(200,0,0));
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(invalid,agent.AgentIdValue,out _),Is.False);
            yield return RuntimeWait.Until(()=>presenter.Alpha>0.95f,"failure fade in");
            Assert.That(presenter.CurrentText,Is.EqualTo("指令下达失败：目标不可达"));
            yield return RuntimeWait.Until(()=>presenter.Alpha<=0f,"failure fade out");
            ContractCompleted=true;
        }
    }
}

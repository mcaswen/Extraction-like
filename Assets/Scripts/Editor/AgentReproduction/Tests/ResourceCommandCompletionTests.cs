using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using Core.BehaviorTree.Debugging;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ResourceCommandCompletionTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static string[] Changes = { "Depleted", "Remaining", "Unreachable" };

        [UnityTest]
        public IEnumerator ResourceChangeBetweenLifecycleAndMovementHasCorrectTerminal(
            [ValueSource(nameof(Changes))] string change, [ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var cluster = change == "Remaining"
                ? TargetFactory.Resources(World, new Vector3(30, 0, 0), new Vector3(-30, 0, 0))
                : TargetFactory.Resources(World, new Vector3(30, 0, 0));
            yield return null;
            Time.timeScale = speed;
            var dispatcher = new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(cluster, "1", out var request), Is.True);
            agent.DirectiveLifecycle.Tick();
            Assert.That(cluster.HasBeenCompleted, Is.False);
            var resource = cluster.ResourceMembers[0].EntityObject.GetComponent<WorldLootItem>();
            // 显式构造队友消耗最后资源后、群聚合尚未刷新的一帧边界，不写命令或完成状态。
            if (change == "Unreachable") resource.transform.position = new Vector3(2000, 0, 0);
            else resource.CurrentAmount = 0;
            Assert.That(cluster.HasBeenCompleted, Is.False, "The fixture must retain the stale aggregate seen by lifecycle validation.");
            var results = new List<AgentDirectiveResult>();
            Action<AgentDirectiveResult> observe = results.Add;
            AgentDirectiveFeedbackChannel.Published += observe;
            try
            {
                var node = new MoveToTargetActionNode("Resource completion boundary", AgentDirectiveType.Search,
                    AgentTargetKind.Resource, AgentBlackboardKeys.InteractionDistance, 0);
                var context = new BehaviorTreeContext(agent.Blackboard, new BehaviorTreeDebugTrace(), agent);
                node.Execute(context);
                CaseArtifactWriter.Trace("resource-completion-boundary", change + "; speed=" + speed +
                    "; aggregate=" + cluster.HasBeenCompleted + "; events=" + string.Join(",", results.Select(x => x.Stage + ":" + x.Reason)));
                if (change == "Depleted")
                {
                    Assert.That(cluster.HasBeenCompleted, Is.True);
                    Assert.That(results.Count(x => x.Request.CommandId == request.CommandId && x.Stage == AgentDirectiveStage.Completed), Is.EqualTo(1));
                    Assert.That(results.Any(x => x.Stage == AgentDirectiveStage.Failed), Is.False);
                    Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
                    var next = TargetFactory.Resources(World, new Vector3(-20, 0, 0));
                    Assert.That(dispatcher.TrySubmitClusterCommand(next, "1", out var replacement), Is.True);
                    yield return RuntimeWait.Until(() => agent.Position.x < -1, "new command after exhausted shared resource", 5);
                    Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(replacement.CommandId));
                    Assert.That(results.Count(x => x.Request.CommandId == request.CommandId && x.Stage == AgentDirectiveStage.Completed), Is.EqualTo(1));
                }
                else if (change == "Remaining")
                {
                    Assert.That(cluster.HasBeenCompleted, Is.False);
                    Assert.That(results, Is.Empty);
                    yield return RuntimeWait.Until(() => agent.Position.x < -1, "remaining member actual approach", 5);
                    Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(request.CommandId));
                }
                else
                {
                    Assert.That(cluster.HasBeenCompleted, Is.False);
                    Assert.That(results.Count(x => x.Stage == AgentDirectiveStage.Failed), Is.EqualTo(1));
                    Assert.That(results.Any(x => x.Stage == AgentDirectiveStage.Completed), Is.False);
                    Assert.That(agent.DirectiveLifecycle.Active.HasValue, Is.False);
                }
                node.Abort(context);
                ContractCompleted = true;
            }
            finally { AgentDirectiveFeedbackChannel.Published -= observe; }
        }
    }
}

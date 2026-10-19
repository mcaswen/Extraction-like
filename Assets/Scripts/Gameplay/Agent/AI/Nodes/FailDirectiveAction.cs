using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Commands;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：以指定原因结束当前指令，默认返回成功（本轮到此为止，由生命周期决定后续）
    /// </summary>
    public sealed class FailDirectiveAction : AgentActionNodeBase
    {
        private readonly AgentDirectiveFailure _failure;
        private readonly bool _keepRunning;

        /// <param name="keepRunning">为真时结束指令后返回运行中，等待状态机切换（例如资源群暂时不可达）</param>
        public FailDirectiveAction(string nodeName, string displayName, AgentDirectiveFailure failure, bool keepRunning = false)
            : base(nodeName)
        {
            _failure = failure;
            _keepRunning = keepRunning;
            DisplayName = displayName;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            FailPendingDirective(context, _failure);
            return _keepRunning ? Running() : Succeed();
        }
    }
}

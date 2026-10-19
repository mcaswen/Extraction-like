using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Commands;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：以指定原因结束当前指令并返回成功（本轮交战到此为止，由生命周期决定后续）
    /// </summary>
    public sealed class FailDirectiveAction : AgentActionNodeBase
    {
        private readonly AgentDirectiveFailure _failure;

        public FailDirectiveAction(string nodeName, string displayName, AgentDirectiveFailure failure)
            : base(nodeName)
        {
            _failure = failure;
            DisplayName = displayName;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            FailPendingDirective(context, _failure);
            return Succeed();
        }
    }
}

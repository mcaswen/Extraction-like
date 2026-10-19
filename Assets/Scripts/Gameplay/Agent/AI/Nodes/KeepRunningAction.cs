using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 动作：本帧什么都不做，保持运行中（例如等待攻击冷却）
    /// </summary>
    public sealed class KeepRunningAction : ActionNode
    {
        public KeepRunningAction(string nodeName, string displayName)
            : base(nodeName)
        {
            DisplayName = displayName;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context) => Running();
    }
}

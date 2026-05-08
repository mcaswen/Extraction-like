using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Gameplay.Player.Data;

namespace Gameplay.Player.AI.Actions
{
    /// <summary>
    /// 维持当前宏状态的占位行为树叶子节点
    /// 当前阶段用于让状态绑定的行为树保持 Running，并同步宏状态到 Blackboard
    /// </summary>
    public sealed class MaintainMacroStateActionNode : ActionNode
    {
        private readonly PlayerMacroStateId _macroStateId;

        public MaintainMacroStateActionNode(string nodeName, PlayerMacroStateId macroStateId)
            : base(nodeName)
        {
            _macroStateId = macroStateId;
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            // 当前阶段使用占位节点持续维持 Running
            // 是为了让每个宏状态都真正绑定一棵行为树
            context.Blackboard.SetValue(
                PlayerBlackboardKeys.CurrentMacroStateId,
                _macroStateId,
                context.TimeSeconds);

            context.Blackboard.SetValue(
                PlayerBlackboardKeys.CurrentMacroStateName,
                _macroStateId.ToString(),
                context.TimeSeconds);

            return Running();
        }
    }
}

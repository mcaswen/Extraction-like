using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;
using Core.StateMachine.Runtime;
using Gameplay.Player.Data;

namespace Gameplay.Player.AI.States
{
    /// <summary>
    /// 主角 Brain 的最小具体状态类
    /// 用于承载一个宏状态及其绑定的行为树
    /// </summary>
    public sealed class PlayerBrainState : StateMachineState
    {
        public PlayerBrainState(
            PlayerMacroStateId macroStateId,
            string stateName,
            BehaviorTreeType boundBehaviorTree = null)
            : base(stateName, boundBehaviorTree)
        {
            MacroStateId = macroStateId;
        }

        public PlayerMacroStateId MacroStateId { get; }

        protected override void OnEnter(StateMachineContext context, StateChangeReason reason)
        {
            // 进入状态时同步宏状态 ID，而不是同步裸字符串。
            // 后续 UI、调试面板、行为树观察器都应围绕强类型状态值工作
            context.Blackboard.SetValue(
                PlayerBlackboardKeys.CurrentMacroStateId,
                MacroStateId,
                context.TimeSeconds);

            context.Blackboard.SetValue(
                PlayerBlackboardKeys.CurrentMacroStateName,
                StateName,
                context.TimeSeconds);
        }
    }
}

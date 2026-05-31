using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;
using Core.StateMachine.Runtime;
using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.States
{
    /// <summary>
    /// Agent Brain 的最小具体状态类
    /// 用于承载一个宏状态及其绑定的行为树
    /// </summary>
    public sealed class AgentBrainState : StateMachineState
    {
        /// <summary>
        /// 创建一个可绑定行为树的 Agent Brain 状态
        /// </summary>
        /// <param name="macroStateId"></param>
        /// <param name="stateName"></param>
        /// <param name="boundBehaviorTree"></param>
        public AgentBrainState(
            AgentMacroStateId macroStateId,
            string stateName,
            BehaviorTreeType boundBehaviorTree = null)
            : base(stateName, boundBehaviorTree)
        {
            MacroStateId = macroStateId;
        }

        /// <summary>
        /// 当前状态对应的 Agent 宏状态 ID
        /// </summary>
        public AgentMacroStateId MacroStateId { get; }

        protected override void OnEnter(StateMachineContext context, StateChangeReason reason)
        {
            // 进入状态时同步宏状态 ID，而不是同步裸字符串
            // 后续 UI、调试面板、行为树观察器都应围绕强类型状态值工作
            context.Blackboard.SetValue(
                AgentBlackboardKeys.CurrentMacroStateId,
                MacroStateId,
                context.TimeSeconds);

            context.Blackboard.SetValue(
                AgentBlackboardKeys.CurrentMacroStateName,
                StateName,
                context.TimeSeconds);
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.StateMachine.Runtime;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using UnityEngine;

namespace Gameplay.Agent.Telemetry
{
    public enum AgentBrainTimelineKind
    {
        Transition = 0,
        Directive = 1,
        Selection = 2
    }

    /// <summary>
    /// 面板时间线上的一条：状态转移、指令事件或自主选中目标的变化。只存值和已有引用，写入不分配内存
    /// </summary>
    public struct AgentBrainTimelineEntry
    {
        public AgentBrainTimelineKind Kind;
        public double TimeSeconds;

        // 状态转移
        public StateMachineState From;
        public StateMachineState To;
        public StateTransition Transition;

        // 指令事件
        public AgentDirectiveRequest Directive;
        public AgentDirectiveStage Stage;
        public AgentDirectiveFailure Failure;

        // 自主选中
        public GameObject Target;
        public AgentDecisionTargetKind TargetKind;
        public float Score;
    }
}
#endif

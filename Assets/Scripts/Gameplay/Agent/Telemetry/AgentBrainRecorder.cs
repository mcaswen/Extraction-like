#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.BehaviorTree.Debugging;
using Core.BehaviorTree.Nodes.Decorators;
using Core.StateMachine.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Navigation;
using UnityEngine;

namespace Gameplay.Agent.Telemetry
{
    /// <summary>
    /// 一个智能体的大脑记录器：挂到状态机观察者、行为树事件接收器、决策评分和接近查询上，
    /// 只记录这些系统本来就在做的事。全部缓冲定长复用，记录不分配内存，也不调用任何会改变游戏状态的方法。
    /// </summary>
    public sealed class AgentBrainRecorder : IStateMachineObserver, IAgentDecisionRoundSink
    {
        public const int EvaluationCapacity = 32;
        public const int TransitionCapacity = 64;
        public const int RoundCapacity = 8;
        public const int PathCornerCapacity = 64;
        public const int TimelineCapacity = 64;

        private readonly StateMachineState[] _evaluatedOwners = new StateMachineState[EvaluationCapacity];
        private readonly StateTransition[] _evaluatedTransitions = new StateTransition[EvaluationCapacity];
        private readonly bool[] _evaluatedSatisfied = new bool[EvaluationCapacity];
        private readonly AgentStateTransitionRecord[] _transitions = new AgentStateTransitionRecord[TransitionCapacity];
        private readonly AgentDecisionRoundRecord[] _rounds = new AgentDecisionRoundRecord[RoundCapacity];
        private readonly Vector3[] _pathCorners = new Vector3[PathCornerCapacity];
        private readonly AgentBrainTimelineEntry[] _timeline = new AgentBrainTimelineEntry[TimelineCapacity];
        private readonly System.Action<AgentDirectiveResult> _directiveHandler;
        private int _timelineNext;
        private AgentDirectiveLifecycleController _lifecycle;
        private GameObject _lastChosenTarget;
        private int _transitionNext;
        private int _roundNext;
        private bool _fallbackList;
        private AgentDecisionRoundRecord _openRound;
        private AgentTargetDecisionController _decisionController;

        public AgentBrainRecorder(AgentPawnRoot pawn)
        {
            Pawn = pawn;
            for (int index = 0; index < RoundCapacity; index++) _rounds[index] = new AgentDecisionRoundRecord();
            _directiveHandler = OnDirectiveResult;
        }

        public AgentPawnRoot Pawn { get; }
        public bool IsAttached { get; private set; }
        public StateMachineState RootState { get; private set; }
        public StateMachineState CurrentLeaf { get; private set; }
        public BehaviorNodeEventRing NodeEvents { get; } = new BehaviorNodeEventRing(512);
        public AgentEngageMemory EngageMemory { get; private set; }
        public AgentSearchMemory SearchMemory { get; private set; }
        public AgentCombatShooter Shooter { get; private set; }
        public AgentApproachProbeLog ApproachProbes { get; } = new AgentApproachProbeLog();

        /// <summary>本帧状态机求值过的出边数（只含状态机自己求值的）</summary>
        public int EvaluatedCount { get; private set; }
        public StateMachineState EvaluationStart { get; private set; }
        public int TransitionCount { get; private set; }
        public long TransitionTotal { get; private set; }
        public long RoundTotal { get; private set; }
        public AgentDecisionSkipReason LastSkip { get; private set; }
        public double LastSkipTime { get; private set; } = double.NegativeInfinity;
        public int PathCornerCount { get; private set; }
        public int TimelineCount { get; private set; }
        public long TimelineTotal { get; private set; }

        /// <summary>本帧行为树上下文的推进序号；节点的 LastTickVersion 等于它时表示本帧运行过</summary>
        public int TreeTickVersion { get; private set; }
        public double TreeTimeSeconds { get; private set; }

        /// <summary>评分配置；智能体没有决策模块时为空</summary>
        public AgentDecisionConfig DecisionConfig => _decisionController != null ? _decisionController.TelemetryConfig : null;

        public StateMachineState GetEvaluatedOwner(int index) => _evaluatedOwners[index];
        public StateTransition GetEvaluatedTransition(int index) => _evaluatedTransitions[index];
        public bool GetEvaluatedSatisfied(int index) => _evaluatedSatisfied[index];
        public Vector3 GetPathCorner(int index) => _pathCorners[index];

        /// <summary>按时间顺序取第 index 次转移（0 为仍保留的最早一次）</summary>
        public AgentStateTransitionRecord GetTransition(int index)
        {
            int start = (_transitionNext - TransitionCount + TransitionCapacity) % TransitionCapacity;
            return _transitions[(start + index) % TransitionCapacity];
        }

        /// <summary>时间线上的一条，back = 0 为最新</summary>
        public AgentBrainTimelineEntry GetTimeline(int back)
            => _timeline[((_timelineNext - 1 - back) % TimelineCapacity + TimelineCapacity) % TimelineCapacity];

        /// <summary>最近一轮完成的评分；还没有时为空。back = 0 为最新，1 为上一轮</summary>
        public AgentDecisionRoundRecord GetRound(int back)
        {
            if (back < 0 || back >= RoundCapacity || back >= RoundTotal) return null;
            int index = ((_roundNext - 1 - back) % RoundCapacity + RoundCapacity) % RoundCapacity;
            return _rounds[index];
        }

        internal void Attach()
        {
            AgentBrainController brain = Pawn.BrainController;
            if (brain == null || IsAttached) return;

            RootState = brain.StateMachine.RootState;
            brain.StateMachine.Observer = this;
            brain.TreeContext.NodeEventSink = NodeEvents;
            _decisionController = Pawn.GetComponent<AgentTargetDecisionController>();
            if (_decisionController != null) _decisionController.TelemetrySink = this;
            Shooter = Pawn.GetComponent<AgentCombatShooter>();
            FindTreeMemories(RootState);
            if (EngageMemory != null) EngageMemory.ApproachBuffer.Probes = ApproachProbes;
            _lifecycle = Pawn.DirectiveLifecycle;
            if (_lifecycle != null) _lifecycle.ResultPublished += _directiveHandler;
            IsAttached = true;
        }

        internal void Detach()
        {
            if (!IsAttached) return;
            IsAttached = false;
            if (_lifecycle != null) _lifecycle.ResultPublished -= _directiveHandler;
            _lifecycle = null;
            if (Pawn == null) return;
            AgentBrainController brain = Pawn.BrainController;
            if (brain != null)
            {
                if (ReferenceEquals(brain.StateMachine.Observer, this)) brain.StateMachine.Observer = null;
                if (ReferenceEquals(brain.TreeContext.NodeEventSink, NodeEvents)) brain.TreeContext.NodeEventSink = null;
            }
            if (_decisionController != null && ReferenceEquals(_decisionController.TelemetrySink, this)) _decisionController.TelemetrySink = null;
            if (EngageMemory != null && ReferenceEquals(EngageMemory.ApproachBuffer.Probes, ApproachProbes)) EngageMemory.ApproachBuffer.Probes = null;
        }

        /// <summary>每帧大脑推进之后调用：同步当前叶子和导航路径</summary>
        internal void LateTick()
        {
            if (!IsAttached || Pawn == null) return;
            AgentBrainController brain = Pawn.BrainController;
            CurrentLeaf = brain?.StateMachine.CurrentLeafState;
            if (brain != null)
            {
                TreeTickVersion = brain.TreeContext.TickVersion;
                TreeTimeSeconds = brain.TreeContext.TimeSeconds;
            }
            AgentNavigationMotor motor = Pawn.NavigationMotor;
            PathCornerCount = motor != null ? Mathf.Min(motor.CopyPathCorners(_pathCorners), PathCornerCapacity) : 0;
        }

        // 交战和搜索两棵树的执行记忆由作用域装饰器持有
        private void FindTreeMemories(StateMachineState state)
        {
            if (state == null) return;
            if (state.BoundBehaviorTree != null)
            {
                foreach (var node in state.BoundBehaviorTree.Nodes)
                {
                    if (!(node is ScopeDecorator scope)) continue;
                    if (scope.Scope is AgentEngageMemory engage) EngageMemory = engage;
                    if (scope.Scope is AgentSearchMemory search) SearchMemory = search;
                }
            }

            for (int index = 0; index < state.Children.Count; index++)
                FindTreeMemories(state.Children[index]);
        }

        void IStateMachineObserver.OnTransitionEvaluationStarted(StateMachineState leafState, double timeSeconds)
        {
            EvaluationStart = leafState;
            EvaluatedCount = 0;
        }

        void IStateMachineObserver.OnTransitionEvaluated(StateMachineState ownerState, StateTransition transition, bool satisfied)
        {
            if (EvaluatedCount >= EvaluationCapacity) return;
            _evaluatedOwners[EvaluatedCount] = ownerState;
            _evaluatedTransitions[EvaluatedCount] = transition;
            _evaluatedSatisfied[EvaluatedCount] = satisfied;
            EvaluatedCount++;
        }

        void IStateMachineObserver.OnTransitionPerformed(StateMachineState fromLeaf, StateMachineState toLeaf, StateTransition transition,
            StateChangeReason reason, double timeSeconds)
        {
            _transitions[_transitionNext] = new AgentStateTransitionRecord(fromLeaf, toLeaf, transition, timeSeconds);
            _transitionNext = (_transitionNext + 1) % TransitionCapacity;
            if (TransitionCount < TransitionCapacity) TransitionCount++;
            TransitionTotal++;
            CurrentLeaf = toLeaf;
            AddTimeline(new AgentBrainTimelineEntry
            {
                Kind = AgentBrainTimelineKind.Transition, TimeSeconds = timeSeconds, From = fromLeaf, To = toLeaf, Transition = transition
            });
        }

        void IAgentDecisionRoundSink.BeginRound(double timeSeconds, in AgentDecisionContext context)
        {
            _openRound = _rounds[_roundNext];
            _roundNext = (_roundNext + 1) % RoundCapacity;
            RoundTotal++;
            _openRound.Begin(RoundTotal, timeSeconds, context);
        }

        void IAgentDecisionRoundSink.BeginList(bool fallbackExits)
        {
            _fallbackList = fallbackExits;
            _openRound?.BeginList();
        }

        void IAgentDecisionRoundSink.RecordCandidate(int index, in AgentDecisionCandidate candidate, AgentCandidateVerdict verdict, in AgentScoreBreakdown breakdown)
            => _openRound?.Add(index, candidate, _fallbackList, verdict, breakdown);

        void IAgentDecisionRoundSink.EndList(int bestIndex)
        {
            if (bestIndex < 0 || _openRound == null) return;
            _openRound.MarkChosen(bestIndex);
            if (_openRound.ChosenRow < 0) return;
            AgentDecisionCandidateRow chosen = _openRound.GetRow(_openRound.ChosenRow);
            if (chosen.TargetObject == _lastChosenTarget) return;
            _lastChosenTarget = chosen.TargetObject;
            AddTimeline(new AgentBrainTimelineEntry
            {
                Kind = AgentBrainTimelineKind.Selection, TimeSeconds = _openRound.TimeSeconds, Target = chosen.TargetObject,
                TargetKind = chosen.Kind, Score = chosen.Breakdown.Total
            });
        }

        // 只记录生命周期本来就会发布的结果；被拒绝和规划中的请求没有改变指令槽，不上时间线
        private void OnDirectiveResult(AgentDirectiveResult result)
        {
            if (result.Stage == AgentDirectiveStage.Rejected || result.Stage == AgentDirectiveStage.Planning) return;
            AddTimeline(new AgentBrainTimelineEntry
            {
                Kind = AgentBrainTimelineKind.Directive, TimeSeconds = Time.timeAsDouble, Directive = result.Request,
                Stage = result.Stage, Failure = result.Reason
            });
        }

        private void AddTimeline(in AgentBrainTimelineEntry entry)
        {
            _timeline[_timelineNext] = entry;
            _timelineNext = (_timelineNext + 1) % TimelineCapacity;
            if (TimelineCount < TimelineCapacity) TimelineCount++;
            TimelineTotal++;
        }

        void IAgentDecisionRoundSink.RecordSkip(double timeSeconds, AgentDecisionSkipReason reason)
        {
            LastSkip = reason;
            LastSkipTime = timeSeconds;
        }
    }
}
#endif

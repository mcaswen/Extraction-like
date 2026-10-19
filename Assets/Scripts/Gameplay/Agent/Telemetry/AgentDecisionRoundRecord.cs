#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.StateMachine.Runtime;
using Gameplay.Agent.Decision;
using UnityEngine;

namespace Gameplay.Agent.Telemetry
{
    /// <summary>一轮评分里的一个候选；只含值类型和已有引用</summary>
    public struct AgentDecisionCandidateRow
    {
        public string TargetId;
        public GameObject TargetObject;
        public AgentDecisionTargetKind Kind;
        public Vector3 Position;
        public bool FallbackList;
        public int ListIndex;
        public bool HasRouteEstimate;
        public float RouteRisk;
        public AgentCandidateVerdict Verdict;
        public AgentScoreBreakdown Breakdown;
    }

    /// <summary>
    /// 一轮目标评分的记录：时间、智能体状态、每个候选的拆项和判定。定长数组复用，记录不分配内存
    /// </summary>
    public sealed class AgentDecisionRoundRecord
    {
        public const int CandidateCapacity = 48;

        private readonly AgentDecisionCandidateRow[] _rows = new AgentDecisionCandidateRow[CandidateCapacity];
        private int _listStart;

        public double TimeSeconds { get; private set; }
        public float HealthRatio { get; private set; }
        public int CarriedValue { get; private set; }
        public int Count { get; private set; }
        public int ChosenRow { get; private set; } = -1;
        public bool Overflowed { get; private set; }
        public long Sequence { get; private set; }

        public AgentDecisionCandidateRow GetRow(int index) => _rows[index];

        internal void Begin(long sequence, double timeSeconds, in AgentDecisionContext context)
        {
            Sequence = sequence;
            TimeSeconds = timeSeconds;
            HealthRatio = context.HealthRatio;
            CarriedValue = context.CarriedValue;
            Count = 0;
            ChosenRow = -1;
            Overflowed = false;
            _listStart = 0;
        }

        internal void BeginList() => _listStart = Count;

        internal void Add(int listIndex, in AgentDecisionCandidate candidate, bool fallback, AgentCandidateVerdict verdict, in AgentScoreBreakdown breakdown)
        {
            if (Count >= CandidateCapacity)
            {
                Overflowed = true;
                return;
            }

            _rows[Count] = new AgentDecisionCandidateRow
            {
                TargetId = candidate.TargetId,
                TargetObject = candidate.TargetObject,
                Kind = candidate.DecisionTargetKind,
                Position = candidate.TargetPosition,
                FallbackList = fallback,
                ListIndex = listIndex,
                HasRouteEstimate = candidate.HasRouteEstimate,
                RouteRisk = candidate.RouteRisk,
                Verdict = verdict,
                Breakdown = breakdown
            };
            Count++;
        }

        // 在当前列表的行里找评分选中的那一个
        internal void MarkChosen(int listIndex)
        {
            for (int row = _listStart; row < Count; row++)
            {
                if (_rows[row].ListIndex != listIndex) continue;
                ChosenRow = row;
                _rows[row].Verdict = AgentCandidateVerdict.Chosen;
                return;
            }
        }
    }

    /// <summary>一次叶子状态切换</summary>
    public readonly struct AgentStateTransitionRecord
    {
        public readonly StateMachineState From;
        public readonly StateMachineState To;
        public readonly StateTransition Transition;
        public readonly double TimeSeconds;

        public AgentStateTransitionRecord(StateMachineState from, StateMachineState to,
            StateTransition transition, double timeSeconds)
        {
            From = from;
            To = to;
            Transition = transition;
            TimeSeconds = timeSeconds;
        }
    }
}
#endif

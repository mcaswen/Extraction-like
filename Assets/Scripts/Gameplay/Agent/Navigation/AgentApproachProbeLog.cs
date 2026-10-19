#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Gameplay.Agent.Navigation
{
    /// <summary>接近点探测的阶段：敌人脚下、部分路径终点、三圈环扫</summary>
    public enum AgentApproachProbeStage : byte { Direct = 0, PartialPathEnd = 1, Ring0 = 2, Ring1 = 3, Ring2 = 4 }

    /// <summary>
    /// 一次接近点求解中探测过的点和结果，定长数组复用，记录不分配内存。
    /// 由遥测挂到执行侧的接近查询缓冲上；查询的取舍完全不受影响。
    /// </summary>
    public sealed class AgentApproachProbeLog
    {
        public const int Capacity = 24;

        private readonly Vector3[] _points = new Vector3[Capacity];
        private readonly AgentApproachProbeStage[] _stages = new AgentApproachProbeStage[Capacity];
        private readonly byte[] _outcomes = new byte[Capacity];

        public int Count { get; private set; }

        /// <summary>选中点的序号；没有选中时为 -1</summary>
        public int ChosenIndex { get; private set; } = -1;

        /// <summary>累计求解次数，读者据此判断记录是否更新</summary>
        public long Version { get; private set; }

        public Vector3 GetPoint(int index) => _points[index];

        public AgentApproachProbeStage GetStage(int index) => _stages[index];

        /// <summary>0 为选中；1 采样不到导航点；2 路径不可达；3 落点偏离；4 没有路径拐点；5 身体看不见目标；6 枪口打不到</summary>
        public byte GetOutcome(int index) => _outcomes[index];

        internal void Begin()
        {
            Count = 0;
            ChosenIndex = -1;
            Version++;
        }

        internal void Add(Vector3 point, AgentApproachProbeStage stage, AgentCombatApproachQuery.Outcome outcome)
        {
            if (Count >= Capacity) return;
            _points[Count] = point;
            _stages[Count] = stage;
            _outcomes[Count] = (byte)outcome;
            if (outcome == AgentCombatApproachQuery.Outcome.Chosen) ChosenIndex = Count;
            Count++;
        }
    }
}
#endif

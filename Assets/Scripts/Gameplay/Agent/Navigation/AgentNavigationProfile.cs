using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>导航配置的不可变快照；场景烘焙和运行时查询使用同一组采样约束及区域成本。</summary>
    public sealed class AgentNavigationProfile
    {
        private readonly NavMeshQueryFilter _filter;
        public int AgentTypeId => _filter.agentTypeID;
        public int AreaMask => _filter.areaMask;
        public float SampleRadius { get; }
        public float HeightTolerance { get; }
        // 成本数组由本实例独占。查询仅借用 filter，不能调用 SetAreaCost。
        internal NavMeshQueryFilter Filter => _filter;

        public AgentNavigationProfile(int agentTypeId, int areaMask, float sampleRadius,
            float heightTolerance, IReadOnlyList<float> areaCosts = null)
        {
            if (!Finite(sampleRadius) || sampleRadius <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRadius));
            if (!Finite(heightTolerance) || heightTolerance < 0) throw new ArgumentOutOfRangeException(nameof(heightTolerance));
            if (areaCosts != null && areaCosts.Count != 32) throw new ArgumentException("需要全部 32 个区域成本。", nameof(areaCosts));
            var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };
            for (int i = 0; i < 32; i++)
            {
                float cost = areaCosts == null ? 1f : areaCosts[i];
                if (!Finite(cost) || cost < 1) throw new ArgumentOutOfRangeException(nameof(areaCosts));
                filter.SetAreaCost(i, cost);
            }
            _filter = filter;
            SampleRadius = sampleRadius;
            HeightTolerance = heightTolerance;
        }

        public float GetAreaCost(int area) => _filter.GetAreaCost(area);

        /// <summary>配置变更时重建快照；不应在每次显示刷新时创建。</summary>
        public static AgentNavigationProfile FromAgent(NavMeshAgent agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            var costs = new float[32];
            for (int i = 0; i < costs.Length; i++) costs[i] = agent.GetAreaCost(i);
            return new AgentNavigationProfile(agent.agentTypeID, agent.areaMask,
                Mathf.Max(0.5f, agent.radius * 2f), Mathf.Max(0.5f, agent.height * 0.5f), costs);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

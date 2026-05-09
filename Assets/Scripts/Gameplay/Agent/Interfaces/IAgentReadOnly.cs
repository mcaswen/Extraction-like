using UnityEngine;
using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Interfaces
{
    /// <summary>
    /// 外部系统获取Agent信息用接口，比如生命值、位置等
    /// UI、敌人感知、地图系统等都应通过该接口获取Agent状态，而不是直接查找内部字段
    /// </summary>
    public interface IAgentReadOnly
    {
        AgentId AgentId { get; }
        string AgentIdValue { get; }

        // 位置相关
        Transform CachedTransform { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }

        AgentMacroStateId CurrentMacroStateId { get; } // 当前状态机的宏观状态 ID
        string CurrentMacroStateName { get; } // 当前状态机的宏观状态名称

        // 属性相关
        int CurrentHealth { get; }
        int MaxHealth { get; }
        float HealthRatio { get; }
        bool IsDead { get; }

        BehaviorBlackboard Blackboard { get; } // 行为树共享黑板参数
    }
}

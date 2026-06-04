using UnityEngine;
using UnityEngine.AI;
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
        /// <summary>
        /// Agent 的强类型运行时 ID
        /// </summary>
        AgentId AgentId { get; }

        /// <summary>
        /// Agent 的字符串运行时 ID
        /// </summary>
        string AgentIdValue { get; }

        /// <summary>
        /// Agent 缓存 Transform
        /// </summary>
        Transform CachedTransform { get; }

        /// <summary>
        /// Agent 使用的 NavMeshAgent 组件
        /// </summary>
        NavMeshAgent NavMeshAgent { get; }

        /// <summary>
        /// Agent 当前世界坐标
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Agent 当前朝向
        /// </summary>
        Vector3 Forward { get; }

        /// <summary>
        /// 当前状态机的宏观状态 ID
        /// </summary>
        AgentMacroStateId CurrentMacroStateId { get; }

        /// <summary>
        /// 当前状态机的宏观状态名称
        /// </summary>
        string CurrentMacroStateName { get; }

        /// <summary>
        /// 当前生命值
        /// </summary>
        int CurrentHealth { get; }

        /// <summary>
        /// 最大生命值
        /// </summary>
        int MaxHealth { get; }

        /// <summary>
        /// 当前生命比例
        /// </summary>
        float HealthRatio { get; }

        /// <summary>
        /// 当前 Agent 是否死亡
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// 行为树和状态机共享黑板
        /// 外部只读系统可通过它观察运行时事实
        /// </summary>
        BehaviorBlackboard Blackboard { get; }
    }
}

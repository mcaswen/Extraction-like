using UnityEngine;
using Core.BehaviorTree.Blackboard;

namespace Gameplay.Player.Interfaces
{
    /// <summary>
    /// 外部系统获取主角信息用接口，比如生命值、位置等
    /// UI、敌人感知、地图系统等都应通过该接口获取主角状态，而不是直接查找内部字段
    /// </summary>
    public interface IPlayerReadOnly
    {
        // 位置相关
        Transform CachedTransform { get; }
        Vector3 Position { get; }
        Vector3 Forward { get; }

        string CurrentMacroStateName { get; } // 当前状态机的宏观状态名称

        // 属性相关
        int CurrentHealth { get; }
        int MaxHealth { get; }
        float HealthRatio { get; }
        bool IsDead { get; }

        BehaviorBlackboard Blackboard { get; } // 行为树共享黑板参数
    }
}
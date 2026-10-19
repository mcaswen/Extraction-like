using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Combat;
using Gameplay.Agent.Navigation;
using UnityEngine;

namespace Gameplay.Agent.AI.Memory
{
    /// <summary>
    /// 交战子树的执行记忆。跨帧字段与重构前交战节点的字段一一对应：子树进入时重置计时（命令 ID 保留），
    /// 命令切换时由"同步指令记忆"节点重置。帧内字段由各评估节点每帧重新写入，条件节点只读。
    /// </summary>
    public sealed class AgentEngageMemory : IBehaviorScope
    {
        // 跨帧
        public string CommandId;
        public double LostSightSince = -1d;
        public bool HasObservedTarget;
        public double InitialApproachDeadline = -1d;
        public readonly AgentCombatApproachQuery.Buffer ApproachBuffer = new AgentCombatApproachQuery.Buffer();
        public Vector3 ApproachPosition;
        public Vector3 ApproachEnemyPosition;
        public double ApproachTime = -1d;
        public float ApproachRange;
        public float ApproachRadius;
        public float ApproachHeight;
        public float ApproachBaseOffset;
        public Vector3 ApproachScale;
        public int ApproachAreaMask;
        public int ApproachAgentType;

        // 帧内
        public global::EnemyHealthController Enemy;
        public AgentCombatShooter Shooter;
        public AgentCombatController Combat;
        public float AttackRange;
        public float MoveSpeed;
        public bool ShooterUnavailable;
        public bool CanFire;
        public bool Observed;
        public bool Obstructed;
        public bool InitialManualApproach;
        public float LostSightTimeout;
        public bool ApproachUnreachable;
        public bool BudgetPathFailed;
        public bool BudgetExpired;

        public void ResetTimers()
        {
            LostSightSince = -1d;
            ApproachTime = -1d;
            HasObservedTarget = false;
            InitialApproachDeadline = -1d;
        }

        public void OnScopeEnter(BehaviorTreeContext context) => ResetTimers();

        public void OnScopeExit(BehaviorTreeContext context, BehaviorNodeResult result) { }

        public void OnScopeAbort(BehaviorTreeContext context) { }
    }
}

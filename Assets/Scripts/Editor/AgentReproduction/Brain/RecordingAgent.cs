using System;
using System.Collections.Generic;
using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace AgentReproduction.Brain
{
    /// <summary>
    /// 等价对拍用的智能体替身：只读数据转发给真实智能体，命令一律记录。
    /// 移动不交给导航系统，按脚本返回导航结果，智能体位置完全由对拍脚本决定。
    /// 结束指令时按生命周期的做法清掉黑板上的待处理指令。
    /// </summary>
    public sealed class RecordingAgent : IAgentReadOnly, IAgentCommandReceiver
    {
        private readonly AgentPawnRoot _pawn;
        private readonly BehaviorBlackboard _blackboard;
        private readonly List<string> _log;

        public RecordingAgent(AgentPawnRoot pawn, BehaviorBlackboard blackboard, List<string> log)
        {
            _pawn = pawn;
            _blackboard = blackboard;
            _log = log;
        }

        /// <summary>脚本化导航：参数为目标点和停止距离；为空时按平面距离判断是否到达</summary>
        public Func<Vector3, float, AgentNavigationStatus> Navigation { get; set; }

        public double TimeSeconds { get; set; }

        public AgentRouteSnapshot RouteSnapshot => default;
        public event Action<AgentRouteResult> RouteResultPublished { add { } remove { } }
        public bool CanSelectAutonomousRouteTarget(AgentTargetRef target) { _log.Add("call CanSelectAutonomousRouteTarget"); return true; }
        public AgentId AgentId => _pawn.AgentId;
        public string AgentIdValue => _pawn.AgentIdValue;
        public Transform CachedTransform => _pawn.CachedTransform;
        public NavMeshAgent NavMeshAgent => _pawn.NavMeshAgent;
        public Vector3 Position => _pawn.Position;
        public Vector3 Forward => _pawn.Forward;
        public AgentMacroStateId CurrentMacroStateId => _pawn.CurrentMacroStateId;
        public string CurrentMacroStateName => _pawn.CurrentMacroStateName;
        public int CurrentHealth => _pawn.CurrentHealth;
        public int MaxHealth => _pawn.MaxHealth;
        public float HealthRatio => _pawn.HealthRatio;
        public bool IsDead => _pawn.IsDead;
        public BehaviorBlackboard Blackboard => _blackboard;

        public AgentRouteResult TrySubmitRoute(AgentRouteRequest request) { _log.Add("call TrySubmitRoute"); return default; }
        public void ApplyDamage(DamageRequest damageRequest) => _log.Add("call ApplyDamage");
        public void SetVisibleEnemy(bool hasVisibleEnemy) => _log.Add("call SetVisibleEnemy " + hasVisibleEnemy);
        public void SetHasEnemySourceTarget(bool value) => _log.Add("call SetHasEnemySourceTarget " + value);
        public void SetHasResourceTarget(bool value) => _log.Add("call SetHasResourceTarget " + value);
        public void SetShouldExtract(bool value) => _log.Add("call SetShouldExtract " + value);
        public void SetHasInteractableTarget(bool value) => _log.Add("call SetHasInteractableTarget " + value);
        public void SubmitDirective(AgentDirectiveRequest request) => _log.Add("call SubmitDirective " + request.CommandId);
        public AgentDirectiveResult TrySubmitDirective(AgentDirectiveRequest request) { _log.Add("call TrySubmitDirective " + request.CommandId); return default; }
        public void ClearDirective() => _log.Add("call ClearDirective");
        public void StopDirectiveMovement() => _log.Add("stop");

        public bool FinishDirective(string commandId, AgentDirectiveFailure failure = AgentDirectiveFailure.None)
        {
            bool matched = _blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest, out AgentDirectiveRequest pending) &&
                           pending.CommandId == commandId;
            _log.Add($"finish {commandId} {failure} matched={matched}");
            if (matched)
            {
                _blackboard.SetValue(AgentBlackboardKeys.HasPendingDirective, false, TimeSeconds);
                _blackboard.RemoveValue(AgentBlackboardKeys.PendingDirectiveRequest, TimeSeconds);
            }

            return matched;
        }

        public AgentNavigationResult MoveDirective(Vector3 destination, float stoppingDistance, float speed)
        {
            AgentNavigationStatus status = Navigation != null ? Navigation(destination, stoppingDistance) : DefaultNavigation(destination, stoppingDistance);
            _log.Add($"move {BrainTrace.Format(destination)} stop={stoppingDistance:0.###} speed={speed:0.###} -> {status}");
            return new AgentNavigationResult(status, destination);
        }

        private AgentNavigationStatus DefaultNavigation(Vector3 destination, float stoppingDistance)
        {
            Vector3 position = _pawn.Position;
            float dx = position.x - destination.x, dz = position.z - destination.z;
            return dx * dx + dz * dz <= (stoppingDistance + 0.01f) * (stoppingDistance + 0.01f)
                ? AgentNavigationStatus.Arrived
                : AgentNavigationStatus.Moving;
        }
    }
}

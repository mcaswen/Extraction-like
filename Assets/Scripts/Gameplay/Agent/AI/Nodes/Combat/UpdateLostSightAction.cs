using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Perception;

namespace Gameplay.Agent.AI.Nodes.Combat
{
    /// <summary>
    /// 动作：不能开火时更新失视计时。看得见且没被挡就清零；否则开始计时。
    /// 玩家手动交战在第一次看见目标之前不计时，改用一次性的接近预算（见"检查初次接近预算"）。
    /// </summary>
    public sealed class UpdateLostSightAction : AgentEngageActionBase
    {
        public UpdateLostSightAction(string nodeName, AgentEngageMemory memory)
            : base(nodeName, "更新失视计时", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetAgent(context, out IAgentReadOnly agent))
                return Fail(BehaviorFailureCode.ConditionFailed, InvalidAgent);
            if (!TryGetDirective(context, AgentDirectiveType.Engage, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Engage);

            Memory.Observed = context.Blackboard.TryGetValue(AgentBlackboardKeys.HasVisibleEnemy, out bool visible) && visible;
            Memory.Obstructed = Memory.Shooter != null && Memory.Shooter.LastShotResult == TargetVisibilityResult.Occluded;
            Memory.HasObservedTarget |= Memory.Observed;
            Memory.InitialManualApproach = AgentManualDirectiveLock.IsManualDirective(directiveRequest) && !Memory.HasObservedTarget;
            if (Memory.Observed && !Memory.Obstructed) Memory.LostSightSince = -1d;
            else if (!Memory.InitialManualApproach && Memory.LostSightSince < 0d) Memory.LostSightSince = context.TimeSeconds;
            Memory.LostSightTimeout = agent is Gameplay.Agent.Core.AgentPawnRoot pawn ? pawn.CombatLostSightTimeout : 2f;
            return Succeed();
        }
    }
}

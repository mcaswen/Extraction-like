using Core.BehaviorTree.Runtime;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.AI.Nodes.Search
{
    /// <summary>
    /// 动作：解析抽象资源点的位置；解析不到时失败
    /// </summary>
    public sealed class ResolveAbstractPointAction : AgentSearchActionBase
    {
        private const string InvalidResourcePosition = "Resource target position is invalid";

        public ResolveAbstractPointAction(string nodeName, AgentSearchMemory memory)
            : base(nodeName, "解析抽象资源点", memory)
        {
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            if (!TryGetDirective(context, AgentDirectiveType.Search, out AgentDirectiveRequest directiveRequest))
                return FailMissingDirective(AgentDirectiveType.Search);
            if (!TryResolveTargetPosition(directiveRequest.TargetRef, out Vector3 targetPosition))
                return Fail(BehaviorFailureCode.MissingBlackboardValue, InvalidResourcePosition);

            Memory.MemberTargetPosition = targetPosition;
            return Succeed();
        }
    }
}

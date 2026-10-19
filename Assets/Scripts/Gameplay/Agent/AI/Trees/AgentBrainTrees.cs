using Core.BehaviorTree.Nodes.Composites;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.AI.Nodes;
using Gameplay.Agent.Data;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace Gameplay.Agent.AI.Trees
{
    /// <summary>
    /// 各宏状态的细粒度行为树。每次调用都新建节点和记忆，节点不得在树之间共享。
    /// 原来同一帧内从头重查的逻辑用响应式组合，原来跨帧记住进度的地方保留记忆式组合。
    /// </summary>
    public static class AgentBrainTrees
    {
        public static BehaviorTreeType Navigate()
            => MoveTree("NavigateTree", "Navigate", "沿路线移动", AgentDirectiveType.MoveTo, AgentTargetKind.Location, true);

        // 敌人来源点只负责靠近侦查，真正接战必须等活跃敌人群出现
        public static BehaviorTreeType InvestigateEnemySource()
            => MoveTree("InvestigateEnemySourceTree", "InvestigateEnemySource", "侦查敌人来源", AgentDirectiveType.MoveTo, AgentTargetKind.EnemySource, false);

        // 先到达撤离点范围（记忆式：到达后不再重复这一段），再持续驻留并向 RaidFlowController 续写在点内
        public static BehaviorTreeType Extraction()
        {
            var approachMemory = new AgentMoveMemory();
            var presenceMemory = new AgentExtractionMemory();
            var root = new SequenceNode(
                "Extraction_Sequence",
                new ReactiveSequenceNode(
                    "Extraction_Approach",
                    new AgentCanActCondition("Extraction_Approach_CanAct"),
                    new HoldsDirectiveCondition("Extraction_Approach_HoldsDirective", AgentDirectiveType.Extract, AgentTargetKind.Extraction),
                    new ResolveDirectiveTargetAction("Extraction_Approach_ResolveTarget", AgentDirectiveType.Extract, AgentTargetKind.Extraction, approachMemory),
                    new MoveToResolvedTargetAction("Extraction_Approach_Move", AgentBlackboardKeys.InteractionDistance, 1.5f, false, approachMemory))
                    { DisplayName = "前往撤离点" },
                new ReactiveSequenceNode(
                    "Extraction_Hold",
                    new AgentCanActCondition("Extraction_Hold_CanAct"),
                    new HoldsDirectiveCondition("Extraction_Hold_HoldsDirective", AgentDirectiveType.Extract, AgentTargetKind.None),
                    new ResolveExtractionPointAction("Extraction_Hold_ResolvePoint", presenceMemory),
                    new MoveToResolvedTargetAction("Extraction_Hold_Move", AgentBlackboardKeys.InteractionDistance, 1.5f, false, presenceMemory),
                    new HoldExtractionPresenceAction("Extraction_Hold_Presence", presenceMemory))
                    { DisplayName = "驻留撤离点" }) { DisplayName = "撤离" };
            return new BehaviorTreeType("ExtractionTree", root);
        }

        private static BehaviorTreeType MoveTree(
            string treeName,
            string prefix,
            string displayName,
            AgentDirectiveType directiveType,
            AgentTargetKind targetKind,
            bool finishDirectiveOnArrival)
        {
            var memory = new AgentMoveMemory();
            var root = new ReactiveSequenceNode(
                prefix + "_Sequence",
                new AgentCanActCondition(prefix + "_CanAct"),
                new HoldsDirectiveCondition(prefix + "_HoldsDirective", directiveType, targetKind),
                new ResolveDirectiveTargetAction(prefix + "_ResolveTarget", directiveType, targetKind, memory),
                new MoveToResolvedTargetAction(
                    prefix + "_MoveToTarget",
                    AgentBlackboardKeys.MoveStoppingDistance,
                    2f,
                    finishDirectiveOnArrival,
                    memory)) { DisplayName = displayName };
            return new BehaviorTreeType(treeName, root);
        }
    }
}

// 重构前的行为树节点，只作为编辑器里等价对拍的对照，不进入构建
#if UNITY_EDITOR
using Core.BehaviorTree.Nodes.Composites;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Data;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace Gameplay.Agent.AI.Trees
{
    /// <summary>
    /// 重构前的行为树（每个状态一两个大动作节点）。只在编辑器中编译，作为等价对拍的对照；运行时一律使用 AgentBrainTrees。
    /// </summary>
    public static class AgentLegacyBrainTrees
    {
        public static BehaviorTreeType Explore() => MaintainState(AgentMacroStateId.Explore);

        public static BehaviorTreeType Navigate()
            => new BehaviorTreeType("NavigateTree", new MoveToTargetActionNode("Navigate_MoveToLocation",
                AgentDirectiveType.MoveTo, AgentTargetKind.Location, AgentBlackboardKeys.MoveStoppingDistance,
                2f, finishDirectiveOnArrival: true));

        public static BehaviorTreeType Combat()
        {
            return new BehaviorTreeType("CombatTree", new EngageEnemyActionNode("Combat_EngageEnemy"));
        }

        // 敌人来源点只负责靠近侦查，真正接战必须等活跃敌人群出现
        public static BehaviorTreeType InvestigateEnemySource()
        {
            return new BehaviorTreeType(
                "InvestigateEnemySourceTree",
                new MoveToTargetActionNode(
                    "InvestigateEnemySource_MoveToSource",
                    AgentDirectiveType.MoveTo,
                    AgentTargetKind.EnemySource,
                    AgentBlackboardKeys.MoveStoppingDistance,
                    2f));
        }

        // 搜索和开箱共用同一条执行链，宏状态差异只保留在状态名上
        public static BehaviorTreeType SearchResource(AgentMacroStateId macroStateId)
        {
            return new BehaviorTreeType(
                $"{macroStateId}Tree",
                new SequenceNode(
                    $"{macroStateId}_SearchSequence",
                    new MoveToTargetActionNode(
                        $"{macroStateId}_MoveToResource",
                        AgentDirectiveType.Search,
                        AgentTargetKind.Resource,
                        AgentBlackboardKeys.InteractionDistance,
                        1.5f),
                    new SearchResourceActionNode($"{macroStateId}_SearchResource")));
        }

        // 撤离树先到达范围，再让撤离节点持续桥接 RaidFlowController 的计时状态
        public static BehaviorTreeType Extraction()
        {
            return new BehaviorTreeType(
                "ExtractionTree",
                new SequenceNode(
                    "Extraction_Sequence",
                    new MoveToTargetActionNode(
                        "Extraction_MoveToPoint",
                        AgentDirectiveType.Extract,
                        AgentTargetKind.Extraction,
                        AgentBlackboardKeys.InteractionDistance,
                        1.5f),
                    new ExtractActionNode("Extraction_Extract")));
        }

        public static BehaviorTreeType MaintainState(AgentMacroStateId macroStateId)
        {
            return new BehaviorTreeType(
                $"{macroStateId}Tree",
                new MaintainMacroStateActionNode(
                    $"{macroStateId}_MaintainMacroState",
                    macroStateId));
        }
    }
}
#endif

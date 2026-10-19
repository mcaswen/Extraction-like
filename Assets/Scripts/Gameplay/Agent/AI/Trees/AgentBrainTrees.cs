using Core.BehaviorTree.Nodes.Composites;
using Core.BehaviorTree.Nodes.Decorators;
using Gameplay.Agent.AI.Memory;
using Gameplay.Agent.AI.Nodes;
using Gameplay.Agent.AI.Nodes.Combat;
using Gameplay.Agent.AI.Nodes.Search;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
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

        // 交战：每帧从头确认指令和目标，再在"收尾 / 攻击不可用 / 开火 / 接近"之间按优先级选一条；
        // 交战记忆由作用域在树进入时重置计时（命令 ID 保留），与重构前交战节点的进入语义一致
        public static BehaviorTreeType Combat()
        {
            var m = new AgentEngageMemory();
            var fire = new ReactiveSequenceNode(
                "Combat_Fire",
                new AgentPredicateCondition("Combat_CanFire", "可开火", _ => m.CanFire),
                new HoldFirePositionAction("Combat_HoldPosition", m),
                new ReactiveSelectorNode(
                    "Combat_Attack",
                    new ReactiveSequenceNode(
                        "Combat_Cooldown",
                        new AgentPredicateCondition("Combat_CoolingDown", "攻击冷却中", context => m.Combat != null && !m.Combat.IsAttackReady(context.TimeSeconds)),
                        new KeepRunningAction("Combat_WaitCooldown", "等待冷却")) { DisplayName = "冷却" },
                    new CastReadySkillAction("Combat_CastSkill", m),
                    new ShootEnemyAction("Combat_Shoot", m)) { DisplayName = "出手" }) { DisplayName = "开火" };

            var approach = new ReactiveSequenceNode(
                "Combat_Approach",
                new UpdateLostSightAction("Combat_UpdateLostSight", m),
                new ReactiveSelectorNode(
                    "Combat_ApproachDecision",
                    new ReactiveSequenceNode(
                        "Combat_LostSight",
                        new AgentPredicateCondition("Combat_LostSightExpired", "失视超时", context => m.LostSightSince >= 0d && context.TimeSeconds - m.LostSightSince >= m.LostSightTimeout),
                        new FailDirectiveAction("Combat_FailLostSight", "判定失去视野", AgentDirectiveFailure.LostSight)) { DisplayName = "失去视野" },
                    new ReactiveSequenceNode(
                        "Combat_NavigationNotReady",
                        new AgentPredicateCondition("Combat_NavNotReady", "导航未就绪", context =>
                            AgentTreeQueries.TryGetAgent(context, out var agent) && !AgentNavigationQuery.IsReady(agent.NavMeshAgent)),
                        new ChargeEnemyAction("Combat_Charge", m)) { DisplayName = "直接靠近" },
                    new ReactiveSequenceNode(
                        "Combat_ApproachPoint",
                        new RefreshApproachPointAction("Combat_RefreshApproach", m),
                        new CheckApproachBudgetAction("Combat_CheckBudget", m),
                        new ReactiveSelectorNode(
                            "Combat_ApproachOutcome",
                            new ReactiveSequenceNode(
                                "Combat_ApproachUnreachable",
                                new AgentPredicateCondition("Combat_NoApproachPoint", "求不出接近点", _ => m.ApproachUnreachable),
                                new FailDirectiveAction("Combat_FailUnreachable", "判定不可达", AgentDirectiveFailure.Unreachable)) { DisplayName = "不可达" },
                            new ReactiveSequenceNode(
                                "Combat_BudgetUnreachable",
                                new AgentPredicateCondition("Combat_BudgetPathFailed", "初次路径不可达", _ => m.BudgetPathFailed),
                                new FailDirectiveAction("Combat_FailBudgetUnreachable", "判定不可达", AgentDirectiveFailure.Unreachable)) { DisplayName = "初次路径不可达" },
                            new ReactiveSequenceNode(
                                "Combat_BudgetExpired",
                                new AgentPredicateCondition("Combat_BudgetUsedUp", "接近预算耗尽", _ => m.BudgetExpired),
                                new FailDirectiveAction("Combat_FailBudgetExpired", "判定失去视野", AgentDirectiveFailure.LostSight)) { DisplayName = "预算耗尽" },
                            new MoveToApproachPointAction("Combat_MoveToApproach", m)) { DisplayName = "接近结果" }) { DisplayName = "求点并移动" }) { DisplayName = "接近决策" }) { DisplayName = "接近" };

            var engage = new ReactiveSequenceNode(
                "Combat_Engaging",
                new NotifyEngagedAction("Combat_NotifyEngaged", m),
                new AssessFiringLineAction("Combat_AssessFiringLine", m),
                new ReactiveSelectorNode(
                    "Combat_Respond",
                    new ReactiveSequenceNode(
                        "Combat_AttackUnavailable",
                        new AgentPredicateCondition("Combat_ShooterUnavailable", "射手不可用", _ => m.ShooterUnavailable),
                        new FailDirectiveAction("Combat_FailAttackUnavailable", "判定攻击不可用", AgentDirectiveFailure.AttackUnavailable)) { DisplayName = "攻击不可用" },
                    fire,
                    approach) { DisplayName = "应对" }) { DisplayName = "交战中" };

            var root = new ScopeDecorator(
                "Combat_Scope",
                m,
                new ReactiveSequenceNode(
                    "Combat_Sequence",
                    new AgentCanActCondition("Combat_CanAct"),
                    new HoldsDirectiveCondition("Combat_HoldsDirective", AgentDirectiveType.Engage, AgentTargetKind.None),
                    new SyncEngageCommandAction("Combat_SyncCommand", m),
                    new ResolveTargetEnemyAction("Combat_ResolveEnemy", m),
                    new ReactiveSelectorNode(
                        "Combat_Handle",
                        new ReactiveSequenceNode(
                            "Combat_TargetEnded",
                            new AgentPredicateCondition("Combat_TargetGoneOrDead", "目标丢失或已阵亡", _ => m.Enemy == null || m.Enemy.GetCurrentHealthRatio() <= 0f),
                            new ConcludeCombatAction("Combat_Conclude", m)) { DisplayName = "目标已结束" },
                        engage) { DisplayName = "处置" }) { DisplayName = "交战" }) { DisplayName = "交战记忆" };
            return new BehaviorTreeType("CombatTree", root);
        }

        // 搜索资源：先到达最近的未处理成员（记忆式：到达后不再重复这一段），再在搜索作用域里处理资源。
        // 搜索记忆在作用域进入时重置，退出时按结果上报完成或离开，中断时上报离开。
        public static BehaviorTreeType SearchResource()
        {
            var approachMemory = new AgentMoveMemory();
            var s = new AgentSearchMemory();
            var cluster = new ReactiveSequenceNode(
                "Search_Cluster",
                new AgentPredicateCondition("Search_IsCluster", "目标是资源群", _ => s.Form == AgentSearchTargetForm.Cluster),
                new ResolveNextMemberAction("Search_ResolveNextMember", s),
                new ReactiveSelectorNode(
                    "Search_MemberOutcome",
                    new ReactiveSequenceNode(
                        "Search_ClusterDone",
                        new AgentPredicateCondition("Search_NoPendingMember", "群已处理完", _ => s.Resolution == AgentSearchMemberResolution.ClusterDone),
                        new CompleteSearchAction("Search_CompleteCluster", s)) { DisplayName = "群已处理完" },
                    new ReactiveSequenceNode(
                        "Search_MemberUnreachable",
                        new AgentPredicateCondition("Search_PendingButUnreachable", "成员走不到", _ => s.Resolution == AgentSearchMemberResolution.Unreachable),
                        new FailDirectiveAction("Search_FailUnreachable", "判定不可达", AgentDirectiveFailure.Unreachable, keepRunning: true)) { DisplayName = "成员不可达" },
                    new ReactiveSequenceNode(
                        "Search_CurrentMember",
                        new SwitchClusterMemberAction("Search_SwitchMember", s),
                        ProcessMember("Search_ClusterMember", s),
                        new AfterClusterMemberAction("Search_AfterMember", s)) { DisplayName = "处理当前成员" }) { DisplayName = "成员处理结果" }) { DisplayName = "资源群" };

            var abstractPoint = new ReactiveSequenceNode(
                "Search_AbstractPoint",
                new AgentPredicateCondition("Search_IsAbstractPoint", "目标是抽象资源点", _ => s.Form == AgentSearchTargetForm.AbstractPoint),
                new ResolveAbstractPointAction("Search_ResolveAbstractPoint", s),
                new MoveToAbstractPointAction("Search_MoveToAbstractPoint", s),
                new CompleteSearchAction("Search_CompleteAbstractPoint", s)) { DisplayName = "抽象资源点" };

            // 三个形态分支都带守卫：前一个分支失败时不能落到后面的分支（旧节点在这里直接失败）
            var concrete = new ReactiveSequenceNode(
                "Search_Concrete",
                new AgentPredicateCondition("Search_IsConcrete", "目标是具体资源", _ => s.Form == AgentSearchTargetForm.ConcreteObject),
                new SelectConcreteResourceAction("Search_SelectConcrete", s),
                ProcessMember("Search_ConcreteMember", s)) { DisplayName = "具体资源" };

            var process = new ScopeDecorator(
                "Search_Scope",
                s,
                new ReactiveSequenceNode(
                    "Search_Process",
                    new AgentCanActCondition("Search_CanAct"),
                    new HoldsDirectiveCondition("Search_HoldsDirective", AgentDirectiveType.Search, AgentTargetKind.None),
                    new SyncSearchTargetAction("Search_SyncTarget", s),
                    new ReactiveSelectorNode("Search_ByForm", cluster, abstractPoint, concrete) { DisplayName = "按目标形态" }) { DisplayName = "处理资源" }) { DisplayName = "搜索记忆" };

            var root = new SequenceNode(
                "Search_Sequence",
                new ReactiveSequenceNode(
                    "Search_Approach",
                    new AgentCanActCondition("Search_Approach_CanAct"),
                    new HoldsDirectiveCondition("Search_Approach_HoldsDirective", AgentDirectiveType.Search, AgentTargetKind.Resource),
                    new ResolveDirectiveTargetAction("Search_Approach_ResolveTarget", AgentDirectiveType.Search, AgentTargetKind.Resource, approachMemory),
                    new MoveToResolvedTargetAction("Search_Approach_Move", AgentBlackboardKeys.InteractionDistance, 1.5f, false, approachMemory))
                    { DisplayName = "前往资源" },
                process) { DisplayName = "搜索资源" };
            return new BehaviorTreeType("SearchResourceTree", root);
        }

        // 处理一个箱子或地面物品：对象不存在就结束；否则定位、靠近，再按类型检查或等待玩家开关背包
        private static ReactiveSelectorNode ProcessMember(string prefix, AgentSearchMemory s)
        {
            return new ReactiveSelectorNode(
                prefix,
                new ReactiveSequenceNode(
                    prefix + "_Gone",
                    new AgentPredicateCondition(prefix + "_IsGone", "成员已不存在", _ => s.Member == null),
                    new EndMemberAction(prefix + "_EndGone", "结束本成员", s)) { DisplayName = "成员已不存在" },
                new ReactiveSequenceNode(
                    prefix + "_OnSite",
                    new LocateMemberAction(prefix + "_Locate", s),
                    new ApproachMemberAction(prefix + "_Approach", s),
                    new ReactiveSelectorNode(
                        prefix + "_ByType",
                        new ReactiveSequenceNode(
                            prefix + "_LootBox",
                            new AgentPredicateCondition(prefix + "_IsLootBox", "是箱子", _ => s.LootBox != null),
                            new ReactiveSelectorNode(
                                prefix + "_LootBoxOutcome",
                                new InspectLootBoxAction(prefix + "_InspectLootBox", s),
                                WaitForInventory(prefix + "_LootBoxWait", s, true)) { DisplayName = "处理箱子" }) { DisplayName = "箱子" },
                        new ReactiveSequenceNode(
                            prefix + "_WorldItem",
                            new AgentPredicateCondition(prefix + "_IsWorldItem", "是地面物品", _ => s.WorldItem != null),
                            new ReactiveSelectorNode(
                                prefix + "_WorldItemOutcome",
                                new InspectWorldItemAction(prefix + "_InspectWorldItem", s),
                                WaitForInventory(prefix + "_WorldItemWait", s, false)) { DisplayName = "处理地面物品" }) { DisplayName = "地面物品" },
                        new EndMemberAction(prefix + "_EndOther", "其他对象直接结束", s)) { DisplayName = "按成员类型" }) { DisplayName = "到场处理" }) { DisplayName = "处理单个成员" };
        }

        private static ReactiveSequenceNode WaitForInventory(string prefix, AgentSearchMemory s, bool lootBox)
        {
            return new ReactiveSequenceNode(
                prefix,
                new ReportWaitingForInventoryAction(prefix + "_Report", s, lootBox),
                new ObserveInventorySessionAction(prefix + "_Observe", s, lootBox),
                new ReactiveSelectorNode(
                    prefix + "_Outcome",
                    new ReactiveSequenceNode(
                        prefix + "_CapacityBlocked",
                        new AgentPredicateCondition(prefix + "_IsCapacityBlocked", "背包容量不足", _ => s.SessionCapacityBlocked),
                        new FinishInventorySessionAction(prefix + "_FinishBlocked", s, lootBox, true)) { DisplayName = "容量不足" },
                    new FinishInventorySessionAction(prefix + "_FinishClosed", s, lootBox, false)) { DisplayName = "会话结果" }) { DisplayName = "等待背包" };
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

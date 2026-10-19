#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.StateMachine.Runtime;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Agent.Runtime;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>面板上的中文名称：状态、转移、指令种类和来源、候选被排除的原因</summary>
    public static class AgentBrainText
    {
        public static string State(StateMachineState state) => state == null ? "—" : StateName(state.StateName);

        public static string StateName(string stateName)
        {
            switch (stateName)
            {
                case "Explore": return "探索";
                case "Navigate": return "导航";
                case "InvestigateEnemySource": return "侦查敌源";
                case "SearchResource": return "搜索资源";
                case "Combat": return "战斗";
                case "Extraction": return "撤离";
                case "Raid": return "局内";
                default: return stateName;
            }
        }

        /// <summary>转移的原因，按转移名的目标归类；手动搜索和受击打断单独说明</summary>
        public static string Transition(StateTransition transition)
        {
            if (transition == null) return "—";
            string name = transition.Name;
            if (name == "ToNavigate") return "收到路线";
            if (name == "SearchResourceToCombat") return "受击打断";
            if (name == "ExtractionToCombat") return "撤离遇袭";
            if (name.EndsWith("ManualSearchResource")) return "指挥员改派";
            if (name.EndsWith("ToCombat")) return "进入战斗";
            if (name.EndsWith("ToSearchResource")) return "开始搜索";
            if (name.EndsWith("ToInvestigateEnemySource")) return "侦查敌源";
            if (name.EndsWith("ToExtraction")) return "开始撤离";
            if (name == "NavigateToSource") return "侦查敌源";
            if (name == "NavigateToSearch") return "开始搜索";
            if (name.EndsWith("ToExplore")) return "回到探索";
            return name;
        }

        public static string DirectiveKind(AgentDirectiveType type)
        {
            switch (type)
            {
                case AgentDirectiveType.Search: return "搜索";
                case AgentDirectiveType.Engage: return "交战";
                case AgentDirectiveType.MoveTo: return "移动";
                case AgentDirectiveType.Extract: return "撤离";
                case AgentDirectiveType.RequestBuff: return "增益";
                default: return "—";
            }
        }

        public static string DirectiveSource(in AgentDirectiveRequest request)
        {
            if (AgentManualDirectiveLock.IsCombatDamageDirective(request)) return "反击";
            if (request.RouteContext.IsValid) return request.RouteContext.IsPlayerRoute ? "指挥员" : "自主";
            return AgentManualDirectiveLock.IsManualDirective(request) ? "指挥员" : "自主";
        }

        public static string TargetKind(AgentDecisionTargetKind kind)
        {
            switch (kind)
            {
                case AgentDecisionTargetKind.Resource: return "资源群";
                case AgentDecisionTargetKind.EnemySource: return "敌人来源";
                case AgentDecisionTargetKind.ActiveEnemy: return "敌人群";
                case AgentDecisionTargetKind.Extraction: return "撤离点";
                default: return "目标";
            }
        }

        public static string Verdict(AgentCandidateVerdict verdict)
        {
            switch (verdict)
            {
                case AgentCandidateVerdict.Invalid: return "目标无效";
                case AgentCandidateVerdict.LowHealthCombat: return "血量低，不接战";
                case AgentCandidateVerdict.RiskOverThreshold: return "风险超过上限";
                default: return string.Empty;
            }
        }
    }
}
#endif

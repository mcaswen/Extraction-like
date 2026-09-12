using Gameplay.Agent.Commands;
using Gameplay.Agent.Routes;

namespace Gameplay.Targets.Presentation
{
    public static class AgentCommandFeedbackText
    {
        public static string Format(AgentRouteResult result)
        {
            if (result.Accepted) return "指令下达成功";
            string reason = result.Reason switch {
                AgentRouteFailure.NoAgent => "未选择可用角色",
                AgentRouteFailure.AgentUnavailable => "角色不可用",
                AgentRouteFailure.MapUnavailable => "指挥地图不可用",
                AgentRouteFailure.MissingTarget => "目标不存在",
                AgentRouteFailure.TargetUnavailable => "目标不可用",
                AgentRouteFailure.NoReachableEntry => "无法进入地图通路",
                AgentRouteFailure.Disconnected => "没有可通行的群路线",
                AgentRouteFailure.Unreachable => "目标不可达",
                AgentRouteFailure.NoProgress => "移动受阻",
                AgentRouteFailure.NavigationNotReady => "通路尚未就绪",
                AgentRouteFailure.StaleContext => "地图已变化，无法继续原路线",
                AgentRouteFailure.SpawnFailed => "敌人群尚未准备就绪",
                AgentRouteFailure.NoExecutableMember => "群内暂无可处理目标",
                AgentRouteFailure.CapacityExtraction => "背包已满，转入自主撤离",
                AgentRouteFailure.SettlementFailed => "撤离结算失败",
                AgentRouteFailure.Superseded => "已有优先任务",
                _ => "目标无效"
            };
            return "指令下达失败：" + reason;
        }

        public static string Format(AgentDirectiveResult result)
        {
            if (result.Accepted) return "指令下达成功";
            string reason;
            switch (result.Reason)
            {
                case AgentDirectiveFailure.NoAgent: reason = "未选择可用角色"; break;
                case AgentDirectiveFailure.AgentUnavailable: reason = "角色不可用"; break;
                case AgentDirectiveFailure.NavigationNotReady: reason = "导航尚未就绪"; break;
                case AgentDirectiveFailure.Unreachable: reason = "目标不可达"; break;
                case AgentDirectiveFailure.NoProgress: reason = "移动受阻"; break;
                case AgentDirectiveFailure.LostSight: reason = "目标失去视野或超出范围"; break;
                case AgentDirectiveFailure.AttackUnavailable: reason = "攻击组件或弹体配置不可用"; break;
                case AgentDirectiveFailure.TargetCompleted: reason = "目标已完成"; break;
                case AgentDirectiveFailure.Superseded: reason = "已有优先任务"; break;
                default: reason = "目标无效"; break;
            }
            return "指令下达失败：" + reason;
        }
    }
}

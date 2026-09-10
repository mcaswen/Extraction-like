using Gameplay.Agent.Commands;

namespace Gameplay.Targets.Presentation
{
    public static class AgentCommandFeedbackText
    {
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
                case AgentDirectiveFailure.TargetCompleted: reason = "目标已完成"; break;
                case AgentDirectiveFailure.Superseded: reason = "已有优先任务"; break;
                default: reason = "目标无效"; break;
            }
            return "指令下达失败：" + reason;
        }
    }
}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Text;
using Gameplay.Agent.Routes;

namespace Gameplay.Demo
{
    /// <summary>导演场景的一步：进入时做的事（可以跨帧，例如模拟在地图上点群）、推进条件、超时（不受暂停影响的秒数）和字幕</summary>
    public sealed class AgentDemoStep
    {
        public AgentDemoStep(string title, string narration, float timeoutSeconds, Func<AgentDemoRun, bool> done, Func<AgentDemoRun, IEnumerator> enter = null)
        {
            Title = title;
            Narration = narration;
            TimeoutSeconds = timeoutSeconds;
            Done = done;
            Enter = enter;
        }

        public string Title { get; }
        public string Narration { get; }
        public float TimeoutSeconds { get; }
        public Func<AgentDemoRun, bool> Done { get; }
        public Func<AgentDemoRun, IEnumerator> Enter { get; }
    }

    /// <summary>一个导演场景：重新载入正式场景后按步骤真实触发，步骤由推进条件切换</summary>
    public sealed class AgentDemoScene
    {
        public AgentDemoScene(string id, string title, params AgentDemoStep[] steps)
        {
            Id = id;
            Title = title;
            Steps = steps;
        }

        public string Id { get; }
        public string Title { get; }
        public AgentDemoStep[] Steps { get; }

        /// <summary>场景配置的指纹：编号、各步标题和超时，写进事件记录</summary>
        public string Fingerprint
        {
            get
            {
                var text = new StringBuilder(Id);
                foreach (AgentDemoStep step in Steps) text.Append('|').Append(step.Title).Append(':').Append(step.TimeoutSeconds);
                uint hash = 2166136261;
                string value = text.ToString();
                for (int index = 0; index < value.Length; index++) hash = (hash ^ value[index]) * 16777619;
                return hash.ToString("x8");
            }
        }
    }

    /// <summary>三个演示场景</summary>
    public static class AgentDemoScenes
    {
        public const float MoveMeters = 3f;

        public static readonly AgentDemoScene AutonomousSelection = new AgentDemoScene("autonomous-selection", "自主选目标",
            new AgentDemoStep("候选与拆项",
                "开局后，智能体收集视野内的候选群，按同一个公式给每个候选打分：基础分减去风险和路程的扣分，再加上各项加成。",
                20f, run => run.HasDecisionRound),
            new AgentDemoStep("选出最高分",
                "分数最高的候选被选中。超过风险上限或在图上不可达的候选被排除，并写明原因。",
                10f, run => run.HasChosenCandidate),
            new AgentDemoStep("规划路线并出发",
                "智能体在决策图上规划到目标群的路线，路线经过的敌人群会计入风险。路线确定后立即出发。",
                20f, run => run.RouteActive(AgentRouteSource.Autonomous) && run.MovedSinceStart(MoveMeters)));

        public static readonly AgentDemoScene AmbushResume = new AgentDemoScene("ambush-resume", "遇袭挂起恢复",
            new AgentDemoStep("指挥员命令执行",
                "指挥员经地图给智能体下达一条路线，指令槽里是指挥员命令，状态机进入导航。",
                // 布景：把智能体移到一个附近有敌人的资源群前方，再下达去这个群的路线
                30f, run => run.RouteActive(AgentRouteSource.Player) && run.MovedSinceStep(MoveMeters) && run.RememberCommand(),
                run => run.SubmitAmbushRoute()),
            new AgentDemoStep("遇袭挂起与反击",
                "智能体正在执行指挥员下达的命令。受到攻击时，这条命令被移进挂起槽，反击命令接管行为树。",
                30f, run => run.IsRetaliatingOverRememberedCommand(),
                run => run.ArmAmbush()),
            new AgentDemoStep("同一条命令恢复",
                "战斗结束后，挂起槽里的同一条命令回到活动槽，路线从中断处继续执行。",
                90f, run => run.RememberedCommandResumed()));

        public static readonly AgentDemoScene CommanderReassign = new AgentDemoScene("commander-reassign", "指挥员改派",
            new AgentDemoStep("自主路线",
                "智能体按自己的评分选定目标，正沿自主路线前进。",
                30f, run => run.RouteActive(AgentRouteSource.Autonomous) && run.MovedSinceStart(MoveMeters) && run.RememberRoute()),
            new AgentDemoStep("指挥员改派",
                "指挥员在地图上点了另一个群，新的路线请求优先于自主路线。",
                15f, run => run.CommanderRouteReplacedRemembered(),
                run => run.SubmitReassignRoute()),
            new AgentDemoStep("新路线执行",
                "原来的自主路线被替换，智能体转向指挥员指定的群。",
                30f, run => run.RouteActive(AgentRouteSource.Player) && run.MovedSinceStep(MoveMeters)));

        public static readonly AgentDemoScene[] All = { AutonomousSelection, AmbushResume, CommanderReassign };
    }
}
#endif

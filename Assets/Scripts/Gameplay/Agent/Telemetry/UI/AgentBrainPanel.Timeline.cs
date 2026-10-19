#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Gameplay.Agent.Commands;
using TMPro;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>右栏底部的时间线：从评分区下方排到面板底部，能放几条放几条；最新的在上，状态转移、指令事件和自主选中目标各用一种颜色</summary>
    public sealed partial class AgentBrainPanel
    {
        private const float TimelineRowHeight = 26, TimelineBottom = 656;
        public const int TimelineRows = 12;

        private static readonly string MutedHex = "#" + ColorUtility.ToHtmlStringRGB(Style.Muted);

        private readonly TMP_Text[] _timelineTimes = new TMP_Text[TimelineRows], _timelineTexts = new TMP_Text[TimelineRows];

        private void BuildTimeline()
        {
            for (int row = 0; row < TimelineRows; row++)
            {
                _timelineTimes[row] = Label(_right, string.Empty, 16, 0, 60, TimelineRowHeight, Style.TinySize, Style.Muted, Style.MonoFont);
                _timelineTexts[row] = Label(_right, string.Empty, 98, 0, 262, TimelineRowHeight, 13, Style.Ink);
                _timelineTexts[row].richText = true;
                _timelineTexts[row].overflowMode = TextOverflowModes.Ellipsis;
                _timelineTimes[row].name = "Timeline_Row_" + row + "_Time";
                _timelineTexts[row].name = "Timeline_Row_" + row + "_Text";
            }
        }

        private void RefreshTimeline(float scoresBottom)
        {
            float rule = scoresBottom + 14, top = rule + 10;
            _rightVectors.Fill(16, rule, 344, 1, Style.Rule);
            int fit = Mathf.Clamp(Mathf.FloorToInt((TimelineBottom - top) / TimelineRowHeight), 0, TimelineRows);
            int count = _recorder != null ? Mathf.Min(_recorder.TimelineCount, fit) : 0;
            // 显示自本场景载入起的时间
            double origin = Time.timeAsDouble - Time.timeSinceLevelLoadAsDouble;
            for (int row = 0; row < TimelineRows; row++)
            {
                bool visible = row < count;
                _timelineTimes[row].gameObject.SetActive(visible);
                _timelineTexts[row].gameObject.SetActive(visible);
                if (!visible) continue;

                float y = top + row * TimelineRowHeight;
                _timelineTimes[row].rectTransform.anchoredPosition = new Vector2(16, -y);
                _timelineTexts[row].rectTransform.anchoredPosition = new Vector2(98, -y);
                AgentBrainTimelineEntry entry = _recorder.GetTimeline(row);
                SetText(_timelineTimes[row], Clock(entry.TimeSeconds - origin));
                SetText(_timelineTexts[row], Describe(entry, out Color dot));
                _rightVectors.Fill(82, y + 9, 8, 8, dot);
            }
        }

        private static string Clock(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int minutes = (int)(seconds / 60);
            return minutes.ToString("00") + ":" + (seconds - minutes * 60).ToString("00.0");
        }

        private string Describe(in AgentBrainTimelineEntry entry, out Color dot)
        {
            switch (entry.Kind)
            {
                case AgentBrainTimelineKind.Transition:
                    dot = Style.Teal;
                    return AgentBrainText.State(entry.From) + " → " + AgentBrainText.State(entry.To) + Quiet(AgentBrainText.Transition(entry.Transition));
                case AgentBrainTimelineKind.Selection:
                    dot = Style.Success;
                    return "自主选中" + Quiet(_names.Of(entry.Target) + " · " + entry.Score.ToString("0.0"));
                default:
                    return DescribeDirective(entry, out dot);
            }
        }

        private string DescribeDirective(in AgentBrainTimelineEntry entry, out Color dot)
        {
            string kind = AgentBrainText.DirectiveKind(entry.Directive.DirectiveType);
            string source = AgentBrainText.DirectiveSource(entry.Directive);
            string target = DirectiveTarget(entry.Directive);
            switch (entry.Stage)
            {
                case AgentDirectiveStage.Accepted:
                    dot = source == "指挥员" ? Style.Violet : source == "反击" ? Style.Amber : Style.Success;
                    return source == "反击" ? "开始反击" + Quiet(target)
                        : source == "指挥员" ? "指挥员派出" + kind + Quiet(target)
                        : "自主" + kind + Quiet(target);
                case AgentDirectiveStage.Suspended:
                    dot = Style.Amber;
                    return kind + "命令挂起" + Quiet("反击接管");
                case AgentDirectiveStage.Resumed:
                    dot = Style.Teal;
                    return kind + "命令恢复" + Quiet(target);
                case AgentDirectiveStage.Completed:
                    dot = Style.Success;
                    return kind + "完成" + Quiet(target);
                case AgentDirectiveStage.Failed:
                    dot = Style.Failure;
                    return kind + "失败" + Quiet(Failure(entry.Failure));
                default:
                    dot = Style.Dim;
                    return kind + "命令取消" + Quiet(Failure(entry.Failure));
            }
        }

        private static string Quiet(string text) => string.IsNullOrEmpty(text) ? string.Empty : " <color=" + MutedHex + ">" + text + "</color>";

        private static string Failure(AgentDirectiveFailure failure)
        {
            switch (failure)
            {
                case AgentDirectiveFailure.Unreachable: return "不可达";
                case AgentDirectiveFailure.LostSight: return "失去视野";
                case AgentDirectiveFailure.NoProgress: return "没有进展";
                case AgentDirectiveFailure.AttackUnavailable: return "攻击不可用";
                case AgentDirectiveFailure.Superseded: return "被新命令替换";
                case AgentDirectiveFailure.TargetCompleted: return "目标已完成";
                case AgentDirectiveFailure.InvalidTarget: return "目标无效";
                case AgentDirectiveFailure.NavigationNotReady: return "导航未就绪";
                case AgentDirectiveFailure.AgentUnavailable: return "智能体不可用";
                case AgentDirectiveFailure.NoAgent: return "没有智能体";
                default: return string.Empty;
            }
        }
    }
}
#endif

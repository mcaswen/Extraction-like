#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using TMPro;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 右栏上半：活动槽和挂起槽（命令种类、来源和目标），以及最近一轮目标评分。
    /// 每个候选一行，堆叠条按公式拆项：基础分、当前目标加成、撤离加成，再从顶端扣掉风险和路程，竖线是总分；被排除的候选写原因。
    /// 图例紧跟在最后一行之后，候选少时空出的高度留给下面的时间线。
    /// </summary>
    public sealed partial class AgentBrainPanel
    {
        private const float SlotY = 56, SlotW = 168, SlotH = 62;
        private const float ScoreTitleY = 136, FormulaY = 158, ScoreRowsY = 184, ScoreRowHeight = 44;
        private const float ScoreNameX = 16, ScoreBarX = 154, ScoreBarW = 156, ScoreValueX = 320, ScoreBarH = 14;
        public const int MaxScoreRows = 6;

        private static readonly string[] LegendNames = { "基础分", "当前目标", "撤离加成", "风险扣分", "路程扣分" };

        private readonly TMP_Text[] _slotTags = new TMP_Text[2], _slotWhat = new TMP_Text[2], _slotTarget = new TMP_Text[2];
        private readonly TMP_Text[] _scoreNames = new TMP_Text[MaxScoreRows], _scoreValues = new TMP_Text[MaxScoreRows], _scoreReasons = new TMP_Text[MaxScoreRows];
        private readonly TMP_Text[] _legendLabels = new TMP_Text[5];
        private readonly int[] _scoreOrder = new int[AgentDecisionRoundRecord.CandidateCapacity];
        private readonly string[] _scoreLabels = new string[MaxScoreRows];
        private TMP_Text _formula, _scoreEmpty;

        private void BuildSlots()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                float x = 16 + slot * (SlotW + 8);
                string side = slot == 0 ? "Active" : "Held";
                _slotTags[slot] = Label(_right, slot == 0 ? "活动" : "挂起", x + 10, SlotY + 7, SlotW - 20, 14, Style.TinySize, slot == 0 ? Style.Teal : Style.Amber);
                _slotWhat[slot] = Label(_right, "—", x + 10, SlotY + 22, SlotW - 20, 20, Style.SectionTitleSize, Style.Ink, Style.HeadingFont);
                _slotTarget[slot] = Label(_right, string.Empty, x + 10, SlotY + 41, SlotW - 20, 18, Style.SmallSize, Style.Muted);
                _slotTarget[slot].overflowMode = TextOverflowModes.Ellipsis;
                _slotTags[slot].name = "Slot_" + side + "_Tag";
                _slotWhat[slot].name = "Slot_" + side + "_What";
                _slotTarget[slot].name = "Slot_" + side + "_Target";
            }
        }

        private void BuildScores()
        {
            Label(_right, "目标评分", ScoreNameX, ScoreTitleY, 200, 17, Style.SmallSize, Style.Dim).name = "Score_Title";
            _formula = Label(_right, string.Empty, ScoreNameX, FormulaY, 344, 16, Style.SmallSize, Style.Muted);
            _formula.name = "Score_Formula";
            _scoreEmpty = Label(_right, "尚无评分", ScoreNameX, ScoreRowsY, 200, ScoreRowHeight, Style.BodySize, Style.Dim);
            _scoreEmpty.name = "Score_Empty";
            for (int row = 0; row < MaxScoreRows; row++)
            {
                float y = ScoreRowsY + row * ScoreRowHeight;
                _scoreNames[row] = Label(_right, string.Empty, ScoreNameX, y, ScoreBarX - ScoreNameX - 8, ScoreRowHeight, Style.BodySize, Style.Muted);
                _scoreNames[row].overflowMode = TextOverflowModes.Ellipsis;
                _scoreValues[row] = Label(_right, string.Empty, ScoreValueX, y, 40, ScoreRowHeight, 15, Style.Ink, Style.MonoFont, TextAlignmentOptions.MidlineRight);
                _scoreReasons[row] = Label(_right, string.Empty, ScoreBarX, y, ScoreValueX - ScoreBarX - 6, ScoreRowHeight, 13, Style.Dim);
                _scoreNames[row].name = "Score_Row_" + row + "_Name";
                _scoreValues[row].name = "Score_Row_" + row + "_Value";
                _scoreReasons[row].name = "Score_Row_" + row + "_Reason";
            }

            for (int item = 0; item < LegendNames.Length; item++)
            {
                _legendLabels[item] = Label(_right, LegendNames[item], 0, 0, 120, 18, Style.SmallSize, Style.Muted);
                _legendLabels[item].name = "Score_Legend_" + item;
            }
        }

        private void RefreshSlots()
        {
            var lifecycle = _recorder != null && _recorder.Pawn != null ? _recorder.Pawn.DirectiveLifecycle : null;
            SetSlot(0, lifecycle?.Active);
            SetSlot(1, lifecycle?.SuspendedDirective);
            for (int slot = 0; slot < 2; slot++)
            {
                float x = 16 + slot * (SlotW + 8);
                if (slot == 0)
                {
                    _rightVectors.Fill(x, SlotY, SlotW, SlotH, Style.ActiveSlotFill);
                    _rightVectors.Outline(x, SlotY, SlotW, SlotH, new Color(Style.Teal.r, Style.Teal.g, Style.Teal.b, .6f));
                }
                else
                {
                    _rightVectors.Fill(x, SlotY, SlotW, SlotH, Style.HeldSlotFill);
                    _rightVectors.Outline(x, SlotY, SlotW, SlotH, new Color(Style.Amber.r, Style.Amber.g, Style.Amber.b, .7f), 1f, true);
                }
            }
        }

        private void SetSlot(int slot, AgentDirectiveRequest? request)
        {
            if (!request.HasValue)
            {
                SetText(_slotWhat[slot], "—");
                SetText(_slotTarget[slot], string.Empty);
                return;
            }

            AgentDirectiveRequest value = request.Value;
            SetText(_slotWhat[slot], AgentBrainText.DirectiveKind(value.DirectiveType));
            string target = DirectiveTarget(value);
            SetText(_slotTarget[slot], AgentBrainText.DirectiveSource(value) + (target.Length > 0 ? " · " + target : string.Empty));
        }

        // 目标已经销毁（例如敌人被击败）时返回空，不显示占位符
        private string DirectiveTarget(in AgentDirectiveRequest request)
        {
            string target = request.RouteContext.IsValid ? _names.Node(request.RouteContext.NodeId) : _names.Of(request.TargetObject);
            return target == "—" ? string.Empty : target;
        }

        /// <returns>评分区（含图例）的底边</returns>
        private float RefreshScores()
        {
            AgentDecisionConfig config = _recorder != null ? _recorder.DecisionConfig : null;
            SetText(_formula, config == null ? string.Empty
                : "S = B - λ(" + config.RiskPenaltyWeight.ToString("0.##") + "R + " + config.DistancePenaltyWeight.ToString("0.##") + "D) + 加成");

            AgentDecisionRoundRecord round = _recorder != null ? _recorder.GetRound(0) : null;
            int count = round != null ? SortCandidates(round) : 0;
            _scoreEmpty.gameObject.SetActive(count == 0);

            float maxTop = 1f;
            for (int row = 0; row < count; row++)
            {
                AgentDecisionCandidateRow candidate = round.GetRow(_scoreOrder[row]);
                if (Accepted(candidate.Verdict)) maxTop = Mathf.Max(maxTop, Top(candidate.Breakdown));
            }

            float scale = ScoreBarW / maxTop;
            for (int row = 0; row < MaxScoreRows; row++)
            {
                bool visible = row < count;
                _scoreNames[row].gameObject.SetActive(visible);
                _scoreValues[row].gameObject.SetActive(visible);
                _scoreReasons[row].gameObject.SetActive(false);
                if (!visible) continue;

                AgentDecisionCandidateRow candidate = round.GetRow(_scoreOrder[row]);
                float y = ScoreRowsY + row * ScoreRowHeight;
                bool chosen = candidate.Verdict == AgentCandidateVerdict.Chosen;
                SetText(_scoreNames[row], _scoreLabels[row]);
                if (!Accepted(candidate.Verdict))
                {
                    _scoreNames[row].color = Style.Dim;
                    _scoreValues[row].color = Style.Dim;
                    _scoreValues[row].font = Style.BodyFont;
                    SetText(_scoreValues[row], "—");
                    _scoreReasons[row].gameObject.SetActive(true);
                    SetText(_scoreReasons[row], Exclusion(candidate, config));
                    continue;
                }

                if (chosen) _rightVectors.Fill(10, y, 356, ScoreRowHeight, Style.ActiveSlotFill);
                _scoreNames[row].color = chosen ? Style.Ink : Style.Muted;
                _scoreValues[row].color = chosen ? Style.Teal : Style.Ink;
                _scoreValues[row].font = Style.MonoFont;
                SetText(_scoreValues[row], candidate.Breakdown.Total.ToString("0.0"));
                DrawBar(candidate.Breakdown, y + (ScoreRowHeight - ScoreBarH) * .5f, scale);
            }

            return DrawLegend(config, ScoreRowsY + Mathf.Max(1, count) * ScoreRowHeight + 10);
        }

        private static bool Accepted(AgentCandidateVerdict verdict) => verdict == AgentCandidateVerdict.Chosen || verdict == AgentCandidateVerdict.NotBest;

        private static float Top(in AgentScoreBreakdown b) => b.BaseScore + b.CurrentTargetBonus + b.LowHealthBonus + b.CarriedValueBonus;

        // 参与比较的按总分从高到低，排除的跟在后面；同名的（同一敌人群里的几个敌人）只留排在前面的一个；最多 MaxScoreRows 行
        private int SortCandidates(AgentDecisionRoundRecord round)
        {
            int count = 0;
            for (int pass = 0; pass < 2; pass++)
            for (int index = 0; index < round.Count && count < _scoreOrder.Length; index++)
            {
                AgentDecisionCandidateRow candidate = round.GetRow(index);
                if (Accepted(candidate.Verdict) != (pass == 0)) continue;
                int position = count++;
                if (pass == 0)
                    while (position > 0 && round.GetRow(_scoreOrder[position - 1]).Breakdown.Total < candidate.Breakdown.Total)
                    {
                        _scoreOrder[position] = _scoreOrder[position - 1];
                        position--;
                    }
                _scoreOrder[position] = index;
            }

            int kept = 0;
            for (int index = 0; index < count && kept < MaxScoreRows; index++)
            {
                string name = CandidateName(round.GetRow(_scoreOrder[index]));
                bool duplicate = false;
                for (int previous = 0; previous < kept && !duplicate; previous++) duplicate = _scoreLabels[previous] == name;
                if (duplicate) continue;
                _scoreLabels[kept] = name;
                _scoreOrder[kept++] = _scoreOrder[index];
            }
            return kept;
        }

        private string CandidateName(in AgentDecisionCandidateRow candidate)
        {
            string name = _names.Of(candidate.TargetObject);
            return name == "—" ? AgentBrainText.TargetKind(candidate.Kind) : name;
        }

        private static string Exclusion(in AgentDecisionCandidateRow candidate, AgentDecisionConfig config)
        {
            if (candidate.Verdict == AgentCandidateVerdict.RiskOverThreshold && config != null)
                return "风险 " + candidate.Breakdown.Risk.ToString("0.0") + " 超过上限 " + config.GetRiskThreshold(candidate.Kind).ToString("0.0");
            return AgentBrainText.Verdict(candidate.Verdict);
        }

        private void DrawBar(in AgentScoreBreakdown b, float y, float scale)
        {
            _rightVectors.Fill(ScoreBarX, y, ScoreBarW, ScoreBarH, Style.Track);
            float baseEnd = Mathf.Max(0, b.BaseScore);
            float bonusEnd = baseEnd + Mathf.Max(0, b.CurrentTargetBonus);
            float top = bonusEnd + Mathf.Max(0, b.LowHealthBonus + b.CarriedValueBonus);
            float total = Mathf.Clamp(b.Total, 0, top);
            float riskEnd = Mathf.Min(top, total + Mathf.Max(0, b.RiskPenalty));
            Segment(0, baseEnd, y, scale, Style.Teal, false);
            Segment(baseEnd, bonusEnd, y, scale, Style.Violet, false);
            Segment(bonusEnd, top, y, scale, Style.Success, false);
            Segment(total, riskEnd, y, scale, Style.Amber, true);
            Segment(riskEnd, top, y, scale, Style.Muted, true);
            _rightVectors.Fill(ScoreBarX + Mathf.Min(ScoreBarW, total * scale) - 1, y - 4, 2, ScoreBarH + 8, Style.Ink);
        }

        private void Segment(float from, float to, float y, float scale, Color tint, bool hatched)
        {
            float x0 = Mathf.Min(ScoreBarW, from * scale), x1 = Mathf.Min(ScoreBarW, to * scale);
            if (x1 - x0 < .5f) return;
            if (!hatched)
            {
                _rightVectors.Fill(ScoreBarX + x0, y, x1 - x0, ScoreBarH, tint);
                return;
            }

            _rightVectors.Fill(ScoreBarX + x0, y, x1 - x0, ScoreBarH, new Color(.035f, .063f, .09f));
            _rightVectors.Hatch(ScoreBarX + x0, y, x1 - x0, ScoreBarH, tint);
        }

        // 图例按文字实际宽度换行，返回底边
        private float DrawLegend(AgentDecisionConfig config, float top)
        {
            SetText(_legendLabels[1], config != null ? "当前目标 +" + config.CurrentTargetBonus.ToString("0.#") : LegendNames[1]);
            float x = ScoreNameX, y = top;
            for (int item = 0; item < LegendNames.Length; item++)
            {
                float width = 16 + _legendLabels[item].preferredWidth;
                if (x + width > 360 && x > ScoreNameX)
                {
                    x = ScoreNameX;
                    y += 22;
                }

                float swatchY = y + 4;
                switch (item)
                {
                    case 0: _rightVectors.Fill(x, swatchY, 12, 10, Style.Teal); break;
                    case 1: _rightVectors.Fill(x, swatchY, 12, 10, Style.Violet); break;
                    case 2: _rightVectors.Fill(x, swatchY, 12, 10, Style.Success); break;
                    case 3: _rightVectors.Hatch(x, swatchY, 12, 10, Style.Amber); break;
                    default: _rightVectors.Hatch(x, swatchY, 12, 10, Style.Muted); break;
                }

                _legendLabels[item].rectTransform.anchoredPosition = new Vector2(x + 16, -y);
                x += width + 14;
            }
            return y + 18;
        }
    }
}
#endif

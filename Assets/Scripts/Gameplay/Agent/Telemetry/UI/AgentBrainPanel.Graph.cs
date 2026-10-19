#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Core.StateMachine.Runtime;
using TMPro;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 状态机节点图：局内六个叶子状态排成两行三列。只画当前状态的出边，标优先级和本帧条件真假；
    /// 刚发生的转移用青色画出并走一个光点，转移来源用橙色虚框标为上一状态。连线横竖走向，跨行的经两行之间的通道。
    /// </summary>
    public sealed partial class AgentBrainPanel
    {
        private const float GraphX = 20, GraphY = 64, GraphW = 500, GraphH = 232;
        private const float NodeW = 120, NodeH = 40;
        private const int MaxEdges = 8;

        private static readonly string[] GraphStates = { "Explore", "Navigate", "InvestigateEnemySource", "SearchResource", "Combat", "Extraction" };

        private readonly TMP_Text[] _stateLabels = new TMP_Text[6];
        private readonly TMP_Text[] _priorityLabels = new TMP_Text[MaxEdges];
        private readonly List<StateTransition> _outEdges = new List<StateTransition>(MaxEdges);
        private readonly Vector2[] _path = new Vector2[4];
        private readonly Vector2[] _firedPath = new Vector2[4];
        private TMP_Text _previousTag, _firedLabel;
        private AgentBrainVectorGraphic _pulse;
        private int _firedPathCount;
        private double _firedTime = double.NegativeInfinity;

        private static Vector2 NodeOrigin(int slot) => new Vector2(GraphX + 14 + slot % 3 * 176, GraphY + 40 + slot / 3 * 124);

        private static int Slot(StateMachineState state)
        {
            if (state == null) return -1;
            for (int index = 0; index < GraphStates.Length; index++)
                if (GraphStates[index] == state.StateName) return index;
            return -1;
        }

        private void BuildGraph()
        {
            Label(_left, "局内", GraphX + 12, GraphY + 6, 60, 16, Style.TinySize, Style.Dim).name = "Graph_Parent";
            for (int slot = 0; slot < GraphStates.Length; slot++)
            {
                Vector2 origin = NodeOrigin(slot);
                _stateLabels[slot] = Label(_left, AgentBrainText.StateName(GraphStates[slot]), origin.x, origin.y, NodeW, NodeH, Style.BodySize, Style.Muted, null, TextAlignmentOptions.Center);
                _stateLabels[slot].name = "State_" + GraphStates[slot];
            }

            _previousTag = Label(_left, "上一状态", 0, 0, NodeW, 14, 10, Style.Amber, null, TextAlignmentOptions.Center);
            _previousTag.name = "State_Previous_Tag";
            for (int index = 0; index < MaxEdges; index++)
            {
                _priorityLabels[index] = Label(_left, string.Empty, 0, 0, 30, 14, 10, Style.Muted, Style.MonoFont, TextAlignmentOptions.MidlineRight);
                _priorityLabels[index].name = "Edge_Priority_" + index;
            }

            _firedLabel = Label(_left, string.Empty, 0, 0, 180, 14, Style.TinySize, Style.Teal, null, TextAlignmentOptions.Center);
            _firedLabel.name = "Edge_Fired_Label";

            var pulse = new GameObject("Edge_Pulse", typeof(RectTransform), typeof(AgentBrainVectorGraphic));
            pulse.transform.SetParent(_left, false);
            _pulse = pulse.GetComponent<AgentBrainVectorGraphic>();
            _pulse.raycastTarget = false;
            Rect(_pulse.rectTransform, 0, 0, 16, 16);
            _pulse.Disc(new Vector2(8, 8), 7, new Color(Style.Teal.r, Style.Teal.g, Style.Teal.b, .25f));
            _pulse.Disc(new Vector2(8, 8), 4, Style.Glow);
            _pulse.Commit();
            _pulse.gameObject.SetActive(false);
        }

        private void RefreshGraph()
        {
            _leftVectors.Outline(GraphX, GraphY, GraphW, GraphH, Style.Rule, 1f, true);
            StateMachineState current = _recorder != null ? _recorder.CurrentLeaf : null;
            int currentSlot = Slot(current);

            // 上一状态：最近一次进入当前状态的转移的来源
            int previousSlot = -1;
            StateTransition fired = null;
            _firedTime = double.NegativeInfinity;
            if (_recorder != null && _recorder.TransitionCount > 0)
            {
                AgentStateTransitionRecord last = _recorder.GetTransition(_recorder.TransitionCount - 1);
                if (last.To == current)
                {
                    previousSlot = Slot(last.From);
                    fired = last.Transition;
                    _firedTime = last.TimeSeconds;
                }
            }

            for (int slot = 0; slot < GraphStates.Length; slot++)
            {
                Vector2 o = NodeOrigin(slot);
                bool isCurrent = slot == currentSlot, isPrevious = slot == previousSlot && !isCurrent;
                TMP_Text label = _stateLabels[slot];
                label.font = isCurrent ? Style.HeadingFont : Style.BodyFont;
                label.fontSize = isCurrent ? Style.SectionTitleSize : Style.BodySize;
                label.color = isCurrent || isPrevious ? Style.Ink : Style.Muted;
                label.rectTransform.anchoredPosition = new Vector2(o.x, -(o.y - (isPrevious ? 6 : 0)));
                if (isCurrent)
                {
                    for (int glow = 1; glow <= 3; glow++)
                        _leftVectors.Outline(o.x - glow * 2, o.y - glow * 2, NodeW + glow * 4, NodeH + glow * 4, new Color(Style.Teal.r, Style.Teal.g, Style.Teal.b, .2f / glow), 2f);
                    _leftVectors.Fill(o.x, o.y, NodeW, NodeH, Style.SelectedFill);
                    _leftVectors.Outline(o.x, o.y, NodeW, NodeH, Style.Teal, 2f);
                }
                else
                {
                    _leftVectors.Fill(o.x, o.y, NodeW, NodeH, Style.NodeFill);
                    if (isPrevious) _leftVectors.Outline(o.x, o.y, NodeW, NodeH, new Color(Style.Amber.r, Style.Amber.g, Style.Amber.b, .85f), 1f, true);
                    else _leftVectors.Outline(o.x, o.y, NodeW, NodeH, Style.NodeEdge);
                }
            }

            _previousTag.gameObject.SetActive(previousSlot >= 0 && previousSlot != currentSlot);
            if (_previousTag.gameObject.activeSelf)
            {
                Vector2 o = NodeOrigin(previousSlot);
                _previousTag.rectTransform.anchoredPosition = new Vector2(o.x, -(o.y + 22));
            }

            DrawOutEdges(current, currentSlot);
            DrawFiredEdge(fired, previousSlot, currentSlot);
        }

        private void DrawOutEdges(StateMachineState current, int currentSlot)
        {
            _outEdges.Clear();
            for (StateMachineState owner = current; owner != null; owner = owner.ParentState)
            {
                IReadOnlyList<StateTransition> transitions = owner.Transitions;
                for (int index = 0; index < transitions.Count && _outEdges.Count < MaxEdges; index++)
                    if (Slot(transitions[index].TargetState) >= 0 && transitions[index].TargetState != current) _outEdges.Add(transitions[index]);
            }

            // 从同一条边离开的出边按目标横坐标分开出口，经通道的出边各占一条通道
            int lane = 0;
            for (int index = 0; index < MaxEdges; index++)
            {
                TMP_Text label = _priorityLabels[index];
                if (index >= _outEdges.Count || currentSlot < 0)
                {
                    label.gameObject.SetActive(false);
                    continue;
                }

                StateTransition transition = _outEdges[index];
                int target = Slot(transition.TargetState);
                float exitOffset = ExitOffset(currentSlot, target);
                int count = Route(currentSlot, target, exitOffset, -7f, ChannelLane(currentSlot, target, ref lane), _path);
                bool evaluated = TryGetEvaluation(transition, out bool satisfied);
                Color line = satisfied ? Style.Teal : Style.EdgeLine;
                for (int point = 1; point < count; point++) _leftVectors.Line(_path[point - 1], _path[point], line);
                _leftVectors.Arrow(_path[count - 1], _path[count - 1] - _path[count - 2], 6f, satisfied ? Style.Teal : new Color(Style.Muted.r, Style.Muted.g, Style.Muted.b, .8f));

                // 优先级标在最长一段的中点旁：横段在上方，竖段在左侧
                LongestSegment(count, out Vector2 a, out Vector2 b);
                Vector2 middle = (a + b) * .5f;
                bool horizontal = Mathf.Abs(a.y - b.y) < .5f;
                Vector2 anchor = horizontal ? new Vector2(middle.x - 4, middle.y - 9) : new Vector2(middle.x - 10, middle.y);
                label.gameObject.SetActive(true);
                SetText(label, transition.Priority.ToString());
                label.color = evaluated ? Style.Muted : Style.Dim;
                label.rectTransform.anchoredPosition = new Vector2(anchor.x - 30, -(anchor.y - 7));
                if (!evaluated) continue;
                Vector2 mark = new Vector2(anchor.x + 5, anchor.y);
                if (satisfied) _leftVectors.Check(mark, 8f, Style.Success, 1.3f);
                else _leftVectors.Cross(mark, 8f, Style.Muted, 1.2f);
            }
        }

        private void DrawFiredEdge(StateTransition fired, int previousSlot, int currentSlot)
        {
            bool recent = fired != null && previousSlot >= 0 && currentSlot >= 0 && previousSlot != currentSlot &&
                          Time.timeAsDouble - _firedTime < Style.FiredTransitionSeconds;
            _firedLabel.gameObject.SetActive(recent);
            _firedPathCount = 0;
            if (!recent)
            {
                _pulse.gameObject.SetActive(false);
                return;
            }

            int lane = 5;
            _firedPathCount = Route(previousSlot, currentSlot, 16f, 7f, ChannelLane(previousSlot, currentSlot, ref lane), _firedPath);
            for (int point = 1; point < _firedPathCount; point++) _leftVectors.Line(_firedPath[point - 1], _firedPath[point], Style.Teal, 2f);
            _leftVectors.Arrow(_firedPath[_firedPathCount - 1], _firedPath[_firedPathCount - 1] - _firedPath[_firedPathCount - 2], 7f, Style.Teal);

            SetText(_firedLabel, AgentBrainText.Transition(fired) + " · " + fired.Priority);
            Vector2 from = _firedPath[0], to = _firedPath[_firedPathCount - 1];
            bool horizontal = _firedPathCount == 2 && Mathf.Abs(from.y - to.y) < .5f;
            float labelX = horizontal ? (from.x + to.x) * .5f : to.x;
            float labelY = horizontal ? NodeOrigin(currentSlot).y + NodeH + 6 : to.y + (to.y > from.y ? -24 : 8);
            _firedLabel.rectTransform.anchoredPosition = new Vector2(labelX - 90, -labelY);
            _leftVectors.Check(new Vector2(labelX + _firedLabel.preferredWidth * .5f + 7, labelY + 7), 8f, Style.Teal, 1.3f);
            _pulse.gameObject.SetActive(true);
            AnimatePulse();
        }

        // 光点每帧只移动位置，不重建网格
        private void AnimatePulse()
        {
            if (_pulse == null || !_pulse.gameObject.activeSelf || _firedPathCount < 2) return;
            float length = 0;
            for (int point = 1; point < _firedPathCount; point++) length += Vector2.Distance(_firedPath[point - 1], _firedPath[point]);
            float distance = Mathf.Repeat(Time.unscaledTime / Style.PulseSeconds, 1f) * length;
            Vector2 position = _firedPath[_firedPathCount - 1];
            for (int point = 1; point < _firedPathCount; point++)
            {
                float segment = Vector2.Distance(_firedPath[point - 1], _firedPath[point]);
                if (distance <= segment)
                {
                    position = Vector2.Lerp(_firedPath[point - 1], _firedPath[point], segment > 0 ? distance / segment : 0);
                    break;
                }
                distance -= segment;
            }
            _pulse.rectTransform.anchoredPosition = new Vector2(position.x - 8, -(position.y - 8));
        }

        private bool TryGetEvaluation(StateTransition transition, out bool satisfied)
        {
            satisfied = false;
            for (int index = 0; index < _recorder.EvaluatedCount; index++)
            {
                if (_recorder.GetEvaluatedTransition(index) != transition) continue;
                satisfied = _recorder.GetEvaluatedSatisfied(index);
                return true;
            }
            return false;
        }

        // 同一侧离开的出边按目标横坐标排开：左边的目标从左侧出口走
        private float ExitOffset(int from, int to)
        {
            if (from / 3 != to / 3) return (to % 3 - from % 3) * 24f;
            return Mathf.Abs(to % 3 - from % 3) == 2 ? (to % 3 > from % 3 ? 36f : -36f) : 0f;
        }

        private static float ChannelLane(int from, int to, ref int lane)
        {
            bool needsChannel = from % 3 != to % 3 && (from / 3 != to / 3 || Mathf.Abs(from % 3 - to % 3) == 2);
            if (!needsChannel) return 0f;
            float y = GraphY + 92 + Mathf.Min(lane, 5) * 11f;
            lane++;
            return y;
        }

        /// <summary>
        /// 横竖走线：同行相邻直接横连（sideOffset 区分两个方向）；同列直接竖连；其余经两行之间的通道。
        /// </summary>
        private static int Route(int from, int to, float exitOffset, float sideOffset, float laneY, Vector2[] path)
        {
            Vector2 a = NodeOrigin(from), b = NodeOrigin(to);
            int rowA = from / 3, rowB = to / 3, colA = from % 3, colB = to % 3;
            if (rowA == rowB && Mathf.Abs(colA - colB) == 1)
            {
                float y = a.y + NodeH * .5f + sideOffset;
                bool right = colB > colA;
                path[0] = new Vector2(right ? a.x + NodeW : a.x, y);
                path[1] = new Vector2(right ? b.x : b.x + NodeW, y);
                return 2;
            }

            float centerA = a.x + NodeW * .5f, centerB = b.x + NodeW * .5f;
            if (colA == colB)
            {
                float x = centerA + exitOffset * .5f;
                bool down = rowB > rowA;
                path[0] = new Vector2(x, down ? a.y + NodeH : a.y);
                path[1] = new Vector2(x, down ? b.y : b.y + NodeH);
                return 2;
            }

            // 上行从下边出、下行从上边出，进入目标同理
            float exitY = rowA == 0 ? a.y + NodeH : a.y;
            float enterY = rowB == 0 ? b.y + NodeH : b.y;
            float exitX = centerA + exitOffset, enterX = centerB - Mathf.Sign(colB - colA) * 12f;
            path[0] = new Vector2(exitX, exitY);
            path[1] = new Vector2(exitX, laneY);
            path[2] = new Vector2(enterX, laneY);
            path[3] = new Vector2(enterX, enterY);
            return 4;
        }

        private void LongestSegment(int count, out Vector2 a, out Vector2 b)
        {
            a = _path[0];
            b = _path[1];
            float best = -1;
            for (int point = 1; point < count; point++)
            {
                float length = Vector2.Distance(_path[point - 1], _path[point]);
                if (length <= best) continue;
                best = length;
                a = _path[point - 1];
                b = _path[point];
            }
        }
    }
}
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Core.BehaviorTree.Nodes;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Runtime;
using TMPro;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 当前状态的行为树：按缩进逐行显示，本帧运行过的节点按结果点亮（运行中、成功、失败）。
    /// 只展开正在运行的组合节点；已结束和本帧没运行到的组合节点折叠成一行，本帧没运行到的叶子不显示。
    /// 行数多时把行距从 25 压到 18；仍放不下就让正在运行的最深节点保持可见。
    /// </summary>
    public sealed partial class AgentBrainPanel
    {
        private const float TreeX = 20, TreeY = 310, TreeW = 500, TreeAreaHeight = 504, TreeRowHeight = 25, TreeMinRowHeight = 18, TreeIndent = 16;

        /// <summary>行标签的数量：按最小行距能放下的行数</summary>
        public const int MaxTreeRows = 28;

        private enum RowState : byte { Idle, Running, Success, Failure }

        private struct TreeRow
        {
            public BehaviorNode Node;
            public int Depth;
            public RowState State;
            public bool Collapsed;
        }

        private readonly TreeRow[] _treeRows = new TreeRow[128];
        private readonly TMP_Text[] _treeLabels = new TMP_Text[MaxTreeRows];
        private TMP_Text _treeSide, _treeEmpty;
        private int _treeRowCount, _tickVersion;

        private void BuildTree()
        {
            for (int row = 0; row < MaxTreeRows; row++)
            {
                _treeLabels[row] = Label(_left, string.Empty, TreeX, TreeY + row * TreeRowHeight, TreeW - 90, TreeRowHeight, Style.BodySize, Style.Ink);
                _treeLabels[row].name = "Tree_Row_" + row.ToString("00");
                _treeLabels[row].richText = true;
            }

            _treeSide = Label(_left, string.Empty, TreeX + TreeW - 120, 0, 114, TreeRowHeight, Style.SmallSize, Style.Teal, null, TextAlignmentOptions.MidlineRight);
            _treeSide.name = "Tree_Focus_Detail";
            _treeEmpty = Label(_left, "等待智能体", TreeX, TreeY, TreeW, TreeRowHeight, Style.BodySize, Style.Dim);
            _treeEmpty.name = "Tree_Empty";
        }

        private void RefreshTree()
        {
            StateMachineState leaf = _recorder != null ? _recorder.CurrentLeaf : null;
            BehaviorTreeType tree = leaf != null ? leaf.BoundBehaviorTree : null;
            _treeRowCount = 0;
            _tickVersion = _recorder != null ? _recorder.TreeTickVersion : 0;
            if (tree != null && tree.RootNode != null) Flatten(tree.RootNode, 0);

            _treeEmpty.gameObject.SetActive(_treeRowCount == 0);
            float rowHeight = Mathf.Clamp(TreeAreaHeight / Mathf.Max(1, _treeRowCount), TreeMinRowHeight, TreeRowHeight);
            int visibleRows = Mathf.Min(MaxTreeRows, Mathf.FloorToInt(TreeAreaHeight / rowHeight));
            int focus = FocusRow();
            int first = focus >= visibleRows ? focus - visibleRows + 2 : 0;
            _treeSide.gameObject.SetActive(false);
            for (int row = 0; row < MaxTreeRows; row++)
            {
                TMP_Text label = _treeLabels[row];
                int index = first + row;
                bool truncated = row == visibleRows - 1 && index < _treeRowCount - 1;
                if (row >= visibleRows || index >= _treeRowCount)
                {
                    label.gameObject.SetActive(false);
                    continue;
                }

                label.gameObject.SetActive(true);
                float y = TreeY + row * rowHeight;
                label.rectTransform.sizeDelta = new Vector2(TreeW - 90, rowHeight);
                label.fontSize = rowHeight < 22 ? 13 : Style.BodySize;
                if (truncated)
                {
                    SetText(label, "…");
                    label.color = Style.Dim;
                    label.rectTransform.anchoredPosition = new Vector2(TreeX + 22, -y);
                    continue;
                }

                TreeRow entry = _treeRows[index];
                float x = TreeX + entry.Depth * TreeIndent;
                if (index == focus)
                {
                    _leftVectors.Fill(TreeX, y, TreeW, rowHeight, Style.FocusRow);
                    string detail = FocusDetail(entry.Node);
                    if (!string.IsNullOrEmpty(detail))
                    {
                        _treeSide.gameObject.SetActive(true);
                        SetText(_treeSide, detail);
                        _treeSide.rectTransform.anchoredPosition = new Vector2(TreeX + TreeW - 120, -y);
                        _treeSide.rectTransform.sizeDelta = new Vector2(114, rowHeight);
                    }
                }

                DrawMark(entry, new Vector2(x + 7, y + rowHeight * .5f));
                label.rectTransform.anchoredPosition = new Vector2(x + 22, -y);
                label.color = entry.State == RowState.Running ? Style.Ink : entry.State == RowState.Idle ? Style.Dim : Style.Muted;
                SetText(label, entry.State == RowState.Idle && entry.Node.ChildCount > 0
                    ? entry.Node.DisplayName + "<size=12>  未运行</size>"
                    : entry.Node.DisplayName);
            }
        }

        private void Flatten(BehaviorNode node, int depth)
        {
            if (node == null || _treeRowCount >= _treeRows.Length) return;
            bool ticked = _tickVersion > 0 && node.LastTickVersion == _tickVersion;
            if (!ticked && node.ChildCount == 0 && depth > 0) return;
            RowState state = !ticked ? RowState.Idle
                : node.LastStatus == BehaviorNodeStatus.Running ? RowState.Running
                : node.LastStatus == BehaviorNodeStatus.Success ? RowState.Success
                : RowState.Failure;
            bool expand = state == RowState.Running && node.ChildCount > 0;
            _treeRows[_treeRowCount++] = new TreeRow { Node = node, Depth = depth, State = state, Collapsed = node.ChildCount > 0 && !expand };
            if (!expand) return;
            for (int index = 0; index < node.ChildCount; index++) Flatten(node.GetChild(index), depth + 1);
        }

        // 正在运行的最深节点
        private int FocusRow()
        {
            int focus = -1, depth = -1;
            for (int index = 0; index < _treeRowCount; index++)
            {
                if (_treeRows[index].State != RowState.Running || _treeRows[index].Depth <= depth) continue;
                focus = index;
                depth = _treeRows[index].Depth;
            }
            return focus;
        }

        private void DrawMark(in TreeRow entry, Vector2 center)
        {
            switch (entry.State)
            {
                case RowState.Running:
                    _leftVectors.Disc(center, 5f, Style.Teal);
                    break;
                case RowState.Success:
                    _leftVectors.Check(center, 12f, Style.Success, 1.6f);
                    break;
                case RowState.Failure:
                    _leftVectors.Cross(center, 12f, Style.Failure, 1.6f);
                    break;
                default:
                    if (entry.Collapsed) _leftVectors.Caret(center, 11f, Style.Dim);
                    else _leftVectors.Ring(center, 4f, Style.Dim, 1.2f);
                    break;
            }
        }

        // 行尾只给等待冷却显示剩余时间
        private string FocusDetail(BehaviorNode node)
        {
            if (node.NodeName != "Combat_WaitCooldown" || _recorder.EngageMemory == null || _recorder.EngageMemory.Combat == null) return null;
            double remaining = _recorder.EngageMemory.Combat.TelemetryNextAttackTime - _recorder.TreeTimeSeconds;
            return remaining > 0 ? "剩 " + remaining.ToString("0.00") + " s" : null;
        }
    }
}
#endif

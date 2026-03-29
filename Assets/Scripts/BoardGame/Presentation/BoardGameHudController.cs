using System.Linq;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 顶层 HUD 刷新控制器
    /// </summary>
    public sealed class BoardGameHudController : MonoBehaviour
    {
        // AI 当前生命值显示文本
        [SerializeField] private TMP_Text _healthText;
        // 当前已携带总收益显示文本
        [SerializeField] private TMP_Text _valueText;
        // 当前背包容量占用显示文本
        [SerializeField] private TMP_Text _capacityText;
        // 当前动作类型显示文本
        [SerializeField] private TMP_Text _actionText;
        // 当前锁定目标节点显示文本
        [SerializeField] private TMP_Text _targetText;
        // 当前路径节点串显示文本
        [SerializeField] private TMP_Text _pathText;
        // 最近一次系统状态消息显示文本
        [SerializeField] private TMP_Text _statusText;
        // 当前是否处于玩家重定向模式的显示文本
        [SerializeField] private TMP_Text _redirectStateText;
        // 当前动作公共进度条填充图
        [SerializeField] private Image _actionProgressFillImage;

        private BoardGamePrototypeController _prototypeController;

        /// <summary>
        /// 绑定运行时总控
        /// </summary>
        public void Bind(BoardGamePrototypeController prototypeController)
        {
            _prototypeController = prototypeController;
            _prototypeController.SessionChanged += Refresh;
            _prototypeController.SelectionChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// 刷新 HUD 文本和进度条
        /// </summary>
        private void Refresh()
        {
            if (_prototypeController == null)
            {
                return;
            }

            BoardGameSessionState sessionState = _prototypeController.SessionState;
            BoardAgentState agentState = sessionState.AgentState;
            BoardNodeRuntimeState targetNode = _prototypeController.GetNodeState(agentState.CurrentTargetNodeId);

            if (_healthText != null)
            {
                _healthText.text = $"HP: {agentState.CurrentHealth}/{agentState.MaxHealth}";
            }

            if (_valueText != null)
            {
                _valueText.text = $"Value: {agentState.InventoryState.TotalValue}";
            }

            if (_capacityText != null)
            {
                _capacityText.text = $"Capacity: {agentState.InventoryState.UsedCapacity:0.0}/{agentState.InventoryState.MaxCapacity:0.0}";
            }

            if (_actionText != null)
            {
                _actionText.text = $"Action: {BoardGameTypes.GetActionLabel(agentState.CurrentActionType)}";
            }

            if (_targetText != null)
            {
                _targetText.text = $"Target: {(targetNode != null ? targetNode.NodeId : "None")}";
            }

            if (_pathText != null)
            {
                string path = agentState.RemainingPathNodeIds.Count == 0
                    ? "None"
                    : string.Join(" -> ", agentState.RemainingPathNodeIds.Select(nodeId =>
                    {
                        BoardNodeRuntimeState nodeState = _prototypeController.GetNodeState(nodeId);
                        return nodeState != null ? nodeState.NodeId : nodeId;
                    }));
                _pathText.text = $"Path: {path}";
            }

            if (_statusText != null)
            {
                _statusText.text = sessionState.StatusMessage;
            }

            if (_redirectStateText != null)
            {
                _redirectStateText.text = "Control: Hover and click a node to redirect";
            }

            if (_actionProgressFillImage != null)
            {
                _actionProgressFillImage.fillAmount = Mathf.Clamp01(agentState.CurrentActionProgress);
            }
        }
    }
}

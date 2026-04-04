using BoardGame.Runtime;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// AI 角色表现组件
    /// </summary>
    public sealed class BoardGameAgentView : MonoBehaviour
    {
        private static readonly Color SelectionHaloColor = new Color(0.22f, 0.62f, 1f, 1f);

        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private TMP_Text _labelText;

        private string _agentId;
        private BoardGameSelectionHalo _selectionHalo;

        public string AgentId => _agentId;

        private void Awake()
        {
            EnsureSelectionHalo();
        }

        /// <summary>
        /// 初始化 Agent 视图的静态身份信息
        /// </summary>
        public void Initialize(string agentId)
        {
            _agentId = agentId ?? string.Empty;
        }

        /// <summary>
        /// 刷新角色当前位置与动作展示
        /// </summary>
        public void Refresh(BoardAgentState agentState, Vector3 displayPosition, bool isSelected)
        {
            transform.position = displayPosition;

            _selectionHalo?.Refresh(isSelected, SelectionHaloColor);

            if (_labelText != null)
            {
                _labelText.text = isSelected
                    ? $"{agentState.DisplayName}\n{BuildFocusedStatusLabel(agentState)}"
                    : agentState.DisplayName;
            }

            if (_bodyRenderer != null)
            {
                _bodyRenderer.color = agentState.IsAlive
                    ? agentState.AgentColor
                    : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
        }

        /// <summary>
        /// 只有当前焦点 Agent 显示详细动作和进度
        /// 非焦点 Agent 只保留名字，避免多人同节点时信息过载
        /// </summary>
        private static string BuildFocusedStatusLabel(BoardAgentState agentState)
        {
            if (agentState == null)
            {
                return string.Empty;
            }

            string actionLabel = BoardGameTypes.GetActionLabel(agentState.CurrentActionType);

            if (agentState.CurrentActionType == BoardActionType.Idle ||
                agentState.CurrentActionType == BoardActionType.Completed ||
                agentState.CurrentActionType == BoardActionType.Downed)
            {
                return $"{actionLabel}  HP {agentState.CurrentHealth}/{agentState.MaxHealth}";
            }

            return $"{actionLabel} {Mathf.Clamp01(agentState.CurrentActionProgress):P0}  HP {agentState.CurrentHealth}/{agentState.MaxHealth}";
        }

        /// <summary>
        /// 自动创建角色蓝色高光外圈
        /// 不再依赖 prefab 额外配置选中渲染器
        /// </summary>
        private void EnsureSelectionHalo()
        {
            if (_bodyRenderer == null)
            {
                return;
            }

            Transform haloTransform = transform.Find("SelectionHalo");

            if (haloTransform == null)
            {
                GameObject haloObject = new GameObject("SelectionHalo");
                haloObject.transform.SetParent(transform, false);
                _selectionHalo = haloObject.AddComponent<BoardGameSelectionHalo>();
            }
            else
            {
                _selectionHalo = haloTransform.GetComponent<BoardGameSelectionHalo>();

                if (_selectionHalo == null)
                {
                    _selectionHalo = haloTransform.gameObject.AddComponent<BoardGameSelectionHalo>();
                }
            }

            _selectionHalo.Initialize(_bodyRenderer, 0.14f, 0.09f);
        }
    }
}

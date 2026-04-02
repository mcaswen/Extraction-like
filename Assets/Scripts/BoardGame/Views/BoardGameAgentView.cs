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

        private BoardGameSelectionHalo _selectionHalo;

        private void Awake()
        {
            EnsureSelectionHalo();
        }

        /// <summary>
        /// 刷新角色当前位置与动作展示
        /// </summary>
        public void Refresh(BoardAgentState agentState, bool isSelected)
        {
            transform.position = agentState.WorldPosition;

            _selectionHalo?.Refresh(isSelected, SelectionHaloColor);

            if (_labelText != null)
            {
                _labelText.text = BoardGameTypes.GetActionLabel(agentState.CurrentActionType);
            }

            if (_bodyRenderer != null)
            {
                _bodyRenderer.color = agentState.IsAlive
                    ? new Color(0.94f, 0.94f, 0.94f, 1f)
                    : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
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

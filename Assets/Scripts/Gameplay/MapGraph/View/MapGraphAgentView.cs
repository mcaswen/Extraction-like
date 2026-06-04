using Gameplay.MapGraph.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>
    /// 抽象图 UI Agent 视图
    /// 只负责显示图上位置、颜色和名称，不显示旧桌游战斗或升级信息
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapGraphAgentView : MonoBehaviour
    {
        [SerializeField] private Image _bodyImage;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Vector2 _defaultSize = new Vector2(24f, 24f);

        private RectTransform _rectTransform;
        private string _agentId;

        /// <summary>
        /// 当前 Agent ID
        /// </summary>
        public string AgentId => _agentId ?? string.Empty;

        /// <summary>
        /// 初始化 Agent 静态身份
        /// </summary>
        /// <param name="agentId"></param>
        public void Initialize(string agentId)
        {
            _agentId = agentId ?? string.Empty;
            EnsureReferences();
            gameObject.name = $"Agent_{AgentId}";
        }

        /// <summary>
        /// 刷新 Agent 图上位置和显示信息
        /// </summary>
        /// <param name="state"></param>
        /// <param name="anchoredPosition"></param>
        public void Refresh(MapGraphAgentRuntimeState state, Vector2 anchoredPosition)
        {
            EnsureReferences();
            _rectTransform.anchoredPosition = anchoredPosition;

            if (_bodyImage != null)
                _bodyImage.color = state != null ? state.AgentColor : Color.white;

            if (_labelText != null)
                _labelText.text = state != null ? state.DisplayName : string.Empty;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform.sizeDelta == Vector2.zero)
                _rectTransform.sizeDelta = _defaultSize;

            if (_bodyImage == null)
                _bodyImage = GetComponent<Image>();
            if (_bodyImage == null)
                _bodyImage = gameObject.AddComponent<Image>();

            _bodyImage.raycastTarget = false;

            if (_labelText == null)
                _labelText = GetComponentInChildren<TMP_Text>();
            if (_labelText == null)
                _labelText = CreateLabel();
        }

        private TMP_Text CreateLabel()
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 3f);
            labelRect.sizeDelta = new Vector2(96f, 22f);

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Bottom;
            label.fontSize = 10f;
            label.color = new Color(0.95f, 0.98f, 1f, 0.96f);
            label.raycastTarget = false;
            return label;
        }
    }
}

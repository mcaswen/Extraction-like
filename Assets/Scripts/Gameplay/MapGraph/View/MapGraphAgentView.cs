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
        [SerializeField] private Image _selectionRingImage;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Vector2 _defaultSize = new Vector2(28f, 28f);
        [SerializeField] private Vector2 _labelSize = new Vector2(112f, 24f);
        [SerializeField] private Color _selectionRingColor = new Color(0.22f, 0.62f, 1f, 0.36f);

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

            if (_selectionRingImage != null)
            {
                bool showRing = state != null && !string.IsNullOrWhiteSpace(state.CurrentTargetNodeId);
                _selectionRingImage.gameObject.SetActive(showRing);
                _selectionRingImage.color = _selectionRingColor;
            }

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
                _bodyImage = ResolveChildImage("Body");

            if (_selectionRingImage == null)
                _selectionRingImage = ResolveChildImage("SelectionRing");

            if (_labelText == null)
                _labelText = GetComponentInChildren<TMP_Text>();

            if (_bodyImage != null)
                _bodyImage.raycastTarget = false;
            if (_selectionRingImage != null)
                _selectionRingImage.raycastTarget = false;
            LayoutImage(_selectionRingImage, Vector2.zero, _defaultSize + new Vector2(10f, 10f), 0);
            if (_selectionRingImage != null)
                _selectionRingImage.gameObject.SetActive(false);
            if (_labelText != null)
                _labelText.transform.SetAsLastSibling();
        }

        private Image ResolveChildImage(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null && existing.TryGetComponent(out Image existingImage))
                return existingImage;

            return null;
        }

        private static void LayoutImage(Image image, Vector2 anchoredPosition, Vector2 size, int siblingIndex)
        {
            if (image == null)
                return;

            RectTransform rectTransform = image.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
            image.transform.SetSiblingIndex(siblingIndex);
        }
    }
}

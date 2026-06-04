using Gameplay.MapGraph.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>
    /// 抽象图 UI 节点视图
    /// 使用 Image 和 TMP_Text 渲染，避免依赖旧 SpriteRenderer 预制体
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapGraphNodeView : MonoBehaviour
    {
        private static readonly Color TargetRingColor = new Color(1f, 0.78f, 0.18f, 0.36f);
        private static readonly Color OccupiedRingColor = new Color(0.24f, 0.78f, 1f, 0.32f);
        private static readonly Color CompletedOverlayColor = new Color(0.08f, 0.08f, 0.08f, 0.46f);
        private static readonly Color TouchedOverlayColor = new Color(1f, 1f, 1f, 0.18f);

        [SerializeField] private Image _bodyImage;
        [SerializeField] private Image _statusOverlayImage;
        [SerializeField] private Image _targetRingImage;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Vector2 _defaultSize = new Vector2(34f, 34f);

        private RectTransform _rectTransform;
        private MapGraphNodeDefinition _nodeDefinition;
        private Color _baseColor = Color.white;

        /// <summary>
        /// 当前节点 ID
        /// </summary>
        public string NodeId => _nodeDefinition != null ? _nodeDefinition.NodeId : string.Empty;

        /// <summary>
        /// 节点 RectTransform
        /// </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = GetComponent<RectTransform>();

                return _rectTransform;
            }
        }

        /// <summary>
        /// 初始化节点静态信息
        /// </summary>
        /// <param name="nodeDefinition"></param>
        /// <param name="baseColor"></param>
        public void Initialize(MapGraphNodeDefinition nodeDefinition, Color baseColor)
        {
            _nodeDefinition = nodeDefinition;
            _baseColor = baseColor;
            EnsureReferences();

            gameObject.name = $"Node_{NodeId}";
            if (_bodyImage != null)
                _bodyImage.color = _baseColor;

            if (_labelText != null)
                _labelText.text = nodeDefinition != null ? nodeDefinition.DisplayName : string.Empty;
        }

        /// <summary>
        /// 刷新节点运行时表现
        /// </summary>
        /// <param name="presentationState"></param>
        public void Refresh(MapGraphNodePresentationState presentationState)
        {
            EnsureReferences();

            if (_bodyImage != null)
                _bodyImage.color = presentationState.HasBeenCompleted ? _baseColor * 0.58f : _baseColor;

            if (_targetRingImage != null)
            {
                bool showRing = presentationState.IsTargeted || presentationState.IsOccupied;
                _targetRingImage.gameObject.SetActive(showRing);
                _targetRingImage.color = presentationState.IsTargeted ? TargetRingColor : OccupiedRingColor;
            }

            if (_statusOverlayImage != null)
            {
                bool showOverlay = presentationState.HasBeenCompleted || presentationState.HasBeenTouched;
                _statusOverlayImage.gameObject.SetActive(showOverlay);
                _statusOverlayImage.color = presentationState.HasBeenCompleted
                    ? CompletedOverlayColor
                    : TouchedOverlayColor;
            }
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

            if (_targetRingImage == null)
                _targetRingImage = CreateChildImage("TargetRing", _defaultSize + new Vector2(10f, 10f));

            if (_statusOverlayImage == null)
                _statusOverlayImage = CreateChildImage("StatusOverlay", _defaultSize);

            if (_labelText == null)
                _labelText = GetComponentInChildren<TMP_Text>();
            if (_labelText == null)
                _labelText = CreateLabel();

            _targetRingImage.gameObject.SetActive(false);
            _statusOverlayImage.gameObject.SetActive(false);
        }

        private Image CreateChildImage(string childName, Vector2 size)
        {
            GameObject childObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(transform, false);
            childObject.transform.SetAsFirstSibling();

            RectTransform childRect = childObject.GetComponent<RectTransform>();
            childRect.anchorMin = new Vector2(0.5f, 0.5f);
            childRect.anchorMax = new Vector2(0.5f, 0.5f);
            childRect.pivot = new Vector2(0.5f, 0.5f);
            childRect.anchoredPosition = Vector2.zero;
            childRect.sizeDelta = size;

            Image image = childObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateLabel()
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -4f);
            labelRect.sizeDelta = new Vector2(96f, 24f);

            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.alignment = TextAlignmentOptions.Top;
            label.fontSize = 11f;
            label.color = new Color(0.92f, 0.96f, 1f, 0.94f);
            label.raycastTarget = false;
            return label;
        }
    }

    /// <summary>
    /// 节点视图刷新所需的运行时状态快照
    /// </summary>
    public readonly struct MapGraphNodePresentationState
    {
        public MapGraphNodePresentationState(
            bool isTargeted,
            bool isOccupied,
            bool hasBeenTouched,
            bool hasBeenCompleted)
        {
            IsTargeted = isTargeted;
            IsOccupied = isOccupied;
            HasBeenTouched = hasBeenTouched;
            HasBeenCompleted = hasBeenCompleted;
        }

        public bool IsTargeted { get; }
        public bool IsOccupied { get; }
        public bool HasBeenTouched { get; }
        public bool HasBeenCompleted { get; }
    }
}

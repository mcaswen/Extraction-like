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
        private static readonly Color ProgressBackgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.72f);
        private static readonly Color SearchProgressColor = new Color(0.24f, 0.82f, 0.35f, 0.95f);
        private static readonly Color CompletedProgressColor = new Color(0.48f, 0.5f, 0.54f, 0.92f);

        [SerializeField] private Image _bodyImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _statusOverlayImage;
        [SerializeField] private Image _targetRingImage;
        [SerializeField] private Image _occupiedRingImage;
        [SerializeField] private Image _progressBackgroundImage;
        [SerializeField] private Image _progressFillImage;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private Vector2 _defaultSize = new Vector2(42f, 42f);
        [SerializeField] private Vector2 _labelSize = new Vector2(112f, 28f);
        [SerializeField] private Vector2 _progressSize = new Vector2(52f, 6f);

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
        /// <param name="iconOverride"></param>
        public void Initialize(
            MapGraphNodeDefinition nodeDefinition,
            Color baseColor,
            Sprite iconOverride = null)
        {
            _nodeDefinition = nodeDefinition;
            _baseColor = baseColor;
            EnsureReferences();

            gameObject.name = $"Node_{NodeId}";
            if (_bodyImage != null)
                _bodyImage.color = _baseColor;

            if (_iconImage != null)
            {
                Sprite iconSprite = iconOverride;
                if (iconSprite == null && nodeDefinition != null)
                    iconSprite = nodeDefinition.Icon;
                if (iconSprite == null)
                    iconSprite = _fallbackSprite;

                _iconImage.sprite = iconSprite;
                _iconImage.enabled = iconSprite != null;
                _iconImage.color = iconSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            }

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
                _targetRingImage.gameObject.SetActive(presentationState.IsTargeted);
                _targetRingImage.color = TargetRingColor;
            }

            if (_occupiedRingImage != null)
            {
                _occupiedRingImage.gameObject.SetActive(presentationState.IsOccupied);
                _occupiedRingImage.color = OccupiedRingColor;
            }

            if (_statusOverlayImage != null)
            {
                bool showOverlay = presentationState.HasBeenCompleted || presentationState.HasBeenTouched;
                _statusOverlayImage.gameObject.SetActive(showOverlay);
                _statusOverlayImage.color = presentationState.HasBeenCompleted
                    ? CompletedOverlayColor
                    : TouchedOverlayColor;
            }

            RefreshProgress(presentationState);
        }

        /// <summary>
        /// 当前节点主体在 UI 坐标中的近似半径
        /// 供边线裁切头尾和 Agent 重叠排布使用
        /// </summary>
        public float GetVisualRadius()
        {
            EnsureReferences();
            Vector2 size = RectTransform.sizeDelta == Vector2.zero ? _defaultSize : RectTransform.sizeDelta;
            return Mathf.Max(size.x, size.y) * 0.5f;
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

            if (_bodyImage != null)
                _bodyImage.raycastTarget = false;

            if (_targetRingImage == null)
                _targetRingImage = ResolveChildImage("TargetRing");

            if (_occupiedRingImage == null)
                _occupiedRingImage = ResolveChildImage("OccupiedRing");

            if (_statusOverlayImage == null)
                _statusOverlayImage = ResolveChildImage("StatusOverlay");

            if (_iconImage == null)
                _iconImage = ResolveChildImage("Icon");

            if (_progressBackgroundImage == null)
                _progressBackgroundImage = ResolveChildImage("ProgressBackground");

            if (_progressFillImage == null)
                _progressFillImage = ResolveChildImage("ProgressFill");

            if (_labelText == null)
                _labelText = GetComponentInChildren<TMP_Text>();

            if (_targetRingImage != null)
                _targetRingImage.gameObject.SetActive(false);
            if (_occupiedRingImage != null)
                _occupiedRingImage.gameObject.SetActive(false);
            if (_statusOverlayImage != null)
                _statusOverlayImage.gameObject.SetActive(false);
            SetupProgressBar();
            ApplyChildLayout();
        }

        private Image ResolveChildImage(string childName)
        {
            Transform existing = transform.Find(childName);
            if (existing != null && existing.TryGetComponent(out Image existingImage))
                return existingImage;

            return null;
        }

        private void SetupProgressBar()
        {
            if (_progressBackgroundImage != null)
            {
                _progressBackgroundImage.color = ProgressBackgroundColor;
                _progressBackgroundImage.gameObject.SetActive(false);
            }

            if (_progressFillImage != null)
            {
                _progressFillImage.type = Image.Type.Filled;
                _progressFillImage.fillMethod = Image.FillMethod.Horizontal;
                _progressFillImage.fillOrigin = 0;
                _progressFillImage.fillAmount = 0f;
                _progressFillImage.gameObject.SetActive(false);
            }
        }

        private void ApplyChildLayout()
        {
            LayoutImage(_targetRingImage, Vector2.zero, _defaultSize + new Vector2(14f, 14f), 0);
            LayoutImage(_occupiedRingImage, Vector2.zero, _defaultSize + new Vector2(8f, 8f), 1);
            LayoutImage(_statusOverlayImage, Vector2.zero, _defaultSize, 2);
            LayoutImage(_iconImage, Vector2.zero, _defaultSize * 0.72f, 3);
            LayoutImage(_progressBackgroundImage, new Vector2(0f, (_defaultSize.y * 0.5f) + 7f), _progressSize, 4);
            LayoutImage(_progressFillImage, new Vector2(0f, (_defaultSize.y * 0.5f) + 7f), _progressSize, 5);

            if (_labelText != null)
                _labelText.transform.SetAsLastSibling();
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

        private void RefreshProgress(MapGraphNodePresentationState presentationState)
        {
            float progress = 0f;
            if (presentationState.HasBeenCompleted)
                progress = 1f;
            else if (presentationState.HasBeenTouched)
                progress = 0.55f;

            bool showProgress = progress > 0f;
            if (_progressBackgroundImage != null)
                _progressBackgroundImage.gameObject.SetActive(showProgress);

            if (_progressFillImage == null)
                return;

            _progressFillImage.gameObject.SetActive(showProgress);
            _progressFillImage.fillAmount = Mathf.Clamp01(progress);
            _progressFillImage.color = presentationState.HasBeenCompleted
                ? CompletedProgressColor
                : SearchProgressColor;
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

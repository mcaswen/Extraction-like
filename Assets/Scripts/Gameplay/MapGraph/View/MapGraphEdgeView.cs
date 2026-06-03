using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>
    /// 抽象图 UI 边视图
    /// 通过拉伸旋转 Image 绘制点与点之间的连线
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MapGraphEdgeView : MonoBehaviour
    {
        [SerializeField] private Image _lineImage;

        private RectTransform _rectTransform;
        private string _edgeId;
        private Color _baseColor;

        /// <summary>
        /// 当前边 ID
        /// </summary>
        public string EdgeId => _edgeId ?? string.Empty;

        /// <summary>
        /// 初始化边静态表现
        /// </summary>
        /// <param name="edgeId"></param>
        /// <param name="fromPosition"></param>
        /// <param name="toPosition"></param>
        /// <param name="width"></param>
        /// <param name="baseColor"></param>
        public void Initialize(
            string edgeId,
            Vector2 fromPosition,
            Vector2 toPosition,
            float width,
            Color baseColor)
        {
            Initialize(edgeId, fromPosition, toPosition, width, baseColor, 0f, 0f);
        }

        /// <summary>
        /// 初始化边的静态表现，并按两端节点视觉半径裁掉头尾
        /// </summary>
        /// <param name="edgeId"></param>
        /// <param name="fromPosition"></param>
        /// <param name="toPosition"></param>
        /// <param name="width"></param>
        /// <param name="baseColor"></param>
        /// <param name="fromRadius"></param>
        /// <param name="toRadius"></param>
        public void Initialize(
            string edgeId,
            Vector2 fromPosition,
            Vector2 toPosition,
            float width,
            Color baseColor,
            float fromRadius,
            float toRadius)
        {
            _edgeId = edgeId ?? string.Empty;
            _baseColor = baseColor;
            EnsureReferences();
            gameObject.name = $"Edge_{EdgeId}";
            ApplyLineTransform(
                fromPosition,
                toPosition,
                width,
                Mathf.Max(0f, fromRadius),
                Mathf.Max(0f, toRadius));
            Refresh(false);
        }

        /// <summary>
        /// 刷新边高亮状态
        /// </summary>
        /// <param name="isHighlighted"></param>
        public void Refresh(bool isHighlighted)
        {
            EnsureReferences();
            if (_lineImage == null)
                return;

            _lineImage.color = isHighlighted
                ? new Color(1f, 0.78f, 0.18f, 0.92f)
                : _baseColor;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_lineImage == null)
                _lineImage = GetComponent<Image>();

            if (_lineImage != null)
                _lineImage.raycastTarget = false;
        }

        private void ApplyLineTransform(
            Vector2 fromPosition,
            Vector2 toPosition,
            float width,
            float fromRadius,
            float toRadius)
        {
            Vector2 delta = toPosition - fromPosition;
            float length = delta.magnitude;
            if (length > Mathf.Epsilon)
            {
                Vector2 direction = delta / length;
                float trimmedFromDistance = Mathf.Max(0f, fromRadius);
                float trimmedToDistance = Mathf.Max(0f, toRadius);

                if (trimmedFromDistance + trimmedToDistance >= length)
                {
                    trimmedFromDistance = Mathf.Min(trimmedFromDistance, length * 0.4f);
                    trimmedToDistance = Mathf.Min(trimmedToDistance, length * 0.4f);
                }

                fromPosition += direction * trimmedFromDistance;
                toPosition -= direction * trimmedToDistance;
                delta = toPosition - fromPosition;
                length = delta.magnitude;
            }

            Vector2 center = (fromPosition + toPosition) * 0.5f;

            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.anchoredPosition = center;
            _rectTransform.sizeDelta = new Vector2(length, Mathf.Max(1f, width));
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}

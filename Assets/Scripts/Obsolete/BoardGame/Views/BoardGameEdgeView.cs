using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// 地图边表现组件
    /// 建议挂在线渲染器预制体上
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BoardGameEdgeView : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.86f, 0.21f, 0.95f);

        private string _edgeId;

        public string EdgeId => _edgeId;

        private void Awake()
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
            }

            if (_lineRenderer != null)
            {
                _lineRenderer.useWorldSpace = true;
            }
        }

        /// <summary>
        /// 初始化边的几何信息
        /// </summary>
        public void Initialize(
            string edgeId,
            Vector3 fromPosition,
            Vector3 toPosition,
            float fromRadius,
            float toRadius)
        {
            _edgeId = edgeId;
            _lineRenderer.positionCount = 2;
            ApplyTrimmedPositions(fromPosition, toPosition, fromRadius, toRadius);
            Refresh(false);
        }

        /// <summary>
        /// 刷新边的高亮状态
        /// </summary>
        public void Refresh(bool isHighlighted)
        {
            Color color = isHighlighted ? _highlightColor : _normalColor;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }

        /// <summary>
        /// 按两端节点的主体半径裁掉线段头尾
        /// 避免边线直接插到节点中心
        /// </summary>
        private void ApplyTrimmedPositions(
            Vector3 fromPosition,
            Vector3 toPosition,
            float fromRadius,
            float toRadius)
        {
            Vector3 direction = toPosition - fromPosition;
            float distance = direction.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                _lineRenderer.SetPosition(0, fromPosition);
                _lineRenderer.SetPosition(1, toPosition);
                return;
            }

            Vector3 normalizedDirection = direction / distance;
            float trimmedFromDistance = Mathf.Max(0f, fromRadius);
            float trimmedToDistance = Mathf.Max(0f, toRadius);

            if (trimmedFromDistance + trimmedToDistance >= distance)
            {
                trimmedFromDistance = Mathf.Min(trimmedFromDistance, distance * 0.4f);
                trimmedToDistance = Mathf.Min(trimmedToDistance, distance * 0.4f);
            }

            Vector3 trimmedFromPosition = fromPosition + normalizedDirection * trimmedFromDistance;
            Vector3 trimmedToPosition = toPosition - normalizedDirection * trimmedToDistance;

            _lineRenderer.SetPosition(0, trimmedFromPosition);
            _lineRenderer.SetPosition(1, trimmedToPosition);
        }
    }
}

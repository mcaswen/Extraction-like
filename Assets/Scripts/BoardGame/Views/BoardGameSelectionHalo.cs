using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// 选中高光外圈
    /// 不要求 prefab 预先配置单独的高亮渲染器
    /// 由脚本自动创建一圈线框高光
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardGameSelectionHalo : MonoBehaviour
    {
        private static readonly int SegmentCount = 40;
        private static Material _sharedHaloMaterial;

        private LineRenderer _lineRenderer;
        private SpriteRenderer _sourceRenderer;
        private float _radius = 0.6f;

        /// <summary>
        /// 绑定主体精灵并按其尺寸生成高光外圈
        /// </summary>
        public void Initialize(SpriteRenderer sourceRenderer, float padding, float width)
        {
            _sourceRenderer = sourceRenderer;
            EnsureLineRenderer();
            RebuildCircle(padding, width);
            SetVisible(false);
        }

        /// <summary>
        /// 刷新外圈显隐和颜色
        /// </summary>
        public void Refresh(bool isVisible, Color color)
        {
            EnsureLineRenderer();

            if (_lineRenderer == null)
            {
                return;
            }

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
            SetVisible(isVisible);
        }

        /// <summary>
        /// 按当前半径重建一个闭合圆环
        /// </summary>
        private void RebuildCircle(float padding, float width)
        {
            if (_lineRenderer == null)
            {
                return;
            }

            Bounds localBounds = _sourceRenderer != null
                ? _sourceRenderer.localBounds
                : new Bounds(Vector3.zero, Vector3.one);

            _radius = Mathf.Max(localBounds.extents.x, localBounds.extents.y) + Mathf.Max(0.01f, padding);
            _lineRenderer.widthMultiplier = Mathf.Max(0.01f, width);
            _lineRenderer.positionCount = SegmentCount;

            for (int index = 0; index < SegmentCount; index++)
            {
                float angle = (float)index / SegmentCount * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * _radius;
                float y = Mathf.Sin(angle) * _radius;
                _lineRenderer.SetPosition(index, new Vector3(x, y, 0f));
            }
        }

        private void SetVisible(bool isVisible)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = isVisible;
            }
        }

        private void EnsureLineRenderer()
        {
            if (_lineRenderer != null)
            {
                return;
            }

            _lineRenderer = GetComponent<LineRenderer>();

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            _lineRenderer.loop = true;
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.textureMode = LineTextureMode.Stretch;
            _lineRenderer.numCapVertices = 8;
            _lineRenderer.numCornerVertices = 8;
            _lineRenderer.alignment = LineAlignment.TransformZ;
            _lineRenderer.material = GetSharedHaloMaterial();

            if (_sourceRenderer != null)
            {
                _lineRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
                _lineRenderer.sortingOrder = _sourceRenderer.sortingOrder + 1;
            }
        }

        private static Material GetSharedHaloMaterial()
        {
            if (_sharedHaloMaterial != null)
            {
                return _sharedHaloMaterial;
            }

            Shader haloShader = Shader.Find("Sprites/Default");

            if (haloShader == null)
            {
                haloShader = Shader.Find("Unlit/Color");
            }

            _sharedHaloMaterial = new Material(haloShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            return _sharedHaloMaterial;
        }
    }
}

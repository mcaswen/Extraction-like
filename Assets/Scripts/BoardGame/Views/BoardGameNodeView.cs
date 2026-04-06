using BoardGame.Config;
using BoardGame.Runtime;
using BoardGame.Runtime.State;
using TMPro;
using UnityEngine;

namespace BoardGame.Views
{
    /// <summary>
    /// 地图节点表现组件
    /// </summary>
    public sealed class BoardGameNodeView : MonoBehaviour
    {
        private const float HaloPadding = 0.14f;
        private const float HaloWidth = 0.08f;

        private static readonly Color EnemyProgressColor = new Color(0.93f, 0.24f, 0.24f, 1f);
        private static readonly Color SearchProgressColor = new Color(0.24f, 0.82f, 0.35f, 1f);
        private static readonly Color ExtractProgressColor = new Color(0.96f, 0.82f, 0.2f, 1f);
        private static readonly Color SelectionHaloColor = new Color(0.22f, 0.62f, 1f, 1f);
        private static readonly Color RedirectHaloColor = new Color(0.35f, 0.78f, 1f, 1f);
        private static readonly Color TargetHaloColor = new Color(0.97f, 0.78f, 0.18f, 1f);
        private static readonly Vector3 DefaultIconScale = Vector3.one;

        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private Sprite _fallbackSprite;
        [SerializeField] private Transform _progressFillTransform;
        [SerializeField] private SpriteRenderer _progressFillRenderer;
        [SerializeField] private TMP_Text _detailText;

        private string _nodeId;
        private SO_BoardGame_MapDefinition _mapDefinition;
        private Vector3 _progressFillBaseScale = Vector3.one;
        private Color _progressFillBaseColor = Color.white;
        private BoardGameSelectionHalo _selectionHalo;
        private BoardGameSelectionHalo _targetHalo;

        public string NodeId => _nodeId;

        /// <summary>
        /// 返回节点主体在世界空间下的近似半径
        /// 供边线从节点外缘开始绘制
        /// </summary>
        public float GetVisualWorldRadius()
        {
            CircleCollider2D hitCollider = GetComponent<CircleCollider2D>();

            if (hitCollider != null)
            {
                return hitCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            }

            if (_iconRenderer != null && _iconRenderer.enabled && _iconRenderer.sprite != null)
            {
                Bounds worldBounds = _iconRenderer.bounds;
                return Mathf.Max(worldBounds.extents.x, worldBounds.extents.y);
            }

            return 0.25f;
        }

        private void Awake()
        {
            if (_progressFillTransform != null)
            {
                _progressFillBaseScale = _progressFillTransform.localScale;
            }

            if (_progressFillRenderer == null && _progressFillTransform != null)
            {
                _progressFillRenderer = _progressFillTransform.GetComponent<SpriteRenderer>();
            }

            if (_progressFillRenderer != null)
            {
                _progressFillBaseColor = _progressFillRenderer.color;
            }

            EnsureIconRenderer();
            EnsureHalos();
        }

        /// <summary>
        /// 初始化节点视图
        /// </summary>
        public void Initialize(string nodeId, SO_BoardGame_MapDefinition mapDefinition)
        {
            _nodeId = nodeId;
            _mapDefinition = mapDefinition;
        }

        /// <summary>
        /// 刷新节点视觉状态
        /// </summary>
        public void Refresh(
            BoardNodeRuntimeState nodeState,
            bool isCurrentTarget,
            bool isSelected,
            bool canRedirect,
            bool showRuntimeInfo,
            string runtimeInfoOverrideText = null,
            float? progressOverride01 = null)
        {
            if (nodeState == null)
            {
                return;
            }

            Sprite iconSprite = ResolveDisplaySprite(nodeState);
            RefreshIconRenderer(iconSprite, ResolveDisplayColor(nodeState, iconSprite));
            _selectionHalo?.Initialize(_iconRenderer, HaloPadding, HaloWidth);
            _targetHalo?.Initialize(_iconRenderer, HaloPadding, HaloWidth);
            _selectionHalo?.Refresh(
                isSelected || canRedirect,
                canRedirect ? RedirectHaloColor : SelectionHaloColor);
            _targetHalo?.Refresh(isCurrentTarget, TargetHaloColor);

            if (_detailText != null)
            {
                _detailText.text = showRuntimeInfo
                    ? (string.IsNullOrEmpty(runtimeInfoOverrideText) ? BuildRuntimeInfoText(nodeState) : runtimeInfoOverrideText)
                    : string.Empty;
                _detailText.gameObject.SetActive(!string.IsNullOrEmpty(_detailText.text));
            }

            if (_progressFillTransform != null)
            {
                float progress = progressOverride01 ?? GetNodeProgress(nodeState);
                _progressFillTransform.gameObject.SetActive(showRuntimeInfo && progress > 0f);
                _progressFillTransform.localScale = new Vector3(
                    _progressFillBaseScale.x * Mathf.Clamp01(progress),
                    _progressFillBaseScale.y,
                    _progressFillBaseScale.z);
            }

            if (_progressFillRenderer != null)
            {
                _progressFillRenderer.color = GetProgressColor(nodeState);
            }
        }

        /// <summary>
        /// 生成节点的等级说明文本
        /// </summary>
        /// <summary>
        /// 生成节点的实时数值信息
        /// 仅用于 AI 当前所在局部节点
        /// </summary>
        private static string BuildRuntimeInfoText(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    if (nodeState.ResourceState == BoardResourceStateType.Looted)
                    {
                        return "Looted";
                    }

                    if (nodeState.HasPendingLootInteraction())
                    {
                        return nodeState.IsLootRevealComplete()
                            ? "Loot Ready"
                            : $"Search {nodeState.GetLootRevealProgress01():P0}";
                    }

                    if (nodeState.SearchProgressSeconds <= Mathf.Epsilon ||
                        nodeState.SearchRequiredSeconds <= Mathf.Epsilon)
                    {
                        return string.Empty;
                    }

                    return $"Search {nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds:P0}";
                case BoardNodeType.Enemy:
                    if (ShouldShowLootProgress(nodeState))
                    {
                        return $"Search {nodeState.GetLootRevealProgressWithPartial01():P0}";
                    }

                    return $"HP {nodeState.EnemyCurrentHealth}/{Mathf.Max(1, nodeState.EnemyMaxHealth)}";
                case BoardNodeType.Boss:
                    if (ShouldShowLootProgress(nodeState))
                    {
                        return $"Search {nodeState.GetLootRevealProgressWithPartial01():P0}";
                    }

                    return $"HP {nodeState.BossCurrentHealth}/{Mathf.Max(1, nodeState.BossMaxHealth)}";
                case BoardNodeType.Extract:
                    if (nodeState.ExtractState == BoardExtractStateType.Extracted)
                    {
                        return "Extracted";
                    }

                    if (nodeState.ExtractProgressSeconds <= Mathf.Epsilon ||
                        nodeState.ExtractRequiredSeconds <= Mathf.Epsilon)
                    {
                        return string.Empty;
                    }

                    return $"Extract {nodeState.ExtractProgressSeconds / nodeState.ExtractRequiredSeconds:P0}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 计算节点过程进度，用于节点上的小进度条展示
        /// </summary>
        private static float GetNodeProgress(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    return nodeState.SearchRequiredSeconds <= Mathf.Epsilon
                        ? 0f
                        : nodeState.SearchProgressSeconds / nodeState.SearchRequiredSeconds;
                case BoardNodeType.Enemy:
                    if (ShouldShowLootProgress(nodeState))
                    {
                        return nodeState.GetLootRevealProgressWithPartial01();
                    }

                    return nodeState.EnemyMaxHealth <= 0
                        ? 0f
                        : (float)nodeState.EnemyCurrentHealth / nodeState.EnemyMaxHealth;
                case BoardNodeType.Boss:
                    if (ShouldShowLootProgress(nodeState))
                    {
                        return nodeState.GetLootRevealProgressWithPartial01();
                    }

                    return nodeState.BossMaxHealth <= 0
                        ? 0f
                        : (float)nodeState.BossCurrentHealth / nodeState.BossMaxHealth;
                case BoardNodeType.Extract:
                    return nodeState.ExtractRequiredSeconds <= Mathf.Epsilon
                        ? 0f
                        : nodeState.ExtractProgressSeconds / nodeState.ExtractRequiredSeconds;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 根据节点过程类型返回进度条颜色
        /// </summary>
        private Color GetProgressColor(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    return SearchProgressColor;
                case BoardNodeType.Enemy:
                case BoardNodeType.Boss:
                    if (ShouldShowLootProgress(nodeState))
                    {
                        return SearchProgressColor;
                    }

                    return EnemyProgressColor;
                case BoardNodeType.Extract:
                    return ExtractProgressColor;
                default:
                    return _progressFillBaseColor;
            }
        }

        /// <summary>
        /// 自动创建节点高光外圈
        /// 当前蓝圈和金圈共用同一层外圈几何，不再区分里外圈
        /// </summary>
        private static bool ShouldShowLootProgress(BoardNodeRuntimeState nodeState)
        {
            return nodeState.HasPendingLootContainer() && nodeState.HasRemainingLootItems();
        }

        private Sprite ResolveDisplaySprite(BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null || _mapDefinition == null || _mapDefinition.NodeIconSet == null)
            {
                return _fallbackSprite;
            }

            Sprite iconSprite = _mapDefinition.NodeIconSet.GetIcon(
                nodeState.NodeType,
                nodeState.ResourceTier,
                nodeState.DangerTier);
            return iconSprite != null ? iconSprite : _fallbackSprite;
        }

        private Color ResolveDisplayColor(BoardNodeRuntimeState nodeState, Sprite iconSprite)
        {
            if (nodeState == null)
            {
                return Color.white;
            }

            if (iconSprite != null &&
                iconSprite != _fallbackSprite &&
                nodeState.NodeType != BoardNodeType.Start)
            {
                return Color.white;
            }

            return BoardGameTypes.GetNodeColor(nodeState.NodeType, nodeState.ResourceTier, nodeState.DangerTier);
        }

        private void EnsureHalos()
        {
            if (_iconRenderer == null)
            {
                return;
            }

            _selectionHalo = EnsureHalo("SelectionHalo");
            _selectionHalo.Initialize(_iconRenderer, HaloPadding, HaloWidth);
            _targetHalo = EnsureHalo("TargetHalo");
            _targetHalo.Initialize(_iconRenderer, HaloPadding, HaloWidth);
        }

        private BoardGameSelectionHalo EnsureHalo(string haloName)
        {
            Transform haloTransform = transform.Find(haloName);

            if (haloTransform == null)
            {
                GameObject haloObject = new GameObject(haloName);
                haloObject.transform.SetParent(transform, false);
                return haloObject.AddComponent<BoardGameSelectionHalo>();
            }

            BoardGameSelectionHalo halo = haloTransform.GetComponent<BoardGameSelectionHalo>();

            if (halo == null)
            {
                halo = haloTransform.gameObject.AddComponent<BoardGameSelectionHalo>();
            }

            return halo;
        }

        private void EnsureIconRenderer()
        {
            if (_iconRenderer != null)
            {
                return;
            }

            Transform iconTransform = transform.Find("IconRenderer");
            GameObject iconObject;

            if (iconTransform == null)
            {
                iconObject = new GameObject("IconRenderer");
                iconObject.transform.SetParent(transform, false);
            }
            else
            {
                iconObject = iconTransform.gameObject;
            }

            _iconRenderer = iconObject.GetComponent<SpriteRenderer>();

            if (_iconRenderer == null)
            {
                _iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            }

            iconObject.transform.localPosition = Vector3.zero;
            iconObject.transform.localRotation = Quaternion.identity;
            iconObject.transform.localScale = DefaultIconScale;
            _iconRenderer.sortingOrder = 2;
            _iconRenderer.color = Color.white;
            _iconRenderer.enabled = false;
        }

        private void RefreshIconRenderer(Sprite iconSprite, Color tintColor)
        {
            if (_iconRenderer == null)
            {
                return;
            }

            _iconRenderer.sprite = iconSprite;
            _iconRenderer.enabled = iconSprite != null;
            _iconRenderer.color = tintColor;
            _iconRenderer.transform.localScale = iconSprite != null
                ? ResolveIconScale(iconSprite)
                : DefaultIconScale;
        }

        private Vector3 ResolveIconScale(Sprite iconSprite)
        {
            if (iconSprite == null)
            {
                return DefaultIconScale;
            }

            float iconMaxDimension = Mathf.Max(iconSprite.bounds.size.x, iconSprite.bounds.size.y);

            if (iconMaxDimension <= Mathf.Epsilon)
            {
                return DefaultIconScale;
            }

            float targetMaxDimension = ResolveIconTargetMaxDimension();
            float uniformScale = targetMaxDimension / iconMaxDimension;
            return new Vector3(uniformScale, uniformScale, 1f);
        }

        private float ResolveIconTargetMaxDimension()
        {
            CircleCollider2D hitCollider = GetComponent<CircleCollider2D>();
            return hitCollider != null ? hitCollider.radius * 2f : 1f;
        }
    }
}

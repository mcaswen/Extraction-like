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

        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private Transform _progressFillTransform;
        [SerializeField] private SpriteRenderer _progressFillRenderer;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _detailText;

        private string _nodeId;
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
            if (_bodyRenderer == null)
            {
                return 0.25f;
            }

            Bounds worldBounds = _bodyRenderer.bounds;
            return Mathf.Max(worldBounds.extents.x, worldBounds.extents.y);
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

            EnsureHalos();
        }

        /// <summary>
        /// 初始化节点视图
        /// </summary>
        public void Initialize(string nodeId)
        {
            _nodeId = nodeId;

            if (_titleText != null)
            {
                _titleText.text = nodeId;
            }
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

            if (_bodyRenderer != null)
            {
                _bodyRenderer.color = BoardGameTypes.GetNodeColor(nodeState.NodeType, nodeState.ResourceTier, nodeState.DangerTier);
            }

            _selectionHalo?.Refresh(
                isSelected || canRedirect,
                canRedirect ? RedirectHaloColor : SelectionHaloColor);
            _targetHalo?.Refresh(isCurrentTarget, TargetHaloColor);

            if (_titleText != null)
            {
                _titleText.text = BoardGameTypes.GetNodeTypeLabel(nodeState.NodeType);
            }

            if (_statusText != null)
            {
                _statusText.text = BuildTierText(nodeState);
                _statusText.gameObject.SetActive(!string.IsNullOrEmpty(_statusText.text));
            }

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
        private static string BuildTierText(BoardNodeRuntimeState nodeState)
        {
            switch (nodeState.NodeType)
            {
                case BoardNodeType.Resource:
                    return $"Resource {BoardGameTypes.GetResourceTierLabel(nodeState.ResourceTier)}";
                case BoardNodeType.Enemy:
                    return $"Risk {BoardGameTypes.GetDangerLabel(nodeState.DangerTier)}";
                case BoardNodeType.Boss:
                    return $"Risk {BoardGameTypes.GetDangerLabel(nodeState.DangerTier)}";
                case BoardNodeType.Extract:
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

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
                    return $"HP {nodeState.EnemyCurrentHealth}/{Mathf.Max(1, nodeState.EnemyMaxHealth)}";
                case BoardNodeType.Boss:
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
                    return nodeState.EnemyMaxHealth <= 0
                        ? 0f
                        : (float)nodeState.EnemyCurrentHealth / nodeState.EnemyMaxHealth;
                case BoardNodeType.Boss:
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
        private void EnsureHalos()
        {
            if (_bodyRenderer == null)
            {
                return;
            }

            _selectionHalo = EnsureHalo("SelectionHalo");
            _selectionHalo.Initialize(_bodyRenderer, HaloPadding, HaloWidth);
            _targetHalo = EnsureHalo("TargetHalo");
            _targetHalo.Initialize(_bodyRenderer, HaloPadding, HaloWidth);
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
    }
}

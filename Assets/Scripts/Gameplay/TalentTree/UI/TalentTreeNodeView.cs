using Gameplay.Agent.Talent;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameplay.TalentTree.UI
{
    /// <summary>
    /// 天赋树节点 UI 视图。
    /// 负责把一个可点击 UI 节点绑定到运行时天赋节点，并刷新锁定/可解锁/已解锁表现。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TalentTreeNodeView : MonoBehaviour, IPointerEnterHandler
    {
        private static readonly Color LockedColor = new Color(0.42f, 0.45f, 0.5f, 0.86f);
        private static readonly Color AvailableColor = new Color(0.98f, 0.92f, 0.36f, 1f);
        private static readonly Color UnlockedColor = new Color(0.24f, 0.86f, 0.42f, 1f);
        private static readonly Color ConnectorColor = new Color(0.28f, 0.3f, 0.34f, 0.76f);

        [SerializeField] private AgentTalentNodeId _nodeId;
        [SerializeField] private Button _button;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _lockedCore;
        [SerializeField] private Text _labelText;

        private TalentTreePanelController _controller;
        private string _title;
        private string _description;
        private string _effect;
        private bool _isBound;

        /// <summary>
        /// 当前视图绑定的天赋节点 ID。
        /// </summary>
        public AgentTalentNodeId NodeId => _nodeId;

        /// <summary>
        /// 当前节点标题。
        /// </summary>
        public string Title => string.IsNullOrWhiteSpace(_title) ? _nodeId.ToString() : _title;

        /// <summary>
        /// 当前节点描述。
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// 当前节点增益说明。
        /// </summary>
        public string Effect => _effect;

        private void Awake()
        {
            CacheReferences();
            RegisterClickHandler();
        }

        private void OnEnable()
        {
            CacheReferences();
            RegisterClickHandler();
        }

        /// <summary>
        /// 绑定节点数据和所属面板。
        /// </summary>
        /// <param name="controller">所属天赋树面板。</param>
        /// <param name="nodeId">运行时天赋节点 ID。</param>
        /// <param name="title">显示标题。</param>
        /// <param name="description">节点描述。</param>
        /// <param name="effect">节点效果。</param>
        public void Bind(
            TalentTreePanelController controller,
            AgentTalentNodeId nodeId,
            string title,
            string description,
            string effect)
        {
            CacheReferences();
            _controller = controller;
            _nodeId = nodeId;
            _title = title;
            _description = description;
            _effect = effect;
            _isBound = true;

            if (_labelText != null)
                _labelText.text = title;

            RegisterClickHandler();
            Refresh();
        }

        /// <summary>
        /// 将该节点设置为仅展示连接用的非交互节点。
        /// </summary>
        public void BindAsConnector()
        {
            CacheReferences();
            _controller = null;
            _title = string.Empty;
            _description = string.Empty;
            _effect = string.Empty;
            _isBound = false;

            SetInteractable(false);
            SetNodeColor(ConnectorColor);
            SetLockedCoreVisible(false);
            SetIconSprite(null);
        }

        /// <summary>
        /// 根据运行时天赋状态刷新节点表现。
        /// </summary>
        public void Refresh()
        {
            CacheReferences();
            if (!_isBound)
            {
                BindAsConnector();
                return;
            }

            AgentTalentRuntimeController talentRuntime =
                _controller != null ? _controller.TargetTalentRuntime : null;
            bool hasRuntime = talentRuntime != null;
            bool isUnlocked = hasRuntime && talentRuntime.IsUnlocked(_nodeId);
            bool canUnlock = hasRuntime && talentRuntime.CanUnlockNode(_nodeId);

            SetInteractable(canUnlock);
            SetLockedCoreVisible(!isUnlocked);

            if (isUnlocked)
                SetNodeColor(UnlockedColor);
            else if (canUnlock)
                SetNodeColor(AvailableColor);
            else
                SetNodeColor(LockedColor);
        }

        /// <summary>
        /// 设置节点图标 Sprite。
        /// </summary>
        /// <param name="iconSprite">要显示的节点图标；为空时清空图标。</param>
        public void SetIconSprite(Sprite iconSprite)
        {
            CacheReferences();
            if (_iconImage == null)
                return;

            _iconImage.sprite = iconSprite;
            _iconImage.color = Color.white;
            _iconImage.preserveAspect = true;

            for (int i = 0; i < _iconImage.transform.childCount; i++)
            {
                Transform child = _iconImage.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Icon_"))
                    child.gameObject.SetActive(iconSprite == null);
            }
        }

        /// <summary>
        /// 鼠标进入节点时刷新详情面板。
        /// </summary>
        /// <param name="eventData">指针事件数据。</param>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isBound && _controller != null)
                _controller.ShowNodeDetails(this);
        }

        private void HandleButtonClicked()
        {
            if (_isBound && _controller != null)
                _controller.TryUnlockNode(this);
        }

        private void CacheReferences()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_backgroundImage == null)
                _backgroundImage = GetComponent<Image>();

            if (_fillImage == null)
            {
                Transform fillTransform = transform.Find("Fill");
                _fillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            }

            if (_iconImage == null)
            {
                Transform iconTransform = transform.Find("Icon");
                if (iconTransform == null)
                    iconTransform = transform.Find("Fill");

                _iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            }

            if (_lockedCore == null)
            {
                Transform lockedTransform = FindChildRecursive(transform, "LockedCore");
                _lockedCore = lockedTransform != null ? lockedTransform.gameObject : null;
            }

            if (_labelText == null)
                _labelText = GetComponentInChildren<Text>(true);
        }

        private void RegisterClickHandler()
        {
            if (_button == null)
                return;

            _button.onClick.RemoveListener(HandleButtonClicked);
            _button.onClick.AddListener(HandleButtonClicked);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null)
                    continue;

                if (child.name == childName)
                    return child;

                Transform nestedChild = FindChildRecursive(child, childName);
                if (nestedChild != null)
                    return nestedChild;
            }

            return null;
        }

        private void SetInteractable(bool isInteractable)
        {
            if (_button != null)
                _button.interactable = isInteractable;
        }

        private void SetLockedCoreVisible(bool visible)
        {
            if (_lockedCore != null)
                _lockedCore.SetActive(visible);
        }

        private void SetNodeColor(Color color)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = color;

            if (_fillImage != null && _fillImage != _iconImage)
                _fillImage.color = color;
        }
    }
}

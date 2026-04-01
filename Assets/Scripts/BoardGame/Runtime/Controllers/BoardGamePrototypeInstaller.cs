using BoardGame.Config;
using BoardGame.Presentation;
using BoardGame.Runtime;
using BoardGame.Views;
using UnityEngine;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 场景挂载入口
    /// 通过显式序列化引用，将配置、视图和 UI 控制器装配到原型总控上
    /// </summary>
    public sealed class BoardGamePrototypeInstaller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private SO_BoardGame_MapDefinition _mapDefinition;
        [SerializeField] private SO_BoardGame_RuleSet _ruleSet;
        [SerializeField] private SO_BoardGame_LootTableSet _lootTableSet;
        [SerializeField] private BoardGameBagLayoutSettings _bagLayoutSettings = new BoardGameBagLayoutSettings();

        [Header("Map View")]
        [SerializeField] private Transform _mapRoot;
        [SerializeField] private BoardGameMapViewController _mapViewController;
        [SerializeField] private BoardGameNodeView _nodeViewPrefab;
        [SerializeField] private BoardGameEdgeView _edgeViewPrefab;
        [SerializeField] private BoardGameAgentView _agentViewPrefab;

        [Header("UI")]
        [SerializeField] private BoardGameHudController _hudController;
        [SerializeField] private BoardGameItemBarController _itemBarController;
        [SerializeField] private BoardGameLevelUpController _levelUpController;
        [SerializeField] private BoardGameLootInventoryController _lootInventoryController;
        [SerializeField] private BoardGameSelectionController _selectionController;
        [SerializeField] private Camera _worldCamera;

        private BoardGamePrototypeController _prototypeController;

        public BoardGamePrototypeController PrototypeController => _prototypeController;

        /// <summary>
        /// 组装 BoardGame 运行时总控，并把地图、HUD、道具栏和输入控制器全部绑定起来
        /// </summary>
        private void Start()
        {
            if (!ValidateRequiredConfiguration())
            {
                enabled = false;
                return;
            }

            _prototypeController = new BoardGamePrototypeController(_mapDefinition, _ruleSet, _lootTableSet, _bagLayoutSettings);

            if (_mapViewController != null)
            {
                // 地图视图先初始化，后续 HUD 和输入控制器才能立刻读取到完整节点状态
                _mapViewController.Initialize(
                    _mapDefinition,
                    _prototypeController.GraphService,
                    _prototypeController.RuntimeQueryController,
                    _prototypeController.SelectionStateController,
                    _mapRoot,
                    _nodeViewPrefab,
                    _edgeViewPrefab,
                    _agentViewPrefab);
            }

            _hudController?.Bind(_prototypeController.RuntimeQueryController);
            _itemBarController?.Bind(_prototypeController.RuntimeQueryController, _prototypeController.ItemUseController);
            ResolveLevelUpController()?.Bind(_prototypeController.RuntimeQueryController, _prototypeController.ProgressionController);
            _selectionController?.Bind(
                _prototypeController.RuntimeQueryController,
                _prototypeController.SelectionStateController,
                _prototypeController.TargetRedirectController,
                _prototypeController.ProgressionController,
                _worldCamera != null ? _worldCamera : Camera.main);

            if (_prototypeController.IsBagSystemEnabled)
            {
                // 只有开背包系统时才装配 Bag bridge，避免旧容量模式额外创建运行时 UI
                ResolveLootInventoryController()?.Bind(
                    _prototypeController.RuntimeQueryController,
                    _prototypeController.LootInteractionController,
                    ResolveParentCanvas());
            }
        }

        /// <summary>
        /// 推进 BoardGame 原型每帧逻辑
        /// </summary>
        private void Update()
        {
            _prototypeController?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 检查原型运行所需的核心配置是否齐全
        /// </summary>
        private bool ValidateRequiredConfiguration()
        {
            if (_mapDefinition == null || _ruleSet == null || _lootTableSet == null)
            {
                Debug.LogError("BoardGamePrototypeInstaller is missing required config references");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析升级面板控制器
        /// 若场景中未提供，则尝试基于已有 Canvas 运行时创建一份
        /// </summary>
        private BoardGameLevelUpController ResolveLevelUpController()
        {
            if (_levelUpController != null)
            {
                return _levelUpController;
            }

            _levelUpController = FindObjectOfType<BoardGameLevelUpController>();

            if (_levelUpController != null)
            {
                return _levelUpController;
            }

            Canvas parentCanvas = null;

            if (_hudController != null)
            {
                parentCanvas = _hudController.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null && _itemBarController != null)
            {
                parentCanvas = _itemBarController.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null && _selectionController != null)
            {
                parentCanvas = _selectionController.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null)
            {
                return null;
            }

            // 升级面板是纯运行时 overlay，所以这里直接补建并拉满父 Canvas
            GameObject levelUpObject = new GameObject("LevelUpOverlay", typeof(RectTransform), typeof(BoardGameLevelUpController));
            RectTransform rectTransform = levelUpObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parentCanvas.transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.SetAsLastSibling();

            _levelUpController = levelUpObject.GetComponent<BoardGameLevelUpController>();
            return _levelUpController;
        }

        /// <summary>
        /// 解析 loot 背包桥接控制器
        /// 若场景中未挂载，则在安装器节点下运行时补建一份
        /// </summary>
        private BoardGameLootInventoryController ResolveLootInventoryController()
        {
            if (_lootInventoryController != null)
            {
                return _lootInventoryController;
            }

            _lootInventoryController = FindObjectOfType<BoardGameLootInventoryController>();

            if (_lootInventoryController != null)
            {
                return _lootInventoryController;
            }

            GameObject inventoryBridgeObject = new GameObject("BoardGameLootInventoryController", typeof(BoardGameLootInventoryController));
            inventoryBridgeObject.transform.SetParent(transform, false);
            _lootInventoryController = inventoryBridgeObject.GetComponent<BoardGameLootInventoryController>();
            return _lootInventoryController;
        }

        /// <summary>
        /// 从已绑定的 HUD、道具栏或选择控制器中寻找可复用的父级 Canvas
        /// </summary>
        private Canvas ResolveParentCanvas()
        {
            Canvas parentCanvas = null;

            if (_hudController != null)
            {
                parentCanvas = _hudController.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null && _itemBarController != null)
            {
                parentCanvas = _itemBarController.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null && _selectionController != null)
            {
                parentCanvas = _selectionController.GetComponentInParent<Canvas>();
            }

            return parentCanvas;
        }
    }
}

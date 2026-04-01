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
        // 固定地图配置，提供节点、边和 2D 坐标
        [SerializeField] private SO_BoardGame_MapDefinition _mapDefinition;
        // 规则配置，提供 AI 阈值、移动时长、战斗公式和撤离规则
        [SerializeField] private SO_BoardGame_RuleSet _ruleSet;
        // 掉落配置，提供资源点与敌人的战利品表
        [SerializeField] private SO_BoardGame_LootTableSet _lootTableSet;
        // BoardGame 与背包网格的桥接布局配置
        [SerializeField] private BoardGameBagLayoutSettings _bagLayoutSettings = new BoardGameBagLayoutSettings();

        [Header("Map View")]
        // 地图运行时表现对象的父节点，留空时会退回当前物体
        [SerializeField] private Transform _mapRoot;
        // 地图视图总控，负责实例化节点、边和 AI 表现
        [SerializeField] private BoardGameMapViewController _mapViewController;
        // 节点预制体，必须带 2D Collider 以支持点击
        [SerializeField] private BoardGameNodeView _nodeViewPrefab;
        // 边预制体，建议挂 LineRenderer
        [SerializeField] private BoardGameEdgeView _edgeViewPrefab;
        // AI 表现预制体，必须带 2D Collider 以支持选中
        [SerializeField] private BoardGameAgentView _agentViewPrefab;

        [Header("UI")]
        // 顶部 HUD 控制器，显示 AI 总览与进度
        [SerializeField] private BoardGameHudController _hudController;
        // 道具栏控制器，展示并触发消耗品使用
        [SerializeField] private BoardGameItemBarController _itemBarController;
        // 升级弹窗控制器，展示 3 选 1 增益
        [SerializeField] private BoardGameLevelUpController _levelUpController;
        // 战利品背包桥接控制器，负责在桌游场景里运行时创建背包 UI
        [SerializeField] private BoardGameLootInventoryController _lootInventoryController;
        // 输入控制器，负责鼠标选中与重定向操作
        [SerializeField] private BoardGameSelectionController _selectionController;
        // 世界相机，负责把鼠标位置投到 2D 地图上
        [SerializeField] private Camera _worldCamera;

        private BoardGamePrototypeController _prototypeController;

        public BoardGamePrototypeController PrototypeController => _prototypeController;

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
                _mapViewController.Initialize(_prototypeController, _mapRoot, _nodeViewPrefab, _edgeViewPrefab, _agentViewPrefab);
            }

            _hudController?.Bind(_prototypeController);
            _itemBarController?.Bind(_prototypeController);
            ResolveLevelUpController()?.Bind(_prototypeController);
            _selectionController?.Bind(_prototypeController, _worldCamera != null ? _worldCamera : Camera.main);
            ResolveLootInventoryController()?.Bind(_prototypeController, ResolveParentCanvas());
        }

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

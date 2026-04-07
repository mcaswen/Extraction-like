using BoardGame.Config;
using BoardGame.Presentation;
using BoardGame.Runtime;
using BoardGame.Views;
using UnityEngine;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// Scene entry point.
    /// Wires config, views and UI controllers into the prototype controller.
    /// </summary>
    public sealed class BoardGamePrototypeInstaller : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private SO_BoardGame_MapDefinition _mapDefinition;
        [SerializeField] private SO_BoardGame_RuleSet _ruleSet;
        [SerializeField] private SO_BoardGame_LootTableSet _lootTableSet;
        [SerializeField] private SO_BoardGame_AgentRoster _agentRoster;
        [SerializeField] private BoardGameBagLayoutSettings _bagLayoutSettings = new BoardGameBagLayoutSettings();

        [Header("Map View")]
        [SerializeField] private Transform _mapRoot;
        [SerializeField] private BoardGameMapViewController _mapViewController;
        [SerializeField] private BoardGameNodeView _nodeViewPrefab;
        [SerializeField] private BoardGameEdgeView _edgeViewPrefab;
        [SerializeField] private BoardGameAgentView _agentViewPrefab;

        [Header("UI")]
        [SerializeField] private BoardGameHudController _hudController;
        [SerializeField] private BoardGameCombatHudController _combatHudController;
        [SerializeField] private bool _enableCombatHud = true;
        [SerializeField] private BoardGameItemBarController _itemBarController;
        [SerializeField] private BoardGameLevelUpController _levelUpController;
        [SerializeField] private BoardGameLootInventoryController _lootInventoryController;
        [SerializeField] private BoardGameSelectionController _selectionController;
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

            _prototypeController = new BoardGamePrototypeController(
                _mapDefinition,
                _ruleSet,
                _lootTableSet,
                _agentRoster,
                _bagLayoutSettings);

            if (_mapViewController != null)
            {
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
            if (_enableCombatHud)
            {
                BoardGameCombatHudController combatHudController = ResolveCombatHudController();
                combatHudController?.SetFeatureEnabled(true);
                combatHudController?.Bind(_prototypeController.RuntimeQueryController, ResolveParentCanvas());
            }
            else
            {
                BoardGameCombatHudController combatHudController = _combatHudController != null
                    ? _combatHudController
                    : FindObjectOfType<BoardGameCombatHudController>();
                combatHudController?.SetFeatureEnabled(false);
            }

            _itemBarController?.Bind(_prototypeController.RuntimeQueryController, _prototypeController.ItemUseController);
            ResolveLevelUpController()?.Bind(
                _prototypeController.RuntimeQueryController,
                _prototypeController.ProgressionController);
            _selectionController?.Bind(
                _prototypeController.RuntimeQueryController,
                _prototypeController.SelectionStateController,
                _prototypeController.AgentFocusController,
                _prototypeController.TargetRedirectController,
                _prototypeController.ProgressionController,
                _worldCamera != null ? _worldCamera : Camera.main);

            if (_prototypeController.IsBagSystemEnabled)
            {
                ResolveLootInventoryController()?.Bind(
                    _prototypeController.RuntimeQueryController,
                    _prototypeController.LootInteractionController,
                    ResolveParentCanvas());
            }
        }

        private BoardGameCombatHudController ResolveCombatHudController()
        {
            if (_combatHudController != null)
            {
                return _combatHudController;
            }

            _combatHudController = FindObjectOfType<BoardGameCombatHudController>();

            if (_combatHudController != null)
            {
                return _combatHudController;
            }

            Canvas parentCanvas = ResolveParentCanvas();

            if (parentCanvas == null)
            {
                GameObject overlayObject = new GameObject(
                    "BoardGameCombatOverlay",
                    typeof(RectTransform),
                    typeof(BoardGameCombatHudController));
                _combatHudController = overlayObject.GetComponent<BoardGameCombatHudController>();
                return _combatHudController;
            }

            GameObject combatObject = new GameObject(
                "BoardGameCombatOverlay",
                typeof(RectTransform),
                typeof(BoardGameCombatHudController));
            RectTransform rectTransform = combatObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parentCanvas.transform, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.SetAsLastSibling();

            _combatHudController = combatObject.GetComponent<BoardGameCombatHudController>();
            return _combatHudController;
        }
        private void Update()
        {
            _prototypeController?.Tick(Time.deltaTime);
        }

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

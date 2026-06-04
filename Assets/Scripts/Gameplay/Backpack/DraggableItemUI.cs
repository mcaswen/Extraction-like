using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单个背包物品的运行时视图
/// 负责拖拽交互与基础显示，不直接管理网格规则本身
/// </summary>
[RequireComponent(typeof(Image))]
public partial class DraggableItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Ownership")]
    public InventoryUIController CurrentGrid;

    [Header("Item Data")]
    public bool IsDebugItem;

    [Header("Stack")]
    public Text AmountText;
    public bool AutoTickSearchProgress = true;

    public Vector2Int _originalGridIndex;
    public bool _originalIsRotated;

    private InventoryItemRuntimeState _runtimeState = new InventoryItemRuntimeState();
    private InventoryUIController _lastHoveredGrid;
    private InventoryUIController _lastPreviewGrid;
    private Vector2Int _lastPreviewIndex;
    private int _lastPreviewWidth;
    private int _lastPreviewHeight;
    private bool _hasPreviewPlacement;
    private bool _currentPreviewIsRotated;
    private Vector3 _visualDragOffset;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Image _itemImage;
    private Transform _originalParent;
    private RectTransform _searchOverlayRoot;
    private CanvasGroup _searchOverlayCanvasGroup;
    private Image _searchBackdropImage;
    private Image _searchOuterTrackImage;
    private Image _searchProgressImage;
    private Image _searchPulseRingImage;
    private Image _searchSweepImage;
    private Image _searchCenterGlowImage;
    private Image _searchRevealFlashImage;
    private bool _isRevealAnimating;
    private float _revealAnimationTimer;

    private static Sprite _defaultSearchSprite;
    private const float RevealAnimationDuration = 0.32f;

    public static DraggableItemUI CurrentlyDraggedItem;

    public InventoryItemRuntimeState RuntimeState => _runtimeState;
    public InventoryItemData ItemData
    {
        get => _runtimeState.ItemData;
        set => _runtimeState.ItemData = value;
    }
    public int CurrentAmount
    {
        get => _runtimeState.Amount;
        set => _runtimeState.Amount = value;
    }
    public string RuntimeItemId
    {
        get => _runtimeState.RuntimeItemId;
        set => _runtimeState.RuntimeItemId = value;
    }
    public List<ContainerItemSaveData> InternalItems
    {
        get => _runtimeState.InternalItems;
        set => _runtimeState.InternalItems = value;
    }
    public List<ContainerCellStateSaveData> InternalCellStates
    {
        get => _runtimeState.InternalCellStates;
        set => _runtimeState.InternalCellStates = value;
    }
    public bool RequiresSearch => _runtimeState.RequiresSearch;
    public bool IsSearched => _runtimeState.IsSearched;
    public float SearchProgressSeconds => _runtimeState.SearchProgressSeconds;
    public float SearchDurationSeconds => _runtimeState.SearchDurationSeconds;

    private void Awake()
    {
        EnsureComponents();
    }

    private void Update()
    {
        if (AutoTickSearchProgress)
        {
            TickSearchProgress();
        }

        TickSearchRevealAnimation();
    }
}

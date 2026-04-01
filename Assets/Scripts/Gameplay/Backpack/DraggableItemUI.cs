using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 单个背包物品的运行时视图
/// 负责拖拽交互与基础显示，不直接管理网格规则本身
/// </summary>
[RequireComponent(typeof(Image))]
public partial class DraggableItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IInventoryItemView
{
    [Header("Ownership")]
    public InventoryUIController CurrentGrid;

    [Header("Item Data")]
    public InventoryItemData ItemData;
    public bool IsDebugItem;

    [Header("Stack")]
    public int CurrentAmount = 1;
    public Text AmountText;
    public string RuntimeItemId;
    public bool AutoTickSearchProgress = true;

    public Vector2Int _originalGridIndex;
    public bool _originalIsRotated;
    public List<ContainerItemSaveData> InternalItems = new List<ContainerItemSaveData>();
    public List<ContainerCellStateSaveData> InternalCellStates = new List<ContainerCellStateSaveData>();

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
    private bool _requiresSearch;
    private bool _isSearched = true;
    private float _searchProgressSeconds;
    private float _searchDurationSeconds;
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

    InventoryItemData IInventoryItemView.ItemData => ItemData;
    public bool RequiresSearch => _requiresSearch;
    public bool IsSearched => _isSearched;
    public float SearchProgressSeconds => _searchProgressSeconds;
    public float SearchDurationSeconds => _searchDurationSeconds;

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

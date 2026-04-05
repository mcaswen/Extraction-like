using System.Collections.Generic;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// BoardGame 运行时战利品背包桥接
    /// 运行时创建最小可用的玩家背包和战利品网格，并复用 Bag 系统的拖拽交互
    /// </summary>
    public sealed class BoardGameLootInventoryController : MonoBehaviour
    {
        private const float GridCellSize = 52f;
        private const float GridSpacing = 4f;

        private readonly Dictionary<string, BoardItemInstance> _itemInstancesByRuntimeId =
            new Dictionary<string, BoardItemInstance>();

        private readonly Dictionary<string, int> _lootSequenceByRuntimeId =
            new Dictionary<string, int>();

        private readonly Dictionary<string, InventoryItemData> _runtimeItemDataByItemId =
            new Dictionary<string, InventoryItemData>();

        private readonly Dictionary<int, Sprite> _raritySpritesByKey =
            new Dictionary<int, Sprite>();

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameLootInteractionController _lootInteractionController;
        private Canvas _parentCanvas;
        private GameObject _overlayRoot;
        private InventoryUIController _playerGrid;
        private InventoryUIController _lootGrid;
        private InventoryItemFactory _itemFactory;
        private RectTransform _dragLayer;
        private Text _headerText;
        private Text _hintText;
        private bool _isOpen;
        private int _revealedItemCount;
        private int _totalItemCount;

        /// <summary>
        /// 绑定 loot 模块和只读查询控制器，并初始化 loot overlay
        /// </summary>
        public void Bind(
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameLootInteractionController lootInteractionController,
            Canvas parentCanvas)
        {
            _runtimeQueryController = runtimeQueryController;
            _lootInteractionController = lootInteractionController;
            _parentCanvas = parentCanvas;
            EnsureOverlay();
        }

        /// <summary>
        /// 处理 loot 面板的打开、关闭和逐件揭露流程
        /// </summary>
        private void Update()
        {
            if (_runtimeQueryController == null || _lootInteractionController == null)
            {
                return;
            }

            if (!_runtimeQueryController.IsBagSystemEnabled)
            {
                if (_isOpen)
                {
                    CloseLootNode();
                }

                return;
            }

            if (!_isOpen)
            {
                if (Input.GetKeyDown(KeyCode.F) && _lootInteractionController.TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState))
                {
                    OpenLootNode(nodeState);
                }

                return;
            }

            TickReveal(Time.unscaledDeltaTime);

            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape))
            {
                CloseLootNode();
            }
        }

        /// <summary>
        /// 打开当前节点的 loot 面板，并把节点与玩家库存投影成 Bag 运行时网格
        /// </summary>
        private void OpenLootNode(BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null)
            {
                return;
            }

            // overlay 和 EventSystem 都可能来自运行时补建，所以每次打开前都确保依赖齐全
            EnsureOverlay();
            EnsureEventSystem();
            _overlayRoot.SetActive(true);
            _overlayRoot.transform.SetAsLastSibling();

            _itemInstancesByRuntimeId.Clear();
            _lootSequenceByRuntimeId.Clear();
            _revealedItemCount = nodeState.LootRevealedItemCount;
            _totalItemCount = Mathf.Max(nodeState.LootTotalItemCount, nodeState.LootContainerItems.Count);

            // 左侧玩家背包和右侧 loot 网格都从运行时状态重新投影，避免残留上次 UI 状态
            BuildPlayerInventoryGrid();
            BuildLootInventoryGrid(nodeState);
            RefreshTexts(nodeState);
            _isOpen = true;
        }

        /// <summary>
        /// 关闭 loot 面板，并把两个网格中的结果回写回 BoardGame 运行时状态
        /// </summary>
        private void CloseLootNode()
        {
            if (!_isOpen)
            {
                return;
            }

            List<BoardItemInstance> playerItems = ExtractPlayerInventoryItems();
            List<BoardLootContainerItemState> remainingLootItems = ExtractRemainingLootItems();

            // 关闭时从两个网格重新抽取结果，再统一交回 loot 模块做状态回写
            _lootInteractionController?.CloseActiveLootNode(remainingLootItems, playerItems, _revealedItemCount);
            if (_overlayRoot != null)
            {
                _overlayRoot.SetActive(false);
            }

            _isOpen = false;
        }

        /// <summary>
        /// 推进当前待揭露物品的搜索进度
        /// 每次只允许一件物品前进，保持 BoardGame 的顺序揭露节奏
        /// </summary>
        private void TickReveal(float deltaTime)
        {
            DraggableItemUI currentRevealItem = GetCurrentRevealItem();

            if (currentRevealItem == null)
            {
                return;
            }

            bool completedReveal = currentRevealItem.AdvanceSearchProgressManually(deltaTime);

            if (!completedReveal)
            {
                return;
            }

            _revealedItemCount = Mathf.Min(_revealedItemCount + 1, _totalItemCount);
            _lootInteractionController.ApplyLootRevealProgress(_revealedItemCount);
            RefreshTexts(_lootInteractionController.GetActiveLootNodeState());
        }

        /// <summary>
        /// 根据当前玩家库存构造背包网格快照
        /// 若历史数据中的物品数量超出配置槽位，则临时扩列避免吞物品
        /// </summary>
        private void BuildPlayerInventoryGrid()
        {
            List<ContainerItemSaveData> playerItems = new List<ContainerItemSaveData>();
            BoardAgentState agentState = _runtimeQueryController.GetActiveInteractionAgentState() ??
                                         _runtimeQueryController.GetFocusedAgentState();

            if (agentState == null)
            {
                _playerGrid.RebuildGridUI(
                    _runtimeQueryController.BagLayoutSettings.PlayerInventoryColumns,
                    _runtimeQueryController.BagLayoutSettings.PlayerInventoryRows,
                    new List<Vector2Int>());
                _playerGrid.LoadFromRuntimeState(new List<ContainerItemSaveData>(), new List<ContainerCellStateSaveData>());
                return;
            }

            IReadOnlyList<BoardItemInstance> inventoryItems = agentState.InventoryState.Items;

            foreach (BoardItemInstance itemInstance in inventoryItems)
            {
                if (itemInstance == null)
                {
                    continue;
                }

                playerItems.Add(CreateSaveDataForBoardItem(itemInstance, false, 0f, true));
            }

            int rows = _runtimeQueryController.BagLayoutSettings.PlayerInventoryRows;
            int configuredColumns = _runtimeQueryController.BagLayoutSettings.PlayerInventoryColumns;
            int columns = configuredColumns;
            int availableSlots = configuredColumns * rows;

            if (playerItems.Count > availableSlots)
            {
                // 这里不直接丢物品，而是临时扩列兜底，让旧存档或异常数据至少能被看见和手动整理
                columns = Mathf.CeilToInt(playerItems.Count / (float)rows);
                Debug.LogWarning(
                    $"BoardGame inventory currently contains {playerItems.Count} item(s), exceeding the configured backpack size of {availableSlots} slots. " +
                    "Temporarily expanding the runtime grid to avoid dropping items.");
            }

            if (_playerGrid == null)
            {
                return;
            }

            AssignSequentialPositions(playerItems, columns);
            _playerGrid.RebuildGridUI(columns, rows, new List<Vector2Int>());
            _playerGrid.LoadFromRuntimeState(playerItems, new List<ContainerCellStateSaveData>());
        }

        /// <summary>
        /// 根据节点 loot 容器状态构造右侧战利品网格，并关闭 Bag 默认的自动搜索推进
        /// </summary>
        private void BuildLootInventoryGrid(BoardNodeRuntimeState nodeState)
        {
            List<ContainerItemSaveData> lootItems = new List<ContainerItemSaveData>();

            foreach (BoardLootContainerItemState itemState in nodeState.LootContainerItems)
            {
                if (itemState?.ItemInstance == null)
                {
                    continue;
                }

                // reveal 顺序和进度都要保留下来，关闭后节点才能继续上次未完成的搜索流程
                _lootSequenceByRuntimeId[itemState.ItemInstance.InstanceId] = itemState.RevealSequenceIndex;
                lootItems.Add(CreateSaveDataForBoardItem(
                    itemState.ItemInstance,
                    !itemState.IsRevealed,
                    itemState.RevealDurationSeconds,
                    itemState.IsRevealed,
                    itemState.GridX,
                    itemState.GridY,
                    itemState.IsRotated,
                    itemState.RevealProgressSeconds));
            }

            if (_lootGrid == null)
            {
                return;
            }

            _lootGrid.RebuildGridUI(nodeState.LootContainerColumns, nodeState.LootContainerRows, new List<Vector2Int>());
            _lootGrid.LoadFromRuntimeState(lootItems, new List<ContainerCellStateSaveData>());

            // BoardGame 自己驱动逐件 reveal，所以这里显式关掉 Bag 默认的自动推进
            foreach (DraggableItemUI itemView in EnumerateGridItems(_lootGrid))
            {
                itemView.SetSearchAutoTickEnabled(false);
            }
        }

        /// <summary>
        /// 从玩家网格和被拖进玩家侧的物品中抽回最终库存列表
        /// </summary>
        private List<BoardItemInstance> ExtractPlayerInventoryItems(IEnumerable<ContainerItemSaveData> saveDataList = null)
        {
            List<BoardItemInstance> extractedItems = new List<BoardItemInstance>();
            // Only items still placed in the player grid remain in the player's inventory.

            IEnumerable<ContainerItemSaveData> source = saveDataList ??
                (_playerGrid != null
                    ? _playerGrid.ExtractSaveData()
                    : new List<ContainerItemSaveData>());

            foreach (ContainerItemSaveData saveData in source)
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (_itemInstancesByRuntimeId.TryGetValue(saveData.RuntimeItemId, out BoardItemInstance itemInstance) && itemInstance != null)
                {
                    extractedItems.Add(itemInstance);
                }
            }

            return extractedItems;
        }

        /// <summary>
        /// 从 loot 网格中抽回仍然属于节点的剩余物品，并恢复揭露顺序与进度
        /// </summary>
        private List<BoardLootContainerItemState> ExtractRemainingLootItems(IEnumerable<ContainerItemSaveData> saveDataList = null)
        {
            List<BoardLootContainerItemState> remainingItems = new List<BoardLootContainerItemState>();
            int nextRevealSequence = GetNextRevealSequenceIndex();

            IEnumerable<ContainerItemSaveData> source = saveDataList ??
                (_lootGrid != null
                    ? _lootGrid.ExtractSaveData()
                    : new List<ContainerItemSaveData>());

            foreach (ContainerItemSaveData saveData in source)
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.RuntimeItemId))
                {
                    continue;
                }

                // 只有原本就属于 loot 节点的物品，关闭时才写回节点剩余掉落
                if (!_itemInstancesByRuntimeId.TryGetValue(saveData.RuntimeItemId, out BoardItemInstance itemInstance) || itemInstance == null)
                {
                    continue;
                }

                int revealSequence = _lootSequenceByRuntimeId.TryGetValue(saveData.RuntimeItemId, out int sequenceIndex)
                    ? sequenceIndex
                    : nextRevealSequence++;
                float revealDuration = saveData.SearchDurationSeconds > 0f
                    ? saveData.SearchDurationSeconds
                    : 0.01f;

                BoardLootContainerItemState itemState = new BoardLootContainerItemState(
                    itemInstance,
                    saveData.X,
                    saveData.Y,
                    revealSequence,
                    revealDuration);
                itemState.IsRotated = saveData.IsRotated;
                itemState.RevealProgressSeconds = saveData.SearchProgressSeconds;
                itemState.IsRevealed = saveData.IsSearched || saveData.SearchProgressSeconds >= revealDuration;
                remainingItems.Add(itemState);
            }

            remainingItems.Sort((left, right) => left.RevealSequenceIndex.CompareTo(right.RevealSequenceIndex));
            return remainingItems;
        }

        private int GetNextRevealSequenceIndex()
        {
            int nextSequence = 0;

            foreach (int sequenceIndex in _lootSequenceByRuntimeId.Values)
            {
                if (sequenceIndex >= nextSequence)
                {
                    nextSequence = sequenceIndex + 1;
                }
            }

            return nextSequence;
        }

        /// <summary>
        /// 找到当前应当被揭露的下一件物品
        /// 规则是所有未揭露物品里 reveal sequence 最小的那一个
        /// </summary>
        private DraggableItemUI GetCurrentRevealItem()
        {
            DraggableItemUI nextItem = null;
            int bestSequence = int.MaxValue;

            foreach (DraggableItemUI itemView in EnumerateGridItems(_lootGrid))
            {
                if (itemView == null || itemView.IsSearched || string.IsNullOrEmpty(itemView.RuntimeItemId))
                {
                    continue;
                }

                int sequence = _lootSequenceByRuntimeId.TryGetValue(itemView.RuntimeItemId, out int revealSequence)
                    ? revealSequence
                    : int.MaxValue;

                if (sequence >= bestSequence)
                {
                    continue;
                }

                bestSequence = sequence;
                nextItem = itemView;
            }

            return nextItem;
        }

        /// <summary>
        /// 把 BoardGame 物品实例投影成 Bag 运行时保存结构
        /// </summary>
        private ContainerItemSaveData CreateSaveDataForBoardItem(
            BoardItemInstance itemInstance,
            bool requiresSearch,
            float searchDurationSeconds,
            bool isSearched,
            int x = 0,
            int y = 0,
            bool isRotated = false,
            float searchProgressSeconds = 0f)
        {
            _itemInstancesByRuntimeId[itemInstance.InstanceId] = itemInstance;

            return new ContainerItemSaveData
            {
                RuntimeItemId = itemInstance.InstanceId,
                ItemData = GetOrCreateRuntimeItemData(itemInstance),
                Amount = 1,
                X = x,
                Y = y,
                IsRotated = isRotated,
                RequiresSearch = requiresSearch,
                IsSearched = isSearched,
                SearchProgressSeconds = searchProgressSeconds,
                SearchDurationSeconds = requiresSearch ? Mathf.Max(0.01f, searchDurationSeconds) : 0f,
                InternalItems = new List<ContainerItemSaveData>(),
                InternalCellStates = new List<ContainerCellStateSaveData>()
            };
        }

        /// <summary>
        /// 为 BoardGame 物品懒创建一个最小可用的 Bag 物品配置
        /// 这里只保留拖拽、显示和搜索所需的数据，不承载 BoardGame 的真实领域状态
        /// </summary>
        private InventoryItemData GetOrCreateRuntimeItemData(BoardItemInstance itemInstance)
        {
            string itemId = string.IsNullOrEmpty(itemInstance.ItemId)
                ? itemInstance.InstanceId
                : itemInstance.ItemId;

            if (_runtimeItemDataByItemId.TryGetValue(itemId, out InventoryItemData cachedData) && cachedData != null)
            {
                return cachedData;
            }

            InventoryItemData itemData = ScriptableObject.CreateInstance<InventoryItemData>();
            itemData.hideFlags = HideFlags.HideAndDontSave;
            itemData.ItemID = itemId;
            itemData.ItemName = itemInstance.DisplayName;
            itemData.Width = 1;
            itemData.Height = 1;
            itemData.IsStackable = false;
            itemData.MaxStack = 1;
            itemData.Type = itemInstance.ItemCategory == BoardItemCategory.Consumable
                ? ItemType.Medical
                : ItemType.Junk;
            itemData.Rarity = ConvertRarity(itemInstance.ItemRarity);
            itemData.ItemIcon = GetOrCreateRaritySprite(itemData.Rarity, itemData.Type);
            itemData.RequiresSearchInLootContainer = false;
            itemData.SearchDurationOverride = -1f;
            _runtimeItemDataByItemId[itemId] = itemData;
            return itemData;
        }

        /// <summary>
        /// 按稀有度和类型生成一个纯色占位图标
        /// BoardGame 当前不依赖正式背包图标资源，所以在运行时即时生成即可
        /// </summary>
        private Sprite GetOrCreateRaritySprite(ItemRarity rarity, ItemType itemType)
        {
            int key = ((int)rarity << 8) | (int)itemType;

            if (_raritySpritesByKey.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            Color fillColor = itemType == ItemType.Medical
                ? new Color(0.82f, 0.26f, 0.22f, 1f)
                : rarity switch
                {
                    ItemRarity.Common => new Color(0.44f, 0.68f, 0.82f, 1f),
                    ItemRarity.Uncommon => new Color(0.35f, 0.76f, 0.47f, 1f),
                    ItemRarity.Rare => new Color(0.28f, 0.48f, 0.9f, 1f),
                    ItemRarity.Epic => new Color(0.66f, 0.35f, 0.86f, 1f),
                    ItemRarity.Legendary => new Color(0.92f, 0.66f, 0.21f, 1f),
                    _ => new Color(0.44f, 0.68f, 0.82f, 1f)
                };

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, fillColor);
            texture.Apply();

            sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            _raritySpritesByKey[key] = sprite;
            return sprite;
        }

        private static ItemRarity ConvertRarity(BoardItemRarity rarity)
        {
            return rarity switch
            {
                BoardItemRarity.Common => ItemRarity.Common,
                BoardItemRarity.Uncommon => ItemRarity.Uncommon,
                BoardItemRarity.Rare => ItemRarity.Rare,
                BoardItemRarity.Epic => ItemRarity.Epic,
                BoardItemRarity.Legendary => ItemRarity.Legendary,
                _ => ItemRarity.Common
            };
        }

        /// <summary>
        /// 刷新 loot 面板头部文案与揭露进度文本
        /// </summary>
        private void RefreshTexts(BoardNodeRuntimeState nodeState)
        {
            BoardAgentState activeInteractionAgentState = _runtimeQueryController != null
                ? _runtimeQueryController.GetActiveInteractionAgentState() ?? _runtimeQueryController.GetFocusedAgentState()
                : null;
            string agentLabel = activeInteractionAgentState != null ? activeInteractionAgentState.DisplayName : "Focused AI";

            if (_headerText != null)
            {
                string nodeName = nodeState != null ? nodeState.NodeId : "Loot";
                _headerText.text = $"Loot Search - {nodeName} - {agentLabel}";
            }

            if (_hintText != null)
            {
                _hintText.text = $"Game paused  {agentLabel}  Revealed {_revealedItemCount}/{Mathf.Max(0, _totalItemCount)}  Press F or Esc to close";
            }
        }

        /// <summary>
        /// 创建 loot overlay 及其内部的双栏网格 UI
        /// 整个桥接界面都在运行时搭建，避免要求场景额外维护一套专用预制
        /// </summary>
        private void EnsureOverlay()
        {
            if (_overlayRoot != null)
            {
                return;
            }

            Canvas canvas = _parentCanvas;

            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("BoardGameLootCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            _overlayRoot = new GameObject("BoardGameLootOverlay", typeof(RectTransform), typeof(Image));
            _overlayRoot.transform.SetParent(canvas.transform, false);
            RectTransform overlayRect = _overlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image overlayImage = _overlayRoot.GetComponent<Image>();
            overlayImage.color = new Color(0.04f, 0.06f, 0.08f, 0.82f);
            overlayImage.raycastTarget = true;

            // 中央面板只负责布局两列网格，标题和提示文本直接挂在 overlay 根节点上
            // 这样无论网格内部如何重建，都不会影响顶部文案的锚点
            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            panelObject.transform.SetParent(_overlayRoot.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1320f, 720f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.09f, 0.12f, 0.16f, 0.96f);

            HorizontalLayoutGroup layoutGroup = panelObject.GetComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 28f;
            layoutGroup.padding = new RectOffset(28, 28, 76, 28);
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = true;

            _headerText = CreateLabel("Header", _overlayRoot.transform, 26, FontStyle.Bold, TextAnchor.UpperLeft);
            RectTransform headerRect = _headerText.rectTransform;
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -28f);
            headerRect.sizeDelta = new Vector2(1260f, 36f);

            _hintText = CreateLabel("Hint", _overlayRoot.transform, 18, FontStyle.Normal, TextAnchor.UpperRight);
            RectTransform hintRect = _hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -66f);
            hintRect.sizeDelta = new Vector2(1260f, 28f);

            Transform playerColumn = CreateGridColumn("PlayerColumn", panelObject.transform, "Backpack");
            Transform lootColumn = CreateGridColumn("LootColumn", panelObject.transform, "Loot");

            _playerGrid = CreateGridView("PlayerGrid", playerColumn, 8, 4);
            _lootGrid = CreateGridView("LootGrid", lootColumn, 8, 3);

            GameObject dragLayerObject = new GameObject("DragLayer", typeof(RectTransform), typeof(CanvasGroup));
            dragLayerObject.transform.SetParent(_overlayRoot.transform, false);
            _dragLayer = dragLayerObject.GetComponent<RectTransform>();
            _dragLayer.anchorMin = Vector2.zero;
            _dragLayer.anchorMax = Vector2.one;
            _dragLayer.offsetMin = Vector2.zero;
            _dragLayer.offsetMax = Vector2.zero;
            dragLayerObject.GetComponent<CanvasGroup>().blocksRaycasts = false;

            _itemFactory = _overlayRoot.AddComponent<InventoryItemFactory>();
            _itemFactory.GlobalDragLayer = _dragLayer;
            _overlayRoot.SetActive(false);
        }

        /// <summary>
        /// 创建单侧网格列容器，包含标题和网格挂载区域
        /// </summary>
        private static Transform CreateGridColumn(string name, Transform parent, string title)
        {
            GameObject columnObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            columnObject.transform.SetParent(parent, false);
            columnObject.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 0.96f);

            VerticalLayoutGroup layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement layoutElement = columnObject.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;

            Text titleText = CreateLabel("Title", columnObject.transform, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.text = title;
            titleText.color = new Color(0.94f, 0.96f, 0.99f, 0.98f);
            titleText.rectTransform.sizeDelta = new Vector2(0f, 28f);
            return columnObject.transform;
        }

        /// <summary>
        /// 创建一个最小可运行的背包网格视图
        /// </summary>
        private static InventoryUIController CreateGridView(string name, Transform parent, int columns, int rows)
        {
            GameObject gridObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(InventoryGridController), typeof(InventoryUIController));
            gridObject.transform.SetParent(parent, false);

            Image backgroundImage = gridObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.1f, 0.13f, 0.94f);

            LayoutElement layoutElement = gridObject.GetComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 1f;

            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.sizeDelta = new Vector2(0f, 0f);

            GameObject backgroundLayer = new GameObject("GridBackground", typeof(RectTransform), typeof(GridLayoutGroup));
            backgroundLayer.transform.SetParent(gridObject.transform, false);

            GameObject itemContainer = new GameObject("ItemContainer", typeof(RectTransform));
            itemContainer.transform.SetParent(gridObject.transform, false);

            GameObject highlighter = new GameObject("Highlighter", typeof(RectTransform), typeof(Image));
            highlighter.transform.SetParent(itemContainer.transform, false);

            InventoryGridController gridController = gridObject.GetComponent<InventoryGridController>();
            gridController.Columns = columns;
            gridController.Rows = rows;
            gridController.BlockedCells = new List<Vector2Int>();

            InventoryUIController uiController = gridObject.GetComponent<InventoryUIController>();
            uiController.GridBackground = backgroundLayer.transform;
            uiController.ItemContainer = itemContainer.GetComponent<RectTransform>();
            uiController.Highlighter = highlighter.GetComponent<RectTransform>();
            uiController.CellSize = GridCellSize;
            uiController.Spacing = GridSpacing;
            return uiController;
        }

        /// <summary>
        /// 创建一个运行时文本标签
        /// </summary>
        private static Text CreateLabel(string name, Transform parent, int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.94f, 0.98f, 0.98f);
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// 按顺序把一组物品重新摆成从左到右、从上到下的网格位置
        /// </summary>
        private static void AssignSequentialPositions(List<ContainerItemSaveData> items, int columns)
        {
            for (int index = 0; index < items.Count; index++)
            {
                ContainerItemSaveData item = items[index];
                item.X = index % columns;
                item.Y = index / columns;
                item.IsRotated = false;
            }
        }

        /// <summary>
        /// 枚举某个网格容器下当前存在的所有物品视图
        /// </summary>
        private static IEnumerable<DraggableItemUI> EnumerateGridItems(InventoryUIController grid)
        {
            if (grid == null || grid.ItemContainer == null)
            {
                yield break;
            }

            foreach (Transform child in grid.ItemContainer)
            {
                DraggableItemUI itemView = child.GetComponent<DraggableItemUI>();

                if (itemView != null)
                {
                    yield return itemView;
                }
            }
        }

        /// <summary>
        /// 保证运行时存在 EventSystem，避免纯原型场景里无法响应 UI 输入
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemObject.transform.SetParent(null, false);
        }
    }
}

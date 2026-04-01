using System.Collections.Generic;
using System.Linq;
using BoardGame.Runtime;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoardGame.Presentation
{
    /// <summary>
    /// BoardGame 运行时战利品背包桥接。
    /// 运行时创建最小可用的玩家背包和战利品网格，并复用 Bag 系统的拖拽交互。
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

        private BoardGamePrototypeController _prototypeController;
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

        public void Bind(BoardGamePrototypeController prototypeController, Canvas parentCanvas)
        {
            _prototypeController = prototypeController;
            _parentCanvas = parentCanvas;
            EnsureOverlay();
        }

        private void Update()
        {
            if (_prototypeController == null)
            {
                return;
            }

            if (!_isOpen)
            {
                if (Input.GetKeyDown(KeyCode.F) && _prototypeController.TryOpenActiveLootNode(out BoardNodeRuntimeState nodeState))
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

        private void OpenLootNode(BoardNodeRuntimeState nodeState)
        {
            if (nodeState == null)
            {
                return;
            }

            EnsureOverlay();
            EnsureEventSystem();

            _overlayRoot.SetActive(true);
            _overlayRoot.transform.SetAsLastSibling();
            _isOpen = true;
            _itemInstancesByRuntimeId.Clear();
            _lootSequenceByRuntimeId.Clear();
            _revealedItemCount = nodeState.LootRevealedItemCount;
            _totalItemCount = Mathf.Max(nodeState.LootTotalItemCount, nodeState.LootContainerItems.Count);

            BuildPlayerInventoryGrid();
            BuildLootInventoryGrid(nodeState);
            RefreshTexts(nodeState);
        }

        private void CloseLootNode()
        {
            if (!_isOpen || _prototypeController == null)
            {
                return;
            }

            if (DraggableItemUI.CurrentlyDraggedItem != null)
            {
                DraggableItemUI.CurrentlyDraggedItem.BounceBack();
                DraggableItemUI.CurrentlyDraggedItem.ForceEndDrag();
            }

            List<BoardItemInstance> playerItems = ExtractPlayerInventoryItems();
            List<BoardLootContainerItemState> remainingLootItems = ExtractRemainingLootItems();
            _prototypeController.CloseActiveLootNode(remainingLootItems, playerItems, _revealedItemCount);
            _overlayRoot.SetActive(false);
            _isOpen = false;
        }

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
            _prototypeController.ApplyLootRevealProgress(_revealedItemCount);
            RefreshTexts(_prototypeController.GetActiveLootNodeState());
        }

        private void BuildPlayerInventoryGrid()
        {
            List<ContainerItemSaveData> playerItems = new List<ContainerItemSaveData>();
            IReadOnlyList<BoardItemInstance> inventoryItems = _prototypeController.SessionState.AgentState.InventoryState.Items;

            foreach (BoardItemInstance itemInstance in inventoryItems)
            {
                if (itemInstance == null)
                {
                    continue;
                }

                playerItems.Add(CreateSaveDataForBoardItem(itemInstance, false, 0f, true));
            }

            int rows = _prototypeController.BagLayoutSettings.PlayerInventoryRows;
            int columns = Mathf.Max(
                _prototypeController.BagLayoutSettings.PlayerInventoryColumns,
                playerItems.Count <= 0 ? _prototypeController.BagLayoutSettings.PlayerInventoryColumns : Mathf.CeilToInt(playerItems.Count / (float)rows));

            AssignSequentialPositions(playerItems, columns);
            _playerGrid.RebuildGridUI(columns, rows, new List<Vector2Int>());
            _playerGrid.LoadFromRuntimeState(playerItems, new List<ContainerCellStateSaveData>());
        }

        private void BuildLootInventoryGrid(BoardNodeRuntimeState nodeState)
        {
            List<ContainerItemSaveData> lootItems = new List<ContainerItemSaveData>();

            foreach (BoardLootContainerItemState itemState in nodeState.LootContainerItems)
            {
                if (itemState?.ItemInstance == null)
                {
                    continue;
                }

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

            _lootGrid.RebuildGridUI(nodeState.LootContainerColumns, nodeState.LootContainerRows, new List<Vector2Int>());
            _lootGrid.LoadFromRuntimeState(lootItems, new List<ContainerCellStateSaveData>());

            foreach (DraggableItemUI itemView in EnumerateGridItems(_lootGrid))
            {
                itemView.SetSearchAutoTickEnabled(false);
            }
        }

        private List<BoardItemInstance> ExtractPlayerInventoryItems()
        {
            List<BoardItemInstance> extractedItems = new List<BoardItemInstance>();
            HashSet<string> collectedRuntimeIds = new HashSet<string>();

            foreach (ContainerItemSaveData saveData in _playerGrid.ExtractSaveData())
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (_itemInstancesByRuntimeId.TryGetValue(saveData.RuntimeItemId, out BoardItemInstance itemInstance) && itemInstance != null)
                {
                    extractedItems.Add(itemInstance);
                    collectedRuntimeIds.Add(saveData.RuntimeItemId);
                }
            }

            foreach (ContainerItemSaveData saveData in _lootGrid.ExtractSaveData())
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (_lootSequenceByRuntimeId.ContainsKey(saveData.RuntimeItemId) || collectedRuntimeIds.Contains(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (_itemInstancesByRuntimeId.TryGetValue(saveData.RuntimeItemId, out BoardItemInstance itemInstance) && itemInstance != null)
                {
                    extractedItems.Add(itemInstance);
                    collectedRuntimeIds.Add(saveData.RuntimeItemId);
                }
            }

            return extractedItems;
        }

        private List<BoardLootContainerItemState> ExtractRemainingLootItems()
        {
            List<BoardLootContainerItemState> remainingItems = new List<BoardLootContainerItemState>();

            foreach (ContainerItemSaveData saveData in _lootGrid.ExtractSaveData())
            {
                if (saveData == null || string.IsNullOrEmpty(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (!_lootSequenceByRuntimeId.ContainsKey(saveData.RuntimeItemId))
                {
                    continue;
                }

                if (!_itemInstancesByRuntimeId.TryGetValue(saveData.RuntimeItemId, out BoardItemInstance itemInstance) || itemInstance == null)
                {
                    continue;
                }

                int revealSequence = _lootSequenceByRuntimeId.TryGetValue(saveData.RuntimeItemId, out int sequenceIndex)
                    ? sequenceIndex
                    : remainingItems.Count;
                float revealDuration = saveData.SearchDurationSeconds > 0f
                    ? saveData.SearchDurationSeconds
                    : 0.45f;

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

        private void RefreshTexts(BoardNodeRuntimeState nodeState)
        {
            if (_headerText != null)
            {
                string nodeName = nodeState != null ? nodeState.NodeId : "Loot";
                _headerText.text = $"Loot Search - {nodeName}";
            }

            if (_hintText != null)
            {
                _hintText.text = $"Revealed {_revealedItemCount}/{Mathf.Max(0, _totalItemCount)}  Press F or Esc to close";
            }
        }

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

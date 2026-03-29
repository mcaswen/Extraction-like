using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
[RequireComponent(typeof(Image))] // 强制要求挂载此脚本的物体必须有Image组件
public class DraggableItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    // ==========================================
    // 变量声明区
    // ==========================================

    [Header("容器归属")]
    public InventoryUIController CurrentGrid;      // 记录当前物品挂在哪个背包/宝箱面板下
    private InventoryUIController _lastHoveredGrid;// 记录拖拽时，鼠标上一帧悬停的背包（用于跨背包时关闭高亮框）
    private Transform _originalParent;             // 记录开始拖拽前，物品原本的父物体层级[Header("物品数据与状态")]
    public InventoryItemData ItemData;             // 物品的数据模板（占几格、能不能堆叠等）

    [Header("测试配置")]
    public bool IsDebugItem = false;               // 是否为开局自动放置的测试物品
    public Vector2Int StartGridIndex;              // 测试物品的初始行列坐标

    // 【新增】：全局记录当前正飞在天上的物品，用于被强中断保护拦截！
    public static DraggableItemUI CurrentlyDraggedItem;

    // UI 组件缓存
    private RectTransform _rectTransform;          // 控制UI坐标和大小的核心组件
    private CanvasGroup _canvasGroup;              // 控制UI透明度、拦截鼠标射线的组件
    private Image _itemImage;                      // 物品的图片组件

    // 拖拽前的数据记录（用于失败时弹回老家）
    public Vector2Int _originalGridIndex;         // 拖拽前的二维数组行列坐标
    public bool _originalIsRotated;               // 拖拽前的旋转状态
    // 【替换为】：纯粹的局部网格偏移量，绝对免疫任何 Canvas 屏幕缩放！
    private Vector2 _localGridOffset;
    // 保留这个用于控制物品UI跟着鼠标飞的视觉偏差
    private Vector3 _visualDragOffset;

    // 拖拽过程中的状态记录
    private bool _currentPreviewIsRotated;         // 拖拽时，系统预测的当前旋转状态
    private Vector2 _screenDragOffset;             // 【极其关键】：记录鼠标点击点与物品左上角在屏幕像素上的绝对差值

    private Canvas _mainCanvas;                    // 物品所在的主画布（用于获取缩放比例等）

    [Header("堆叠与数量")]
    public int CurrentAmount = 1;                  // 当前堆叠数量
    public Text AmountText;                        // 显示数量的文字 UI

    // 【终极救命稻草】：确保组件绝对存在！
    private void EnsureComponents()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
            _itemImage = GetComponent<Image>();

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _rectTransform.anchorMin = new Vector2(0, 1);
            _rectTransform.anchorMax = new Vector2(0, 1);
            _rectTransform.pivot = new Vector2(0, 1);

            _mainCanvas = GetComponentInParent<Canvas>();
        }
    }


    // ==========================================
    // 生命周期与初始化
    // ==========================================

    void Awake()
    {
        EnsureComponents();
    }

    // 由工厂或测试脚本调用，正式初始化这个物品
    public void InitializeItem(InventoryItemData data, Vector2Int startPos, bool isRotated)
    {
        // 【核心修复】：在被生成并初始化时，无视是否隐藏，强行装配组件！防死空指针！
        EnsureComponents();

        ItemData = data;
        _originalGridIndex = startPos;
        _originalIsRotated = isRotated;

        // 强行同步预测旋转状态，防止克隆体生成时状态错乱
        _currentPreviewIsRotated = isRotated;

        // 替换UI图片
        if (data.ItemIcon != null) _itemImage.sprite = data.ItemIcon;

        // 更新UI的实际像素大小（长和宽）
        UpdateVisualSize(isRotated);
        // 将UI移动到背包面板下的精确局部坐标位置
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(startPos.x, startPos.y);

        // 刷新数量显示
        UpdateAmountText();
    }

    // 拖拽核心逻辑

    // 当鼠标刚刚按下并拖动的那一帧触发
    public void OnBeginDrag(PointerEventData eventData)
    {
        CurrentlyDraggedItem = this; // 记录自己正在被拖拽

        // 如果开着拆分窗口，立刻关掉
        if (SplitUIController.Instance != null) SplitUIController.Instance.CloseWindow();

        // 1. 将自己从底层数组的大脑中除名（腾出空位，方便后面自己和其他物品的检测）
        CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);

        // 1. 算出鼠标点下去的那一瞬间，鼠标在【当前背包】里的局部坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(CurrentGrid.ItemContainer, eventData.position, eventData.pressEventCamera, out Vector2 startMouseLocalPos);

        // 2. 拿到物品左上角本来在【当前背包】里的绝对正确局部坐标
        Vector2 startItemLocalPos = CurrentGrid.GetLocalPosition(_originalGridIndex.x, _originalGridIndex.y);

        // 3. 算出纯净偏差！(物品左上角坐标 - 鼠标坐标)
        _localGridOffset = startItemLocalPos - startMouseLocalPos;
        

        // --- 下面是视觉UI拖拽层转移，保持不变 ---
        transform.SetParent(InventoryItemFactory.Instance.GlobalDragLayer, true);

        // 4. 变成半透明，并关闭射线阻挡（让鼠标射线能穿透物品去探测下面的背包格子）
        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;

        RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)InventoryItemFactory.Instance.GlobalDragLayer, eventData.position, eventData.pressEventCamera, out Vector3 globalMousePos);
        _visualDragOffset = _rectTransform.position - globalMousePos;
    }

    // 当鼠标拖拽过程中，每一帧都会触发
    public void OnDrag(PointerEventData eventData)
    {
        // 1. 【更新物品UI的位置】：
        // eventData.position (当前鼠标像素) + _screenDragOffset (按下时记录的偏差) = 物品左上角理论上应该在的屏幕像素位置
        // 将这个屏幕像素位置，转换成拖拽层(GlobalDragLayer)里的世界坐标，并直接赋予物品！
        // （调试思路：如果你发现物品跟鼠标偏移了，一定是 GlobalDragLayer 的缩放/坐标系有畸变，导致这里转算 WorldPoint 不准）
        // 视觉跟随保持不变
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)InventoryItemFactory.Instance.GlobalDragLayer, eventData.position, eventData.pressEventCamera, out Vector3 globalMousePos))
        {
            _rectTransform.position = globalMousePos + _visualDragOffset;
        }

        // 2. 发射射线，探测鼠标当前指在哪一个背包/宝箱面板上
        InventoryUIController hoveredGrid = GetHoveredGrid(eventData);

        // 3. 跨面板处理：如果鼠标离开了上一个背包，把上一个背包的预测绿框关掉
        if (_lastHoveredGrid != null && _lastHoveredGrid != hoveredGrid) _lastHoveredGrid.HideHighlight();
        _lastHoveredGrid = hoveredGrid;

        // 4. 如果鼠标当前确实停留在某个背包上方
        if (hoveredGrid != null)
        {
            // 1. 拿到此时此刻，鼠标在【目标悬停背包】里的局部坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hoveredGrid.ItemContainer, eventData.position, eventData.pressEventCamera, out Vector2 currentMouseLocalPos);

            // 2. 预测坐标 = 当前鼠标的局部坐标 + 刚才算出的绝对纯净偏差！
            Vector2 predictedLocalPos = currentMouseLocalPos + _localGridOffset;

            // 3. 将预测的坐标换算成第几行第几列
            Vector2Int hoverIndex = hoveredGrid.GetGridIndex(predictedLocalPos);

            // 4.2 拿到当前背包的最大宽高，和物品在当前旋转状态下的宽高
            int cols = hoveredGrid.GetGridController().Columns;
            int rows = hoveredGrid.GetGridController().Rows;
            int currentW = _currentPreviewIsRotated ? ItemData.Height : ItemData.Width;
            int currentH = _currentPreviewIsRotated ? ItemData.Width : ItemData.Height;

            // 4.3 【智能挤压旋转算法】：
            // 算出物品在X轴和Y轴上，分别超出了背包边界多少格？
            int overshootX = hoverIndex.x < 0 ? -hoverIndex.x : (hoverIndex.x + currentW > cols ? (hoverIndex.x + currentW) - cols : 0);
            int overshootY = hoverIndex.y < 0 ? -hoverIndex.y : (hoverIndex.y + currentH > rows ? (hoverIndex.y + currentH) - rows : 0);

            // 假设下一秒的状态等于当前状态，然后开始纠正：
            bool nextRotatedState = _currentPreviewIsRotated;

            // 如果X轴挤压得更厉害，且当前的宽度更长，转过去可以缩短宽度，那就旋转！
            if (overshootX > 0 && overshootX >= overshootY && currentW > currentH) nextRotatedState = !_currentPreviewIsRotated;
            // 反之，如果Y轴挤压更厉害，且高度更长，那就旋转！
            else if (overshootY > 0 && overshootY > overshootX && currentH > currentW) nextRotatedState = !_currentPreviewIsRotated;

            // 如果预测的状态发生了变化，立刻更新物品UI的大小，并重新计算当前的宽和高
            if (nextRotatedState != _currentPreviewIsRotated)
            {
                _currentPreviewIsRotated = nextRotatedState;
                UpdateVisualSize(_currentPreviewIsRotated);
                currentW = _currentPreviewIsRotated ? ItemData.Height : ItemData.Width;
                currentH = _currentPreviewIsRotated ? ItemData.Width : ItemData.Height;
            }

            // 4.4 【裁判判定】：去底层大脑询问，现在这个格子加上现在的宽高，到底能不能放下？
            bool canPlace = hoveredGrid.GetGridController().IsSpaceAvailable(hoverIndex.x, hoverIndex.y, currentW, currentH);

            // 4.5 呼叫当前背包，把高亮框画出来（能放下画绿框，放不下画红框）
            hoveredGrid.ShowHighlight(hoverIndex.x, hoverIndex.y, currentW, currentH, canPlace);
        }
    }

    // 【PRD 模块三：丢弃到 3D 场景并保留持久化数据】
    // =========================================================
    private void DropToWorld()
    {
        if (ItemData.WorldPrefab != null)
        {
            // 找到玩家的位置（假设用 Tag 寻找，或者通过 GameManager 引用）
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 dropPosition = player != null ? player.transform.position + player.transform.forward * 1.5f + Vector3.up : Vector3.zero;

            // 在 3D 世界中生成该物品的物理模型
            GameObject dropObj = Instantiate(ItemData.WorldPrefab, dropPosition, Quaternion.identity);

            // 挂载或获取掉落物脚本，把“记忆”传给它
            WorldLootItem worldItem = dropObj.GetComponent<WorldLootItem>();
            if (worldItem == null) worldItem = dropObj.AddComponent<WorldLootItem>();

            worldItem.InitializeDrop(ItemData, CurrentAmount);

            Debug.Log($"丢弃操作：{ItemData.ItemName} 被扔到了地上！");

            // 彻底销毁自己这个 UI
            Destroy(this.gameObject);
        }
        else
        {
            Debug.LogWarning($"该物品没有配置 3D WorldPrefab，无法丢弃！已弹回。");
            BounceBack();
        }
    }

    // =========================================================
    // 【PRD 模块三：严格的防套娃约束 (Anti-Nesting Rules)】
    // =========================================================
    private bool CheckAntiMatryoshka(InventoryUIController targetGrid)
    {
        // 规则 2：【背包】绝对禁止放入任何容器（防止无限套娃）
        if (this.ItemData.Type == ItemType.Bag)
        {
            Debug.LogWarning("防套娃保护：背包类物品禁止放入其他网格容器中！");
            return false;
        }

        // 规则 1：【胸挂】放入时必须检查内部是否为空（这里先做类型拦截，后续有了多网格再校验内部）
        if (this.ItemData.Type == ItemType.Rig)
        {
            // TODO: 后续接入容器数据后，这里要 if (this.ContainerData.IsEmpty == false) return false;
            Debug.Log("防套娃检查：胸挂正在放入，请确保其内部为空！(目前暂时放行)");
        }

        return true; // 校验通过
    }



    // 当玩家松开鼠标，结束拖拽的那一帧触发
    public void OnEndDrag(PointerEventData eventData)
    {
        CurrentlyDraggedItem = this; // 记录自己正在被拖拽

        // 1. 恢复UI透明度，开启射线拦截（允许再次被点击）
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // 隐藏绿框
        if (_lastHoveredGrid != null) _lastHoveredGrid.HideHighlight();

        // 再次发射射线，确认最终扔在了哪个背包上
        InventoryUIController targetGrid = GetHoveredGrid(eventData);

        // 【核心修改 1：丢弃物品到 3D 场景】
        // 如果扔在了没有背包面板的空地上（UI 外）
        // =========================================================
        if (targetGrid == null)
        {
            DropToWorld(); // <--- 替换掉原来的 BounceBack()
            return;
        }

        // =========================================================
        // 【核心修改 2：放入前，进行防套娃安全性扫描】
        // =========================================================
        if (!CheckAntiMatryoshka(targetGrid))
        {
            BounceBack(); // 触发防套娃，弹回老家！
            return;
        }

        // 2. 重复 OnDrag 里的计算：利用 鼠标位置+屏幕偏差 推算出物品在目标背包里的绝对格子索引
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetGrid.ItemContainer, eventData.position, eventData.pressEventCamera, out Vector2 currentMouseLocalPos);

        Vector2 predictedLocalPos = currentMouseLocalPos + _localGridOffset;
        Vector2Int targetIndex = targetGrid.GetGridIndex(predictedLocalPos);

        int finalW = _currentPreviewIsRotated ? ItemData.Height : ItemData.Width;
        int finalH = _currentPreviewIsRotated ? ItemData.Width : ItemData.Height;
        InventoryGridController targetGridController = targetGrid.GetGridController();

        // 3. 终极判定：目标位置是纯净的空格子吗？
        bool isSpaceAvailable = targetGridController.IsSpaceAvailable(targetIndex.x, targetIndex.y, finalW, finalH);

        // 如果不是纯净的空格子（被占用了）
        if (!isSpaceAvailable)
        {
            // 获取被压住的物品都有谁
            HashSet<DraggableItemUI> blockingItems = targetGridController.GetItemsInArea(targetIndex.x, targetIndex.y, finalW, finalH);

            // 目前仅支持1换1的操作
            if (blockingItems.Count == 1)
            {
                DraggableItemUI blockingUI = null;
                foreach (var item in blockingItems) blockingUI = item; // 取出被压住的那个倒霉蛋

                // 【判定 A：堆叠系统】
                // 如果是同类型、且支持堆叠的物品
                if (this.ItemData == blockingUI.ItemData && this.ItemData.IsStackable)
                {
                    int totalAmount = this.CurrentAmount + blockingUI.CurrentAmount;

                    // 没有超出上限：全部融合给目标
                    if (totalAmount <= this.ItemData.MaxStack)
                    {
                        blockingUI.CurrentAmount = totalAmount;
                        blockingUI.UpdateAmountText();
                        Destroy(this.gameObject); // 自己销毁
                        return;
                    }
                    else
                    {
                        // 溢出：目标补满，自己扣除差值并弹回原处
                        int overflow = totalAmount - this.ItemData.MaxStack;
                        blockingUI.CurrentAmount = this.ItemData.MaxStack;
                        blockingUI.UpdateAmountText();

                        this.CurrentAmount = overflow;
                        this.UpdateAmountText();
                        BounceBack();
                        return;
                    }
                }

                // 【判定 B：完美互换系统 (仅限同容器)】
                if (targetGrid == CurrentGrid)
                {
                    // 1. 把倒霉蛋拿出来
                    targetGridController.RemoveItem(blockingUI, blockingUI._originalGridIndex.x, blockingUI._originalGridIndex.y, blockingUI._originalIsRotated);

                    // 2. 看看现在有位置放自己了吗？
                    if (targetGridController.IsSpaceAvailable(targetIndex.x, targetIndex.y, finalW, finalH))
                    {
                        // 自己先占住这个坑，防Bug
                        targetGridController.PlaceItem(this, targetIndex.x, targetIndex.y, _currentPreviewIsRotated);

                        int targetW = blockingUI._originalIsRotated ? blockingUI.ItemData.Height : blockingUI.ItemData.Width;
                        int targetH = blockingUI._originalIsRotated ? blockingUI.ItemData.Width : blockingUI.ItemData.Height;
                        bool targetFoundSpace = false;
                        Vector2Int targetNewPos = Vector2Int.zero;
                        bool targetNewRot = false;

                        // 尝试把倒霉蛋放到自己以前的位置（原地互换）
                        if (targetGridController.IsSpaceAvailable(_originalGridIndex.x, _originalGridIndex.y, targetW, targetH))
                        {
                            targetFoundSpace = true;
                            targetNewPos = _originalGridIndex;
                            targetNewRot = blockingUI._originalIsRotated;
                        }
                        // 尝试让倒霉蛋在背包里另找一个空位
                        else if (targetGridController.FindFirstAvailableSpace(blockingUI.ItemData.Width, blockingUI.ItemData.Height, out targetNewPos, out targetNewRot))
                        {
                            targetFoundSpace = true;
                        }

                        // 自己从坑里退出来
                        targetGridController.RemoveItem(this, targetIndex.x, targetIndex.y, _currentPreviewIsRotated);

                        // 互换成功执行
                        if (targetFoundSpace)
                        {
                            blockingUI.PlaceSuccessfully(targetNewPos, targetNewRot);
                            transform.SetParent(targetGrid.ItemContainer, false);
                            PlaceSuccessfully(targetIndex, _currentPreviewIsRotated);
                            return;
                        }
                    }
                    // 互换失败，把倒霉蛋放回原位
                    targetGridController.PlaceItem(blockingUI, blockingUI._originalGridIndex.x, blockingUI._originalGridIndex.y, blockingUI._originalIsRotated);
                }
            }

            // 非堆叠、或者是跨容器的非法挤压：全部拦截并弹回！
            Debug.LogWarning("目标位置有冲突，且不可堆叠/不可跨界互换！零容忍弹回！");
            BounceBack();
            return;
        }

        // --- 如果是完美的空位，直接放入新位置 ---
        transform.SetParent(targetGrid.ItemContainer, false);
        CurrentGrid = targetGrid;
        PlaceSuccessfully(targetIndex, _currentPreviewIsRotated);
    }

    // 被 GameUIController 强行中断时调用
    public void ForceEndDrag()
    {
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // =========================================================
        // 【核心修复】：强中断时，别忘了把刚才悬停的绿框/红框也一起关掉！
        // =========================================================
        if (_lastHoveredGrid != null)
        {
            _lastHoveredGrid.HideHighlight();
        }

        CurrentlyDraggedItem = null;
        if (SplitUIController.Instance != null) SplitUIController.Instance.CloseWindow();
    }



    // ==========================================
    // 内部助手函数
    // ==========================================

    // 成功放置到目标格子
    public void PlaceSuccessfully(Vector2Int index, bool isRotated)
    {
        // 写入底层二维数组
        CurrentGrid.GetGridController().PlaceItem(this, index.x, index.y, isRotated);
        _originalGridIndex = index;
        _originalIsRotated = isRotated;

        // 根据当前的背包(CurrentGrid)获取精准的UI局部坐标，强行覆盖给物品！
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(index.x, index.y);

        _currentPreviewIsRotated = isRotated;
        UpdateVisualSize(isRotated);
    }

    // 放置失败，弹回老家
    public void BounceBack()
    {
        // 认回老父亲，防止流落在全局拖拽层
        transform.SetParent(CurrentGrid.ItemContainer, false);

        CurrentGrid.GetGridController().PlaceItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);

        // 读取原本的格子坐标进行还原
        _rectTransform.anchoredPosition = CurrentGrid.GetLocalPosition(_originalGridIndex.x, _originalGridIndex.y);

        _currentPreviewIsRotated = _originalIsRotated;
        UpdateVisualSize(_originalIsRotated);
    }

    // 更新物品在UI上的宽和高
    private void UpdateVisualSize(bool isRotated)
    {
        int actualWidth = isRotated ? ItemData.Height : ItemData.Width;
        int actualHeight = isRotated ? ItemData.Width : ItemData.Height;

        if (CurrentGrid != null)
        {
            // 通过所属的 UI Controller 获取包含了边距(Spacing)的真实尺寸
            _rectTransform.sizeDelta = CurrentGrid.GetItemActualSize(actualWidth, actualHeight);
        }
        // 彻底禁止Z轴旋转，防止UI错位
        _rectTransform.localEulerAngles = Vector3.zero;
    }

    // 刷新物品右下角的堆叠数量显示
    public void UpdateAmountText()
    {
        if (AmountText == null) return;

        if (ItemData != null && ItemData.IsStackable && CurrentAmount > 0)
        {
            AmountText.text = CurrentAmount.ToString();
            AmountText.gameObject.SetActive(true);
        }
        else
        {
            AmountText.gameObject.SetActive(false);
        }
    }


    // ==========================================
    // 拆分与交互功能
    // ==========================================

    // 接口实现：监听鼠标单击
    // 接口实现：监听鼠标单击
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
        {
            // =========================================================
            // 【PRD 核心：快捷转移 (Quick Transfer)】
            // 判定：按住了左侧或右侧的 Ctrl 键 + 鼠标左键
            // =========================================================
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                ExecuteQuickTransfer();
                return; // 执行完转移后直接返回，防止弹出拆分窗口
            }

            // 原来的拆分判定
            if (ItemData != null && ItemData.IsStackable && CurrentAmount > 1)
            {
                SplitUIController.Instance.OpenSplitWindow(this);
            }
        }
    }

    // 执行跨容器快捷转移
    // 执行跨容器快捷转移
    private void ExecuteQuickTransfer()
    {
        // 1. 问交警：我要去哪里？
        InventoryUIController targetGrid = GameUIController.Instance.GetQuickTransferTarget(CurrentGrid);

        // =========================================================
        // 【防呆提示】：如果没反应，立刻在控制台爆红警告你！
        // =========================================================
        if (targetGrid == null)
        {
            Debug.LogError($"❌ 快捷转移失败：找不到目标容器！请检查 GameManager 上的 GameUIController 脚本，【Backpack Grid】和【Loot Chest Grid】是否已经拖拽赋值！当前物品所在容器是：{CurrentGrid.name}");
            return;
        }

        InventoryGridController targetGridController = targetGrid.GetGridController();

        // 2. 问目标大脑：你有空位吗？给我找一个最近的！
        if (targetGridController.FindFirstAvailableSpace(ItemData.Width, ItemData.Height, out Vector2Int newPos, out bool needsRot))
        {
            // 3. 拔出老家
            CurrentGrid.GetGridController().RemoveItem(this, _originalGridIndex.x, _originalGridIndex.y, _originalIsRotated);

            // 4. 认新主人，落户新家
            transform.SetParent(targetGrid.ItemContainer, false);
            CurrentGrid = targetGrid;

            PlaceSuccessfully(newPos, needsRot);

            Debug.Log($"✅ 快捷转移成功！{ItemData.ItemName} 瞬间飞入了 {targetGrid.name}");
        }
        else
        {
            // 没空位就飘黄字警告
            Debug.LogWarning($"⚠️ 快捷转移失败：目标容器 ({targetGrid.name}) 空间已满！");
        }
    }

    // 被拆分窗口点击确定时调用
    public void ExecuteSplit(int splitAmount)
    {
        InventoryGridController gridController = CurrentGrid.GetGridController();

        // 以原物品为中心向四周扫描空位
        if (gridController.FindSpaceAround(_originalGridIndex.x, _originalGridIndex.y, ItemData.Width, ItemData.Height, out Vector2Int newPos, out bool needsRot))
        {
            // 扣除本体数量
            this.CurrentAmount -= splitAmount;
            this.UpdateAmountText();

            // 生成克隆体（false 参数保证使用局部的纯净坐标，防止克隆体漂移）
            GameObject cloneObj = Instantiate(this.gameObject, CurrentGrid.ItemContainer, false);

            DraggableItemUI cloneUI = cloneObj.GetComponent<DraggableItemUI>();
            cloneUI.IsDebugItem = false;
            cloneUI.name = this.gameObject.name + "_SplitClone";
            // 【极其重要】：赋予克隆体原本的主人，防止它是孤儿导致拖拽报空指针
            cloneUI.CurrentGrid = this.CurrentGrid;

            // 恢复克隆体的透明度和射线接收
            CanvasGroup cloneCanvasGroup = cloneUI.GetComponent<CanvasGroup>();
            if (cloneCanvasGroup != null)
            {
                cloneCanvasGroup.alpha = 1f;
                cloneCanvasGroup.blocksRaycasts = true;
            }

            cloneUI.CurrentAmount = splitAmount;

            // 初始化克隆体并在新位置写入数据
            cloneUI.InitializeItem(ItemData, newPos, needsRot);
            gridController.PlaceItem(cloneUI, newPos.x, newPos.y, needsRot);
            cloneUI.UpdateAmountText();
        }
        else
        {
            Debug.LogWarning("背包空间不足，无法进行拆分！");
        }
    }

    // ==========================================
    // 雷达探测
    // ==========================================

    // 发射射线探测鼠标此时此刻停留在哪个背包的 UI 上
    private InventoryUIController GetHoveredGrid(PointerEventData eventData)
    {
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            // 向上遍历父节点，只要找到了挂载着 InventoryUIController 的面板就返回它
            InventoryUIController grid = result.gameObject.GetComponentInParent<InventoryUIController>();
            if (grid != null) return grid;
        }
        return null; // 鼠标指在了空气中
    }


}
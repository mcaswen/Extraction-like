using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家交互扫描器
/// 负责寻找最近可交互对象，并驱动场景中的悬浮提示 UI
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    public float InteractionRadius = 5f;
    public LayerMask InteractableLayer;

    [Header("Prompt UI")]
    public RectTransform FloatingPromptUI;
    public Text PromptText;
    public float HeightOffset = 1.5f;

    [Header("Debug")]
    public bool LogInteractionDebug = true;

    private IInteractable _closestInteractable;
    private ISecondaryInteractable _closestSecondaryInteractable;
    private Transform _closestTransform;
    private Camera _mainCamera;
    private static int _lastPrimaryInputHandledFrame = -1;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (FloatingPromptUI != null)
        {
            FloatingPromptUI.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        ScanForInteractables();
        UpdateFloatingUI();
        HandleInteractionInput();
    }

    // 扫描交互半径内最近的可交互对象，供提示和按键入口复用
    private void ScanForInteractables()
    {
        Collider[] hits = InteractableLayer.value == 0
            ? Physics.OverlapSphere(transform.position, InteractionRadius)
            : Physics.OverlapSphere(transform.position, InteractionRadius, InteractableLayer);

        float minDistance = float.MaxValue;
        IInteractable nearestInteractable = null;
        ISecondaryInteractable nearestSecondaryInteractable = null;
        Transform nearestTransform = null;
        int bestPriority = int.MaxValue;

        foreach (Collider hit in hits)
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                continue;
            }

            int priority = GetInteractablePriority(hit);
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (priority > bestPriority || (priority == bestPriority && distance >= minDistance))
            {
                continue;
            }

            bestPriority = priority;
            minDistance = distance;
            nearestInteractable = interactable;
            nearestSecondaryInteractable = hit.GetComponentInParent<ISecondaryInteractable>();
            nearestTransform = hit.transform;
        }

        _closestInteractable = nearestInteractable;
        _closestSecondaryInteractable = nearestSecondaryInteractable;
        _closestTransform = nearestTransform;
    }

    private static int GetInteractablePriority(Collider hit)
    {
        LootBoxEntity lootBox = hit != null ? hit.GetComponentInParent<LootBoxEntity>() : null;
        if (lootBox != null && !lootBox.IsResourcePointLooted)
        {
            return 0;
        }

        return 1;
    }

    // 刷新世界空间悬浮提示，并在背包打开时隐藏提示避免 UI 干扰
    private void UpdateFloatingUI()
    {
        if (FloatingPromptUI == null || PromptText == null || _mainCamera == null)
        {
            return;
        }

        bool inventoryOpen = InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen;
        if (_closestInteractable == null || _closestTransform == null || inventoryOpen)
        {
            FloatingPromptUI.gameObject.SetActive(false);
            return;
        }

        FloatingPromptUI.gameObject.SetActive(true);
        string prompt = _closestInteractable.GetPromptText();
        if (_closestSecondaryInteractable != null)
        {
            string secondaryPrompt = _closestSecondaryInteractable.GetSecondaryPromptText();
            if (!string.IsNullOrEmpty(secondaryPrompt))
            {
                prompt = $"{prompt}\n{secondaryPrompt}";
            }
        }

        string equipmentGuidePrompt = GetEquipmentGuidePrompt();
        if (!string.IsNullOrEmpty(equipmentGuidePrompt))
        {
            prompt = $"{prompt}\n{equipmentGuidePrompt}";
        }

        PromptText.text = prompt;

        Vector3 worldPosition = _closestTransform.position + Vector3.up * HeightOffset;
        Vector3 screenPosition = _mainCamera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f)
        {
            FloatingPromptUI.gameObject.SetActive(false);
            return;
        }

        FloatingPromptUI.position = screenPosition;
    }

    // 统一处理主交互键和副交互键输入
    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (!CanHandlePrimaryInput())
            {
                if (LogInteractionDebug)
                {
                    Debug.Log($"[PlayerInteraction] F ignored by non-focused interaction owner. player={name}.", this);
                }

                return;
            }

            if (_lastPrimaryInputHandledFrame == Time.frameCount)
            {
                if (LogInteractionDebug)
                {
                    Debug.Log($"[PlayerInteraction] F ignored: already handled this frame. player={name}.", this);
                }

                return;
            }

            _lastPrimaryInputHandledFrame = Time.frameCount;

            if (LogInteractionDebug)
            {
                Debug.Log(
                    $"[PlayerInteraction] F pressed. player={name}, " +
                    $"inventoryInstance={(InventoryScreenController.Instance != null ? "yes" : "no")}, " +
                    $"inventoryOpen={(InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen)}, " +
                    $"closest={DescribeClosestInteractable()}",
                    this);
            }

            HandlePrimaryInteractOrInventoryToggle();
        }

        if (Input.GetKeyDown(KeyCode.E) && _closestSecondaryInteractable != null)
        {
            _closestSecondaryInteractable.SecondaryInteract();
        }
    }

    private bool CanHandlePrimaryInput()
    {
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null ||
            !registry.TryGetFocusedHandle(out AgentRuntimeHandle focusedHandle) ||
            focusedHandle.CachedTransform == null)
        {
            return true;
        }

        Transform focusedTransform = focusedHandle.CachedTransform;
        return transform == focusedTransform ||
               transform.IsChildOf(focusedTransform) ||
               focusedTransform.IsChildOf(transform);
    }

    private void HandlePrimaryInteractOrInventoryToggle()
    {
        InventoryScreenController inventoryController = InventoryScreenController.Instance;
        if (inventoryController != null && inventoryController.IsInventoryOpen)
        {
            if (LogInteractionDebug)
            {
                Debug.Log("[PlayerInteraction] F route -> close inventory.", this);
            }

            inventoryController.CloseInventory();
            return;
        }

        if (_closestInteractable != null)
        {
            if (LogInteractionDebug)
            {
                Debug.Log($"[PlayerInteraction] F route -> interact with {DescribeClosestInteractable()}.", this);
            }

            _closestInteractable.Interact();
            return;
        }

        if (inventoryController == null)
        {
            Debug.LogWarning("[PlayerInteraction] F route -> open inventory failed: InventoryScreenController.Instance is null.", this);
            return;
        }

        if (LogInteractionDebug)
        {
            Debug.Log("[PlayerInteraction] F route -> open inventory.", this);
        }

        inventoryController.OpenInventory();
    }

    private string DescribeClosestInteractable()
    {
        if (_closestInteractable == null)
        {
            return "none";
        }

        string transformName = _closestTransform != null ? _closestTransform.name : "no-transform";
        string interactableType = _closestInteractable.GetType().Name;
        return $"{interactableType} on {transformName}";
    }

    // 当目标是背包或胸挂时，额外补一行装备指引，帮助玩家理解 F/E 的区别
    private string GetEquipmentGuidePrompt()
    {
        WorldLootItem worldLootItem = _closestTransform != null
            ? _closestTransform.GetComponentInParent<WorldLootItem>()
            : null;
        if (worldLootItem == null || worldLootItem.ItemData == null)
        {
            return string.Empty;
        }

        InventoryScreenController gameUiController = InventoryScreenController.Instance;
        if (gameUiController == null)
        {
            return string.Empty;
        }

        if (worldLootItem.ItemData.Type == ItemType.Bag)
        {
            bool hasBagEquipped = gameUiController.BackpackSlot != null && gameUiController.BackpackSlot.HasEquippedItem;
            return hasBagEquipped
                ? "提示：按 E 替换当前背包"
                : "提示：按 E 装备背包";
        }

        if (worldLootItem.ItemData.Type == ItemType.Rig)
        {
            bool hasRigEquipped = gameUiController.RigSlot != null && gameUiController.RigSlot.HasEquippedItem;
            return hasRigEquipped
                ? "提示：按 E 替换当前胸挂"
                : "提示：按 E 装备胸挂";
        }

        return string.Empty;
    }

    // 在 Scene 视图里绘制交互半径，方便调试交互范围
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractionRadius);
    }
}

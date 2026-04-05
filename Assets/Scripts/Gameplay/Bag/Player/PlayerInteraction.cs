using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家交互扫描器。
/// 负责寻找最近可交互对象，并驱动场景中的悬浮提示 UI。
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    public float InteractionRadius = 3f;
    public LayerMask InteractableLayer;

    [Header("Prompt UI")]
    public RectTransform FloatingPromptUI;
    public Text PromptText;
    public float HeightOffset = 1.5f;

    private IInteractable _closestInteractable;
    private ISecondaryInteractable _closestSecondaryInteractable;
    private Transform _closestTransform;
    private Camera _mainCamera;

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
        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            if (FloatingPromptUI != null)
            {
                FloatingPromptUI.gameObject.SetActive(false);
            }
            return;
        }

        ScanForInteractables();
        UpdateFloatingUI();
        HandleInteractionInput();
    }

    private void ScanForInteractables()
    {
        Collider[] hits = InteractableLayer.value == 0
            ? Physics.OverlapSphere(transform.position, InteractionRadius)
            : Physics.OverlapSphere(transform.position, InteractionRadius, InteractableLayer);

        float minDistance = float.MaxValue;
        IInteractable nearestInteractable = null;
        ISecondaryInteractable nearestSecondaryInteractable = null;
        Transform nearestTransform = null;

        foreach (Collider hit in hits)
        {
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance >= minDistance)
            {
                continue;
            }

            minDistance = distance;
            nearestInteractable = interactable;
            nearestSecondaryInteractable = hit.GetComponentInParent<ISecondaryInteractable>();
            nearestTransform = hit.transform;
        }

        _closestInteractable = nearestInteractable;
        _closestSecondaryInteractable = nearestSecondaryInteractable;
        _closestTransform = nearestTransform;
    }

    private void UpdateFloatingUI()
    {
        if (FloatingPromptUI == null || PromptText == null || _mainCamera == null)
        {
            return;
        }

        bool inventoryOpen = GameUIController.Instance != null && GameUIController.Instance.IsInventoryOpen;
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

    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(KeyCode.F) && _closestInteractable != null)
        {
            _closestInteractable.Interact();
        }

        if (Input.GetKeyDown(KeyCode.E) && _closestSecondaryInteractable != null)
        {
            _closestSecondaryInteractable.SecondaryInteract();
        }
    }

    private string GetEquipmentGuidePrompt()
    {
        WorldLootItem worldLootItem = _closestTransform != null
            ? _closestTransform.GetComponentInParent<WorldLootItem>()
            : null;
        if (worldLootItem == null || worldLootItem.ItemData == null)
        {
            return string.Empty;
        }

        GameUIController gameUiController = GameUIController.Instance;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, InteractionRadius);
    }
}

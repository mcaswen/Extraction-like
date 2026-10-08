using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    [RequireComponent(typeof(Button))]
    public sealed class AerospacePartsGrantButton : MonoBehaviour
    {
        [SerializeField] private Text label;
        private Button button;
        private string activeAgentId;

        private void Awake() => button = GetComponent<Button>();

        public void Initialize(Text buttonLabel) => label = buttonLabel;

        private void Update()
        {
            var inventory = InventoryScreenController.Instance;
            string agentId = inventory != null ? inventory.ActiveInventoryAgentId : null;
            if (activeAgentId != agentId)
            {
                activeAgentId = agentId;
                if (label != null) label.text = "领取五个飞船零件";
            }
            button.interactable = AerospaceCollectionRuntime.Instance != null && inventory != null &&
                inventory.BackpackGrid != null && InventoryItemFactory.Instance != null &&
                !string.IsNullOrEmpty(agentId) && !inventory.UsesCustomPlayerInventory &&
                !AerospaceCollectionRuntime.RaidLocked && !AerospaceUiInputGate.BlocksGameplayInput &&
                DraggableItemUI.CurrentlyDraggedItem == null;
        }

        public void GrantParts()
        {
            var runtime = AerospaceCollectionRuntime.Instance;
            if (runtime == null || !button.interactable) return;
            bool complete = runtime.TryGrantPartsToCurrentBackpack();
            if (label != null) label.text = complete ? "五个零件已领取" : "空间不足，清理后再点";
        }
    }
}

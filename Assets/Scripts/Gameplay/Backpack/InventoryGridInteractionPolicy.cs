using UnityEngine;

/// <summary>
/// Optional per-grid interaction policy. Grids without this component keep the default inventory behavior.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryGridInteractionPolicy : MonoBehaviour
{
    [Header("Drag")]
    public bool AllowItemDragStart = true;
    public bool AllowItemDrops = true;

    public static bool CanBeginDragFrom(InventoryUIController grid)
    {
        if (grid == null)
        {
            return true;
        }

        InventoryGridInteractionPolicy policy = grid.GetComponent<InventoryGridInteractionPolicy>();
        return policy == null || policy.AllowItemDragStart;
    }

    public static bool CanDropInto(InventoryUIController grid)
    {
        if (grid == null)
        {
            return false;
        }

        InventoryGridInteractionPolicy policy = grid.GetComponent<InventoryGridInteractionPolicy>();
        return policy == null || policy.AllowItemDrops;
    }
}

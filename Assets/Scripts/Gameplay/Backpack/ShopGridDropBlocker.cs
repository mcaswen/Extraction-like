using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShopGridDropBlocker : MonoBehaviour, IInventoryGridPlacementPolicy
{
    public bool CanAcceptItem(DraggableItemUI itemView)
    {
        return false;
    }
}


using UnityEngine;
using UnityEngine.EventSystems;

public enum ShopItemClickMode
{
    SellFromStorage,
    BuyFromShop
}

[DisallowMultipleComponent]
public sealed class ShopItemClickTarget : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ShopScreenController _controller;
    private DraggableItemUI _itemView;
    private string _stockItemId;
    private ShopItemClickMode _mode;

    public void Configure(
        ShopScreenController controller,
        DraggableItemUI itemView,
        ShopItemClickMode mode,
        string stockItemId = null)
    {
        _controller = controller;
        _itemView = itemView;
        _mode = mode;
        _stockItemId = stockItemId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left ||
            eventData.dragging ||
            _controller == null)
        {
            return;
        }

        if (_mode == ShopItemClickMode.SellFromStorage)
        {
            _controller.HandleStorageItemClicked(_itemView, eventData);
            return;
        }

        _controller.HandleShopItemClicked(_stockItemId, _itemView, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }
}

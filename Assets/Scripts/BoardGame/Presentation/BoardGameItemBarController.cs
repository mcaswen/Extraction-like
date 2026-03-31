using System.Collections.Generic;
using System.Linq;
using BoardGame.Runtime.Controllers;
using BoardGame.Runtime.State;
using BoardGame.Views;
using TMPro;
using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 道具栏控制器
    /// </summary>
    public sealed class BoardGameItemBarController : MonoBehaviour
    {
        // 预先摆好的道具槽位列表，不在运行时动态创建 UI
        [SerializeField] private List<BoardGameItemSlotView> _itemSlots = new List<BoardGameItemSlotView>();

        private BoardGamePrototypeController _prototypeController;

        /// <summary>
        /// 绑定运行时总控
        /// </summary>
        public void Bind(BoardGamePrototypeController prototypeController)
        {
            _prototypeController = prototypeController;
            _prototypeController.SessionChanged += Refresh;
            Refresh();
        }

        /// <summary>
        /// 按当前背包中的消耗品刷新道具栏
        /// </summary>
        private void Refresh()
        {
            if (_prototypeController == null)
            {
                return;
            }

            IEnumerable<IGrouping<string, BoardItemInstance>> groupedItems = _prototypeController.SessionState.AgentState.InventoryState
                .GetConsumables()
                .GroupBy(item => item.ItemId);

            int slotIndex = 0;

            foreach (IGrouping<string, BoardItemInstance> itemGroup in groupedItems)
            {
                if (slotIndex >= _itemSlots.Count)
                {
                    break;
                }

                BoardItemInstance firstItem = itemGroup.First();
                BoardItemInstance useItem = firstItem;
                _itemSlots[slotIndex].Bind(
                    firstItem.DisplayName,
                    itemGroup.Count(),
                    () => _prototypeController.TryUseItem(useItem.InstanceId),
                    !_prototypeController.IsInteractionLocked);
                slotIndex++;
            }

            for (; slotIndex < _itemSlots.Count; slotIndex++)
            {
                _itemSlots[slotIndex].ClearSlot();
            }
        }
    }
}

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
        [SerializeField] private List<BoardGameItemSlotView> _itemSlots = new List<BoardGameItemSlotView>();

        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameItemUseController _itemUseController;

        /// <summary>
        /// 绑定运行时只读查询和道具使用控制器
        /// </summary>
        public void Bind(
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameItemUseController itemUseController)
        {
            _runtimeQueryController = runtimeQueryController;
            _itemUseController = itemUseController;
            _runtimeQueryController.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// 按当前背包中的消耗品刷新道具栏
        /// </summary>
        private void Refresh()
        {
            if (_runtimeQueryController == null || _itemUseController == null)
            {
                return;
            }

            BoardAgentState agentState = _runtimeQueryController.GetFocusedAgentState();

            if (agentState == null)
            {
                foreach (BoardGameItemSlotView itemSlot in _itemSlots)
                {
                    itemSlot?.ClearSlot();
                }

                return;
            }

            IEnumerable<IGrouping<string, BoardItemInstance>> groupedItems = agentState.InventoryState
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
                    () => _itemUseController.TryUseItem(useItem.InstanceId),
                    !_runtimeQueryController.IsInteractionLocked);
                slotIndex++;
            }

            for (; slotIndex < _itemSlots.Count; slotIndex++)
            {
                _itemSlots[slotIndex].ClearSlot();
            }
        }
    }
}

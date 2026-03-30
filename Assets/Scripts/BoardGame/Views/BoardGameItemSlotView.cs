using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BoardGame.Views
{
    /// <summary>
    /// 道具栏单个槽位视图
    /// </summary>
    public sealed class BoardGameItemSlotView : MonoBehaviour
    {
        // 槽位点击按钮
        [SerializeField] private Button _button;
        // 道具名称文本
        [SerializeField] private TMP_Text _nameText;
        // 道具数量文本
        [SerializeField] private TMP_Text _countText;

        /// <summary>
        /// 绑定槽位显示和点击事件
        /// </summary>
        public void Bind(string itemName, int count, UnityAction onClick)
        {
            gameObject.SetActive(true);

            if (_nameText != null)
            {
                _nameText.text = itemName;
            }

            if (_countText != null)
            {
                _countText.text = $"x{count}";
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(onClick);
                _button.interactable = true;
            }
        }

        /// <summary>
        /// 清空槽位显示
        /// </summary>
        public void ClearSlot()
        {
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
            }

            gameObject.SetActive(false);
        }
    }
}

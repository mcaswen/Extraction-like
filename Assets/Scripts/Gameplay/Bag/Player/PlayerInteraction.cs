using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float InteractionDistance = 3f;

    private GameObject _currentLootBox;

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, InteractionDistance))
        {
            if (hit.collider.CompareTag("LootBox"))
            {
                _currentLootBox = hit.collider.gameObject;
            }
            else
            {
                _currentLootBox = null;
            }
        }
        else
        {
            _currentLootBox = null;
        }

        // 按下 F 键，并且当前有可以交互的 LootBox
        if (Input.GetKeyDown(KeyCode.F) && _currentLootBox != null)
        {
            // 如果背包已经处于打开状态，不允许重复按 F 开箱（防错乱）
            if (GameUIController.Instance.IsInventoryOpen) return;

            LootBoxEntity lootEntity = _currentLootBox.GetComponent<LootBoxEntity>();

            if (lootEntity != null)
            {
                // 【核心呼叫】：直接让 UI 大管家去处理打开宝箱的所有逻辑！
                GameUIController.Instance.OpenLootBox(lootEntity);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * InteractionDistance);
    }
}
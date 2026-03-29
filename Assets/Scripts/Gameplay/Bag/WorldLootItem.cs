using UnityEngine;

public class WorldLootItem : MonoBehaviour
{
    [Header("持久化数据")]
    public InventoryItemData ItemData;
    public int CurrentAmount;

    // 当物品被丢弃到地上时，由背包系统调用来写入数据
    public void InitializeDrop(InventoryItemData data, int amount)
    {
        ItemData = data;
        CurrentAmount = amount;

        Debug.Log($"实体化成功：生成了一个 {data.ItemName}，数量为 {amount}");

        // 如果你们有 3D 物理需求，可以在这里给刚体加上一点随机抛物线推力
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f)).normalized;
            rb.AddForce(randomDirection * 3f, ForceMode.Impulse);
        }
    }

    // 后续可以用射线或者碰撞触发拾取，调用 InventoryItemFactory.Instance 重新塞回背包
}
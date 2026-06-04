using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class RoomZoneManager : MonoBehaviour
{
    public TargetZone linkedZone; // 关联的底层抽象数据

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    // 当AI走入该房间的Collider范围时触发
    private void OnTriggerEnter(Collider other)
    {
        AIIntentController ai = other.GetComponent<AIIntentController>();
        if (ai != null)
        {
            Debug.Log($"【房间系统】AI进入了房间：{gameObject.name}，弹出专属UI面板");

            // 呼叫 UI 管理器显示面板，并传入当前房间的数据
            RoomUIManager.Instance.ShowRoomPanel(linkedZone, ai);
        }
    }

    // AI离开房间时可选择隐藏面板
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<AIIntentController>() != null)
        {
            RoomUIManager.Instance.HideRoomPanel();
        }
    }
}
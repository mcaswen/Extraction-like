using UnityEngine;

/// <summary>
/// 白盒战斗空间使用的双向传送点控制器
/// </summary>
[RequireComponent(typeof(Collider))]
public class TeleportPadController : MonoBehaviour
{
    public string PadName = "传送点";
    public Transform DestinationPoint;
    public float TeleportCooldownSeconds = 0.6f;
    public Vector3 ArrivalOffset = new Vector3(0f, 0.1f, 0f);

    private float _lastTeleportTime = -999f;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        WhiteboxCharacterVisualUtility.ApplySolidColor(gameObject, new Color(0.92f, 0.98f, 1f, 1f));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (DestinationPoint == null || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        if (Time.time - _lastTeleportTime < TeleportCooldownSeconds)
        {
            return;
        }

        // 传送后同步目标传送点冷却，避免玩家在两个点之间来回触发
        Transform target = other.transform;
        Vector3 destinationPosition = DestinationPoint.position + ArrivalOffset;
        target.position = destinationPosition;

        _lastTeleportTime = Time.time;

        TeleportPadController linkedPad = DestinationPoint.GetComponent<TeleportPadController>();
        if (linkedPad != null)
        {
            linkedPad.NotifyRemoteTeleport(Time.time);
        }
    }

    /// <summary>
    /// 接收另一端传送点同步过来的冷却时间
    /// </summary>
    /// <param name="teleportTime">本次传送发生的游戏时间</param>
    public void NotifyRemoteTeleport(float teleportTime)
    {
        _lastTeleportTime = teleportTime;
    }
}

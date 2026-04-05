using UnityEngine;

/// <summary>
/// 最小撤离点控制器。
/// 当玩家进入触发区后开始撤离倒计时。
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExtractionPointController : MonoBehaviour
{
    public string ExtractionPointName = "撤离点";
    public float ExtractionDurationSeconds = 3f;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        RaidFlowController.Instance?.SetPlayerInsideExtractionPoint(this, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        RaidFlowController.Instance?.SetPlayerInsideExtractionPoint(this, false);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.25f);
        Collider trigger = GetComponent<Collider>();
        if (trigger is BoxCollider boxCollider)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            Gizmos.color = new Color(0.2f, 1f, 0.8f, 0.85f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            return;
        }

        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}

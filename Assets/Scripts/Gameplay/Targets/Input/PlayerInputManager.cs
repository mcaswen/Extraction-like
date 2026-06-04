using UnityEngine;

public class PlayerInputManager : MonoBehaviour
{
    [Header("设置检测层级")]
    public LayerMask targetLayerMask; // 目标圈的Layer (TargetArea)
    public LayerMask groundLayerMask; // 地面的Layer (Ground)

    [Header("当前操纵的AI")]
    public AIIntentController activeAI;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ProcessPlayerClick();
        }
    }

    private void ProcessPlayerClick()
    {
        if (activeAI == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 优先检测是否点中了目标群 (Cluster) 或 区域 (Zone)
        if (Physics.Raycast(ray, out hit, 100f, targetLayerMask))
        {
            TargetCluster clickedCluster = hit.collider.GetComponent<TargetCluster>();
            if (clickedCluster != null && !clickedCluster.isCompleted)
            {
                activeAI.CommandToCluster(clickedCluster);
                return;
            }
        }

        // 如果没有点中目标，则检测是否点中了普通地面进行 Point-and-Click 寻路
        if (Physics.Raycast(ray, out hit, 100f, groundLayerMask))
        {
            activeAI.CommandToMovePoint(hit.point);
        }
    }
}
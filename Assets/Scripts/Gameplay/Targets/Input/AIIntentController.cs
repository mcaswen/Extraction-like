using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIIntentController : MonoBehaviour
{
    public TargetCluster targetedCluster;
    private NavMeshAgent navAgent;

    void Start()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    // 处理功能区域选择 (点击群目标)
    public void CommandToCluster(TargetCluster cluster)
    {
        targetedCluster = cluster;
        Debug.Log($"【AI】前往群区域：{cluster.gameObject.name}");

        // 简单实现：直接让AI走到该群目标的中心点
        navAgent.SetDestination(cluster.transform.position);
    }

    // 处理目的地指点 (Point-and-Click)
    public void CommandToMovePoint(Vector3 targetPoint)
    {
        targetedCluster = null; // 清除之前的抽象群目标
        Debug.Log($"【AI】前往具体坐标：{targetPoint}");

        navAgent.SetDestination(targetPoint);
    }
}
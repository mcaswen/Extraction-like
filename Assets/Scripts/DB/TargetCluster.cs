using UnityEngine;
using System.Collections.Generic;

public enum ClusterType { Enemy, Resource, Evac }

[RequireComponent(typeof(Collider))] // 确保有碰撞体用于玩家射线点击选择
public class TargetCluster : MonoBehaviour
{
    public ClusterType clusterType;
    public List<TargetEntity> entities = new List<TargetEntity>();
    public float rangeRadius = 5f; // 编辑器定义的平滑圈半径

    public bool isContacted { get; private set; } = false;
    public bool isCompleted { get; private set; } = false;

    private TargetZone parentZone;

    public void Init(TargetZone zone)
    {
        parentZone = zone;
        foreach (var entity in entities)
        {
            if (entity != null) entity.Init(this);
        }

        // 自动同步碰撞体半径（如果是球体碰撞体）
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = rangeRadius;
        }
    }

    // 条件推导：至少存在一个具体目标已接触
    public void EvaluateContactStatus()
    {
        if (isContacted) return;

        foreach (var entity in entities)
        {
            if (entity != null && entity.isContacted)
            {
                isContacted = true;
                Debug.Log($"群目标 {gameObject.name} 状态更新为：已接触");
                if (parentZone != null) parentZone.EvaluateContactStatus();
                break;
            }
        }
    }

    // 条件推导：所有具体目标全部已完成
    public void EvaluateCompletionStatus()
    {
        if (entities.Count == 0) return;

        foreach (var entity in entities)
        {
            if (entity != null && !entity.isCompleted)
            {
                return; // 只要有一个没完成，群目标就未完成
            }
        }

        isCompleted = true;
        Debug.Log($"群目标 {gameObject.name} 状态更新为：已完成");

        // 隐藏范围圈或关闭碰撞，防止玩家重复点击
        GetComponent<Collider>().enabled = false;

        if (parentZone != null) parentZone.EvaluateCompletionStatus();
    }

    // 在场景编辑器中程序化绘制丝滑的群范围圈
    private void OnDrawGizmos()
    {
        if (isCompleted) return;

        Gizmos.color = clusterType == ClusterType.Enemy ? new Color(1f, 0f, 0f, 0.4f) : new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, rangeRadius);
    }
}
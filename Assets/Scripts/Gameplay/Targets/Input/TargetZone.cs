using UnityEngine;
using System.Collections.Generic;

public class TargetZone : MonoBehaviour
{
    public List<TargetCluster> clusters = new List<TargetCluster>();

    public bool isContacted { get; private set; } = false;
    public bool isCompleted { get; private set; } = false;

    private void Start()
    {
        // 初始化管辖的所有 Cluster
        foreach (var cluster in clusters)
        {
            if (cluster != null) cluster.Init(this);
        }
    }

    // 条件推导：至少存在一个群目标已接触
    public void EvaluateContactStatus()
    {
        if (isContacted) return;

        foreach (var cluster in clusters)
        {
            if (cluster != null && cluster.isContacted)
            {
                isContacted = true;
                Debug.Log($"区域目标 {gameObject.name} 状态更新为：已接触");
                break;
            }
        }
    }

    // 条件推导：所有群目标全部已完成
    public void EvaluateCompletionStatus()
    {
        if (clusters.Count == 0) return;

        foreach (var cluster in clusters)
        {
            if (cluster != null && !cluster.isCompleted)
            {
                return;
            }
        }

        isCompleted = true;
        Debug.Log($"区域目标 {gameObject.name} 状态更新为：全部完成！");
    }
}
using UnityEngine;

public class TargetEntity : MonoBehaviour
{
    public bool isContacted { get; private set; } = false;
    public bool isCompleted { get; private set; } = false;

    private TargetCluster parentCluster;

    public void Init(TargetCluster cluster)
    {
        parentCluster = cluster;
    }

    // 当 Agent 开始与该实体交互（如接战、开始搜刮）时调用
    public void OnContact()
    {
        if (isContacted) return;
        isContacted = true;
        Debug.Log($"{gameObject.name} 已被接触");

        // 向上层传递：具体目标已接触 -> 检查群目标
        if (parentCluster != null) parentCluster.EvaluateContactStatus();
    }

    // 当该实体互动结束（如敌人死亡、箱子被打开）时调用
    public void OnComplete()
    {
        if (isCompleted) return;
        isCompleted = true;
        Debug.Log($"{gameObject.name} 已完成");

        // 向上层传递：具体目标已完成 -> 检查群目标是否全部完成
        if (parentCluster != null) parentCluster.EvaluateCompletionStatus();
    }
}
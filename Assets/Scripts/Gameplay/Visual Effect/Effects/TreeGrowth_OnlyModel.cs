using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家靠近后按阶段切换树木模型的生长演示组件
/// </summary>
public class TreeGrowth_OnlyModel : MonoBehaviour
{
    [Header("核心配置")]
    public Transform player;
    public float triggerDistance = 50f;
    public float totalGrowDuration = 6f;

    [Header("生长阶段模型")]
    public GameObject seedling;   // 小树苗
    public GameObject smallTree; // 中树
    public GameObject bigTree;   // 大树

    private bool hasStartedGrow = false;
    private bool isFullyGrown = false;

    void Start()
    {
        InitializeTreeModels();
    }

    void Update()
    {
        if (isFullyGrown) return;
        CheckPlayerDistanceAndGrow();
    }

    // 关键：编辑模式下自动重置模型状态
    void OnEnable()
    {
        if (!Application.isPlaying) InitializeTreeModels();
    }
    void OnDisable()
    {
        if (!Application.isPlaying) InitializeTreeModels();
    }

    private void InitializeTreeModels()
    {
        if (seedling != null) seedling.SetActive(true);
        if (smallTree != null) smallTree.SetActive(false);
        if (bigTree != null) bigTree.SetActive(false);
    }

    private void CheckPlayerDistanceAndGrow()
    {
        if (player == null) return;
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= triggerDistance && !hasStartedGrow)
        {
            hasStartedGrow = true;
            StartCoroutine(GrowTreeStepByStep());
        }
    }

    private IEnumerator GrowTreeStepByStep()
    {
        // 用三个阶段切换模型，避免同时缩放多套树模型造成穿插
        yield return new WaitForSeconds(totalGrowDuration / 3);
        if (seedling != null) seedling.SetActive(false);
        if (smallTree != null) smallTree.SetActive(true);

        yield return new WaitForSeconds(totalGrowDuration * 2 / 3);
        if (smallTree != null) smallTree.SetActive(false);
        if (bigTree != null) bigTree.SetActive(true);

        isFullyGrown = true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);
    }
}

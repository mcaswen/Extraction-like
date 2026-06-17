using UnityEngine;

/// <summary>
/// 检测玩家靠近后让物体漂浮到目标高度的视觉演示组件
/// </summary>
public class FloatOnDetectPlayer : MonoBehaviour
{
    [Header("检测设置")]
    [Tooltip("检测Player的范围（米）")]
    public float detectRange = 20f;         
    [Tooltip("是否正在漂浮")]
    private bool isFloating = false;         
    [Tooltip("是否检测到Player在范围内")]
    private bool isPlayerInRange = false;   

    [Header("漂浮设置")]
    [Tooltip("漂浮速度（米/秒），值越小越慢")]
    public float floatSpeed = 0.5f;          
    [Tooltip("最终漂浮高度（相对于初始位置）")]
    public float targetFloatHeight = 25f;    
    private Vector3 startPosition;           
    private Vector3 targetPosition;          

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            Debug.LogWarning("物体没有Rigidbody组件，已自动添加！");
        }
        rb.useGravity = true;

  
        startPosition = transform.position;

        targetPosition = startPosition + Vector3.up * targetFloatHeight;
    }

    void Update()
    {
       
        CheckPlayerRangeStatus();

        // 玩家进入范围时关闭重力并上浮，离开后恢复刚体状态
        if (isPlayerInRange)
        {
            StartFloatLogic();
        }
        else
        {
            ResetToNormalState();
        }
    }


    private void CheckPlayerRangeStatus()
    {
        bool playerDetected = false;

        // 使用球形范围作为演示触发器，不依赖额外触发器配置
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectRange);

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                playerDetected = true;
                break;
            }
        }
        isPlayerInRange = playerDetected;
    }

  
    private void StartFloatLogic()
    {
        if (!isFloating)
        {
            Debug.Log("检测到Player（20米范围内），开始漂浮！");
           
            rb.useGravity = false;
        
            rb.freezeRotation = true;
      
            isFloating = true;
        }

        
        if (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                floatSpeed * Time.deltaTime
            );
        }
        else if (isFloating)
        {
            Debug.Log("已漂浮到25米高度，停止移动！");
          
        }
    }

    
    private void ResetToNormalState()
    {
        if (isFloating)
        {
            Debug.Log("Player离开20米范围，恢复重力并停止漂浮！");
            rb.useGravity = true;
        
            rb.freezeRotation = false;
           
            isFloating = false;
        }
    }

    
    void OnDrawGizmos()
    {
        
        Gizmos.color = isPlayerInRange ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

     
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(startPosition, 0.5f);

       
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPosition, 0.5f);

      
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, targetPosition);
    }
}

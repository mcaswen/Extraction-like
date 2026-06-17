using UnityEngine;

/// <summary>
/// 玩家靠近后复制自身并向两侧分离的视觉演示组件
/// </summary>
public class SplitAndSeparate : MonoBehaviour
{
    [Header("检测设置")]
    public float detectRange = 5f; 
    private bool hasSplit = false; 

    [Header("移动设置")]
    public float moveSpeed = 2f; 
    private GameObject cloneObject; 
    private Vector3 originalPos; 


    private Vector3 playerTargetPos;
    private Vector3 cloneTargetPos;

    void Update()
    {
       
        if (!hasSplit)
        {
            DetectPlayerAndSplit();
        }

        HandleMovement();
    }


    private void DetectPlayerAndSplit()
    {
    
        // 首次检测到玩家后只分裂一次，克隆体会关闭同脚本避免递归复制
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectRange);

        foreach (Collider col in colliders)
        {
           
            if (col.CompareTag("Player"))
            {
              
                originalPos = transform.position;
                
                CreateClone();
               
                SetMoveTargets();
                
                hasSplit = true;
                break;
            }
        }
    }

    
    private void CreateClone()
    {
        
        cloneObject = Instantiate(gameObject, transform.position, transform.rotation);

     
        cloneObject.name = $"{gameObject.name}_Clone_{Time.time}";

        
        SplitAndSeparate cloneScript = cloneObject.GetComponent<SplitAndSeparate>();
        if (cloneScript != null)
        {
            // 防止克隆体再次触发分裂生成更多克隆
            cloneScript.enabled = false;
        }

        Debug.Log($"分裂完成！克隆体名称：{cloneObject.name}");
    }

  
    private void SetMoveTargets()
    {
       
        playerTargetPos = originalPos + Vector3.left * 15f;
       
        cloneTargetPos = originalPos + Vector3.right * 15f;
    }

    
    private void HandleMovement()
    {
        if (hasSplit && cloneObject != null)
        {
         
            if (Vector3.Distance(transform.position, playerTargetPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    playerTargetPos,
                    moveSpeed * Time.deltaTime
                );
            }

           
            if (Vector3.Distance(cloneObject.transform.position, cloneTargetPos) > 0.01f)
            {
                cloneObject.transform.position = Vector3.MoveTowards(
                    cloneObject.transform.position,
                    cloneTargetPos,
                    moveSpeed * Time.deltaTime
                );
            }
        }
    }

    void OnDrawGizmos()
    {
      
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

       
        if (hasSplit)
        {
           
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(playerTargetPos, 0.2f);

           
            if (cloneObject != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(cloneTargetPos, 0.2f);
            }
        }
    }
}

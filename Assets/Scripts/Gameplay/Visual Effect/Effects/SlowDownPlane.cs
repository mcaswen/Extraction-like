using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家进入触发区域时临时降低移动速度的区域组件
/// </summary>
public class SlowDownPlane : MonoBehaviour
{
    [Header("减速设置")]
    [Tooltip("要降低的速度值（单位：米/秒）")]
    public float speedReduction = 10f;

    [Tooltip("离开Plane后是否恢复原速度")]
    public bool restoreSpeedOnExit = true;


    private Dictionary<GameObject, float> playerOriginalSpeeds = new Dictionary<GameObject, float>();

    private void Start()
    {
        
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning("Plane缺少碰撞体，已自动添加MeshCollider！");
            col = gameObject.AddComponent<MeshCollider>();
        }
        
        col.isTrigger = true;
    }

   
    private void OnTriggerEnter(Collider other)
    {
        // 检测是否是玩家标签的物体
        if (other.CompareTag("Player"))
        {
            // 获取玩家的移动脚本
            PlayerMovement playerMove = other.GetComponent<PlayerMovement>();
            if (playerMove != null)
            {
                // 存储原始速度（避免重复存储）
                if (!playerOriginalSpeeds.ContainsKey(other.gameObject))
                {
                    playerOriginalSpeeds[other.gameObject] = playerMove.moveSpeed;
                }
                // 降低速度并限制到非负值，避免减速区域把移动方向反转
                playerMove.moveSpeed = Mathf.Max(0, playerMove.moveSpeed - speedReduction);
                Debug.Log($"Player进入减速区域，速度从{playerOriginalSpeeds[other.gameObject]}降至{playerMove.moveSpeed}");
            }
            else
            {
                Debug.LogWarning("Player物体上未挂载PlayerMovement脚本！");
            }
        }
    }

    
    private void OnTriggerExit(Collider other)
    {
        if (restoreSpeedOnExit && other.CompareTag("Player"))
        {
            PlayerMovement playerMove = other.GetComponent<PlayerMovement>();
            if (playerMove != null && playerOriginalSpeeds.ContainsKey(other.gameObject))
            {
                // 恢复原始速度
                playerMove.moveSpeed = playerOriginalSpeeds[other.gameObject];
                playerOriginalSpeeds.Remove(other.gameObject);
                Debug.Log($"Player离开减速区域，速度恢复为{playerMove.moveSpeed}");
            }
        }
    }

   
    private void OnDestroy()
    {
        playerOriginalSpeeds.Clear();
    }
}

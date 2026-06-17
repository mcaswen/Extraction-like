using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// 玩家进入触发区域时临时提高移动速度的区域组件
/// </summary>
public class SpeedUpPlane : MonoBehaviour
{
    [Header("加速设置")]
    [Tooltip("要增加的速度值（单位：米/秒）")]
    public float speedIncrease = 10f; 

    [Tooltip("离开Plane后是否恢复原速度")]
    public bool restoreSpeedOnExit = true;

  
    private Dictionary<GameObject, float> playerOriginalSpeeds = new Dictionary<GameObject, float>();

    private void Start()
    {
    
        // 保留网格碰撞作为实体表面，再额外补一个盒形碰撞体作为速度触发器
        MeshCollider physicsCollider = GetComponent<MeshCollider>();
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<MeshCollider>();
        }
        physicsCollider.isTrigger = false; 

        BoxCollider triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }
        triggerCollider.isTrigger = true; 
        triggerCollider.size = new Vector3(10, 0.1f, 10); 
    }

  
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerMovement playerMove = other.GetComponent<PlayerMovement>();
            if (playerMove != null)
            {
                if (!playerOriginalSpeeds.ContainsKey(other.gameObject))
                {
                    playerOriginalSpeeds[other.gameObject] = playerMove.moveSpeed;
                }
               
                // 从记录的原始速度叠加，避免反复进入时速度持续累加
                playerMove.moveSpeed = playerOriginalSpeeds[other.gameObject] + speedIncrease;
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
            }
        }
    }

    private void OnDestroy()
    {
        playerOriginalSpeeds.Clear();
    }
}

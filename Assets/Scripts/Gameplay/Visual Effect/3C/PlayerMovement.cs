using UnityEngine;


public class PlayerMovement : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("移动速度（单位：米/秒）")]
    public float moveSpeed = 5f;

    [Header("视角跟随（可选）")]
    [Tooltip("是否让玩家朝向移动方向")]
    public bool faceMoveDirection = true;

    private void Update()
    {
        HandleMovement();
    }

   
    private void HandleMovement()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal"); // A/D键
        float verticalInput = Input.GetAxisRaw("Vertical");     // W/S键

    
        Vector3 moveDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

       
        if (moveDirection.magnitude > 0.1f)
        {
            transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

            if (faceMoveDirection)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
             
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.1f);
            }
        }
        
    }
}
using UnityEngine;
/// <summary>
/// 2.5D摄像机控制
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    public Transform TargetTransform; // 拖入玩家 Player

    // 摄像机相对于玩家朝向的偏移量（z < 0 表示在目标背后）
    public Vector3 Offset = new Vector3(0, 10f, -10f);//2.5D偏移量

    // 摄像机跟随的平滑度，数字越大跟得越紧
    public float SmoothSpeed = 5f;

    [Header("Rotation")]
    public bool FollowTargetYaw = true;
    public bool LookAtTarget = true;
    public Vector3 LookAtOffset = new Vector3(0f, 1.5f, 0f);
    public float RotationSmoothSpeed = 8f;

    /// <summary>
    /// 摄像机控制专用控制update
    /// </summary>
    void LateUpdate()
    {
        if (TargetTransform != null)//玩家组件
        {
            // 计算摄像机应该在的位置
            Vector3 desiredPosition = TargetTransform.position + ResolveOffset();//人物位置加摄像机偏移量（以人物位置为中心）

            // 用 Lerp 做一个平滑移动的效果，让镜头有顺滑的电影感
            transform.position = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed * Time.deltaTime);

            if (LookAtTarget)
            {
                Vector3 lookTarget = TargetTransform.position + LookAtOffset;
                Vector3 lookDirection = lookTarget - transform.position;
                if (lookDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, RotationSmoothSpeed * Time.deltaTime);
                }
            }
        }
    }

    private Vector3 ResolveOffset()
    {
        if (!FollowTargetYaw)
        {
            return Offset;
        }

        Vector3 forward = TargetTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return right * Offset.x + Vector3.up * Offset.y + forward * Offset.z;
    }
}

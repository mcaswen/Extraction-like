using UnityEngine;
/// <summary>
/// 2.5D摄像机控制
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    public Transform TargetTransform; // 拖入玩家 Player

    // 摄像机相对于玩家的固定偏移量（默认是高 10 米，往后退 10 米）
    public Vector3 Offset = new Vector3(0, 10f, -10f);//2.5D偏移量

    // 摄像机跟随的平滑度，数字越大跟得越紧
    public float SmoothSpeed = 5f;
    /// <summary>
    /// 摄像机控制专用控制update
    /// </summary>
    void LateUpdate()
    {
        if (TargetTransform != null)//玩家组件
        {
            // 计算摄像机应该在的位置
            Vector3 desiredPosition = TargetTransform.position + Offset;//人物位置加摄像机偏移量（以人物位置为中心）

            // 用 Lerp 做一个平滑移动的效果，让镜头有顺滑的电影感
            transform.position = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed * Time.deltaTime);
        }
    }
}
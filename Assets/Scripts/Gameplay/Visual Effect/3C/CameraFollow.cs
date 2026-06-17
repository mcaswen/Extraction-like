using UnityEngine;

/// <summary>
/// 简易相机跟随控制器，支持目标自动查找、边界限制和视角翻转
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标设置")]
    [Tooltip("要跟随的目标（自动查找Tag为Player的物体）")]
    public Transform target;

    [Header("相机偏移设置")]
    [Tooltip("相机相对于玩家的位置偏移（x:左右，y:高低，z:前后）")]
    public Vector3 offset = new Vector3(0f, 5f, -10f);

    [Header("跟随平滑设置")]
    [Tooltip("跟随平滑系数（值越大跟随越灵敏，建议0.1-1）")]
    public float smoothSpeed = 0.125f;

    [Header("边界限制（可选）")]
    [Tooltip("是否限制相机移动范围")]
    public bool limitCameraBounds = false;
    [Tooltip("相机X轴最小移动范围")]
    public float minX = -20f;
    [Tooltip("相机X轴最大移动范围")]
    public float maxX = 20f;
    [Tooltip("相机Z轴最小移动范围")]
    public float minZ = -20f;
    [Tooltip("相机Z轴最大移动范围")]
    public float maxZ = 20f;

    [Header("Z轴旋转设置")]
    [Tooltip("Z轴旋转角度（默认180度）")]
    public float zRotateAngle = 180f;
    [Tooltip("旋转平滑系数（值越大旋转越快，建议0.05-0.2）")]
    public float rotateSmoothSpeed = 0.1f;

    // 记录当前视角是否处于翻转状态
    private bool isRotated = false;
    // 记录初始旋转
    private Quaternion originalRotation;
    // 记录旋转180度后的目标旋转
    private Quaternion rotatedTargetRotation;

   
    private void Start()
    {
        if (target == null)
        {
            // 作为演示脚本时允许不手动拖引用，启动时按玩家标签兜底寻找目标
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                Debug.Log("成功找到Player物体，相机将跟随它移动");
            }
            else
            {
                Debug.LogError("未找到Tag为Player的物体！请检查物体的Tag设置");
            }
        }

        originalRotation = transform.rotation;
        rotatedTargetRotation = originalRotation * Quaternion.Euler(0f, 0f, zRotateAngle);
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isRotated = !isRotated;
            Debug.Log(isRotated ? "相机已沿Z轴旋转180度" : "相机已恢复初始旋转");
        }


        if (target == null) return;


        Vector3 desiredPosition = target.position + offset;


        if (limitCameraBounds)
        {
            // 只限制水平面位置，高度仍由偏移值控制
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
        }

       
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        
        Quaternion targetRot = isRotated ? rotatedTargetRotation : originalRotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotateSmoothSpeed);

        
    }
}

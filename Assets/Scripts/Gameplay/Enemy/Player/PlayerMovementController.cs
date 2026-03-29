using UnityEngine;

// 强制要求玩家身上必须有 Rigidbody（刚体）组件，防止遗漏
[RequireComponent(typeof(Rigidbody))]
//角色移动脚本
public class PlayerMovementController : MonoBehaviour
{
    public float MoveSpeed = 6f;//移动速度

    private Rigidbody _playerRigidbody;//私有变量  角色rigidbody
    private Camera _mainCamera;//摄像机
    /// <summary>
    /// 脚本开始
    /// </summary>
    void Start()
    {
        _playerRigidbody = GetComponent<Rigidbody>();//实例rigidbody
        _mainCamera = Camera.main;//输入主函数
    }

    // 涉及到物理移动，必须放在 FixedUpdate 中执行
    void FixedUpdate()
    {
        Move();//移动函数
        Aim();//目标函数
    }
    /// <summary>
    /// 移动函数
    /// </summary>
    private void Move()
    {
        // 获取 WASD 输入 (-1 到 1 之间的值)
        float horizontal = Input.GetAxisRaw("Horizontal"); // A 和 D
        float vertical = Input.GetAxisRaw("Vertical");     // W 和 S

        // 将输入转化为三维方向（Y轴为0，因为我们只在地面上平移）
        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;//移动速度和向量归一化

        // 使用刚体移动玩家，不卡墙、不穿模
        _playerRigidbody.MovePosition(_playerRigidbody.position + movement * MoveSpeed * Time.fixedDeltaTime);//角色移动
        //Time.fixedDeltaTime 就是设置的一帧的时间
    }
    /// <summary>
    /// 目标函数  看向函数
    /// </summary>
    private void Aim()
    {
        // 从主摄像机，朝着鼠标在屏幕上的位置，发射一条虚拟射线
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);//按照屏幕位置

        // 凭空捏造一个数学上的“无限大平地”（高度 Y=0）
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float rayDistance;

        // 如果射线打到了这个“平地”上
        if (groundPlane.Raycast(ray, out rayDistance))
        {
            // 获取鼠标在 3D 世界中究竟指着哪块地砖
            Vector3 point = ray.GetPoint(rayDistance);

            // 像之前敌人看玩家一样，锁定玩家的 Y 轴高度，防止玩家趴下看地
            Vector3 lookPos = new Vector3(point.x, transform.position.y, point.z);

            // 让玩家永远面朝鼠标所在的位置！
            transform.LookAt(lookPos);
        }
    }
}
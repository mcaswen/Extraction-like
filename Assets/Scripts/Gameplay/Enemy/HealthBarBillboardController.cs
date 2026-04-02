using UnityEngine;
/// <summary>
/// 敌人血条UI朝向控制
/// </summary>
public class HealthBarBillboardController : MonoBehaviour
{
    private Camera _mainCamera;//获取摄像头组件

    void Start()
    {
        _mainCamera = Camera.main;//初始化赋值摄像头组件
    }

    void LateUpdate()
    {
        if (_mainCamera != null)
        {
            transform.LookAt(transform.position + _mainCamera.transform.forward);//控制血条朝向和摄像头前方方向平行
        }
    }
}
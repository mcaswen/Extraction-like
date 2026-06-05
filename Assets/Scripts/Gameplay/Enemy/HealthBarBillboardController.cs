using UnityEngine;

/// <summary>
/// 世界空间血条朝向控制器，让血条始终面向当前主摄像机。
/// </summary>
public class HealthBarBillboardController : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null)
        {
            return;
        }

        transform.LookAt(transform.position + _mainCamera.transform.forward);
    }
}


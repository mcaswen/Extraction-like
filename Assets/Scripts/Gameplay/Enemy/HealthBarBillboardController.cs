using UnityEngine;

/// <summary>
/// Keeps world-space health bars facing the active camera.
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


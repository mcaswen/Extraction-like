using UnityEngine;

/// <summary>
/// 2.5D camera follower. Locks the initial rear view when a target is bound,
/// then follows position without rotating around the target every frame.
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    public Transform TargetTransform;

    public Vector3 Offset = new Vector3(0f, 10f, -10f);
    public float SmoothSpeed = 5f;

    [Header("Rotation")]
    public bool FollowTargetYaw = true;
    public bool LookAtTarget = true;
    public Vector3 LookAtOffset = new Vector3(0f, 1.5f, 0f);
    public float RotationSmoothSpeed = 8f;

    private Transform _lockedTarget;
    private Vector3 _lockedOffset;
    private Quaternion _lockedRotation;
    private bool _hasLockedInitialView;

    private void LateUpdate()
    {
        if (TargetTransform == null)
        {
            _lockedTarget = null;
            _hasLockedInitialView = false;
            return;
        }

        if (!_hasLockedInitialView || _lockedTarget != TargetTransform)
        {
            LockInitialView();
        }

        Vector3 desiredPosition = TargetTransform.position + _lockedOffset;
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            SmoothSpeed * Time.deltaTime);

        if (LookAtTarget)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                _lockedRotation,
                RotationSmoothSpeed * Time.deltaTime);
        }
    }

    private void LockInitialView()
    {
        _lockedTarget = TargetTransform;
        _lockedOffset = ResolveOffset();
        _lockedRotation = transform.rotation;
        _hasLockedInitialView = true;

        if (!LookAtTarget || TargetTransform == null)
        {
            return;
        }

        Vector3 cameraPosition = TargetTransform.position + _lockedOffset;
        Vector3 lookTarget = TargetTransform.position + LookAtOffset;
        Vector3 lookDirection = lookTarget - cameraPosition;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            _lockedRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private Vector3 ResolveOffset()
    {
        if (!FollowTargetYaw || TargetTransform == null)
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

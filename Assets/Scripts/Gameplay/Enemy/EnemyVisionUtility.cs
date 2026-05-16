using UnityEngine;

public static class EnemyVisionUtility
{
    private const float FullViewAngleThreshold = 359.9f;

    public static bool CanSeeTarget(
        Transform viewer,
        Transform target,
        float viewRange,
        float viewAngle,
        LayerMask lineOfSightBlockMask,
        float eyeHeight,
        float targetHeight)
    {
        if (viewer == null || target == null)
        {
            return false;
        }

        if (!IsTargetInHorizontalRange(viewer.position, target.position, viewRange))
        {
            return false;
        }

        if (!IsTargetInHorizontalAngle(viewer, target.position, viewAngle))
        {
            return false;
        }

        return HasLineOfSight(viewer, target, lineOfSightBlockMask, eyeHeight, targetHeight);
    }

    public static bool IsTargetInHorizontalRange(Vector3 viewerPosition, Vector3 targetPosition, float viewRange)
    {
        Vector3 toTarget = targetPosition - viewerPosition;
        toTarget.y = 0f;
        float effectiveRange = Mathf.Max(0.1f, viewRange);
        return toTarget.sqrMagnitude <= effectiveRange * effectiveRange;
    }

    public static bool IsTargetInHorizontalAngle(Transform viewer, Vector3 targetPosition, float viewAngle)
    {
        if (viewer == null)
        {
            return false;
        }

        float effectiveAngle = Mathf.Clamp(viewAngle, 1f, 360f);
        if (effectiveAngle >= FullViewAngleThreshold)
        {
            return true;
        }

        Vector3 forward = viewer.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 toTarget = targetPosition - viewer.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float angleToTarget = Vector3.Angle(forward.normalized, toTarget.normalized);
        return angleToTarget <= effectiveAngle * 0.5f;
    }

    public static bool HasLineOfSight(
        Transform viewer,
        Transform target,
        LayerMask lineOfSightBlockMask,
        float eyeHeight,
        float targetHeight)
    {
        if (viewer == null || target == null)
        {
            return false;
        }

        Vector3 origin = GetEyePosition(viewer, eyeHeight);
        Vector3 targetPosition = GetTargetPosition(target, targetHeight);
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Ray ray = new Ray(origin, direction / distance);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            distance,
            lineOfSightBlockMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return true;
        }

        System.Array.Sort(hits, CompareHitDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null ||
                hitCollider.transform.IsChildOf(viewer) ||
                hitCollider.transform.IsChildOf(target))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static Vector3 GetEyePosition(Transform viewer, float eyeHeight)
    {
        return viewer != null
            ? viewer.position + Vector3.up * Mathf.Max(0f, eyeHeight)
            : Vector3.zero;
    }

    public static Vector3 GetTargetPosition(Transform target, float targetHeight)
    {
        return target != null
            ? target.position + Vector3.up * Mathf.Max(0f, targetHeight)
            : Vector3.zero;
    }

    public static void DrawVisionGizmos(
        Transform viewer,
        Transform target,
        float viewRange,
        float viewAngle,
        float eyeHeight,
        float targetHeight,
        Color color)
    {
        if (viewer == null)
        {
            return;
        }

        float effectiveRange = Mathf.Max(0.1f, viewRange);
        float effectiveAngle = Mathf.Clamp(viewAngle, 1f, 360f);

        Gizmos.color = color;
        Gizmos.DrawWireSphere(viewer.position, effectiveRange);

        if (effectiveAngle < FullViewAngleThreshold)
        {
            Vector3 forward = viewer.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                Quaternion leftRotation = Quaternion.Euler(0f, -effectiveAngle * 0.5f, 0f);
                Quaternion rightRotation = Quaternion.Euler(0f, effectiveAngle * 0.5f, 0f);
                Vector3 left = leftRotation * forward * effectiveRange;
                Vector3 right = rightRotation * forward * effectiveRange;
                Gizmos.DrawLine(viewer.position, viewer.position + left);
                Gizmos.DrawLine(viewer.position, viewer.position + right);
            }
        }

        if (target != null)
        {
            Gizmos.DrawLine(GetEyePosition(viewer, eyeHeight), GetTargetPosition(target, targetHeight));
        }
    }

    private static int CompareHitDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }
}

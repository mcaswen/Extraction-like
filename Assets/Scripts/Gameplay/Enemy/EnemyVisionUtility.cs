using UnityEngine;

/// <summary>
/// 敌人视野检测工具，封装距离、水平角度和遮挡射线判断。
/// </summary>
public static class EnemyVisionUtility
{
    private const float FullViewAngleThreshold = 359.9f;

    /// <summary>
    /// 判断观察者是否能在给定视野参数下看到目标。
    /// </summary>
    /// <param name="viewer">观察者节点。</param>
    /// <param name="target">被观察目标。</param>
    /// <param name="viewRange">视野距离。</param>
    /// <param name="viewAngle">水平视野角度。</param>
    /// <param name="lineOfSightBlockMask">阻挡视线的层。</param>
    /// <param name="eyeHeight">观察者视线起点高度。</param>
    /// <param name="targetHeight">目标视线采样高度。</param>
    /// <returns>目标在距离、角度和无遮挡条件内时返回 true。</returns>
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

    /// <summary>
    /// 判断目标是否处于水平视距内。
    /// </summary>
    /// <param name="viewerPosition">观察者位置。</param>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="viewRange">视野距离。</param>
    /// <returns>目标水平距离不超过视野距离时返回 true。</returns>
    public static bool IsTargetInHorizontalRange(Vector3 viewerPosition, Vector3 targetPosition, float viewRange)
    {
        Vector3 toTarget = targetPosition - viewerPosition;
        toTarget.y = 0f;
        float effectiveRange = Mathf.Max(0.1f, viewRange);
        return toTarget.sqrMagnitude <= effectiveRange * effectiveRange;
    }

    /// <summary>
    /// 判断目标是否处于观察者水平视野夹角内。
    /// </summary>
    /// <param name="viewer">观察者节点。</param>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="viewAngle">水平视野角度。</param>
    /// <returns>目标落在视野夹角内时返回 true。</returns>
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

    /// <summary>
    /// 判断观察者和目标之间是否没有被指定层遮挡。
    /// </summary>
    /// <param name="viewer">观察者节点。</param>
    /// <param name="target">目标节点。</param>
    /// <param name="lineOfSightBlockMask">阻挡视线的层。</param>
    /// <param name="eyeHeight">观察者视线起点高度。</param>
    /// <param name="targetHeight">目标视线采样高度。</param>
    /// <returns>射线路径没有有效遮挡物时返回 true。</returns>
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

        // RaycastAll 不保证顺序，先按距离排序才能稳定跳过自身和目标碰撞体。
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

    /// <summary>
    /// 获取观察者视线起点。
    /// </summary>
    /// <param name="viewer">观察者节点。</param>
    /// <param name="eyeHeight">相对观察者根节点的高度。</param>
    /// <returns>视线起点世界坐标。</returns>
    public static Vector3 GetEyePosition(Transform viewer, float eyeHeight)
    {
        return viewer != null
            ? viewer.position + Vector3.up * Mathf.Max(0f, eyeHeight)
            : Vector3.zero;
    }

    /// <summary>
    /// 获取目标视线采样点。
    /// </summary>
    /// <param name="target">目标节点。</param>
    /// <param name="targetHeight">相对目标根节点的高度。</param>
    /// <returns>目标采样点世界坐标。</returns>
    public static Vector3 GetTargetPosition(Transform target, float targetHeight)
    {
        return target != null
            ? target.position + Vector3.up * Mathf.Max(0f, targetHeight)
            : Vector3.zero;
    }

    /// <summary>
    /// 在 Scene 视图中绘制敌人视野调试辅助线。
    /// </summary>
    /// <param name="viewer">观察者节点。</param>
    /// <param name="target">可选目标节点。</param>
    /// <param name="viewRange">视野距离。</param>
    /// <param name="viewAngle">水平视野角度。</param>
    /// <param name="eyeHeight">观察者视线起点高度。</param>
    /// <param name="targetHeight">目标视线采样高度。</param>
    /// <param name="color">绘制颜色。</param>
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

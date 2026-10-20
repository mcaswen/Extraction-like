using UnityEngine;

namespace Gameplay.Perception
{
    public static class TargetVisibilityQuery
    {
        public static TargetVisibilityResult Check(Transform viewer, Vector3 origin, Transform target, float range,
            float angle = 360f, int mask = Physics.DefaultRaycastLayers)
            => Check(viewer, origin, target, range, angle, mask, out _, out _);

        /// <summary>同上，另外给出判定为受阻时挡住视线的碰撞体和挡点；未受阻时碰撞体为空</summary>
        public static TargetVisibilityResult Check(Transform viewer, Vector3 origin, Transform target, float range,
            float angle, int mask, out Collider blocker, out Vector3 blockPoint)
        {
            blocker = null; blockPoint = origin;
            if (viewer == null || target == null || !target.gameObject.activeInHierarchy) return TargetVisibilityResult.Invalid;
            Vector3 aim = CombatAimPointResolver.Resolve(target);
            Vector3 offset = aim - origin;
            if (offset.sqrMagnitude > range * range) return TargetVisibilityResult.OutOfRange;
            Vector3 planar = new Vector3(offset.x, 0f, offset.z);
            if (angle < 360f && planar.sqrMagnitude > 0.001f && Vector3.Angle(viewer.forward, planar) > angle * 0.5f)
                return TargetVisibilityResult.OutsideView;
            return ClearSegment(viewer, target, origin, aim, mask, null, out blocker, out blockPoint)
                ? TargetVisibilityResult.Visible : TargetVisibilityResult.Occluded;
        }

        public static bool ClearSegment(Transform viewer, Transform target, Vector3 from, Vector3 to,
            int mask = Physics.DefaultRaycastLayers, System.Predicate<Collider> ignore = null)
            => ClearSegment(viewer, target, from, to, mask, ignore, out _, out _);

        /// <summary>同上，另外给出挡住线段的碰撞体和挡点；线段通畅时碰撞体为空</summary>
        public static bool ClearSegment(Transform viewer, Transform target, Vector3 from, Vector3 to,
            int mask, System.Predicate<Collider> ignore, out Collider blocker, out Vector3 blockPoint)
        {
            blocker = null; blockPoint = from;
            // Vision transforms can be bones/eye children. Resolve only the nearest body collider,
            // never transform.root (which may be a whole level container).
            if (viewer != null)
            {
                Collider body = viewer.GetComponentInParent<Collider>();
                if (body != null) viewer = body.transform;
            }
            Vector3 offset = to - from;
            if (offset.sqrMagnitude < 0.000001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(from, offset.normalized, offset.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                Transform root = hit.collider.transform;
                if (BelongsTo(root, viewer) || BelongsTo(root, target) || (ignore != null && ignore(hit.collider))) continue;
                blocker = hit.collider; blockPoint = hit.point;
                return false;
            }
            // Raycasts do not report a collider containing the ray origin.
            foreach (Collider collider in Physics.OverlapSphere(from, 0.02f, mask, QueryTriggerInteraction.Ignore))
                if (!BelongsTo(collider.transform, viewer) && !BelongsTo(collider.transform, target) &&
                    (ignore == null || !ignore(collider)))
                {
                    blocker = collider;
                    return false;
                }
            return true;
        }

        public static bool BelongsTo(Transform candidate, Transform root) => root != null && (candidate == root || candidate.IsChildOf(root));
    }
}

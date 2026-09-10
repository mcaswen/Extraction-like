using UnityEngine;

namespace Gameplay.Perception
{
    public static class TargetVisibilityQuery
    {
        public static TargetVisibilityResult Check(Transform viewer, Vector3 origin, Transform target, float range,
            float angle = 360f, int mask = Physics.DefaultRaycastLayers)
        {
            if (viewer == null || target == null || !target.gameObject.activeInHierarchy) return TargetVisibilityResult.Invalid;
            Vector3 aim = CombatAimPointResolver.Resolve(target);
            Vector3 offset = aim - origin;
            if (offset.sqrMagnitude > range * range) return TargetVisibilityResult.OutOfRange;
            Vector3 planar = new Vector3(offset.x, 0f, offset.z);
            if (angle < 360f && planar.sqrMagnitude > 0.001f && Vector3.Angle(viewer.forward, planar) > angle * 0.5f)
                return TargetVisibilityResult.OutsideView;
            return ClearSegment(viewer, target, origin, aim, mask) ? TargetVisibilityResult.Visible : TargetVisibilityResult.Occluded;
        }

        public static bool ClearSegment(Transform viewer, Transform target, Vector3 from, Vector3 to, int mask = Physics.DefaultRaycastLayers)
        {
            Vector3 offset = to - from;
            if (offset.sqrMagnitude < 0.000001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(from, offset.normalized, offset.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                Transform root = hit.collider.transform;
                if (BelongsTo(root, viewer) || BelongsTo(root, target)) continue;
                return false;
            }
            // Raycasts do not report a collider containing the ray origin.
            foreach (Collider collider in Physics.OverlapSphere(from, 0.02f, mask, QueryTriggerInteraction.Ignore))
                if (!BelongsTo(collider.transform, viewer) && !BelongsTo(collider.transform, target)) return false;
            return true;
        }

        public static bool BelongsTo(Transform candidate, Transform root) => root != null && (candidate == root || candidate.IsChildOf(root));
    }
}

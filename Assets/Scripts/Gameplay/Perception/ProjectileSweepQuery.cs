using UnityEngine;

namespace Gameplay.Perception
{
    /// <summary>Finds the first solid collision on a projectile's next physics segment.</summary>
    public static class ProjectileSweepQuery
    {
        public static bool TryFirstHit(Transform projectile, Transform source, Vector3 from, Vector3 to, float radius, out Collider collider)
        {
            collider=null;
            foreach (Collider overlap in Physics.OverlapSphere(from,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if (IsBlocking(overlap,projectile,source)) { collider=overlap; return true; }
            Vector3 offset=to-from;
            if (offset.sqrMagnitude < 0.000001f) return false;
            RaycastHit[] hits=Physics.SphereCastAll(from,radius,offset.normalized,offset.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float best=float.PositiveInfinity;
            foreach(RaycastHit hit in hits)
            {
                if (hit.distance>=best || !IsBlocking(hit.collider,projectile,source)) continue;
                best=hit.distance; collider=hit.collider;
            }
            return collider != null;
        }
        public static bool IsBlocking(Collider collider, Transform projectile, Transform source) => collider != null &&
            collider.enabled && !collider.isTrigger && !TargetVisibilityQuery.BelongsTo(collider.transform,projectile) &&
            !TargetVisibilityQuery.BelongsTo(collider.transform,source);
    }
}

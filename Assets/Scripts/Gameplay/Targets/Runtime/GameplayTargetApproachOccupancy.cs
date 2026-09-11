using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Targets.Runtime
{
    /// <summary>Checks physical navigation occupancy without owning targets or movement.</summary>
    public sealed class GameplayTargetApproachOccupancy
    {
        private const float Clearance = 0.2f;
        private Collider[] _overlaps = new Collider[16];

        public bool IsClear(NavMeshAgent requester, Vector3 groundPosition)
        {
            if (requester == null || !requester.isActiveAndEnabled) return true;
            Vector3 scale = requester.transform.lossyScale;
            float radius = Mathf.Max(0.01f, requester.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            float height = Mathf.Max(radius * 2f, requester.height * Mathf.Abs(scale.y));
            Vector3 bottom = groundPosition + Vector3.up * radius;
            Vector3 top = groundPosition + Vector3.up * (height - radius);
            int count;
            // A full buffer is incomplete evidence. Expand once needed, then reuse it.
            while ((count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + Clearance, _overlaps,
                       Physics.AllLayers, QueryTriggerInteraction.Ignore)) == _overlaps.Length)
                System.Array.Resize(ref _overlaps, _overlaps.Length * 2);

            for (int i = 0; i < count; i++)
            {
                Collider body = _overlaps[i];
                if (body == null || body.transform.IsChildOf(requester.transform)) continue;
                NavMeshAgent occupant = body.GetComponentInParent<NavMeshAgent>();
                if (occupant != null && occupant != requester && occupant.isActiveAndEnabled && occupant.isOnNavMesh)
                    return false;
            }
            return true;
        }
    }
}

using UnityEngine;

namespace Gameplay.Perception
{
    public static class CombatAimPointResolver
    {
        public static Vector3 Resolve(Transform target)
        {
            if (target == null) return default;
            foreach (Collider collider in target.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy)
                    return collider.bounds.center;
            return target.position + Vector3.up;
        }
    }
}

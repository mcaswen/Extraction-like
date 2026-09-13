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
                    return CurrentCenter(collider);
            return target.position + Vector3.up;
        }

        private static Vector3 CurrentCenter(Collider collider)
        {
            // bounds 属于物理同步后的姿态，NavMesh/Transform 本帧位移后可能仍在旧位置。
            // 本地几何中心按当前姿态变换，不为感知查询触发全局 Physics.SyncTransforms。
            Vector3 local;
            switch (collider)
            {
                case CapsuleCollider capsule: local = capsule.center; break;
                case SphereCollider sphere: local = sphere.center; break;
                case BoxCollider box: local = box.center; break;
                case MeshCollider mesh when mesh.sharedMesh != null: local = mesh.sharedMesh.bounds.center; break;
                default: return collider.bounds.center;
            }
            return collider.transform.TransformPoint(local);
        }
    }
}

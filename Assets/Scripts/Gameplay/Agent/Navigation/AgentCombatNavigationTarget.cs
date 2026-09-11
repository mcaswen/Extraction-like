using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>将敌人身体根坐标解析为追击地表点；不改变瞄准点、射程或完整路径约束。</summary>
    public static class AgentCombatNavigationTarget
    {
        public static Vector3 Resolve(global::EnemyHealthController enemy)
        {
            if (enemy == null) return default;
            var root = enemy.transform;
            if (enemy.TryGetComponent(out NavMeshAgent navigation) && AgentNavigationQuery.IsReady(navigation))
                return navigation.nextPosition - Vector3.up * navigation.baseOffset * Mathf.Abs(root.lossyScale.y);
            Vector3 position = root.position;
            if (!enemy.TryGetComponent(out Collider body) || !body.enabled || body.isTrigger) return position;
            var bounds = body.bounds;
            Vector3 feet = new Vector3(position.x, bounds.min.y, position.z);
            if (NavMesh.SamplePosition(feet, out var hit, 0.5f, NavMesh.AllAreas)) return hit.position;
            // 兼容居中 Collider 部分埋入地面的旧资产；禁止横向跳到另一片导航面。
            if (NavMesh.SamplePosition(position, out hit, Mathf.Max(0.5f, bounds.extents.y), NavMesh.AllAreas) &&
                hit.position.y >= bounds.min.y - 0.1f && hit.position.y <= bounds.max.y + 0.1f &&
                new Vector2(hit.position.x - position.x, hit.position.z - position.z).sqrMagnitude <= 0.25f)
                return hit.position;
            return position;
        }
    }
}

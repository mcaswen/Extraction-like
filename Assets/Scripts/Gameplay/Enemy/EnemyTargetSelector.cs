using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using Gameplay.Perception;
using UnityEngine;

public static class EnemyTargetSelector
{
    public static bool TrySelectVisible(Transform viewer, float range, float angle, LayerMask mask, float eyeHeight, out Transform target)
        => Select(viewer, range, angle, mask, eyeHeight, true, out target);
    public static bool TrySelectNearest(Transform viewer, out Transform target)
        => Select(viewer, float.PositiveInfinity, 360f, Physics.DefaultRaycastLayers, 1f, false, out target);

    private static bool Select(Transform viewer, float range, float angle, int mask, float eyeHeight, bool requireVisibility, out Transform target)
    {
        target=null;
        float best=float.PositiveInfinity;
        string bestId=null;
        var registry=AgentRuntimeRegistry.ActiveInstance;
        if (registry != null)
        {
            var agents=registry.RegisteredAgents;
            for(int i=0;i<agents.Count;i++)
            {
                var handle=agents[i];
                if (!handle.IsAlive || !Eligible(viewer,handle.CachedTransform,range,angle,mask,eyeHeight,requireVisibility)) continue;
                float distance=viewer != null ? (handle.CachedTransform.position-viewer.position).sqrMagnitude : 0f;
                if (distance>best || distance==best && string.CompareOrdinal(handle.AgentId.Value,bestId)>=0) continue;
                target=handle.CachedTransform; best=distance; bestId=handle.AgentId.Value;
            }
        }
        if (target != null) return true;
        // Legacy scenes can have a Player without an Agent registry. Never resurrect an invalid registered pawn via its tag.
        foreach(GameObject player in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (player.GetComponentInParent<AgentPawnRoot>() != null || !Eligible(viewer,player.transform,range,angle,mask,eyeHeight,requireVisibility)) continue;
            float distance=viewer != null ? (player.transform.position-viewer.position).sqrMagnitude : 0f;
            if (distance>=best) continue;
            target=player.transform; best=distance;
        }
        return target != null;
    }
    private static bool Eligible(Transform viewer, Transform target, float range, float angle, int mask, float eyeHeight, bool visibility)
    {
        return EnemyCombatTargetBinding.TryCreate(target,out _) && (!visibility ||
            TargetVisibilityQuery.Check(viewer,viewer.position+Vector3.up*eyeHeight,target,range,angle,mask)==TargetVisibilityResult.Visible);
    }
}

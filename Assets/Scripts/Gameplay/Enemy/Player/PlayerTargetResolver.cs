using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;

public static class PlayerTargetResolver
{
    public static bool TryGetCurrentPlayerTransform(Transform seeker, out Transform target)
    {
        return EnemyTargetSelector.TrySelectNearest(seeker, out target);
    }

    public static bool TryGetDamageReceiver(Transform target, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (target == null)
            return false;

        return CombatDamageUtility.TryGetDamageReceiver(target, out receiver);
    }

    public static bool TryGetDamageReceiver(Collider collider, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (collider == null)
            return false;

        return CombatDamageUtility.TryGetDamageReceiver(collider, out receiver);
    }

    public static bool TryGetAgentHealth(Transform target, out AgentHealthController healthController)
    {
        healthController = null;
        if (target == null)
            return false;

        healthController = target.GetComponentInParent<AgentHealthController>();
        if (healthController != null)
            return true;

        healthController = target.GetComponentInChildren<AgentHealthController>();
        return healthController != null;
    }

    public static bool IsPlayerTarget(Transform target)
    {
        if (target == null)
            return false;

        if (TryGetAgentHealth(target, out _))
            return true;

        Transform root = target.root;
        return target.CompareTag("Player") || (root != null && root.CompareTag("Player"));
    }
}

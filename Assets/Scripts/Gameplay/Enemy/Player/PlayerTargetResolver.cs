using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;

public static class PlayerTargetResolver
{
    public static bool TryGetCurrentPlayerTransform(Transform seeker, out Transform target)
    {
        target = null;

        if (TryGetNearestRegisteredAgent(seeker, out target))
        {
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return false;

        target = playerObject.transform;
        return true;
    }

    private static bool TryGetNearestRegisteredAgent(Transform seeker, out Transform target)
    {
        target = null;
        AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
        if (registry == null)
            return false;

        float bestDistanceSqr = float.PositiveInfinity;
        var registeredAgents = registry.RegisteredAgents;
        for (int i = 0; i < registeredAgents.Count; i++)
        {
            AgentRuntimeHandle handle = registeredAgents[i];
            if (!handle.IsAlive || handle.CachedTransform == null)
                continue;

            if (seeker == null)
            {
                target = handle.CachedTransform;
                return true;
            }

            float distanceSqr = (handle.CachedTransform.position - seeker.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            target = handle.CachedTransform;
        }

        return target != null;
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

        Transform root = target.root;
        if (root == null)
            return false;

        healthController = root.GetComponent<AgentHealthController>();
        if (healthController != null)
            return true;

        healthController = root.GetComponentInChildren<AgentHealthController>();
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

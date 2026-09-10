using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;

/// <summary>An atomic identity for damage and movement; never retains a receiver from another pawn.</summary>
public readonly struct EnemyCombatTargetBinding
{
    public Transform Target { get; }
    public ICombatDamageReceiver DamageReceiver { get; }
    public IExternalMovementReceiver MovementReceiver { get; }
    private EnemyCombatTargetBinding(Transform target, ICombatDamageReceiver damage, IExternalMovementReceiver movement)
    { Target=target; DamageReceiver=damage; MovementReceiver=movement; }

    public static bool TryCreate(Transform target, out EnemyCombatTargetBinding binding)
    {
        binding=default;
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        if (!CombatDamageUtility.TryGetDamageReceiver(target,out ICombatDamageReceiver receiver) ||
            receiver.DamageRootTransform == null || !receiver.IsCombatDamageReceiverAlive) return false;
        Transform root=receiver.DamageRootTransform;
        if (!root.gameObject.activeInHierarchy || receiver is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
        var pawn=root.GetComponent<AgentPawnRoot>();
        if (pawn != null && (!pawn.isActiveAndEnabled || pawn.IsDead || AgentRuntimeRegistry.ActiveInstance == null ||
            !AgentRuntimeRegistry.ActiveInstance.TryGetHandle(pawn.AgentId,out var handle) || handle.PawnRoot != pawn)) return false;
        binding=new EnemyCombatTargetBinding(root,receiver,root.GetComponent<IExternalMovementReceiver>());
        return true;
    }
}

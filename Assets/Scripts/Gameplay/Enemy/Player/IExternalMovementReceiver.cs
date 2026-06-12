using UnityEngine;

/// <summary>
/// Receives external movement effects such as knockback and temporary movement slow.
/// </summary>
public interface IExternalMovementReceiver
{
    void ApplyExternalPull(Vector3 targetPosition, float pullStrength);

    void ApplyExternalImpulse(Vector3 direction, float strength);

    void ApplyMoveSpeedDebuff(float multiplier, float duration);
}

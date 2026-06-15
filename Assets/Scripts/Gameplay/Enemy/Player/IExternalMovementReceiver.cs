using UnityEngine;

/// <summary>
/// Receives external movement effects such as direct pull, knockback, and temporary movement slow.
/// </summary>
public interface IExternalMovementReceiver
{
    /// <summary>
    /// Directly moves the receiver toward a target position.
    /// </summary>
    /// <param name="targetPosition">World position to pull toward.</param>
    /// <param name="pullStrength">Pull speed in world units per second.</param>
    void ApplyExternalPull(Vector3 targetPosition, float pullStrength);

    void ApplyExternalImpulse(Vector3 direction, float strength);

    void ApplyMoveSpeedDebuff(float multiplier, float duration);
}

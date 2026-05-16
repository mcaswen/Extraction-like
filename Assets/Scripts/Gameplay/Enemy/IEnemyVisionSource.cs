using UnityEngine;

public interface IEnemyVisionSource
{
    Transform VisionTransform { get; }
    Transform PlayerTransform { get; }
    float DetectionRange { get; }
    float ViewAngle { get; }
    LayerMask LineOfSightBlockMask { get; }
    LayerMask GroundMask { get; }
    float EyeHeight { get; }
    float TargetHeight { get; }
    bool ShouldShowVision { get; }
    bool CanSeePlayerForVision { get; }
}

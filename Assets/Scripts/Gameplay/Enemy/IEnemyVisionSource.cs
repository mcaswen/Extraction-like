using UnityEngine;

/// <summary>
/// 敌人视野系统的数据源接口。
/// 由具体敌人控制器提供视野朝向、距离、遮挡层和玩家引用。
/// </summary>
public interface IEnemyVisionSource
{
    /// <summary>
    /// 用于视野检测和扇形可视化的朝向节点。
    /// </summary>
    Transform VisionTransform { get; }

    /// <summary>
    /// 当前敌人正在检测的玩家目标。
    /// </summary>
    Transform PlayerTransform { get; }

    /// <summary>
    /// 敌人视野检测距离。
    /// </summary>
    float DetectionRange { get; }

    /// <summary>
    /// 敌人水平视野角度。
    /// </summary>
    float ViewAngle { get; }

    /// <summary>
    /// 视线检测时会阻挡视野的层。
    /// </summary>
    LayerMask LineOfSightBlockMask { get; }

    /// <summary>
    /// 视野可视化投影到地面时使用的层。
    /// </summary>
    LayerMask GroundMask { get; }

    /// <summary>
    /// 敌人视线起点高度。
    /// </summary>
    float EyeHeight { get; }

    /// <summary>
    /// 目标视线采样高度。
    /// </summary>
    float TargetHeight { get; }

    /// <summary>
    /// 当前敌人是否允许显示视野扇形。
    /// </summary>
    bool ShouldShowVision { get; }

    /// <summary>
    /// 当前敌人是否已经通过玩法视野看到玩家。
    /// </summary>
    bool CanSeePlayerForVision { get; }
}

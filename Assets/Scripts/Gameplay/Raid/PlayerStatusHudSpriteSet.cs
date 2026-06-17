using UnityEngine;

/// <summary>
/// 玩家状态界面的图标和进度条图片配置资产
/// </summary>
[CreateAssetMenu(menuName = "Raid/Player Status HUD Sprite Set")]
public sealed class PlayerStatusHudSpriteSet : ScriptableObject
{
    [SerializeField] private Sprite _avatarIcon;
    [SerializeField] private Sprite _healthIcon;
    [SerializeField] private Sprite _carryIcon;
    [SerializeField] private Sprite _progressBar;
    [SerializeField] private Rect _avatarCropRect;

    public Sprite AvatarIcon => _avatarIcon;
    public Sprite HealthIcon => _healthIcon;
    public Sprite CarryIcon => _carryIcon;
    public Sprite ProgressBar => _progressBar;
    public Rect AvatarCropRect => _avatarCropRect;

    /// <summary>
    /// 由编辑器生成工具写入状态界面图片引用
    /// </summary>
    /// <param name="avatarIcon">头像图标</param>
    /// <param name="healthIcon">生命图标</param>
    /// <param name="carryIcon">负重图标</param>
    /// <param name="progressBar">进度条图片</param>
    /// <param name="avatarCropRect">头像裁切区域</param>
    public void Configure(Sprite avatarIcon, Sprite healthIcon, Sprite carryIcon, Sprite progressBar, Rect avatarCropRect)
    {
        _avatarIcon = avatarIcon;
        _healthIcon = healthIcon;
        _carryIcon = carryIcon;
        _progressBar = progressBar;
        _avatarCropRect = avatarCropRect;
    }
}

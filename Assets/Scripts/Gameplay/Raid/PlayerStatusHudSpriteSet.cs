using UnityEngine;

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

    public void Configure(Sprite avatarIcon, Sprite healthIcon, Sprite carryIcon, Sprite progressBar, Rect avatarCropRect)
    {
        _avatarIcon = avatarIcon;
        _healthIcon = healthIcon;
        _carryIcon = carryIcon;
        _progressBar = progressBar;
        _avatarCropRect = avatarCropRect;
    }
}

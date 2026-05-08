using UnityEngine;

namespace Gameplay.Player.SO
{
    /// <summary>
    /// 主角身体层静态配置。
    /// 当前阶段先只承载 Pawn Root 真正会用到的最小基础参数
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Player_PawnConfig",
        menuName = "SO/Player/PawnConfig")]
    public sealed class PlayerPawnConfig : ScriptableObject
    {
        [Header("基础属性")]
        [SerializeField] private int _maxHealth = 100;

        [Header("恢复相关")]
        [SerializeField] private float _lowHealthRecoveryThreshold = 0.35f;

        public int MaxHealth => _maxHealth;
        public float LowHealthRecoveryThreshold => _lowHealthRecoveryThreshold;
    }
}
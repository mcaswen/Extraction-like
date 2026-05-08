using UnityEngine;

namespace Gameplay.Player.Data
{
    /// <summary>
    /// 主角受到伤害的请求数据结构
    /// 包含伤害数值、击中位置和击中方向等信息
    /// </summary>
    public readonly struct DamageRequest
    {
        public int DamageAmount { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitDirection { get; }

        public DamageRequest(int damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            DamageAmount = damageAmount;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }
    }
}
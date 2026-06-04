using UnityEngine;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent受到伤害的请求数据结构
    /// 包含伤害数值、击中位置和击中方向等信息
    /// </summary>
    public readonly struct DamageRequest
    {
        /// <summary>
        /// 伤害数值
        /// </summary>
        public int DamageAmount { get; }

        /// <summary>
        /// 命中世界坐标
        /// </summary>
        public Vector3 HitPoint { get; }

        /// <summary>
        /// 命中方向
        /// </summary>
        public Vector3 HitDirection { get; }

        /// <summary>
        /// 创建 Agent 受伤请求
        /// </summary>
        /// <param name="damageAmount"></param>
        /// <param name="hitPoint"></param>
        /// <param name="hitDirection"></param>
        public DamageRequest(int damageAmount, Vector3 hitPoint, Vector3 hitDirection)
        {
            DamageAmount = damageAmount;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }
    }
}

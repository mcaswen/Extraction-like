using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗风格配置
    /// 汇总普通攻击参数、元素类型和可释放技能列表
    /// </summary>
    [CreateAssetMenu(
        fileName = "SO_Agent_CombatStyle",
        menuName = "SO/Agent/Combat/Style")]
    public sealed class AgentCombatStyleConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _styleId;
        [SerializeField] private string _displayName;
        [SerializeField] private AgentCombatElementType _element = AgentCombatElementType.Physical;
        [SerializeField] private AgentCombatWeaponType _weaponType = AgentCombatWeaponType.None;

        [Header("Normal Attack")]
        [SerializeField, Min(0f)] private float _normalAttackRange = 8f;
        [SerializeField, Min(0.05f)] private float _normalAttackInterval = 0.65f;
        [SerializeField] private AgentCombatStatScaling _normalAttackDamage =
            new AgentCombatStatScaling(AgentCombatStatScalingSource.Attack, 1f);

        [Header("Skills")]
        [SerializeField] private AgentCombatSkillConfigBase[] _skills;

        /// <summary>
        /// 战斗风格稳定 ID，未填写时使用资产名
        /// </summary>
        public string StyleId => string.IsNullOrWhiteSpace(_styleId) ? name : _styleId;

        /// <summary>
        /// 战斗风格显示名，未填写时使用风格 ID
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? StyleId : _displayName;

        /// <summary>
        /// 普通攻击与默认子弹使用的元素类型
        /// </summary>
        public AgentCombatElementType Element => _element;

        /// <summary>
        /// 战斗风格使用的武器类型
        /// </summary>
        public AgentCombatWeaponType WeaponType => _weaponType;

        /// <summary>
        /// 普通攻击射程
        /// </summary>
        public float NormalAttackRange => Mathf.Max(0f, _normalAttackRange);

        /// <summary>
        /// 普通攻击间隔
        /// </summary>
        public float NormalAttackInterval => Mathf.Max(0.05f, _normalAttackInterval);

        /// <summary>
        /// 当前风格配置的技能列表
        /// </summary>
        public IReadOnlyList<AgentCombatSkillConfigBase> Skills => _skills;

        /// <summary>
        /// 根据运行时属性计算普通攻击伤害
        /// </summary>
        /// <param name="stats"></param>
        /// <returns></returns>
        public float CalculateNormalAttackDamage(AgentCombatRuntimeStats stats)
        {
            return _normalAttackDamage.Evaluate(stats);
        }

        private void OnValidate()
        {
            _normalAttackRange = Mathf.Max(0f, _normalAttackRange);
            _normalAttackInterval = Mathf.Max(0.05f, _normalAttackInterval);
        }
    }
}

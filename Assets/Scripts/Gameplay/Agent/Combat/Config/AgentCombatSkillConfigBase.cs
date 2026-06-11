using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗技能配置基类
    /// 定义技能身份、元素类型和通用冷却
    /// </summary>
    public abstract class AgentCombatSkillConfigBase : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _skillId;
        [SerializeField] private string _displayName;

        [Header("Common")]
        [SerializeField] private AgentCombatElementType _element = AgentCombatElementType.Physical;
        [SerializeField, Min(0.05f)] private float _cooldownSeconds = 1f;

        /// <summary>
        /// 技能稳定 ID，未填写时使用资产名
        /// </summary>
        public string SkillId => string.IsNullOrWhiteSpace(_skillId) ? name : _skillId;

        /// <summary>
        /// 技能显示名，未填写时使用技能 ID
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? SkillId : _displayName;

        /// <summary>
        /// 技能元素类型
        /// </summary>
        public AgentCombatElementType Element => _element;

        /// <summary>
        /// 技能冷却时间
        /// </summary>
        public float CooldownSeconds => Mathf.Max(0.05f, _cooldownSeconds);

        /// <summary>
        /// 创建该配置对应的运行时技能实例
        /// </summary>
        /// <returns></returns>
        public abstract AgentCombatSkillBase CreateRuntimeSkill();

        protected virtual void OnValidate()
        {
            _cooldownSeconds = Mathf.Max(0.05f, _cooldownSeconds);
        }
    }
}

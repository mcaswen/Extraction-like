using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public abstract class AgentCombatSkillConfigBase : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _skillId;
        [SerializeField] private string _displayName;

        [Header("Common")]
        [SerializeField] private AgentCombatElementType _element = AgentCombatElementType.Physical;
        [SerializeField, Min(0.05f)] private float _cooldownSeconds = 1f;

        public string SkillId => string.IsNullOrWhiteSpace(_skillId) ? name : _skillId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? SkillId : _displayName;
        public AgentCombatElementType Element => _element;
        public float CooldownSeconds => Mathf.Max(0.05f, _cooldownSeconds);

        public abstract AgentCombatSkillBase CreateRuntimeSkill();

        protected virtual void OnValidate()
        {
            _cooldownSeconds = Mathf.Max(0.05f, _cooldownSeconds);
        }
    }
}

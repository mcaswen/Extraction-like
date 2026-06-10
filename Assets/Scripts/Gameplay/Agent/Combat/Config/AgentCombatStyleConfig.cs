using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
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

        public string StyleId => string.IsNullOrWhiteSpace(_styleId) ? name : _styleId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? StyleId : _displayName;
        public AgentCombatElementType Element => _element;
        public AgentCombatWeaponType WeaponType => _weaponType;
        public float NormalAttackRange => Mathf.Max(0f, _normalAttackRange);
        public float NormalAttackInterval => Mathf.Max(0.05f, _normalAttackInterval);
        public IReadOnlyList<AgentCombatSkillConfigBase> Skills => _skills;

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

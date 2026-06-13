using System.Collections.Generic;
using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 持续区域伤害运行时实体
    /// 在固定区域内按 tick 对敌人造成伤害并附加状态
    /// </summary>
    public sealed class AgentCombatAreaDamageOverTime : MonoBehaviour
    {
        private readonly HashSet<global::EnemyHealthController> _targets =
            new HashSet<global::EnemyHealthController>();

        private Transform _casterTransform;
        private LayerMask _enemyLayerMask;
        private float _radius;
        private float _damagePerSecond;
        private float _durationSeconds;
        private float _tickInterval;
        private float _elapsedSeconds;
        private float _tickTimer;
        private float _slowDurationBonusSeconds;
        private AgentCombatStatusEffectDefinition _statusEffect;

        /// <summary>
        /// 初始化持续伤害区域的施法者、范围、伤害和视觉表现
        /// </summary>
        /// <param name="casterTransform"></param>
        /// <param name="enemyLayerMask"></param>
        /// <param name="radius"></param>
        /// <param name="damagePerSecond"></param>
        /// <param name="durationSeconds"></param>
        /// <param name="tickInterval"></param>
        /// <param name="statusEffect"></param>
        /// <param name="slowDurationBonusSeconds"></param>
        /// <param name="visualPrefab"></param>
        /// <param name="indicatorColor"></param>
        public void Initialize(
            Transform casterTransform,
            LayerMask enemyLayerMask,
            float radius,
            float damagePerSecond,
            float durationSeconds,
            float tickInterval,
            AgentCombatStatusEffectDefinition statusEffect,
            float slowDurationBonusSeconds,
            GameObject visualPrefab,
            Color indicatorColor)
        {
            _casterTransform = casterTransform;
            _enemyLayerMask = enemyLayerMask;
            _radius = Mathf.Max(0.1f, radius);
            _damagePerSecond = Mathf.Max(0f, damagePerSecond);
            _durationSeconds = Mathf.Max(0.05f, durationSeconds);
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            _statusEffect = statusEffect;
            _slowDurationBonusSeconds = Mathf.Max(0f, slowDurationBonusSeconds);
            _tickTimer = 0f;

            SpawnIndicator(visualPrefab, indicatorColor);
            Destroy(gameObject, _durationSeconds + 0.05f);
        }

        private void Update()
        {
            // 生命周期到期后销毁整块区域，避免残留 tick
            _elapsedSeconds += Time.deltaTime;
            if (_elapsedSeconds > _durationSeconds)
            {
                Destroy(gameObject);
                return;
            }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f)
                return;

            _tickTimer = _tickInterval;
            // tick 间隔到达后重新收集范围内敌人并结算本轮伤害
            ApplyTickDamage();
        }

        private void ApplyTickDamage()
        {
            // 每轮 tick 重新收集目标，适配敌人进出区域的情况
            AgentCombatSkillUtility.CollectEnemiesInSphere(
                transform.position,
                _radius,
                _enemyLayerMask,
                _targets);

            // DOT 本身没有独立属性，伤害上下文仅保留施法者和本次 tick 时长
            AgentCombatSkillContext context = new AgentCombatSkillContext(
                _casterTransform,
                null,
                new AgentCombatRuntimeStats(1, 0f, 0f),
                _enemyLayerMask,
                _tickInterval,
                new AgentCombatSkillModifiers(1f, 0f, _slowDurationBonusSeconds));

            float tickDamage = _damagePerSecond * _tickInterval;
            foreach (global::EnemyHealthController enemyHealth in _targets)
            {
                if (enemyHealth == null)
                    continue;

                AgentCombatSkillUtility.ApplyDamageAndStatus(
                    enemyHealth,
                    tickDamage,
                    context,
                    enemyHealth.transform.position,
                    global::EnemyDamageSourceType.Magic,
                    _statusEffect);
            }
        }

        private void SpawnIndicator(GameObject visualPrefab, Color indicatorColor)
        {
            if (visualPrefab != null)
            {
                // 有配置视觉时直接挂到区域根节点，跟随区域生命周期销毁，并对齐持续区域半径。
                GameObject visualObject = Instantiate(visualPrefab, transform);
                visualObject.transform.localPosition = Vector3.zero;
                visualObject.transform.localRotation = Quaternion.identity;
                visualObject.transform.localScale = AgentCombatSkillUtility.CreatePlanarRangeVisualScale(
                    _radius,
                    AgentCombatSkillUtility.AuthoredCircleVisualRadius);
                SkillEffectLayerUtility.ApplyToRoot(visualObject);
                return;
            }

            // 未配置视觉时生成最小圆柱指示器，保证技能在白盒场景中仍可见
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicatorObject.name = "AgentAreaDamageOverTimeIndicator";
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localPosition = Vector3.up * 0.03f;
            indicatorObject.transform.localScale = new Vector3(_radius * 2f, 0.02f, _radius * 2f);
            SkillEffectLayerUtility.ApplyToRoot(indicatorObject);

            Collider indicatorCollider = indicatorObject.GetComponent<Collider>();
            if (indicatorCollider != null)
                indicatorCollider.enabled = false;

            AgentCombatSkillUtility.ApplyColor(indicatorObject, indicatorColor);
        }
    }
}

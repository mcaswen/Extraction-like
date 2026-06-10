using System.Collections.Generic;
using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
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
        private AgentCombatStatusEffectDefinition _statusEffect;

        public void Initialize(
            Transform casterTransform,
            LayerMask enemyLayerMask,
            float radius,
            float damagePerSecond,
            float durationSeconds,
            float tickInterval,
            AgentCombatStatusEffectDefinition statusEffect,
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
            _tickTimer = 0f;

            SpawnIndicator(visualPrefab, indicatorColor);
            Destroy(gameObject, _durationSeconds + 0.05f);
        }

        private void Update()
        {
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
            ApplyTickDamage();
        }

        private void ApplyTickDamage()
        {
            AgentCombatSkillUtility.CollectEnemiesInSphere(
                transform.position,
                _radius,
                _enemyLayerMask,
                _targets);

            AgentCombatSkillContext context = new AgentCombatSkillContext(
                _casterTransform,
                null,
                new AgentCombatRuntimeStats(1, 0f, 0f),
                _enemyLayerMask,
                _tickInterval);

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
                GameObject visualObject = Instantiate(visualPrefab, transform);
                visualObject.transform.localPosition = Vector3.zero;
                visualObject.transform.localRotation = Quaternion.identity;
                visualObject.transform.localScale = Vector3.one;
                return;
            }

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

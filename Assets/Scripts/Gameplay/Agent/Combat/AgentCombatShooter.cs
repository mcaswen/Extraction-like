using Gameplay.SkillEffect;
using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// Agent 战斗射击组件
    /// 负责把行为树的攻击请求转换为可飞行子弹
    /// </summary>
    public sealed class AgentCombatShooter : MonoBehaviour
    {
        [Header("Fire Setup")]
        [SerializeField] private Transform _firePoint;
        [SerializeField] private GameObject _bulletPrefab;
        [SerializeField] private Vector3 _fallbackFirePointLocalOffset = new Vector3(0f, 1.2f, 0.6f);
        [SerializeField] private bool _createFallbackBulletIfPrefabMissing = true;

        [Header("Bullet Setup")]
        [SerializeField] private float _bulletMoveSpeed = 20f;
        [SerializeField] private float _bulletLifeTime = 3f;
        [SerializeField] private float _fallbackBulletRadius = 0.12f;
        [SerializeField] private Color _bulletColor = new Color(1f, 0.62f, 0.24f, 1f);
        [SerializeField] private global::BulletController.AttackElementType _attackElement =
            global::BulletController.AttackElementType.Fire;
        private AgentCombatProjectileStatus _projectileStatus = AgentCombatProjectileStatus.None;

        /// <summary>
        /// 尝试向目标敌人发射一颗子弹
        /// </summary>
        /// <param name="targetEnemy"></param>
        /// <param name="damage"></param>
        /// <returns></returns>
        public bool TryShootAt(global::EnemyHealthController targetEnemy, float damage)
        {
            if (targetEnemy == null)
                return false;

            Vector3 aimPosition = ResolveAimPosition(targetEnemy);
            FaceTarget(aimPosition);

            Vector3 firePosition = ResolveFirePosition();
            aimPosition.y = firePosition.y;
            Vector3 fireDirection = aimPosition - firePosition;
            if (fireDirection.sqrMagnitude <= 0.0001f)
                fireDirection = transform.forward;

            Quaternion fireRotation = Quaternion.LookRotation(fireDirection.normalized, Vector3.up);
            GameObject bulletObject = CreateBulletObject(firePosition, fireRotation);
            if (bulletObject == null)
                return false;

            ConfigureBulletObject(bulletObject, fireDirection.normalized, damage);
            IgnoreShooterCollisions(bulletObject);
            return true;
        }

        public void ConfigureProjectileElement(
            AgentCombatElementType element,
            AgentCombatProjectileStatus projectileStatus)
        {
            _attackElement = ConvertElementType(element);
            _projectileStatus = projectileStatus;
        }

        // 优先使用配置挂点，缺失时用本地偏移保证运行时仍能发射
        private Vector3 ResolveFirePosition()
        {
            if (_firePoint != null)
                return _firePoint.position;

            return transform.TransformPoint(_fallbackFirePointLocalOffset);
        }

        private void FaceTarget(Vector3 aimPosition)
        {
            Vector3 planarDirection = new Vector3(
                aimPosition.x - transform.position.x,
                0f,
                aimPosition.z - transform.position.z);

            if (planarDirection.sqrMagnitude <= 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
        }

        private GameObject CreateBulletObject(Vector3 firePosition, Quaternion fireRotation)
        {
            if (_bulletPrefab != null)
                return Instantiate(_bulletPrefab, firePosition, fireRotation);

            if (!_createFallbackBulletIfPrefabMissing)
                return null;

            // Prefab 未配置时创建最小运行时子弹，保证 MVP 不依赖额外资产挂载
            GameObject bulletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulletObject.name = "AgentRuntimeBullet";
            bulletObject.transform.SetPositionAndRotation(firePosition, fireRotation);
            bulletObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, _fallbackBulletRadius * 2f);

            Collider bulletCollider = bulletObject.GetComponent<Collider>();
            if (bulletCollider != null)
                bulletCollider.isTrigger = true;

            Rigidbody rigidbodyComponent = bulletObject.AddComponent<Rigidbody>();
            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            bulletObject.AddComponent<global::BulletController>();
            return bulletObject;
        }

        private void ConfigureBulletObject(
            GameObject bulletObject,
            Vector3 fireDirection,
            float damage)
        {
            global::BulletController bulletController = bulletObject.GetComponent<global::BulletController>();
            if (bulletController == null)
                bulletController = bulletObject.AddComponent<global::BulletController>();

            SkillEffectLayerUtility.ApplyToRoot(bulletObject);

            bulletController.Damage = Mathf.Max(0f, damage);
            bulletController.MoveSpeed = Mathf.Max(0f, _bulletMoveSpeed);
            bulletController.LifeTime = Mathf.Max(0.1f, _bulletLifeTime);
            bulletController.BulletColor = _bulletColor;
            bulletController.AttackElement = _attackElement;
            bulletController.SlowMultiplier = _projectileStatus.SlowMultiplier;
            bulletController.SlowDurationSeconds = _projectileStatus.SlowDurationSeconds;
            bulletController.FreezeDurationSeconds = _projectileStatus.FreezeDurationSeconds;
            bulletController.SourceTransform = transform;

            Rigidbody rigidbodyComponent = bulletObject.GetComponent<Rigidbody>();
            if (rigidbodyComponent == null)
                rigidbodyComponent = bulletObject.AddComponent<Rigidbody>();

            rigidbodyComponent.useGravity = false;
            rigidbodyComponent.velocity = fireDirection * bulletController.MoveSpeed;
        }

        // 子弹忽略发射者碰撞，避免近距离生成时立即命中自己
        private void IgnoreShooterCollisions(GameObject bulletObject)
        {
            Collider[] bulletColliders = bulletObject.GetComponentsInChildren<Collider>();
            Collider[] shooterColliders = GetComponentsInChildren<Collider>();

            for (int bulletIndex = 0; bulletIndex < bulletColliders.Length; bulletIndex++)
            {
                Collider bulletCollider = bulletColliders[bulletIndex];
                if (bulletCollider == null)
                    continue;

                for (int shooterIndex = 0; shooterIndex < shooterColliders.Length; shooterIndex++)
                {
                    Collider shooterCollider = shooterColliders[shooterIndex];
                    if (shooterCollider == null)
                        continue;

                    Physics.IgnoreCollision(bulletCollider, shooterCollider);
                }
            }
        }

        private static Vector3 ResolveAimPosition(global::EnemyHealthController targetEnemy)
        {
            Collider targetCollider = targetEnemy.GetComponentInChildren<Collider>();
            if (targetCollider != null)
                return targetCollider.bounds.center;

            return targetEnemy.transform.position + Vector3.up;
        }

        private static global::BulletController.AttackElementType ConvertElementType(AgentCombatElementType element)
        {
            switch (element)
            {
                case AgentCombatElementType.Fire:
                    return global::BulletController.AttackElementType.Fire;
                case AgentCombatElementType.Ice:
                    return global::BulletController.AttackElementType.Ice;
                default:
                    return global::BulletController.AttackElementType.Physical;
            }
        }
    }
}

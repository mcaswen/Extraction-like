using System.Collections.Generic;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Authoring
{
    public enum SceneEnemySourceIconKind
    {
        Inherit = 0,
        Enemy = 1,
        Boss = 2
    }

    public enum SceneEnemyDangerTier
    {
        Inherit = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 敌人来源群目标配置
    /// 表达敌人可能出现的位置，并把出生点生成的敌人转交给对应活跃敌人群
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySourceClusterAuthoring : GameplayTargetClusterAuthoringBase
    {
        [Header("Board Game Icon Binding")]
        [SerializeField] private SceneEnemySourceIconKind _iconKind = SceneEnemySourceIconKind.Inherit;
        [SerializeField] private SceneEnemyDangerTier _dangerTier = SceneEnemyDangerTier.Inherit;

        [Header("Active Enemy Binding")]
        [SerializeField] private ActiveEnemyClusterAuthoring _activeEnemyCluster;
        [SerializeField] private bool _autoResolveActiveEnemyCluster = true;

        [Header("Enemy Sources")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

        public override GameplayTargetKind TargetKind => GameplayTargetKind.EnemySource;
        protected override string IdPrefix => "EnemySourceCluster";
        protected override bool RefreshStateEveryFrame => true;

        public ActiveEnemyClusterAuthoring ActiveEnemyCluster => ResolveActiveEnemyCluster();
        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;
        public SceneEnemySourceIconKind IconKind => _iconKind;
        public SceneEnemyDangerTier DangerTier => _dangerTier;

        /// <summary>
        /// 尝试将指定出生点生成的敌人注册到绑定的活跃敌人群
        /// </summary>
        /// <param name="spawnPoint"></param>
        /// <param name="enemy"></param>
        /// <returns></returns>
        public bool TryRegisterSpawnedEnemy(
            Transform spawnPoint,
            global::EnemyHealthController enemy)
        {
            if (!ContainsSpawnPoint(spawnPoint) || enemy == null)
                return false;

            ActiveEnemyClusterAuthoring activeCluster = ResolveActiveEnemyCluster();
            if (activeCluster == null)
                return false;

            activeCluster.RegisterSpawnedEnemy(enemy, TargetId);
            MarkTouched();
            RefreshRuntimeState();
            return true;
        }

        /// <summary>
        /// 判断出生点是否属于当前来源群
        /// </summary>
        /// <param name="spawnPoint"></param>
        /// <returns></returns>
        public bool ContainsSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoint == null || _spawnPoints == null)
                return false;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] == spawnPoint)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 获取离指定位置最近的出生点
        /// </summary>
        /// <param name="agentPosition"></param>
        /// <param name="spawnPoint"></param>
        /// <returns></returns>
        public bool TryGetNearestSpawnPoint(Vector3 agentPosition, out Transform spawnPoint)
        {
            spawnPoint = null;
            float nearestDistanceSqr = float.MaxValue;
            if (_spawnPoints == null)
                return false;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                Transform candidate = _spawnPoints[i];
                if (candidate == null)
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(agentPosition, candidate.position);
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                spawnPoint = candidate;
                nearestDistanceSqr = distanceSqr;
            }

            return spawnPoint != null;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _spawnPoints ??= new List<Transform>();
            ResolveActiveEnemyCluster();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveActiveEnemyCluster();
        }

        protected override void CollectMemberPositions(List<Vector3> memberPositions)
        {
            if (_spawnPoints == null)
                return;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                Transform spawnPoint = _spawnPoints[i];
                if (spawnPoint != null)
                    memberPositions.Add(spawnPoint.position);
            }
        }

        protected override void RefreshRuntimeState()
        {
            ActiveEnemyClusterAuthoring activeCluster = ResolveActiveEnemyCluster();
            bool hasSource = HasAnySpawnPoint();
            bool hasTouchedEnemy = activeCluster != null && activeCluster.HasBeenTouched;
            bool hasCompletedActiveCluster =
                activeCluster != null &&
                activeCluster.HasRegisteredEnemy &&
                activeCluster.HasBeenCompleted;

            SetAggregatedState(HasBeenTouched || hasTouchedEnemy, hasSource && hasCompletedActiveCluster);
        }

        private ActiveEnemyClusterAuthoring ResolveActiveEnemyCluster()
        {
            if (_activeEnemyCluster != null || !_autoResolveActiveEnemyCluster)
                return _activeEnemyCluster;

            if (TryGetComponent(out ActiveEnemyClusterAuthoring localCluster))
            {
                _activeEnemyCluster = localCluster;
                return _activeEnemyCluster;
            }

            _activeEnemyCluster = GetComponentInParent<ActiveEnemyClusterAuthoring>();
            if (_activeEnemyCluster != null)
                return _activeEnemyCluster;

            if (transform.parent != null)
            {
                _activeEnemyCluster = transform.parent.GetComponentInChildren<ActiveEnemyClusterAuthoring>(true);
                if (_activeEnemyCluster != null)
                    return _activeEnemyCluster;
            }

            _activeEnemyCluster = GetComponentInChildren<ActiveEnemyClusterAuthoring>(true);
            return _activeEnemyCluster;
        }

        private bool HasAnySpawnPoint()
        {
            if (_spawnPoints == null)
                return false;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] != null)
                    return true;
            }

            return false;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}

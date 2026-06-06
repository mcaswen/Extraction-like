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
        private const string DefaultEnemyTierRuleSetResourcesPath = "Enemy/SO_SceneEnemyTierRuleSet";

        private static global::SceneEnemyTierRuleSet _defaultEnemyTierRuleSet;

        [Header("Board Game Icon Binding")]
        [SerializeField] private SceneEnemySourceIconKind _iconKind = SceneEnemySourceIconKind.Inherit;
        [SerializeField] private SceneEnemyDangerTier _dangerTier = SceneEnemyDangerTier.Low;

        [Header("Enemy Tier Rules")]
        [SerializeField] private global::SceneEnemyTierRuleSet _enemyTierRuleSet;

        [Header("Active Enemy Binding")]
        [SerializeField] private ActiveEnemyClusterAuthoring _activeEnemyCluster;
        [SerializeField] private bool _autoResolveActiveEnemyCluster = true;
        [SerializeField] private bool _autoCreateActiveEnemyCluster = true;

        [Header("Enemy Prefab Pool")]
        [SerializeField] private List<GameObject> _enemyPrefabs = new List<GameObject>();

        [Header("Enemy Sources")]
        [SerializeField] private bool _autoCollectChildSpawnPoints = true;
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();

        public override GameplayTargetKind TargetKind => GameplayTargetKind.EnemySource;
        protected override string IdPrefix => "EnemySourceCluster";
        protected override bool RefreshStateEveryFrame => true;

        public ActiveEnemyClusterAuthoring ActiveEnemyCluster => ResolveActiveEnemyCluster(Application.isPlaying);
        public IReadOnlyList<GameObject> EnemyPrefabs => _enemyPrefabs;
        public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;
        public SceneEnemySourceIconKind IconKind => _iconKind;
        public SceneEnemyDangerTier DangerTier => ResolveDangerTier();

        /// <summary>
        /// 按出生点在群内的顺序解析要生成的敌人预制体
        /// 群内只配一份时所有出生点共用，配多份时按出生点索引循环
        /// </summary>
        /// <param name="spawnPoint"></param>
        /// <param name="enemyPrefab"></param>
        /// <returns></returns>
        public bool TryResolveEnemyPrefabForSpawnPoint(
            Transform spawnPoint,
            out GameObject enemyPrefab)
        {
            enemyPrefab = null;
            EnsureEnemySourceLists(true);
            if (spawnPoint == null || !TryGetFirstEnemyPrefabIndex(out int firstPrefabIndex))
                return false;

            int spawnPointIndex = FindSpawnPointIndex(spawnPoint);
            if (spawnPointIndex < 0)
            {
                if (!_autoCollectChildSpawnPoints || !IsOwnedChildSpawnPoint(spawnPoint))
                    return false;

                AddUniqueSpawnPoint(spawnPoint);
                spawnPointIndex = FindSpawnPointIndex(spawnPoint);
            }

            int prefabCount = _enemyPrefabs.Count;
            int preferredIndex = prefabCount > 0 ? Mathf.Abs(spawnPointIndex) % prefabCount : firstPrefabIndex;
            for (int offset = 0; offset < prefabCount; offset++)
            {
                int prefabIndex = (preferredIndex + offset) % prefabCount;
                if (_enemyPrefabs[prefabIndex] == null)
                    continue;

                enemyPrefab = _enemyPrefabs[prefabIndex];
                return true;
            }

            enemyPrefab = _enemyPrefabs[firstPrefabIndex];
            return enemyPrefab != null;
        }

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

            ActiveEnemyClusterAuthoring activeCluster = ResolveActiveEnemyCluster(Application.isPlaying);
            if (activeCluster == null)
                return false;

            ApplyEnemyTierRules(enemy.gameObject);
            activeCluster.RegisterSpawnedEnemy(enemy, TargetId);
            MarkTouched();
            RefreshRuntimeState();
            return true;
        }

        /// <summary>
        /// 将当前敌人来源群的等级规则应用到生成出来的敌人对象。
        /// </summary>
        /// <param name="enemyObject"></param>
        public void ApplyEnemyTierRules(GameObject enemyObject)
        {
            if (enemyObject == null)
                return;

            global::SceneEnemyTierRuleSet ruleSet = ResolveEnemyTierRuleSet();
            float maxHealthMultiplier = ruleSet != null
                ? ruleSet.ResolveMaxHealthMultiplier(DangerTier)
                : ResolveFallbackMaxHealthMultiplier(DangerTier);
            bool spawnDeathLoot = ruleSet == null || ruleSet.ResolveSpawnDeathLoot(DangerTier);

            global::EnemyHealthController[] healthControllers =
                enemyObject.GetComponentsInChildren<global::EnemyHealthController>(true);
            for (int i = 0; i < healthControllers.Length; i++)
            {
                healthControllers[i]?.ApplyMaxHealthMultiplier(maxHealthMultiplier);
            }

            MonoBehaviour[] behaviours = enemyObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is global::IEnemyDeathLootRuleReceiver deathLootRuleReceiver)
                    deathLootRuleReceiver.SetCanSpawnDeathLoot(spawnDeathLoot);
            }
        }

        /// <summary>
        /// 判断出生点是否属于当前来源群
        /// </summary>
        /// <param name="spawnPoint"></param>
        /// <returns></returns>
        public bool ContainsSpawnPoint(Transform spawnPoint)
        {
            EnsureEnemySourceLists(true);
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
            NormalizeDangerTier();
            EnsureEnemySourceLists(true);
            ResolveActiveEnemyCluster(false);
        }

        protected override void OnEnable()
        {
            NormalizeDangerTier();
            EnsureEnemySourceLists(true);
            ResolveActiveEnemyCluster(Application.isPlaying);
            base.OnEnable();
            ResolveActiveEnemyCluster(Application.isPlaying);
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
            ActiveEnemyClusterAuthoring activeCluster = ResolveActiveEnemyCluster(Application.isPlaying);
            bool hasSource = HasAnySpawnPoint();
            bool hasTouchedEnemy = activeCluster != null && activeCluster.HasBeenTouched;
            bool hasCompletedActiveCluster =
                activeCluster != null &&
                activeCluster.HasRegisteredEnemy &&
                activeCluster.HasBeenCompleted;

            SetAggregatedState(HasBeenTouched || hasTouchedEnemy, hasSource && hasCompletedActiveCluster);
        }

        private ActiveEnemyClusterAuthoring ResolveActiveEnemyCluster(bool createIfMissing)
        {
            if (_activeEnemyCluster != null || !_autoResolveActiveEnemyCluster)
                return _activeEnemyCluster;

            if (TryGetComponent(out ActiveEnemyClusterAuthoring localCluster))
            {
                _activeEnemyCluster = localCluster;
                return _activeEnemyCluster;
            }

            _activeEnemyCluster = FindOwnedActiveEnemyClusterInChildren();
            if (_activeEnemyCluster != null)
                return _activeEnemyCluster;

            if (createIfMissing && _autoCreateActiveEnemyCluster)
                _activeEnemyCluster = CreateOwnedActiveEnemyCluster();

            return _activeEnemyCluster;
        }

        private ActiveEnemyClusterAuthoring FindOwnedActiveEnemyClusterInChildren()
        {
            ActiveEnemyClusterAuthoring[] childClusters =
                GetComponentsInChildren<ActiveEnemyClusterAuthoring>(true);
            for (int i = 0; i < childClusters.Length; i++)
            {
                ActiveEnemyClusterAuthoring childCluster = childClusters[i];
                if (childCluster != null && IsOwnedChildActiveEnemyCluster(childCluster.transform))
                    return childCluster;
            }

            return null;
        }

        private ActiveEnemyClusterAuthoring CreateOwnedActiveEnemyCluster()
        {
            GameObject activeClusterObject = new GameObject($"{name}_ActiveEnemyCluster");
            activeClusterObject.transform.SetParent(transform, false);
            return activeClusterObject.AddComponent<ActiveEnemyClusterAuthoring>();
        }

        private bool HasAnySpawnPoint()
        {
            EnsureEnemySourceLists(false);
            if (_spawnPoints == null)
                return false;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] != null)
                    return true;
            }

            return false;
        }

        [ContextMenu("Collect Child Spawn Points")]
        private void CollectChildSpawnPointsFromMenu()
        {
            EnsureEnemySourceLists(true);
            RefreshRangeShape();
        }

        private void EnsureEnemySourceLists(bool collectChildSpawnPoints)
        {
            _enemyPrefabs ??= new List<GameObject>();
            _spawnPoints ??= new List<Transform>();

            if (collectChildSpawnPoints && _autoCollectChildSpawnPoints)
                AddMissingChildSpawnPoints();
        }

        private void AddMissingChildSpawnPoints()
        {
            global::EnemySpawnPoint[] childSpawnPoints =
                GetComponentsInChildren<global::EnemySpawnPoint>(true);
            for (int i = 0; i < childSpawnPoints.Length; i++)
            {
                global::EnemySpawnPoint spawnPoint = childSpawnPoints[i];
                if (spawnPoint == null || !IsOwnedChildSpawnPoint(spawnPoint.transform))
                    continue;

                AddUniqueSpawnPoint(spawnPoint.transform);
            }
        }

        private void AddUniqueSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoint == null)
                return;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] == spawnPoint)
                    return;
            }

            _spawnPoints.Add(spawnPoint);
        }

        private int FindSpawnPointIndex(Transform spawnPoint)
        {
            if (spawnPoint == null || _spawnPoints == null)
                return -1;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                if (_spawnPoints[i] == spawnPoint)
                    return i;
            }

            return -1;
        }

        private bool TryGetFirstEnemyPrefabIndex(out int prefabIndex)
        {
            prefabIndex = -1;
            if (_enemyPrefabs == null)
                return false;

            for (int i = 0; i < _enemyPrefabs.Count; i++)
            {
                if (_enemyPrefabs[i] == null)
                    continue;

                prefabIndex = i;
                return true;
            }

            return false;
        }

        private bool IsOwnedChildSpawnPoint(Transform spawnPoint)
        {
            Transform current = spawnPoint;
            while (current != null)
            {
                if (current.TryGetComponent(out EnemySourceClusterAuthoring sourceCluster))
                    return sourceCluster == this;

                current = current.parent;
            }

            return false;
        }

        private bool IsOwnedChildActiveEnemyCluster(Transform activeEnemyCluster)
        {
            Transform current = activeEnemyCluster;
            while (current != null)
            {
                if (current.TryGetComponent(out EnemySourceClusterAuthoring sourceCluster))
                    return sourceCluster == this;

                current = current.parent;
            }

            return false;
        }

        private global::SceneEnemyTierRuleSet ResolveEnemyTierRuleSet()
        {
            if (_enemyTierRuleSet != null)
                return _enemyTierRuleSet;

            if (_defaultEnemyTierRuleSet == null)
            {
                _defaultEnemyTierRuleSet =
                    Resources.Load<global::SceneEnemyTierRuleSet>(DefaultEnemyTierRuleSetResourcesPath);
            }

            return _defaultEnemyTierRuleSet;
        }

        private SceneEnemyDangerTier ResolveDangerTier()
        {
            return _dangerTier == SceneEnemyDangerTier.Inherit
                ? SceneEnemyDangerTier.Low
                : _dangerTier;
        }

        private void NormalizeDangerTier()
        {
            if (_dangerTier == SceneEnemyDangerTier.Inherit)
                _dangerTier = SceneEnemyDangerTier.Low;
        }

        private static float ResolveFallbackMaxHealthMultiplier(SceneEnemyDangerTier dangerTier)
        {
            switch (dangerTier)
            {
                case SceneEnemyDangerTier.Medium:
                    return 1.2f;
                case SceneEnemyDangerTier.High:
                    return 1.4f;
                default:
                    return 1f;
            }
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}

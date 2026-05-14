using System.Collections.Generic;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using UnityEngine;

namespace Gameplay.Targets.Runtime
{
    /// <summary>
    /// Gameplay 目标运行时注册表
    /// 统一索引区域目标、群目标和具体目标状态写入口
    /// </summary>
    public sealed class GameplayTargetRegistry : MonoBehaviour
    {
        private static GameplayTargetRegistry _activeInstance;

        private readonly Dictionary<string, GameplayTargetAuthoringBase> _targetsById =
            new Dictionary<string, GameplayTargetAuthoringBase>();

        private readonly List<TargetZoneAuthoring> _zones = new List<TargetZoneAuthoring>();
        private readonly List<GameplayTargetClusterAuthoringBase> _clusters =
            new List<GameplayTargetClusterAuthoringBase>();

        public static GameplayTargetRegistry ActiveInstance => _activeInstance;
        public int ZoneCount => _zones.Count;
        public int ClusterCount => _clusters.Count;

        /// <summary>
        /// 获取或创建目标注册表
        /// </summary>
        /// <returns></returns>
        public static GameplayTargetRegistry GetOrCreate()
        {
            if (_activeInstance != null)
                return _activeInstance;

            _activeInstance = FindObjectOfType<GameplayTargetRegistry>();
            if (_activeInstance != null)
                return _activeInstance;

            GameObject registryObject = new GameObject("[GameplayTargetRegistry]");
            _activeInstance = registryObject.AddComponent<GameplayTargetRegistry>();
            return _activeInstance;
        }

        /// <summary>
        /// 注册一个 Gameplay 目标
        /// </summary>
        /// <param name="target"></param>
        public void RegisterTarget(GameplayTargetAuthoringBase target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.TargetId))
                return;

            if (_targetsById.TryGetValue(target.TargetId, out GameplayTargetAuthoringBase existingTarget) &&
                existingTarget != null &&
                existingTarget != target)
            {
                Debug.LogWarning(
                    $"Duplicate gameplay target id [{target.TargetId}] between [{existingTarget.name}] and [{target.name}]",
                    target);
                return;
            }

            _targetsById[target.TargetId] = target;

            if (target is TargetZoneAuthoring zone)
                AddUnique(_zones, zone);

            if (target is GameplayTargetClusterAuthoringBase cluster)
            {
                AddUnique(_clusters, cluster);
                ValidateClusterBinding(cluster);
            }
        }

        /// <summary>
        /// 注销一个 Gameplay 目标
        /// </summary>
        /// <param name="target"></param>
        public void UnregisterTarget(GameplayTargetAuthoringBase target)
        {
            if (target == null)
                return;

            if (!string.IsNullOrWhiteSpace(target.TargetId) &&
                _targetsById.TryGetValue(target.TargetId, out GameplayTargetAuthoringBase existingTarget) &&
                existingTarget == target)
            {
                _targetsById.Remove(target.TargetId);
            }

            if (target is TargetZoneAuthoring zone)
                _zones.Remove(zone);

            if (target is GameplayTargetClusterAuthoringBase cluster)
                _clusters.Remove(cluster);
        }

        /// <summary>
        /// 通过稳定 ID 查找目标
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool TryGetTarget(
            string targetId,
            out GameplayTargetAuthoringBase target)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                target = null;
                return false;
            }

            return _targetsById.TryGetValue(targetId.Trim(), out target) && target != null;
        }

        /// <summary>
        /// 查找指定类型中离目标位置最近的未完成群目标
        /// </summary>
        /// <param name="targetKind"></param>
        /// <param name="position"></param>
        /// <param name="maxDistanceSqr"></param>
        /// <param name="nearestCluster"></param>
        /// <returns></returns>
        public bool TryFindNearestCluster(
            GameplayTargetKind targetKind,
            Vector3 position,
            float maxDistanceSqr,
            out GameplayTargetClusterAuthoringBase nearestCluster)
        {
            nearestCluster = null;
            float nearestDistanceSqr = float.MaxValue;

            for (int i = 0; i < _clusters.Count; i++)
            {
                GameplayTargetClusterAuthoringBase cluster = _clusters[i];
                if (cluster == null ||
                    cluster.HasBeenCompleted ||
                    (targetKind != GameplayTargetKind.None && cluster.TargetKind != targetKind))
                {
                    continue;
                }

                float distanceSqr = GetPlanarDistanceSqr(position, cluster.CenterPosition);
                if (distanceSqr > maxDistanceSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestCluster = cluster;
                nearestDistanceSqr = distanceSqr;
            }

            return nearestCluster != null;
        }

        /// <summary>
        /// 根据资源实体查找所属资源群
        /// </summary>
        /// <param name="resourceObject"></param>
        /// <param name="resourceCluster"></param>
        /// <returns></returns>
        public bool TryFindResourceClusterByEntity(
            GameObject resourceObject,
            out ResourceClusterAuthoring resourceCluster)
        {
            resourceCluster = null;
            if (resourceObject == null)
                return false;

            for (int i = 0; i < _clusters.Count; i++)
            {
                if (_clusters[i] is ResourceClusterAuthoring candidate &&
                    candidate.ContainsResource(resourceObject))
                {
                    resourceCluster = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 根据敌人实体查找所属敌人群
        /// </summary>
        /// <param name="enemy"></param>
        /// <param name="enemyCluster"></param>
        /// <returns></returns>
        public bool TryFindEnemyClusterByEnemy(
            global::EnemyHealthController enemy,
            out EnemyClusterAuthoring enemyCluster)
        {
            enemyCluster = null;
            if (enemy == null)
                return false;

            for (int i = 0; i < _clusters.Count; i++)
            {
                if (_clusters[i] is EnemyClusterAuthoring candidate &&
                    candidate.ContainsEnemy(enemy))
                {
                    enemyCluster = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 根据撤离点实体查找所属撤离点群
        /// </summary>
        /// <param name="extractionPoint"></param>
        /// <param name="extractionCluster"></param>
        /// <returns></returns>
        public bool TryFindExtractionClusterByPoint(
            global::ExtractionPointController extractionPoint,
            out ExtractionClusterAuthoring extractionCluster)
        {
            extractionCluster = null;
            if (extractionPoint == null)
                return false;

            for (int i = 0; i < _clusters.Count; i++)
            {
                if (_clusters[i] is ExtractionClusterAuthoring candidate &&
                    candidate.ContainsExtractionPoint(extractionPoint))
                {
                    extractionCluster = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 通知资源实体已经被接触
        /// </summary>
        /// <param name="resourceObject"></param>
        public void NotifyResourceTouched(GameObject resourceObject)
        {
            if (TryFindResourceClusterByEntity(resourceObject, out ResourceClusterAuthoring resourceCluster))
                resourceCluster.MarkResourceTouched(resourceObject);
        }

        /// <summary>
        /// 通知资源实体已经完成
        /// </summary>
        /// <param name="resourceObject"></param>
        public void NotifyResourceCompleted(GameObject resourceObject)
        {
            if (TryFindResourceClusterByEntity(resourceObject, out ResourceClusterAuthoring resourceCluster))
                resourceCluster.MarkResourceCompleted(resourceObject);
        }

        /// <summary>
        /// 通知敌人实体已经接战
        /// </summary>
        /// <param name="enemy"></param>
        public void NotifyEnemyEngaged(global::EnemyHealthController enemy)
        {
            if (TryFindEnemyClusterByEnemy(enemy, out EnemyClusterAuthoring enemyCluster))
                enemyCluster.MarkEnemyTouched(enemy);
        }

        /// <summary>
        /// 尝试把出生点生成的敌人注册到对应敌人群
        /// </summary>
        /// <param name="spawnPoint"></param>
        /// <param name="enemy"></param>
        /// <returns></returns>
        public bool TryRegisterSpawnedEnemy(
            Transform spawnPoint,
            global::EnemyHealthController enemy)
        {
            if (spawnPoint == null || enemy == null)
                return false;

            for (int i = 0; i < _clusters.Count; i++)
            {
                if (_clusters[i] is EnemyClusterAuthoring enemyCluster &&
                    enemyCluster.TryRegisterSpawnedEnemy(spawnPoint, enemy))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 通知敌人实体已经被消灭
        /// </summary>
        /// <param name="enemy"></param>
        public void NotifyEnemyDefeated(global::EnemyHealthController enemy)
        {
            if (TryFindEnemyClusterByEnemy(enemy, out EnemyClusterAuthoring enemyCluster))
                enemyCluster.MarkEnemyCompleted(enemy);
        }

        /// <summary>
        /// 通知撤离点已经被接触
        /// </summary>
        /// <param name="extractionPoint"></param>
        public void NotifyExtractionTouched(global::ExtractionPointController extractionPoint)
        {
            if (TryFindExtractionClusterByPoint(extractionPoint, out ExtractionClusterAuthoring extractionCluster))
                extractionCluster.MarkExtractionTouched(extractionPoint);
        }

        /// <summary>
        /// 通知撤离点已经完成
        /// </summary>
        /// <param name="extractionPoint"></param>
        public void NotifyExtractionCompleted(global::ExtractionPointController extractionPoint)
        {
            if (TryFindExtractionClusterByPoint(extractionPoint, out ExtractionClusterAuthoring extractionCluster))
                extractionCluster.MarkExtractionCompleted(extractionPoint);
        }

        /// <summary>
        /// 通知目标状态已经变化
        /// </summary>
        /// <param name="target"></param>
        public void NotifyTargetStateChanged(GameplayTargetAuthoringBase target)
        {
            if (target is GameplayTargetClusterAuthoringBase cluster)
                cluster.Zone?.RefreshAggregatedState();
        }

        /// <summary>
        /// 复制当前注册的群目标列表
        /// </summary>
        /// <param name="output"></param>
        public void CopyClustersTo(List<GameplayTargetClusterAuthoringBase> output)
        {
            output.Clear();
            output.AddRange(_clusters);
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning("场景中存在多个 GameplayTargetRegistry，后创建的实例将被停用", this);
                enabled = false;
                return;
            }

            _activeInstance = this;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        private static void AddUnique<TValue>(List<TValue> list, TValue value)
            where TValue : class
        {
            if (value != null && !list.Contains(value))
                list.Add(value);
        }

        // 注册时校验 Cluster 必须绑定 Zone，资源实体不能重复归属多个资源群
        private void ValidateClusterBinding(GameplayTargetClusterAuthoringBase cluster)
        {
            if (cluster.Zone == null)
            {
                Debug.LogWarning(
                    $"Gameplay target cluster [{cluster.name}] is not bound to a TargetZoneAuthoring",
                    cluster);
            }

            if (cluster is ResourceClusterAuthoring resourceCluster)
                ValidateResourceOwnership(resourceCluster);
        }

        // 资源实体归属冲突只做 warning，不在运行时强行改配置
        private void ValidateResourceOwnership(ResourceClusterAuthoring resourceCluster)
        {
            IReadOnlyList<GameplayTargetEntityMember> members = resourceCluster.ResourceMembers;

            for (int i = 0; i < members.Count; i++)
            {
                GameObject resourceObject = members[i]?.EntityObject;
                if (resourceObject == null)
                    continue;

                for (int clusterIndex = 0; clusterIndex < _clusters.Count; clusterIndex++)
                {
                    if (!(_clusters[clusterIndex] is ResourceClusterAuthoring otherCluster) ||
                        otherCluster == resourceCluster ||
                        !otherCluster.ContainsResource(resourceObject))
                    {
                        continue;
                    }

                    Debug.LogWarning(
                        $"Resource [{resourceObject.name}] is assigned to multiple resource clusters",
                        resourceObject);
                    break;
                }
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

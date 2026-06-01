using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>
    /// 抽象图与当前实时场景目标的绑定配置
    /// 负责把图节点 ID 映射到 GameplayTarget 的稳定 ID 和世界坐标
    /// </summary>
    public sealed class MapGraphBindingAuthoring : MonoBehaviour
    {
        [SerializeField] private SO_MapGraphDefinition _mapDefinition;
        [SerializeField] private List<MapGraphTargetBinding> _targetBindings =
            new List<MapGraphTargetBinding>();

        /// <summary>
        /// 当前绑定使用的图定义
        /// </summary>
        public SO_MapGraphDefinition MapDefinition => _mapDefinition;

        /// <summary>
        /// 当前全部节点目标绑定
        /// </summary>
        public IReadOnlyList<MapGraphTargetBinding> TargetBindings => _targetBindings;

        /// <summary>
        /// 根据实时目标 ID 查找对应图节点 ID
        /// </summary>
        /// <param name="targetId"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public bool TryGetNodeIdForTargetId(string targetId, out string nodeId)
        {
            string normalizedTargetId = NormalizeId(targetId);
            for (int index = 0; index < _targetBindings.Count; index++)
            {
                MapGraphTargetBinding binding = _targetBindings[index];
                if (binding == null || !binding.MatchesTargetId(normalizedTargetId))
                    continue;

                nodeId = binding.NodeId;
                return !string.IsNullOrWhiteSpace(nodeId);
            }

            nodeId = string.Empty;
            return false;
        }

        /// <summary>
        /// 根据图节点 ID 查找绑定的实时目标 ID
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="targetId"></param>
        /// <returns></returns>
        public bool TryGetTargetIdForNodeId(string nodeId, out string targetId)
        {
            MapGraphTargetBinding binding = FindBindingByNodeId(nodeId);
            targetId = binding != null ? binding.TargetId : string.Empty;
            return !string.IsNullOrWhiteSpace(targetId);
        }

        /// <summary>
        /// 根据图节点 ID 解析实时目标对象
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool TryResolveTargetForNodeId(
            string nodeId,
            out GameplayTargetAuthoringBase target)
        {
            MapGraphTargetBinding binding = FindBindingByNodeId(nodeId);
            if (binding != null && binding.TryResolveTarget(out target))
                return true;

            target = null;
            return false;
        }

        /// <summary>
        /// 根据图节点 ID 获取绑定目标的实时世界坐标
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool TryGetWorldPositionForNodeId(string nodeId, out Vector3 position)
        {
            MapGraphTargetBinding binding = FindBindingByNodeId(nodeId);
            if (binding != null && binding.TryGetWorldPosition(out position))
                return true;

            position = default;
            return false;
        }

        /// <summary>
        /// 在所有已绑定目标中查找离世界坐标最近的图节点
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="maxDistance"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public bool TryFindNearestBoundNode(
            Vector3 worldPosition,
            float maxDistance,
            out string nodeId)
        {
            nodeId = string.Empty;
            float maxDistanceSqr = Mathf.Max(0f, maxDistance) * Mathf.Max(0f, maxDistance);
            float nearestDistanceSqr = float.MaxValue;

            for (int index = 0; index < _targetBindings.Count; index++)
            {
                MapGraphTargetBinding binding = _targetBindings[index];
                if (binding == null || string.IsNullOrWhiteSpace(binding.NodeId))
                    continue;

                if (!binding.TryGetWorldPosition(out Vector3 targetPosition))
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(worldPosition, targetPosition);
                if (distanceSqr > maxDistanceSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
                nodeId = binding.NodeId;
            }

            return !string.IsNullOrWhiteSpace(nodeId);
        }

        /// <summary>
        /// 从目标对象上解析 GameplayTarget 并查找对应图节点
        /// </summary>
        /// <param name="targetObject"></param>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        public bool TryGetNodeIdForTargetObject(GameObject targetObject, out string nodeId)
        {
            nodeId = string.Empty;
            if (targetObject == null)
                return false;

            GameplayTargetAuthoringBase target = targetObject.GetComponent<GameplayTargetAuthoringBase>();
            if (target == null)
                target = targetObject.GetComponentInParent<GameplayTargetAuthoringBase>();
            if (target == null)
                target = targetObject.GetComponentInChildren<GameplayTargetAuthoringBase>();

            return target != null && TryGetNodeIdForTargetId(target.TargetId, out nodeId);
        }

        private MapGraphTargetBinding FindBindingByNodeId(string nodeId)
        {
            string normalizedNodeId = NormalizeId(nodeId);
            for (int index = 0; index < _targetBindings.Count; index++)
            {
                MapGraphTargetBinding binding = _targetBindings[index];
                if (binding == null || binding.NodeId != normalizedNodeId)
                    continue;

                return binding;
            }

            return null;
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

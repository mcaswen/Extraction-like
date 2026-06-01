using System;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Data;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>
    /// 图节点到实时 Gameplay 目标的绑定项
    /// 支持直接引用场景目标，也支持只填写目标 ID 以兼容运行时注册表查询
    /// </summary>
    [Serializable]
    public sealed class MapGraphTargetBinding
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private GameplayTargetKind _expectedTargetKind = GameplayTargetKind.None;
        [SerializeField] private GameplayTargetAuthoringBase _target;
        [SerializeField] private string _targetIdOverride;
        [SerializeField] private bool _useFallbackWorldPosition;
        [SerializeField] private Vector3 _fallbackWorldPosition;

        /// <summary>
        /// 绑定的图节点 ID
        /// </summary>
        public string NodeId => NormalizeId(_nodeId);

        /// <summary>
        /// 期望绑定的 Gameplay 目标类型
        /// 仅用于配置校验和阅读，不参与强制过滤
        /// </summary>
        public GameplayTargetKind ExpectedTargetKind => _expectedTargetKind;

        /// <summary>
        /// 绑定的目标 ID
        /// 直接引用目标时优先使用目标自身的稳定 ID
        /// </summary>
        public string TargetId
        {
            get
            {
                if (_target != null)
                    return _target.TargetId;

                return NormalizeId(_targetIdOverride);
            }
        }

        /// <summary>
        /// 当前绑定是否具备基本可用信息
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(NodeId) && !string.IsNullOrWhiteSpace(TargetId);

        /// <summary>
        /// 判断该绑定是否匹配指定目标 ID
        /// </summary>
        /// <param name="targetId"></param>
        /// <returns></returns>
        public bool MatchesTargetId(string targetId)
        {
            return string.Equals(TargetId, NormalizeId(targetId), StringComparison.Ordinal);
        }

        /// <summary>
        /// 尝试解析绑定的实时 Gameplay 目标
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool TryResolveTarget(out GameplayTargetAuthoringBase target)
        {
            if (_target != null)
            {
                target = _target;
                return true;
            }

            GameplayTargetRegistry registry = GameplayTargetRegistry.ActiveInstance;
            if (registry != null && registry.TryGetTarget(TargetId, out target))
                return true;

            target = null;
            return false;
        }

        /// <summary>
        /// 尝试获取绑定目标在实时场景中的世界坐标
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool TryGetWorldPosition(out Vector3 position)
        {
            if (TryResolveTarget(out GameplayTargetAuthoringBase target))
            {
                position = target.CenterPosition;
                return true;
            }

            if (_useFallbackWorldPosition)
            {
                position = _fallbackWorldPosition;
                return true;
            }

            position = default;
            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

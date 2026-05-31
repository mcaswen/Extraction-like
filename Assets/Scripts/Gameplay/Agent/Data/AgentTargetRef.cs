using System;
using UnityEngine;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent 目标语义
    /// 描述 Agent 要处理“什么类型的问题”，不描述目标如何被绑定
    /// </summary>
    public enum AgentTargetKind
    {
        None = 0,
        Resource = 1,
        Enemy = 2,
        Location = 3,
        Extraction = 4,
        EnemySource = 5
    }

    /// <summary>
    /// Agent 目标绑定方式
    /// ConcreteObject 服务当前 MVP 场景对象，AbstractPoint 服务之后的抽象资源点/敌人点
    /// </summary>
    public enum AgentTargetBindingType
    {
        None = 0,
        ConcreteObject = 1,
        AbstractPoint = 2
    }

    /// <summary>
    /// Agent 目标引用
    /// 用一套结构同时承载当前场景里的具体箱子/敌人，以及之后的抽象资源点/敌人点
    /// </summary>
    [Serializable]
    public struct AgentTargetRef
    {
        [SerializeField] private AgentTargetKind _kind;
        [SerializeField] private AgentTargetBindingType _bindingType;
        [SerializeField] private GameObject _targetObject;
        [SerializeField] private Vector3 _targetPosition;
        [SerializeField] private bool _hasTargetPosition;
        [SerializeField] private string _targetId;

        /// <summary>
        /// 创建一个 Agent 目标引用
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="bindingType"></param>
        /// <param name="targetObject"></param>
        /// <param name="targetPosition"></param>
        /// <param name="hasTargetPosition"></param>
        /// <param name="targetId"></param>
        public AgentTargetRef(
            AgentTargetKind kind,
            AgentTargetBindingType bindingType,
            GameObject targetObject = null,
            Vector3 targetPosition = default,
            bool hasTargetPosition = false,
            string targetId = "")
        {
            _kind = kind;
            _bindingType = bindingType;
            _targetObject = targetObject;
            _targetPosition = targetPosition;
            _hasTargetPosition = hasTargetPosition;
            _targetId = NormalizeId(targetId);
        }

        /// <summary>
        /// 空目标引用
        /// </summary>
        public static AgentTargetRef None => new AgentTargetRef(
            AgentTargetKind.None,
            AgentTargetBindingType.None);

        /// <summary>
        /// 目标业务类型
        /// </summary>
        public AgentTargetKind Kind => _kind;

        /// <summary>
        /// 目标绑定方式
        /// </summary>
        public AgentTargetBindingType BindingType => _bindingType;

        /// <summary>
        /// 目标场景对象
        /// </summary>
        public GameObject TargetObject => _targetObject;

        /// <summary>
        /// 目标业务 ID
        /// </summary>
        public string TargetId => _targetId ?? string.Empty;

        /// <summary>
        /// 目标当前位置
        /// 具体对象目标会实时读取 Transform，抽象点目标读取快照位置
        /// </summary>
        public Vector3 TargetPosition =>
            _targetObject != null ? _targetObject.transform.position : _targetPosition;

        /// <summary>
        /// 当前目标是否具备可用位置
        /// </summary>
        public bool HasTargetPosition => _targetObject != null || _hasTargetPosition;

        /// <summary>
        /// 当前目标是否绑定到具体场景对象
        /// </summary>
        public bool IsConcreteObject => _bindingType == AgentTargetBindingType.ConcreteObject;

        /// <summary>
        /// 当前目标是否绑定到抽象点位
        /// </summary>
        public bool IsAbstractPoint => _bindingType == AgentTargetBindingType.AbstractPoint;

        /// <summary>
        /// 当前目标引用是否具备可执行所需的基本信息
        /// </summary>
        public bool IsValid =>
            _kind != AgentTargetKind.None &&
            _bindingType != AgentTargetBindingType.None &&
            (_targetObject != null || HasTargetPosition || !string.IsNullOrEmpty(TargetId));

        /// <summary>
        /// 创建绑定到具体场景对象的目标引用
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="targetObject"></param>
        /// <param name="targetId"></param>
        /// <returns></returns>
        public static AgentTargetRef FromConcreteObject(
            AgentTargetKind kind,
            GameObject targetObject,
            string targetId = "")
        {
            return new AgentTargetRef(
                kind,
                AgentTargetBindingType.ConcreteObject,
                targetObject,
                targetObject != null ? targetObject.transform.position : default,
                targetObject != null,
                targetId);
        }

        /// <summary>
        /// 创建绑定到抽象点位的目标引用
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="targetId"></param>
        /// <param name="targetPosition"></param>
        /// <param name="hasTargetPosition"></param>
        /// <returns></returns>
        public static AgentTargetRef FromAbstractPoint(
            AgentTargetKind kind,
            string targetId,
            Vector3 targetPosition,
            bool hasTargetPosition = true)
        {
            return new AgentTargetRef(
                kind,
                AgentTargetBindingType.AbstractPoint,
                null,
                targetPosition,
                hasTargetPosition,
                targetId);
        }

        /// <summary>
        /// 返回替换目标 ID 后的新引用
        /// </summary>
        /// <param name="targetId"></param>
        /// <returns></returns>
        public AgentTargetRef WithTargetId(string targetId)
        {
            return new AgentTargetRef(
                _kind,
                _bindingType,
                _targetObject,
                _targetPosition,
                _hasTargetPosition,
                targetId);
        }

        /// <summary>
        /// 返回便于调试显示的目标引用字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string binding = _bindingType.ToString();
            string id = string.IsNullOrEmpty(TargetId) ? "NoId" : TargetId;
            return $"{_kind}/{binding}/{id}";
        }

        private static string NormalizeId(string targetId)
        {
            return string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();
        }
    }
}

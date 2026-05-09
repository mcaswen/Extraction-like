using System;
using UnityEngine;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent 目标语义。
    /// 描述 Agent 要处理“什么类型的问题”，不描述目标如何被绑定。
    /// </summary>
    public enum AgentTargetKind
    {
        None = 0,
        Resource = 1,
        Enemy = 2,
        Location = 3,
        Extraction = 4
    }

    /// <summary>
    /// Agent 目标绑定方式。
    /// ConcreteObject 服务当前 MVP 场景对象，AbstractPoint 服务之后的抽象资源点/敌人点。
    /// </summary>
    public enum AgentTargetBindingType
    {
        None = 0,
        ConcreteObject = 1,
        AbstractPoint = 2
    }

    /// <summary>
    /// Agent 目标引用。
    /// 用一套结构同时承载当前场景里的具体箱子/敌人，以及之后的抽象资源点/敌人点。
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

        public static AgentTargetRef None => new AgentTargetRef(
            AgentTargetKind.None,
            AgentTargetBindingType.None);

        public AgentTargetKind Kind => _kind;
        public AgentTargetBindingType BindingType => _bindingType;
        public GameObject TargetObject => _targetObject;
        public string TargetId => _targetId ?? string.Empty;

        public Vector3 TargetPosition =>
            _targetObject != null ? _targetObject.transform.position : _targetPosition;

        public bool HasTargetPosition => _targetObject != null || _hasTargetPosition;
        public bool IsConcreteObject => _bindingType == AgentTargetBindingType.ConcreteObject;
        public bool IsAbstractPoint => _bindingType == AgentTargetBindingType.AbstractPoint;

        public bool IsValid =>
            _kind != AgentTargetKind.None &&
            _bindingType != AgentTargetBindingType.None &&
            (_targetObject != null || HasTargetPosition || !string.IsNullOrEmpty(TargetId));

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

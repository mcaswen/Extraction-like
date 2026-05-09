using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 多 Agent 运行时注册表
    /// 允许外部系统按 AgentId 查询具体 Agent，而不是依赖某个单体 Pawn 单例
    /// </summary>
    public sealed class AgentRuntimeRegistry : MonoBehaviour
    {
        private static AgentRuntimeRegistry _activeInstance;

        private readonly Dictionary<AgentId, AgentRuntimeHandle> _handlesById =
            new Dictionary<AgentId, AgentRuntimeHandle>();

        private readonly List<AgentRuntimeHandle> _registeredAgents =
            new List<AgentRuntimeHandle>();

        private AgentRuntimeQuery _query;

        public static AgentRuntimeRegistry ActiveInstance => _activeInstance;

        public static AgentRuntimeRegistry GetOrCreate()
        {
            if (_activeInstance != null)
                return _activeInstance;

            _activeInstance = UnityEngine.Object.FindObjectOfType<AgentRuntimeRegistry>();
            if (_activeInstance != null)
                return _activeInstance;

            GameObject registryObject = new GameObject("[AgentRuntimeRegistry]");
            _activeInstance = registryObject.AddComponent<AgentRuntimeRegistry>();
            return _activeInstance;
        }

        public int AgentCount => _registeredAgents.Count;
        public IReadOnlyList<AgentRuntimeHandle> RegisteredAgents => _registeredAgents;

        public AgentRuntimeQuery Query
        {
            get
            {
                if (_query == null)
                    _query = new AgentRuntimeQuery(this);

                return _query;
            }
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning("场景中存在多个 AgentRuntimeRegistry，后创建的实例将被停用。", this);
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

        public bool Register(AgentPawnRoot pawnRoot)
        {
            if (pawnRoot == null)
                return false;

            AgentId agentId = pawnRoot.AgentId;
            if (agentId.IsEmpty)
            {
                Debug.LogError("AgentRuntimeRegistry 拒绝注册空 AgentId。", pawnRoot);
                return false;
            }

            AgentRuntimeHandle existingHandle;
            if (_handlesById.TryGetValue(agentId, out existingHandle))
            {
                if (existingHandle.PawnRoot == pawnRoot)
                {
                    _handlesById[agentId] = new AgentRuntimeHandle(pawnRoot);
                    return true;
                }

                if (existingHandle.PawnRoot != null)
                {
                    Debug.LogError($"重复的 AgentId：{agentId}。多 Agent 运行时要求每个 AgentId 唯一。", pawnRoot);
                    return false;
                }

                RemoveHandle(agentId);
            }

            AgentRuntimeHandle handle = new AgentRuntimeHandle(pawnRoot);
            _handlesById.Add(agentId, handle);
            _registeredAgents.Add(handle);
            return true;
        }

        public void Unregister(AgentPawnRoot pawnRoot)
        {
            if (pawnRoot == null)
                return;

            AgentRuntimeHandle existingHandle;
            if (!_handlesById.TryGetValue(pawnRoot.AgentId, out existingHandle))
                return;

            if (existingHandle.PawnRoot != pawnRoot)
                return;

            RemoveHandle(pawnRoot.AgentId);
        }

        public bool TryGetHandle(AgentId agentId, out AgentRuntimeHandle handle)
        {
            if (!agentId.IsEmpty && _handlesById.TryGetValue(agentId, out handle) && handle.IsValid)
                return true;

            handle = default;
            return false;
        }

        public bool TryGetHandle(string agentId, out AgentRuntimeHandle handle)
        {
            return TryGetHandle(AgentId.FromString(agentId), out handle);
        }

        public bool TryGetReadOnly(AgentId agentId, out IAgentReadOnly readOnly)
        {
            AgentRuntimeHandle handle;
            if (TryGetHandle(agentId, out handle))
            {
                readOnly = handle.ReadOnly;
                return true;
            }

            readOnly = null;
            return false;
        }

        public bool TryGetReadOnly(string agentId, out IAgentReadOnly readOnly)
        {
            return TryGetReadOnly(AgentId.FromString(agentId), out readOnly);
        }

        public bool TryGetCommandReceiver(AgentId agentId, out IAgentCommandReceiver commandReceiver)
        {
            AgentRuntimeHandle handle;
            if (TryGetHandle(agentId, out handle))
            {
                commandReceiver = handle.CommandReceiver;
                return true;
            }

            commandReceiver = null;
            return false;
        }

        public bool TryGetCommandReceiver(string agentId, out IAgentCommandReceiver commandReceiver)
        {
            return TryGetCommandReceiver(AgentId.FromString(agentId), out commandReceiver);
        }

        public bool TryGetPrimaryHandle(out AgentRuntimeHandle handle)
        {
            for (int i = 0; i < _registeredAgents.Count; i++)
            {
                handle = _registeredAgents[i];
                if (handle.IsValid)
                    return true;
            }

            handle = default;
            return false;
        }

        public void CopyHandlesTo(List<AgentRuntimeHandle> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < _registeredAgents.Count; i++)
            {
                AgentRuntimeHandle handle = _registeredAgents[i];
                if (handle.IsValid)
                    results.Add(handle);
            }
        }

        private void RemoveHandle(AgentId agentId)
        {
            _handlesById.Remove(agentId);

            for (int i = _registeredAgents.Count - 1; i >= 0; i--)
            {
                if (_registeredAgents[i].AgentId == agentId)
                    _registeredAgents.RemoveAt(i);
            }
        }
    }
}

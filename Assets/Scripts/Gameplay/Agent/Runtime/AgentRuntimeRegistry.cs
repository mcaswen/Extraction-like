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

        /// <summary>
        /// 当前场景中的 Agent 运行时注册表实例
        /// </summary>
        public static AgentRuntimeRegistry ActiveInstance => _activeInstance;

        /// <summary>
        /// 获取或创建 Agent 运行时注册表
        /// 场景未显式放置时，会创建一个运行时对象承接注册关系
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 当前有效注册列表中的 Agent 数量
        /// </summary>
        public int AgentCount => _registeredAgents.Count;

        /// <summary>
        /// 已注册 Agent 句柄列表的只读视图
        /// 外部只允许读取，不直接修改 Registry 内部集合
        /// </summary>
        public IReadOnlyList<AgentRuntimeHandle> RegisteredAgents => _registeredAgents;

        /// <summary>
        /// Agent 查询入口
        /// 延迟创建，避免查询逻辑散落在外部系统中
        /// </summary>
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

        /// <summary>
        /// 注册一个 AgentPawnRoot 到运行时表中
        /// 同一个 AgentId 只允许对应一个有效 Pawn
        /// </summary>
        /// <param name="pawnRoot"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 从运行时表中注销指定 Pawn
        /// 只会移除与当前 Pawn 实例匹配的句柄，避免误删同 ID 的新实例
        /// </summary>
        /// <param name="pawnRoot"></param>
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

        /// <summary>
        /// 按 AgentId 查询运行时句柄
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetHandle(AgentId agentId, out AgentRuntimeHandle handle)
        {
            if (!agentId.IsEmpty && _handlesById.TryGetValue(agentId, out handle) && handle.IsValid)
                return true;

            handle = default;
            return false;
        }

        /// <summary>
        /// 按字符串 AgentId 查询运行时句柄
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool TryGetHandle(string agentId, out AgentRuntimeHandle handle)
        {
            return TryGetHandle(AgentId.FromString(agentId), out handle);
        }

        /// <summary>
        /// 按 AgentId 查询 Agent 只读接口
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="readOnly"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 按字符串 AgentId 查询 Agent 只读接口
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="readOnly"></param>
        /// <returns></returns>
        public bool TryGetReadOnly(string agentId, out IAgentReadOnly readOnly)
        {
            return TryGetReadOnly(AgentId.FromString(agentId), out readOnly);
        }

        /// <summary>
        /// 按 AgentId 查询 Agent 命令接收接口
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="commandReceiver"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 按字符串 AgentId 查询 Agent 命令接收接口
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="commandReceiver"></param>
        /// <returns></returns>
        public bool TryGetCommandReceiver(string agentId, out IAgentCommandReceiver commandReceiver)
        {
            return TryGetCommandReceiver(AgentId.FromString(agentId), out commandReceiver);
        }

        /// <summary>
        /// 获取当前第一个有效 Agent 句柄
        /// 用于未指定目标 AgentId 时的兼容路径
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 将当前有效 Agent 句柄复制到外部缓冲区
        /// 避免外部直接持有内部集合并修改注册状态
        /// </summary>
        /// <param name="results"></param>
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

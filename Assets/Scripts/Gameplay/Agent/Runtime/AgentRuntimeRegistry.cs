using System;
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
        private const string DefaultFocusedAgentIdValue = "1";

        private static AgentRuntimeRegistry _activeInstance;

        private readonly Dictionary<AgentId, AgentRuntimeHandle> _handlesById =
            new Dictionary<AgentId, AgentRuntimeHandle>();

        private readonly List<AgentRuntimeHandle> _registeredAgents =
            new List<AgentRuntimeHandle>();

        private AgentRuntimeQuery _query;
        private AgentId _focusedAgentId;
        private bool _hasExplicitFocusSelection;

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
        /// 当前玩家视角聚焦的 AgentId
        /// UI、背包和默认指令路由都会以该 Agent 为上下文
        /// </summary>
        public AgentId FocusedAgentId => _focusedAgentId;

        /// <summary>
        /// 焦点 Agent 变化事件
        /// 参数依次为旧焦点句柄与新焦点句柄
        /// </summary>
        public event Action<AgentRuntimeHandle, AgentRuntimeHandle> FocusedAgentChanged;

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

            if (!_hasExplicitFocusSelection && IsDefaultFocusedAgent(agentId))
            {
                SetFocusedHandle(handle);
                return true;
            }

            if (!TryGetFocusedHandle(out _))
                SetFocusedHandle(handle);

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

            AgentId removedAgentId = pawnRoot.AgentId;
            bool removedFocusedAgent = removedAgentId == _focusedAgentId;

            RemoveHandle(removedAgentId);

            if (removedFocusedAgent)
            {
                _hasExplicitFocusSelection = false;
                FocusFirstAvailableAgent();
            }
        }

        /// <summary>
        /// 通知 Registry 某个 Agent 已死亡，必要时把焦点切到其他存活 Agent
        /// </summary>
        /// <param name="pawnRoot"></param>
        public void NotifyAgentDied(AgentPawnRoot pawnRoot)
        {
            if (pawnRoot == null || pawnRoot.AgentId != _focusedAgentId)
                return;

            _hasExplicitFocusSelection = false;
            FocusFirstAvailableAgent();
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
            if (TryGetDefaultFocusedHandle(out handle))
                return true;

            for (int i = 0; i < _registeredAgents.Count; i++)
            {
                handle = _registeredAgents[i];
                if (handle.IsAlive)
                    return true;
            }

            handle = default;
            return false;
        }

        /// <summary>
        /// 获取当前焦点 Agent 句柄
        /// 若焦点已失效，会自动回落到第一个有效 Agent
        /// </summary>
        public bool TryGetFocusedHandle(out AgentRuntimeHandle handle)
        {
            if (!_focusedAgentId.IsEmpty &&
                TryGetHandle(_focusedAgentId, out handle) &&
                handle.IsAlive)
            {
                return true;
            }

            return FocusFirstAvailableAgent(out handle);
        }

        /// <summary>
        /// 将指定 Agent 设置为当前焦点
        /// </summary>
        public bool TrySetFocusedAgent(AgentId agentId)
        {
            AgentRuntimeHandle handle;
            if (!TryGetHandle(agentId, out handle))
                return false;

            if (!handle.IsAlive)
                return false;

            SetFocusedHandle(handle);
            _hasExplicitFocusSelection = true;
            return true;
        }

        /// <summary>
        /// 将指定字符串 AgentId 设置为当前焦点
        /// </summary>
        public bool TrySetFocusedAgent(string agentId)
        {
            return TrySetFocusedAgent(AgentId.FromString(agentId));
        }

        /// <summary>
        /// 切换到注册顺序中的下一个有效 Agent
        /// </summary>
        public bool TryFocusNextAgent()
        {
            if (_registeredAgents.Count <= 0)
                return false;

            int startIndex = FindFocusedAgentIndex();
            int candidateCount = _registeredAgents.Count;
            for (int offset = 1; offset <= candidateCount; offset++)
            {
                int index = startIndex >= 0
                    ? (startIndex + offset) % candidateCount
                    : offset - 1;

                AgentRuntimeHandle handle = _registeredAgents[index];
                if (!handle.IsAlive)
                    continue;

                SetFocusedHandle(handle);
                _hasExplicitFocusSelection = true;
                return true;
            }

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

        private bool FocusFirstAvailableAgent()
        {
            AgentRuntimeHandle handle;
            return FocusFirstAvailableAgent(out handle);
        }

        private bool FocusFirstAvailableAgent(out AgentRuntimeHandle handle)
        {
            if (!TryGetPrimaryHandle(out handle))
            {
                SetFocusedHandle(default);
                return false;
            }

            SetFocusedHandle(handle);
            return true;
        }

        private bool TryGetDefaultFocusedHandle(out AgentRuntimeHandle handle)
        {
            return TryGetHandle(DefaultFocusedAgentIdValue, out handle) && handle.IsAlive;
        }

        private static bool IsDefaultFocusedAgent(AgentId agentId)
        {
            return string.Equals(agentId.Value, DefaultFocusedAgentIdValue, StringComparison.Ordinal);
        }

        private int FindFocusedAgentIndex()
        {
            if (_focusedAgentId.IsEmpty)
                return -1;

            for (int i = 0; i < _registeredAgents.Count; i++)
            {
                if (_registeredAgents[i].AgentId == _focusedAgentId)
                    return i;
            }

            return -1;
        }

        private void SetFocusedHandle(AgentRuntimeHandle handle)
        {
            AgentRuntimeHandle previousHandle = default;
            if (!_focusedAgentId.IsEmpty)
                TryGetHandle(_focusedAgentId, out previousHandle);

            AgentId nextAgentId = handle.IsValid ? handle.AgentId : AgentId.Empty;
            if (_focusedAgentId == nextAgentId)
                return;

            _focusedAgentId = nextAgentId;
            FocusedAgentChanged?.Invoke(previousHandle, handle);
        }
    }
}

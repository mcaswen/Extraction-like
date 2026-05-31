using Gameplay.Agent.Core;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Registry 对外暴露的运行时句柄
    /// 外部系统通过它拿只读视图或命令接口，而不是持有 Pawn 内部字段
    /// </summary>
    public readonly struct AgentRuntimeHandle
    {
        /// <summary>
        /// 根据 Pawn Root 创建运行时句柄
        /// 同时缓存只读接口和命令接口，避免外部重复转换
        /// </summary>
        /// <param name="pawnRoot"></param>
        public AgentRuntimeHandle(AgentPawnRoot pawnRoot)
        {
            PawnRoot = pawnRoot;
            AgentId = pawnRoot != null ? pawnRoot.AgentId : Gameplay.Agent.Runtime.AgentId.Empty;
            ReadOnly = pawnRoot;
            CommandReceiver = pawnRoot;
        }

        /// <summary>
        /// 当前句柄对应的 AgentId
        /// </summary>
        public AgentId AgentId { get; }

        /// <summary>
        /// 当前句柄对应的 Pawn Root
        /// </summary>
        public AgentPawnRoot PawnRoot { get; }

        /// <summary>
        /// 对外暴露的只读状态接口
        /// </summary>
        public IAgentReadOnly ReadOnly { get; }

        /// <summary>
        /// 对外暴露的命令接收接口
        /// </summary>
        public IAgentCommandReceiver CommandReceiver { get; }

        /// <summary>
        /// 句柄是否仍指向一个有效 Agent
        /// </summary>
        public bool IsValid => !AgentId.IsEmpty && PawnRoot != null;

        /// <summary>
        /// 当前 Agent 的缓存 Transform
        /// </summary>
        public Transform CachedTransform => IsValid ? ReadOnly.CachedTransform : null;
    }
}

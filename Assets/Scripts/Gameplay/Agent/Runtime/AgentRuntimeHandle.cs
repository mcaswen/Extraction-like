using Gameplay.Agent.Core;
using Gameplay.Agent.Interfaces;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Registry 对外暴露的运行时句柄。
    /// 外部系统通过它拿只读视图或命令接口，而不是持有 Pawn 内部字段。
    /// </summary>
    public readonly struct AgentRuntimeHandle
    {
        public AgentRuntimeHandle(AgentPawnRoot pawnRoot)
        {
            PawnRoot = pawnRoot;
            AgentId = pawnRoot != null ? pawnRoot.AgentId : Gameplay.Agent.Runtime.AgentId.Empty;
            ReadOnly = pawnRoot;
            CommandReceiver = pawnRoot;
        }

        public AgentId AgentId { get; }
        public AgentPawnRoot PawnRoot { get; }
        public IAgentReadOnly ReadOnly { get; }
        public IAgentCommandReceiver CommandReceiver { get; }

        public bool IsValid => !AgentId.IsEmpty && PawnRoot != null;
        public Transform CachedTransform => IsValid ? ReadOnly.CachedTransform : null;
    }
}

using Gameplay.Agent.Runtime;
using UnityEngine;

namespace Gameplay.Targets.Data
{
    /// <summary>
    /// 一次玩家重访的起始版本。所有 Agent 共享箱子的处理记录，不保存角色私有完成状态。
    /// 起始版本随指令挂起/恢复及同根重规划保留；新的玩家命令才会重新允许处理余物。
    /// </summary>
    public sealed class ResourceSearchPass
    {
        private readonly long _startedAtRevision = AgentSearchedResourceRegistry.Revision;
        public bool HasProcessed(GameObject resource)
            => AgentSearchedResourceRegistry.WasSearchedAfter(resource, _startedAtRevision);
    }
}

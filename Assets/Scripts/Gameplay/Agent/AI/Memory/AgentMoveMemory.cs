using UnityEngine;

namespace Gameplay.Agent.AI.Memory
{
    /// <summary>
    /// 移动子树的帧内记忆：解析节点写入本帧目标点和命令，移动节点读取
    /// </summary>
    public sealed class AgentMoveMemory
    {
        public Vector3 TargetPosition { get; set; }

        public string CommandId { get; set; } = string.Empty;
    }
}

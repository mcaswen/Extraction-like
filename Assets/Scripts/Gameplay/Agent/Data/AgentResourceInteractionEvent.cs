using UnityEngine;

namespace Gameplay.Agent.Data
{
    public enum AgentResourceInteractionStage { Approaching, WaitingForInventory, Left, Completed }

    /// <summary>搜索节点的真实交互事实；不包含指令或背包操作。</summary>
    public readonly struct AgentResourceInteractionEvent
    {
        public string AgentId { get; }
        public string CommandId { get; }
        public GameObject Resource { get; }
        public Vector3 NavigationPosition { get; }
        public AgentResourceInteractionStage Stage { get; }
        public AgentResourceInteractionEvent(string agentId, string commandId, GameObject resource,
            Vector3 navigationPosition, AgentResourceInteractionStage stage)
        { AgentId = agentId; CommandId = commandId; Resource = resource; NavigationPosition = navigationPosition; Stage = stage; }
    }
}

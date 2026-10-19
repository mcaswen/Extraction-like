namespace Gameplay.Agent.AI.Memory
{
    /// <summary>
    /// 驻留撤离点子树的帧内记忆：除移动目标外，还记录本帧解析出的具体撤离点
    /// </summary>
    public sealed class AgentExtractionMemory : AgentMoveMemory
    {
        public global::ExtractionPointController ExtractionPoint { get; set; }
    }
}

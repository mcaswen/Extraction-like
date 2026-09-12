using Gameplay.MapGraph.Config;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>读取一次规划使用的成本。false 表示该方向不可用，返回值必须为非负有限数。</summary>
    public interface IMapGraphCostProvider
    {
        bool TryGetCost(MapGraphEdgeDefinition edge, string fromNodeId, out float cost);
    }

    /// <summary>旧 MVP 配置的兼容成本；正式场景图应显式传入导航成本快照。</summary>
    internal sealed class ConfiguredMapGraphCostProvider : IMapGraphCostProvider
    {
        public static readonly ConfiguredMapGraphCostProvider Instance = new ConfiguredMapGraphCostProvider();

        public bool TryGetCost(MapGraphEdgeDefinition edge, string fromNodeId, out float cost)
        {
            cost = edge != null ? edge.LengthUnits : 0f;
            return edge != null && (fromNodeId == edge.FromNodeId || fromNodeId == edge.ToNodeId);
        }
    }
}

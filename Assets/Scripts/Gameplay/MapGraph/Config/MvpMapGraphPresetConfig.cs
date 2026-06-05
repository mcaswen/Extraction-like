using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>
    /// MVP graph planner preset.
    /// Keeps the fixed node metadata and topology in code, matching the old board-game preset style.
    /// </summary>
    public static class MvpMapGraphPresetConfig
    {
        public const string MapId = "MVP_Graph";
        public const string DisplayName = "MVP Graph";
        public const string StartNodeId = "a";
        public const float DefaultEdgeLengthUnits = 2f;

        public static List<MvpMapGraphNodePresetDefinition> CreateNodePresets()
        {
            return new List<MvpMapGraphNodePresetDefinition>
            {
                new MvpMapGraphNodePresetDefinition("a", MapGraphNodeKind.Start, MapGraphNodeIconKind.Start, new Vector2(-8f, 0f), "Spawn point"),
                new MvpMapGraphNodePresetDefinition("b", MapGraphNodeKind.Extraction, MapGraphNodeIconKind.Extraction, new Vector2(6.8f, 0.6f), "Extraction point 1"),
                new MvpMapGraphNodePresetDefinition("c", MapGraphNodeKind.Extraction, MapGraphNodeIconKind.Extraction, new Vector2(6.8f, -4f), "Extraction point 2"),
                new MvpMapGraphNodePresetDefinition("1", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(-6.8f, 0f), "Enemy Low", MapGraphResourceTier.None, MapGraphDangerTier.Low),
                new MvpMapGraphNodePresetDefinition("2", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(-5.6f, 0f), "Enemy Low", MapGraphResourceTier.None, MapGraphDangerTier.Low),
                new MvpMapGraphNodePresetDefinition("3", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(-4.4f, 0.7f), "Resource Low", MapGraphResourceTier.Low),
                new MvpMapGraphNodePresetDefinition("4", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(-3.2f, 0.7f), "Enemy Medium", MapGraphResourceTier.None, MapGraphDangerTier.Medium),
                new MvpMapGraphNodePresetDefinition("5", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(-2f, 1.5f), "Resource Medium", MapGraphResourceTier.Medium),
                new MvpMapGraphNodePresetDefinition("6", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(-2f, 0.2f), "Resource Low", MapGraphResourceTier.Low),
                new MvpMapGraphNodePresetDefinition("7", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(-0.8f, 0.2f), "Resource Low", MapGraphResourceTier.Low),
                new MvpMapGraphNodePresetDefinition("8", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(0.4f, 0.2f), "Enemy Medium", MapGraphResourceTier.None, MapGraphDangerTier.Medium),
                new MvpMapGraphNodePresetDefinition("9", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(1.6f, 0.8f), "Resource High", MapGraphResourceTier.High),
                new MvpMapGraphNodePresetDefinition("10", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Boss, new Vector2(2.8f, 0.6f), "Boss", MapGraphResourceTier.None, MapGraphDangerTier.High),
                new MvpMapGraphNodePresetDefinition("11", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Boss, new Vector2(4f, 0.6f), "Boss", MapGraphResourceTier.None, MapGraphDangerTier.High),
                new MvpMapGraphNodePresetDefinition("12", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(5.2f, 1.4f), "Resource Medium", MapGraphResourceTier.Medium),
                new MvpMapGraphNodePresetDefinition("13", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(5.2f, -0.3f), "Resource Medium", MapGraphResourceTier.Medium),
                new MvpMapGraphNodePresetDefinition("14", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(2.8f, -1.3f), "Resource Low", MapGraphResourceTier.Low),
                new MvpMapGraphNodePresetDefinition("15", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(1.6f, -2.2f), "Enemy Medium", MapGraphResourceTier.None, MapGraphDangerTier.Medium),
                new MvpMapGraphNodePresetDefinition("16", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(4f, -2.2f), "Enemy Low", MapGraphResourceTier.None, MapGraphDangerTier.Low),
                new MvpMapGraphNodePresetDefinition("17", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(5f, -2.9f), "Resource Medium", MapGraphResourceTier.Medium),
                new MvpMapGraphNodePresetDefinition("18", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(6.2f, -2.4f), "Enemy Medium", MapGraphResourceTier.None, MapGraphDangerTier.Medium),
                new MvpMapGraphNodePresetDefinition("19", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(6.2f, -3.5f), "Enemy Medium", MapGraphResourceTier.None, MapGraphDangerTier.Medium),
                new MvpMapGraphNodePresetDefinition("20", MapGraphNodeKind.EnemySource, MapGraphNodeIconKind.Enemy, new Vector2(2.8f, -3.5f), "Enemy High", MapGraphResourceTier.None, MapGraphDangerTier.High),
                new MvpMapGraphNodePresetDefinition("21", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(4f, -3.9f), "Resource High", MapGraphResourceTier.High),
                new MvpMapGraphNodePresetDefinition("22", MapGraphNodeKind.Resource, MapGraphNodeIconKind.Resource, new Vector2(5.2f, -4.3f), "Resource Low", MapGraphResourceTier.Low)
            };
        }

        public static List<MapGraphEdgeDefinition> CreateEdgePresets()
        {
            return new List<MapGraphEdgeDefinition>
            {
                CreateEdge("1", "a", "1"),
                CreateEdge("2", "1", "2"),
                CreateEdge("3", "2", "3"),
                CreateEdge("4", "3", "4"),
                CreateEdge("5", "4", "5"),
                CreateEdge("6", "4", "6"),
                CreateEdge("7", "5", "6"),
                CreateEdge("8", "6", "7"),
                CreateEdge("9", "7", "8"),
                CreateEdge("10", "8", "9"),
                CreateEdge("11", "9", "10"),
                CreateEdge("12", "10", "11"),
                CreateEdge("13", "11", "12"),
                CreateEdge("14", "11", "13"),
                CreateEdge("15", "12", "b"),
                CreateEdge("16", "13", "b"),
                CreateEdge("17", "10", "14"),
                CreateEdge("18", "14", "15"),
                CreateEdge("19", "14", "16"),
                CreateEdge("20", "1", "15"),
                CreateEdge("21", "15", "16"),
                CreateEdge("22", "14", "20"),
                CreateEdge("23", "10", "20"),
                CreateEdge("24", "16", "17"),
                CreateEdge("25", "17", "18"),
                CreateEdge("26", "17", "19"),
                CreateEdge("27", "18", "19"),
                CreateEdge("28", "20", "21"),
                CreateEdge("29", "21", "22"),
                CreateEdge("30", "20", "c"),
                CreateEdge("31", "22", "c")
            };
        }

        private static MapGraphEdgeDefinition CreateEdge(string edgeId, string fromNodeId, string toNodeId)
        {
            return new MapGraphEdgeDefinition(edgeId, fromNodeId, toNodeId, DefaultEdgeLengthUnits);
        }
    }

    public readonly struct MvpMapGraphNodePresetDefinition
    {
        public MvpMapGraphNodePresetDefinition(
            string nodeId,
            MapGraphNodeKind nodeKind,
            MapGraphNodeIconKind iconKind,
            Vector2 defaultPosition,
            string description = "",
            MapGraphResourceTier resourceTier = MapGraphResourceTier.None,
            MapGraphDangerTier dangerTier = MapGraphDangerTier.None)
        {
            NodeId = nodeId;
            NodeKind = nodeKind;
            IconKind = iconKind;
            DefaultPosition = defaultPosition;
            Description = description;
            ResourceTier = resourceTier;
            DangerTier = dangerTier;
        }

        public string NodeId { get; }
        public MapGraphNodeKind NodeKind { get; }
        public MapGraphNodeIconKind IconKind { get; }
        public Vector2 DefaultPosition { get; }
        public string Description { get; }
        public MapGraphResourceTier ResourceTier { get; }
        public MapGraphDangerTier DangerTier { get; }
    }
}

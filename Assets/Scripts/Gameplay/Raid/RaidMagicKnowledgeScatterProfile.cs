using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 魔法知识可投放的局内区域用途掩码
/// </summary>
[Flags]
public enum RaidRegionPurposeMask
{
    None = 0,
    Spawn = 1 << 0,
    Resource = 1 << 1,
    DenseResource = 1 << 2,
    Boss = 1 << 3,
    Extraction = 1 << 4,
    Transit = 1 << 5,
    All = ~0
}

/// <summary>
/// 单类魔法知识拾取物的投放配置
/// </summary>
[Serializable]
public class RaidMagicKnowledgeSpawnEntry
{
    public string EntryId = "entry";
    public string Label = "Magic Knowledge";
    public GameObject Prefab;
    [Min(0)]
    public int Count = 1;
    [Min(0.5f)]
    public float MinSpacing = 5f;
    public RaidRegionPurposeMask AllowedRegionPurposes =
        RaidRegionPurposeMask.Resource |
        RaidRegionPurposeMask.DenseResource |
        RaidRegionPurposeMask.Boss;
}

/// <summary>
/// 编辑器散布工具使用的魔法知识投放配置资产
/// </summary>
[CreateAssetMenu(fileName = "SO_RaidMagicKnowledgeScatterProfile", menuName = "Raid/Magic Knowledge Scatter Profile")]
public class RaidMagicKnowledgeScatterProfile : ScriptableObject
{
    [Header("Target Scene")]
    public string TargetScenePath = "Assets/Scenes/Scene_lyl_IslandWhitebox.unity";
    public string GeneratedRootName = "Generated_MagicKnowledgePickups";
    public bool ClearPreviousGeneratedRoot = true;

    [Header("Placement Random")]
    public bool UseFixedSeed = true;
    public int FixedSeed = 20260409;
    [Min(8)]
    public int PlacementAttemptsPerItem = 64;

    [Header("Grounding")]
    public LayerMask GroundMask = ~0;
    [Min(1f)]
    public float GroundProbeHeight = 40f;
    public float SpawnHeightOffset = 0.05f;
    [Min(0f)]
    public float RegionEdgePadding = 1f;
    [Min(0.5f)]
    public float GlobalMinSpacing = 4f;

    [Header("Spawn Entries")]
    public List<RaidMagicKnowledgeSpawnEntry> SpawnEntries = new List<RaidMagicKnowledgeSpawnEntry>();
}

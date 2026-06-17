using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 驱动白盒局内原型场景随机布置的编辑器配置资产
/// </summary>
[CreateAssetMenu(fileName = "RaidMvpPopulationProfile", menuName = "Raid/MVP Population Profile")]
public class RaidMvpPopulationProfile : ScriptableObject
{
    [Header("Scene Bootstrap")]
    public GameObject PlayerPrefab;
    [HideInInspector]
    public GameObject DefaultEnemyDeathLootPrefab;
    public bool SpawnStartingGearDrops = true;
    public GameObject StartingBagWorldPrefab;
    public GameObject StartingRigWorldPrefab;
    public bool ClearPreviousGeneratedRoot = true;
    public string GeneratedRootName = "Generated_RaidMvp";
    public bool UseFixedSeed = true;
    public int FixedSeed = 20260405;

    [Header("Placement")]
    public LayerMask GroundMask = ~0;
    public float GroundProbeHeight = 40f;
    public float SpawnHeightOffset = 0.05f;
    public float DefaultEnemySpacing = 2.8f;
    public float DefaultChestSpacing = 2.2f;
    public float StartingDropSpacing = 1.6f;
    [HideInInspector]
    public bool OverrideMissingEnemyDeathLootPrefab = true;

    [Header("Extraction Fallback")]
    public bool AutoCreateExtractionPointForExtractionRegion = true;
    public float ExtractionDurationSeconds = 4f;
    public Vector3 ExtractionTriggerSize = new Vector3(4.5f, 2.4f, 4.5f);

    [Header("Density Rules")]
    public List<RaidDensitySpawnRule> DensityRules = new List<RaidDensitySpawnRule>
    {
        new RaidDensitySpawnRule
        {
            Density = RaidSpawnDensity.None,
            EnemyCountRange = new Vector2Int(0, 0),
            ChestCountRange = new Vector2Int(0, 0)
        },
        new RaidDensitySpawnRule
        {
            Density = RaidSpawnDensity.Low,
            EnemyCountRange = new Vector2Int(1, 2),
            ChestCountRange = new Vector2Int(1, 2)
        },
        new RaidDensitySpawnRule
        {
            Density = RaidSpawnDensity.Medium,
            EnemyCountRange = new Vector2Int(2, 4),
            ChestCountRange = new Vector2Int(2, 3)
        },
        new RaidDensitySpawnRule
        {
            Density = RaidSpawnDensity.High,
            EnemyCountRange = new Vector2Int(4, 6),
            ChestCountRange = new Vector2Int(3, 5)
        },
        new RaidDensitySpawnRule
        {
            Density = RaidSpawnDensity.Boss,
            EnemyCountRange = new Vector2Int(1, 2),
            ChestCountRange = new Vector2Int(1, 2)
        }
    };

    [Header("Prefab Pools")]
    public List<RaidRegionPrefabPool> RegionPools = new List<RaidRegionPrefabPool>
    {
        new RaidRegionPrefabPool { Purpose = RaidRegionPurpose.Resource },
        new RaidRegionPrefabPool { Purpose = RaidRegionPurpose.DenseResource },
        new RaidRegionPrefabPool { Purpose = RaidRegionPurpose.Boss },
        new RaidRegionPrefabPool { Purpose = RaidRegionPurpose.Extraction },
        new RaidRegionPrefabPool { Purpose = RaidRegionPurpose.Transit }
    };

    /// <summary>
    /// 按区域密度查找刷怪和箱子数量规则
    /// </summary>
    /// <param name="density">区域密度</param>
    /// <returns>匹配的密度规则，未配置时返回空值</returns>
    public RaidDensitySpawnRule GetDensityRule(RaidSpawnDensity density)
    {
        for (int i = 0; i < DensityRules.Count; i++)
        {
            if (DensityRules[i] != null && DensityRules[i].Density == density)
            {
                return DensityRules[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 按区域用途查找预制体池，特殊区域会回退到资源区池
    /// </summary>
    /// <param name="purpose">区域用途</param>
    /// <returns>匹配的预制体池，未配置时返回空值</returns>
    public RaidRegionPrefabPool GetRegionPool(RaidRegionPurpose purpose)
    {
        for (int i = 0; i < RegionPools.Count; i++)
        {
            if (RegionPools[i] != null && RegionPools[i].Purpose == purpose)
            {
                return RegionPools[i];
            }
        }

        if (purpose == RaidRegionPurpose.DenseResource)
        {
            return GetRegionPool(RaidRegionPurpose.Resource);
        }

        if (purpose == RaidRegionPurpose.Extraction || purpose == RaidRegionPurpose.Transit)
        {
            return GetRegionPool(RaidRegionPurpose.Resource);
        }

        return null;
    }
}

/// <summary>
/// 单种区域密度对应的敌人和箱子数量区间
/// </summary>
[Serializable]
public class RaidDensitySpawnRule
{
    public RaidSpawnDensity Density = RaidSpawnDensity.Medium;
    public Vector2Int EnemyCountRange = new Vector2Int(1, 2);
    public Vector2Int ChestCountRange = new Vector2Int(1, 2);
}

/// <summary>
/// 单种区域用途对应的敌人和箱子预制体池
/// </summary>
[Serializable]
public class RaidRegionPrefabPool
{
    public RaidRegionPurpose Purpose = RaidRegionPurpose.Resource;
    public List<RaidSpawnPrefabEntry> EnemyPrefabs = new List<RaidSpawnPrefabEntry>();
    public List<RaidSpawnPrefabEntry> ChestPrefabs = new List<RaidSpawnPrefabEntry>();
}

/// <summary>
/// 带权重和摆放参数的局内生成预制体条目
/// </summary>
[Serializable]
public class RaidSpawnPrefabEntry
{
    public string Label = "Entry";
    public GameObject Prefab;
    public int Weight = 1;
    public float MinSpacing = 2f;
    public bool RandomizeYaw = true;
    public Vector3 PositionOffset = Vector3.zero;
    public Vector3 RotationOffset = Vector3.zero;
}

using UnityEngine;

/// <summary>
/// 局内区域的玩法用途
/// </summary>
public enum RaidRegionPurpose
{
    Spawn,
    Resource,
    DenseResource,
    Boss,
    Extraction,
    Transit
}

/// <summary>
/// 局内区域的刷怪密度等级
/// </summary>
public enum RaidSpawnDensity
{
    None,
    Low,
    Medium,
    High,
    Boss
}

/// <summary>
/// 场景中的局内逻辑区域标记，用于编辑器生成敌人、箱子和撤离点
/// </summary>
public class RaidRegionMarker : MonoBehaviour
{
    public string RegionId = "Region";
    public RaidRegionPurpose Purpose = RaidRegionPurpose.Resource;
    public RaidSpawnDensity Density = RaidSpawnDensity.Medium;

    [Header("Bounds")]
    public Vector3 RegionSize = new Vector3(18f, 4f, 18f);
    public float EnemyEdgePadding = 1.25f;
    public float ChestEdgePadding = 1f;

    [Header("Overrides")]
    public bool OverrideSpawnCounts;
    public Vector2Int EnemyCountRangeOverride = new Vector2Int(0, 0);
    public Vector2Int ChestCountRangeOverride = new Vector2Int(0, 0);

    /// <summary>
    /// 获取区域在世界空间中的包围盒
    /// </summary>
    /// <returns>以标记位置为中心的区域包围盒</returns>
    public Bounds GetWorldBounds()
    {
        return new Bounds(transform.position, RegionSize);
    }

    private void OnDrawGizmosSelected()
    {
        Bounds bounds = GetWorldBounds();
        Gizmos.color = GetGizmoColor(false);
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = GetGizmoColor(true);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private Color GetGizmoColor(bool wire)
    {
        Color baseColor = Purpose switch
        {
            RaidRegionPurpose.Spawn => new Color(0.26f, 0.9f, 0.52f, 0.2f),
            RaidRegionPurpose.Resource => new Color(0.3f, 0.7f, 1f, 0.2f),
            RaidRegionPurpose.DenseResource => new Color(0.2f, 0.6f, 1f, 0.22f),
            RaidRegionPurpose.Boss => new Color(1f, 0.36f, 0.28f, 0.24f),
            RaidRegionPurpose.Extraction => new Color(0.2f, 1f, 0.82f, 0.22f),
            _ => new Color(0.9f, 0.86f, 0.34f, 0.2f)
        };

        if (wire)
        {
            baseColor.a = 0.95f;
        }

        return baseColor;
    }
}

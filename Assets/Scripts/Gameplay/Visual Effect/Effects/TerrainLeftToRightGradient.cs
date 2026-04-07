using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainLeftToRightGradient : MonoBehaviour
{
    [Header("=== 渐变高度设置 ===")]
    [Tooltip("地形最左侧（X最小处）的高度")]
    public float leftHeight = 20f;

    [Tooltip("地形最右侧（X最大处）的高度")]
    public float rightHeight = 0f;

    [Header("=== 渐变曲线设置 ===")]
    [Tooltip("控制渐变平滑度的曲线，X轴为0-1的水平位置，Y轴为0-1的高度比例")]
    public AnimationCurve heightCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("=== 自动应用设置 ===")]
    [Tooltip("勾选后，每次修改参数自动更新地形")]
    public bool autoUpdate = true;

    private Terrain _terrain;
    private TerrainData _terrainData;

    private void OnValidate()
    {
        CacheTerrainReferences();
    }

    private void Awake()
    {
        CacheTerrainReferences();
    }

    private void CacheTerrainReferences()
    {
        _terrain = GetComponent<Terrain>();
        if (_terrain != null)
        {
            _terrainData = _terrain.terrainData;
        }
    }

    /// <summary>
    /// 手动调用更新地形渐变（运行时也可调用）
    /// </summary>
    [ContextMenu("更新地形渐变")]
    public void UpdateTerrainGradient()
    {
        CacheTerrainReferences();
        if (_terrainData == null) return;

        // 1. 获取地形高度图分辨率
        int heightmapResolution = _terrainData.heightmapResolution;
        float[,] heights = new float[heightmapResolution, heightmapResolution];

        // 2. 获取地形世界空间尺寸
        float terrainWidth = _terrainData.size.x;
        float terrainHeight = _terrainData.size.y;

        // 3. 遍历高度图，计算每个点的高度
        for (int x = 0; x < heightmapResolution; x++)
        {
            float normalizedX = (float)x / (heightmapResolution - 1);
            float curveValue = heightCurve.Evaluate(normalizedX);
            float targetWorldHeight = Mathf.Lerp(leftHeight, rightHeight, normalizedX);
            targetWorldHeight = leftHeight - (leftHeight - rightHeight) * curveValue;
            float normalizedHeight = targetWorldHeight / terrainHeight;

            for (int z = 0; z < heightmapResolution; z++)
            {
                heights[x, z] = normalizedHeight;
            }
        }

        // 4. 将计算好的高度图应用到地形
        _terrainData.SetHeights(0, 0, heights);
    }

    /// <summary
    /// 重置为默认线性渐变（左20右0，直线平滑）
    /// </summary>
    [ContextMenu("重置为默认线性渐变")]
    public void ResetToDefault()
    {
        leftHeight = 20f;
        rightHeight = 0f;
        heightCurve = AnimationCurve.Linear(0, 1, 1, 0);
        UpdateTerrainGradient();
    }
}

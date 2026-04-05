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

    // 🔧 修复：在OnValidate中主动获取Terrain，避免Awake未执行的问题
    private void OnValidate()
    {
        // 每次Inspector修改时，都重新获取Terrain和TerrainData，确保不为空
        _terrain = GetComponent<Terrain>();
        if (_terrain != null)
        {
            _terrainData = _terrain.terrainData;
        }

        if (autoUpdate && Application.isEditor)
        {
            UpdateTerrainGradient();
        }
    }

    private void Awake()
    {
        // 运行时初始化，和OnValidate逻辑一致，双重保险
        _terrain = GetComponent<Terrain>();
        _terrainData = _terrain.terrainData;
    }

    /// <summary>
    /// 手动调用更新地形渐变（运行时也可调用）
    /// </summary>
    [ContextMenu("更新地形渐变")]
    public void UpdateTerrainGradient()
    {
        // 🔧 修复：增加空值判断，避免报错
        if (_terrain == null || _terrainData == null)
        {
            Debug.LogError("Terrain或TerrainData为空，请确保脚本挂载在Terrain对象上！");
            return;
        }

        // 1. 获取地形高度图分辨率
        int heightmapResolution = _terrainData.heightmapResolution;
        float[,] heights = new float[heightmapResolution, heightmapResolution];

        // 2. 获取地形世界空间尺寸
        float terrainWidth = _terrainData.size.x;
        float terrainHeight = _terrainData.size.y;

        // 🔧 修复：高度安全校验，避免超出地形最大高度
        if (leftHeight > terrainHeight || rightHeight > terrainHeight)
        {
            Debug.LogWarning($"高度超出地形最大高度{terrainHeight}，已自动限制！");
            leftHeight = Mathf.Min(leftHeight, terrainHeight);
            rightHeight = Mathf.Min(rightHeight, terrainHeight);
        }

        // 3. 遍历高度图，计算每个点的高度
        for (int x = 0; x < heightmapResolution; x++)
        {
            // 计算当前X在地形上的归一化位置（0=最左，1=最右）
            float normalizedX = (float)x / (heightmapResolution - 1);

            // 通过曲线获取高度比例（0-1）
            float curveValue = heightCurve.Evaluate(normalizedX);

            // 计算当前点的目标世界高度（左高右低平滑渐变）
            float targetWorldHeight = Mathf.Lerp(leftHeight, rightHeight, normalizedX);
            // 用曲线修正高度，实现自定义平滑度
            targetWorldHeight = leftHeight - (leftHeight - rightHeight) * curveValue;

            // 转换为Terrain高度图的归一化值（0-1）
            float normalizedHeight = targetWorldHeight / terrainHeight;

            // 给高度图赋值（Z轴所有点保持相同高度，实现左右渐变）
            for (int z = 0; z < heightmapResolution; z++)
            {
                heights[x, z] = normalizedHeight;
            }
        }

        // 4. 将计算好的高度图应用到地形
        _terrainData.SetHeights(0, 0, heights);

        Debug.Log($"✅ 地形渐变已更新！左高度: {leftHeight}，右高度: {rightHeight}");
    }

    /// <summary>
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
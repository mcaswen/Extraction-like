using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为卡通描边烘焙平滑法线数据的编辑器工具
/// </summary>
public static class ToonSmoothNormalBaker
{
    /// <summary>
    /// 平滑法线写入目标通道
    /// </summary>
    public enum SmoothNormalTarget
    {
        Tangent,
        UV2
    }

    private const string DefaultOutputDirectory = "Assets/Art/Generated/ToonSmoothNormalMeshes";

    /// <summary>
    /// 将当前选择的网格平滑法线烘焙到切线通道
    /// </summary>
    [MenuItem("Tools/Rendering/Toon Outlines/Bake Smooth Normals To Tangent")]
    private static void BakeSelectionToTangent()
    {
        BakeSelection(SmoothNormalTarget.Tangent);
    }

    [MenuItem("Tools/Rendering/Toon Outlines/Bake Smooth Normals To UV2")]
    private static void BakeSelectionToUv2()
    {
        BakeSelection(SmoothNormalTarget.UV2);
    }

    [MenuItem("Tools/Rendering/Toon Outlines/Bake Smooth Normals To Tangent", true)]
    [MenuItem("Tools/Rendering/Toon Outlines/Bake Smooth Normals To UV2", true)]
    private static bool HasMeshSelection()
    {
        return Selection.gameObjects.Length > 0 || Selection.objects.Length > 0;
    }

    /// <summary>
    /// 为当前选择的网格资源烘焙平滑法线
    /// </summary>
    /// <param name="target">平滑法线写入目标</param>
    public static void BakeSelection(SmoothNormalTarget target)
    {
        Directory.CreateDirectory(DefaultOutputDirectory);

        int bakedCount = 0;
        foreach (Mesh mesh in CollectSelectedMeshes())
        {
            Mesh bakedMesh = Bake(mesh, target);
            string sourcePath = AssetDatabase.GetAssetPath(mesh);
            string sourceName = string.IsNullOrEmpty(sourcePath) ? mesh.name : Path.GetFileNameWithoutExtension(sourcePath);
            string outputPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultOutputDirectory}/{sourceName}_ToonSmoothNormals.asset");

            AssetDatabase.CreateAsset(bakedMesh, outputPath);
            bakedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ToonSmoothNormalBaker: baked {bakedCount} mesh asset(s) to {DefaultOutputDirectory}.");
    }

    /// <summary>
    /// 为指定网格生成带平滑法线数据的新网格
    /// </summary>
    /// <param name="source">源网格</param>
    /// <param name="target">平滑法线写入目标</param>
    /// <returns>烘焙后的新网格实例</returns>
    public static Mesh Bake(Mesh source, SmoothNormalTarget target)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Mesh mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = $"{source.name}_ToonSmoothNormals";

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        if (vertices.Length == 0 || normals.Length != vertices.Length || triangles.Length == 0)
        {
            Debug.LogWarning($"ToonSmoothNormalBaker: mesh {source.name} has no usable normals/triangles; copied without baking.");
            return mesh;
        }

        Vector3[] smoothed = ComputeAreaWeightedSmoothNormals(vertices, triangles);

        if (target == SmoothNormalTarget.Tangent)
        {
            WriteSmoothNormalsToTangent(mesh, smoothed);
        }
        else
        {
            WriteSmoothNormalsToUv2(mesh, smoothed);
        }

        return mesh;
    }

    private static IEnumerable<Mesh> CollectSelectedMeshes()
    {
        HashSet<Mesh> meshes = new HashSet<Mesh>();

        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is Mesh selectedMesh)
            {
                meshes.Add(selectedMesh);
            }
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            foreach (MeshFilter meshFilter in selectedObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.sharedMesh != null)
                {
                    meshes.Add(meshFilter.sharedMesh);
                }
            }

            foreach (SkinnedMeshRenderer skinnedMeshRenderer in selectedObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinnedMeshRenderer.sharedMesh != null)
                {
                    meshes.Add(skinnedMeshRenderer.sharedMesh);
                }
            }
        }

        return meshes;
    }

    private static Vector3[] ComputeAreaWeightedSmoothNormals(Vector3[] vertices, int[] triangles)
    {
        Dictionary<VertexKey, Vector3> normalByPosition = new Dictionary<VertexKey, Vector3>(vertices.Length);
        Vector3[] faceAccumulated = new Vector3[vertices.Length];

        // 使用面积加权面法线，避免大型轮廓面被细小倒角三角面过度影响
        // 这样能让卡通描边更稳定
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 p0 = vertices[i0];
            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];
            Vector3 areaNormal = Vector3.Cross(p1 - p0, p2 - p0);

            faceAccumulated[i0] += areaNormal;
            faceAccumulated[i1] += areaNormal;
            faceAccumulated[i2] += areaNormal;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            VertexKey key = new VertexKey(vertices[i]);
            normalByPosition.TryGetValue(key, out Vector3 current);
            normalByPosition[key] = current + faceAccumulated[i];
        }

        Vector3[] smoothed = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 smoothNormal = normalByPosition[new VertexKey(vertices[i])];
            smoothed[i] = smoothNormal.sqrMagnitude > 0.000001f ? smoothNormal.normalized : Vector3.up;
        }

        return smoothed;
    }

    private static void WriteSmoothNormalsToTangent(Mesh mesh, Vector3[] smoothed)
    {
        Vector4[] tangents = new Vector4[smoothed.Length];
        for (int i = 0; i < smoothed.Length; i++)
        {
            Vector3 normal = smoothed[i];
            tangents[i] = new Vector4(normal.x, normal.y, normal.z, 1f);
        }

        mesh.tangents = tangents;
    }

    private static void WriteSmoothNormalsToUv2(Mesh mesh, Vector3[] smoothed)
    {
        List<Vector4> uv2 = new List<Vector4>(smoothed.Length);
        for (int i = 0; i < smoothed.Length; i++)
        {
            // 贴图坐标通道通常按零到一数据导入，所以把带符号的物体空间法线编码后写入
            Vector3 encoded = smoothed[i] * 0.5f + Vector3.one * 0.5f;
            uv2.Add(new Vector4(encoded.x, encoded.y, encoded.z, 1f));
        }

        mesh.SetUVs(1, uv2);
    }

    private readonly struct VertexKey : IEquatable<VertexKey>
    {
        private const float Precision = 100000f;

        private readonly int _x;
        private readonly int _y;
        private readonly int _z;

        public VertexKey(Vector3 position)
        {
            _x = Mathf.RoundToInt(position.x * Precision);
            _y = Mathf.RoundToInt(position.y * Precision);
            _z = Mathf.RoundToInt(position.z * Precision);
        }

        /// <summary>
        /// 判断两个量化后的顶点位置是否相同
        /// </summary>
        /// <param name="other">另一个顶点键</param>
        /// <returns>位置键完全相同时返回真值</returns>
        public bool Equals(VertexKey other)
        {
            return _x == other._x && _y == other._y && _z == other._z;
        }

        /// <summary>
        /// 判断对象是否为相同的顶点键
        /// </summary>
        /// <param name="obj">待比较对象</param>
        /// <returns>对象表示相同顶点键时返回真值</returns>
        public override bool Equals(object obj)
        {
            return obj is VertexKey other && Equals(other);
        }

        /// <summary>
        /// 获取量化顶点键的哈希值
        /// </summary>
        /// <returns>基于三个坐标分量计算的哈希值</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = _x;
                hashCode = (hashCode * 397) ^ _y;
                hashCode = (hashCode * 397) ^ _z;
                return hashCode;
            }
        }
    }
}

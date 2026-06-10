using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ToonSmoothNormalBaker
{
    public enum SmoothNormalTarget
    {
        Tangent,
        UV2
    }

    private const string DefaultOutputDirectory = "Assets/Art/Generated/ToonSmoothNormalMeshes";

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

        // Area-weighted face normals keep large silhouette faces from being overruled
        // by tiny bevel triangles, which is important for stable anime-style outlines.
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
            // UV channels are usually imported as 0..1 data, so encode the signed
            // object-space normal instead of writing raw -1..1 values.
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

        public bool Equals(VertexKey other)
        {
            return _x == other._x && _y == other._y && _z == other._z;
        }

        public override bool Equals(object obj)
        {
            return obj is VertexKey other && Equals(other);
        }

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

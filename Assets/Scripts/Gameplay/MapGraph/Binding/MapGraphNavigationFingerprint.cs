using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>启动/生成/导航变更时捕获，不是每帧可调用的状态查询。没有 Editor 资产 API。</summary>
    public static class MapGraphNavigationFingerprint
    {
        private static readonly Unity.Profiling.ProfilerMarker CaptureMarker = new Unity.Profiling.ProfilerMarker("Anomaly.MapGraph.NavigationFingerprint");
        public static string Capture(Scene scene)
        {
            using var scope = CaptureMarker.Auto();
            if (!scene.IsValid() || !scene.isLoaded) return string.Empty;
            var triangulation = NavMesh.CalculateTriangulation();
            string geometry = MapGraphNavigationGeometrySignature.Capture(triangulation);
            if (geometry.Length == 0) return string.Empty;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("MapNavigation:v2");
            // 原生注册/重载顺序不影响几何内容；精确顶点、区域、绕序和重复数量仍参与签名。
            writer.Write(geometry);
            var signatures = new List<string>();
            bool hasLiveLinks = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var link in root.GetComponentsInChildren<NavMeshLink>(true))
                {
                    hasLiveLinks |= link.isActiveAndEnabled;
                    signatures.Add(Signature(w => {
                        w.Write("NavMeshLink"); w.Write(link.isActiveAndEnabled); w.Write(link.agentTypeID);
                        // 该版本 NavMeshLink 使用位置和旋转，不使用 Transform 的缩放。
                        Write(w, link.transform.position + link.transform.rotation * link.startPoint);
                        Write(w, link.transform.position + link.transform.rotation * link.endPoint);
                        w.Write(link.width); w.Write(link.costModifier); w.Write(link.area); w.Write(link.bidirectional); w.Write(link.autoUpdate);
                    }));
                }
                foreach (var link in root.GetComponentsInChildren<OffMeshLink>(true))
                {
                    hasLiveLinks |= link.isActiveAndEnabled && link.activated;
                    signatures.Add(Signature(w => {
                        w.Write("OffMeshLink"); w.Write(link.isActiveAndEnabled); w.Write(link.activated);
                        w.Write(link.startTransform != null); w.Write(link.endTransform != null);
                        if (link.startTransform != null) Write(w, link.startTransform.position);
                        if (link.endTransform != null) Write(w, link.endTransform.position);
                        w.Write(link.costOverride); w.Write(link.area); w.Write(link.biDirectional); w.Write(link.autoUpdatePositions);
                    }));
                }
                foreach (var surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                {
                    signatures.Add(Signature(w => {
                        w.Write("Surface"); w.Write(surface.isActiveAndEnabled); w.Write(surface.agentTypeID);
                        Write(w, surface.transform.position); Write(w, surface.transform.rotation.eulerAngles);
                        w.Write(surface.navMeshData != null);
                        if (surface.navMeshData != null) {
                            Write(w, surface.navMeshData.sourceBounds.center); Write(w, surface.navMeshData.sourceBounds.size);
                        }
                    }));
                }
            }
            signatures.Sort(StringComparer.Ordinal); writer.Write(signatures.Count);
            foreach (string signature in signatures) writer.Write(signature);
            // 链接的已提交原生位置未公开，尤其 autoUpdate=false 时不能凭组件值证明缓存仍有效。
            // 保留指纹用于变化诊断，但运行时存在活动链接时保守补验已有边。
            writer.Flush(); return (hasLiveLinks ? "live-links:" : string.Empty) + Hash(stream);
        }
        private static string Signature(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            write(writer); writer.Flush(); return Hash(stream);
        }
        private static void Write(BinaryWriter writer, Vector3 value)
        { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        private static string Hash(MemoryStream stream)
        {
            stream.Position = 0;
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}

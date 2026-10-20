#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gameplay.Agent.Telemetry.Overlay
{
    /// <summary>
    /// 一组复用的世界空间线：每帧 Begin 后按需取线，End 时隐藏本帧没用到的。
    /// 线池只在需要更多线时增长，稳态每帧只改点、颜色和宽度，不分配内存。
    /// </summary>
    public sealed class AgentBrainOverlayLines
    {
        public const int MaxPoints = 72;
        private const int CircleSegments = 40;

        private readonly Transform _root;
        private readonly Material _material;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly Vector3[] _scratch = new Vector3[MaxPoints];
        private int _used;

        public AgentBrainOverlayLines(Transform root, Material material)
        {
            _root = root;
            _material = material;
        }

        /// <summary>本帧画了几条线（测试读取）</summary>
        public int UsedCount => _used;

        public LineRenderer Get(int index) => _lines[index];

        public void Begin() => _used = 0;

        public void End()
        {
            for (int index = _used; index < _lines.Count; index++)
                if (_lines[index].enabled) _lines[index].enabled = false;
        }

        public void Polyline(Vector3[] points, int count, Color color, float width, bool loop = false)
        {
            if (count < 2) return;
            LineRenderer line = Next(color, width, loop);
            line.positionCount = count;
            line.SetPositions(points);
        }

        public void Segment(Vector3 from, Vector3 to, Color color, float width)
        {
            _scratch[0] = from;
            _scratch[1] = to;
            Polyline(_scratch, 2, color, width);
        }

        /// <summary>水平圆，圆心高度即线的高度</summary>
        public void Circle(Vector3 center, float radius, Color color, float width)
        {
            for (int index = 0; index < CircleSegments; index++)
            {
                float angle = index * (Mathf.PI * 2f / CircleSegments);
                _scratch[index] = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            Polyline(_scratch, CircleSegments, color, width, true);
        }

        /// <summary>包围盒的 12 条棱，一笔画成</summary>
        public void Box(Bounds bounds, Color color, float width)
        {
            Vector3 a = bounds.min, b = bounds.max;
            int n = 0;
            _scratch[n++] = new Vector3(a.x, a.y, a.z); _scratch[n++] = new Vector3(b.x, a.y, a.z);
            _scratch[n++] = new Vector3(b.x, a.y, b.z); _scratch[n++] = new Vector3(a.x, a.y, b.z);
            _scratch[n++] = new Vector3(a.x, a.y, a.z); _scratch[n++] = new Vector3(a.x, b.y, a.z);
            _scratch[n++] = new Vector3(b.x, b.y, a.z); _scratch[n++] = new Vector3(b.x, a.y, a.z);
            _scratch[n++] = new Vector3(b.x, b.y, a.z); _scratch[n++] = new Vector3(b.x, b.y, b.z);
            _scratch[n++] = new Vector3(b.x, a.y, b.z); _scratch[n++] = new Vector3(b.x, b.y, b.z);
            _scratch[n++] = new Vector3(a.x, b.y, b.z); _scratch[n++] = new Vector3(a.x, a.y, b.z);
            _scratch[n++] = new Vector3(a.x, b.y, b.z); _scratch[n++] = new Vector3(a.x, b.y, a.z);
            Polyline(_scratch, n, color, width);
        }

        public void HideAll()
        {
            Begin();
            End();
        }

        private LineRenderer Next(Color color, float width, bool loop)
        {
            if (_used == _lines.Count) _lines.Add(Create());
            LineRenderer line = _lines[_used++];
            if (!line.enabled) line.enabled = true;
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            line.loop = loop;
            return line;
        }

        private LineRenderer Create()
        {
            var go = new GameObject("Overlay_Line_" + _lines.Count);
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.allowOcclusionWhenDynamic = false;
            return line;
        }
    }
}
#endif

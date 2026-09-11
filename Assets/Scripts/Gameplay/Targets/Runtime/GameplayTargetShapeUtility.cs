using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Targets.Runtime
{
    /// <summary>
    /// Gameplay 目标范围形状工具
    /// 根据成员位置生成可视化用的平滑水平轮廓
    /// </summary>
    public static class GameplayTargetShapeUtility
    {
        private const int GroundHitBufferSize = 16;
        private const float MinDirectionSqrMagnitude = 0.0001f;
        private static readonly RaycastHit[] GroundHitBuffer = new RaycastHit[GroundHitBufferSize];
        private static readonly System.Comparison<Vector2> PointComparison = CompareVector2;
        public sealed class HullBuffer
        {
            internal readonly List<Vector2> Points = new List<Vector2>();
            internal readonly List<Vector2> Lower = new List<Vector2>();
            internal readonly List<Vector2> Upper = new List<Vector2>();
        }

        /// <summary>
        /// 根据一组世界坐标生成水平平滑范围轮廓
        /// 1. 0 到 1 个点时退化为圆形
        /// 2. 2 个点时退化为胶囊形
        /// 3. 3 个及以上点时生成凸包并用 CatmullRom 曲线平滑
        /// </summary>
        /// <param name="sourcePositions"></param>
        /// <param name="fallbackCenter"></param>
        /// <param name="padding"></param>
        /// <param name="fallbackRadius"></param>
        /// <param name="circleSegments"></param>
        /// <param name="smoothSegmentsPerEdge"></param>
        /// <param name="resultPoints"></param>
        /// <param name="center"></param>
        public static void BuildSmoothRange(
            IReadOnlyList<Vector3> sourcePositions,
            Vector3 fallbackCenter,
            float padding,
            float fallbackRadius,
            int circleSegments,
            int smoothSegmentsPerEdge,
            List<Vector3> resultPoints,
            out Vector3 center,
            HullBuffer buffer = null)
        {
            resultPoints.Clear();

            int sourceCount = sourcePositions != null ? sourcePositions.Count : 0;
            center = sourceCount > 0 ? CalculateCenter(sourcePositions) : fallbackCenter;
            float safePadding = Mathf.Max(0f, padding);
            float safeFallbackRadius = Mathf.Max(0.25f, fallbackRadius + safePadding);
            int safeCircleSegments = Mathf.Max(12, circleSegments);

            if (sourceCount <= 1)
            {
                BuildCircle(center, safeFallbackRadius, safeCircleSegments, resultPoints);
                return;
            }

            if (sourceCount == 2)
            {
                BuildCapsule(sourcePositions[0], sourcePositions[1], safeFallbackRadius, safeCircleSegments, resultPoints);
                center = CalculateCenter(sourcePositions);
                return;
            }

            List<Vector2> hull = BuildConvexHull(sourcePositions, buffer ?? new HullBuffer());
            if (hull.Count < 3)
            {
                BuildCircle(center, safeFallbackRadius, safeCircleSegments, resultPoints);
                return;
            }

            ExpandHull(hull, center, safePadding);
            BuildClosedCatmullRom(hull, center.y, Mathf.Max(1, smoothSegmentsPerEdge), resultPoints);
        }

        /// <summary>
        /// 将范围点投射到最近的可用地表
        /// </summary>
        /// <param name="point"></param>
        /// <param name="ownerTransform"></param>
        /// <param name="probeHeight"></param>
        /// <param name="probeDistance"></param>
        /// <param name="minGroundNormalY"></param>
        /// <returns></returns>
        public static Vector3 ProjectPointToGround(
            Vector3 point,
            Transform ownerTransform,
            float probeHeight,
            float probeDistance,
            float minGroundNormalY)
        {
            float safeProbeHeight = Mathf.Max(0.1f, probeHeight);
            float safeProbeDistance = Mathf.Max(safeProbeHeight + 0.1f, probeDistance);
            Vector3 origin = point + Vector3.up * safeProbeHeight;
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                GroundHitBuffer,
                safeProbeDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
                return point;

            float closestDistance = float.MaxValue;
            Vector3 projectedPoint = point;
            bool hasProjectedPoint = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hitInfo = GroundHitBuffer[i];
                if (!IsValidGroundHit(hitInfo, ownerTransform, minGroundNormalY))
                    continue;

                if (hitInfo.distance >= closestDistance)
                    continue;

                closestDistance = hitInfo.distance;
                projectedPoint = hitInfo.point;
                hasProjectedPoint = true;
            }

            return hasProjectedPoint ? projectedPoint : point;
        }

        // 过滤技能、UI、目标实体本身等不适合作为地面的碰撞体
        private static bool IsValidGroundHit(
            RaycastHit hitInfo,
            Transform ownerTransform,
            float minGroundNormalY)
        {
            if (hitInfo.collider == null)
                return false;

            if (hitInfo.normal.y < Mathf.Clamp01(minGroundNormalY))
                return false;

            Transform hitTransform = hitInfo.collider.transform;
            if (ownerTransform != null && hitTransform.IsChildOf(ownerTransform))
                return false;

            int hitLayer = hitInfo.collider.gameObject.layer;
            if (hitLayer == LayerMask.NameToLayer("UI"))
                return false;

            if (hitLayer == LayerMask.NameToLayer("Skill Effect"))
                return false;

            if (hitInfo.collider.GetComponentInParent<global::EnemyHealthController>() != null)
                return false;

            if (hitInfo.collider.GetComponentInParent<global::LootBoxEntity>() != null)
                return false;

            if (hitInfo.collider.GetComponentInParent<global::WorldLootItem>() != null)
                return false;

            if (hitInfo.collider.GetComponentInParent<global::ExtractionPointController>() != null)
                return false;

            return true;
        }

        // 计算所有成员点的平均中心
        private static Vector3 CalculateCenter(IReadOnlyList<Vector3> sourcePositions)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < sourcePositions.Count; i++)
            {
                sum += sourcePositions[i];
            }

            return sum / Mathf.Max(1, sourcePositions.Count);
        }

        // 在成员不足时生成稳定的圆形范围
        private static void BuildCircle(
            Vector3 center,
            float radius,
            int segmentCount,
            List<Vector3> resultPoints)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = Mathf.PI * 2f * i / segmentCount;
                resultPoints.Add(new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        // 两个成员点时用胶囊形避免范围退化成一条线
        private static void BuildCapsule(
            Vector3 first,
            Vector3 second,
            float radius,
            int segmentCount,
            List<Vector3> resultPoints)
        {
            Vector3 direction = second - first;
            direction.y = 0f;
            if (direction.sqrMagnitude <= MinDirectionSqrMagnitude)
            {
                BuildCircle(first, radius, segmentCount, resultPoints);
                return;
            }

            direction.Normalize();
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            int halfSegments = Mathf.Max(6, segmentCount / 2);

            AddArc(first, -side, side, radius, halfSegments, true, resultPoints);
            AddArc(second, side, -side, radius, halfSegments, true, resultPoints);
        }

        // 向范围点列表追加一段水平圆弧，显式指定扫描方向来避免 180 度半圆选错外侧
        private static void AddArc(
            Vector3 center,
            Vector3 fromDirection,
            Vector3 toDirection,
            float radius,
            int segmentCount,
            bool clockwise,
            List<Vector3> resultPoints)
        {
            float fromAngle = Mathf.Atan2(fromDirection.z, fromDirection.x);
            float toAngle = Mathf.Atan2(toDirection.z, toDirection.x);
            float deltaAngle = Mathf.DeltaAngle(fromAngle * Mathf.Rad2Deg, toAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (clockwise && deltaAngle > 0f)
                deltaAngle -= Mathf.PI * 2f;

            if (!clockwise && deltaAngle < 0f)
                deltaAngle += Mathf.PI * 2f;

            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = fromAngle + deltaAngle * i / segmentCount;
                resultPoints.Add(new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        /// <summary>
        /// 生成成员点的二维凸包
        /// 采用 monotonic chain 算法保证范围点顺序稳定
        /// </summary>
        /// <param name="sourcePositions"></param>
        /// <returns></returns>
        private static List<Vector2> BuildConvexHull(IReadOnlyList<Vector3> sourcePositions, HullBuffer buffer)
        {
            List<Vector2> points = buffer.Points;
            points.Clear();
            for (int i = 0; i < sourcePositions.Count; i++)
            {
                Vector3 position = sourcePositions[i];
                points.Add(new Vector2(position.x, position.z));
            }

            points.Sort(PointComparison);
            List<Vector2> lowerHull = buffer.Lower;
            lowerHull.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                AppendHullPoint(lowerHull, points[i]);
            }

            List<Vector2> upperHull = buffer.Upper;
            upperHull.Clear();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                AppendHullPoint(upperHull, points[i]);
            }

            lowerHull.RemoveAt(lowerHull.Count - 1);
            upperHull.RemoveAt(upperHull.Count - 1);
            lowerHull.AddRange(upperHull);
            return lowerHull;
        }

        // 维护凸包边界，移除会导致右转或共线回退的点
        private static void AppendHullPoint(List<Vector2> hull, Vector2 point)
        {
            while (hull.Count >= 2)
            {
                Vector2 previous = hull[hull.Count - 2];
                Vector2 current = hull[hull.Count - 1];
                if (Cross(current - previous, point - current) > 0f)
                    break;

                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(point);
        }

        // 从中心向外扩展凸包点，给目标范围留出视觉和导航余量
        private static void ExpandHull(List<Vector2> hull, Vector3 center, float padding)
        {
            if (padding <= 0f)
                return;

            Vector2 center2D = new Vector2(center.x, center.z);
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 offset = hull[i] - center2D;
                if (offset.sqrMagnitude <= MinDirectionSqrMagnitude)
                    continue;

                hull[i] = center2D + offset.normalized * (offset.magnitude + padding);
            }
        }

        /// <summary>
        /// 根据闭合凸包生成平滑曲线点
        /// </summary>
        /// <param name="hull"></param>
        /// <param name="y"></param>
        /// <param name="segmentsPerEdge"></param>
        /// <param name="resultPoints"></param>
        private static void BuildClosedCatmullRom(
            List<Vector2> hull,
            float y,
            int segmentsPerEdge,
            List<Vector3> resultPoints)
        {
            int count = hull.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = hull[(i - 1 + count) % count];
                Vector2 p1 = hull[i];
                Vector2 p2 = hull[(i + 1) % count];
                Vector2 p3 = hull[(i + 2) % count];

                for (int segment = 0; segment < segmentsPerEdge; segment++)
                {
                    float t = segment / (float)segmentsPerEdge;
                    Vector2 point = CatmullRom(p0, p1, p2, p3, t);
                    resultPoints.Add(new Vector3(point.x, y, point.y));
                }
            }
        }

        // CatmullRom 插值只负责曲线平滑，不改变目标点的整体顺序
        private static Vector2 CatmullRom(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static int CompareVector2(Vector2 first, Vector2 second)
        {
            int xComparison = first.x.CompareTo(second.x);
            return xComparison != 0 ? xComparison : first.y.CompareTo(second.y);
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }
    }
}

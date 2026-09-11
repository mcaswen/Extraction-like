using System;
using Gameplay.Targets.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalySearch.Editor.GameplayTargets
{
    /// <summary>只修复 Authoring 的范围显示接线，轮廓计算继续由 Gameplay 所有。</summary>
    public static class TargetRangeRendererRepair
    {
        public const string MaterialPath = "Assets/Art/Materials/Raid/M_TargetRangeLine.mat";

        public static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) throw new InvalidOperationException("找不到 URP Particles/Unlit shader。");
            material = new Material(shader) { name = "M_TargetRangeLine", renderQueue = (int)RenderQueue.Transparent };
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        public static void Repair(GameplayTargetAuthoringBase owner, Material fallback, TargetHierarchyRepair.Report report)
        {
            if (fallback == null) throw new ArgumentNullException(nameof(fallback));
            var serialized = new SerializedObject(owner);
            var line = serialized.FindProperty("_rangeLineRenderer").objectReferenceValue as LineRenderer;
            // 本地组件是唯一范围 Renderer；外部引用不得修改另一个目标的线。
            if (line == null || line.gameObject != owner.gameObject)
            {
                line = owner.GetComponent<LineRenderer>();
                if (line == null)
                {
                    line = Undo.AddComponent<LineRenderer>(owner.gameObject);
                    report.changes.Add(TargetHierarchyRepair.Path(owner) + "：新增 LineRenderer");
                }
                TargetHierarchyRepair.SetReference(serialized, "_rangeLineRenderer", line, report);
            }

            if (owner is ExtractionClusterAuthoring extraction)
            {
                serialized.Update();
                var target = serialized.FindProperty("_rangeColliderTarget").objectReferenceValue as GameObject;
                if (target == null || TargetHierarchyRepair.Nearest<GameplayTargetClusterAuthoringBase>(target.transform) != extraction ||
                    target.GetComponentInChildren<Collider>(true) == null)
                {
                    GameObject replacement = null;
                    foreach (var point in TargetHierarchyRepair.Owned<ExtractionPointController>(extraction))
                        if (point.GetComponentInChildren<Collider>(true) != null) { replacement = point.gameObject; break; }
                    TargetHierarchyRepair.SetReference(serialized, "_rangeColliderTarget", replacement, report);
                }
            }

            string before = EditorJsonUtility.ToJson(line);
            var beforePoints = new Vector3[line.positionCount];
            line.GetPositions(beforePoints);
            Undo.RecordObject(line, "修复目标范围线");
            if (line.sharedMaterial == null || line.sharedMaterial.shader == null ||
                line.sharedMaterial.shader.name == "Hidden/InternalErrorShader") line.sharedMaterial = fallback;
            line.enabled = true;
            line.loop = true;
            line.useWorldSpace = true;
            line.widthCurve = AnimationCurve.Linear(0, 1, 1, 1);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (owner is GameplayTargetClusterAuthoringBase cluster) cluster.RefreshRangeShape();
            else ((TargetZoneAuthoring)owner).RefreshRangeShape();
            if (EditorJsonUtility.ToJson(line) != before)
            {
                TargetHierarchyRepair.Mark(line);
                float maxDelta = 0;
                for (int i = 0; i < Mathf.Min(beforePoints.Length, line.positionCount); i++)
                    maxDelta = Mathf.Max(maxDelta, Vector3.Distance(beforePoints[i], line.GetPosition(i)));
                report.changes.Add(TargetHierarchyRepair.Path(owner) + "：刷新范围线/材质 " +
                    $"points={beforePoints.Length}->{line.positionCount} maxDelta={maxDelta:R}");
            }
        }
    }
}

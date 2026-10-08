using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceInspectionStage
    {
        private int selectedIndex = -1, hoverIndex = -1;
        private Vector3 focusPoint, smoothFocus;
        private bool cameraInitialized, userCamera, focusSaved;
        private float returnZoom, scan = -1;
        private Material overlayMaterial, ghostMaterial;
        private AerospaceTeachingFlow flow;
        private string demoReturnMode;
        private bool demoReturnFold, demoReturnStudy, demoReturnFocusSaved, demoReturnUserCamera;
        private int demoReturnRelation, demoReturnSelected;
        private float demoReturnZoom, demoReturnBaseZoom, demoReturnYaw, demoReturnPitch;
        private Vector3 demoReturnFocus;
        public bool ReducedMotion { get; private set; }
        public bool DemoActive { get; private set; }
        public bool DemoPlaying { get; private set; }
        public float DemoTime { get; private set; }
        public int DemoStep => Mathf.Min(2, (int)(DemoTime / 2.4f));
        public bool DemoEnded => DemoTime >= 7.2f;
        public float ScanProgress => scan;
        public float DisplayYaw => Mathf.Repeat(yaw, 360);
        public int FocusedHotspot => selectedIndex;
        public int TeachingPathCount => flow != null ? flow.PathCount : 0;

        private void PrepareAnalysis()
        {
            var shader = Resources.Load<Shader>("Aerospace/AnalysisOverlay");
            if (shader == null) throw new InvalidOperationException("Missing private analysis overlay shader.");
            overlayMaterial = new Material(shader) { name = "Private teaching vectors" };
            // Paths are explicitly labelled analytical overlays, not simulated fluid surfaces.
            overlayMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            ghostMaterial = new Material(shader) { name = "Private section silhouettes" };
            ghostMaterial.SetFloat("_EdgeOnly", 1);
            ghostMaterial.SetColor("_BaseColor", new Color(.71f, .78f, .48f, 1));
            foreach (var p in poses.Where(p => p.cut))
            {
                var filter = p.renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var go = new GameObject("Section_Silhouette", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = 31; go.transform.SetParent(p.t, false);
                go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                p.ghost = go.GetComponent<MeshRenderer>(); p.ghost.sharedMaterial = ghostMaterial;
                p.ghost.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; p.ghost.receiveShadows = false; p.ghost.enabled = false;
            }
            var vectors = new GameObject("Teaching_Vectors_Only"); vectors.layer = 31; vectors.transform.SetParent(pivot, false);
            flow = vectors.AddComponent<AerospaceTeachingFlow>(); flow.Initialize(overlayMaterial);
        }

        public void SetReducedMotion(bool value)
        {
            ReducedMotion = value;
            if (value) { scan = -1; DemoPlaying = false; }
        }
        public void StartScan() { scan = ReducedMotion ? -1 : 0; }
        public void SkipScan() { scan = -1; }
        public void Hover(int index) { hoverIndex = Ready && index >= 0 && index < config.hotspots.Length ? index : -1; }

        private void Focus(int index)
        {
            if (!focusSaved) { returnZoom = zoom; focusSaved = true; }
            if (anchors[index] != null) focusPoint = Vector3.ClampMagnitude(pivot.InverseTransformPoint(anchors[index].position) * .68f, .25f);
            zoom = Mathf.Clamp(returnZoom * .86f, .74f, 1.45f); userCamera = false;
        }
        private void ClearFocus(bool restore)
        {
            if (restore && focusSaved) zoom = returnZoom;
            focusSaved = false; focusPoint = Vector3.zero; userCamera = false;
        }

        // Conservative depth cue, not collider-based picking: rear-facing anchor labels recede.
        // Keep them clickable in the structure list even when their model label is subdued.
        public float AnchorVisibility(int index)
        {
            if (!Ready || index < 0 || index >= anchors.Count || anchors[index] == null) return 0;
            Vector3 relative = anchors[index].position - pivot.position;
            Vector3 towardCamera = (viewCamera.transform.position - pivot.position).normalized;
            return Vector3.Dot(relative, towardCamera) < -.06f ? .25f : 1;
        }
        public Rect ProjectedModelBounds()
        {
            Vector2 min = Vector2.one, max = Vector2.zero;
            if (!Ready) return new Rect(.3f, .1f, .4f, .8f);
            foreach (var pose in poses)
            {
                if (pose.visibility < .5f) continue;
                Bounds b = pose.renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = viewCamera.WorldToViewportPoint(corner);
                    min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public void BeginDemonstration()
        {
            if (!Ready || DemoActive) return;
            demoReturnMode = Mode; demoReturnFold = Folded; demoReturnZoom = zoom; demoReturnFocus = focusPoint;
            demoReturnStudy = Study; demoReturnRelation = RelationPhase; demoReturnSelected = selectedIndex;
            demoReturnFocusSaved = focusSaved; demoReturnBaseZoom = returnZoom; demoReturnUserCamera = userCamera;
            demoReturnYaw = yaw; demoReturnPitch = pitch;
            selectedIndex = hoverIndex = -1; focusSaved = false; focusPoint = Vector3.zero; userCamera = false;
            ApplyMode(config.code == "R01" || config.code == "R02" ? "cutaway" : "assembled");
            zoom = config.code == "R01" || config.code == "R02" ? .91f : 1; Folded = false;
            if (config.code == "R03") ToggleStudy();
            DemoActive = true; DemoPlaying = !ReducedMotion; DemoTime = 0;
            if (config.code == "R05") RelationPhase = 0;
            BuildTeachingPaths();
        }
        public void ToggleDemonstration()
        {
            if (!DemoActive) { BeginDemonstration(); return; }
            if (DemoEnded) DemoTime = 0;
            DemoPlaying = !DemoPlaying;
            // An explicit Play request may animate a teaching example even with reduced UI motion.
        }
        public void SetDemonstrationStep(int step)
        {
            if (!DemoActive) return;
            DemoTime = Mathf.Clamp(step, 0, 2) * 2.4f + .6f; DemoPlaying = false;
        }
        public void StopDemonstration(bool restore = true)
        {
            if (!DemoActive) return;
            DemoActive = DemoPlaying = false; flow?.Clear();
            LeaveStudy(); RelationPhase = -1;
            foreach (var p in poses) { p.t.localRotation = p.rotation; p.t.localPosition = p.position; }
            if (restore)
            {
                ApplyMode(demoReturnMode);
                if (demoReturnStudy) ToggleStudy();
                RelationPhase = demoReturnRelation; Folded = demoReturnFold;
                zoom = demoReturnZoom; focusPoint = demoReturnFocus;
                selectedIndex = demoReturnSelected; hoverIndex = -1;
                focusSaved = demoReturnFocusSaved; returnZoom = demoReturnBaseZoom;
                yaw = demoReturnYaw; pitch = demoReturnPitch; userCamera = demoReturnUserCamera;
            }
        }
        private void TickAnalysis()
        {
            if (scan >= 0) { scan += Time.unscaledDeltaTime / .85f; if (scan > 1) scan = -1; }
            if (DemoActive && DemoPlaying)
            {
                DemoTime = Mathf.Min(7.2f, DemoTime + Time.unscaledDeltaTime);
                if (DemoEnded) DemoPlaying = false;
            }
            if (DemoActive && config.code == "R05") RelationPhase = DemoStep;
            flow?.Present(DemoActive, DemoTime, DemoStep);
        }
        private void ApplyAnalysisMaterial(Pose p, float t)
        {
            int index = hoverIndex >= 0 ? hoverIndex : selectedIndex;
            bool selected = index >= 0 && config.hotspots[index].groups.Contains(p.semantic);
            p.highlight = Mathf.Lerp(p.highlight, selected ? 1 : 0, ReducedMotion ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20));
            properties.Clear(); properties.SetFloat("_Highlight", p.highlight);
            properties.SetFloat("_ContextDim", index >= 0 && !selected ? .17f : 0);
            properties.SetFloat("_Visibility", p.visibility);
            properties.SetFloat("_Scan", scan);
            properties.SetFloat("_ScanHeight", stage.transform.position.y + .62f - Mathf.Max(0, scan) * 1.24f);
            p.renderer.SetPropertyBlock(properties);
            if (p.ghost != null)
            {
                p.ghost.enabled = p.visibility < .98f;
                properties.Clear(); properties.SetFloat("_Opacity", (1 - p.visibility) * .32f); p.ghost.SetPropertyBlock(properties);
            }
        }
        private Vector3 Native(Vector3 blender) => model.localPosition + new Vector3(blender.x, blender.z, blender.y) * modelScale;
        private Vector3 StudyNative(Vector3 blender)
        {
            // Exported study and assembly share Blender's Z-up coordinate convention.
            return studyModel.localPosition + new Vector3(blender.x, blender.z, blender.y) * studyFitScale;
        }
        private void BuildTeachingPaths()
        {
            flow.Clear();
            Color cyan = new Color(.76f, .88f, .48f), orange = new Color(1, .71f, .38f);
            if (config.code == "R01")
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    var points = new Vector3[36]; float angle = -Mathf.PI / 2 + (lane - 1) * .21f;
                    for (int i = 0; i < points.Length; i++)
                    {
                        float z = Mathf.Lerp(-.29f, .23f, i / 35f), u = Mathf.Clamp01((.262f - z) / .582f);
                        float radius = .064f + .179f * Mathf.Pow(u, 1.63f) + .005f * Mathf.Exp(-Mathf.Pow((z - .262f) / .025f, 2)) + .007f;
                        points[i] = Native(new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), z));
                    }
                    flow.Add(points, cyan, lane * .18f, 0);
                }
            }
            else if (config.code == "R02")
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    float angle = -Mathf.PI * .5f + (lane - 1) * .42f;
                    flow.Add(new[] { Native(new Vector3(.055f * Mathf.Cos(angle), .055f * Mathf.Sin(angle), .29f)), Native(new Vector3(.055f * Mathf.Cos(angle), .055f * Mathf.Sin(angle), .075f)), Native(new Vector3(.10f * Mathf.Cos(angle), .10f * Mathf.Sin(angle), .04f)), Native(new Vector3(.18f * Mathf.Cos(angle), .18f * Mathf.Sin(angle), .02f)) }, cyan, lane * .2f, 0);
                }
                flow.Add(new[] { Native(new Vector3(.15f, .10f, .018f)), Native(new Vector3(.23f, .105f, .018f)), Native(new Vector3(.32f, .105f, .018f)) }, orange, 0, 2);
            }
            else if (config.code == "R03")
            {
                flow.Add(new[] { StudyNative(new Vector3(-.13f, 0, -.034f)), StudyNative(new Vector3(-.04f, 0, -.034f)), StudyNative(new Vector3(0, 0, -.034f)), StudyNative(new Vector3(0, 0, .235f)) }, cyan, 0, 0);
                flow.Add(new[] { StudyNative(new Vector3(.13f, 0, .027f)), StudyNative(new Vector3(.037f, 0, .027f)), StudyNative(new Vector3(.026f, -.025f, .07f)), StudyNative(new Vector3(.026f, -.025f, .24f)) }, orange, .35f, 0);
            }
            else if (config.code == "R04")
            {
                // Straight arrows through open cell centres; not a computed aerodynamic field.
                for (int lane = -1; lane <= 1; lane++)
                    flow.Add(new[] { Native(new Vector3(lane * .098f, 0, .27f)), Native(new Vector3(lane * .098f, 0, .04f)), Native(new Vector3(lane * .098f, 0, -.20f)) }, cyan, (lane + 1) * .2f, 1);
            }
            else
            {
                // Direction-only guide beside the interface, never drawn as a live release mechanism.
                foreach (float x in new[] { -.30f, .30f })
                    flow.Add(new[] { Native(new Vector3(x, -.39f, .065f)), Native(new Vector3(x, -.39f, .22f)) }, cyan, 0, 2);
            }
        }
        private void ClearAnalysis()
        {
            DemoActive = DemoPlaying = false; DemoTime = 0; scan = -1; selectedIndex = hoverIndex = -1;
            focusPoint = smoothFocus = Vector3.zero; focusSaved = cameraInitialized = userCamera = false;
            if (flow != null) { flow.gameObject.SetActive(false); Destroy(flow.gameObject); flow = null; }
            if (overlayMaterial != null) Destroy(overlayMaterial);
            if (ghostMaterial != null) Destroy(ghostMaterial);
            overlayMaterial = ghostMaterial = null;
        }
    }

    /// <summary>Low-cost local-space vector trails. No particles, colliders or gameplay lights.</summary>
    public sealed class AerospaceTeachingFlow : MonoBehaviour
    {
        private sealed class Path
        {
            public Vector3[] points; public LineRenderer baseLine, trail; public float phase; public int fromStep;
            public readonly Vector3[] moving = new Vector3[9];
        }
        private readonly List<Path> paths = new List<Path>();
        private Material material;
        public int PathCount => paths.Count;
        public void Initialize(Material value) { material = value; }
        public void Clear()
        {
            foreach (var p in paths) { p.baseLine.gameObject.SetActive(false); p.trail.gameObject.SetActive(false); Destroy(p.baseLine.gameObject); Destroy(p.trail.gameObject); }
            paths.Clear();
        }
        private LineRenderer Line(string title, Color tint, float width)
        {
            var go = new GameObject(title); go.layer = 31; go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = tint;
            line.numCapVertices = 3; line.numCornerVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        public void Add(Vector3[] points, Color tint, float phase, int fromStep)
        {
            var dim = tint; dim.a = .40f;
            var p = new Path { points = points, baseLine = Line("Schematic_Path", dim, .0025f), trail = Line("Flow_Direction", tint, .007f), phase = phase, fromStep = fromStep };
            p.baseLine.positionCount = points.Length; p.baseLine.SetPositions(points); p.trail.positionCount = 9;
            p.trail.widthCurve = new AnimationCurve(new Keyframe(0, .12f), new Keyframe(.68f, 1), new Keyframe(.78f, 2.2f), new Keyframe(1, 0));
            p.trail.widthMultiplier = .007f;
            paths.Add(p);
        }
        public void Present(bool active, float time, int step)
        {
            foreach (var p in paths)
            {
                bool show = active && step >= p.fromStep;
                p.baseLine.enabled = p.trail.enabled = show; if (!show) continue;
                float head = Mathf.Repeat(time * .38f + p.phase, 1.25f);
                for (int i = 0; i < p.moving.Length; i++)
                {
                    float u = Mathf.Clamp01(head - .21f + i * .21f / 8), index = u * (p.points.Length - 1);
                    int a = Mathf.Min(p.points.Length - 2, Mathf.FloorToInt(index));
                    p.moving[i] = Vector3.Lerp(p.points[a], p.points[a + 1], index - a);
                }
                p.trail.SetPositions(p.moving);
            }
        }
    }
}

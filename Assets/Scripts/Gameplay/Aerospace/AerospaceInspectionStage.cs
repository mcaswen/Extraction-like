using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ExtractionLike.Aerospace
{
    /// <summary>A render-texture-only display; contains no collision, navigation or gameplay behaviours.</summary>
    public sealed partial class AerospaceInspectionStage : MonoBehaviour
    {
        public const float ViewWidth = 1152, ViewHeight = 680;
        private static readonly HashSet<string> ReleaseParts = new HashSet<string> { "ClampShoes", "ClampBand", "ReleaseHousing", "ReleaseCover", "Harness", "Identification", "Markings", "ReleaseMarkings", "EquipmentReleaseGuard", "EquipmentReleaseGuardInk", "EquipmentReleaseServices", "EquipmentReleaseServicesInk" };
        private static readonly HashSet<string> UpperParts = new HashSet<string> { "UpperInterface", "UpperHardware", "UpperMarkings", "EquipmentUpperAdapter", "EquipmentUpperInspection", "EquipmentUpperInspectionInk" };
        private sealed class Pose { public Transform t; public Vector3 position, offset; public Quaternion rotation; public bool cut; public Renderer renderer, ghost; public string semantic; public float visibility = 1, highlight; }
        private readonly List<Pose> poses = new List<Pose>();
        private readonly List<Transform> anchors = new List<Transform>();
        private Transform pivot, model, foldPivot;
        private Quaternion foldRest;
        private Camera viewCamera;
        private GameObject stage;
        private AerospaceModelAsset binding;
        private AerospacePart config;
        private MaterialPropertyBlock properties;
        private int generation;
        private float zoom = 1, fitDistance = 2, pitch, yaw;
        private Vector3 direction;
        private Transform studyModel;
        private float modelScale = 1, studyFitScale = 1;
        public bool Study { get; private set; }
        public int RelationPhase { get; private set; } = -1;
        public RenderTexture Texture { get; private set; }
        public bool Ready { get; private set; }
        public string Mode { get; private set; } = "assembled";
        public bool Folded { get; private set; }
        public bool Recovered { get; private set; }
        public int HotspotCount => anchors.Count(a => a != null);
        public Camera DisplayCamera => viewCamera;
        public GameObject CurrentModel => model != null ? model.gameObject : null;

        private void EnsureStage()
        {
            if (stage != null) return;
            stage = new GameObject("Aerospace_Private_Inspection_Stage"); stage.transform.SetParent(transform, false);
            stage.transform.position = new Vector3(0, -10000, 0);
            pivot = new GameObject("360_Turntable").transform; pivot.SetParent(stage.transform, false);
            viewCamera = new GameObject("Inspection_Camera").AddComponent<Camera>(); viewCamera.transform.SetParent(stage.transform, false);
            viewCamera.gameObject.AddComponent<AerospaceInspectionCamera>();
            viewCamera.cullingMask = 1 << 31; viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.clear;
            viewCamera.fieldOfView = 32; viewCamera.nearClipPlane = .01f; viewCamera.farClipPlane = 12;
            viewCamera.allowHDR = false; viewCamera.allowMSAA = true; viewCamera.allowDynamicResolution = false; viewCamera.depth = -100;
            var data = viewCamera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
            data.renderShadows = false; data.volumeLayerMask = 0;
            data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            UpdateDisplayResolution(new Vector2(ViewWidth, ViewHeight));
            viewCamera.aspect = ViewWidth / ViewHeight;
            properties = new MaterialPropertyBlock();
        }
        public static Vector2Int RenderSizeFor(Vector2 displayPixels)
        {
            // Integer 144:85 steps preserve the viewport aspect. The cap bounds GPU memory at 4K.
            int maximum = Mathf.Max(1, Mathf.Min(24, SystemInfo.maxTextureSize / 144));
            int minimum = Mathf.Min(14, maximum);
            float width = Mathf.Max(Mathf.Abs(displayPixels.x), Mathf.Abs(displayPixels.y) * ViewWidth / ViewHeight);
            int steps = Mathf.Clamp(Mathf.CeilToInt(width * 1.25f / 288) * 2, minimum, maximum);
            return new Vector2Int(steps * 144, steps * 85);
        }
        public void UpdateDisplayResolution(Vector2 displayPixels)
        {
            if (viewCamera == null) return;
            Vector2Int size = RenderSizeFor(displayPixels);
            if (Texture != null && Texture.width == size.x && Texture.height == size.y) return;
            var replacement = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
            {
                name = "Aerospace_Inspection_Dossier", antiAliasing = 4,
                filterMode = FilterMode.Bilinear, useDynamicScale = false
            };
            if (!replacement.Create()) { Destroy(replacement); return; }
            var previous = Texture;
            Texture = replacement; viewCamera.targetTexture = Texture;
            viewCamera.aspect = ViewWidth / ViewHeight;
            if (previous != null) { previous.Release(); Destroy(previous); }
        }
        public IEnumerator Load(AerospacePart part, string quality, Action<string> done)
        {
            int ticket = ++generation; ClearModel(); EnsureStage(); viewCamera.enabled = true; config = part;
            var request = Resources.LoadAsync<GameObject>("Aerospace/Models/" + part.code + "_" + quality);
            yield return request;
            if (ticket != generation) yield break;
            if (request.asset == null) { done("模型资源缺失，可关闭后重试。"); yield break; }
            var instance = Instantiate((GameObject)request.asset, pivot, false); model = instance.transform;
            model.localPosition = Vector3.zero; // Keep the FBX root's axis-conversion rotation.
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            binding = instance.GetComponent<AerospaceModelAsset>();
            if (binding == null || binding.meshes.Length == 0) { ClearModel(); done("模型绑定不完整。"); yield break; }
            var bounds = binding.meshes[0].bounds; foreach (var r in binding.meshes) bounds.Encapsulate(r.bounds);
            float scale = 1f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
            modelScale = scale;
            Vector3 center = pivot.InverseTransformPoint(bounds.center);
            model.localScale *= scale; model.localPosition -= center * scale;
            foreach (var r in binding.meshes)
            {
                var p = Array.Find(part.parts, entry => entry.name == r.name);
                if (p == null) continue;
                Vector3 offsetWorld = pivot.TransformVector(AerospaceCatalog.Convert(p.explode) * scale);
                poses.Add(new Pose { t = r.transform, position = r.transform.localPosition, rotation = r.transform.localRotation, offset = r.transform.parent.InverseTransformVector(offsetWorld),
                    cut = p.cut, renderer = r, semantic = r.name.Replace(part.code + "_", "").Replace("_low", "") });
            }
            var all = instance.GetComponentsInChildren<Transform>(true);
            foreach (var h in part.hotspots)
            {
                var anchor = Array.Find(all, t => t.name == h.anchor);
                var owner = poses.Find(p => h.groups != null && h.groups.Contains(p.semantic));
                if (anchor != null && owner != null) anchor.SetParent(owner.t, true);
                anchors.Add(anchor);
            }
            foldPivot = Array.Find(all, t => t.name == "R04_FoldPivot");
            if (foldPivot != null) foldRest = foldPivot.localRotation;
            direction = AerospaceCatalog.Convert(part.camera).normalized;
            Ready = true; PrepareAnalysis(); ResetView(); done(null);
        }
        public void SetMode(string mode)
        {
            StopDemonstration(false); ClearFocus(true); selectedIndex = hoverIndex = -1;
            ApplyMode(mode);
        }
        private void ApplyMode(string mode)
        {
            LeaveStudy(); RelationPhase = -1;
            Mode = mode == "cutaway" || mode == "exploded" ? mode : "assembled";
            if (Mode == "exploded") Folded = false;
        }
        public void Select(int index)
        {
            if (!Ready || index < -1 || index >= config.hotspots.Length) return;
            StopDemonstration(false); hoverIndex = -1;
            if (index < 0) ClearFocus(true);
            else Focus(index);
            selectedIndex = index; ApplyMode(index >= 0 ? config.hotspots[index].view : "assembled");
        }
        public void ResetView()
        {
            // Discard any saved focus before assigning the default zoom. Otherwise
            // SetMode would silently restore the pre-focus zoom over the reset value.
            StopDemonstration(false); ClearFocus(false);
            pitch = yaw = 0; zoom = 1; Folded = false; Recovered = false; binding?.ApplySurface(false);
            SetMode("assembled"); Select(-1);
            if (pivot != null) pivot.localRotation = Quaternion.identity;
            // Use the assembled pose even when resetting from a moving exploded view.
            foreach (var p in poses) { p.t.localPosition = p.position; p.t.localRotation = p.rotation; p.t.gameObject.SetActive(true); p.renderer.enabled = true; p.visibility = 1; }
            if (foldPivot != null) foldPivot.localRotation = foldRest;
            focusPoint = smoothFocus = Vector3.zero; cameraInitialized = false;
            FitView(binding != null ? binding.meshes : null); PoseCamera();
        }
        public void ToggleSurface() { Recovered = !Recovered; binding?.ApplySurface(Recovered); }
        public void ToggleFold() { if (foldPivot == null) return; bool next = !Folded; SetMode("assembled"); Folded = next; }
        public void CycleRelation()
        {
            if (!Ready || config.code != "R05") return;
            int next = (RelationPhase + 1) % 3; SetMode("assembled"); RelationPhase = next;
        }
        public bool ToggleStudy()
        {
            if (!Ready || config.code != "R03") return false;
            ClearFocus(true); selectedIndex = hoverIndex = -1;
            if (Study) { LeaveStudy(); return true; }
            if (studyModel == null)
            {
                var prefab = Resources.Load<GameObject>("Aerospace/Models/R03_Study"); if (prefab == null) return false;
                pivot.localRotation = Quaternion.identity;
                studyModel = Instantiate(prefab, pivot, false).transform;
                foreach (var t in studyModel.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                studyModel.localPosition = Vector3.zero;
                var renderers = studyModel.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float s = 1 / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, .001f);
                studyFitScale = s;
                Vector3 center = pivot.InverseTransformPoint(bounds.center); studyModel.localScale *= s; studyModel.localPosition -= center * s;
            }
            Study = true; model.gameObject.SetActive(false); studyModel.gameObject.SetActive(true); pitch = yaw = 0; zoom = 1;
            direction = new Vector3(.32f, .27f, -.7f).normalized;
            pivot.localRotation = Quaternion.identity; FitView(studyModel.GetComponentsInChildren<Renderer>()); return true;
        }
        private void LeaveStudy()
        {
            bool wasStudy = Study;
            Study = false; if (studyModel != null) studyModel.gameObject.SetActive(false); if (model != null) model.gameObject.SetActive(true);
            if (config != null) direction = AerospaceCatalog.Convert(config.camera).normalized;
            if (wasStudy) { pitch = yaw = 0; zoom = 1; pivot.localRotation = Quaternion.identity; FitView(binding.meshes); }
        }
        public void Orbit(Vector2 delta) { if (!Ready) return; userCamera = true; yaw += delta.x * .32f; pitch -= delta.y * .32f; }
        public void Zoom(float delta) { if (!Ready) return; userCamera = true; zoom = Mathf.Clamp(zoom - delta * .055f, .64f, 1.85f); }
        public bool TryAnchor(int index, out Vector2 point)
        {
            point = default;
            if (!Ready || Study || index < 0 || index >= anchors.Count || anchors[index] == null || !anchors[index].gameObject.activeInHierarchy) return false;
            if (poses.Any(pose => pose.t == anchors[index].parent && pose.visibility < .5f)) return false;
            Vector3 p = viewCamera.WorldToViewportPoint(anchors[index].position);
            point = new Vector2(p.x, p.y); return p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
        }
        private void LateUpdate()
        {
            if (!Ready) return;
            TickAnalysis();
            float t = ReducedMotion ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8);
            pivot.localRotation = Quaternion.Euler(pitch, yaw, 0);
            foreach (var p in poses)
            {
                p.visibility = Mathf.MoveTowards(p.visibility, Mode == "cutaway" && p.cut ? 0 : 1, ReducedMotion ? 1 : Time.unscaledDeltaTime / .6f);
                p.renderer.enabled = p.visibility > .001f;
                Vector3 extra = Vector3.zero;
                if (RelationPhase >= 1 && ReleaseParts.Contains(p.semantic)) extra += new Vector3(0, 0, -.105f);
                if (RelationPhase == 2 && UpperParts.Contains(p.semantic)) extra += new Vector3(0, .19f, 0);
                Vector3 relation = p.t.parent.InverseTransformVector(pivot.TransformVector(extra * modelScale));
                p.t.localPosition = Vector3.Lerp(p.t.localPosition, p.position + relation + (Mode == "exploded" ? p.offset : Vector3.zero), t);
                p.t.localRotation = p.rotation;
                if (DemoActive && config.code == "R02" && (p.semantic == "Impeller" || p.semantic == "Inducer" || p.semantic == "Shaft"))
                {
                    Vector3 axis = p.t.parent.InverseTransformDirection(pivot.up);
                    Vector3 origin = p.t.parent.InverseTransformPoint(pivot.TransformPoint(Native(Vector3.zero)));
                    var rotation = Quaternion.AngleAxis(DemoTime * 36, axis);
                    p.t.localPosition = origin + rotation * (p.position - origin); p.t.localRotation = rotation * p.rotation;
                }
                ApplyAnalysisMaterial(p, t);
            }
            float foldAngle = Folded ? 65 : 0;
            if (DemoActive && config.code == "R04") foldAngle = DemoStep == 0 ? Mathf.Lerp(65, 0, Mathf.SmoothStep(0, 1, DemoTime / 2)) : 0;
            if (foldPivot != null) foldPivot.localRotation = Quaternion.Slerp(foldPivot.localRotation, foldRest * Quaternion.AngleAxis(foldAngle, Vector3.right), t);
            PoseCamera();
        }
        private void PoseCamera()
        {
            if (viewCamera == null) return;
            float t = !cameraInitialized || ReducedMotion || userCamera ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 9);
            smoothFocus = Vector3.Lerp(smoothFocus, pivot.localRotation * focusPoint, t);
            Vector3 desired = smoothFocus + direction * fitDistance * zoom * (Study ? 1 : Mode == "exploded" ? 1.38f : RelationPhase >= 0 ? 1.58f : 1);
            viewCamera.transform.localPosition = Vector3.Lerp(viewCamera.transform.localPosition, desired, t);
            viewCamera.transform.LookAt(stage.transform.TransformPoint(smoothFocus)); cameraInitialized = true;
        }
        private void FitView(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0 || viewCamera == null) return;
            Quaternion cameraSpace = Quaternion.Inverse(Quaternion.LookRotation(-direction, Vector3.up));
            float tangent = Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad), distance = .6f;
            float verticalFill = config != null && config.code == "R04" && !Study ? .90f : .96f;
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = cameraSpace * (corner - pivot.position);
                    distance = Mathf.Max(distance, Mathf.Abs(p.y) / (tangent * verticalFill) - p.z, Mathf.Abs(p.x) / (tangent * viewCamera.aspect * .78f) - p.z);
                }
            }
            fitDistance = distance;
        }
        private void ClearModel()
        {
            ClearAnalysis();
            Ready = false; poses.Clear(); anchors.Clear(); foldPivot = null; binding = null;
            Study = false; RelationPhase = -1;
            if (studyModel != null) { studyModel.gameObject.SetActive(false); Destroy(studyModel.gameObject); studyModel = null; }
            if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); model = null; }
            if (pivot != null) pivot.localRotation = Quaternion.identity;
        }
        public void Unload()
        {
            ++generation; ClearModel();
            if (viewCamera != null) viewCamera.enabled = false;
            if (Texture != null) { if (viewCamera != null) viewCamera.targetTexture = null; Texture.Release(); Destroy(Texture); Texture = null; }
            if (stage != null) { stage.SetActive(false); Destroy(stage); stage = null; }
        }
        private void OnDestroy() { Unload(); }
    }
}

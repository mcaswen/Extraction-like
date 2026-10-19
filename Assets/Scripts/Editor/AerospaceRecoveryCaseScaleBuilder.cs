using System;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    public const string CaseScaleReports = "UserSettings/ScenePreviews/RecoveryCaseScale_20261019";
    [Serializable] public sealed class CaseSizeRow
    {
        public string container;
        public Vector3 beforeSize, afterSize;
        public float beforeBottom, afterBottom;
    }
    [Serializable] public sealed class CaseSizeDelivery
    {
        public float visualScale;
        public int count, colliders, agents, pathProbes, completePaths;
        public string gameplayHash, navigationHash;
        public CaseSizeRow[] cases;
    }

    [MenuItem("Tools/Extraction-like/Aerospace/All Routes/Resize Cases to 75 Percent")]
    public static void ResizeRecoveryCases()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Save and exit Play mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var manifest = AllInScene<AerospaceRouteSetDressing>(scene).Single();
        if (manifest.cases.Length != 32) throw new InvalidOperationException("Unexpected formal container count.");
        EnsureSceneBackup("UserSettings/SceneBackups/Before_RecoveryCaseScale_20261019.unity");
        Directory.CreateDirectory(CaseScaleReports);
        // Render only after saving: previews may initialize pipeline-side data that
        // must not be included in this visual-transform-only scene edit.
        var before = CaptureInvestigationNavigation(scene);
        string gameplay = RouteGameplaySignature(scene), sources = ForestBuildSources(scene);
        int colliderCount = AllInScene<Collider>(scene).Length;
        var visualRoots = manifest.cases.Select(c => c.replacement.transform).ToHashSet();
        var protectedTransforms = AllInScene<Transform>(scene).Where(t => !visualRoots.Contains(t)).ToDictionary(t => t, RouteTransformSignature);
        var sizes = manifest.cases.Select(c => {
            TryGetBounds(c.replacement, out var b);
            return new CaseSizeRow { container = GetPath(c.container.transform), beforeSize = b.size, beforeBottom = b.min.y };
        }).ToArray();
        for (int i = 0; i < manifest.cases.Length; i++)
        {
            var entry = manifest.cases[i];
            entry.visualScale = CompactRecoveryCaseScale;
            FitRecoveryCase(entry);
            TryGetBounds(entry.replacement, out var b);
            sizes[i].afterSize = b.size; sizes[i].afterBottom = b.min.y;
        }
        // Repeat fitting to verify it does not compound the reduction on later refreshes.
        var fitted = manifest.cases.Select(c => new { position = c.replacement.transform.localPosition, scale = c.replacement.transform.localScale }).ToArray();
        foreach (var entry in manifest.cases) FitRecoveryCase(entry);
        for (int i = 0; i < fitted.Length; i++)
            if (Vector3.Distance(fitted[i].position, manifest.cases[i].replacement.transform.localPosition) > .0001f ||
                Vector3.Distance(fitted[i].scale, manifest.cases[i].replacement.transform.localScale) > .0001f)
                throw new InvalidOperationException("Case fitting is not idempotent.");
        foreach (var pair in protectedTransforms)
            if (pair.Key == null || RouteTransformSignature(pair.Key) != pair.Value)
                throw new InvalidOperationException("A non-visual transform changed: " + pair.Key);
        ValidateAerospaceRouteNetwork();
        AssertInvestigationSnapshotEqual(before, CaptureInvestigationNavigation(scene));
        if (gameplay != RouteGameplaySignature(scene) || sources != ForestBuildSources(scene) || colliderCount != AllInScene<Collider>(scene).Length)
            throw new InvalidOperationException("Original gameplay, collision or navigation sources changed.");
        EditorUtility.SetDirty(manifest); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        File.WriteAllText(CaseScaleReports + "/verification.json", JsonUtility.ToJson(new CaseSizeDelivery {
            visualScale = CompactRecoveryCaseScale, count = sizes.Length, colliders = colliderCount,
            agents = before.Agents, pathProbes = before.Probes, completePaths = before.Complete,
            gameplayHash = gameplay, navigationHash = before.GeometryHash, cases = sizes
        }, true));
        RenderCaseSizeViews(scene, manifest, "after");
        Debug.Log("RECOVERY_CASE_SIZE_COMPLETE count=" + sizes.Length + " scale=" + CompactRecoveryCaseScale + " gameplayAndNavigationUnchanged=true");
    }

    static void RenderCaseSizeViews(Scene scene, AerospaceRouteSetDressing manifest, string label)
    {
        var cycle = FindDayNight(scene);
        float phase = cycle.CurrentPhase; var saved = AerospaceLightingState.Capture(cycle.sunLight);
        var sky = new Material(cycle.skyboxVersion2);
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var selected = new[] { "员工食堂", "奇点塔" }.Select(name => manifest.cases.FirstOrDefault(c => GetPath(c.container.transform).Contains(name)))
            .Where(c => c != null).ToArray();
        if (selected.Length == 0) selected = new[] { manifest.cases[0] };
        try
        {
            cycle.ApplyPhase(0, sky);
            for (int i = 0; i < selected.Length; i++)
            {
                var entry = selected[i];
                Vector3 focus = entry.container.transform.TransformPoint(entry.originalLocalBounds.center);
                string path = CaseScaleReports + "/game-" + i + "-" + label + ".png";
                RenderActualGameView(scene, path, focus); RenderActualGameView(scene, path, focus);
                if (i == 0 && label == "after")
                {
                    var feedback = entry.replacement.GetComponent<RecoveryCaseOpeningFeedback>();
                    try
                    {
                        feedback.PreviewPose(RecoveryCaseOpeningFeedback.OpeningDuration);
                        RenderActualGameView(scene, CaseScaleReports + "/game-open-after.png", focus);
                    }
                    finally { feedback.PreviewPose(0); }
                }
            }
        }
        finally
        {
            cycle.ApplyPhase(phase, sky); saved.Restore(cycle.sunLight);
            ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(sky);
        }
    }
}

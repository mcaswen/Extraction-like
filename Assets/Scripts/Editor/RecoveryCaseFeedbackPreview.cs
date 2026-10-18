using System;
using System.IO;
using System.Linq;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class AerospaceSceneLayoutBuilder
{
    public static void RenderRecoveryCaseOpening()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var entry = AllInScene<AerospaceRouteSetDressing>(scene).Single().cases.First(c => GetPath(c.container.transform).Contains("员工食堂"));
        var feedback = entry.replacement.GetComponent<RecoveryCaseOpeningFeedback>();
        var cycle = FindDayNight(scene); var sky = new Material(cycle.skyboxVersion2);
        const string output = "UserSettings/ScenePreviews/RecoveryCase";
        Directory.CreateDirectory(output);
        bool old = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            cycle.ApplyPhase(0, sky);
            TryGetBounds(entry.replacement, out var bounds);
            Debug.Log("RECOVERY_PREVIEW bounds="+bounds+" scale="+entry.replacement.transform.lossyScale);
            foreach (float age in new[]{0f,.17f,.48f,.78f,1.08f})
            {
                feedback.PreviewPose(age);
                string id = Mathf.RoundToInt(age * 100).ToString("000");
                RenderActualGameView(scene, output+"/game-"+id+".png", entry.container.transform.position);
                RenderActualGameView(scene, output+"/game-"+id+".png", entry.container.transform.position);
                RenderCameraPreview(output+"/detail-"+id+".png", bounds.center+new Vector3(15,19,-24), bounds.center+Vector3.up*3.2f,40);
            }
            // Test the same open appearance at the darkest cycle point, without adding lights.
            cycle.ApplyPhase(.5f, sky);
            RenderActualGameView(scene, output+"/night-open.png", entry.container.transform.position);
            Debug.Log("RECOVERY_PREVIEW_COMPLETE");
        }
        finally {feedback.PreviewPose(0);ShaderUtil.allowAsyncCompilation=old;Object.DestroyImmediate(sky);}
    }
}

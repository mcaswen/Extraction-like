#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalySearch.Automation.SceneRaid
{
    public static class SceneRaidRenderEvidence
    {
        [Serializable] private sealed class CameraState
        {
            public string name, type, targetTexture;
            public bool enabled, active;
            public int width, height, display, cullingMask;
        }
        [Serializable] private sealed class State
        {
            public string pipelineAsset, pipelineType, qualityPipeline, screenMode;
            public bool focused;
            public int width, height, renderInterval, contexts, renderedFrames;
            public CameraState[] cameras;
        }
        public static void Save(string output, string name, SceneRaidFrameSampler sampler)
        {
            var state = new State
            {
                pipelineAsset = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "null",
                pipelineType = RenderPipelineManager.currentPipeline?.GetType().FullName ?? "null",
                qualityPipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "null",
                screenMode = Screen.fullScreenMode.ToString(), focused = Application.isFocused,
                width = Screen.width, height = Screen.height, renderInterval = OnDemandRendering.renderFrameInterval,
                contexts = sampler.RenderContexts, renderedFrames = sampler.RenderedFrames,
                cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true).Select(camera => new CameraState
                {
                    name = camera.name, type = camera.cameraType.ToString(), enabled = camera.enabled,
                    active = camera.gameObject.activeInHierarchy, width = camera.pixelWidth, height = camera.pixelHeight,
                    display = camera.targetDisplay, cullingMask = camera.cullingMask,
                    targetTexture = camera.targetTexture != null ? camera.targetTexture.name : "null"
                }).ToArray()
            };
            File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(state, true));
        }
    }
}
#endif

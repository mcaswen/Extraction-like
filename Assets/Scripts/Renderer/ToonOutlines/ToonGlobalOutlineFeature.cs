using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ToonGlobalOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public bool enabled = true;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public LayerMask layerMask = ~0;
        public RenderQueueRangeMode renderQueue = RenderQueueRangeMode.Opaque;
        [Range(0f, 4f)] public float globalThicknessScale = 1f;
        public bool includeSceneView = true;
    }

    public enum RenderQueueRangeMode
    {
        Opaque,
        Transparent,
        All
    }

    public Settings settings = new Settings();

    private ToonGlobalOutlinePass _pass;

    public override void Create()
    {
        _pass = new ToonGlobalOutlinePass
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRender(renderingData.cameraData) || _pass == null)
        {
            return;
        }

        _pass.renderPassEvent = settings.renderPassEvent;
        _pass.Setup(settings);
        renderer.EnqueuePass(_pass);
    }

    private bool ShouldRender(CameraData cameraData)
    {
        if (!settings.enabled || cameraData.isPreviewCamera)
        {
            return false;
        }

        if (cameraData.cameraType == CameraType.SceneView)
        {
            return settings.includeSceneView;
        }

        return cameraData.cameraType == CameraType.Game;
    }

    private static RenderQueueRange ToRenderQueueRange(RenderQueueRangeMode mode)
    {
        switch (mode)
        {
            case RenderQueueRangeMode.Transparent:
                return RenderQueueRange.transparent;
            case RenderQueueRangeMode.All:
                return RenderQueueRange.all;
            default:
                return RenderQueueRange.opaque;
        }
    }

    private sealed class ToonGlobalOutlinePass : ScriptableRenderPass
    {
        private static readonly ShaderTagId ToonOutlineShaderTag = new ShaderTagId("ToonOutline");
        private static readonly int GlobalOutlineThicknessScaleId = Shader.PropertyToID("_GlobalOutlineThicknessScale");

        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Toon Global Inverted Hull Outline");

        private FilteringSettings _filteringSettings;
        private float _globalThicknessScale = 1f;

        public void Setup(Settings settings)
        {
            _globalThicknessScale = settings.globalThicknessScale;
            _filteringSettings = new FilteringSettings(ToRenderQueueRange(settings.renderQueue), settings.layerMask);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Toon Global Outline");
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                cmd.SetGlobalFloat(GlobalOutlineThicknessScaleId, _globalThicknessScale);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
            DrawingSettings drawingSettings = CreateDrawingSettings(ToonOutlineShaderTag, ref renderingData, sortingCriteria);
            context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _filteringSettings);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

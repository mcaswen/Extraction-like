using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ToonSobelOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        public bool enabled = true;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Shader shader;
        public Color outlineColor = new Color(0.02f, 0.018f, 0.016f, 1f);
        [Range(0f, 4f)] public float intensity = 1f;
        [Range(0.0001f, 0.2f)] public float depthThreshold = 0.01f;
        [Range(0.0001f, 2f)] public float normalThreshold = 0.25f;
        [Range(0f, 8f)] public float depthWeight = 1f;
        [Range(0f, 8f)] public float normalWeight = 1f;
        [Range(1f, 4f)] public float thickness = 1f;
        public bool includeSceneView;
    }

    public Settings settings = new Settings();

    private ToonSobelOutlinePass _pass;
    private Material _material;

    public override void Create()
    {
        Shader shader = settings.shader != null ? settings.shader : Shader.Find("Hidden/TA/Toon/Sobel Outline");
        _material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        _pass = new ToonSobelOutlinePass(_material)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (!ShouldRender(renderingData.cameraData) || _pass == null)
            {
                return;
            }

            _pass.renderPassEvent = settings.renderPassEvent;
            // Requesting depth and normals lets URP generate _CameraDepthTexture and
            // _CameraNormalsTexture before this pass. The Toon shader includes a
            // DepthNormals pass specifically so stylized characters contribute here.
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            _pass.SetTarget(renderer.cameraColorTargetHandle, settings);
        }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRender(renderingData.cameraData) || _material == null || _pass == null)
        {
            return;
        }

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        CoreUtils.Destroy(_material);
    }

    private bool ShouldRender(CameraData cameraData)
    {
        if (!settings.enabled)
        {
            return false;
        }

        if (cameraData.isPreviewCamera)
        {
            return false;
        }

        if (cameraData.cameraType == CameraType.SceneView)
        {
            return settings.includeSceneView;
        }

        return cameraData.cameraType == CameraType.Game;
    }

    private sealed class ToonSobelOutlinePass : ScriptableRenderPass
    {
        private static readonly int SobelOutlineColorId = Shader.PropertyToID("_SobelOutlineColor");
        private static readonly int SobelIntensityId = Shader.PropertyToID("_SobelIntensity");
        private static readonly int SobelDepthThresholdId = Shader.PropertyToID("_SobelDepthThreshold");
        private static readonly int SobelNormalThresholdId = Shader.PropertyToID("_SobelNormalThreshold");
        private static readonly int SobelDepthWeightId = Shader.PropertyToID("_SobelDepthWeight");
        private static readonly int SobelNormalWeightId = Shader.PropertyToID("_SobelNormalWeight");
        private static readonly int SobelThicknessId = Shader.PropertyToID("_SobelThickness");

        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Toon Sobel Outline");
        private readonly Material _material;
        private RTHandle _cameraColorTarget;
        private RTHandle _temporaryColorTarget;
        private Settings _settings;

        public ToonSobelOutlinePass(Material material)
        {
            _material = material;
        }

        public void SetTarget(RTHandle cameraColorTarget, Settings settings)
        {
            _cameraColorTarget = cameraColorTarget;
            _settings = settings;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(
                ref _temporaryColorTarget,
                descriptor,
                FilterMode.Bilinear,
                name: "_ToonSobelOutlineTempColor");

            ConfigureTarget(_cameraColorTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _cameraColorTarget == null || _settings == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("Toon Sobel Outline");
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                _material.SetColor(SobelOutlineColorId, _settings.outlineColor);
                _material.SetFloat(SobelIntensityId, _settings.intensity);
                _material.SetFloat(SobelDepthThresholdId, _settings.depthThreshold);
                _material.SetFloat(SobelNormalThresholdId, _settings.normalThreshold);
                _material.SetFloat(SobelDepthWeightId, _settings.depthWeight);
                _material.SetFloat(SobelNormalWeightId, _settings.normalWeight);
                _material.SetFloat(SobelThicknessId, _settings.thickness);

                // Do not blit camera color to itself. Some URP backends return black
                // when a material pass reads and writes the same color target.
                Blitter.BlitCameraTexture(cmd, _cameraColorTarget, _temporaryColorTarget, _material, 0);
                Blitter.BlitCameraTexture(cmd, _temporaryColorTarget, _cameraColorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _temporaryColorTarget?.Release();
            _temporaryColorTarget = null;
        }
    }
}

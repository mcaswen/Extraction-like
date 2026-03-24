using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class PixelDitherFeature : ScriptableRendererFeature
{
    [Header("=== 降采样 & 抖动参数 ===")]
    [Range(2, 32)]
    [Tooltip("降采样倍数（推荐 4~8）")]
    public int downSampleFactor = 6;

    [Header("=== Dither 材质 ===")]
    [Tooltip("必须手动创建 DitherBayer.mat 并拖入这里！\n" +
             "1. 右键 Create → Material\n" +
             "2. Shader 选 Hidden/PixelDitherBayer\n" +
             "3. 拖到这里（避免 mismatch）")]
    public Material ditherMaterial;

    private PixelDitherPass m_Pass;

    public override void Create()
    {
        m_Pass = new PixelDitherPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ditherMaterial == null || ditherMaterial.shader == null)
        {
            Debug.LogWarning("PixelDitherFeature：请先创建并拖入 DitherBayer.mat！");
            return;
        }

        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        m_Pass.Setup(downSampleFactor, ditherMaterial);
        m_Pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; // 放在后处理前，避免被其他 post 干扰
        renderer.EnqueuePass(m_Pass);
    }

    private class PixelDitherPass : ScriptableRenderPass
    {
        private int _downSampleFactor;
        private Material _ditherMat;
        private RTHandle _lowResRT;

        public void Setup(int factor, Material mat)
        {
            _downSampleFactor = Mathf.Max(2, factor);
            _ditherMat = mat;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.width  = Mathf.Max(1, desc.width  / _downSampleFactor);
            desc.height = Mathf.Max(1, desc.height / _downSampleFactor);

            RenderingUtils.ReAllocateIfNeeded(ref _lowResRT, desc, FilterMode.Bilinear, name: "_ObraDinnLowResRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Obra Dinn Dither");

            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 1. 降采样到低分辨率（Bilinear）
            Blitter.BlitCameraTexture(cmd, source, _lowResRT);

            // 关键：传递 downSampleFactor 给 Shader，用于 Bayer 坐标缩放
            cmd.SetGlobalFloat("_DownSampleFactor", _downSampleFactor);

            // 2. 用材质做 Bayer 抖动 + Point 放大回屏幕
            Blitter.BlitCameraTexture(cmd, _lowResRT, source, _ditherMat, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle 自动管理
        }
    }
}
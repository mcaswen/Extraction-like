#if UNITY_EDITOR || DEVELOPMENT_BUILD
using ExtractionLike.Aerospace;
using TMPro;
using UnityEngine;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 大脑面板的终端样式：与科普界面同名的颜色取相同的值（由测试钉住），字体沿用科普界面的三套字体资源。
    /// 科普界面本身不引用这里，保持原样。
    /// </summary>
    public static class AgentBrainTerminalStyle
    {
        // 与科普界面同名同值
        public static readonly Color Ink = new Color(.91f, .95f, .96f), Muted = new Color(.61f, .74f, .84f);
        public static readonly Color Accent = new Color(.48f, .90f, 1f), Teal = new Color(.32f, .83f, .90f);
        public static readonly Color Rule = new Color(.24f, .46f, .66f, .42f), ButtonFill = new Color(.065f, .125f, .205f, .92f);
        public static readonly Color SelectedFill = new Color(.075f, .225f, .36f, .97f);

        // 面板专用：用户确认的录制布局稿里的颜色
        // 稿子里是 0.94 的不透明度，但项目在线性空间混合，暗底压在亮场景上会透出很多；取 0.985 才和稿子观感一致
        public static readonly Color PanelFill = new Color(.035f, .063f, .09f, .985f);
        public static readonly Color Dim = new Color(.50f, .59f, .66f);
        public static readonly Color Amber = new Color(1f, .68f, .32f);
        public static readonly Color Success = new Color(.43f, .77f, .61f);
        public static readonly Color Failure = new Color(.88f, .58f, .55f);
        public static readonly Color Violet = new Color(.55f, .62f, 1f);
        public static readonly Color NodeFill = new Color(.05f, .086f, .118f, .95f);
        public static readonly Color NodeEdge = new Color(.24f, .46f, .66f, .6f);
        public static readonly Color EdgeLine = new Color(.61f, .74f, .84f, .6f);
        public static readonly Color Track = new Color(.106f, .165f, .208f, .9f);
        public static readonly Color FocusRow = new Color(.32f, .83f, .90f, .13f);
        public static readonly Color ActiveSlotFill = new Color(.075f, .227f, .36f, .6f);
        public static readonly Color HeldSlotFill = new Color(.106f, .165f, .208f, .7f);
        public static readonly Color Glow = new Color(.906f, .984f, 1f);

        public const int TitleSize = 17, SectionTitleSize = 15, BodySize = 14, SmallSize = 12, TinySize = 11;

        /// <summary>文字刷新周期，使用不受暂停影响的时钟</summary>
        public const float RefreshInterval = .1f;

        /// <summary>刚发生的转移保持高亮的时长</summary>
        public const float FiredTransitionSeconds = 3f;

        /// <summary>光点沿转移线走一遍的时长</summary>
        public const float PulseSeconds = .9f;

        private static AerospacePresentationAssets _assets;

        public static TMP_FontAsset BodyFont => Fonts() != null && _assets.inspectionBody != null ? _assets.inspectionBody : TMP_Settings.defaultFontAsset;
        public static TMP_FontAsset HeadingFont => Fonts() != null && _assets.inspectionHeading != null ? _assets.inspectionHeading : BodyFont;
        public static TMP_FontAsset MonoFont => Fonts() != null && _assets.inspectionMono != null ? _assets.inspectionMono : BodyFont;

        private static AerospacePresentationAssets Fonts()
        {
            if (_assets == null) _assets = Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation");
            return _assets;
        }
    }
}
#endif

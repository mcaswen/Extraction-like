#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using Style = Gameplay.Agent.Telemetry.UI.AgentBrainTerminalStyle;

namespace Gameplay.Agent.Telemetry.UI
{
    /// <summary>
    /// 演示模式的底部字幕条（录制布局稿里的样式）：左边"原理演示"、步骤号和步骤标题，右边解说；顶部细线按步骤推进。
    /// 由现场导演切换步骤；只在面板显示时可见，放在面板左右两栏之间、叠加层图例上方。
    /// </summary>
    public sealed partial class AgentBrainPanel
    {
        private const float CaptionX = 585, CaptionBottom = 58, CaptionWidth = 915, CaptionHeight = 100, CaptionSplit = 250;

        private RectTransform _caption;
        private AgentBrainVectorGraphic _captionVectors;
        private TMP_Text _captionStep, _captionTitle, _captionNarration;

        public bool IsDemoShown => _caption != null && _caption.gameObject.activeSelf;

        /// <summary>显示第 index 步（从 1 起）；count 为 0 时不显示步骤号和进度，用于中断等提示</summary>
        public void ShowDemoStep(int index, int count, string title, string narration)
        {
            if (_canvas == null) Build();
            if (_caption == null) BuildCaption();
            _caption.gameObject.SetActive(true);
            SetText(_captionStep, count > 0 ? index.ToString("00") + " / " + count.ToString("00") : string.Empty);
            SetText(_captionTitle, title);
            SetText(_captionNarration, narration);

            _captionVectors.Clear();
            _captionVectors.Outline(0, 0, CaptionWidth, CaptionHeight, Style.Rule);
            _captionVectors.Fill(0, 0, CaptionWidth, 2, Style.Track);
            if (count > 0) _captionVectors.Fill(0, 0, CaptionWidth * Mathf.Clamp01((float)index / count), 2, Style.Teal);
            _captionVectors.Fill(CaptionSplit, 18, 1, CaptionHeight - 36, Style.Rule);
            _captionVectors.Commit();
        }

        public void HideDemo()
        {
            if (_caption != null) _caption.gameObject.SetActive(false);
        }

        public string DemoTitle => _captionTitle != null ? _captionTitle.text : string.Empty;

        private void BuildCaption()
        {
            _caption = Panel(_canvas.transform, "Demo_Caption", Vector2.zero, Vector2.zero, Vector2.zero);
            _caption.pivot = Vector2.zero;
            _caption.anchoredPosition = new Vector2(CaptionX, CaptionBottom);
            _caption.sizeDelta = new Vector2(CaptionWidth, CaptionHeight);
            _captionVectors = Vectors(_caption, "Demo_Vectors", CaptionWidth, CaptionHeight);
            Label(_caption, "原理演示", 22, 14, 120, 18, Style.TinySize, Style.Muted).name = "Demo_Label";
            _captionStep = Label(_caption, string.Empty, 22, 32, 120, 24, Style.BodySize, Style.Accent, Style.MonoFont);
            _captionStep.name = "Demo_Step";
            _captionTitle = Label(_caption, string.Empty, 22, 58, CaptionSplit - 34, 30, 20, Style.Ink, Style.HeadingFont);
            _captionTitle.name = "Demo_Title";
            _captionNarration = Label(_caption, string.Empty, CaptionSplit + 22, 16, CaptionWidth - CaptionSplit - 44, CaptionHeight - 32,
                Style.BodySize, Style.Ink, null, TextAlignmentOptions.TopLeft);
            _captionNarration.enableWordWrapping = true;
            _captionNarration.lineSpacing = 12;
            _captionNarration.name = "Demo_Narration";
            _caption.gameObject.SetActive(false);
        }
    }
}
#endif

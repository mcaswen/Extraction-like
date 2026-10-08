using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceScienceUI
    {
        private void Build()
        {
            var go = new GameObject("Aerospace_Archive_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;

            // The existing out-of-modal archive entrance is deliberately unchanged.
            var hudPanel = Panel(canvas.transform, "Open_Aerospace_Archive", new Color(.075f, .145f, .169f), 22, 838, 270, 42);
            hudPanel.GetComponent<Image>().raycastTarget = true;
            var hudButton = hudPanel.gameObject.AddComponent<Button>(); hudButton.targetGraphic = hudPanel.GetComponent<Image>();
            hudButton.onClick.AddListener(() => Open(null, false)); hudButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var hudLabel = new GameObject("Archive_Entrance_Label", typeof(RectTransform), typeof(Text));
            hudLabel.transform.SetParent(hudPanel, false); hud = hudLabel.GetComponent<Text>(); hud.font = bodyFont;
            hud.text = "航天档案  本局 0 / 5   [J]"; hud.fontSize = 17; hud.color = new Color(.82f, .91f, .94f); hud.alignment = TextAnchor.MiddleCenter;
            hud.raycastTarget = false; hud.horizontalOverflow = HorizontalWrapMode.Wrap; hud.verticalOverflow = VerticalWrapMode.Overflow; hud.lineSpacing = 1.22f;
            Rect(hud.rectTransform, 6, 0, 258, 42);
            var hudColors = hudButton.colors; hudColors.highlightedColor = new Color(.52f, .9f, .86f);
            hudColors.pressedColor = new Color(.3f, .68f, .67f); hudColors.disabledColor = new Color(.4f, .43f, .45f); hudButton.colors = hudColors;
            hudPanel.anchorMin = hudPanel.anchorMax = Vector2.zero; hudPanel.anchoredPosition = new Vector2(22, 62);

            var grantPanel = Panel(canvas.transform, "Grant_Five_Ship_Parts", new Color(.075f, .145f, .169f), 22, 0, 320, 42);
            grantPanel.anchorMin = grantPanel.anchorMax = Vector2.zero;
            grantPanel.anchoredPosition = new Vector2(22, 114);
            var grantImage = grantPanel.GetComponent<Image>(); grantImage.raycastTarget = true;
            var grantButton = grantPanel.gameObject.AddComponent<Button>(); grantButton.targetGraphic = grantImage;
            grantButton.navigation = new Navigation { mode = Navigation.Mode.None }; grantButton.colors = hudButton.colors;
            var grantLabel = Instantiate(hud, grantPanel, false); grantLabel.name = "Grant_Parts_Label";
            grantLabel.text = "领取五个飞船零件"; Rect(grantLabel.rectTransform, 6, 0, 308, 42);
            var grantAction = grantPanel.gameObject.AddComponent<AerospacePartsGrantButton>();
            grantAction.Initialize(grantLabel); grantButton.onClick.AddListener(grantAction.GrantParts);

            modal = Panel(canvas.transform, "Science_Modal", new Color(.025f, .047f, .073f), 0, 0, 1600, 900).gameObject;
            modal.GetComponent<Image>().raycastTarget = true;
            var full = modal.GetComponent<RectTransform>(); full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            var frame = Panel(modal.transform, "Responsive_Frame", Color.clear, 0, 0, 1600, 900);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            contentGroup = frame.gameObject.AddComponent<CanvasGroup>();
            var background = new GameObject("Graphite_Inspection_Surface", typeof(RectTransform), typeof(AerospaceInspectionBackdrop));
            background.transform.SetParent(frame, false); Rect(background.GetComponent<RectTransform>(), 0, 0, 1600, 900);
            background.GetComponent<AerospaceInspectionBackdrop>().raycastTarget = false;

            Panel(frame, "Header_Accent", Amber, 48, 32, 28, 3);
            Label(frame, "航天样本分析终端", 90, 21, 240, 26, 16, Ink);
            MonoLabel(frame, "ASTRA / FIELD LAB", 321, 25, 480, 22, 12, Teal);
            heading = Label(frame, "航天样本档案", 46, 54, 1040, 53, 34, Ink); heading.font = font;
            english = Label(frame, "", 49, 108, 1000, 25, 16, Muted);
            progress = Label(frame, "本局收集   0 / 5", 1120, 60, 190, 28, 17, Ink);
            for (int i = 0; i < 5; i++) progressMarks.Add(Panel(frame, "Collection_" + i, Rule, 1120 + i * 30, 99, 22, 3).GetComponent<Image>());
            var close = Button(frame, "返回游戏   [Esc]", 1348, 49, 204, 48, Close);
            close.name = "Close_Inspection"; close.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            closeLabel = close.GetComponentInChildren<TMP_Text>();
            Panel(frame, "Header_Rule", Rule, 48, 139, 1504, 1);

            viewport = new GameObject("Model_Viewport", typeof(RectTransform), typeof(RawImage), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(frame, false); Rect(viewport, 48, 145, 1024, 580);
            modelImage = viewport.GetComponent<RawImage>(); modelImage.color = new Color(1, 1, 1, 0);
            viewport.gameObject.AddComponent<AerospaceOrbitInput>().stage = stage;
            BuildTerminal(frame);
            MonoLabel(viewport, "SPECIMEN", 20, 21, 120, 20, 11, Teal);
            sampleId = MonoLabel(viewport, "— / 05", 20, 42, 170, 38, 23, Ink);
            // Reference ticks are visual locators, not fabricated dimensions or measurements.
            for (int i = 0; i < 7; i++)
            {
                Panel(viewport, "Left_Locator_" + i, new Color(.43f, .50f, .50f, .25f), 6, 150 + i * 47, i % 3 == 0 ? 9 : 4, 1);
                Panel(viewport, "Right_Locator_" + i, new Color(.43f, .50f, .50f, .25f), 1008, 150 + i * 47, i % 3 == 0 ? 9 : 4, 1);
            }
            loading = Label(viewport, "选择已收集的样本", 160, 244, 704, 100, 23, Ink, TextAlignmentOptions.Center);
            resetButton = Button(frame, "重置视角", 944, 159, 112, 32, () => { stage.ResetView(); ShowDetail(-1); });
            resetButton.name = "Reset_View"; resetButton.GetComponentInChildren<TMP_Text>().fontSize = 14;
            Label(frame, "左键拖动旋转    /    滚轮缩放    /    点击编号解读结构", 238, 701, 724, 23, 14, Muted, TextAlignmentOptions.Center);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var line = new GameObject("Hotspot_Leader_" + (i + 1), typeof(RectTransform), typeof(AerospaceHotspotLeader));
                line.transform.SetParent(viewport, false); Rect(line.GetComponent<RectTransform>(), 0, 0, 1024, 580);
                var leader = line.GetComponent<AerospaceHotspotLeader>(); leader.raycastTarget = false; leaders.Add(leader);
            }
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var pin = Button(viewport, (i + 1).ToString("00"), 22, 150 + 80 * i, 166, 36, () => ShowDetail(index));
                pin.name = "Hotspot_" + (i + 1);
                var number = pin.GetComponentInChildren<TMP_Text>(); number.font = monoFont; number.fontSize = 15;
                Rect(number.rectTransform, 4, 0, 32, 36); number.alignment = TextAlignmentOptions.Center;
                var name = Label(pin.transform, "", 41, 0, 119, 36, 15, Ink, TextAlignmentOptions.Left);
                pinNames.Add(name);
                pin.gameObject.AddComponent<AerospaceHotspotPointer>().hover = value => { if (value) hoveredPin = index; else if (hoveredPin == index) hoveredPin = -1; };
                pins.Add(pin);
                var dot = Button(viewport, "", 0, 0, 24, 24, () => ShowDetail(index)); dot.name = "Anchor_" + (i + 1);
                dot.GetComponent<Image>().color = Color.clear;
                dot.gameObject.AddComponent<AerospaceHotspotPointer>().hover = value => { if (value) hoveredPin = index; else if (hoveredPin == index) hoveredPin = -1; };
                anchorButtons.Add(dot);
            }

            controls = new GameObject("Model_Controls", typeof(RectTransform)); controls.transform.SetParent(frame, false);
            Rect(controls.GetComponent<RectTransform>(), 48, 738, 1024, 44);
            string[] titles = { "完整结构", "教学剖面", "分解观察" }, modes = { "assembled", "cutaway", "exploded" };
            for (int i = 0; i < 3; i++)
            {
                string mode = modes[i]; var b = Button(controls.transform, titles[i], i * 138, 0, 134, 42, () => Mode(mode));
                b.name = "Mode_" + mode; modeButtons.Add(b);
            }
            Panel(controls.transform, "Tool_Divider", Rule, 435, 8, 1, 26);
            foldButton = Button(controls.transform, "", 456, 0, 196, 42, ModelSpecialAction);
            foldButton.name = "Special_Observation"; foldText = foldButton.GetComponentInChildren<TMP_Text>();
            demoButton = Button(controls.transform, "原理演示  ›", 674, 0, 177, 42, StartDemonstration);
            demoButton.name = "Start_Demonstration"; demoButton.GetComponentInChildren<TMP_Text>().color = Teal;
            settingsButton = Button(controls.transform, "展示设置  ···", 868, 0, 156, 42, () => SetSettings(!IsSettingsOpen));
            settingsButton.name = "Display_Settings";

            var details = Panel(frame, "Science_Reading_Panel", new Color(.052f, .087f, .12f, .96f), 1104, 155, 448, 627);
            Panel(details, "Reading_Edge", Rule, 0, 0, 1, 627);
            Panel(details, "Analysis_Accent", Teal, 0, 0, 3, 30);
            subtitle = Label(details, "分析结果", 24, 9, 260, 28, 15, Teal);
            MonoLabel(details, "ANALYSIS", 329, 13, 100, 22, 11, Muted);
            for (int i = 0; i < 3; i++)
            {
                int index = i; var tab = Button(details, new[] { "整体介绍", "工作原理", "结构细节" }[i], 24 + 132 * i, 50, 128, 42, () => SetReadingTab(index));
                tab.name = "Reading_Tab_" + i; readingTabs.Add(tab);
            }
            var scroll = Panel(details, "Reading_Scroll", Color.clear, 24, 118, 397, 415);
            scroll.GetComponent<Image>().raycastTarget = true;
            readingScroll = scroll.gameObject.AddComponent<ScrollRect>(); readingScroll.horizontal = false; readingScroll.scrollSensitivity = 24;
            scroll.gameObject.AddComponent<RectMask2D>();
            detailContent = new GameObject("Reading_Content", typeof(RectTransform)).GetComponent<RectTransform>(); detailContent.SetParent(scroll, false);
            detailContent.anchorMin = new Vector2(0, 1); detailContent.anchorMax = Vector2.one; detailContent.pivot = new Vector2(.5f, 1);
            detailContent.sizeDelta = new Vector2(0, 415);
            readingReveal = detailContent.gameObject.AddComponent<CanvasGroup>();
            readingScroll.viewport = scroll; readingScroll.content = detailContent; readingScroll.movementType = ScrollRect.MovementType.Clamped;
            var barRect = Panel(details, "Reading_Scrollbar", new Color(.34f, .39f, .40f, .15f), 432, 118, 3, 415);
            var bar = barRect.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Panel(barRect, "Handle", new Color(.59f, .64f, .64f, .55f), 0, 0, 3, 90);
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = bar; readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Panel(details, "Footnote_Rule", Rule, 24, 552, 397, 1);
            sourceButton = Button(details, "资料说明与来源  ↗", 16, 568, 228, 30, ShowSources);
            sourceButton.name = "Science_Sources"; sourceButton.GetComponentInChildren<TMP_Text>().fontSize = 14;
            var clues = Button(details, "搜集线索", 294, 568, 134, 30, ShowCollectionClues);
            clues.name = "Collection_Clues"; clues.GetComponentInChildren<TMP_Text>().fontSize = 14;
            Label(details, "教学示意  /  非制造图纸", 24, 606, 390, 21, 12, Muted);

            Panel(frame, "Archive_Divider", Rule, 48, 801, 1504, 1);
            Label(frame, "回收样本库", 48, 817, 150, 30, 18, Ink);
            MonoLabel(frame, "ARCHIVE / 05", 48, 854, 150, 20, 10, Teal);
            foreach (var part in AerospaceCatalog.Load().models)
            {
                string code = part.code; int i = cards.Count;
                var card = Button(frame, "", 206 + i * 272, 815, 264, 66, () => Open(code, false)); card.name = "Archive_" + code;
                var emptyLabel = card.GetComponentInChildren<TMP_Text>(); emptyLabel.gameObject.SetActive(false);
                var icon = Panel(card.transform, "Icon", Color.white, 6, 5, 55, 55).GetComponent<Image>();
                icon.sprite = Resources.Load<Sprite>("Aerospace/Icons/" + code); icon.preserveAspect = true; icon.raycastTarget = false; cardIcons.Add(icon);
                cardStates.Add(Label(card.transform, "", 72, 6, 188, 23, 12, Muted));
                cardTitles.Add(Label(card.transform, "", 72, 29, 190, 31, 17, Ink));
                cardRules.Add(Panel(card.transform, "Current_Sample_Rule", Color.clear, 0, 0, 2, 66).GetComponent<Image>());
                cards.Add(card);
            }

            settingsOverlay = Panel(frame, "Display_Settings_Overlay", Color.clear, 0, 0, 1600, 900).gameObject;
            settingsOverlay.GetComponent<Image>().raycastTarget = true;
            var dismiss = settingsOverlay.AddComponent<Button>(); dismiss.transition = Selectable.Transition.None; dismiss.onClick.AddListener(() => SetSettings(false));
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            var settings = Panel(settingsOverlay.transform, "Settings_Panel", new Color(.07f, .12f, .17f, .99f), 746, 462, 326, 264);
            settings.GetComponent<Image>().raycastTarget = true;
            settings.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            Panel(settings, "Settings_Accent", Amber, 0, 0, 326, 2);
            Label(settings, "展示设置", 20, 16, 270, 26, 19, Ink).font = font;
            var surface = Button(settings, "表面状态   清理后样件", 16, 57, 294, 40, () => { stage.ToggleSurface(); RefreshButtons(); });
            surface.name = "Toggle_Surface"; surfaceText = surface.GetComponentInChildren<TMP_Text>();
            var q = Button(settings, "贴图清晰度   2K", 16, 106, 294, 40, ToggleQuality);
            q.name = "Toggle_Quality"; qualityText = q.GetComponentInChildren<TMP_Text>();
            var motion = Button(settings, "减少动态效果   关", 16, 155, 294, 40, ToggleReducedMotion);
            motion.name = "Toggle_Reduced_Motion"; motionText = motion.GetComponentInChildren<TMP_Text>();
            Label(settings, "仅影响检视窗口。减少动态时，原理演示默认停在第一步，可手动播放。", 20, 210, 288, 45, 13, Muted);
            settingsOverlay.SetActive(false);
            transitionRect = Panel(frame, "Discovery_Hero_Icon", Color.white, 420, 68, 760, 760);
            transitionImage = transitionRect.GetComponent<Image>(); transitionImage.preserveAspect = true; transitionImage.raycastTarget = false;
            transitionRect.gameObject.SetActive(false);
            skipButton.transform.SetAsLastSibling();
            close.transform.SetAsLastSibling(); // Closing is always reachable, including during first-pickup reveal.
            modal.SetActive(false); RefreshArchive(); RefreshButtons(); HidePins();
        }

        private void RenderReading()
        {
            readingAge = 0;
            for (int i = detailContent.childCount - 1; i >= 0; i--)
            {
                var child = detailContent.GetChild(i).gameObject; child.SetActive(false); Destroy(child);
            }
            float y = 0;
            subtitle.text = current != null ? "分析结果   /   " + current.code : "分析结果";
            if (readingTab == 4) RenderClues(ref y);
            else if (current != null)
            {
                var copy = AerospaceScienceCopy.For(current.code);
                for (int i = 0; i < pinNames.Count; i++) pinNames[i].text = ShortHotspot(current.code, i);
                if (readingTab == 0)
                {
                    ReadText("组件概览 / " + copy.system, ref y, 13, Teal, 12);
                    ReadText(copy.shortName, ref y, 28, Ink, 14, true, "Overview_Question");
                    ReadText(copy.summary, ref y, 20, Ink, 18, false, "Overview_Summary");
                    AddLocationDiagram(ref y);
                    Panel(detailContent, "Key_Idea_Accent", Amber, 0, y + 3, 2, 48);
                    var note = Label(detailContent, copy.takeaway, 17, y, 365, 68, 17, Muted);
                    float noteHeight = Mathf.Max(48, note.preferredHeight + 4); note.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, noteHeight); y += noteHeight + 20;
                    var explain = Button(detailContent, "观看原理演示   ›", 0, y, 383, 42, StartDemonstration); explain.name = "Overview_Demonstration"; y += 54;
                }
                else if (readingTab == 1)
                {
                    ReadText("工作原理", ref y, 13, Amber, 16);
                    ReadText("从结构，理解作用", ref y, 26, Ink, 27, true);
                    var play = Button(detailContent, "播放三维原理演示   ›", 0, y, 383, 44, StartDemonstration); play.name = "Principle_Demonstration"; y += 66;
                    for (int i = 0; i < copy.steps.Length; i++)
                    {
                        MonoLabel(detailContent, (i + 1).ToString("00"), 0, y + 3, 38, 28, 16, Amber);
                        Label(detailContent, copy.stepTitles[i], 45, y, 337, 30, 21, Ink).font = font; y += 42;
                        ReadText(copy.steps[i], ref y, 19, Ink, 28);
                    }
                }
                else if (readingTab == 2)
                {
                    bool focused = selectedHotspot >= 0;
                    ReadText(focused ? "局部结构   /   " + (selectedHotspot + 1).ToString("00") + " · 04" : "点击编号，对照模型观察", ref y, 14, Muted, 18);
                    for (int i = 0; i < current.hotspots.Length; i++)
                    {
                        int index = i;
                        string caption = (i + 1).ToString("00") + (focused ? "" : "   " + current.hotspots[i].title);
                        var b = Button(detailContent, caption, focused ? i * 98 : 0, y, focused ? 89 : 383, 42, () => ShowDetail(index));
                        b.name = "Detail_Selector_" + i;
                        if (!focused)
                        {
                            b.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Left;
                            Rect(b.GetComponentInChildren<TMP_Text>().rectTransform, 13, 0, 367, 42);
                        }
                        StyleButton(b, selectedHotspot == i); if (!focused) y += 48;
                    }
                    if (focused) y += 48;
                    y += 23;
                    if (selectedHotspot >= 0)
                    {
                        ReadText(current.hotspots[selectedHotspot].title, ref y, 24, Ink, 17, true);
                        ReadText(current.hotspots[selectedHotspot].text, ref y, 19, Ink, 18, false, "Hotspot_Explanation");
                        var demo = Button(detailContent, "查看关联原理演示   ›", 0, y, 383, 40, StartDemonstration); demo.name = "Hotspot_Demonstration"; y += 52;
                    }
                    else ReadText("选择一个部位，镜头将靠近对应结构。模型旁的标记也可以直接点击。", ref y, 19, Muted, 16);
                    if (focused)
                    {
                        Button(detailContent, "查看全部部位名称", 0, y, 383, 36, () => { selectedHotspot = -1; stage.Select(-1); SetReadingTab(2); }); y += 46;
                    }
                    var back = Button(detailContent, "←  返回整体介绍", 0, y, 383, 40, () => ShowDetail(-1)); back.name = "Back_To_Overview"; y += 54;
                }
                else if (readingTab == 3)
                {
                    ReadText("资料说明", ref y, 26, Ink, 20, true);
                    ReadText("本界面展示游戏化教学样件，不是实物复刻或制造图纸。", ref y, 19, Ink, 18);
                    ReadText(current.intro, ref y, 17, Muted, 28);
                    ReadText("参考来源", ref y, 18, Ink, 14, true);
                    if (current.sources != null)
                    for (int i = 0; i < current.sources.Length; i++)
                    {
                        int index = i;
                        var b = Button(detailContent, current.sources[i].label + "  ↗", 0, y, 383, 58, () => OpenSource(index));
                        b.GetComponentInChildren<TMP_Text>().fontSize = 16; y += 68;
                    }
                    ReadText("链接将在浏览器中打开。", ref y, 13, Muted, 16);
                }
                else if (readingTab == 5)
                {
                    ReadText("结构演示  /  教学示意", ref y, 13, Amber, 17);
                    ReadText(specialTitle, ref y, 27, Ink, 20, true);
                    ReadText(specialBody, ref y, 19, Ink, 22);
                    Button(detailContent, "←  返回整体介绍", 0, y, 383, 40, () => ShowDetail(-1)); y += 54;
                }
                else if (readingTab == 6) RenderDemonstration(ref y);
            }
            detailContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(415, y + 8));
            detailContent.anchoredPosition = Vector2.zero; readingScroll.velocity = Vector2.zero; readingScroll.verticalNormalizedPosition = 1;
        }

        private void RenderClues(ref float y)
        {
            ReadText("本局搜集线索", ref y, 26, Ink, 21, true);
            ReadText("首个新箱必出样本，后续沿探索路线间隔出现。重复开箱不推进进度；成功放入背包才解锁档案。", ref y, 19, Ink, 24);
            ReadText("已探索不同箱子：" + owner.SearchedBoxCount + "   已投放样本：" + owner.PlacedPartCount + "/5", ref y, 16, Teal, 20);
            foreach (var reservation in owner.Reservations)
            {
                if (!reservation.placed)
                {
                    ReadText(reservation.code + "   待发现", ref y, 18, Muted, 7, true);
                    ReadText("第 " + reservation.searchNumber + " 个新箱保底出现（两名 Agent 共享进度）", ref y, 16, Muted, 20);
                    continue;
                }
                string zone = reservation.cluster != null ? reservation.cluster.name : "资源区";
                for (var p = reservation.cluster != null ? reservation.cluster.transform : null; p != null; p = p.parent)
                    if (p.name.StartsWith("Zone", StringComparison.Ordinal)) { zone = p.name; break; }
                ReadText(reservation.code + (owner.HasCollected(reservation.code) ? "   本局已收集" : "   待收集"), ref y, 18, owner.HasCollected(reservation.code) ? Teal : Ink, 7, true);
                ReadText(zone + " / " + (reservation.box != null ? reservation.box.BoxName : "搜索点不可用"), ref y, 16, Muted, 20);
            }
            if (!owner.IsReady) ReadText(owner.Status, ref y, 16, Muted, 18);
            ReadText("每件占 1 格。满包时请先腾出空间；档案会保留，之后可以再次查看。", ref y, 16, Muted, 20);
        }

        private void ReadText(string text, ref float y, int size, Color color, float after, bool headingStyle = false, string objectName = null)
        {
            var label = Label(detailContent, text, 0, y, 383, 100, size, color);
            if (headingStyle) { label.font = font; label.lineSpacing = 2; }
            if (objectName != null) label.name = objectName;
            float h = Mathf.Ceil(label.GetPreferredValues(text, 383, Mathf.Infinity).y) + 5;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h); y += h + after;
        }

        private RectTransform Panel(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            var r = (RectTransform)go.transform; Rect(r, x, y, w, h); return r;
        }
        private TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(text.Length > 28 ? text.Substring(0, 28) : string.IsNullOrEmpty(text) ? "Label" : text, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = bodyTypeface; label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment;
            label.raycastTarget = false; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; label.lineSpacing = 4; label.paragraphSpacing = 8; label.extraPadding = true; label.richText = false;
            Rect(label.rectTransform, x, y, w, h); return label;
        }
        private TMP_Text MonoLabel(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        {
            var label = Label(parent, text, x, y, w, h, size, color); label.font = monoFont; return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float w, float h, Action clicked)
        {
            var panel = Panel(parent, "Button_" + text, ButtonFill, x, y, w, h);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(() => clicked());
            var colors = button.colors; colors.highlightedColor = new Color(1.3f, 1.3f, 1.25f); colors.pressedColor = new Color(.76f, .78f, .76f);
            colors.selectedColor = Color.white; colors.disabledColor = new Color(.40f, .43f, .43f, .65f); colors.fadeDuration = .12f; button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(panel, text, 8, 0, w - 16, h, 17, Ink, TextAlignmentOptions.Center);
            return button;
        }
        private void StyleButton(Button button, bool selected, bool tab = false)
        {
            button.GetComponent<Image>().color = selected ? new Color(.22f, .23f, .22f, .97f) : tab ? new Color(.09f, .14f, .18f, .50f) : ButtonFill;
            var label = button.GetComponentInChildren<TMP_Text>(); if (label != null) label.color = selected ? Amber : Muted;
            var line = button.transform.Find("Selection_Rule");
            if (line == null)
            {
                var r = button.GetComponent<RectTransform>();
                line = Panel(button.transform, "Selection_Rule", Color.clear, 0, r.rect.height - 2, r.rect.width, 2);
            }
            line.GetComponent<Image>().color = selected ? Amber : Color.clear;
        }
        private static void Rect(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        }
    }
}

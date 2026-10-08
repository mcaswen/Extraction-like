using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceScienceUI
    {
        private const float ReadingWidth = 383, ReadingHeight = 224;
        private void Build()
        {
            var go = new GameObject("Aerospace_Archive_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 29000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;

            // The out-of-modal entrance and all collection behaviour remain unchanged.
            var hudPanel = Panel(canvas.transform, "Open_Aerospace_Archive", new Color(.075f, .145f, .169f), 22, 838, 270, 42);
            hudPanel.GetComponent<Image>().raycastTarget = true;
            var hudButton = hudPanel.gameObject.AddComponent<Button>(); hudButton.targetGraphic = hudPanel.GetComponent<Image>();
            hudButton.onClick.AddListener(() => Open(null, false)); hudButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var hudLabel = new GameObject("Archive_Entrance_Label", typeof(RectTransform), typeof(Text)); hudLabel.transform.SetParent(hudPanel, false);
            hud = hudLabel.GetComponent<Text>(); hud.font = bodyFont; hud.text = "航天档案  本局 0 / 5   [J]"; hud.fontSize = 17;
            hud.color = new Color(.82f, .91f, .94f); hud.alignment = TextAnchor.MiddleCenter; hud.raycastTarget = false;
            hud.horizontalOverflow = HorizontalWrapMode.Wrap; hud.verticalOverflow = VerticalWrapMode.Overflow; hud.lineSpacing = 1.22f;
            Rect(hud.rectTransform, 6, 0, 258, 42);
            var hudColors = hudButton.colors; hudColors.highlightedColor = new Color(.52f, .9f, .86f); hudColors.pressedColor = new Color(.3f, .68f, .67f);
            hudColors.disabledColor = new Color(.4f, .43f, .45f); hudButton.colors = hudColors;
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

            modal = Panel(canvas.transform, "Science_Modal", new Color(.12f, .145f, .09f), 0, 0, 1600, 900).gameObject;
            modal.GetComponent<Image>().raycastTarget = true;
            var full = modal.GetComponent<RectTransform>(); full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            var frame = Panel(modal.transform, "Responsive_Frame", Color.clear, 0, 0, 1600, 900);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            contentGroup = frame.gameObject.AddComponent<CanvasGroup>();
            var background = new GameObject("Olive_Inspection_Surface", typeof(RectTransform), typeof(AerospaceInspectionBackdrop));
            background.transform.SetParent(frame, false); Rect(background.GetComponent<RectTransform>(), 448, 0, 1152, 900);
            background.GetComponent<AerospaceInspectionBackdrop>().raycastTarget = false;
            watermark = MonoLabel(frame, "", 1086, 286, 490, 320, 252, new Color(.60f, .65f, .43f, .09f)); watermark.name = "Specimen_Watermark";
            watermark.enableWordWrapping = false;

            BuildDossier(frame);
            BuildArchiveRail(frame);
            var close = Button(frame, "返回游戏   [Esc]", 1392, 23, 184, 42, Close); close.name = "Close_Inspection";
            close.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true; closeLabel = close.GetComponentInChildren<TMP_Text>(); closeLabel.fontSize = 15;
            Panel(frame, "Header_Rule", Rule, 448, 94, 1152, 1);

            viewport = new GameObject("Model_Viewport", typeof(RectTransform), typeof(RawImage), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(frame, false); Rect(viewport, 448, 108, AerospaceInspectionStage.ViewWidth, AerospaceInspectionStage.ViewHeight);
            modelImage = viewport.GetComponent<RawImage>(); modelImage.color = new Color(1, 1, 1, 0);
            viewport.gameObject.AddComponent<AerospaceOrbitInput>().stage = stage;
            BuildTerminal(frame);
            Label(frame, "回收样本检视", 477, 120, 240, 24, 13, Muted);
            surfaceState = Label(frame, "清理后样件", 1424, 120, 150, 24, 13, Muted, TextAlignmentOptions.TopRight); surfaceState.name = "Specimen_Surface_State";
            loading = Label(viewport, "选择已收集的样本", 214, 262, 724, 110, 24, Ink, TextAlignmentOptions.Center);
            BuildHotspots();
            BuildModelControls(frame);
            BuildSettings(frame);

            transitionRect = Panel(frame, "Discovery_Hero_Icon", Color.white, 610, 40, 820, 820);
            transitionImage = transitionRect.GetComponent<Image>(); transitionImage.preserveAspect = true; transitionImage.raycastTarget = false;
            transitionRect.gameObject.SetActive(false); skipButton.transform.SetAsLastSibling(); close.transform.SetAsLastSibling();
            modal.SetActive(false); RefreshArchive(); RefreshButtons(); HidePins();
        }

        private void BuildDossier(Transform frame)
        {
            readingPane = Panel(frame, "Science_Reading_Panel", Paper, 0, 0, 448, 900);
            var brand = Label(readingPane, "ASTRA", 32, 21, 153, 48, 38, PaperInk); brand.font = font;
            Label(readingPane, "/", 181, 27, 28, 38, 31, PaperMuted);
            Label(readingPane, "航天样本档案", 211, 32, 211, 29, 18, PaperInk);
            MonoLabel(readingPane, "FIELD ARCHIVE  /  AEROSPACE", 33, 73, 385, 20, 10, PaperMuted);
            Panel(readingPane, "Paper_Header_Rule", Rule, 32, 104, 383, 1);
            Panel(readingPane, "Category_Mark", PaperMuted, 33, 136, 5, 5);
            subtitle = Label(readingPane, "航天系统", 49, 127, 182, 24, 15, PaperMuted);
            Panel(readingPane, "Category_Rule", Rule, 184, 138, 31, 1);
            Label(readingPane, "回收样件", 232, 127, 176, 24, 15, PaperMuted);
            heading = Label(readingPane, "航天样本\n档案", 32, 162, 383, 109, 42, PaperInk); heading.font = font; heading.lineSpacing = -1;
            heading.name = "Specimen_Title";
            sampleId = MonoLabel(readingPane, "—", 33, 282, 86, 43, 35, PaperInk);
            Panel(readingPane, "Identity_Divider", Rule, 118, 283, 1, 37);
            english = MonoLabel(readingPane, "AEROSPACE COMPONENTS", 134, 283, 278, 42, 11, PaperMuted); english.characterSpacing = 1;
            for (int i = 0; i < 3; i++)
            {
                int index = i; var tab = Button(readingPane, new[] { "整体介绍", "工作原理", "结构细节" }[i], 32 + i * 128, 339, 128, 47, () => SetReadingTab(index));
                tab.name = "Reading_Tab_" + i; tab.GetComponentInChildren<TMP_Text>().fontSize = 16; readingTabs.Add(tab);
            }
            var scroll = Panel(readingPane, "Reading_Scroll", Color.clear, 32, 408, 383, ReadingHeight);
            scroll.GetComponent<Image>().raycastTarget = true; readingScroll = scroll.gameObject.AddComponent<ScrollRect>();
            readingScroll.horizontal = false; readingScroll.scrollSensitivity = 24; scroll.gameObject.AddComponent<RectMask2D>();
            detailContent = new GameObject("Reading_Content", typeof(RectTransform)).GetComponent<RectTransform>(); detailContent.SetParent(scroll, false);
            detailContent.anchorMin = new Vector2(0, 1); detailContent.anchorMax = Vector2.one; detailContent.pivot = new Vector2(.5f, 1);
            detailContent.sizeDelta = new Vector2(0, ReadingHeight); readingReveal = detailContent.gameObject.AddComponent<CanvasGroup>();
            readingScroll.viewport = scroll; readingScroll.content = detailContent; readingScroll.movementType = ScrollRect.MovementType.Clamped;
            var barRect = Panel(readingPane, "Reading_Scrollbar", new Color(.36f, .40f, .27f, .1f), 426, 408, 3, ReadingHeight);
            var bar = barRect.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Panel(barRect, "Handle", new Color(.42f, .47f, .30f, .65f), 0, 0, 3, 80);
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.navigation = new Navigation { mode = Navigation.Mode.None };
            readingScroll.verticalScrollbar = bar; readingScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var locator = Panel(frame, "Rocket_System_Locator", new Color(.19f, .225f, .145f), 0, 650, 448, 172);
            Panel(locator, "Locator_Top_Rule", Rule, 0, 0, 448, 1);
            var diagram = new GameObject("Rocket_Location_Diagram", typeof(RectTransform), typeof(AerospaceTerminalGraphic)); diagram.transform.SetParent(locator, false);
            Rect(diagram.GetComponent<RectTransform>(), 27, 11, 83, 150); locationGraphic = diagram.GetComponent<AerospaceTerminalGraphic>(); locationGraphic.locationCode = "R01"; locationGraphic.raycastTarget = false;
            MonoLabel(locator, "LOCATION / SYSTEM", 130, 18, 290, 17, 10, Muted);
            locationTitle = Label(locator, "航天系统", 130, 47, 289, 32, 21, Ink); locationTitle.font = font;
            locationBody = Label(locator, "", 130, 85, 286, 43, 14, Ink); locationBody.lineSpacing = 2;
            locationTag = Label(locator, "", 144, 143, 274, 23, 13, Amber); Panel(locator, "Current_Location_Mark", Amber, 131, 150, 5, 5);
            sourceButton = Button(readingPane, "科普资料  ↗", 24, 833, 192, 32, ShowSources); sourceButton.name = "Science_Sources";
            sourceButton.GetComponentInChildren<TMP_Text>().fontSize = 15;
            var clues = Button(readingPane, "搜集线索", 277, 833, 146, 32, ShowCollectionClues); clues.name = "Collection_Clues"; clues.GetComponentInChildren<TMP_Text>().fontSize = 15;
            progress = Label(readingPane, "本局收集   0 / 5", 33, 875, 240, 22, 13, PaperMuted);
            for (int i = 0; i < 5; i++) progressMarks.Add(Panel(readingPane, "Collection_" + i, Rule, 303 + i * 24, 882, 16, 3).GetComponent<Image>());
        }

        private void BuildArchiveRail(Transform frame)
        {
            Panel(frame, "Archive_Header", new Color(.13f, .155f, .10f), 448, 0, 1152, 94);
            foreach (var part in AerospaceCatalog.Load().models)
            {
                string code = part.code; int i = cards.Count;
                var card = Button(frame, "", 472 + i * 182, 12, 174, 70, () => Open(code, false)); card.name = "Archive_" + code;
                card.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
                var icon = Panel(card.transform, "Icon", Color.white, 5, 6, 43, 57).GetComponent<Image>();
                icon.sprite = Resources.Load<Sprite>("Aerospace/Icons/" + code); icon.preserveAspect = true; icon.raycastTarget = false; cardIcons.Add(icon);
                cardStates.Add(Label(card.transform, "", 52, 10, 120, 22, 11, Muted));
                cardTitles.Add(Label(card.transform, "", 52, 35, 120, 28, 15, Ink));
                cardRules.Add(Panel(card.transform, "Current_Sample_Rule", Color.clear, 0, 68, 174, 2).GetComponent<Image>()); cards.Add(card);
            }
        }

        private void BuildHotspots()
        {
            for (int i = 0; i < 4; i++)
            {
                var line = new GameObject("Hotspot_Leader_" + (i + 1), typeof(RectTransform), typeof(AerospaceHotspotLeader));
                line.transform.SetParent(viewport, false); Rect(line.GetComponent<RectTransform>(), 0, 0, viewport.rect.width, viewport.rect.height);
                var leader = line.GetComponent<AerospaceHotspotLeader>(); leader.raycastTarget = false; leaders.Add(leader);
            }
            for (int i = 0; i < 4; i++)
            {
                int index = i; var pin = Button(viewport, (i + 1).ToString("00"), 24, 150 + 80 * i, 42, 42, () => ShowDetail(index)); pin.name = "Hotspot_" + (i + 1);
                var number = pin.GetComponentInChildren<TMP_Text>(); number.font = monoFont; number.fontSize = 15;
                Rect(number.rectTransform, 0, 0, 42, 42); number.alignment = TextAlignmentOptions.Center;
                pinNames.Add(Label(pin.transform, "", 45, 0, 126, 42, 15, PaperInk, TextAlignmentOptions.Left));
                pin.gameObject.AddComponent<AerospaceHotspotPointer>().hover = value => { if (value) hoveredPin = index; else if (hoveredPin == index) hoveredPin = -1; };
                pins.Add(pin);
                var dot = Button(viewport, "", 0, 0, 24, 24, () => ShowDetail(index)); dot.name = "Anchor_" + (i + 1); dot.GetComponent<Image>().color = Color.clear;
                dot.gameObject.AddComponent<AerospaceHotspotPointer>().hover = value => { if (value) hoveredPin = index; else if (hoveredPin == index) hoveredPin = -1; }; anchorButtons.Add(dot);
            }
        }

        private void BuildModelControls(Transform frame)
        {
            Panel(frame, "Toolbar_Background", new Color(.145f, .17f, .105f), 448, 798, 1152, 102); Panel(frame, "Toolbar_Rule", Rule, 448, 798, 1152, 1);
            controls = new GameObject("Model_Controls", typeof(RectTransform)); controls.transform.SetParent(frame, false); Rect(controls.GetComponent<RectTransform>(), 472, 815, 1104, 76);
            string[] titles = { "完整结构", "结构剖面", "分解观察" }, modes = { "assembled", "cutaway", "exploded" };
            for (int i = 0; i < 3; i++)
            {
                string mode = modes[i]; var b = Button(controls.transform, titles[i], i * 138, 0, 130, 44, () => Mode(mode)); b.name = "Mode_" + mode; modeButtons.Add(b);
                AddButtonGlyph(b, i == 0 ? "cube" : i == 1 ? "cut" : "layers");
            }
            Panel(controls.transform, "Tool_Divider", Rule, 419, 8, 1, 28);
            resetButton = Button(controls.transform, "重置视角", 440, 0, 133, 44, () => { stage.ResetView(); ShowDetail(-1); }); resetButton.name = "Reset_View"; AddButtonGlyph(resetButton, "reset");
            pinsButton = Button(controls.transform, "热点", 579, 0, 102, 44, ToggleHotspots); pinsButton.name = "Toggle_Hotspots"; AddButtonGlyph(pinsButton, "eye");
            demoButton = Button(controls.transform, "原理演示  ›", 735, 0, 171, 44, StartDemonstration); demoButton.name = "Start_Demonstration";
            settingsButton = Button(controls.transform, "展示设置  ···", 930, 0, 174, 44, () => SetSettings(!IsSettingsOpen)); settingsButton.name = "Display_Settings";
            foldButton = Button(controls.transform, "", 0, 52, 232, 28, ModelSpecialAction); foldButton.name = "Special_Observation";
            foldText = foldButton.GetComponentInChildren<TMP_Text>(); foldText.fontSize = 14;
            Label(controls.transform, "左键拖动旋转   /   滚轮缩放   /   点击编号查看结构", 454, 56, 650, 22, 13, Muted, TextAlignmentOptions.TopRight);
        }

        private void BuildSettings(Transform frame)
        {
            settingsOverlay = Panel(frame, "Display_Settings_Overlay", Color.clear, 0, 0, 1600, 900).gameObject; settingsOverlay.GetComponent<Image>().raycastTarget = true;
            var dismiss = settingsOverlay.AddComponent<Button>(); dismiss.transition = Selectable.Transition.None; dismiss.onClick.AddListener(() => SetSettings(false)); dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            var settings = Panel(settingsOverlay.transform, "Settings_Panel", new Color(.16f, .195f, .12f, .99f), 1246, 555, 326, 234);
            settings.GetComponent<Image>().raycastTarget = true; settings.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            Panel(settings, "Settings_Accent", Amber, 0, 0, 326, 2); Label(settings, "展示设置", 20, 16, 270, 26, 19, Ink).font = font;
            var surface = Button(settings, "表面状态   清理后样件", 16, 57, 294, 40, () => { stage.ToggleSurface(); RefreshButtons(); }); surface.name = "Toggle_Surface"; surfaceText = surface.GetComponentInChildren<TMP_Text>();
            var q = Button(settings, "贴图清晰度   2K", 16, 108, 294, 40, ToggleQuality); q.name = "Toggle_Quality"; qualityText = q.GetComponentInChildren<TMP_Text>();
            var motion = Button(settings, "减少动态效果   关", 16, 159, 294, 40, ToggleReducedMotion); motion.name = "Toggle_Reduced_Motion"; motionText = motion.GetComponentInChildren<TMP_Text>(); settingsOverlay.SetActive(false);
        }

        private void RenderReading()
        {
            readingAge = 0;
            for (int i = detailContent.childCount - 1; i >= 0; i--) { var child = detailContent.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            float y = 0;
            if (readingTab == 4) RenderClues(ref y);
            else if (current != null)
            {
                var copy = AerospaceScienceCopy.For(current.code);
                for (int i = 0; i < pinNames.Count; i++) pinNames[i].text = ShortHotspot(current.code, i);
                if (readingTab == 0)
                {
                    ReadText("01 / 组件概览", ref y, 12, PaperMuted, 14);
                    ReadText(copy.headline, ref y, 22, PaperInk, 17, true, "Overview_Question");
                    ReadText(copy.summary, ref y, 17, PaperInk, 18, false, "Overview_Summary");
                    Panel(detailContent, "Key_Idea_Accent", PaperMuted, 0, y + 3, 2, 35);
                    var note = Label(detailContent, copy.takeaway, 17, y, 365, 48, 15, PaperMuted);
                    float noteHeight = Mathf.Max(35, note.GetPreferredValues(copy.takeaway, 365, Mathf.Infinity).y + 4); note.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, noteHeight); y += noteHeight + 12;
                    var explain = Button(detailContent, "观看原理演示   ›", 0, y, ReadingWidth, 37, StartDemonstration); explain.name = "Overview_Demonstration"; y += 49;
                }
                else if (readingTab == 1)
                {
                    ReadText("02 / 工作原理", ref y, 12, PaperMuted, 14);
                    ReadText(copy.principleTitle, ref y, 22, PaperInk, 22, true);
                    for (int i = 0; i < copy.steps.Length; i++)
                    {
                        MonoLabel(detailContent, (i + 1).ToString("00"), 0, y + 3, 32, 24, 13, PaperMuted);
                        Label(detailContent, copy.stepTitles[i], 34, y, 347, 27, 18, PaperInk).font = font; y += 35;
                        ReadText(copy.steps[i], ref y, 17, PaperInk, 23);
                    }
                    var play = Button(detailContent, "播放三维原理演示   ›", 0, y, ReadingWidth, 40, StartDemonstration); play.name = "Principle_Demonstration"; y += 52;
                }
                else if (readingTab == 2)
                {
                    ReadText("03 / 局部结构", ref y, 12, PaperMuted, 14);
                    for (int i = 0; i < current.hotspots.Length; i++)
                    {
                        int index = i; var b = Button(detailContent, (i + 1).ToString("00"), i * 62, y, 53, 40, () => ShowDetail(index)); b.name = "Detail_Selector_" + i;
                        b.GetComponentInChildren<TMP_Text>().font = monoFont; StyleButton(b, selectedHotspot == i);
                    }
                    y += 58;
                    if (selectedHotspot >= 0)
                    {
                        ReadText(current.hotspots[selectedHotspot].title, ref y, 24, PaperInk, 16, true);
                        ReadText(current.hotspots[selectedHotspot].text, ref y, 18, PaperInk, 18, false, "Hotspot_Explanation");
                        var demo = Button(detailContent, "查看关联原理演示   ›", 0, y, ReadingWidth, 38, StartDemonstration); demo.name = "Hotspot_Demonstration"; y += 50;
                    }
                    else
                    {
                        ReadText(copy.shortName + "的组成", ref y, 22, PaperInk, 16, true);
                        for (int i = 0; i < current.hotspots.Length; i++) ReadText((i + 1).ToString("00") + "  " + current.hotspots[i].title, ref y, 17, PaperMuted, 12);
                    }
                    var back = Button(detailContent, "←  返回整体介绍", 0, y, ReadingWidth, 37, () => ShowDetail(-1)); back.name = "Back_To_Overview"; y += 49;
                }
                else if (readingTab == 3)
                {
                    ReadText("科普资料", ref y, 24, PaperInk, 20, true);
                    ReadText(current.intro, ref y, 18, PaperInk, 24);
                    ReadText("参考来源", ref y, 18, PaperInk, 14, true);
                    if (current.sources != null) for (int i = 0; i < current.sources.Length; i++)
                    {
                        int index = i; var b = Button(detailContent, current.sources[i].label + "  ↗", 0, y, ReadingWidth, 58, () => OpenSource(index)); b.GetComponentInChildren<TMP_Text>().fontSize = 15; y += 68;
                    }
                }
                else if (readingTab == 5)
                {
                    ReadText("结构演示", ref y, 12, PaperMuted, 17);
                    ReadText(specialTitle, ref y, 24, PaperInk, 20, true); ReadText(specialBody, ref y, 18, PaperInk, 22);
                    Button(detailContent, "←  返回整体介绍", 0, y, ReadingWidth, 38, () => ShowDetail(-1)); y += 50;
                }
                else if (readingTab == 6) RenderDemonstration(ref y);
            }
            detailContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(ReadingHeight, y + 8));
            detailContent.anchoredPosition = Vector2.zero; readingScroll.velocity = Vector2.zero; readingScroll.verticalNormalizedPosition = 1;
        }

        private void RenderClues(ref float y)
        {
            ReadText("本局搜集线索", ref y, 24, PaperInk, 21, true);
            ReadText("首个新箱必出样本，后续沿探索路线间隔出现。重复开箱不推进进度；成功放入背包才解锁档案。", ref y, 17, PaperInk, 24);
            ReadText("已探索不同箱子：" + owner.SearchedBoxCount + "   已投放样本：" + owner.PlacedPartCount + "/5", ref y, 15, PaperMuted, 20);
            foreach (var reservation in owner.Reservations)
            {
                if (!reservation.placed)
                {
                    ReadText(reservation.code + "   待发现", ref y, 17, PaperInk, 7, true);
                    ReadText("第 " + reservation.searchNumber + " 个新箱保底出现（两名 Agent 共享进度）", ref y, 15, PaperMuted, 20); continue;
                }
                string zone = reservation.cluster != null ? reservation.cluster.name : "资源区";
                for (var p = reservation.cluster != null ? reservation.cluster.transform : null; p != null; p = p.parent)
                    if (p.name.StartsWith("Zone", StringComparison.Ordinal)) { zone = p.name; break; }
                ReadText(reservation.code + (owner.HasCollected(reservation.code) ? "   本局已收集" : "   待收集"), ref y, 17, PaperInk, 7, true);
                ReadText(zone + " / " + (reservation.box != null ? reservation.box.BoxName : "搜索点不可用"), ref y, 15, PaperMuted, 20);
            }
            if (!owner.IsReady) ReadText(owner.Status, ref y, 15, PaperMuted, 18);
            ReadText("每件占 1 格。满包时请先腾出空间；已解锁的档案可再次查看。", ref y, 15, PaperMuted, 20);
        }

        private void ReadText(string text, ref float y, int size, Color color, float after, bool headingStyle = false, string objectName = null)
        {
            var label = Label(detailContent, text, 0, y, ReadingWidth, 100, size, color);
            if (headingStyle) { label.font = font; label.lineSpacing = 2; }
            if (objectName != null) label.name = objectName;
            float h = Mathf.Ceil(label.GetPreferredValues(text, ReadingWidth, Mathf.Infinity).y) + 5;
            label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h); y += h + after;
        }
        private RectTransform Panel(Transform parent, string name, Color color, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false; var r = (RectTransform)go.transform; Rect(r, x, y, w, h); return r;
        }
        private TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var go = new GameObject(text.Length > 28 ? text.Substring(0, 28) : string.IsNullOrEmpty(text) ? "Label" : text, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = bodyTypeface; label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment;
            label.raycastTarget = false; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; label.lineSpacing = 4; label.paragraphSpacing = 8; label.extraPadding = true; label.richText = false;
            Rect(label.rectTransform, x, y, w, h); return label;
        }
        private TMP_Text MonoLabel(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
        { var label = Label(parent, text, x, y, w, h, size, color); label.font = monoFont; return label; }
        private bool OnPaper(Transform target) => readingPane != null && target.IsChildOf(readingPane);
        private Button Button(Transform parent, string text, float x, float y, float w, float h, Action clicked)
        {
            bool paper = OnPaper(parent); var panel = Panel(parent, "Button_" + text, paper ? new Color(.36f, .40f, .26f, .08f) : ButtonFill, x, y, w, h); panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>(); button.onClick.AddListener(() => clicked());
            var colors = button.colors; colors.highlightedColor = new Color(1.18f, 1.18f, 1.08f); colors.pressedColor = new Color(.79f, .82f, .70f); colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.54f, .55f, .45f, .65f); colors.fadeDuration = .12f; button.colors = colors; button.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(panel, text, 8, 0, w - 16, h, 16, paper ? PaperInk : Ink, TextAlignmentOptions.Center); return button;
        }
        private void AddButtonGlyph(Button button, string glyph)
        {
            var go = new GameObject("Tool_Glyph", typeof(RectTransform), typeof(AerospaceTerminalGraphic)); go.transform.SetParent(button.transform, false); Rect(go.GetComponent<RectTransform>(), 12, 12, 20, 20);
            var graphic = go.GetComponent<AerospaceTerminalGraphic>(); graphic.glyph = glyph; graphic.color = Muted; graphic.raycastTarget = false;
            var label = button.GetComponentInChildren<TMP_Text>(); Rect(label.rectTransform, 39, 0, button.GetComponent<RectTransform>().rect.width - 44, 44); label.fontSize = 15;
        }
        private void StyleButton(Button button, bool selected, bool tab = false)
        {
            bool paper = OnPaper(button.transform);
            button.GetComponent<Image>().color = selected ? Amber : tab ? new Color(.19f, .23f, .15f) : paper ? new Color(.36f, .40f, .26f, .08f) : ButtonFill;
            Color tint = selected ? PaperInk : tab ? Ink : paper ? PaperInk : Muted;
            var label = button.GetComponentInChildren<TMP_Text>(); if (label != null) label.color = tint;
            var glyph = button.GetComponentInChildren<AerospaceTerminalGraphic>(); if (glyph != null) glyph.color = tint;
        }
        private static void Rect(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ExtractionLike.Aerospace
{
    /// <summary>Presentation-only modal; inventory, discovery and pause ownership are preserved.</summary>
    [DefaultExecutionOrder(50)]
    public sealed partial class AerospaceScienceUI : MonoBehaviour
    {
        // Original blue terminal surfaces (5334f7c), applied to the current dossier layout.
        // Selection is cyan too: no yellow/orange UI accents, including transient states.
        private static readonly Color Ink = new Color(.91f, .95f, .96f), Muted = new Color(.61f, .74f, .84f);
        private static readonly Color Accent = new Color(.48f, .90f, 1f), Teal = new Color(.32f, .83f, .90f);
        private static readonly Color Rule = new Color(.24f, .46f, .66f, .42f), ButtonFill = new Color(.065f, .125f, .205f, .92f);
        private static readonly Color SelectedFill = new Color(.075f, .225f, .36f, .97f);
        // These names identify the retained reading layout, not a separate paper-colour theme.
        private static readonly Color Paper = new Color(.038f, .079f, .145f, .98f), PaperInk = Ink, PaperMuted = Muted;
        private AerospaceCollectionRuntime owner;
        private AerospaceInspectionStage stage;
        private Canvas canvas;
        private Font bodyFont;
        private TMP_FontAsset font, bodyTypeface, monoFont;
        private GameObject modal, controls, settingsOverlay, settingsTooltip;
        private RectTransform viewport, detailContent, transitionRect, readingPane;
        private RawImage modelImage;
        private Image transitionImage;
        private CanvasGroup contentGroup;
        private ScrollRect readingScroll;
        private Text hud;
        private TMP_Text progress, heading, english, subtitle, loading, surfaceText, foldText, qualityText, closeLabel, sampleId;
        private Button foldButton, settingsButton, sourceButton, resetButton, pinsButton;
        private TMP_Text watermark, locationTitle, locationBody, locationTag, surfaceState;
        private AerospaceTerminalGraphic locationGraphic;
        private bool pinsEnabled = true;
        private readonly List<Button> cards = new List<Button>(), pins = new List<Button>(), anchorButtons = new List<Button>(), modeButtons = new List<Button>(), readingTabs = new List<Button>();
        private readonly List<TMP_Text> cardTitles = new List<TMP_Text>(), cardStates = new List<TMP_Text>(), pinNames = new List<TMP_Text>();
        private readonly List<Image> cardIcons = new List<Image>(), cardRules = new List<Image>(), progressMarks = new List<Image>();
        private readonly List<AerospaceHotspotLeader> leaders = new List<AerospaceHotspotLeader>();
        private readonly Vector2[] anchorPoints = new Vector2[4];
        private readonly Vector3[] viewportCorners = new Vector3[4];
        private readonly float[] pinY = new float[4];
        private readonly bool[] pinVisible = new bool[4], pinLeft = new bool[4];
        private AerospacePart current;
        private float savedTimeScale;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible, pauseOwned, transitioning;
        private int ticket, readingTab, selectedHotspot = -1, hoveredPin = -1;
        private string quality = "2K", specialTitle, specialBody;
        private GameObject selectedBeforeOpen;
        public bool IsOpen { get; private set; }
        public string CurrentCode => current?.code;
        public AerospaceInspectionStage Stage => stage;
        public bool IsTransitioning => transitioning;
        public int ActiveReadingTab => readingTab;
        public int SelectedHotspot => selectedHotspot;
        public bool IsSettingsOpen => settingsOverlay != null && settingsOverlay.activeSelf;
        public bool HotspotsVisible => pinsEnabled;

        public void Initialize(AerospaceCollectionRuntime runtime)
        {
            owner = runtime;
            var assets = Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation");
            bodyFont = assets != null && assets.bodyFont != null ? assets.bodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = assets != null ? assets.inspectionHeading : TMP_Settings.defaultFontAsset;
            bodyTypeface = assets != null ? assets.inspectionBody : TMP_Settings.defaultFontAsset;
            monoFont = assets != null ? assets.inspectionMono : TMP_Settings.defaultFontAsset;
            stage = gameObject.AddComponent<AerospaceInspectionStage>(); stage.SetReducedMotion(PlayerPrefs.GetInt(MotionPreference, 0) == 1); Build();
        }

        public void Open(string code, bool discovery)
        {
            if (AerospaceCollectionRuntime.RaidLocked || DraggableItemUI.CurrentlyDraggedItem != null || owner == null) return;
            if (code != null && !owner.IsUnlocked(code)) return;
            if (!IsOpen)
            {
                var inventory = InventoryScreenController.Instance;
                savedTimeScale = inventory != null && inventory.IsInventoryOpen ? inventory.TimeScaleBeforeInventoryPause : Time.timeScale;
                savedCursorLock = Cursor.lockState; savedCursorVisible = Cursor.visible;
                selectedBeforeOpen = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                IsOpen = true; pauseOwned = true; Time.timeScale = 0; AerospaceUiInputGate.Set(true);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true; modal.SetActive(true);
                InventoryItemInfoPanelController.Instance?.Hide(); EventSystem.current?.SetSelectedGameObject(null);
            }
            if (code == null) code = current != null && owner.IsUnlocked(current.code) ? current.code : AerospaceCatalog.Load().models.FirstOrDefault(p => owner.IsUnlocked(p.code))?.code;
            RefreshArchive();
            if (!discovery && code == current?.code && stage.Ready && !transitioning) return;
            if (code != null) BeginLoad(code, discovery);
            else
            {
                heading.text = "航天样本\n档案"; english.text = "AEROSPACE\nCOMPONENTS"; sampleId.text = "—";
                watermark.text = ""; locationTitle.text = "航天系统"; locationBody.text = "推进、返回与载荷连接"; locationTag.text = ""; locationGraphic.locationCode = "R01"; locationGraphic.SetVerticesDirty();
                loading.text = "尚无归档样本\n搜索并拾取零件后，即可在这里检视";
                loading.gameObject.SetActive(true); controls.SetActive(false); HidePins(); ShowCollectionClues(); RefreshButtons();
            }
        }

        private void BeginLoad(string code, bool discovery)
        {
            StopAllCoroutines(); int request = ++ticket;
            skipReveal = false;
            CaptureSwitchSnapshot(discovery); ClearHotspotConfirmation();
            current = AerospaceCatalog.Load().Find(code); stage.Unload(); modelImage.texture = null; modelImage.color = new Color(1, 1, 1, 0);
            if (!HasSwitchSnapshot) ApplyCurrentIdentity();
            pinsEnabled = true;
            HidePins(); SetSettings(false); selectedHotspot = -1; hoveredPin = -1;
            controls.SetActive(false); loading.text = "正在载入样本…"; loading.gameObject.SetActive(!HasSwitchSnapshot);
            readingTab = 0; transitioning = true; readingReveal.interactable = false;
            if (!HasSwitchSnapshot) RenderReading();
            RefreshArchive(); RefreshButtons();
            transitionImage.sprite = Resources.Load<Sprite>("Aerospace/Icons/" + code); transitionImage.color = Color.white;
            Rect(transitionRect, 610, 40, 820, 820); transitionRect.gameObject.SetActive(discovery);
            skipButton.gameObject.SetActive(discovery && !stage.ReducedMotion);
            contentGroup.interactable = !discovery; StartCoroutine(LoadAndReveal(request, discovery));
        }

        private IEnumerator LoadAndReveal(int request, bool discovery)
        {
            bool finished = false; string error = null;
            StartCoroutine(stage.Load(current, quality, e => { error = e; finished = true; }));
            float elapsed = 0;
            while ((!skipReveal && !stage.ReducedMotion && elapsed < (discovery ? .85f : .06f)) || !finished)
            {
                if (request != ticket) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .2f) / .8f));
                if (discovery)
                {
                    transitionRect.anchoredPosition = Vector2.Lerp(new Vector2(610, -40), new Vector2(694, -118), t);
                    transitionRect.sizeDelta = Vector2.Lerp(new Vector2(820, 820), new Vector2(652, 652), t);
                }
                if (elapsed > 25 && !finished) { error = "样本载入超时，请关闭后重试。"; break; }
                yield return null;
            }
            if (request != ticket) yield break;
            if (error == null)
            {
                ApplyCurrentIdentity(); RenderReading();
                modelImage.texture = stage.Texture;
                for (float t = 0; t < 1 && !skipReveal && !stage.ReducedMotion; t += Time.unscaledDeltaTime / .28f)
                {
                    float eased = Mathf.SmoothStep(0, 1, t);
                    modelImage.color = new Color(1, 1, 1, eased); transitionImage.color = new Color(1, 1, 1, 1 - eased);
                    if (snapshotImage != null) snapshotImage.color = new Color(1, 1, 1, 1 - eased);
                    yield return null;
                    if (request != ticket) yield break;
                }
                modelImage.color = Color.white; loading.gameObject.SetActive(false); controls.SetActive(true);
                stage.Select(-1); readingTab = 0; selectedHotspot = -1;
                if (discovery && !skipReveal) stage.StartScan();
            }
            else { ApplyCurrentIdentity(); RenderReading(); loading.gameObject.SetActive(true); loading.text = error; stage.Unload(); }
            ReleaseSwitchSnapshot(); readingReveal.interactable = true;
            transitionRect.gameObject.SetActive(false); skipButton.gameObject.SetActive(stage.ScanProgress >= 0); transitioning = false; contentGroup.interactable = true; RefreshButtons();
        }

        public void ShowDetail(int index)
        {
            if (current == null || index < -1 || index >= current.hotspots.Length) return;
            if (transitioning) return;
            selectedHotspot = index; readingTab = index < 0 ? 0 : 2;
            ClearHotspotConfirmation(); if (index >= 0) pinConfirmationAge[index] = 0;
            stage.Select(index); RenderReading(); RefreshButtons();
        }

        public void SetReadingTab(int index)
        {
            if (current == null || transitioning || index < 0 || index > 2) return;
            RestoreDemonstrationReading();
            readingTab = index;
            if (index != 2) { selectedHotspot = -1; stage.Select(-1); }
            RenderReading(); RefreshButtons();
        }

        private void Mode(string mode)
        {
            if (!stage.Ready) return;
            if (stage.DemoActive) { stage.StopDemonstration(false); readingTab = 1; RenderReading(); }
            if (stage.Study || stage.RelationPhase >= 0) ShowDetail(-1);
            selectedHotspot = hoveredPin = -1;
            stage.SetMode(mode); if (readingTab == 2) RenderReading(); RefreshButtons();
        }
        private void ToggleQuality()
        {
            if (transitioning || current == null) return;
            quality = quality == "2K" ? "4K" : "2K"; BeginLoad(current.code, false);
        }

        public void SetSettings(bool visible)
        {
            if (settingsTooltip != null) settingsTooltip.SetActive(false);
            if (settingsOverlay != null) settingsOverlay.SetActive(visible && current != null && stage.Ready && !transitioning);
            if (settingsButton != null) StyleButton(settingsButton, IsSettingsOpen);
        }

        private void RefreshButtons()
        {
            bool ready = current != null && stage.Ready && !transitioning;
            qualityText.text = "贴图清晰度   " + quality;
            if (motionText != null) motionText.text = "减少动态效果   " + (stage.ReducedMotion ? "开" : "关");
            if (demoButton != null) demoButton.interactable = ready;
            surfaceText.text = stage.Recovered ? "表面状态   回收痕迹" : "表面状态   清理后样件";
            surfaceState.text = stage.Recovered ? "回收痕迹" : "清理后样件";
            foldButton.gameObject.SetActive(current != null && (current.code == "R04" || current.code == "R03" || current.code == "R05"));
            foldButton.interactable = ready;
            foldText.text = current?.code == "R03" ? (stage.Study ? "返回整体样件" : "放大喷注单元") : current?.code == "R05" ? "演示连接关系" : stage.Folded ? "展开栅格舵" : "折叠栅格舵";
            StyleButton(foldButton, stage.Study || stage.RelationPhase >= 0 || stage.Folded);
            resetButton.interactable = settingsButton.interactable = ready;
            pinsButton.interactable = ready; StyleButton(pinsButton, pinsEnabled);
            string[] modes = { "assembled", "cutaway", "exploded" };
            for (int i = 0; i < modeButtons.Count; i++)
            {
                modeButtons[i].interactable = ready;
                StyleButton(modeButtons[i], stage.Mode == modes[i] && !stage.Study && stage.RelationPhase < 0);
            }
            for (int i = 0; i < readingTabs.Count; i++)
            {
                readingTabs[i].interactable = current != null && !transitioning;
                StyleButton(readingTabs[i], readingTab == i, true);
            }
            sourceButton.interactable = current != null && !transitioning;
        }

        public void ModelSpecialAction()
        {
            if (current == null || !stage.Ready) return;
            if (stage.DemoActive) { stage.StopDemonstration(false); readingTab = 0; RenderReading(); }
            if (current.code == "R04") { stage.ToggleFold(); RefreshButtons(); return; }
            selectedHotspot = -1;
            if (current.code == "R03")
            {
                if (!stage.ToggleStudy()) { loading.text = "教学单元资源缺失，请重建资产。"; loading.gameObject.SetActive(true); return; }
                if (!stage.Study) { ShowDetail(-1); return; }
                specialTitle = "同轴单元\n两路分别供给";
                specialBody = "中心管与外围环隙形成两股介质的独立通路。两路分别进入供给区域，在出口附近相邻。\n\n供给层将介质分配到喷注单元，隔板分开两路流体。中心管的外壁与周围结构共同形成环形通道。";
            }
            else if (current.code == "R05")
            {
                stage.CycleRelation();
                specialTitle = new[] { "01  保持约束", "02  解除约束", "03  上下分开" }[stage.RelationPhase];
                specialBody = new[] {
                    "夹紧带与夹块将上下接口保持在一起，使载荷与运载器之间能够传递载荷。",
                    "释放系统解除夹紧约束，上下接口由保持连接转为可以相互分离的状态。",
                    "上下接口分开，载荷与运载器建立相对运动。弹簧推力元件可在解除约束后帮助推动分离。" }[stage.RelationPhase];
            }
            else return;
            readingTab = 5; RenderReading(); RefreshButtons();
        }

        private void RefreshArchive()
        {
            progress.text = "本局收集   " + owner.CollectedCount + " / 5";
            var parts = AerospaceCatalog.Load().models;
            for (int i = 0; i < cards.Count; i++)
            {
                bool unlocked = owner.IsUnlocked(parts[i].code), selected = current?.code == parts[i].code;
                bool collected = owner.HasCollected(parts[i].code); cards[i].interactable = unlocked;
                cardTitles[i].text = AerospaceScienceCopy.For(parts[i].code).shortName;
                cardTitles[i].color = selected ? Ink : unlocked ? Muted : new Color(.39f, .44f, .45f);
                cardStates[i].text = parts[i].code + " / " + (collected ? "本局收集" : unlocked ? "历史归档" : "未发现");
                cardStates[i].color = selected ? Accent : collected ? Teal : Muted;
                cardIcons[i].color = new Color(1, 1, 1, unlocked ? 1 : .22f);
                cardRules[i].color = Color.clear;
                if (selected) MoveArchiveCursor(i);
                cards[i].GetComponent<Image>().color = selected ? new Color(SelectedFill.r, SelectedFill.g, SelectedFill.b, .9f) : Color.clear;
                cards[i].GetComponent<AerospaceInstrumentSurface>().SetSelected(selected);
                progressMarks[i].color = collected ? Teal : new Color(.23f, .28f, .29f);
            }
        }
        public void ShowCollectionClues() { if (transitioning) return; RestoreDemonstrationReading(); readingTab = 4; RenderReading(); RefreshButtons(); }
        private void ShowSources() { if (transitioning) return; RestoreDemonstrationReading(); readingTab = 3; RenderReading(); RefreshButtons(); }
        private void OpenSource(int index)
        {
            if (current?.sources == null || index < 0 || index >= current.sources.Length) return;
            if (Uri.TryCreate(current.sources[index].url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) Application.OpenURL(uri.AbsoluteUri);
        }

        private void Update()
        {
            if (owner == null) return;
            hud.text = "航天档案  本局 " + owner.CollectedCount + " / 5   [J]";
            if (!IsOpen) return;
            TickPresentationMotion();
            UpdateTerminal();
            closeLabel.text = InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen ? "返回背包   [Esc]" : "返回游戏   [Esc]";
            var frame = contentGroup.transform as RectTransform; var canvasRect = canvas.transform as RectTransform;
            frame.localScale = Vector3.one * Mathf.Min(canvasRect.rect.width / 1600, canvasRect.rect.height / 900);
            if (stage.Texture != null)
            {
                // Text stays on the native-resolution Canvas; only the 3D viewport is supersampled.
                viewport.GetWorldCorners(viewportCorners);
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, viewportCorners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, viewportCorners[2]);
                stage.UpdateDisplayResolution(topRight - bottomLeft);
                modelImage.texture = stage.Texture;
            }
            if (AerospaceCollectionRuntime.RaidLocked) Close();
            else if (Input.GetKeyDown(KeyCode.Escape)) { if (IsSettingsOpen) SetSettings(false); else Close(); }
        }
        private void HidePins()
        {
            foreach (var pin in pins) pin.gameObject.SetActive(false);
            foreach (var anchor in anchorButtons) anchor.gameObject.SetActive(false);
            foreach (var line in leaders) line.gameObject.SetActive(false);
        }
        public void ToggleHotspots() { pinsEnabled = !pinsEnabled; hoveredPin = -1; RefreshButtons(); if (!pinsEnabled) HidePins(); }

        private void LateUpdate()
        {
            if (!IsOpen) return;
            float w = viewport.rect.width, h = viewport.rect.height;
            var silhouette = stage.ProjectedModelBounds();
            if (stage.Ready)
                terminalGraphic.SetDatum(new Vector2(Mathf.Clamp(silhouette.center.x * w, 290, w - 290), Mathf.Clamp((1 - silhouette.yMin) * h + 18, 470, h - 62)), Mathf.Clamp(silhouette.width * w * .34f, 140, 235), stage.DisplayYaw);
            for (int i = 0; i < pins.Count; i++)
            {
                pinVisible[i] = pinsEnabled && !transitioning && !stage.DemoActive && stage.TryAnchor(i, out _);
                if (pinVisible[i] && stage.TryAnchor(i, out var point))
                {
                    anchorPoints[i] = new Vector2(point.x * w, (1 - point.y) * h);
                    pinLeft[i] = point.x < .5f; pinY[i] = Mathf.Clamp(anchorPoints[i].y, 90, h - 80);
                }
            }
            // Dock outside the model; separate the four labels when projected anchors overlap.
            for (int pass = 0; pass < 4; pass++)
            for (int i = 0; i < pins.Count; i++)
            for (int j = i + 1; j < pins.Count; j++)
            {
                if (!pinVisible[i] || !pinVisible[j] || pinLeft[i] != pinLeft[j] || Mathf.Abs(pinY[i] - pinY[j]) >= 46) continue;
                float middle = (pinY[i] + pinY[j]) * .5f;
                bool before = pinY[i] < pinY[j] || (Mathf.Approximately(pinY[i], pinY[j]) && i < j);
                pinY[i] = Mathf.Clamp(middle + (before ? -24 : 24), 72, h - 62);
                pinY[j] = Mathf.Clamp(middle + (before ? 24 : -24), 72, h - 62);
            }
            for (int i = 0; i < pins.Count; i++)
            {
                pins[i].gameObject.SetActive(pinVisible[i]); leaders[i].gameObject.SetActive(pinVisible[i]);
                anchorButtons[i].gameObject.SetActive(pinVisible[i]);
                if (!pinVisible[i]) continue;
                Rect(anchorButtons[i].GetComponent<RectTransform>(), anchorPoints[i].x - 12, anchorPoints[i].y - 12, 24, 24);
                bool active = selectedHotspot == i || hoveredPin == i;
                float width = active ? 174 : 42;
                float x = pinLeft[i] ? Mathf.Clamp(silhouette.xMin * w - width - 24, 24, w * .32f) : Mathf.Clamp(silhouette.xMax * w + 24, w * .60f, w - width - 24);
                Rect(pins[i].GetComponent<RectTransform>(), x, pinY[i] - 21, width, 42);
                float visibility = active ? 1 : stage.AnchorVisibility(i);
                pinEmphasis[i] = Mathf.MoveTowards(pinEmphasis[i], active ? 1 : 0, stage.ReducedMotion ? 1 : Time.unscaledDeltaTime / .15f);
                pins[i].GetComponent<Image>().color = Color.Lerp(new Color(.055f, .105f, .135f, .54f * visibility), new Color(.06f, .17f, .24f, .98f), pinEmphasis[i]);
                pins[i].GetComponentInChildren<TMP_Text>().color = active ? Accent : new Color(Teal.r, Teal.g, Teal.b, Mathf.Max(.45f, visibility));
                pinNames[i].gameObject.SetActive(active); pinNames[i].color = Color.Lerp(Muted, Ink, pinEmphasis[i]);
                float endX = pinLeft[i] ? x + width : x;
                var tint = Color.Lerp(new Color(Teal.r, Teal.g, Teal.b, .34f * visibility), Accent, pinEmphasis[i]);
                float reveal = selectedHotspot == i && !stage.ReducedMotion ? Mathf.SmoothStep(0, 1, pinConfirmationAge[i] / .22f) : 1;
                leaders[i].SetPoints(anchorPoints[i], new Vector2(endX, pinY[i]), tint, reveal);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            ++ticket; StopAllCoroutines(); IsOpen = false; transitioning = false; current = null;
            skipReveal = false; selectedHotspot = hoveredPin = -1;
            ReleaseSwitchSnapshot(); ClearHotspotConfirmation();
            if (modal != null) modal.SetActive(false); if (modelImage != null) modelImage.texture = null;
            stage?.Unload(); AerospaceUiInputGate.Set(false);
            if (pauseOwned)
            {
                bool inventoryOpen = InventoryScreenController.Instance != null && InventoryScreenController.Instance.IsInventoryOpen;
                if (!AerospaceCollectionRuntime.RaidLocked) Time.timeScale = inventoryOpen ? 0 : savedTimeScale;
                Cursor.lockState = savedCursorLock; Cursor.visible = savedCursorVisible; pauseOwned = false;
            }
            EventSystem.current?.SetSelectedGameObject(selectedBeforeOpen != null && selectedBeforeOpen.activeInHierarchy ? selectedBeforeOpen : null);
            Resources.UnloadUnusedAssets();
        }
        private void OnDisable() { Close(); }
        private void OnDestroy() { Close(); }
    }
}

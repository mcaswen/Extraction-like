using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceScienceUI
    {
        public const float DiscoveryDuration = 1.9f;
        public const float ArchiveReopenDuration = .25f;
        private sealed class EntryLayer { public CanvasGroup group; public float start, duration; }
        private readonly List<EntryLayer> entryLayers = new List<EntryLayer>();
        private RectTransform entryFrame, receiptRect;
        private CanvasGroup receiptGroup;
        private Image receiptIcon, modalSurface;
        private TMP_Text receiptTitle, receiptState;
        private AerospaceAcquisitionGraphic acquisitionGraphic, acquisitionAtmosphere;
        private Rect originFrame;
        private bool discoveryPresentation, openingShell, scanSoundPlayed, lockSoundPlayed;
        private AudioSource entryVoice;
        private AudioClip scanCue, lockCue;
        public float EntryElapsed { get; private set; }
        public bool IsDiscoveryPresentation => discoveryPresentation;
        public Rect EntryOrigin => originFrame;

        private static float EntryEase(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
        private void BuildEntryPresentation(RectTransform frame, Button close)
        {
            entryFrame = frame; modalSurface = modal.GetComponent<Image>();
            // Fade each existing region at its final position. Text is never scaled with the model.
            foreach (Transform child in frame)
            {
                if (child == viewport || child == close.transform || child == skipButton.transform) continue;
                if (!child.TryGetComponent<CanvasGroup>(out var group)) group = child.gameObject.AddComponent<CanvasGroup>();
                float start = child == readingPane ? 1.30f : child.name.Contains("Archive") || child.name == "Header_Rule" ? 1.42f : 1.55f;
                if (child.name == "Blue_Inspection_Surface") start = .92f;
                entryLayers.Add(new EntryLayer { group = group, start = start, duration = child == readingPane ? .36f : .32f });
            }
            var graphic = new GameObject("Sample_Acquisition_Frame", typeof(RectTransform), typeof(AerospaceAcquisitionGraphic));
            graphic.transform.SetParent(frame, false); Rect(graphic.GetComponent<RectTransform>(), 0, 0, 1600, 900);
            acquisitionGraphic = graphic.GetComponent<AerospaceAcquisitionGraphic>(); acquisitionGraphic.raycastTarget = false;
            graphic.SetActive(false);
            var atmosphere = new GameObject("Sample_Acquisition_Atmosphere", typeof(RectTransform), typeof(AerospaceAcquisitionGraphic));
            atmosphere.transform.SetParent(frame, false); Rect(atmosphere.GetComponent<RectTransform>(), 0, 0, 1600, 900);
            atmosphere.transform.SetSiblingIndex(viewport.GetSiblingIndex());
            acquisitionAtmosphere = atmosphere.GetComponent<AerospaceAcquisitionGraphic>();
            acquisitionAtmosphere.BehindModel = true; acquisitionAtmosphere.raycastTarget = false;
            atmosphere.SetActive(false);
            receiptRect = Panel(frame, "Sample_Receipt", new Color(.025f,.07f,.12f,.96f), 0, 0, 278, 72);
            receiptRect.GetComponent<Image>().raycastTarget = false;
            receiptGroup = receiptRect.gameObject.AddComponent<CanvasGroup>(); receiptGroup.blocksRaycasts = false;
            receiptIcon = Panel(receiptRect, "Receipt_Part_Icon", Color.white, 9, 8, 52, 56).GetComponent<Image>();
            receiptIcon.preserveAspect = true; receiptIcon.raycastTarget = false;
            receiptTitle = Label(receiptRect, "", 74, 12, 194, 27, 17, Ink);
            receiptState = Label(receiptRect, "已入库", 75, 42, 190, 20, 13, Teal);
            receiptRect.gameObject.SetActive(false);
            var audioObject = new GameObject("Sample_Transition_Audio"); audioObject.transform.SetParent(transform, false);
            entryVoice = audioObject.AddComponent<AudioSource>(); entryVoice.playOnAwake = false;
            entryVoice.spatialBlend = 0; entryVoice.ignoreListenerPause = true; entryVoice.priority = 160;
            entryVoice.volume = .16f; entryVoice.dopplerLevel = 0;
            scanCue = MakeEntryCue(false); lockCue = MakeEntryCue(true);
        }
        private void FitEntryFrame()
        {
            if (entryFrame == null) return;
            var canvasRect = (RectTransform)canvas.transform;
            entryFrame.localScale = Vector3.one * Mathf.Min(canvasRect.rect.width / 1600, canvasRect.rect.height / 900);
        }
        private Rect ResolveEntryOrigin(Rect? normalized)
        {
            if (!normalized.HasValue)
            {
                var inventory = InventoryScreenController.Instance;
                if (inventory != null && inventory.IsInventoryOpen && inventory.BackpackGrid != null)
                {
                    var item = inventory.BackpackGrid.GetComponentsInChildren<DraggableItemUI>()
                        .FirstOrDefault(v => AerospaceCatalog.CodeFor(v.ItemData) == current.code);
                    if (item != null) normalized = AerospaceCollectionRuntime.NormalizedPickupRect(item.transform as RectTransform);
                }
            }
            if (!normalized.HasValue) return new Rect(712, 576, 64, 64);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = Vector2.Scale(normalized.Value.center, new Vector2(Screen.width, Screen.height));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(entryFrame, screen, camera, out Vector2 local);
            float scale = Mathf.Max(.001f, canvas.scaleFactor * entryFrame.localScale.x);
            Vector2 size = Vector2.Scale(normalized.Value.size, new Vector2(Screen.width, Screen.height)) / scale;
            size.x = Mathf.Clamp(size.x, 36, 180); size.y = Mathf.Clamp(size.y, 36, 180);
            Vector2 center = new Vector2(Mathf.Clamp(local.x + 800, 32 + size.x*.5f, 1568-size.x*.5f),
                Mathf.Clamp(450-local.y, 32+size.y*.5f, 868-size.y*.5f));
            return new Rect(center-size*.5f,size);
        }
        private void PrepareEntryPresentation(bool discovery, bool opening, Rect? origin)
        {
            FitEntryFrame(); discoveryPresentation = discovery; openingShell = opening;
            EntryElapsed = 0; scanSoundPlayed = lockSoundPlayed = false;
            if (discovery)
            {
                originFrame = ResolveEntryOrigin(origin);
                receiptIcon.sprite = Resources.Load<Sprite>("Aerospace/Icons/" + current.code);
                receiptTitle.text = AerospaceScienceCopy.For(current.code).shortName;
                receiptState.text = "已入库";
                float x = originFrame.xMax + 14;
                if (x + 278 > 1568) x = originFrame.xMin - 292;
                Rect(receiptRect, Mathf.Clamp(x,32,1290), Mathf.Clamp(originFrame.center.y-36,92,796),278,72);
                receiptRect.gameObject.SetActive(true); receiptGroup.alpha = 1;
                acquisitionGraphic.gameObject.SetActive(!stage.ReducedMotion);
                acquisitionGraphic.Present(originFrame, originFrame, 0, -1);
                acquisitionAtmosphere.gameObject.SetActive(!stage.ReducedMotion);
                acquisitionAtmosphere.Present(originFrame, originFrame, 0, -1);
            }
            SetEntrySurfaces(discovery || opening ? 0 : 1);
            modelImage.color = Color.clear;
            loading.gameObject.SetActive(!discovery && !HasSwitchSnapshot);
        }
        private void ShowEntryLoading(float elapsed)
        {
            if (discoveryPresentation)
            {
                // Hold a small, truthful receipt while the selected model loads; no fake progress or blank takeover.
                receiptState.text = elapsed > 1.2f ? "正在载入样本…" : "已入库";
                receiptGroup.alpha = 1;
            }
            else if (openingShell) SetEntrySurfaces(EntryEase(elapsed / ArchiveReopenDuration));
        }
        private IEnumerator AnimateEntry(int request, bool discovery, float loadingElapsed)
        {
            if (skipReveal || stage.ReducedMotion) yield break;
            // Reveal controls with their panel, but keep input locked until docking finishes.
            if (discovery || openingShell) controls.SetActive(true);
            float duration = discovery ? DiscoveryDuration : ArchiveReopenDuration;
            float age = discovery ? Mathf.Min(loadingElapsed, .25f) : 0;
            while (age < duration && !skipReveal && !stage.ReducedMotion)
            {
                if (request != ticket) yield break;
                EntryElapsed = age;
                if (discovery) PresentDiscovery(age);
                else
                {
                    float alpha = EntryEase(age / duration);
                    if (openingShell) SetEntrySurfaces(alpha);
                    modelImage.color = new Color(1,1,1,alpha);
                    if (snapshotImage != null) snapshotImage.color = new Color(1,1,1,1-alpha);
                }
                yield return null;
                age += Time.unscaledDeltaTime;
            }
            EntryElapsed = duration;
        }
        private void PresentDiscovery(float age)
        {
            float grow = 1-Mathf.Pow(1-Mathf.Clamp01((age-.23f)/.42f),3), dock = EntryEase((age-1.25f)/.65f);
            float sweep = age >= .65f && age <= 1.25f ? Mathf.Clamp01((age-.65f)/.6f) : -1;
            stage.SetArrivalPresentation(Mathf.Clamp01((age-.25f)/1.25f), sweep, age < 1.25f ? 1 : 0, age);
            Rect bounds = stage.ProjectedModelBounds();
            Vector2 centerInView = new Vector2(bounds.center.x*1152, (1-bounds.center.y)*680);
            float startScale = Mathf.Clamp(Mathf.Min(originFrame.width / Mathf.Max(1,bounds.width*1152), originFrame.height / Mathf.Max(1,bounds.height*680)),.035f,.24f);
            float size = Mathf.Lerp(Mathf.Lerp(startScale,1.04f,grow),1,dock);
            Vector2 from = originFrame.center - centerInView*startScale;
            Vector2 hero = new Vector2(800,443)-new Vector2(576,340)*1.04f;
            Vector2 topLeft = Vector2.Lerp(Vector2.Lerp(from,hero,grow),new Vector2(448,108),dock);
            // A restrained lifting arc gives the acquisition weight without spinning away from the part's identity.
            topLeft.y -= Mathf.Sin(grow*Mathf.PI)*34;
            viewport.anchoredPosition = new Vector2(topLeft.x,-topLeft.y); viewport.localScale = Vector3.one*size;
            modelImage.color = new Color(1,1,1,EntryEase((age-.23f)/.14f));
            receiptGroup.alpha = 1-EntryEase((age-.25f)/.27f);
            Color surface = modalSurface.color; surface.a = Mathf.Lerp(0,.97f,EntryEase((age-.19f)/.42f));
            surface.a = Mathf.Lerp(surface.a,1,dock); modalSurface.color = surface;
            foreach (var layer in entryLayers) layer.group.alpha = EntryEase((age-layer.start)/layer.duration);
            var modelBounds = new Rect(topLeft.x+bounds.xMin*1152*size,topLeft.y+(1-bounds.yMax)*680*size,bounds.width*1152*size,bounds.height*680*size);
            acquisitionGraphic.Present(originFrame,modelBounds,age,sweep);
            acquisitionAtmosphere.Present(originFrame,modelBounds,age,sweep);
            if (age >= .65f && !scanSoundPlayed) { scanSoundPlayed = true; entryVoice.PlayOneShot(scanCue,.7f); }
            if (age >= 1.25f && !lockSoundPlayed) { lockSoundPlayed = true; entryVoice.PlayOneShot(lockCue,.65f); }
        }
        private void SetEntrySurfaces(float alpha)
        {
            if (modalSurface != null) { Color tint = modalSurface.color; tint.a = alpha; modalSurface.color = tint; }
            foreach (var layer in entryLayers) if (layer.group != null) layer.group.alpha = alpha;
        }
        private void RestoreEntryPresentation()
        {
            discoveryPresentation = openingShell = false;
            SetEntrySurfaces(1);
            if (viewport != null) { viewport.localScale = Vector3.one; viewport.anchoredPosition = new Vector2(448,-108); }
            if (receiptRect != null) receiptRect.gameObject.SetActive(false);
            if (acquisitionGraphic != null) acquisitionGraphic.gameObject.SetActive(false);
            if (acquisitionAtmosphere != null) acquisitionAtmosphere.gameObject.SetActive(false);
            if (stage != null) stage.EndArrivalPresentation();
        }
        private void CancelEntryPresentation() { StopEntryAudio(); RestoreEntryPresentation(); }
        private void StopEntryAudio() { if (entryVoice != null) entryVoice.Stop(); }
        private void DisposeEntryAudio()
        {
            StopEntryAudio();
            if (scanCue != null) Destroy(scanCue);
            if (lockCue != null) Destroy(lockCue);
        }
        private static AudioClip MakeEntryCue(bool latch)
        {
            // Quiet original device noise, independent of the approved rare-pickup master.
            const int rate = 48000;
            int count = Mathf.RoundToInt(rate*(latch ? .11f : .59f));
            var data = new float[count]; var random = new System.Random(latch ? 419 : 811);
            float low = 0;
            for (int i=0;i<count;i++)
            {
                float t=i/(float)rate, u=i/(float)(count-1), noise=(float)(random.NextDouble()*2-1);
                low += (noise-low)*.19f;
                float envelope = latch ? Mathf.Min(1,t/.0015f)*Mathf.Exp(-t*55) : Mathf.Pow(Mathf.Sin(Mathf.PI*u),1.8f);
                data[i]=(latch ? noise-low : low)*envelope*(latch ? .38f : .22f);
            }
            data[0]=data[count-1]=0;
            var clip=AudioClip.Create(latch ? "Sample_Lock_Confirm" : "Sample_Scan_Sweep",count,1,rate,false);
            clip.SetData(data,0); return clip;
        }
    }
}

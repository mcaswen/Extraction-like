using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceScienceUI
    {
        private RenderTexture switchSnapshot;
        private RawImage snapshotImage;
        private RectTransform archiveCursor;
        private readonly List<CanvasGroup> identityGroups = new List<CanvasGroup>();
        private readonly List<AerospaceHotspotPulse> pinPulses = new List<AerospaceHotspotPulse>();
        private readonly float[] pinConfirmationAge = { 1, 1, 1, 1 };
        private int archiveTarget = -1;
        private float archiveFrom, archiveAge = 1, identityAge = 1;
        public bool HasSwitchSnapshot => switchSnapshot != null;

        private void BuildMotionPresentation(Transform frame)
        {
            var snapshot = new GameObject("Previous_Sample_Frame", typeof(RectTransform), typeof(RawImage)); snapshot.transform.SetParent(viewport, false);
            Rect(snapshot.GetComponent<RectTransform>(), 0, 0, viewport.rect.width, viewport.rect.height); snapshot.transform.SetAsFirstSibling();
            snapshotImage = snapshot.GetComponent<RawImage>(); snapshotImage.raycastTarget = false; snapshot.SetActive(false);
            archiveCursor = Panel(frame, "Archive_Selection_Cursor", Amber, 472, 86, 174, 2); archiveCursor.gameObject.SetActive(false);
            foreach (var label in new[] { heading, subtitle, english, sampleId, watermark, locationTitle, locationBody, locationTag })
                identityGroups.Add(label.gameObject.AddComponent<CanvasGroup>());
            foreach (var pin in pins)
            {
                var go = new GameObject("Hotspot_Confirmation", typeof(RectTransform), typeof(AerospaceHotspotPulse)); go.transform.SetParent(pin.transform, false);
                var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var pulse = go.GetComponent<AerospaceHotspotPulse>(); pulse.raycastTarget = false; pulse.color = Amber; pinPulses.Add(pulse);
            }
        }
        private void CaptureSwitchSnapshot(bool discovery)
        {
            if (discovery || stage.ReducedMotion) { ReleaseSwitchSnapshot(); return; }
            if (modelImage.texture == null || modelImage.color.a < .5f) return; // Retain the previous frame during rapid replacement.
            var source = modelImage.texture;
            var capture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
            { name = "Aerospace_Switch_Snapshot", filterMode = FilterMode.Bilinear, antiAliasing = 1 };
            if (!capture.Create()) { Destroy(capture); return; }
            var active = RenderTexture.active;
            Graphics.Blit(source, capture); RenderTexture.active = active;
            ReleaseSwitchSnapshot(); switchSnapshot = capture; snapshotImage.texture = capture;
            snapshotImage.color = Color.white; snapshotImage.gameObject.SetActive(true);
        }
        private void ReleaseSwitchSnapshot()
        {
            if (snapshotImage != null) { snapshotImage.texture = null; snapshotImage.gameObject.SetActive(false); }
            if (switchSnapshot != null) { switchSnapshot.Release(); Destroy(switchSnapshot); switchSnapshot = null; }
        }
        private void ApplyCurrentIdentity()
        {
            var copy = AerospaceScienceCopy.For(current.code);
            heading.text = copy.displayTitle; english.text = current.english;
            sampleId.text = current.code; watermark.text = current.code; subtitle.text = copy.system;
            locationTitle.text = current.locationTitle; locationBody.text = current.locationText; locationTag.text = current.code + " · " + copy.shortName;
            locationGraphic.locationCode = current.code; locationGraphic.SetVerticesDirty();
            identityAge = 0;
        }
        private void MoveArchiveCursor(int index)
        {
            if (archiveCursor == null || archiveTarget == index) return;
            bool visible = archiveCursor.gameObject.activeSelf;
            archiveTarget = index; archiveFrom = visible ? archiveCursor.anchoredPosition.x : 472 + index * 182;
            archiveAge = 0; archiveCursor.gameObject.SetActive(index >= 0);
        }
        private void TickPresentationMotion()
        {
            identityAge += Time.unscaledDeltaTime;
            float alpha = stage.ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, identityAge / .23f);
            foreach (var group in identityGroups) group.alpha = alpha;
            archiveAge += Time.unscaledDeltaTime;
            if (archiveTarget >= 0)
                archiveCursor.anchoredPosition = new Vector2(Mathf.Lerp(archiveFrom, 472 + archiveTarget * 182, stage.ReducedMotion ? 1 : Mathf.SmoothStep(0, 1, archiveAge / .24f)), -86);
            for (int i = 0; i < pinConfirmationAge.Length; i++)
            {
                pinConfirmationAge[i] = Mathf.Min(1, pinConfirmationAge[i] + Time.unscaledDeltaTime);
                pinPulses[i].SetProgress(stage.ReducedMotion || selectedHotspot != i ? 1 : pinConfirmationAge[i] / .46f);
            }
        }
        private void ClearHotspotConfirmation()
        {
            for (int i = 0; i < pinConfirmationAge.Length; i++) { pinConfirmationAge[i] = 1; if (i < pinPulses.Count) pinPulses[i].SetProgress(1); }
        }
    }
}

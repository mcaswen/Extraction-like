using UnityEngine;
using UnityEngine.EventSystems;
namespace ExtractionLike.Aerospace
{
    public sealed class AerospaceOrbitInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IScrollHandler
    {
        public AerospaceInspectionStage stage;
        public System.Func<bool> canInteract;
        private bool Allowed => canInteract == null || canInteract();
        public void OnBeginDrag(PointerEventData e) { if (Allowed && e.button == PointerEventData.InputButton.Left) stage?.BeginOrbitGesture(); }
        public void OnDrag(PointerEventData e)
        {
            if (!Allowed || e.button != PointerEventData.InputButton.Left) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor * transform.parent.localScale.x : 1;
            stage?.Orbit(e.delta / Mathf.Max(.1f, scale));
        }
        public void OnEndDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) stage?.EndOrbitGesture(); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) stage?.EndOrbitGesture(); }
        public void OnScroll(PointerEventData e) { if (Allowed) stage?.Zoom(e.scrollDelta.y); }
        private void OnDisable() { if (stage != null) stage.CancelCameraMotion(); }
    }
}

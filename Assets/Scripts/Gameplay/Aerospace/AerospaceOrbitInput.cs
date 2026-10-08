using UnityEngine;
using UnityEngine.EventSystems;
namespace ExtractionLike.Aerospace
{
    public sealed class AerospaceOrbitInput : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public AerospaceInspectionStage stage;
        public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) stage?.Orbit(e.delta); }
        public void OnScroll(PointerEventData e) { stage?.Zoom(e.scrollDelta.y); }
    }
}

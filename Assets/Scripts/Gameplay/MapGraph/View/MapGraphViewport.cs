using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.MapGraph.View
{
    /// <summary>同一个地图视图的显隐、紧凑/展开尺寸和缩放平移。不会触发路线规划。</summary>
    public sealed class MapGraphViewport : MonoBehaviour,IScrollHandler,IBeginDragHandler,IDragHandler
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private RectTransform _content;
        [SerializeField] private GameObject _backdrop;
        [SerializeField] private Canvas _canvas;
        private CanvasGroup _visibilityGroup;
        private MapGraphOverlayController _overlay;
        private Vector2 _pan;
        private float _zoom=1;
        private Vector2 _lastScreenSize;
        public bool IsExpanded { get; private set; }
        public bool IsVisible { get; private set; }=true;
        public int Revision { get; private set; }
        public float Zoom=>_zoom;
        public Vector2 Pan=>_pan;
        public RectTransform Panel=>_panel;
        public void Initialize(MapGraphOverlayController overlay) { _overlay=overlay; ApplyVisibility(); SetExpanded(false); }
        private void Update()
        {
            if(ExtractionLike.Aerospace.AerospaceUiInputGate.BlocksGameplayInput)return;
            if(Input.GetKeyDown(KeyCode.H)&&!IsTypingInInputField())ToggleVisibility();
            if(Input.GetKeyDown(KeyCode.M))ToggleExpanded();
            var size=new Vector2(Screen.width,Screen.height);
            if(size!=_lastScreenSize) { _lastScreenSize=size; Apply(); }
        }
        public void SetExpanded(bool expanded)
        { IsExpanded=expanded; _zoom=1; _pan=Vector2.zero; Apply(); }
        public void ToggleExpanded()
        {
            if(!IsVisible) {SetVisible(true);SetExpanded(true);}
            else SetExpanded(!IsExpanded);
        }
        public void ToggleVisibility()=>SetVisible(!IsVisible);
        public void SetVisible(bool visible)
        { IsVisible=visible;ApplyVisibility();Revision++; }
        private void ApplyVisibility()
        {
            if(_canvas==null)return;
            if(_visibilityGroup==null)
            {
                _visibilityGroup=_canvas.GetComponent<CanvasGroup>();
                if(_visibilityGroup==null)_visibilityGroup=_canvas.gameObject.AddComponent<CanvasGroup>();
            }
            // Do not deactivate the panel/root: Update must still receive H to restore the map.
            _visibilityGroup.alpha=IsVisible?1f:0f;
            _visibilityGroup.interactable=IsVisible;
            _visibilityGroup.blocksRaycasts=IsVisible;
            if(_backdrop!=null)_backdrop.SetActive(IsVisible&&IsExpanded);
        }
        private static bool IsTypingInInputField()
        {
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            return selected!=null&&(selected.GetComponent<UnityEngine.UI.InputField>()!=null||selected.GetComponent<TMPro.TMP_InputField>()!=null);
        }
        private void Apply()
        {
            if(_overlay==null)return;
            var canvasRect=(RectTransform)_canvas.transform; Vector2 available=canvasRect.rect.size;
            if(available.x<10)available=new Vector2(1920,1080);
            _backdrop.SetActive(IsVisible&&IsExpanded);
            _canvas.sortingOrder=IsExpanded?300:200;
            _panel.anchorMin=_panel.anchorMax=_panel.pivot=IsExpanded?Vector2.one*.5f:Vector2.one;
            _panel.sizeDelta=IsExpanded?new Vector2(Mathf.Min(1100,available.x-100),Mathf.Min(860,available.y-100)):new Vector2(380,350);
            _panel.anchoredPosition=IsExpanded?Vector2.zero:new Vector2(-20,-20);
            _overlay.SetViewport(new Vector2(_panel.sizeDelta.x,_panel.sizeDelta.y-94),IsExpanded,_zoom,_pan); Revision++;
        }
        public void OnScroll(PointerEventData data)
        { if(!IsVisible||!IsExpanded)return;_zoom=Mathf.Clamp(_zoom*Mathf.Pow(1.15f,data.scrollDelta.y),1,4);ClampPan();Apply(); }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data)
        { if(!IsVisible||!IsExpanded||data.button!=PointerEventData.InputButton.Left)return;_pan+=data.delta/Mathf.Max(.01f,_canvas.scaleFactor);ClampPan();Apply(); }
        private void ClampPan()
        { Vector2 limit=_panel.sizeDelta*(_zoom-1)*.5f;_pan=new Vector2(Mathf.Clamp(_pan.x,-limit.x,limit.x),Mathf.Clamp(_pan.y,-limit.y,limit.y)); }
    }
}

using Gameplay.MapGraph.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    public sealed class MapGraphZoneView : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private MapGraphSymbolGraphic _border;
        [SerializeField] private TMP_Text _name;
        private RectTransform _rect;
        public string ZoneId { get; private set; }
        public RectTransform RectTransform => _rect!=null?_rect:_rect=(RectTransform)transform;
        public RectTransform NameRect => _name.rectTransform;
        public void Initialize(MapGraphZoneDefinition zone,SO_MapGraphTheme theme)
        {
            ZoneId=zone.ZoneId; name="Zone_"+ZoneId;
            _fill.color=theme.ZoneFill; _fill.raycastTarget=false;
            _border.Configure(MapGraphSymbol.Border,theme.Border,1);
            _name.font=theme.Font; _name.text=zone.DisplayName; _name.color=theme.MutedText; _name.raycastTarget=false;
        }
        public void Layout(Vector2 center,Vector2 size,Vector2 safeSize,bool expanded)
        {
            RectTransform.anchoredPosition=center; RectTransform.sizeDelta=size;
            _name.rectTransform.anchoredPosition=Vector2.zero; _name.rectTransform.sizeDelta=safeSize;
            _name.fontSize=expanded?15:10; _name.enableAutoSizing=true; _name.fontSizeMax=expanded?15:10; _name.fontSizeMin=6;
        }
    }
}

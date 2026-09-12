using Gameplay.MapGraph.Config;
using TMPro;
using UnityEngine;

namespace Gameplay.MapGraph.View
{
    public sealed class MapGraphAgentView : MonoBehaviour
    {
        [SerializeField] private MapGraphSymbolGraphic _body;
        [SerializeField] private MapGraphSymbolGraphic _ring;
        [SerializeField] private TMP_Text _identity;
        [SerializeField] private TMP_Text _status;
        private RectTransform _rect;
        private SO_MapGraphTheme _theme;
        public string MarkerId { get; private set; }
        public Vector2 Position => _rect.anchoredPosition;
        public string IdentityText => _identity.text;
        public void Initialize(string id,SO_MapGraphTheme theme)
        {
            MarkerId=id; name="AgentMarker_"+id; _rect=(RectTransform)transform;_theme=theme;
            _body.Configure(MapGraphSymbol.Agent,theme.Text); _ring.Configure(MapGraphSymbol.Ring,theme.FocusedRoute,1);
            _identity.font=theme.Font; _identity.color=theme.Background; _identity.raycastTarget=false;
            _status.font=theme.Font; _status.color=theme.Text; _status.raycastTarget=false;
        }
        public void Refresh(Vector2 position,float size,string identity,string status,Color color,bool focused,bool expanded,bool retaliating=false,bool waiting=false)
        {
            _rect.anchoredPosition=position; _rect.sizeDelta=Vector2.one*size;
            _body.color=color; _identity.text=identity; _identity.fontSize=expanded?11:8;
            _ring.gameObject.SetActive(focused||retaliating||waiting); _ring.rectTransform.sizeDelta=Vector2.one*(size+5);
            _ring.color=retaliating?_theme.Enemy:waiting?_theme.Resource:_theme.FocusedRoute;
            _status.text=string.Empty; _status.fontSize=10;
            _status.rectTransform.anchoredPosition=new Vector2(0,size*.5f+9);
            _status.rectTransform.sizeDelta=new Vector2(140,16);
        }
    }
}

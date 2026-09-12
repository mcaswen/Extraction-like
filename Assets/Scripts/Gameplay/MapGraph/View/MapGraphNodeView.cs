using System;
using Gameplay.MapGraph.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>群图标和真实 UGUI 点击入口。目标决定权在 Presenter/Router。</summary>
    public sealed class MapGraphNodeView : MonoBehaviour,IPointerClickHandler,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField] private Image _bodyImage;
        [SerializeField] private MapGraphSymbolGraphic _symbol;
        [SerializeField] private MapGraphSymbolGraphic _targetRing;
        [SerializeField] private TMP_Text _identity;
        private Action<string> _click;
        private Action<string,bool> _hover;
        private SO_MapGraphTheme _theme;
        private MapGraphNodeKind _kind;
        private RectTransform _rect;
        private Vector2 _targetLabelSize;
        public string NodeId { get; private set; }
        public RectTransform RectTransform => _rect!=null?_rect:_rect=(RectTransform)transform;
        public float VisualRadius => RectTransform.sizeDelta.x*.5f;
        public bool HasTargetLabel=>!string.IsNullOrEmpty(_identity.text);
        public Rect TargetLabelBounds=>new Rect(RectTransform.anchoredPosition+_identity.rectTransform.anchoredPosition-_targetLabelSize*.5f,_targetLabelSize);
        public void Initialize(MapGraphNodeDefinition node,SO_MapGraphTheme theme,Action<string> click,Action<string,bool> hover)
        {
            NodeId=node.NodeId; _kind=node.NodeKind; _theme=theme; _click=click; _hover=hover;
            name="Cluster_"+NodeId; _bodyImage.color=theme.Background; _bodyImage.raycastTarget=true;
            _symbol.Configure(_kind==MapGraphNodeKind.Resource?MapGraphSymbol.Box:_kind==MapGraphNodeKind.Extraction?MapGraphSymbol.Exit:MapGraphSymbol.Enemy,theme.KindColor(_kind));
            _targetRing.Configure(MapGraphSymbol.Ring,theme.Route,1.2f);
            _identity.font=theme.Font; _identity.color=theme.Text; _identity.raycastTarget=false;
        }
        public void Layout(Vector2 position,float size,bool expanded)
        {
            RectTransform.anchoredPosition=position; RectTransform.sizeDelta=Vector2.one*size;
            _symbol.Configure(_kind==MapGraphNodeKind.Resource?MapGraphSymbol.Box:_kind==MapGraphNodeKind.Extraction?MapGraphSymbol.Exit:MapGraphSymbol.Enemy,_symbol.color,expanded?1.7f:1.2f);
            _targetRing.rectTransform.sizeDelta=Vector2.one*(size+7);
            _identity.rectTransform.anchoredPosition=new Vector2(0,-size*.5f-8);
            _identity.rectTransform.sizeDelta=new Vector2(72,14); _identity.fontSize=expanded?11:8;
            RefreshLabelSize();
        }
        public void Refresh(bool available,bool completed,string targets,bool pending)
        {
            _symbol.color=!available?_theme.Inactive:completed?Color.Lerp(_theme.KindColor(_kind),_theme.Inactive,.7f):_theme.KindColor(_kind);
            bool targeted=!string.IsNullOrEmpty(targets); _targetRing.gameObject.SetActive(targeted||pending);
            _targetRing.color=pending&&!targeted?_theme.MutedText:_theme.Route;
            string text=targets??string.Empty;
            if(_identity.text!=text){_identity.text=text;RefreshLabelSize();}
        }
        private void RefreshLabelSize()=>_targetLabelSize=new Vector2(Mathf.Min(72,_identity.GetPreferredValues(_identity.text).x)+4,_identity.fontSize+3);
        public void OnPointerClick(PointerEventData data)
        { if(data.button==PointerEventData.InputButton.Left) _click?.Invoke(NodeId); }
        public void OnPointerEnter(PointerEventData data) => _hover?.Invoke(NodeId,true);
        public void OnPointerExit(PointerEventData data) => _hover?.Invoke(NodeId,false);
    }
}

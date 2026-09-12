using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    /// <summary>保存可见直线端口，角色插值复用这两个端口。</summary>
    public sealed class MapGraphEdgeView : MonoBehaviour
    {
        [SerializeField] private Image _lineImage;
        private RectTransform _rect;
        private Color _baseColor;
        private SO_MapGraphTheme _theme;
        public string EdgeId { get; private set; }
        public string FromNodeId { get; private set; }
        public string ToNodeId { get; private set; }
        public Vector2 FromPort { get; private set; }
        public Vector2 ToPort { get; private set; }
        public bool GeometryValid { get; private set; }
        public void Initialize(MapGraphEdgeDefinition edge,SO_MapGraphTheme theme)
        {
            EdgeId=edge.EdgeId; FromNodeId=edge.FromNodeId; ToNodeId=edge.ToNodeId; _theme=theme;
            _rect=(RectTransform)transform; _baseColor=edge.UseColorOverride?edge.ColorOverride:theme.Edge;
            name="Connection_"+EdgeId; _lineImage.raycastTarget=false; _lineImage.color=_baseColor;
        }
        public void Layout(Vector2 from,Vector2 to,float width,float fromInset,float toInset)
        {
            Vector2 delta=to-from;
            GeometryValid=delta.sqrMagnitude>0.01f && (Mathf.Abs(delta.x)<0.01f || Mathf.Abs(delta.y)<0.01f);
            _lineImage.enabled=GeometryValid; if(!GeometryValid) return;
            bool horizontal=Mathf.Abs(delta.y)<0.01f;
            if(horizontal) to.y=from.y; else to.x=from.x;
            delta=to-from; float length=delta.magnitude; Vector2 direction=delta/length;
            FromPort=from+direction*Mathf.Clamp(fromInset,0,length*.4f);
            ToPort=to-direction*Mathf.Clamp(toInset,0,length*.4f);
            _rect.anchoredPosition=(FromPort+ToPort)*.5f;
            _rect.sizeDelta=horizontal?new Vector2(Vector2.Distance(FromPort,ToPort),width):new Vector2(width,Vector2.Distance(FromPort,ToPort));
            _rect.localRotation=Quaternion.identity;
        }
        public Vector2 Position(string executionFrom,float progress) => executionFrom==FromNodeId?
            MapGraphAgentPositionProjection.PositionOnSegment(FromPort,ToPort,progress):MapGraphAgentPositionProjection.PositionOnSegment(ToPort,FromPort,progress);
        public void Refresh(bool route,bool focused) => _lineImage.color=focused?_theme.FocusedRoute:route?_theme.Route:_baseColor;
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.MapGraph.View
{
    public enum MapGraphSymbol { Box, Enemy, Exit, Agent, Ring, Border }
    /// <summary>小尺寸可辨认的 UGUI 几何图标，和主题共用颜色，不依赖世界材质。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapGraphSymbolGraphic : MaskableGraphic
    {
        [SerializeField] private MapGraphSymbol _symbol;
        [SerializeField] private float _stroke=1.5f;
        public void Configure(MapGraphSymbol symbol,Color tint,float stroke=1.5f)
        { if(_symbol!=symbol||_stroke!=stroke) { _symbol=symbol; _stroke=stroke; SetVerticesDirty(); } color=tint; raycastTarget=false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect; float h=Mathf.Min(r.width,r.height)*0.38f; var c=r.center;
            if(_symbol==MapGraphSymbol.Border) { Rectangle(vh,r.min,r.max); return; }
            if(_symbol==MapGraphSymbol.Ring||_symbol==MapGraphSymbol.Agent)
            {
                const int count=24; float radius=Mathf.Min(r.width,r.height)*0.48f;
                for(int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                    var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius; var q=c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius;
                    if(_symbol==MapGraphSymbol.Ring) Line(vh,p,q); else Triangle(vh,c,p,q);
                }
                return;
            }
            if(_symbol==MapGraphSymbol.Box)
            {
                Rectangle(vh,c+new Vector2(-h,-h*.8f),c+new Vector2(h,h*.8f));
                Line(vh,c+new Vector2(-h,h*.25f),c+new Vector2(h,h*.25f));
                Line(vh,c+new Vector2(0,h*.8f),c+new Vector2(0,-h*.15f)); return;
            }
            if(_symbol==MapGraphSymbol.Enemy)
            {
                Line(vh,c+new Vector2(-h,-h*.65f),c+new Vector2(0,h));
                Line(vh,c+new Vector2(0,h),c+new Vector2(h,-h*.65f));
                Line(vh,c+new Vector2(h,-h*.65f),c+new Vector2(-h,-h*.65f));
                Line(vh,c+new Vector2(0,h*.28f),c+new Vector2(0,-h*.24f)); return;
            }
            Line(vh,c+new Vector2(-h*.15f,-h),c+new Vector2(-h,-h));
            Line(vh,c+new Vector2(-h,-h),c+new Vector2(-h,h));
            Line(vh,c+new Vector2(-h,h),c+new Vector2(-h*.15f,h));
            Line(vh,c+new Vector2(-h*.5f,0),c+new Vector2(h,0));
            Line(vh,c+new Vector2(h,0),c+new Vector2(h*.35f,h*.65f));
            Line(vh,c+new Vector2(h,0),c+new Vector2(h*.35f,-h*.65f));
        }
        private void Rectangle(VertexHelper vh,Vector2 min,Vector2 max)
        {
            float inset=_stroke*.5f; min+=Vector2.one*inset; max-=Vector2.one*inset;
            Line(vh,min,new Vector2(max.x,min.y)); Line(vh,new Vector2(max.x,min.y),max);
            Line(vh,max,new Vector2(min.x,max.y)); Line(vh,new Vector2(min.x,max.y),min);
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b)
        {
            var d=(b-a).normalized; var n=new Vector2(-d.y,d.x)*(_stroke*.5f); int index=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero); vh.AddVert(a+n,color,Vector2.zero); vh.AddVert(b+n,color,Vector2.zero); vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3);
        }
        private void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c)
        { int i=vh.currentVertCount; vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero); vh.AddVert(c,color,Vector2.zero); vh.AddTriangle(i,i+1,i+2); }
    }
}

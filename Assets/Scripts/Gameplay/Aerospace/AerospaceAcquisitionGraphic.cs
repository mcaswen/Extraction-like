using UnityEngine;
using UnityEngine.UI;

namespace ExtractionLike.Aerospace
{
    /// <summary>Finite acquisition optics: a soft rear field and a separate, crisp front reticle.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AerospaceAcquisitionGraphic : MaskableGraphic
    {
        public bool BehindModel { get; set; }
        private Rect source, sample;
        private float age, sweep;
        private static readonly Color Ice = new Color(.48f,.86f,1);
        private static readonly Color Blue = new Color(.12f,.46f,.88f);
        public void Present(Rect origin, Rect bounds, float time, float scan)
        { source=origin; sample=bounds; age=time; sweep=scan; SetVerticesDirty(); }
        private static float Ease(float start,float end,float t) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,t));
        private static Color Alpha(Color c,float alpha) { c.a=alpha;return c; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float reveal=Ease(.26f,.57f,age), fade=1-Ease(1.27f,1.70f,age), envelope=reveal*fade;
            if(BehindModel)
            {
                if(envelope<.001f)return;
                Vector2 center=sample.center;
                Vector2 radius=new Vector2(Mathf.Max(300,sample.width*.85f),Mathf.Max(260,sample.height*.64f));
                // All colour stays behind the live model; material and typography remain crisp.
                Glow(mesh,center,radius,Alpha(new Color(.018f,.14f,.36f),envelope*.82f));
                Glow(mesh,center+new Vector2(-radius.x*.20f,radius.y*.25f),radius*.67f,Alpha(new Color(.025f,.23f,.39f),envelope*.37f));
                Vector2 orbit=new Vector2(Mathf.Max(226,sample.width*.59f+38),Mathf.Max(246,sample.height*.57f));
                // Wide/flat parts have conservative renderer bounds; keep the optics inside the safe frame.
                orbit.x=Mathf.Min(orbit.x,Mathf.Max(120,Mathf.Min(center.x-84,1516-center.x)));
                orbit.y=Mathf.Min(orbit.y,Mathf.Max(120,Mathf.Min(center.y-104,826-center.y)));
                for(int i=0;i<4;i++)
                {
                    float start=i*90+age*12;
                    Arc(mesh,center,orbit,start,start+64,Alpha(Blue,envelope*.30f),.9f);
                    Arc(mesh,center,orbit+new Vector2(9,9),start+10,start+29,Alpha(Ice,envelope*.38f),1.5f);
                }
                // Sparse deterministic sampling traces, no random full-screen dust.
                for(int i=0;i<22;i++)
                {
                    float phase=i*2.399963f, travel=Mathf.Repeat(i*.618f+age*.24f,1);
                    float alpha=Mathf.Sin(travel*Mathf.PI)*envelope*(i%4==0?.62f:.28f);
                    Vector2 p=center+Vector2.Scale(new Vector2(Mathf.Cos(phase),Mathf.Sin(phase)),Vector2.Lerp(orbit*1.13f,orbit*.98f,travel));
                    Vector2 radial=(p-center).normalized;
                    Line(mesh,p+radial*(5+i%3*3),p,Alpha(Blue,alpha*.25f),1);
                    // Tiny soft crosses keep the particle mesh bounded.
                    Line(mesh,p-radial*3,p+radial*3,Alpha(Blue,alpha*.2f),4);
                    Line(mesh,p-radial,p+radial,Alpha(Ice,alpha),1.6f);
                }
                return;
            }
            float sourceAlpha=1-Ease(.27f,.54f,age);
            Brackets(mesh,Expand(source,6),Alpha(Ice,sourceAlpha*.94f),12,1.7f);
            if(age>.16f && age<.52f)
            {
                float pulse=Ease(.16f,.52f,age);
                Brackets(mesh,Expand(source,6+18*pulse),Alpha(Blue,(1-pulse)*.7f),8,1);
            }
            if(envelope<.001f)return;
            float settle=Ease(.43f,.72f,age);
            Rect aperture=Expand(sample,Mathf.Lerp(58,24,settle));
            aperture=Rect.MinMaxRect(Mathf.Max(72,aperture.xMin),Mathf.Max(100,aperture.yMin),Mathf.Min(1528,aperture.xMax),Mathf.Min(826,aperture.yMax));
            Color tint=Alpha(Ice,envelope*.76f);
            Brackets(mesh,aperture,tint,Mathf.Lerp(8,32,settle),1.35f);
            for(int i=0;i<13;i++)
            {
                float y=Mathf.Lerp(aperture.yMin,aperture.yMax,(i+1)/14f);
                float emphasis=sweep<0?0:Mathf.Exp(-Mathf.Pow(((i+1)/14f-sweep)*14,2));
                Color tick=Alpha(Ice,envelope*(.14f+emphasis*.70f));
                float length=i%3==0?12:5;
                Line(mesh,new Vector2(aperture.xMin-9,y),new Vector2(aperture.xMin-9-length,y),tick,1);
                Line(mesh,new Vector2(aperture.xMax+9,y),new Vector2(aperture.xMax+9+length,y),tick,1);
            }
            if(sweep>=0)
            {
                float y=Mathf.Lerp(sample.yMin,sample.yMax,sweep);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side<0?aperture.xMin:aperture.xMax;
                    Vector2 point=new Vector2(x,y);
                    Glow(mesh,point,new Vector2(20,8),Alpha(Blue,envelope*.55f));
                    Line(mesh,point+new Vector2(side*17,0),point-new Vector2(side*8,0),Alpha(Ice,envelope),2);
                    Line(mesh,point+new Vector2(side*6,-5),point-new Vector2(side*1,0),Alpha(Ice,envelope*.72f),1);
                    Line(mesh,point+new Vector2(side*6,5),point-new Vector2(side*1,0),Alpha(Ice,envelope*.72f),1);
                }
            }
            float locked=Ease(1.24f,1.30f,age)*(1-Ease(1.30f,1.58f,age));
            if(locked>.001f)
            {
                float expansion=Ease(1.24f,1.58f,age);
                Brackets(mesh,Expand(aperture,expansion*16),Alpha(Ice,locked*.65f),32,2);
            }
            float bottom=Mathf.Min(850,aperture.yMax+17);
            float width=Mathf.Clamp(sample.width*.36f,110,208);
            Line(mesh,new Vector2(aperture.center.x-width/2,bottom),new Vector2(aperture.center.x+width/2,bottom),Alpha(Blue,envelope*.4f),1);
            for(int i=0;i<18;i++)
            {
                float x=aperture.center.x-width/2+width*i/17;
                float done=sweep>=0 && i/17f<=sweep?1:.18f;
                Line(mesh,new Vector2(x,bottom-2),new Vector2(x,bottom+2),Alpha(Ice,envelope*done*.65f),1.5f);
            }
        }
        private static Rect Expand(Rect r,float d)=>new Rect(r.x-d,r.y-d,r.width+d*2,r.height+d*2);
        private void Brackets(VertexHelper mesh,Rect r,Color tint,float length,float width)
        {
            for(int i=0;i<4;i++)
            {
                float x=(i&1)==0?1:-1,y=(i&2)==0?1:-1;
                Vector2 p=new Vector2(x>0?r.xMin:r.xMax,y>0?r.yMin:r.yMax);
                Line(mesh,p,p+new Vector2(x*length,0),tint,width);
                Line(mesh,p,p+new Vector2(0,y*length),tint,width);
            }
        }
        private Vector2 Local(Vector2 p)=>new Vector2(rectTransform.rect.xMin+p.x,rectTransform.rect.yMax-p.y);
        private void Glow(VertexHelper mesh,Vector2 center,Vector2 radius,Color tint)
        {
            const int sides=48,rings=8;
            int start=mesh.currentVertCount;
            mesh.AddVert(Local(center),tint,Vector2.zero);
            for(int ring=1;ring<=rings;ring++)
            {
                float u=ring/(float)rings;
                Color c=Alpha(tint,tint.a*Mathf.Pow(1-u*u,3));
                for(int s=0;s<sides;s++)
                {
                    float a=s*Mathf.PI*2/sides;
                    mesh.AddVert(Local(center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radius)*u),c,Vector2.zero);
                    int n=start+1+(ring-1)*sides+s, next=start+1+(ring-1)*sides+(s+1)%sides;
                    if(ring==1)mesh.AddTriangle(start,n,next);
                    else { mesh.AddTriangle(n-sides,n,next);mesh.AddTriangle(n-sides,next,next-sides); }
                }
            }
        }
        private void Arc(VertexHelper mesh,Vector2 center,Vector2 radius,float from,float to,Color tint,float width)
        {
            int steps=Mathf.CeilToInt((to-from)/3);
            Vector2 previous=center+Vector2.Scale(new Vector2(Mathf.Cos(from*Mathf.Deg2Rad),Mathf.Sin(from*Mathf.Deg2Rad)),radius);
            for(int i=1;i<=steps;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)steps)*Mathf.Deg2Rad;
                Vector2 point=center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radius);
                Line(mesh,previous,point,tint,width);previous=point;
            }
        }
        private void Line(VertexHelper mesh,Vector2 a,Vector2 b,Color color,float width)
        {
            if(color.a<.001f || (a-b).sqrMagnitude<.001f)return;
            a=Local(a);b=Local(b);
            Vector2 direction=(b-a).normalized,normal=new Vector2(-direction.y,direction.x)*width*.5f;
            int n=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero);mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero);mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
        }
    }
}

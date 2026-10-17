using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionLike.Aerospace
{
    public sealed partial class AerospaceInspectionStage
    {
        private Mesh arrivalMesh;
        private MeshRenderer arrivalRenderer;
        private Material arrivalMaterial;
        private readonly List<Vector3> arrivalVertices = new List<Vector3>(4096);
        private readonly List<Vector2> arrivalUV = new List<Vector2>(4096);
        private readonly List<Color> arrivalColors = new List<Color>(4096);
        private readonly List<int> arrivalTriangles = new List<int>(6144);
        public bool ArrivalEffectsVisible => arrivalRenderer != null && arrivalRenderer.enabled;
        public int ArrivalVertexCount => arrivalMesh != null ? arrivalMesh.vertexCount : 0;

        private bool EnsureArrivalGeometry()
        {
            if (arrivalRenderer != null) return true;
            var shader = Resources.Load<Shader>("Aerospace/AcquisitionVolume");
            if (shader == null || !shader.isSupported) return false;
            arrivalMaterial = new Material(shader) { name = "Private sample acquisition optics" };
            var go = new GameObject("Sample_Scan_Volume", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = 31; go.transform.SetParent(stage.transform, false);
            arrivalMesh = new Mesh { name = "Finite sample scan rings" }; arrivalMesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = arrivalMesh;
            arrivalRenderer = go.GetComponent<MeshRenderer>(); arrivalRenderer.sharedMaterial = arrivalMaterial;
            arrivalRenderer.shadowCastingMode = ShadowCastingMode.Off; arrivalRenderer.receiveShadows = false;
            arrivalRenderer.lightProbeUsage = LightProbeUsage.Off; arrivalRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }
        private void TickArrivalGeometry()
        {
            float fadeIn = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.34f,.65f,arrivalAge));
            float fadeOut = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.26f,1.58f,arrivalAge));
            float alpha = fadeIn*fadeOut;
            if (!arrivalActive || ReducedMotion || alpha < .001f)
            {
                if (arrivalRenderer != null) arrivalRenderer.enabled = false;
                return;
            }
            if (!EnsureArrivalGeometry()) return;
            arrivalRenderer.enabled = true;
            arrivalVertices.Clear(); arrivalUV.Clear(); arrivalColors.Clear(); arrivalTriangles.Clear();
            Vector3 center = stage.transform.InverseTransformPoint(arrivalBounds.center);
            float rx = Mathf.Max(.17f,arrivalBounds.extents.x*1.20f+.045f);
            float rz = Mathf.Max(.17f,arrivalBounds.extents.z*1.20f+.045f);
            float top = arrivalTop-stage.transform.position.y, bottom = arrivalBottom-stage.transform.position.y;
            float height = Mathf.Lerp(top,bottom,scan < 0 ? arrivalAge < .65f ? 0 : 1 : scan);
            float phase = arrivalAge*38;
            Vector3 plane = new Vector3(center.x,height,center.z);
            Color blue = new Color(.08f,.43f,.90f,alpha*.22f);
            Color ice = new Color(.38f,.88f,1,alpha*.90f);
            // Depth-tested rings pass behind the opaque sample, then across its front in the same camera.
            ArrivalBand(plane,rx,rz,0,360,.023f,new Color(.06f,.40f,.85f,alpha*.14f));
            ArrivalBand(plane,rx,rz,0,360,.003f,ice);
            for (int i=0;i<6;i++)
            {
                float a=i*60+phase;
                ArrivalBand(plane,rx+.018f,rz+.018f,a,a+43,.006f,new Color(.16f,.64f,1,alpha*.62f));
                ArrivalBand(plane+Vector3.up*.026f,rx+.005f,rz+.005f,a+7,a+38,.0018f,blue);
            }
            // A translucent annulus suggests a physical measuring sheet without hiding the material.
            ArrivalBand(plane,rx*.94f,rz*.94f,0,360,.065f,new Color(.05f,.39f,.78f,alpha*.055f));
            Vector3 foot = new Vector3(center.x,bottom-.038f,center.z);
            for (int i=0;i<3;i++)
            {
                ArrivalBand(foot,rx*.91f,rz*.91f,i*120-phase*.32f,i*120+91-phase*.32f,.002f,blue);
                ArrivalBand(foot,rx*.95f,rz*.95f,i*120+16-phase*.32f,i*120+40-phase*.32f,.004f,new Color(.14f,.64f,.93f,alpha*.35f));
            }
            Vector3 right=viewCamera.transform.right, up=viewCamera.transform.up;
            for (int i=0;i<24;i++)
            {
                float angle=(i*137.508f+phase*.22f)*Mathf.Deg2Rad;
                float travel=Mathf.Repeat(i*.618034f+arrivalAge*.6f,1);
                float opacity=Mathf.Sin(travel*Mathf.PI)*alpha*.65f;
                Vector3 p=plane+new Vector3(Mathf.Cos(angle)*(rx+.018f),.07f*(travel-.5f),Mathf.Sin(angle)*(rz+.018f));
                ArrivalQuad(p-right*.003f-up*.004f,p+right*.003f-up*.004f,p+right*.003f+up*.004f,p-right*.003f+up*.004f,new Color(.40f,.85f,1,opacity));
            }
            arrivalMesh.Clear(); arrivalMesh.SetVertices(arrivalVertices); arrivalMesh.SetUVs(0,arrivalUV);
            arrivalMesh.SetColors(arrivalColors); arrivalMesh.SetTriangles(arrivalTriangles,0,false); arrivalMesh.RecalculateBounds();
        }
        private void ArrivalBand(Vector3 center,float rx,float rz,float from,float to,float width,Color color)
        {
            int segments=Mathf.CeilToInt((to-from)/4);
            for(int i=0;i<segments;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)segments)*Mathf.Deg2Rad;
                float b=Mathf.Lerp(from,to,(i+1)/(float)segments)*Mathf.Deg2Rad;
                Vector3 pa=center+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);
                Vector3 pb=center+new Vector3(Mathf.Cos(b)*rx,0,Mathf.Sin(b)*rz);
                Vector3 na=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width*.5f;
                Vector3 nb=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*width*.5f;
                ArrivalQuad(pa-na,pa+na,pb+nb,pb-nb,color);
            }
        }
        private void ArrivalQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
        {
            int n=arrivalVertices.Count;
            arrivalVertices.Add(a); arrivalVertices.Add(b); arrivalVertices.Add(c); arrivalVertices.Add(d);
            arrivalUV.Add(new Vector2(0,0)); arrivalUV.Add(new Vector2(0,1)); arrivalUV.Add(new Vector2(1,1)); arrivalUV.Add(new Vector2(1,0));
            for(int i=0;i<4;i++)arrivalColors.Add(color);
            arrivalTriangles.Add(n);arrivalTriangles.Add(n+1);arrivalTriangles.Add(n+2);
            arrivalTriangles.Add(n);arrivalTriangles.Add(n+2);arrivalTriangles.Add(n+3);
        }
        private void ClearArrivalGeometry()
        {
            if(arrivalRenderer!=null) { arrivalRenderer.enabled=false;Destroy(arrivalRenderer.gameObject); }
            if(arrivalMesh!=null)Destroy(arrivalMesh);
            if(arrivalMaterial!=null)Destroy(arrivalMaterial);
            arrivalRenderer=null;arrivalMesh=null;arrivalMaterial=null;
            arrivalVertices.Clear();arrivalUV.Clear();arrivalColors.Clear();arrivalTriangles.Clear();
        }
    }
}

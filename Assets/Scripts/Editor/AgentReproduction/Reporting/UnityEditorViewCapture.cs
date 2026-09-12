using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AgentReproduction.Reporting
{
    /// <summary>读取指定 Unity Editor 窗口的渲染表面，外部桌面遮挡不会进入截图。</summary>
    public static class UnityEditorViewCapture
    {
        public static Vector2Int Capture(EditorWindow window,string path,float requiredDarkFraction=0)
        {
            var rect=window.position;float scale=EditorGUIUtility.pixelsPerPoint;
            int width=Mathf.RoundToInt(rect.width*scale),height=Mathf.RoundToInt(rect.height*scale);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            var parent=typeof(EditorWindow).GetField("m_Parent",flags)?.GetValue(window);
            var viewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
            var grab=viewType?.GetMethod("GrabPixels",flags);
            if(parent==null||grab==null)throw new MissingMethodException("Unity 2022.3 GUIView.GrabPixels unavailable.");
            viewType.GetMethod("RepaintImmediately",flags)?.Invoke(parent,null);
            var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                grab.Invoke(parent,new object[]{target,new Rect(0,0,width,height)});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                var pixels=texture.GetPixels32();
                if(SystemInfo.graphicsUVStartsAtTop)
                {
                    for(int y=0;y<height/2;y++)for(int x=0;x<width;x++)
                    {int a=y*width+x,b=(height-1-y)*width+x;(pixels[a],pixels[b])=(pixels[b],pixels[a]);}
                    texture.SetPixels32(pixels);texture.Apply();
                }
                int dark=0;foreach(var p in pixels)if(p.r>5&&p.r<70&&p.g>10&&p.g<90&&p.b>12&&p.b<105)dark++;
                if(dark<pixels.Length*requiredDarkFraction)throw new InvalidOperationException("CaptureDoesNotContainExpectedDarkCanvas");
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,texture.EncodeToPNG());
                return new Vector2Int(width,height);
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}

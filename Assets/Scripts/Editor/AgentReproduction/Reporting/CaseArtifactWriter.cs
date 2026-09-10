using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AgentReproduction.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Reporting
{
    public static class CaseArtifactWriter
    {
        [Serializable] private sealed class EventRecord { public int frame; public float time; public string kind; public string detail; }
        [Serializable] private sealed class ResultRecord { public string caseId; public string execution; public string contract; public string message; public string persistentDataPath; }

        private static string Folder()
        {
            TestRunContext run = TestRunContext.Load();
            string name = TestContext.CurrentContext.Test.FullName;
            string hash;
            using (SHA256 algorithm = SHA256.Create())
                hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(name))).Replace("-", "").Substring(0, 16);
            string path = Path.Combine(run.outputPath, "cases", hash, run.repeat.ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        public static void Trace(string kind, string detail)
        {
            string json = JsonUtility.ToJson(new EventRecord { frame=Time.frameCount, time=Time.time, kind=kind, detail=detail });
            File.AppendAllText(Path.Combine(Folder(), "trace.jsonl"), json + "\n");
        }

        public static void Capture(Camera camera, string name)
        {
            var previous=RenderTexture.active;
            var target=camera.targetTexture;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Folder(),name+".png"),pixels.EncodeToPNG());
                Trace("screenshot",name+".png; "+target.width+"x"+target.height);
            }
            finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(pixels); }
        }

        public static void Complete(string execution)
        {
            var context = TestContext.CurrentContext;
            string result = JsonUtility.ToJson(new ResultRecord {
                caseId=context.Test.FullName, execution=execution,
                contract=context.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Passed ? "PASS" : "FAIL",
                message=context.Result.Message, persistentDataPath=Application.persistentDataPath
            }, true);
            string path = Path.Combine(Folder(), "case.json");
            File.WriteAllText(path + ".tmp", result);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
        }
    }
}

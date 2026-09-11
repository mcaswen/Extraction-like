#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidFrameSampler : IDisposable
    {
        private struct Frame
        {
            public int number;
            public double wall, milliseconds, game;
            public float scale;
            public long loop, gc, discovery, pawn, zone;
        }
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<Frame> _frames = new List<Frame>(16384);
        private readonly ProfilerRecorder[] _recorders = new ProfilerRecorder[5];
        private readonly string[] _descriptions = { "PlayerLoop: unavailable", "GC: unavailable", "Discovery: unavailable", "Pawn: unavailable", "Zone: unavailable" };
        private double _previous;
        private int _lastRenderFrame = -1;
        public int RenderedFrames { get; private set; }
        public int CameraWidth { get; private set; }
        public int CameraHeight { get; private set; }
        public int Count => _frames.Count;
        public bool Overflow { get; private set; }
        public string[] Descriptions => _descriptions;
        public SceneRaidFrameSampler() { RenderPipelineManager.endFrameRendering += Rendered; }
        private void Rendered(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
                if (camera != null && camera.cameraType == CameraType.Game && camera.targetTexture == null)
                {
                    CameraWidth = Math.Max(CameraWidth, camera.pixelWidth);
                    CameraHeight = Math.Max(CameraHeight, camera.pixelHeight);
                    if (_lastRenderFrame != Time.frameCount) { RenderedFrames++; _lastRenderFrame = Time.frameCount; }
                }
        }
        public void DiscoverCounters()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(handle);
                int slot = d.Name == "PlayerLoop" ? 0 : d.Name == "GC Allocated In Frame" ? 1 :
                    d.Name.StartsWith("AgentTargetDiscoveryController.Update()") ? 2 :
                    d.Name.StartsWith("AgentPawnRoot.Update()") ? 3 :
                    d.Name.StartsWith("TargetZoneAuthoring.Update()") ? 4 : -1;
                if (slot < 0 || _recorders[slot].Valid) continue;
                _recorders[slot] = new ProfilerRecorder(handle, 1,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
                _descriptions[slot] = d.Name + " | " + d.Category.Name + " | " + d.UnitType;
            }
        }
        // LastValue 是上一已结束的 Profiler 帧；CSV 明确命名 previous，不冒充本帧 Self 时间。
        private long Value(int i) => _recorders[i].Valid && _recorders[i].Count > 0 ? _recorders[i].LastValue : -1;
        public void Sample()
        {
            double now = _clock.Elapsed.TotalSeconds;
            if (_frames.Count >= 100000) { Overflow = true; return; }
            _frames.Add(new Frame
            {
                number = Time.frameCount, wall = now, milliseconds = _previous == 0 ? -1 : (now - _previous) * 1000,
                game = Time.timeAsDouble, scale = Time.timeScale,
                loop = Value(0), gc = Value(1), discovery = Value(2), pawn = Value(3), zone = Value(4)
            });
            _previous = now;
        }
        public void Save(string output)
        {
            using (var writer = new StreamWriter(Path.Combine(output, "frames.csv")))
            {
                writer.WriteLine("frame,wallSeconds,intervalMs,gameSeconds,timeScale,previousPlayerLoopRaw,previousGCBytes,previousDiscoveryRaw,previousPawnRaw,previousZoneRaw");
                foreach (var f in _frames)
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F6},{2:F6},{3:F6},{4},{5},{6},{7},{8},{9}",
                        f.number, f.wall, f.milliseconds, f.game, f.scale, f.loop, f.gc, f.discovery, f.pawn, f.zone));
            }
        }
        public void Dispose()
        {
            RenderPipelineManager.endFrameRendering -= Rendered;
            for (int i = 0; i < _recorders.Length; i++) if (_recorders[i].Valid) _recorders[i].Dispose();
        }
    }
}
#endif

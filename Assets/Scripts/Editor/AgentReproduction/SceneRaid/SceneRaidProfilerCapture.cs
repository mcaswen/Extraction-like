using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AnomalySearch.Automation.SceneRaid;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace AnomalySearch.Editor.SceneRaid
{
    /// <summary>显式诊断轮的 CPU 层级取证，结果不能作为无 Profiler 性能验收。</summary>
    internal sealed class SceneRaidProfilerCapture
    {
        [Serializable] private sealed class Cost
        {
            public string thread, parent, name;
            public long calls;
            public double totalMs, selfMs, maximumTotalMs;
        }
        [Serializable] private sealed class Sample
        { public int index, parent; public string name; public double totalMs, selfMs; }
        [Serializable] private sealed class SlowFrame
        {
            public int profilerFrame, observedAtGameFrame;
            public string thread;
            public double observedGameSeconds, observedRealtimeSeconds, durationMs;
            public Sample[] samples;
        }
        [Serializable] private sealed class Report
        {
            public string runId;
            public string connection;
            public bool driverEnabled, runtimeEnabled;
            public int firstFrame, lastFrame;
            public bool performanceAcceptance = false, truncated;
            public int sampledFrames, sampledGameFrames, skippedProfilerFrames, negativeSelfSamples;
            public double captureMilliseconds;
            public string[] threads;
            public Cost[] costs;
            public SlowFrame[] slowFrames;
        }
        private readonly string _runId, _output;
        private readonly Dictionary<string, Cost> _costs = new Dictionary<string, Cost>();
        private readonly HashSet<string> _threads = new HashSet<string>();
        private readonly List<SlowFrame> _slowFrames = new List<SlowFrame>();
        private readonly Stack<(int end, int index, string name)> _parents = new Stack<(int, int, string)>();
        private int _lastFrame, _sampled, _sampledGame, _skipped, _negativeSelf;
        private double _captureMs, _nextSample;
        private bool _truncated;
        public SceneRaidProfilerCapture(SceneRaidScenarioConfig config)
        {
            _runId = config.runId; _output = config.outputPath;
            // Runtime Profiler.enabled alone does not enable the Editor history consumer.
            ProfilerDriver.enabled = true;
            _lastFrame = ProfilerDriver.lastFrameIndex;
        }
        public void SampleLatest()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < _nextSample) return;
            _nextSample = now + 0.25;
            int frame = ProfilerDriver.lastFrameIndex;
            if (frame < 0 || frame <= _lastFrame) return;
            // Editor and game profiling use adjacent history frames. Read both, at 4 Hz.
            int first = Math.Max(Math.Max(_lastFrame + 1, ProfilerDriver.firstFrameIndex), frame - 1);
            _skipped += Math.Max(0, first - _lastFrame - 1);
            _lastFrame = frame;
            if (_sampled >= 12000) { _truncated = true; return; }
            long started = Stopwatch.GetTimestamp();
            bool wasRecording = UnityEngine.Profiling.Profiler.enabled;
            // Do not profile the collector's allocations while it reads profiler samples.
            // Otherwise each read creates GC.Alloc samples that the next read expands again.
            UnityEngine.Profiling.Profiler.enabled = false;
            try
            {
                for (int current = first; current <= frame; current++)
                {
                    bool captured = false;
                    for (int thread = 0; thread < 256; thread++)
                    {
                        using var view = ProfilerDriver.GetRawFrameDataView(current, thread);
                        if (!view.valid) break;
                        _threads.Add(view.threadName);
                        if (view.threadName != "Main Thread" && view.threadName != "Render Thread") continue;
                        Capture(view, current);
                        captured = true;
                    }
                    if (captured) _sampled++;
                }
            }
            finally
            {
                _captureMs += (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                UnityEngine.Profiling.Profiler.enabled = wasRecording;
            }
        }
        private void Capture(RawFrameDataView view, int frame)
        {
            _parents.Clear();
            bool gameFrame = false;
            bool saveSlow = view.frameTimeMs > 16.6667 && (_slowFrames.Count < 60 ||
                view.frameTimeMs > _slowFrames.Min(x => x.durationMs));
            var samples = saveSlow ? new List<Sample>() : null;
            for (int i = 0; i < view.sampleCount; i++)
            {
                while (_parents.Count > 0 && i > _parents.Peek().end) _parents.Pop();
                string name = view.GetSampleName(i);
                if (view.threadName == "Main Thread" && name == "PlayerLoop" && view.GetSampleChildrenCountRecursive(i) > 0) gameFrame = true;
                string parent = _parents.Count > 0 ? _parents.Peek().name : "";
                int parentIndex = _parents.Count > 0 ? _parents.Peek().index : -1;
                int descendants = view.GetSampleChildrenCountRecursive(i);
                double total = view.GetSampleTimeMs(i), self = total;
                int end = i + descendants;
                for (int child = i + 1; child <= end; child += 1 + view.GetSampleChildrenCountRecursive(child))
                    self -= view.GetSampleTimeMs(child);
                if (self < -0.01) _negativeSelf++;
                self = Math.Max(0, self);
                string key = view.threadName + "|" + parent + "|" + name;
                if (!_costs.TryGetValue(key, out var cost))
                {
                    cost = new Cost { thread = view.threadName, parent = parent, name = name };
                    _costs.Add(key, cost);
                }
                cost.calls++; cost.totalMs += total; cost.selfMs += self;
                cost.maximumTotalMs = Math.Max(cost.maximumTotalMs, total);
                if (samples != null && total >= 0.05)
                    samples.Add(new Sample { index = i, parent = parentIndex, name = name, totalMs = total, selfMs = self });
                if (descendants > 0) _parents.Push((end, i, name));
            }
            if (gameFrame) _sampledGame++;
            if (!saveSlow) return;
            _slowFrames.Add(new SlowFrame { profilerFrame = frame, observedAtGameFrame = Time.frameCount,
                observedGameSeconds = Time.timeAsDouble, observedRealtimeSeconds = Time.realtimeSinceStartupAsDouble,
                durationMs = view.frameTimeMs, thread = view.threadName, samples = samples.ToArray() });
            if (_slowFrames.Count > 60)
            {
                var smallest = _slowFrames.OrderBy(x => x.durationMs).First();
                _slowFrames.Remove(smallest);
            }
        }
        public void Save()
        {
            var report = new Report { runId = _runId, sampledFrames = _sampled, skippedProfilerFrames = _skipped,
                sampledGameFrames = _sampledGame,
                connection = ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler),
                driverEnabled = ProfilerDriver.enabled, runtimeEnabled = UnityEngine.Profiling.Profiler.enabled,
                firstFrame = ProfilerDriver.firstFrameIndex, lastFrame = ProfilerDriver.lastFrameIndex,
                negativeSelfSamples = _negativeSelf, truncated = _truncated, captureMilliseconds = _captureMs,
                threads = _threads.OrderBy(x => x).ToArray(), costs = _costs.Values.OrderByDescending(x => x.selfMs).ToArray(),
                slowFrames = _slowFrames.OrderByDescending(x => x.durationMs).ToArray() };
            File.WriteAllText(Path.Combine(_output, "cpu-hierarchy.json"), JsonUtility.ToJson(report, true));
            if (_sampledGame == 0) throw new InvalidOperationException("CPU profile enabled but no readable PlayerLoop frame was captured.");
        }
    }
}

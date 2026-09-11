#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidEvidenceWriter : IDisposable
    {
        [Serializable] private sealed class EventRecord
        {
            public long sequence;
            public double wallSeconds, gameSeconds;
            public int frame;
            public string kind, detail;
        }
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<string> _pending = new List<string>(512);
        private readonly StreamWriter _events;
        private long _sequence;
        public int Lost { get; private set; }
        public int Count { get; private set; }
        public double WallSeconds => _clock.Elapsed.TotalSeconds;
        public SceneRaidEvidenceWriter(string output)
        {
            Directory.CreateDirectory(output);
            _events = new StreamWriter(Path.Combine(output, "events.jsonl"), false, new System.Text.UTF8Encoding(false), 65536);
        }
        public void Add(string kind, string detail)
        {
            long sequence = ++_sequence;
            if (_pending.Count >= 20000) { Lost++; return; }
            _pending.Add(JsonUtility.ToJson(new EventRecord
            {
                sequence = sequence, wallSeconds = WallSeconds, gameSeconds = Time.timeAsDouble,
                frame = Time.frameCount, kind = kind, detail = detail
            }));
            Count++;
        }
        public void Flush()
        {
            foreach (string line in _pending) _events.WriteLine(line);
            _pending.Clear();
            _events.Flush();
        }
        public void Dispose() { Flush(); _events.Dispose(); }
    }
}
#endif

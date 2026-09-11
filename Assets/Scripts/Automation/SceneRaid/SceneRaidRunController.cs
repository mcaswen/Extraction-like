#if UNITY_EDITOR || ANOMALY_SCENE_AUTOMATION
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AnomalySearch.Automation.SceneRaid
{
    public sealed class SceneRaidRunController : MonoBehaviour
    {
        private SceneRaidScenarioConfig _config;
        private SceneRaidEvidenceWriter _writer;
        private SceneRaidObserver _observer;
        private SceneRaidReadModel _model;
        private SceneRaidFrameSampler _sampler;
        private readonly SceneRaidContracts _contracts = new SceneRaidContracts();
        private double _nextSnapshot, _nextFlush, _instrumentation;
        private int _updates;
        private float _originalScale, _originalFixed;
        private bool _complete;
        public void Initialize(SceneRaidScenarioConfig config)
        {
            _config = config;
            _originalScale = Time.timeScale; _originalFixed = Time.fixedDeltaTime;
            _writer = new SceneRaidEvidenceWriter(config.outputPath);
            var identity = new SceneRaidIdentityMap();
            _observer = new SceneRaidObserver(_writer, identity);
            _model = new SceneRaidReadModel(identity, _observer.LatestResource);
            _sampler = new SceneRaidFrameSampler();
            _writer.Add("bootstrap.beforeSceneLoad", JsonUtility.ToJson(config));
            _writer.Flush();
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }
        private void LateUpdate()
        {
            if (_config == null || _complete) return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                _updates++;
                _sampler.Sample();
                if (_updates == 3)
                {
                    long queryStart = Stopwatch.GetTimestamp();
                    File.WriteAllText(Path.Combine(_config.outputPath, "runtime-world.json"), JsonUtility.ToJson(_model.CaptureWorld(), true));
                    _writer.Add("diagnostic.worldAudit", "durationMs=" + ((Stopwatch.GetTimestamp() - queryStart) * 1000.0 / Stopwatch.Frequency).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                if (_updates == 3 || _updates == 30) _sampler.DiscoverCounters();
                if (_writer.WallSeconds >= _nextSnapshot)
                {
                    var snapshot = _model.Capture();
                    _observer.Snapshot(snapshot);
                    _contracts.Observe(snapshot, _writer);
                    _nextSnapshot = _writer.WallSeconds + (snapshot.agents.Any(x => x.enemy != null) ? 0.25 : 1);
                }
                if (_writer.WallSeconds >= _nextFlush)
                {
                    _writer.Flush();
                    File.WriteAllText(Path.Combine(_config.outputPath, "heartbeat.json"),
                        "{\"phase\":\"Observe\",\"frame\":" + Time.frameCount + "}");
                    _nextFlush = _writer.WallSeconds + 5;
                }
                if (_writer.WallSeconds >= _config.observeSeconds && _updates > 3) Finish("OBSERVED", "Observation deadline reached; no player input supplied.");
            }
            catch (Exception ex) { Finish("HARNESS_FAILED", ex.ToString()); }
            finally { _instrumentation += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; }
        }
        private void Finish(string status, string reason)
        {
            if (_complete) return;
            _complete = true;
            _observer.Snapshot(_model.Capture());
            if (_writer.Lost > 0 || _sampler.Overflow) { status = "HARNESS_FAILED"; reason = "Evidence buffer overflow."; }
            _writer.Add("run.completed", status + ": " + reason);
            _writer.Flush();
            _sampler.Save(_config.outputPath);
            var result = new SceneRaidRunResult
            {
                runId = _config.runId, mode = _config.mode, status = status, reason = reason,
                scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], persistentDataPath = Application.persistentDataPath,
                batchMode = Application.isBatchMode, profilerEnabled = UnityEngine.Profiling.Profiler.enabled,
                performanceAcceptance = false, screenWidth = Screen.width, screenHeight = Screen.height,
                cameraWidth = _sampler.CameraWidth, cameraHeight = _sampler.CameraHeight,
                frames = _sampler.Count, renderedFrames = _sampler.RenderedFrames, events = _writer.Count, lostEvents = _writer.Lost,
                errors = _observer.Errors, warnings = _observer.Warnings, elapsedWallSeconds = _writer.WallSeconds,
                elapsedGameSeconds = Time.timeAsDouble, instrumentationMilliseconds = _instrumentation,
                observedAgents = _observer.ObservedAgents.OrderBy(x => x).ToArray(), counters = _sampler.Descriptions
            };
            _observer.Dispose(); _sampler.Dispose(); _writer.Dispose();
            // 结果最后原子出现；进程层另外验证完整文件和事件终态。
            string path = Path.Combine(_config.outputPath, "result.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(result, true));
            File.Move(path + ".tmp", path);
        }
        private void OnDestroy()
        {
            if (_config == null) return;
            if (!_complete)
            {
                _observer?.Dispose(); _sampler?.Dispose(); _writer?.Dispose();
            }
            Time.timeScale = _originalScale; Time.fixedDeltaTime = _originalFixed;
        }
    }
}
#endif

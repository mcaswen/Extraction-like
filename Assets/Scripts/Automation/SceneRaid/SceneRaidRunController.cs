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
        private SceneRaidInventoryDriver _inventory;
        private Commands.SceneRaidCommandEvidence _commandEvidence;
        private Commands.SceneRaidClusterCommandDriver _commands;
        private StreamWriter _carriedFile;
        private int _inventoryRevision = -1;
        private bool _initialInventoryCaptured;
        private readonly SceneRaidContracts _contracts = new SceneRaidContracts();
        private double _nextSnapshot, _nextFlush, _instrumentation;
        private int _updates;
        private float _originalScale, _originalFixed;
        private bool _complete, _missionCompleted, _missionFailed;
        private bool _screenshotRequested;
        private bool _commandCatalogCaptured;
        public void Initialize(SceneRaidScenarioConfig config)
        {
            _config = config;
            _originalScale = Time.timeScale; _originalFixed = Time.fixedDeltaTime;
            _writer = new SceneRaidEvidenceWriter(config.outputPath);
            SceneRaidPersistenceEvidence.CaptureWarehouse(config.outputPath, "initial");
            var identity = new SceneRaidIdentityMap();
            _observer = new SceneRaidObserver(_writer, identity);
            _model = new SceneRaidReadModel(identity, _observer.LatestResource);
            _observer.CaptureDirective = _model.CaptureDirective;
            _observer.CaptureNavigation = _model.CaptureNavigation;
            if (config.mode == "Autonomous" || config.mode == "ManualCluster")
                _inventory = new SceneRaidInventoryDriver(_writer, identity, _observer.LatestResource);
            if (config.mode == "ManualCluster")
            {
                var scenario = config.ParseCommandScenario();
                _commandEvidence = new Commands.SceneRaidCommandEvidence(config.outputPath, scenario.id, _writer, _observer, _model, identity);
                _commands = new Commands.SceneRaidClusterCommandDriver(config.outputPath, scenario,
                    new Commands.SceneRaidClusterCatalog(identity), _commandEvidence, _inventory, _writer, identity);
                _carriedFile = new StreamWriter(Path.Combine(config.outputPath, "carried-inventory.jsonl"), false, new System.Text.UTF8Encoding(false));
                _observer.DirectiveObserved += CaptureExtractionInventory;
            }
            _sampler = new SceneRaidFrameSampler(config.observeSeconds);
            _writer.Add("bootstrap.beforeSceneLoad", JsonUtility.ToJson(config));
            _writer.Flush();
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }
        private void Start()
        {
            if (_config == null) return;
            // 场景 Awake（包括 RaidFlow 的时钟复位）完成后，仅应用一次测试速度。
            Time.timeScale = _config.simulationSpeed;
            _writer.Add("simulation.started", "requested=" + _config.simulationSpeed +
                "; actual=" + Time.timeScale + "; fixedDeltaTime=" + Time.fixedDeltaTime);
        }
        private void LateUpdate()
        {
            if (_config == null || _complete) return;
            long start = Stopwatch.GetTimestamp();
            try
            {
                _updates++;
                _sampler.Sample();
                _commandEvidence?.ObserveProgress();
                if (_commands != null && !_initialInventoryCaptured && _updates >= 3)
                {
                    _initialInventoryCaptured = CaptureCarried("initial");
                    if (!_initialInventoryCaptured && _writer.WallSeconds >= 10)
                        throw new InvalidOperationException("Initial carried inventory did not become readable.");
                }
                bool commandAction = _commands != null && _initialInventoryCaptured && _commands.Tick();
                if (!commandAction) _inventory?.Tick();
                if (_commands != null && _initialInventoryCaptured && _inventoryRevision != _inventory.MutationRevision)
                {
                    _inventoryRevision = _inventory.MutationRevision;
                    if (!CaptureCarried("inventoryRevision:" + _inventoryRevision))
                        throw new InvalidOperationException("Changed carried inventory is not readable.");
                }
                if (_config.captureCommandCatalog && !_commandCatalogCaptured && _updates >= 3)
                {
                    var actors = Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance;
                    if (actors != null && actors.RegisteredAgents.Count == 2 && actors.RegisteredAgents.All(x =>
                        x.IsAlive && Gameplay.Agent.Navigation.AgentNavigationQuery.IsReady(x.PawnRoot.NavMeshAgent)))
                    {
                        long queryStart = Stopwatch.GetTimestamp();
                        var catalog = new Commands.SceneRaidClusterCatalog(new SceneRaidIdentityMap()).Capture();
                        File.WriteAllText(Path.Combine(_config.outputPath, "command-catalog.json"), JsonUtility.ToJson(catalog, true));
                        _commandCatalogCaptured = true;
                        _writer.Add("diagnostic.commandCatalog", "durationMs=" +
                            ((Stopwatch.GetTimestamp() - queryStart) * 1000.0 / Stopwatch.Frequency).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else if (_writer.WallSeconds >= 10) throw new InvalidOperationException("Cluster catalog actors did not become ready.");
                }
                if (_updates == 3)
                {
                    long queryStart = Stopwatch.GetTimestamp();
                    File.WriteAllText(Path.Combine(_config.outputPath, "runtime-world.json"), JsonUtility.ToJson(_model.CaptureWorld(), true));
                    SceneRaidRenderEvidence.Save(_config.outputPath, "render-startup", _sampler);
                    _writer.Add("diagnostic.worldAudit", "durationMs=" + ((Stopwatch.GetTimestamp() - queryStart) * 1000.0 / Stopwatch.Frequency).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                _sampler.DiscoverCounters();
                if (!Application.isEditor && !_screenshotRequested && _sampler.RenderedFrames >= 30)
                {
                    _screenshotRequested = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(_config.outputPath, "render-check.png"));
                }
                if (_writer.WallSeconds >= _nextSnapshot)
                {
                    var snapshot = _model.Capture();
                    _missionCompleted = snapshot.missionCompleted;
                    _missionFailed = snapshot.missionFailed;
                    _observer.Snapshot(snapshot);
                    _contracts.Observe(snapshot, _writer);
                    _nextSnapshot = _writer.WallSeconds + (snapshot.agents.Any(x => x.enemy != null) ? 0.25 : 1);
                }
                if (_writer.WallSeconds >= _nextFlush)
                {
                    _writer.Flush();
                    File.WriteAllText(Path.Combine(_config.outputPath, "heartbeat.json"),
                        "{\"phase\":\"" + _config.mode + "\",\"frame\":" + Time.frameCount + "}");
                    _nextFlush = _writer.WallSeconds + 5;
                }
                if (_writer.WallSeconds >= 10 && _sampler.RenderedFrames == 0)
                    Finish("HARNESS_FAILED", "No rendered game-camera frames after ten seconds; inspect render-final.json.");
                else if (_observer.ProbeFailure != null) Finish("HARNESS_FAILED", _observer.ProbeFailure);
                else if (_inventory?.BlockedReason != null) Finish("BEHAVIOR_BLOCKED", _inventory.BlockedReason);
                else if ((_config.mode == "Autonomous" || _config.mode == "ManualCluster") && _missionFailed)
                    Finish("RAID_OBSERVED_FAILURE", "Mission failure observed; verify death terminal state and surviving agents' settlement.");
                else if ((_config.mode == "Autonomous" || _config.mode == "ManualCluster") && _missionCompleted)
                    Finish("RAID_OBSERVED_COMPLETE", "Mission completion observed; final warehouse/coverage contracts are still required.");
                else if (_writer.WallSeconds >= _config.observeSeconds && _updates > 3)
                    Finish(_config.mode == "Observe" ? "OBSERVED" : "BEHAVIOR_BLOCKED", _config.mode == "Observe"
                        ? "Observation deadline reached; no player input supplied." : "Autonomous raid did not complete before its wall-clock deadline.");
            }
            catch (Exception ex) { Finish("HARNESS_FAILED", ex.ToString()); }
            finally { _instrumentation += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency; }
        }
        private void Finish(string status, string reason)
        {
            if (_complete) return;
            _complete = true;
            try
            {
                _inventory?.Dispose();
                _commands?.Stop("RaidEnded:" + status);
                if (_commands != null) CaptureCarried("finalLiveAgents");
                _observer.Snapshot(_model.Capture());
                SceneRaidPersistenceEvidence.CaptureWarehouse(_config.outputPath, "final");
                SceneRaidPersistenceEvidence.CaptureDefinitions(_config.outputPath, _config.runId);
            }
            catch (Exception ex) { status = "HARNESS_FAILED"; reason += "\nFinal capture/cleanup: " + ex; }
            finally
            {
                _observer.DirectiveObserved -= CaptureExtractionInventory;
                _commandEvidence?.Dispose(); _carriedFile?.Dispose();
            }
            if (_writer.Lost > 0 || _sampler.Overflow) { status = "HARNESS_FAILED"; reason = "Evidence buffer overflow."; }
            _writer.Add("run.completed", status + ": " + reason);
            _writer.Flush();
            _sampler.Save(_config.outputPath);
            SceneRaidRenderEvidence.Save(_config.outputPath, "render-final", _sampler);
            var result = new SceneRaidRunResult
            {
                runId = _config.runId, mode = _config.mode, status = status, reason = reason,
                scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                runtime = Application.isEditor ? "Editor" : "Player", graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                developmentBuild = UnityEngine.Debug.isDebugBuild, vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], persistentDataPath = Application.persistentDataPath,
                batchMode = Application.isBatchMode, profilerEnabled = UnityEngine.Profiling.Profiler.enabled,
                performanceAcceptance = false, screenWidth = Screen.width, screenHeight = Screen.height,
                cameraWidth = _sampler.CameraWidth, cameraHeight = _sampler.CameraHeight,
                frames = _sampler.Count, frameCapacity = _sampler.FrameCapacity,
                renderedFrames = _sampler.RenderedFrames, events = _writer.Count, lostEvents = _writer.Lost,
                errors = _observer.Errors, warnings = _observer.Warnings, elapsedWallSeconds = _writer.WallSeconds,
                elapsedGameSeconds = Time.timeAsDouble, instrumentationMilliseconds = _instrumentation,
                inventorySessions = _inventory?.CompletedSessions ?? 0,
                inventoryAgents = _inventory != null ? _inventory.ServedAgents.OrderBy(x => x).ToArray() : Array.Empty<string>(),
                observedAgents = _observer.ObservedAgents.OrderBy(x => x).ToArray(), counters = _sampler.Descriptions
            };
            _observer.Dispose(); _sampler.Dispose(); _writer.Dispose();
            // 结果最后原子出现；进程层另外验证完整文件和事件终态。
            string path = Path.Combine(_config.outputPath, "result.json");
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(result, true));
            File.Move(path + ".tmp", path);
            if (!Application.isEditor && _config.quitPlayerWhenComplete) Application.Quit(0);
        }
        private void OnDestroy()
        {
            if (_config == null) return;
            if (!_complete)
            {
                _inventory?.Dispose(); _commandEvidence?.Dispose(); _carriedFile?.Dispose();
                if (_observer != null) _observer.DirectiveObserved -= CaptureExtractionInventory;
                _observer?.Dispose(); _sampler?.Dispose(); _writer?.Dispose();
            }
            Time.timeScale = _originalScale; Time.fixedDeltaTime = _originalFixed;
        }

        [Serializable] private sealed class CarriedCapture
        {
            public int schemaVersion = 1, frame;
            public double gameSeconds, wallSeconds;
            public string reason;
            public SceneRaidCarriedInventoryEvidence.Record[] agents;
        }
        private bool CaptureCarried(string reason)
        {
            var registry = Gameplay.Agent.Runtime.AgentRuntimeRegistry.ActiveInstance;
            if (registry == null) return false;
            var settled = _model.Capture().settledAgents;
            var rows = registry.RegisteredAgents.Where(x => x.IsAlive && !settled.Contains(x.AgentId.Value))
                .Select(x => SceneRaidCarriedInventoryEvidence.Capture(InventoryScreenController.Instance, x.AgentId.Value)).ToArray();
            bool valid = rows.All(x => x.available);
            if (!_initialInventoryCaptured && (rows.Length != 2 || !valid)) return false;
            string json = JsonUtility.ToJson(new CarriedCapture { frame = Time.frameCount, gameSeconds = Time.timeAsDouble,
                wallSeconds = _writer.WallSeconds, reason = reason, agents = rows });
            _carriedFile.WriteLine(json); _carriedFile.Flush();
            _writer.Add("inventory.carried", json);
            return valid;
        }
        private void CaptureExtractionInventory(Gameplay.Agent.Commands.AgentDirectiveResult result, long sequence)
        {
            if (_initialInventoryCaptured && result.Stage == Gameplay.Agent.Commands.AgentDirectiveStage.Accepted &&
                result.Request.DirectiveType == Gameplay.Agent.Data.AgentDirectiveType.Extract && !CaptureCarried("extractionAccepted:" + result.Request.CommandId))
                throw new InvalidOperationException("Extraction carried inventory is not readable.");
        }
    }
}
#endif

using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Binding;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Raid
{
    /// <summary>正式地图的场景组合根。注册及导航拥有者提供变化，共享服务拥有成本预算，Pawn 拥有路线。</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RaidMapCommandInstaller : MonoBehaviour
    {
        private sealed class Registration
        {
            public AgentPawnRoot Pawn;
            public AgentNavigationProfile Profile;
            public string ProfileId;
            public AgentRouteEnvironment Environment;
        }
        private static readonly Unity.Profiling.ProfilerMarker TickMarker = new Unity.Profiling.ProfilerMarker("Anomaly.MapCommand.Installation");
        private readonly Dictionary<AgentId,Registration> _agents = new Dictionary<AgentId,Registration>();
        private readonly List<AgentId> _removed = new List<AgentId>();
        private readonly HashSet<string> _retainedProfiles = new HashSet<string>();
        private MapGraphBindingAuthoring _binding;
        private AgentRuntimeRegistry _registry;
        private RuntimeNavMeshSurfaceBuilder _navigationOwner;
        private long _navigationRevision = -1;
        private bool _rosterDirty;
        private double _nextProfileObservation;
        private string _navigationFingerprint;
        public MapGraphBindingAuthoring Binding => _binding;
        public MapGraphRouteEnvironmentService Environments { get; private set; }
        public int InstalledAgentCount => _agents.Count;
        public int FingerprintCaptureCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InstallForScene(scene);

        /// <summary>每次载入只读一次本场景层级；无正式 Binding 的旧场景不安装。</summary>
        public static RaidMapCommandInstaller InstallForScene(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded) return null;
            MapGraphBindingAuthoring binding = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<MapGraphBindingAuthoring>(true))
                {
                    if (candidate.MapDefinition == null || !candidate.MapDefinition.IsCommandGraph) continue;
                    if (binding != null) { Debug.LogError("同一场景存在多个指挥地图绑定，无法确定路线归属。", candidate); return null; }
                    binding = candidate;
                }
            if (binding == null) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var existing in root.GetComponentsInChildren<RaidMapCommandInstaller>(true))
                    if (existing.Binding == binding) return existing;
            var holder = new GameObject("[RaidMapCommandInstaller]");
            SceneManager.MoveGameObjectToScene(holder, scene);
            var installer = holder.AddComponent<RaidMapCommandInstaller>();
            installer.Configure(binding);
            return installer;
        }

        public void Configure(MapGraphBindingAuthoring binding)
        {
            if (binding == null) throw new System.ArgumentNullException(nameof(binding));
            ReleaseAll(); _binding = binding;
            if (isActiveAndEnabled) Initialize();
        }
        private void OnEnable() { if (_binding != null) Initialize(); }
        private void OnDisable() => ReleaseAll();
        private void Initialize()
        {
            CaptureNavigation();
            Environments = new MapGraphRouteEnvironmentService(_binding, _navigationFingerprint);
            _navigationOwner = FindNavigationOwner();
            _navigationRevision = _navigationOwner != null ? _navigationOwner.NavigationRevision : -1;
            SetNavigationState(false);
            AttachRegistry(AgentRuntimeRegistry.GetOrCreate());
            _rosterDirty = true; _nextProfileObservation = 0;
            TickInstallation();
        }
        private RuntimeNavMeshSurfaceBuilder FindNavigationOwner()
        {
            var owner = RuntimeNavMeshSurfaceBuilder.Instance;
            return owner != null && owner.gameObject.scene == _binding.gameObject.scene ? owner : null;
        }
        private void CaptureNavigation()
        {
            _navigationFingerprint = MapGraphNavigationFingerprint.Capture(_binding.gameObject.scene);
            FingerprintCaptureCount++;
        }
        private void AttachRegistry(AgentRuntimeRegistry registry)
        {
            if (_registry == registry) return;
            if (_registry != null) {
                _registry.AgentRegistered -= OnRosterChanged;
                _registry.AgentUnregistered -= OnRosterChanged;
            }
            _registry = registry;
            if (_registry != null) {
                _registry.AgentRegistered += OnRosterChanged;
                _registry.AgentUnregistered += OnRosterChanged;
            }
            _rosterDirty = true;
        }
        private void OnRosterChanged(AgentRuntimeHandle handle) => _rosterDirty = true;
        private void Update() => TickInstallation();

        public void TickInstallation()
        {
            using var marker = TickMarker.Auto();
            if (!isActiveAndEnabled || Environments == null) return;
            if (_binding == null) { ReleaseAll(); return; }
            if (_registry != AgentRuntimeRegistry.ActiveInstance) AttachRegistry(AgentRuntimeRegistry.ActiveInstance);
            var owner = FindNavigationOwner();
            long revision = owner != null ? owner.NavigationRevision : -1;
            bool navigationChanged = owner != _navigationOwner || revision != _navigationRevision;
            if (navigationChanged) {
                _navigationOwner = owner; _navigationRevision = revision; CaptureNavigation();
            }
            SetNavigationState(navigationChanged);
            if (_rosterDirty) ReconcileAgents();
            if (Time.realtimeSinceStartupAsDouble >= _nextProfileObservation) {
                _nextProfileObservation = Time.realtimeSinceStartupAsDouble + 0.5;
                foreach (var entry in _agents.Values)
                    if (entry.Pawn != null && !ProfileMatches(entry.Profile, entry.Pawn.NavMeshAgent)) SetProfile(entry);
                PruneProfiles();
            }
            Environments.Tick(2, 4);
            foreach (var entry in _agents.Values) {
                if (entry.Pawn == null || !entry.Pawn.isActiveAndEnabled || entry.Pawn.IsDead) continue;
                if (!Environments.TryGetEnvironment(entry.ProfileId, out var environment) || ReferenceEquals(entry.Environment, environment)) continue;
                entry.Environment = environment; entry.Pawn.ConfigureRoutes(environment);
            }
        }
        private void SetNavigationState(bool changed)
        {
            bool ready = _navigationOwner == null || !_navigationOwner.isActiveAndEnabled ||
                (!_navigationOwner.IsBuilding && !_navigationOwner.HasPendingBuild);
            Environments.SetNavigationState(ready, _navigationFingerprint, changed);
        }
        private void ReconcileAgents()
        {
            _rosterDirty = false; _removed.Clear();
            foreach (var pair in _agents) {
                var pawn = pair.Value.Pawn;
                if (_registry == null || pawn == null || !pawn.isActiveAndEnabled ||
                    !_registry.TryGetHandle(pair.Key, out var handle) || handle.PawnRoot != pawn) _removed.Add(pair.Key);
            }
            foreach (var id in _removed) { Release(_agents[id]); _agents.Remove(id); }
            if (_registry != null)
                foreach (var handle in _registry.RegisteredAgents) {
                    var pawn = handle.PawnRoot;
                    if (pawn == null || !pawn.isActiveAndEnabled || pawn.IsDead || pawn.gameObject.scene != _binding.gameObject.scene ||
                        _agents.ContainsKey(handle.AgentId)) continue;
                    var entry = new Registration { Pawn = pawn }; SetProfile(entry);
                    _agents.Add(handle.AgentId, entry); pawn.RouteResultPublished += OnRouteResult;
                }
            PruneProfiles();
        }
        private void SetProfile(Registration entry)
        {
            entry.Profile = AgentNavigationProfile.FromAgent(entry.Pawn.NavMeshAgent);
            var environment = Environments.GetEnvironment(entry.Profile);
            entry.ProfileId = environment.Costs.ProfileId;
            // 更新留给统一注入点，使同一帧先完成所有失效和预算工作。
            entry.Environment = null;
        }
        private static bool ProfileMatches(AgentNavigationProfile profile, UnityEngine.AI.NavMeshAgent nav)
        {
            if (profile == null || nav == null || profile.AgentTypeId != nav.agentTypeID || profile.AreaMask != nav.areaMask ||
                profile.SampleRadius != Mathf.Max(0.5f, nav.radius * 2) || profile.HeightTolerance != Mathf.Max(0.5f, nav.height * 0.5f)) return false;
            for (int i = 0; i < 32; i++) if (profile.GetAreaCost(i) != nav.GetAreaCost(i)) return false;
            return true;
        }
        private void PruneProfiles()
        {
            _retainedProfiles.Clear();
            foreach (var entry in _agents.Values) _retainedProfiles.Add(entry.ProfileId);
            Environments.PruneProfiles(_retainedProfiles);
        }
        private void OnRouteResult(AgentRouteResult result)
        {
            if (!result.IsReplan || result.Stage != AgentRouteStage.Planning ||
                (result.Reason != AgentRouteFailure.Unreachable && result.Reason != AgentRouteFailure.NoProgress &&
                 result.Reason != AgentRouteFailure.NavigationNotReady)) return;
            if (!_agents.TryGetValue(result.Request.TargetAgentId, out var entry) || entry.Pawn == null) return;
            var snapshot = entry.Pawn.RouteSnapshot;
            if (Environments.Graph != null && Environments.Graph.TryGetEdgeBetween(snapshot.PreviousNodeId, snapshot.CurrentNodeId, out var edge))
                Environments.InvalidateEdge(entry.ProfileId, edge.EdgeId, result.Reason.ToString());
        }
        private void Release(Registration entry)
        {
            if (entry.Pawn == null) return;
            entry.Pawn.RouteResultPublished -= OnRouteResult;
            entry.Pawn.ReleaseRouteEnvironment(entry.Environment);
        }
        private void ReleaseAll()
        {
            AttachRegistry(null);
            foreach (var entry in _agents.Values) Release(entry);
            _agents.Clear(); _retainedProfiles.Clear(); Environments = null;
        }
    }
}

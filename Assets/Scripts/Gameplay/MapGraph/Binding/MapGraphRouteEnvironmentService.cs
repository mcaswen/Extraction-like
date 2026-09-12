using System;
using System.Collections.Generic;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>场景内按导航配置共享图、成本和环境，拥有统一补验预算，不接管 Agent 注册或路线。</summary>
    public sealed class MapGraphRouteEnvironmentService
    {
        private sealed class ProfileRecord
        {
            public string Id;
            public AgentNavigationProfile Profile;
            public MapGraphNavigationCostService Costs;
            public MapGraphCostSnapshot EmptyCosts;
            public AgentRouteEnvironment Environment;
        }
        private readonly MapGraphBindingAuthoring _binding;
        private readonly Dictionary<string,ProfileRecord> _profiles = new Dictionary<string,ProfileRecord>(StringComparer.Ordinal);
        private readonly List<ProfileRecord> _ordered = new List<ProfileRecord>();
        private SO_MapGraphDefinition _definition;
        private long _definitionRevision = -1, _bindingRevision = -1, _targetRevision = -1, _contextVersion;
        private bool _available, _navigationReady = true;
        private string _navigationFingerprint;
        private int _profileCursor;
        public MapGraphService Graph { get; private set; }
        public MapGraphRouteTargetResolver Targets { get; private set; }
        public int ProfileCount => _ordered.Count;
        public long CalculationCount { get; private set; }
        public int PendingEdgeCount
        { get { int count=0; foreach(var profile in _ordered) count+=profile.Costs?.PendingEdgeCount ?? 0; return count; } }

        public MapGraphRouteEnvironmentService(MapGraphBindingAuthoring binding, string navigationFingerprint = "")
        {
            _binding = binding != null ? binding : throw new ArgumentNullException(nameof(binding));
            _navigationFingerprint = navigationFingerprint ?? string.Empty; EnsureContext();
        }

        public AgentRouteEnvironment GetEnvironment(AgentNavigationProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            EnsureContext();
            string id = MapGraphNavigationCostService.CaptureProfile(profile).ProfileId;
            if (!_profiles.TryGetValue(id, out var record))
            {
                record = new ProfileRecord { Id=id, Profile=profile, EmptyCosts=new MapGraphCostSnapshot(id,0,Array.Empty<MapGraphEdgeCost>()) };
                _profiles.Add(id,record); _ordered.Add(record); BuildCosts(record); Publish(record);
            }
            return record.Environment;
        }
        public bool TryGetEnvironment(string profileId, out AgentRouteEnvironment environment)
        {
            EnsureContext(); environment = null;
            if (!_profiles.TryGetValue(profileId ?? string.Empty,out var record)) return false;
            environment = record.Environment; return true;
        }
        /// <summary>由导航拥有者报告真实变化；不在本服务里扫描/重建世界导航。</summary>
        public void SetNavigationState(bool ready, string fingerprint, bool forceInvalidation = false)
        {
            EnsureContext(); fingerprint ??= string.Empty;
            bool changed = fingerprint != _navigationFingerprint || forceInvalidation;
            _navigationFingerprint = fingerprint; _navigationReady = ready;
            foreach (var record in _ordered)
            {
                if (changed) record.Costs?.InvalidateAll("NavigationChanged");
                Publish(record);
            }
        }

        /// <summary>总预算按边计，一条边两个有向查询；不同 profile 轮换，不按 Agent 数重复预算。</summary>
        public int Tick(int maximumEdges = 2, int maximumAnchorObservations = 4)
        {
            EnsureContext();
            if (Targets != null && maximumAnchorObservations > 0) Targets.RefreshAnchors(maximumAnchorObservations);
            if (_available && Targets.Revision != _targetRevision)
            {
                _targetRevision = Targets.Revision;
                // 修订事件才检查已有边锚点，不重新生成候选图，也不查询导航。
                foreach (var record in _ordered) record.Costs?.RefreshChangedAnchors(_definition.Edges.Count);
            }
            int work = 0, idle = 0;
            while (_available && _navigationReady && work < Math.Max(0,maximumEdges) && _ordered.Count > 0 && idle < _ordered.Count)
            {
                if (_profileCursor >= _ordered.Count) _profileCursor = 0;
                var record = _ordered[_profileCursor++]; var costs = record.Costs;
                if (costs == null || costs.PendingEdgeCount == 0) { idle++; continue; }
                long before = costs.CalculationCount;
                work += costs.ProcessPending(1); CalculationCount += costs.CalculationCount-before; idle=0;
            }
            foreach (var record in _ordered) Publish(record);
            return work;
        }

        public void InvalidateEdge(string profileId, string edgeId, string reason)
        {
            EnsureContext();
            if (!_profiles.TryGetValue(profileId ?? string.Empty,out var record)) return;
            record.Costs?.InvalidateEdge(edgeId,reason); Publish(record);
        }
        public void PruneProfiles(ISet<string> retainedProfiles)
        {
            if (retainedProfiles == null) throw new ArgumentNullException(nameof(retainedProfiles));
            for (int i=_ordered.Count-1;i>=0;i--)
            {
                var record=_ordered[i]; if (retainedProfiles.Contains(record.Id)) continue;
                _profiles.Remove(record.Id); _ordered.RemoveAt(i);
            }
            _profileCursor=0;
        }

        private void EnsureContext()
        {
            var definition = _binding != null ? _binding.MapDefinition : null;
            bool available = _binding != null && _binding.isActiveAndEnabled && definition != null && definition.IsCommandGraph && _binding.IsValid;
            long bindingRevision = _binding != null ? _binding.Revision : -1, definitionRevision = definition != null ? definition.Revision : -1;
            if (_definition == definition && _definitionRevision == definitionRevision && _bindingRevision == bindingRevision && _available == available) return;
            _definition=definition; _definitionRevision=definitionRevision; _bindingRevision=bindingRevision; _available=available;
            Graph=available ? new MapGraphService(definition) : null;
            Targets=_binding != null ? new MapGraphRouteTargetResolver(_binding) : null;
            _targetRevision=Targets?.Revision ?? -1;
            foreach (var record in _ordered) { BuildCosts(record); Publish(record); }
        }
        private void BuildCosts(ProfileRecord record)
        {
            record.Costs = _available ? new MapGraphNavigationCostService(_definition,_binding,record.Profile,"","",_navigationFingerprint) : null;
        }
        private void Publish(ProfileRecord record)
        {
            var costs = record.Costs?.Snapshot ?? record.EmptyCosts;
            bool navigationReady = _available && _navigationReady;
            bool ready = navigationReady && record.Costs != null && record.Costs.PendingEdgeCount == 0;
            var previous = record.Environment;
            if (previous != null && ReferenceEquals(previous.Graph,Graph) && ReferenceEquals(previous.Targets,Targets) &&
                ReferenceEquals(previous.Costs,costs) && previous.IsReady==ready && previous.NavigationReady==navigationReady &&
                previous.TargetRevision==(Targets?.Revision ?? 0)) return;
            record.Environment=new AgentRouteEnvironment(Graph,_definitionRevision,++_contextVersion,costs,Targets,record.Profile,ready,navigationReady);
        }
    }
}

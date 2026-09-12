using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>可序列化的导航配置数据；由 Binding 层适配实际导航查询，不引用 Agent。</summary>
    [Serializable]
    public sealed class MapGraphNavigationProfileData : ISerializationCallbackReceiver
    {
        [SerializeField] private string _profileId;
        [SerializeField] private int _agentTypeId;
        [SerializeField] private int _areaMask = -1;
        [SerializeField] private float _sampleRadius = 1;
        [SerializeField] private float _heightTolerance = 1;
        [SerializeField] private List<float> _areaCosts = new List<float>();
        [NonSerialized] private IReadOnlyList<float> _costView;
        public string ProfileId => _profileId ?? string.Empty;
        public int AgentTypeId => _agentTypeId;
        public int AreaMask => _areaMask;
        public float SampleRadius => _sampleRadius;
        public float HeightTolerance => _heightTolerance;
        public IReadOnlyList<float> AreaCosts => _costView ??= _areaCosts.AsReadOnly();
        public MapGraphNavigationProfileData(string profileId, int agentTypeId, int areaMask,
            float sampleRadius, float heightTolerance, IEnumerable<float> areaCosts)
        {
            _profileId = profileId; _agentTypeId = agentTypeId; _areaMask = areaMask;
            _sampleRadius = sampleRadius; _heightTolerance = heightTolerance;
            if (areaCosts != null) _areaCosts = new List<float>(areaCosts);
        }
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => _costView = null;
    }

    /// <summary>绑定锚点和两个方向的物理路程；无效方向用标志保存，不能把零长度误当可达。</summary>
    [Serializable]
    public sealed class MapGraphNavigationEdgeBake
    {
        [SerializeField] private string _edgeId, _fromNodeId, _toNodeId;
        [SerializeField] private Vector3 _fromAnchor, _toAnchor;
        [SerializeField] private bool _forwardAvailable, _reverseAvailable;
        [SerializeField] private float _forwardLength, _reverseLength;
        [SerializeField] private string _forwardFailure, _reverseFailure;
        public string EdgeId => _edgeId ?? string.Empty;
        public string FromNodeId => _fromNodeId ?? string.Empty;
        public string ToNodeId => _toNodeId ?? string.Empty;
        public Vector3 FromAnchor => _fromAnchor;
        public Vector3 ToAnchor => _toAnchor;
        public bool ForwardAvailable => _forwardAvailable && ValidLength(_forwardLength);
        public bool ReverseAvailable => _reverseAvailable && ValidLength(_reverseLength);
        public float ForwardLength => ForwardAvailable ? _forwardLength : float.PositiveInfinity;
        public float ReverseLength => ReverseAvailable ? _reverseLength : float.PositiveInfinity;
        public string ForwardFailure => _forwardFailure ?? string.Empty;
        public string ReverseFailure => _reverseFailure ?? string.Empty;
        public MapGraphNavigationEdgeBake(string edgeId, string fromNodeId, string toNodeId, Vector3 fromAnchor,
            Vector3 toAnchor, float forwardLength, float reverseLength, string forwardFailure = "", string reverseFailure = "")
        {
            _edgeId = edgeId; _fromNodeId = fromNodeId; _toNodeId = toNodeId;
            _fromAnchor = fromAnchor; _toAnchor = toAnchor;
            _forwardAvailable = ValidLength(forwardLength) && string.IsNullOrEmpty(forwardFailure);
            _reverseAvailable = ValidLength(reverseLength) && string.IsNullOrEmpty(reverseFailure);
            _forwardLength = _forwardAvailable ? forwardLength : 0;
            _reverseLength = _reverseAvailable ? reverseLength : 0;
            _forwardFailure = forwardFailure; _reverseFailure = reverseFailure;
        }
        private static bool ValidLength(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>导航烘焙的持久化快照。输入指纹和 profile 必须匹配，运行时才能使用边成本。</summary>
    [Serializable]
    public sealed class MapGraphNavigationBakeData : ISerializationCallbackReceiver
    {
        [SerializeField] private string _sceneFingerprint, _navigationFingerprint;
        [SerializeField] private long _revision;
        [SerializeField] private MapGraphNavigationProfileData _profile;
        [SerializeField] private List<MapGraphNavigationEdgeBake> _edges = new List<MapGraphNavigationEdgeBake>();
        [NonSerialized] private IReadOnlyList<MapGraphNavigationEdgeBake> _edgeView;
        public string SceneFingerprint => _sceneFingerprint ?? string.Empty;
        public string NavigationFingerprint => _navigationFingerprint ?? string.Empty;
        public long Revision => _revision;
        public MapGraphNavigationProfileData Profile => _profile;
        public IReadOnlyList<MapGraphNavigationEdgeBake> Edges => _edgeView ??= _edges.AsReadOnly();
        public MapGraphNavigationBakeData() { }
        public MapGraphNavigationBakeData(string sceneFingerprint, string navigationFingerprint, long revision,
            MapGraphNavigationProfileData profile, IEnumerable<MapGraphNavigationEdgeBake> edges)
        {
            _sceneFingerprint = sceneFingerprint; _navigationFingerprint = navigationFingerprint;
            _revision = revision; _profile = profile;
            if (edges != null) _edges = new List<MapGraphNavigationEdgeBake>(edges);
        }
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => _edgeView = null;
    }
}

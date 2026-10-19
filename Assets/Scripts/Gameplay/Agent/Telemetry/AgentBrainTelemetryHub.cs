#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.Agent.Telemetry
{
    /// <summary>
    /// 大脑遥测的入口：按需创建，跨场景保留。每帧在所有游戏脚本之后同步已注册的智能体，为新智能体挂上记录器、
    /// 为消失的智能体摘除记录器，并让记录器读取本帧结果。场景载入完成和智能体注册时也立刻同步一次，
    /// 赶在决策组件第一次 Update 评分之前挂上，开局那一轮评分才记得到。只在编辑器和开发构建中存在。
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AgentBrainTelemetryHub : MonoBehaviour
    {
        private static AgentBrainTelemetryHub _instance;

        private readonly List<AgentBrainRecorder> _recorders = new List<AgentBrainRecorder>();
        private System.Action<AgentRuntimeHandle> _onRegistered;
        private AgentRuntimeRegistry _registry;

        public static AgentBrainTelemetryHub Instance => _instance;

        public IReadOnlyList<AgentBrainRecorder> Recorders => _recorders;

        public static AgentBrainTelemetryHub Ensure()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("[AgentBrainTelemetry]");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AgentBrainTelemetryHub>();
            _instance.SyncRecorders();
            return _instance;
        }

        /// <summary>按智能体取记录器；没有时返回空</summary>
        public AgentBrainRecorder Find(AgentPawnRoot pawn)
        {
            for (int index = 0; index < _recorders.Count; index++)
                if (_recorders[index].Pawn == pawn) return _recorders[index];
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private void Awake()
        {
            _onRegistered = OnAgentRegistered;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 场景物体的 Awake、OnEnable（智能体注册）之后、第一次 Update 之前
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SyncRecorders();

        private void OnAgentRegistered(AgentRuntimeHandle handle) => SyncRecorders();

        private void LateUpdate()
        {
            SyncRecorders();
            for (int index = 0; index < _recorders.Count; index++) _recorders[index].LateTick();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_registry != null) _registry.AgentRegistered -= _onRegistered;
            for (int index = 0; index < _recorders.Count; index++) _recorders[index].Detach();
            _recorders.Clear();
            if (_instance == this) _instance = null;
        }

        private void SyncRecorders()
        {
            for (int index = _recorders.Count - 1; index >= 0; index--)
            {
                AgentBrainRecorder recorder = _recorders[index];
                if (recorder.Pawn != null && recorder.Pawn.BrainController != null) continue;
                recorder.Detach();
                _recorders.RemoveAt(index);
            }

            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry != _registry)
            {
                if (_registry != null) _registry.AgentRegistered -= _onRegistered;
                _registry = registry;
                if (registry != null) registry.AgentRegistered += _onRegistered;
            }

            if (registry == null) return;
            IReadOnlyList<AgentRuntimeHandle> handles = registry.RegisteredAgents;
            for (int index = 0; index < handles.Count; index++)
            {
                AgentPawnRoot pawn = handles[index].PawnRoot;
                if (pawn == null || pawn.BrainController == null || Find(pawn) != null) continue;
                var recorder = new AgentBrainRecorder(pawn);
                recorder.Attach();
                _recorders.Add(recorder);
            }
        }
    }
}
#endif

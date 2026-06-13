using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 玩家焦点 Agent 输入入口
    /// 负责把 Tab 切换转换为 Registry 中的焦点 Agent 变化
    /// </summary>
    public sealed class AgentFocusInputController : MonoBehaviour
    {
        private static AgentFocusInputController _activeInstance;

        [SerializeField] private AgentRuntimeRegistry _registry;
        [SerializeField] private KeyCode _nextAgentKey = KeyCode.Tab;

        public static AgentFocusInputController ActiveInstance => _activeInstance;

        public static AgentFocusInputController GetOrCreate()
        {
            if (_activeInstance != null)
                return _activeInstance;

            _activeInstance = Object.FindObjectOfType<AgentFocusInputController>();
            if (_activeInstance != null)
                return _activeInstance;

            GameObject controllerObject = new GameObject("[AgentFocusInputController]");
            _activeInstance = controllerObject.AddComponent<AgentFocusInputController>();
            return _activeInstance;
        }

        private AgentRuntimeRegistry Registry
        {
            get
            {
                if (_registry == null)
                    _registry = AgentRuntimeRegistry.GetOrCreate();

                return _registry;
            }
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                Debug.LogWarning("场景中存在多个 AgentFocusInputController，后创建的实例将被停用。", this);
                enabled = false;
                return;
            }

            _activeInstance = this;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        private void Update()
        {
            if (_nextAgentKey == KeyCode.None || !Input.GetKeyDown(_nextAgentKey))
                return;

            Registry.TryFocusNextAgent();
        }
    }
}

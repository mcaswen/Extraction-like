using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// Agent 命令路由器
    /// 外部系统可以把“对哪个 Agent 做什么”交给这里，避免直接依赖单个 Pawn
    /// </summary>
    public sealed class AgentCommandRouter : MonoBehaviour
    {
        private static AgentCommandRouter _activeInstance;

        [SerializeField] private AgentRuntimeRegistry _registry;

        public static AgentCommandRouter ActiveInstance => _activeInstance;

        public static AgentCommandRouter GetOrCreate()
        {
            if (_activeInstance != null)
                return _activeInstance;

            _activeInstance = UnityEngine.Object.FindObjectOfType<AgentCommandRouter>();
            if (_activeInstance != null)
                return _activeInstance;

            GameObject routerObject = new GameObject("[AgentCommandRouter]");
            _activeInstance = routerObject.AddComponent<AgentCommandRouter>();
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
                Debug.LogWarning("场景中存在多个 AgentCommandRouter，后创建的实例将被停用。", this);
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

        public bool TrySubmitDirective(AgentDirectiveRequest directiveRequest)
        {
            return TrySubmitDirective(directiveRequest.TargetAgentId, directiveRequest);
        }

        public bool TrySubmitDirective(AgentId targetAgentId, AgentDirectiveRequest directiveRequest)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SubmitDirective(directiveRequest.WithTargetAgentId(handle.AgentId));
            return true;
        }

        public bool TrySubmitDirective(string targetAgentId, AgentDirectiveRequest directiveRequest)
        {
            return TrySubmitDirective(AgentId.FromString(targetAgentId), directiveRequest);
        }

        public bool TryApplyDamage(AgentId targetAgentId, DamageRequest damageRequest)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.ApplyDamage(damageRequest);
            return true;
        }

        public bool TryApplyDamage(string targetAgentId, DamageRequest damageRequest)
        {
            return TryApplyDamage(AgentId.FromString(targetAgentId), damageRequest);
        }

        public bool TrySetVisibleEnemy(AgentId targetAgentId, bool hasVisibleEnemy)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetVisibleEnemy(hasVisibleEnemy);
            return true;
        }

        public bool TrySetVisibleEnemy(string targetAgentId, bool hasVisibleEnemy)
        {
            return TrySetVisibleEnemy(AgentId.FromString(targetAgentId), hasVisibleEnemy);
        }

        public bool TrySetHasResourceTarget(AgentId targetAgentId, bool hasResourceTarget)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetHasResourceTarget(hasResourceTarget);
            return true;
        }

        public bool TrySetHasResourceTarget(string targetAgentId, bool hasResourceTarget)
        {
            return TrySetHasResourceTarget(AgentId.FromString(targetAgentId), hasResourceTarget);
        }

        public bool TrySetHasInteractableTarget(AgentId targetAgentId, bool hasInteractableTarget)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetHasInteractableTarget(hasInteractableTarget);
            return true;
        }

        public bool TrySetHasInteractableTarget(string targetAgentId, bool hasInteractableTarget)
        {
            return TrySetHasInteractableTarget(AgentId.FromString(targetAgentId), hasInteractableTarget);
        }

        public bool TrySetShouldExtract(AgentId targetAgentId, bool shouldExtract)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetShouldExtract(shouldExtract);
            return true;
        }

        public bool TrySetShouldExtract(string targetAgentId, bool shouldExtract)
        {
            return TrySetShouldExtract(AgentId.FromString(targetAgentId), shouldExtract);
        }

        public bool TryClearDirective(AgentId targetAgentId)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.ClearDirective();
            return true;
        }

        public bool TryClearDirective(string targetAgentId)
        {
            return TryClearDirective(AgentId.FromString(targetAgentId));
        }

        private bool TryResolveTarget(AgentId targetAgentId, out AgentRuntimeHandle handle)
        {
            if (!targetAgentId.IsEmpty)
                return Registry.TryGetHandle(targetAgentId, out handle);

            return Registry.TryGetPrimaryHandle(out handle);
        }
    }
}

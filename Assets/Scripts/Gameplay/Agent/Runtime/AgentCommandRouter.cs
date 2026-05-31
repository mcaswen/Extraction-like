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

        /// <summary>
        /// 当前场景中的 Agent 命令路由器实例
        /// </summary>
        public static AgentCommandRouter ActiveInstance => _activeInstance;

        /// <summary>
        /// 获取或创建 Agent 命令路由器
        /// 外部系统可以通过该入口投递命令而不直接查找 Pawn
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 提交一个已包含目标 AgentId 的指令
        /// 目标为空时会路由到默认 Agent
        /// </summary>
        /// <param name="directiveRequest"></param>
        /// <returns></returns>
        public bool TrySubmitDirective(AgentDirectiveRequest directiveRequest)
        {
            return TrySubmitDirective(directiveRequest.TargetAgentId, directiveRequest);
        }

        /// <summary>
        /// 向指定 AgentId 提交指令
        /// 路由成功后会把实际 AgentId 写回请求体
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="directiveRequest"></param>
        /// <returns></returns>
        public bool TrySubmitDirective(AgentId targetAgentId, AgentDirectiveRequest directiveRequest)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SubmitDirective(directiveRequest.WithTargetAgentId(handle.AgentId));
            return true;
        }

        /// <summary>
        /// 向指定字符串 AgentId 提交指令
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="directiveRequest"></param>
        /// <returns></returns>
        public bool TrySubmitDirective(string targetAgentId, AgentDirectiveRequest directiveRequest)
        {
            return TrySubmitDirective(AgentId.FromString(targetAgentId), directiveRequest);
        }

        /// <summary>
        /// 向指定 Agent 应用伤害请求
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="damageRequest"></param>
        /// <returns></returns>
        public bool TryApplyDamage(AgentId targetAgentId, DamageRequest damageRequest)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.ApplyDamage(damageRequest);
            return true;
        }

        /// <summary>
        /// 向指定字符串 AgentId 应用伤害请求
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="damageRequest"></param>
        /// <returns></returns>
        public bool TryApplyDamage(string targetAgentId, DamageRequest damageRequest)
        {
            return TryApplyDamage(AgentId.FromString(targetAgentId), damageRequest);
        }

        /// <summary>
        /// 设置指定 Agent 是否看见敌人
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasVisibleEnemy"></param>
        /// <returns></returns>
        public bool TrySetVisibleEnemy(AgentId targetAgentId, bool hasVisibleEnemy)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetVisibleEnemy(hasVisibleEnemy);
            return true;
        }

        /// <summary>
        /// 设置指定字符串 AgentId 是否看见敌人
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasVisibleEnemy"></param>
        /// <returns></returns>
        public bool TrySetVisibleEnemy(string targetAgentId, bool hasVisibleEnemy)
        {
            return TrySetVisibleEnemy(AgentId.FromString(targetAgentId), hasVisibleEnemy);
        }

        /// <summary>
        /// 设置指定 Agent 是否存在资源目标
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasResourceTarget"></param>
        /// <returns></returns>
        public bool TrySetHasResourceTarget(AgentId targetAgentId, bool hasResourceTarget)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetHasResourceTarget(hasResourceTarget);
            return true;
        }

        /// <summary>
        /// 设置指定字符串 AgentId 是否存在资源目标
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasResourceTarget"></param>
        /// <returns></returns>
        public bool TrySetHasResourceTarget(string targetAgentId, bool hasResourceTarget)
        {
            return TrySetHasResourceTarget(AgentId.FromString(targetAgentId), hasResourceTarget);
        }

        /// <summary>
        /// 设置指定 Agent 是否存在可交互目标
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasInteractableTarget"></param>
        /// <returns></returns>
        public bool TrySetHasInteractableTarget(AgentId targetAgentId, bool hasInteractableTarget)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetHasInteractableTarget(hasInteractableTarget);
            return true;
        }

        /// <summary>
        /// 设置指定字符串 AgentId 是否存在可交互目标
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="hasInteractableTarget"></param>
        /// <returns></returns>
        public bool TrySetHasInteractableTarget(string targetAgentId, bool hasInteractableTarget)
        {
            return TrySetHasInteractableTarget(AgentId.FromString(targetAgentId), hasInteractableTarget);
        }

        /// <summary>
        /// 设置指定 Agent 是否应该撤离
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="shouldExtract"></param>
        /// <returns></returns>
        public bool TrySetShouldExtract(AgentId targetAgentId, bool shouldExtract)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.SetShouldExtract(shouldExtract);
            return true;
        }

        /// <summary>
        /// 设置指定字符串 AgentId 是否应该撤离
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <param name="shouldExtract"></param>
        /// <returns></returns>
        public bool TrySetShouldExtract(string targetAgentId, bool shouldExtract)
        {
            return TrySetShouldExtract(AgentId.FromString(targetAgentId), shouldExtract);
        }

        /// <summary>
        /// 清除指定 Agent 当前待处理指令
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <returns></returns>
        public bool TryClearDirective(AgentId targetAgentId)
        {
            AgentRuntimeHandle handle;
            if (!TryResolveTarget(targetAgentId, out handle))
                return false;

            handle.CommandReceiver.ClearDirective();
            return true;
        }

        /// <summary>
        /// 清除指定字符串 AgentId 当前待处理指令
        /// </summary>
        /// <param name="targetAgentId"></param>
        /// <returns></returns>
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

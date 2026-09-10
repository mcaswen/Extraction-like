using Gameplay.Agent.Data;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Navigation;
using UnityEngine;

namespace Gameplay.Agent.Interfaces
{
    /// <summary>
    /// 外部影响Agent用的接口，例如：
    ///     - 受到伤害
    ///     - 视觉感知到敌人
    ///     - Agent发布命令
    /// </summary>
    public interface IAgentCommandReceiver
    {
        /// <summary>
        /// 使Agent受到伤害
        /// </summary>
        /// <param name="damageRequest"></param>
        void ApplyDamage(DamageRequest damageRequest);

        /// <summary>
        /// 设置Agent是否感知到敌人
        /// </summary>
        /// <param name="hasVisibleEnemy"></param>
        void SetVisibleEnemy(bool hasVisibleEnemy);

        /// <summary>
        /// 设置Agent当前是否有可侦查的敌人来源点
        /// </summary>
        /// <param name="hasEnemySourceTarget"></param>
        void SetHasEnemySourceTarget(bool hasEnemySourceTarget);

        /// <summary>
        /// 设置Agent当前是否有可搜索的资源点
        /// </summary>
        /// <param name="hasResourceTarget"></param>
        void SetHasResourceTarget(bool hasResourceTarget);

        /// <summary>
        /// 设置Agent是否应该撤离
        /// </summary>
        /// <param name="shouldExtract"></param>
        void SetShouldExtract(bool shouldExtract);

        /// <summary>
        /// 设置Agent是否有可交互的目标
        /// </summary>
        /// <param name="hasInteractableTarget"></param>
        void SetHasInteractableTarget(bool hasInteractableTarget);

        /// <summary>
        /// 提交一个Agent干预请求
        /// 当前阶段只留接口，不在此处固化具体业务逻辑
        /// </summary>
        /// <param name="directiveRequest"></param>
        void SubmitDirective(AgentDirectiveRequest directiveRequest);
        AgentDirectiveResult TrySubmitDirective(AgentDirectiveRequest directiveRequest);
        bool FinishDirective(string commandId, AgentDirectiveFailure failure = AgentDirectiveFailure.None);
        AgentNavigationResult MoveDirective(Vector3 destination, float stoppingDistance, float speed);
        void StopDirectiveMovement();

        /// <summary>
        /// 清除当前待处理的Agent干预请求
        /// </summary>
        void ClearDirective();
    }
}

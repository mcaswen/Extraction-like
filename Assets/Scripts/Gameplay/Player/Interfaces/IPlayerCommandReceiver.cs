
using Gameplay.Player.Data;

namespace Gameplay.Player.Interfaces
{
    /// <summary>
    /// 外部影响主角用的接口，例如：
    ///     - 受到伤害
    ///     - 视觉感知到敌人
    ///     - 玩家发布命令
    /// </summary>
    public interface IPlayerCommandReceiver
    {
        /// <summary>
        /// 使主角受到伤害
        /// </summary>
        /// <param name="damageRequest"></param>
        void ApplyDamage(DamageRequest damageRequest);

        /// <summary>
        /// 设置主角是否感知到敌人
        /// </summary>
        /// <param name="hasVisibleEnemy"></param>
        void SetVisibleEnemy(bool hasVisibleEnemy);

        /// <summary>
        /// 设置主角当前是否有可搜索的资源点
        /// </summary>
        /// <param name="hasResourceTarget"></param>
        void SetHasResourceTarget(bool hasResourceTarget);


        /// <summary>
        /// 设置主角是否应该撤离
        /// </summary>
        /// <param name="shouldExtract"></param>
        void SetShouldExtract(bool shouldExtract);

        /// <summary>
        /// 设置主角是否有可交互的目标
        /// </summary>
        /// <param name="hasInteractableTarget"></param>
        void SetHasInteractableTarget(bool hasInteractableTarget);

        /// <summary>
        /// 提交一个玩家干预请求
        /// 当前阶段只留接口，不在此处固化具体业务逻辑
        /// </summary>
        /// <param name="directiveRequest"></param>
        void SubmitDirective(PlayerDirectiveRequest directiveRequest);

        /// <summary>
        /// 清除当前待处理的玩家干预请求
        /// </summary>
        void ClearDirective();
    }
}

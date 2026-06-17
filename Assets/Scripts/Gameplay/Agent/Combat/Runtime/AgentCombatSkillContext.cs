using UnityEngine;

namespace Gameplay.Agent.Combat
{
    /// <summary>
    /// 技能释放时的运行时上下文
    /// 汇总施法者、风格配置、战斗属性和目标筛选层
    /// </summary>
    public readonly struct AgentCombatSkillContext
    {
        /// <summary>
        /// 创建技能释放上下文
        /// </summary>
        /// <param name="casterTransform"></param>
        /// <param name="styleConfig"></param>
        /// <param name="stats"></param>
        /// <param name="enemyLayerMask"></param>
        /// <param name="actionLockSeconds"></param>
        /// <param name="skillModifiers"></param>
        public AgentCombatSkillContext(
            Transform casterTransform,
            AgentCombatStyleConfig styleConfig,
            AgentCombatRuntimeStats stats,
            LayerMask enemyLayerMask,
            float actionLockSeconds,
            AgentCombatSkillModifiers skillModifiers = default,
            bool playPrototypeSkillVfx = false,
            bool suppressConfiguredSkillVfx = false,
            float prototypeSkillVfxRangeScale = 1f)
        {
            CasterTransform = casterTransform;
            StyleConfig = styleConfig;
            Stats = stats;
            EnemyLayerMask = enemyLayerMask;
            ActionLockSeconds = Mathf.Max(0.05f, actionLockSeconds);
            SkillModifiers = skillModifiers;
            PlayPrototypeSkillVfx = playPrototypeSkillVfx;
            SuppressConfiguredSkillVfx = suppressConfiguredSkillVfx;
            PrototypeSkillVfxRangeScale = Mathf.Max(0.01f, prototypeSkillVfxRangeScale);
        }

        /// <summary>
        /// 技能施法者 Transform
        /// </summary>
        public Transform CasterTransform { get; }

        /// <summary>
        /// 技能来源对象
        /// </summary>
        public GameObject SourceObject => CasterTransform != null ? CasterTransform.gameObject : null;

        /// <summary>
        /// 施法者当前战斗风格配置
        /// </summary>
        public AgentCombatStyleConfig StyleConfig { get; }

        /// <summary>
        /// 施法者当前战斗属性
        /// </summary>
        public AgentCombatRuntimeStats Stats { get; }

        /// <summary>
        /// 技能查询敌人时使用的 LayerMask
        /// </summary>
        public LayerMask EnemyLayerMask { get; }

        /// <summary>
        /// 技能成功释放后建议锁定动作的时间
        /// </summary>
        public float ActionLockSeconds { get; }

        /// <summary>
        /// 本次技能释放应用的天赋修正
        /// </summary>
        public AgentCombatSkillModifiers SkillModifiers { get; }

        /// <summary>
        /// 是否额外播放旧提交里的主角技能 prototype 特效，用于测试对比。
        /// </summary>
        public bool PlayPrototypeSkillVfx { get; }

        /// <summary>
        /// prototype 特效播放成功时是否跳过配置里的正式 prefab/指示器。
        /// </summary>
        public bool SuppressConfiguredSkillVfx { get; }

        /// <summary>
        /// Prototype 技能特效的视觉范围缩放，只影响测试特效，不改变技能判定。
        /// </summary>
        public float PrototypeSkillVfxRangeScale { get; }

        /// <summary>
        /// 施法者当前位置
        /// </summary>
        public Vector3 Position => CasterTransform != null ? CasterTransform.position : Vector3.zero;

        /// <summary>
        /// 施法者水平朝向
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                if (CasterTransform == null)
                    return Vector3.forward;

                // 技能朝向只使用水平分量，避免模型俯仰影响地面技能方向
                Vector3 forward = CasterTransform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }
    }
}

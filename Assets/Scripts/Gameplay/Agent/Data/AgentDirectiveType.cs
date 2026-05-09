using System;

namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent干预请求类型
    /// DirectiveType 表示“要做什么”，具体目标由 AgentTargetRef 表示
    /// </summary>
    public enum AgentDirectiveType
    {
        None = 0,
        Search = 1, // 搜索资源目标
        Engage = 2, // 处理敌人目标
        MoveTo = 3, // 移动到位置或区域
        RequestBuff = 4, // 请求增益
        Extract = 5, // 撤离

        [Obsolete("Use Search with AgentTargetRef.Kind = Resource.")]
        ResourceTarget = Search,

        [Obsolete("Use Engage with AgentTargetRef.Kind = Enemy.")]
        EnemyTarget = Engage,

        [Obsolete("Use MoveTo with an abstract point target.")]
        AreaTarget = MoveTo,

        [Obsolete("Use RequestBuff.")]
        BuffRequest = RequestBuff
    }
}

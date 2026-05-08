
namespace Gameplay.Agent.Data
{
    /// <summary>
    /// Agent干预请求的目标类型
    /// 当前阶段只定义请求接口字段，不在此处绑定具体业务规则
    /// </summary>
    public enum AgentDirectiveType
    {
        None = 0,
        ResourceTarget = 1, // 资源点目标
        EnemyTarget = 2, // 敌人目标
        AreaTarget = 3, // 区域目标
        BuffRequest = 4 // 增益请求
    }
}
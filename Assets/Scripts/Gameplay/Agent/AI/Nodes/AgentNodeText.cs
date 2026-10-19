using Gameplay.Agent.Data;

namespace Gameplay.Agent.AI.Nodes
{
    /// <summary>
    /// 节点展示名用到的中文词
    /// </summary>
    public static class AgentNodeText
    {
        public static string Directive(AgentDirectiveType directiveType)
        {
            switch (directiveType)
            {
                case AgentDirectiveType.MoveTo: return "移动";
                case AgentDirectiveType.Search: return "搜索";
                case AgentDirectiveType.Engage: return "交战";
                case AgentDirectiveType.Extract: return "撤离";
                default: return directiveType.ToString();
            }
        }

        public static string Target(AgentTargetKind targetKind)
        {
            switch (targetKind)
            {
                case AgentTargetKind.Location: return "路线锚点";
                case AgentTargetKind.EnemySource: return "敌人来源";
                case AgentTargetKind.Resource: return "资源";
                case AgentTargetKind.Extraction: return "撤离点";
                case AgentTargetKind.Enemy: return "敌人";
                default: return targetKind.ToString();
            }
        }
    }
}

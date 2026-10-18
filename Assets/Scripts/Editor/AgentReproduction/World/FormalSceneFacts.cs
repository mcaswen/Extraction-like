namespace AgentReproduction.World
{
    /// <summary>
    /// Scenezl_Final 1 的作者内容计数，场景变更时只在这里同步
    /// 2026-09-13 6b318e61 移除员工食堂 Env 下的一个敌人来源群（2 个出生点），地图随后按 27 个节点重新发布
    /// </summary>
    public static class FormalSceneFacts
    {
        public const string ScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
        public const int MapNodes = 27;
        public const int EnemySourceNodes = 13;
        public const int NodePairs = MapNodes * (MapNodes - 1) / 2;
        public const int DirectedNodePairs = MapNodes * (MapNodes - 1);
        public const int Zones = 7;
        public const int EnemySpawns = 28;
    }
}

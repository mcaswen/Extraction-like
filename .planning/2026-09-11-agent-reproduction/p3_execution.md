# P3 小规划：成员候选、风险输入与冷却连续性

## 目标与边界

修复 F4、F7、R4。Discovery 新选/保持按同一个可达成员比较；Decision 使用 Pawn 的实际防御、感知范围内全部可见且唯一的敌人，并在没有合适常规候选时选择远处可达撤离点。属性和图腾刷新保留技能冷却，技能配置集合变化时保留仍存在技能的状态；普攻节点重新进入不能绕过攻击锁。

保留现有评分公式、技能数值和宏状态，不迁移程序集，不增加第二套黑板/任务权威。

## 文件职责与依赖

- Extend `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidate.cs`：成员位置、导航落点、距离和是否可执行的候选快照。
- Extend `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs`：集中收集敌人、资源、敌源、撤离成员事实；复用 Perception/Navigation 和 Authoring 成员接口；不决定分数或提交任务。敌人风险集合保留可见但不可接近的敌人，执行候选另行过滤。
- Extend `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs`：复用同一候选集合完成新选/保持；撤离取可达成员，移除重复的群中心距离算法。
- Extend `Assets/Scripts/Gameplay/Agent/Decision/AgentTargetDecisionController.cs`：共同候选转换为评分输入；真实 Defense 与全体去重风险敌人；普通评分无结果时才对范围外撤离兜底。保留可观察黑板事实。
- Reuse `Assets/Scripts/Gameplay/Agent/Decision/AgentTargetDecisionService.cs`：保留既有评分和风险阈值；撤离兜底有明确理由并保留风险观测，不因所有探索候选拒绝而原地永远空闲。
- Extend `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatController.cs`：属性刷新仅更新上下文和发射器；按配置身份复用存续运行时技能。技能集合变化的组装责任仍在此文件。
- Extend `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatSkillBase.cs`（仅确需跨配置实例迁移时）：同 SkillId/类型的受控冷却迁移；新技能不继承不相干技能状态。
- Reuse `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs` 的控制器攻击锁，不在节点入口重置。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/TargetDecisionTests.cs`：资源中心偏移、风险成员去重/遮挡/范围、真实防御、远处可达撤离和不可达近点对照。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/CombatCooldownTests.cs`：真实技能施放后改变属性/图腾/集合，验证冷却和再次施放；进入/离开战斗的普攻节奏。
- Extend `Assets/Scripts/Editor/AgentReproduction/World/TargetFactory.cs` 与 `tools/agent-repro/cases.json`：只添加构造入口和预期用例清单。

## 闭环

1. 先运行旧逻辑并保留失败；所有断言针对实际运行状态、实际伤害或公开刷新入口。
2. 完成共用候选，验证新选/保持不再翻转，风险计数和 Defense 正确。
3. 完成冷却保留，以单技能正反对照排除“其他技能就绪”的假阳性。
4. 独立回归，通过后写架构审查并提交，再进入 P4 全量重复及图形证据。

## 实际结果

待执行。

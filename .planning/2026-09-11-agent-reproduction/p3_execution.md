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

- P2 提交 `26512c9`。修复前 `20260911-015953-841` 复现 F4，但 Decision 夹具仍继承正式 prefab 的关闭开关；显式打开并检查 IsDecisionModuleActive 后，`20260911-020125-718` 正式基线 4/4 失败：F4 由 Search 翻到 Engage，风险 1 而非 2，Defense 0 而非 123，远处撤离缺失。
- `20260911-020035-003` 冷却基线：属性、图腾、等价配置三种刷新均可立即重复施放；P1 已修正的普攻重入控制组通过。
- 实现归属：CandidateCollector 枚举可达撤离成员/敌源出生点，并保留可见但不可接近敌人作为风险事实；Discovery/Decision 只决定采用哪个快照。资源任务继续指向 Cluster 以保留逐成员搜索流程，评分位置使用实际成员；敌源移动指向实际出生点，TargetId 保留源群身份。
- Decision 复用既有评分服务与风险阈值，范围外撤离只在范围内候选均不被接受时评分；没有改写风险公式或绕过阈值。
- 冷却复用按配置对象优先；同运行时类型/SkillId 的配置替换迁移截止时间，新技能不继承其他技能状态。
- 回归 `20260911-020715-242` Decision 4/4 通过；`20260911-020825-401` Cooldown 5/5 通过，新增同一风格资产内重排/新增技能对照。资源观察窗口内扫描间隔 0.2 秒、持续 2 秒，低于 3 秒无进展期限。
- 夹具修正：正式 Pawn 发现范围为 200 米，原来 100 米敌人并非范围外；远目标改为 300 米并显式断言范围前提。`20260911-020517-017` 的风险 3 是正确包含该敌人，不能算生产缺陷。
- 架构审查通过：移除 Discovery/Decision 中重复的群中心选取循环；属性上下文与技能实例生命周期分离；评分服务未引入场景查询。真实库存装备与全量重复在 P4 验证。

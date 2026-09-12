# 同群接战、玩家任务中断恢复

日期：2026-09-12。需求：用户明确同群成员响应交战，玩家任务在反击后继续。沿用已授权的小规划 → 实现 → 自动测试/审查 → 提交闭环。此次是现有 Enemy 和 Agent.Commands 内的修复，不建立新系统、改变模块依赖方向或重写行为树。

## 事实、规则和范围

- `EnemyHealthController.NotifyDamageReaction` 只通知本体的 `IEnemyDirectDamageReceiver`，直接伤害不走怀疑总线。Registry 已有敌人到 ActiveEnemyCluster 的查询，该群已支持场景成员和运行时出生的成员。
- `AgentDirectiveLifecycleController` 仅挂起 Extract，玩家 Search/Engage 被伤害替换时发布 Cancelled。因此自动发现恢复是任务被丢弃的后果。
- 同群通知由带有效攻击者的直接受击触发，包含护盾承伤、致死一击；无来源、零伤害不新增群体警报。通知不造成额外伤害，不跨群，不递归扩散。
- 空闲成员响应攻击者；已经和有效目标交战的成员继续当前战斗，重复通知不重置出招。移动敌人复用有限追击，固定炮台保持原站桩、射程和视线约束。
- 手动 Search/Engage/Extract（以及既有手动移动）保存一份被挂起任务，保留原 CommandId/TargetRef。原自主 Extract 的恢复行为不变，自主 Search/Engage 不新增挂起。有效手动新令、取消、死亡清空旧挂起；重复受击不覆盖它。
- 恢复时继续正式目标/导航校验，已失效目标有明确终态。沿用已经确认的敌人群指令绑定具体成员规则，此次不扩大为清群队列。

## 文件归属、复用和依赖

| 选择 | 具体文件 | 职责和边界 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveLifecycleController.cs` | 将单份挂起状态泛化为任务，仍是唯一的保存、覆盖、恢复所有者；兼容原 SuspendedExtraction 只读视图 |
| Reuse | `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveValidationService.cs`、`Assets/Scripts/Gameplay/Agent/Runtime/AgentManualDirectiveLock.cs` | 恢复校验、手动/反击身份，不复制规则 |
| Create | `Assets/Scripts/Gameplay/Enemy/IEnemyCombatAlertReceiver.cs` + meta | 群体接战接收契约，只携带攻击者，不伪造直接受伤事件 |
| Create | `Assets/Scripts/Gameplay/Enemy/EnemyClusterCombatAlert.cs` + meta | 一次受击对应的同群通知，复用成员查询和列表池；独立存在是为了不让血量类拥有群体策略/遍历 |
| Extend | `Assets/Scripts/Gameplay/Enemy/EnemyHealthController.cs` | 实际正伤害通知群体服务，保留本地受伤和无来源怀疑语义 |
| Extend | `Assets/Scripts/Gameplay/Enemy/EnemyBehaviorController.cs`、`RangedEnemyBehaviorController.cs`、`ModernStranderBehaviorController.cs`、`AncientStranderBehaviorController.cs`、`TidalAberrationBehaviorController.cs`、`HunterBossBehaviorController.cs`、`AnchorSentinelBehaviorController.cs`（均在同目录） | 实现接战契约，各自管理行为状态、接收器身份、现有攻击时序；不合并异种 AI，也不把追击策略放进 Targets |
| Reuse | `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRegistry.cs`、`Assets/Scripts/Gameplay/Targets/Authoring/ActiveEnemyClusterAuthoring.cs`、`Assets/Scripts/Gameplay/Enemy/EnemyCombatTargetBinding.cs` | 群归属、去重存活成员、原子目标身份 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/ManualDirectiveResumeTests.cs`、`EnemyClusterCombatAlertTests.cs` + meta | 独立复现两个问题，使用真实 Prefab、正式 Dispatcher/伤害入口和已有隔离世界；探针不接入正式玩法 |
| Extend | `tools/agent-repro/cases.json` | 精确登记新参数用例 |
| Extend | `Assets/Docs/GameplayAgentFrameworkDesign.md`、`Assets/Docs/EnemySystemOverview.md` | 同步新恢复/同群接战规则 |

依赖保持 Enemy → Targets 的已有 Registry 查询方向，Agent.Commands → 现有校验/导航，Editor Tests → Gameplay。接战接口是 Enemy 模块内的补充契约，不改变既有伤害接口。群体通知是事件触发，无新 Update/全场逐帧扫描；一次成本为现有群查找和本群存活成员遍历（成员去重目前 O(n²)，群小且仅受击时执行），接收器列表复用。

## 小阶段和验收

### P1 玩家任务恢复

先跑正式发令 → 真实移动 → 伤害 → 反击目标死亡 → 原任务恢复、继续移动的红灯；Search、Engage、Extract 在 1×/4×覆盖。补连续受击、两名角色、反击期间新令/拒绝/取消/死亡、目标失效、过期完成回调，覆盖反击失败也能恢复。修复后回归既有生命周期、撤离中断和反击进展用例。

### P2 同群接战

正式伤害打中一个成员，另一个实际行为组件必须响应并接近/合法攻击；群外、重复成员、禁用/死亡成员、空/无来源、致死一击、护盾、已有有效战斗对照。七种真实 Prefab 检查状态和目标接收器一致，重点验证移动敌人墙后知情但无法隔墙造成伤害、固定炮台保留射程约束。测量固定成员数的事件耗时和 GC，不能拿无图形测试 FPS 替代正式场景帧率。

### P3 集成与审查

定向运行相邻测试和一局 Scenezl_Final 1 的 4×手动指令场景，观察自然战斗/恢复/终态；正常死亡允许，未触发分支明确记录覆盖不足。必要时针对失败调整本规划，不全量重跑旧矩阵。记录 XML、日志、实际数量，写 `architecture_review.md` 和结果，按自然中文正文提交。

## 实施结果

待运行，不能把源码定位当作程序化复现通过。

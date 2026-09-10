# P4 小规划：综合回归与渲染证据

## 目标与文件边界

沿用 P0–P3 架构，补组合边界及正式 HUD 图形证据，然后同种子全量重复三次。失败不得通过忽略异常、减少预期数量或反复重跑至通过掩盖。

- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/CommandFeedbackTests.cs`、Create `CommandFeedbackGraphicsTests.cs`：正式反馈预制体成功/失败/消退截图；显示暂停、中文字体、顶部位置与不挡交互断言。图形组只在隔离副本渲染，Agent 读取 PNG 检查。
- Extend `Assets/Scripts/Editor/AgentReproduction/Reporting/CaseArtifactWriter.cs`：图片编码和案例输出路径仍归 Reporting，不由业务层落盘。
- Extend `tools/agent-repro/Invoke-AgentRepro.ps1`、`cases.json`：图形组自动保留图形设备，其他组继续无图形运行；预期用例数与实际 NUnit 结果核对。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/CombatCooldownTests.cs`：真实库存图腾装备触发 Pawn 自动刷新，检验技能冷却；继续保留直接配置变更的隔离用例。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/CombinedRegressionTests.cs`：多 Agent 撤离/受击隔离、动态路径断裂、不可执行敌人不挡其他候选、无效/禁用/完成目标、枪口与缺失攻击配置失败等组合。
- 如组合用例暴露问题，仅在已批准的生产责任文件内修正，先在本节记录具体归属，再实现/复跑。比如缺失发射器/弹体配置由 `AgentCombatShooter.cs` 返回明确原因、`EngageEnemyActionNode.cs` 报任务失败，`AgentDirectiveResult.cs`/`AgentCommandFeedbackText.cs` 表达对应原因，不放到测试适配层绕过。

## 执行顺序与验收

1. 小组运行新增用例和正反对照；定位失败，必要时调整夹具或生产实现，明确区别两者。
2. 渲染正式 Canvas 的成功、失败和消退画面，检查中文内容/位置/透明度，并记录图形配置与截图路径。
3. `-Suite All -Repeat 3`，每组独立进程；图形组自动启用渲染。所有计划行为映射到实际测试名，不宣称穷举所有关卡和帧率。
4. 汇总重复结果、源文件哈希/隔离存档证据与剩余局限，完成阶段审查后提交。

## 实际结果

- P3 提交 `e86513b`。
- `20260911-021208-249`：实际撤离销毁并换人造成伤害、多 Agent 挂起任务隔离、动态断路通过；缺失弹体配置导致任务永久保留，已修正为 AttackUnavailable。RequireComponent 禁止删除 Shooter，因此缺失组件用例改为禁用组件，而非忽略引擎 Error。
- `20260911-021407-121` 组合 5/5 通过。新的射击失败枚举位于 `AgentCombatShooter.cs`，业务任务失败在 Engage 节点桥接，中文原因位于既有反馈映射。
- `20260911-021555-844` 图形用例通过；Agent 已读取 success/failure/fading/hidden 四张 1920×1080 PNG，顶部居中、中文字形完整、成功浅绿/失败浅红、暂停下消退均可见。使用正式反馈 prefab 和真实 Agent prefab；截图摄像机在隔离场景中将同一 Canvas 切换为 ScreenSpaceCamera 以导出，正式资产仍为 Overlay。
- 扩展巡逻矩阵时，敌人实际移动使固定遮挡布局不再成立；夹具关闭 NavMesh 的位置/旋转写回，保留真实控制器扫描/转状态，增加姿态证据。
- 正式 Boss 在换人后的近战动作又触发 ParticleSystem duration Assert。Extend `Assets/Art/VFX/TracerAnchorVortexVfx.cs` 的 `CreateParticles`，同样先 StopEmittingAndClear 再配置，责任仍在 VFX 创建，不忽略运行时异常。
- F6 扩展确实发现 Tidal 的 `TryFireOpeningWaterJet` 绕过可见检查直接进入 RangedAttack 并锁定被墙挡住的 A；在该控制器的开场技能入口补 CanSeePlayer。巡逻测试将初始朝向设为可见 B，并等待实际接战事件，避免把自然巡逻扫视转头后的视角当作固定前提。
- Sentinel 高差失败的轨迹为 EyeOrigin 与平台地板重合，夹具把没有 NavMesh 抬升的居中角色根放在了地面；正式 prefab 的根 Y=1。EnemyFactory 保留这个正式垂直偏移，不改资产或放宽墙体规则。原 LockDuration=5 秒，测试同时按原锁定时长留足等待预算。
- `20260911-022502-528` 远程 10/10 通过，Sentinel 保留正式 5 秒锁定后确实造成伤害。
- `20260911-022751-338` 组合扩展发现禁用撤离点和已完成撤离群仍可下达成功：`AgentDirectiveValidationService.cs` 补组件启用检查；`AgentTargetCommandDispatcher.cs` 在群被转换成具体成员前检查群完成/启用状态，避免丢失原群有效性。职责分别为指令目标校验和输入群解析前置检查。
- Tidal 的开场排队入口也会提前切换战斗状态，因此 `TidalAberrationBehaviorController.cs` 的 Queue 和 TryFire 两个阶段都检查可见性。
- 实际敌人攻击触发玩家反击技能后，`Assets/Art/VFX/PlayerElementalSkillVfx.cs` 同样暴露运行中修改 duration 的 Assert。Extend 既有 `PrepareParticleSystem`：先停止粒子，再统一初始化；不改变技能伤害或测试异常规则。
- `20260911-024110-042` EnemyTargets 16/16 通过，Tidal 两个开场入口及真实玩家技能粒子修复获得验证。
- `20260911-023326-709` 船锚无遮挡对照失败：夹具先创建 Rigidbody 后移动 Transform，没有同步物理位置；生产生成链在添加刚体前已设置位置。夹具改为同一顺序、显式 SyncTransforms，并记录首个扫掠命中前提；不修改生产碰撞逻辑。`20260911-024144-957` RangedSpatial 12/12 通过。
- 最新定向组：Navigation 8/8、Cooldown 6/6（实际图腾装备）、Combined 10/10、Perception 5/5，分别见 `022950-538`、`023225-284`、`023251-913`、`023415-640`（统一前缀 `20260911-`）。

## Review 后追加小规划

对照大规划 4.3 与 6 节发现两个遗漏的边界，现有 71 例继续完成并保留结果，追加以下构造后再验收最终版本：

- Create `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetFailureMemory.cs`：有界、按 Agent 身份和失败目标记录的短期排除缓存。订阅既有指令结果事件，仅执行失败（NoProgress/Unreachable/NavigationNotReady/LostSight/AttackUnavailable）进入缓存；位置变化或 3 秒游戏时间到期解除。最多 64 条，禁用时退订/清空。缓存独立于收集算法，Commands 不反向依赖 Targeting。
- Extend `AgentTargetCandidateCollector.cs`：组合上述缓存；敌人仍进入风险集合但 CanExecute=false，世界目标暂时不进入执行候选。`AgentTargetDiscoveryController.cs`、`AgentTargetDecisionController.cs` 仅在启用/禁用时管理订阅，手动指令入口不受自动候选缓存阻挡。目标恢复可在下次合法扫描重新选择。
- Extend `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatController.cs`：配置列表按 SkillId 保留首次出现的定义，重复项不创建独立技能/冷却；不将状态搬进 SO。刷新和配置重排仍保留原运行状态。
- Extend `TargetDecisionTests.cs`：真实自动任务因静止无进展失败后转向 B；短期不重选 A，其他 Agent 不受牵连，位移后 A 可重新验证。Extend `CombatCooldownTests.cs`：相同/等价重复 ID 在首次施法、属性刷新及重排后不提前施法。
- Extend `DirectiveLifecycleTests.cs`：挂起撤离目标在反击中失效，实际反击结束后产生失败并清空活动/挂起状态。
- Extend `tools/agent-repro/AgentRepro.Report.psm1`：全通过 XML 配上非零进程退出不能算通过；使用程序构造 XML/退出码/缺失数据做报告层故障验证，不改变 Unity 行为判定。
- Create `tools/agent-repro/Test-AgentReproReport.ps1`：保存上述报告层构造回归（正常、异常退出、断言失败、Diagnose、缺失、超时、清理失败），独立于 Unity 进程启动，便于以后修改报告时快速复验。

先跑新增失败基线，再实现对应文件并复跑；对最终生产代码重新执行三轮全量。此处补齐已批准规则，不扩大玩法范围或改变依赖方向。

### 追加结果

- `20260911-024654-362`：原有 71 例三轮 213/213 通过，42 个组进程全部正常，源文件哈希一致。这批先作为 Review 增补前的完整证据保留。
- `20260911-030155-373`：真实 NoProgress 终态后，自动任务未转向 B，复现失败目标立即重选。失败后的 teardown 又暴露测试监听器访问已销毁群，已补对象活性守卫；不改变业务断言或忽略日志。
- `20260911-030230-532`：重复同一技能对象、等价副本两个变体都在原冷却到期前再次成功施法。`030300-027` Lifecycle 4/4 通过，挂起撤离点失效后能清理并报告失败，无需额外生产修复。
- `AgentTargetFailureMemory` 与按 SkillId 去重已按小规划落地；缓存至多 64 条，以 3 秒游戏时间或 Agent/目的地移动超过 0.5m 解除，路径恢复最迟下次期限后重检，不设永久黑名单。
- `ReportProbe-20260911-030411-704`：报告层正常、全绿 XML 后异常退出、断言失败、Diagnose、缺失、超时、清理失败 7/7 通过；异常退出不再被成功 XML 掩盖。
- `20260911-030412-205` Decision 5/5、`20260911-030450-278` Cooldown 8/8 通过。自动失败转向 B、其他 Agent 仍可选 A、手动重选允许、位移后重试，以及重复配置/重排均获得实际执行验证。

## 最终验收与阶段结论

- `20260911-030638-823`：最终 75 例连续三轮，共 **225/225 通过**；42 个组进程，0 失败、0 缺失、0 超时，Regression 退出 0。
- 同一 seed=731、固定物理步 0.02 秒；4,958 个源输入文件 SHA256 运行前后及证据导出时一致。生产代码在这三轮期间未修改。
- 原始证据：`Logs/AgentReproduction/20260911-030638-823/`；仓库内保留 [逐例结果与证据哈希](../../outputs/agent_repro_validation.json)、[验收报告](../../outputs/implementation_validation_report.md)和 [图形证据](../../outputs/feedback/)。
- 架构审查通过：状态/策略/缓存/执行/报告边界清晰，无业务反向依赖 Editor 测试；新增元数据完整且 GUID 无冲突，diff whitespace 检查通过。
- P4 完成，进入 P5 最终文档审查及提交；本阶段提交号由 P5 记录，避免将自引用哈希写入尚未形成的提交。

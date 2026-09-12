# Anomaly Search：目标、执行与交战修复验收

- 日期：2026-09-11。
- 范围：用户确认的 F1–F7、R1–R5；包含生产修复、程序构造、自动 Play Mode、结果定位、图形检查和分阶段提交。
- 环境：Unity `2022.3.62f2c1`、Unity Test Framework `1.1.33`，Windows。通过预定义 Editor 程序集发现测试，并在协程中进入真实 Play Mode；这不是独立 Player 构建测试。
- 当前状态：P0–P5 实现与验收完成；最终 75 例三轮 **225/225 通过**，另有报告层故障构造 7/7 通过。分阶段提交见文末。
- 规划与审查：[大规划](../.planning/2026-09-11-agent-reproduction/task_plan.md)、[生产文件设计](../.planning/2026-09-11-agent-reproduction/repair_design.md)、[阶段架构审查](../.planning/2026-09-11-agent-reproduction/architecture_review.md)。

## 已实现规则及测试归属

| 项目 | 实际行为 | 自动验证入口 |
| --- | --- | --- |
| F1 | 有效敌人伤害挂起原撤离；反击结束恢复原目标和 CommandId；连续伤害只保留一份；新手动指令、取消、死亡清理恢复记录 | `F1ExtractionInterruptTests`、`DirectiveLifecycleTests`、`CombinedRegressionTests.TwoAgentsKeepIndependentSuspendedExtractions` |
| F2 | 死亡/禁用/销毁/离场目标整体失效，Transform、伤害和位移接收器同源；旧攻击状态随目标撤销 | `EnemyTargetBindingTests` 七种正式 prefab；`CombinedRegressionTests.ActualExtractionDestroysAAndRangedEnemyDamagesB` 运行实际 Raid 撤离计时/销毁并观察 B 受伤 |
| F3 | 手动资源允许有效伤害打断；只看到敌人、零伤害和完全被护盾吸收不打断；反击结束不强制恢复旧资源 | `DirectiveLifecycleTests.ManualResourceIgnoresSightButEffectiveDamageInterrupts`、`CombinedRegressionTests.ShieldAbsorptionDoesNotInterruptResourceButDisplacementIsIndependent` |
| F4 | 新选与保持使用同一个可达资源成员的距离，敌人/资源比较不再改用群中心 | `TargetDecisionTests.ResourceSelectionAndRetentionUseSameMemberDistance`，在无进展期限前进行多次实际调度扫描 |
| F5/R1 | 不可达拒绝；动态断路/导航丢失/无进展终止并释放锁；自动失败目标短期排除，手动选择不受此缓存阻挡；0/0.05/0.2 米容差适用于平地/坡面；顶部结果淡入淡出 | `F5UnreachableDirectiveTests`、`NavigationExecutionTests`、`CommandFeedbackTests`、`CommandFeedbackGraphicsTests`、`CombinedRegressionTests`、`TargetDecisionTests.FailedAutomaticTargetIsDeferredPerAgentAndMovementAllowsRetry` |
| F6 | 巡逻逐个检查候选，近但遮挡的 A 不妨碍发现 B；Tidal 的开场技能入口也遵守感知 | `EnemyTargetBindingTests.VisibleFartherCandidateIsScannedDuringPatrol`，覆盖 Base/Ranged/Modern/Tidal/Ancient |
| F7 | 属性、图腾及等价配置刷新保留技能截止时间；重排保留存续技能，新技能正常就绪；重复 SkillId 不产生额外冷却；普攻锁不随节点重入重置 | `CombatCooldownTests`，包括真实 `EquipmentSlotUI.TryEquip → Inventory 修正 → Pawn 定期刷新` 和重复配置两个变体 |
| R2/R3 | 三维射程、眼点/枪口射线、墙体和飞行段碰撞共同约束；玩家、Ranged、Sentinel 可跨高低差；玩家/敌弹/锚弹不穿薄墙；AOE/DOT 按作用原点重检墙体 | `PerceptionCandidateTests`、`RangedSpatialTests`、`CombinedRegressionTests.BlockedMuzzleCannotUseDirectDamageFallback` |
| R4 | Decision 使用 Pawn 的实际 Defense、全部唯一可见风险敌人及成员位置；普通候选无结果时评分远处可达撤离；不可接近敌人仍是风险但不挡住其他可执行目标 | `TargetDecisionTests`、`PerceptionCandidateTests.UnreachableVisibleEnemyDoesNotBlockExecutableCandidate` |
| R5 | 位移后撤销到达与旧背包打开观察；远处关闭背包不能完成资源，返回后需新的有效交互 | `ResourceDisplacementTests.DisplacementInvalidatesInventoryCloseUntilAgentReturns` |

## 复现与修复证据

| 阶段 | 失败证据及修正 | 通过记录 |
| --- | --- | --- |
| P0 | 先验证编译发现、隔离存档、真实 NavMesh/Physics；故意断言失败与超时均保留并核对续跑 | 见 [P0](../.planning/2026-09-11-agent-reproduction/p0_execution.md) |
| P1 | `010520-229`：撤离/战斗振荡、接受不可达命令；`011652-805`：零距离和位移后返回失败，定位 NavMesh baseOffset 距离口径 | `011339-769`、`012201-214`、`012315-921`、`012353-962`，见 [P1](../.planning/2026-09-11-agent-reproduction/p1_execution.md) |
| P2 | `013032-036`：Ranged/Anchor/Boss 死亡留场不换人及 F6；`012851-912`：高差射击/墙后发射；`015358-302`：爆心身体挡住 AOE/DOT 对照 | `015451-642`、`015217-842`、`015641-271`，见 [P2](../.planning/2026-09-11-agent-reproduction/p2_execution.md) |
| P3 | `020125-718`：资源翻转、风险漏计、默认防御和远处撤离缺失；`020035-003`：三类刷新绕过冷却 | `020715-242`、`020825-401`，见 [P3](../.planning/2026-09-11-agent-reproduction/p3_execution.md) |
| P4 | 缺失攻击配置挂死、禁用/完成目标误接受、开场攻击绕过感知、正式 VFX 粒子配置 Assert；Review 又复现失败目标立即重选和重复 SkillId 提前施法。夹具几何/姿态/清理问题单独记录 | 见 [P4](../.planning/2026-09-11-agent-reproduction/p4_execution.md)及下方最终重复结果 |

表中短编号均以 `20260911-` 开头，原始文件位于 `Logs/AgentReproduction/<完整编号>/`。失败记录没有被后续成功覆盖。

## 判读与隔离

1. NUnit XML 是最终 PASS/FAIL 权威；清单中缺失用例、环境/退出故障不计通过。`case.json` 保留协程执行事实，`nunit-final.json` 保存测试框架最终结论。
2. 每例真实跨帧执行并在末尾设置完成检查点，防止进入 Play Mode 时协程重载导致假通过；Transform 身份使用 `SameAs`，避免 NUnit 将其当子节点集合比较。
3. 启动器复制到有所有权标记的隔离项目，使用独立 company/product 和 persistentDataPath，前后校验源文件 SHA256。只管理自己启动的进程，正式存档和用户编辑器不受测试操作影响。
4. 夹具覆盖包括稳定 seed、较高生命值、按需关闭自动发现/技能、固定移动、可控 NavMesh 和实际配置副本；每个覆盖都有隔离目的。正式七种敌人 prefab、实际射击、装备槽、Raid 流程和正式反馈 prefab 均有集成验证。

## 最终重复结果与图形证据

最终运行 **`20260911-030638-823`**，每轮包含全部 14 组、75 个用例：

| 轮次 | 执行 / 预期 | 通过 | 失败 / 缺失 / 超时 |
| --- | ---: | ---: | --- |
| 1 | 75 / 75 | 75 | 0 / 0 / 0 |
| 2 | 75 / 75 | 75 | 0 / 0 / 0 |
| 3 | 75 / 75 | 75 | 0 / 0 / 0 |
| 合计 | 225 / 225 | 225 | 0 / 0 / 0 |

Regression 退出 0，42 个组进程均正常。运行前后及导出证据时核对 4,958 个源输入文件 SHA256 一致。保存了 [逐例结果和原始证据哈希](agent_repro_validation.json)；完整 XML、trace、Editor.log 与 manifest 位于本地 `Logs/AgentReproduction/20260911-030638-823/`，不随 Git 提交。该快照基于 P3 提交加当时未提交的 P4/P5 文件，不能只用 manifest 中的 baseCommit 代替文件哈希。

`20260911-024654-362` 的 71 例 × 3、213/213 是 Review 增补前的历史运行，单独保留，不混入最终 225 次统计。

正式 HUD 原始图像已由 Agent 读取检查：[成功](feedback/success.png)、[失败原因](feedback/failure.png)、[消退中](feedback/fading.png)、[已隐藏](feedback/hidden.png)。来源为上述批次 Graphics 第一次运行，1920×1080；顶部居中、暂停下消退有效，不拦截点击。测试 Camera 导出时将隔离场景中的 Canvas 切到 ScreenSpaceCamera，正式 prefab 保持 Overlay。2026-09-12 更正：当时的字符存在检查和视觉审阅漏掉了字体将「指」画成「址」的问题，现已修复原字体及 TMP 缓存，补充实际字形差异验证，见[字体修复报告](font_repair_report.md)。旧图保留为历史证据。

报告层故障构造 `ReportProbe-20260911-030411-704` 7/7 通过，包含成功 XML 后异常进程退出；故障不会被计入业务通过。

## 验证边界

- 自动构造覆盖已确认行为及关键边界，并未穷举所有关卡布局、任意对象数量、帧率、平台或独立 Player 构建。
- 实际撤离测试调用公开 presence 接口驱动生产计时/结算/销毁；装备测试调用实际装备槽 API。没有声称验证鼠标拖拽和全部触发器碰撞组合。
- 评分的路径风险保留原有直线邻近估算；Sentinel 保留正式锁定时长；Boss 咆哮保留已有掩体减伤规则。没有修改这些玩法数值和招式形状。
- 菜单出战阵容、仓库配装再次出战等产品缺口不属于本次逻辑修复，仍见 [README](../README.md)。

## 阶段提交

| 阶段 | 本地提交 | 内容 |
| --- | --- | --- |
| P0 | `29ec57c` | 隔离运行器和真实 Play Mode 基础验证 |
| P1 | `1700e67` | 指令打断/恢复、导航失败、交互及顶部提示 |
| P2 | `26512c9` | 目标有效性、候选扫描、空间约束与三维交战 |
| P3 | `e86513b` | 成员候选、风险评分及冷却保留 |
| P4 | `c840e20` | 组合/图形验证、Review 边界修复与最终重复 |
| P5 | 本文所在文档提交 | README、系统说明、阶段审查和完整验收证据 |

全部工作在本地 `dev` 分支分阶段提交，未推送。最终提交号可通过 Git 日志定位，运行快照以逐文件哈希核对。

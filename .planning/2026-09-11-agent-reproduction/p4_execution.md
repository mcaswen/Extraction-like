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

待执行。

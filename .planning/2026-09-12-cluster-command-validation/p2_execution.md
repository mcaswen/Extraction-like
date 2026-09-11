# P2：真实场景命令驱动、独立证据和验收

状态：在 P1c 回归期间准备实施小规划。沿用已确认大规划，拆为可独立编译验证的 P2a/P2b/P2c。

## P2a：脚本数据和证据基础

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandScenario.cs` + meta：强类型 Scenario/Step/Selector/Expectation 字段，明确类型、路由、距离、引用前序步骤、等待前提和双期限校验；只有固定可枚举操作，不支持执行脚本字符串。
- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandEvidence.cs` + meta：一次 Dispatcher 调用对应一次 attempt；订阅 Observer 转发的原始反馈序号，默认空 CommandId 拒绝仅落入同步调用边界，之后结果按非空 CommandId 归属。保存尝试前后角色状态、真实目标、耗时和反馈，不自行宣判整局通过。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidObserver.cs`：在原始 directive 事件写入后转发结果和序号，不屏蔽旧日志或失败探针。已有事件仍是权威来源，关联记录必须与之核对。
- Create `Assets/Scripts/Automation/SceneRaid/SceneRaidCarriedInventoryEvidence.cs` + meta：当前 UI 用公开格子和装备状态的深拷贝；非焦点用经过类型校验的私有角色快照，扁平化后不保留可写业务对象。缺快照返回证据缺失，不初始化库存、不调用结算读取、不切焦点。P1c 的生产初始化使未开包角色可只读观察。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCommandHarnessTests.cs`、`tools/agent-repro/cases.json`：同步无身份拒绝、连续两次拒绝隔离、异步旧命令归属和取消、销毁时解除订阅、深层数据往返、字段值校验、携带读取无状态改变。

P2a 不启用场景 ManualCluster 模式，先证明基础采集可信。C# 对强类型字段值和引用校验，PowerShell 对原始 JSON 的未知字段/深度/哈希校验；不为本轮引入第三方 JSON 包。

## P2b：步骤编排和实际运行入口

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidClusterCommandDriver.cs` + meta：有限步骤 Waiting → SubmitOnce → Next，引用此前尝试的真实移动、伤害、会话关闭、终态或近范围事实触发下一步；每条已提交命令继续跟踪动作和终态，步骤提交成功不等于验收通过。
- Extend `SceneRaidClusterCatalog.cs`：在冻结选择规则下解析真实群，按成员距离和稳定身份选群；复用只读 Catalog，选中后仍只把群送到 Dispatcher，不伪造最终成员。完整目录只在下令前采集，不逐帧全场扫描。
- Extend `SceneRaidScenarioConfig.cs`、`SceneRaidRunController.cs`：显式 ManualCluster，加入脚本和哈希，安排动作帧优先级；有命令/焦点动作的帧不让 InventoryDriver 另切焦点。采集初始、库存改变后和撤离前携带证据，普通 Autonomous 默认行为保持。
- Extend `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1`、`scene-raid-cases.json`；Create `cluster-command-scenarios.json`：SC08=4×、SC09=1×，完整归档六个冻结脚本，嵌套序列化显式深度和 SHA256。常驻 Editor、可见 Player、独立存档和硬期限沿用已有能力。

前提超时、自然死亡或目标自然消失保存 coverage missing，停止或跳过按脚本预先声明的步骤，不现场改目标或阈值重试。未知 Failed/Rejected 仍是行为失败。脚本停止后不再补发命令，保留自主尾段；MC01-X 允许没有库存会话但必须有可靠初始携带和真实结算。

## P2c：离线契约和最短真实冒烟

- Create `tools/agent-repro/SceneRaid.Settlement.psm1`：从 `SceneRaid.Contracts.psm1` 提取数量、布局、会话守恒和终态不变量；自主覆盖规则仍归原文件，不创建两套仓库算法。
- Create `SceneRaid.ClusterCommands.Contracts.psm1`：独立核对归档步骤、attempt、原始反馈、动作/终态、原始失败与唯一匹配的预期拒绝；核对携带和最终仓库。
- Extend `SceneRaid.Contracts.psm1`、`SceneRaid.Report.psm1`：按显式模式选择覆盖契约，旧 Autonomous 保持禁止手动命令，原始失败数不抹掉。分开证据、业务、覆盖和性能状态。
- Create `tools/agent-repro/Test-SceneRaidClusterCommands.ps1`；Extend 原报告/契约探针：错原因/角色/目标、额外拒绝、无 CommandId 错关联、只有 Accepted、重复步骤、死亡缺覆盖、丢携带/结算错误、无渲染和 Autonomous 手动污染均不得假通过。

基础自检通过后以真实场景最短脚本收尾验证，原始场景保持；受库存生产修改影响，补 SC02 自主冒烟。P3/P4 才执行最终 11 槽位矩阵，不把 harness 自测计入真实游戏成绩。

## 实施结果

P2a 已实现脚本 DTO、独立携带读取、同步/异步指令证据和 Observer 序号转发。首次 `Logs/AgentReproduction/20260912-041613-243` 12 项中 10 项通过；两项失败是测试在写入器保持打开时用 File.ReadAllLines 读取日志，Windows 共享访问冲突。修正测试为显式 FileShare.ReadWrite，未修改正式写入器；受影响两项在 `20260912-041745-196` 2/2 通过。

审查追加 Observer 转发异常隔离，诊断故障记为 ProbeFailure，不传播到正式命令。故障注入 `20260912-041917-508` 1/1 通过。当前 SceneCommandHarness 清单 13 个不同用例均已通过，包含原 2 个目录自检。命令拒绝对应原始反馈的 sequence，双人同帧空 CommandId 不串联，旧 Cancelled 和真实击杀后的 Completed 仍归各自命令；读取两人的携带没有关闭会话、切焦点、创建缺失快照或回写返回数组。

P2b/P2c 尚待执行。当前还未启用 ManualCluster 模式，没有真实场景玩家指令验收或新性能成绩。

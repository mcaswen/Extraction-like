# P2：真实场景命令驱动、独立证据和验收

状态：P2a/P2b/P2c 已完成，构造自检、旧契约回归、MC01-X 真场景冒烟和 SC02 自主回归通过。真实矩阵继续 P3/P4。

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
- Extend `SceneRaidInventoryDriver.cs`：仅增加库存变更版本和成功关闭的命令身份查询，供编排观察；不改变转移策略。携带采集在初始就绪、版本变化、撤离命令前触发，避免每帧导出物品。
- Extend `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1`、`scene-raid-cases.json`；Create `cluster-command-scenarios.json`：SC08=4×、SC09=1×，完整归档六个冻结脚本，嵌套序列化显式深度和 SHA256。常驻 Editor、可见 Player、独立存档和硬期限沿用已有能力。
- Create `tools/agent-repro/SceneRaid.CommandConfig.psm1`：两个启动器共用原始 JSON 字段/值校验、脚本选择和哈希，不复制校验逻辑。配置 schemaVersion=2 仅用于 ManualCluster，携带完整 scenarioJson 字符串及其 SHA256，并另存可直接阅读的 command-scenario.json；旧三模式保持 schemaVersion=1。字符串封装避免跨 Unity/PowerShell 数值格式差异破坏哈希，内部步骤仍有强类型解析与深度往返测试。

前提超时、自然死亡或目标自然消失保存 coverage missing，停止或跳过按脚本预先声明的步骤，不现场改目标或阈值重试。未知 Failed/Rejected 仍是行为失败。脚本停止后不再补发命令，保留自主尾段；MC01-X 允许没有库存会话但必须有可靠初始携带和真实结算。

P2b 字段细化：`agent=Focused, route=Focused` 表示对当前正式焦点下令，专用于 MC03 打开中的背包所属角色；尝试额外记录实际前后焦点，不用固定 AgentId 替代默认路由。其他步骤仍指定 1/2。MC03 的开包门槛同时要求活动背包角色等于焦点，目标目录在此时按实际角色采集。近远按群内最近未完成成员判断，不能因同群另有远成员就把整个近群算远群；实际执行成员继续从原始资源/交战事件核对。

## P2c：离线契约和最短真实冒烟

- Create `tools/agent-repro/SceneRaid.Settlement.psm1`：从 `SceneRaid.Contracts.psm1` 提取数量、布局、会话守恒和终态不变量；自主覆盖规则仍归原文件，不创建两套仓库算法。
- Create `SceneRaid.ClusterCommands.Contracts.psm1`：独立核对归档步骤、attempt、原始反馈、动作/终态、原始失败与唯一匹配的预期拒绝；核对携带和最终仓库。
- Extend `SceneRaid.Contracts.psm1`、`SceneRaid.Report.psm1`：按显式模式选择覆盖契约，旧 Autonomous 保持禁止手动命令，原始失败数不抹掉。分开证据、业务、覆盖和性能状态。
- Create `tools/agent-repro/Test-SceneRaidClusterCommands.ps1`；Extend 原报告/契约探针：错原因/角色/目标、额外拒绝、无 CommandId 错关联、只有 Accepted、重复步骤、死亡缺覆盖、丢携带/结算错误、无渲染和 Autonomous 手动污染均不得假通过。

基础自检通过后以真实场景最短脚本收尾验证，原始场景保持；受库存生产修改影响，补 SC02 自主冒烟。P3/P4 才执行最终 11 槽位矩阵，不把 harness 自测计入真实游戏成绩。

## 实施结果

P2a 已实现脚本 DTO、独立携带读取、同步/异步指令证据和 Observer 序号转发。首次 `Logs/AgentReproduction/20260912-041613-243` 12 项中 10 项通过；两项失败是测试在写入器保持打开时用 File.ReadAllLines 读取日志，Windows 共享访问冲突。修正测试为显式 FileShare.ReadWrite，未修改正式写入器；受影响两项在 `20260912-041745-196` 2/2 通过。

审查追加 Observer 转发异常隔离，诊断故障记为 ProbeFailure，不传播到正式命令。故障注入 `20260912-041917-508` 1/1 通过。当前 SceneCommandHarness 清单 13 个不同用例均已通过，包含原 2 个目录自检。命令拒绝对应原始反馈的 sequence，双人同帧空 CommandId 不串联，旧 Cancelled 和真实击杀后的 Completed 仍归各自命令；读取两人的携带没有关闭会话、切焦点、创建缺失快照或回写返回数组。

P2b 已接入六个脚本、SC08/SC09、显式 Player 参数、携带读取和有限步骤驱动。`Logs/AgentReproduction/20260912-043033-578`：18 项 Harness、11 项 ClusterInventory 全部通过。增加低频变化触发的 `command.progress` 后，`20260912-045044-393` 再次 29/29，包含同帧真实击杀后生命值从 50 到 0、终态和 attempt 归属。此前 `045001-964` 使用了错误命名空间的过滤字符串，执行 0 项，运行器判基础设施失败；没有将空运行记为通过。

P2c 已提取共享结算。首次 PowerShell 自检发现大小写不敏感变量名碰撞（参数 Carried 与局部 carried、wrapper result 与输入 Result）；修正为 CarriedEvidence、assessment 后，`Logs/SceneRaidContractProbes/20260912-043316-042` 44/44 原自主契约通过。`Logs/SceneRaidReportProbes/20260912-045001-972` 78/78 原报告探针通过；`Logs/SceneRaidCommandProbes/20260912-045001-964` 53/53 新脚本/命令/结算探针通过。

审查补充：Search 受有效伤害取消时，替换它的是正式 CombatDamage 指令，不能只接受下一条测试命令作为替换证据；Extract 撤离销毁时的 Cancelled 也需按正式撤离终态区分。随后的取消反例暴露其夹具本身包含成功撤离，需要分开正常退出和任意提前取消的断言，继续收紧并复跑。真实冒烟 `Logs/SceneRaid/20260912-045220-167` 已启动，当前尚未形成真实场景验收或新性能结论。

### P2c 交接修正小规划

首轮真实启动未进入 Play Mode：常驻 Editor 内仍加载旧 schema/mode 校验器，`PollRetainedEditor` 在 `AssetDatabase.Refresh` 前调用完整 `LoadExplicit(false)`，新 ManualCluster 被旧代码拒绝，导致无法刷新。属于运行器更新协议故障，不是场景 gameplay 失败。

Extend `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs`：空闲阶段先用既有 `SceneRaidRequestFile.ReadPending` 读取请求身份并触发一次刷新；完整模式/隔离校验仍在刷新后的 `Run`，不得绕过实际执行的验证。Reuse `SceneRaidRequestFile.cs` 的显式 enabled 开关、文件共享和缺请求处理，无新公共接口或工具协议。旧 Editor 首次迁移以兼容 SC00 Audit 请求触发源码刷新，保留原进程。归档本次失败并停止其外层等待，不伪造 editor-ready/game result；随后原脚本重新运行。

### 真场景冒烟结果

`Logs/SceneRaid/20260912-045558-205`，MC01-X、4×/731：25.36 秒墙钟，两人实际撤离、库存结算通过；证据 PASS、游戏 PASS，0 errors、0 Failed/Rejected、0 停滞告警。3 条已提交命令全部有真实移动和终态；Actor2 撤离途中受击 → 反击完成 → 恢复原命令也已观察。两人初始携带确为空，初始/撤离前只读快照和最终空仓库一致，不是把缺失快照当空。

覆盖 PARTIAL：脚本依序等待 Actor1 进入 Near 后才考虑 Actor2，Actor2 在该帧已经先完成撤离，因此 exit-near-2 没有下达。保留该自然时序缺口，不改冻结脚本或角色速度来补造覆盖。Actor1 的 Near 重复下令已通过；两人 Far 指令、受击恢复和未开包先撤均有真实证据。4× 诊断平均 88.73 FPS，不能替代 1× 性能验收。

SC02 自主回归 `Logs/SceneRaid/20260912-045721-786`：67.72 秒墙钟、10 次库存会话，两人搜刮、交战、满包撤离，仓库和箱子守恒全部通过；evidence/game PASS，0 errors、0 Failed/Rejected、0 停滞。原 Autonomous 的移动、实际转移、伤害、暂停恢复门槛均通过，没有因支持手动早撤而放宽。

P2 完成，不需要改变已确认的业务架构。进入 P3 六个冻结脚本的实际执行，最终性能和重复矩阵仍待 P4。

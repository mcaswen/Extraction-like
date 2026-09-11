# P5c：测试请求交接，正常战死的结果分类

## 需求与证据

用户最新明确：战死是预期玩法结果，之后不因战死进入 bug 修复。保留伤害、生命、目标策略和反击规则，不要求每个随机种子都全员生还。矩阵区分全部撤离和正常战死，运行错误、卡死、异常终态、错误结算及性能仍严格检查；完整搜打撤覆盖由实际成功回合提供，死亡回合不冒充全员撤离。

另发现自动化故障：`20260912-011111-660` 在常驻 Editor 读取新请求时抛出 Sharing violation，`Poll → Fail → Finish` 使用上一轮 `_config`，覆盖新请求并再次写旧轮 ready/error。Editor 实际仍空闲，不能当作原生挂起。确认未进入场景后，原请求于同一运行期限内重新投递，留下 `request-recovery.json` 和被覆盖请求，不改游戏状态。

## 文件归属与设计

| 决策 | 具体文件 | 职责与变化 |
| --- | --- | --- |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidRequestFile.cs`、`.meta` | 文件协议独立于场景：请求只由启动器发布，Editor 写独立完成标记；按 runId 消费，旧轮完成不能覆盖新请求。共享读取允许原子替换，暂时文件占用留待下一次轮询。 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs` | 复用上述请求读取与完成标记，原有隔离、参数校验保留；手动 Play 不重跑已完成请求。 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs` | 完成后清空已消费配置；空闲读取失败不结束上一轮、不关闭 Editor。活动轮错误仍输出失败证据。完成标记替代重写请求配置。 |
| Extend | `tools/agent-repro/Invoke-SceneRaid.ps1` | 从内存配置写本轮归档，再原子发布请求，避免发布后 Copy-Item 与 Editor 抢读同一路径。 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidRequestFileTests.cs`、`.meta` | 独立协议构造：读取锁释放后重试、旧轮确认与新请求并存、完成请求不再启动、损坏请求不伪装成功。复用已有测试基础设施。 |
| Extend | `tools/agent-repro/cases.json` | 登记上述定向协议测试。 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs` | 只调整自动化结果标签：观察到任务失败终态记为 `RAID_OBSERVED_FAILURE`；报告核对后才能认定正常战死，防止把结算失败也当成战死。不改 RaidFlow 或任何 Gameplay。 |
| Extend | `tools/agent-repro/SceneRaid.Contracts.psm1` | 对死亡终态验证两名角色均有去向，存活者撤离/结算集合一致，死亡者血量为零；仓库只增加实际撤离者携带物品。背包已有会话仍检查守恒。死亡前未发生的搜刮/战斗覆盖记录为缺失，不当作死亡 bug。 |
| Extend | `tools/agent-repro/SceneRaid.Report.psm1`、`Invoke-SceneRaidPlayer.ps1` | `EXPECTED_DEATH` 和完整撤离 `PASS` 分列；只有终态及证据有效、没有运行异常才接受正常战死。1× 平均 FPS >60 门槛不变。 |
| Extend | `tools/agent-repro/Test-SceneRaidContracts.ps1`、`Test-SceneRaidReport.ps1` | 构造单人/双人死亡及未死亡伪终态、活人未撤离、死亡者错误入库等反例；错误和缺证据不能被死亡标签掩盖。 |
| Extend | `tools/agent-repro/README.md`、本目录 `task_plan.md`、`architecture_review.md`、`p5_player_verification.md` | 更新用户确认口径、实际执行证据与审查；不回写历史原始报告。 |

保持进程编排 → Editor → Automation → Gameplay 的依赖方向。文件协议没有 UnityEditor/NUnit 依赖；完成标记与请求分开使每个文件只有一个写入者。没有新增生存策略、回血、伤害调整或测试目标指令。属于已授权自动验证的修复和用户明确要求的验收调整。

## 实施和验收

1. 等当前源码冻结轮完成，保存交接故障及恢复证据。
2. 实现文件协议修复，运行定向 NUnit 和现有 9 项会话探针；在同一个 Editor 连续执行两轮，核对 runId、完成标记与 PID。
3. 实现正常战死分类，运行报告和契约构造。严格保留错误、遗漏终态、错误仓库的反例。
4. 继续新场景矩阵：731 三轮 4×、1731/2731 各一轮 4×，1× Editor 和可见 Player。已经完成的同一 Gameplay/场景输入可保留，但协议/分类改动后的回合明确记录新源码哈希；不为了生还反复重跑死亡种子。
5. 汇总实际覆盖、运行结果、性能和剩余限制，写最终文档和机器报告。按阶段提交，用户 FFT 的三处场景差异仍不提交。

## 实施结果

文件协议已实现为请求/完成标记分别写入；启动器不再发布后 Copy-Item 读取请求，空闲 IOException 下一次轮询重试，完成后清空旧 `_config`。运行器仅输出中性失败终态，报告核对后才给出 EXPECTED_DEATH。

`20260912-012314-577` 的 5 项 SceneRequest 构造通过，正常退出、源码未变；`SceneRaidSessionProbes/20260912-012326-338` 9 项通过。`SceneRaidContractProbes/20260912-012246-373` 44 项、`SceneRaidReportProbes/20260912-012250-052` 78 项通过，包括正常单人/双人/早期死亡，以及活人未撤离、假死亡、漏结算、死亡物品入库和异常日志的反例。

当前场景的正常速度轮 `20260912-011111-660` 在同一外部期限内重新投递后完成，Editor PID 60224 保留，源码未变。平均 107.6121 FPS，0 程序错误、0 失败指令、0 停滞。Agent 2 已撤离，Agent 1 战死；新口径通过只读 `death-adjudication.json` 重核死亡终态和存活者仓库，原始 result/report 不回写。

上一轮 `20260912-010851-707` 的游戏事件和仓库也符合正常死亡，但其 editor-ready/error 被后来的交接故障改写，生命周期证据受污染，明确排除最终矩阵，原始污染文件和新审查记录均保留。731 的三次正式矩阵改用协议修复后的三轮；此前 2731 完整撤离、当前 1× 正常死亡与后续轮次保持相同 Gameplay/场景输入，最终汇总分别记录完整源码与排除自动化后的输入哈希。

同一 Editor PID 60224 连续执行 `20260912-012410-261`、`20260912-012607-353` 两轮 731 / 4×，均两人自主撤离、完整契约通过，0 程序错误、0 失败指令、0 停滞。完成标记依次匹配各 runId，第二轮成功接收、结束后持续空闲，源哈希均未变；第一轮全部覆盖项为 true。协议修复和分类调整完成测试/审查，进入提交；余下矩阵与 Player 在 P6 交付中汇总。

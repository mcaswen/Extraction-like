# P3e：整局完成契约和 1 倍速复核入口

前置结果：4 倍速原场景已有两人撤离、零错误/失败指令的一轮，离线 20 件库存和仓库一致。保存失败构造已修复。下一步把人工分析过的证据变成每轮自动判定，不把单轮顺利等同于整个大规划完成。

## 文件职责和依赖

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidItemEvidence.cs` | 把容器快照展开为带 parentIndex 的平面物品行，包含定义 ID、数量、类型、尺寸、价值；消除递归 DTO 的 Unity 序列化深度告警 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryLedger.cs` | 使用平面行，记录正式装备槽的只读快照，保持原操作守恒检查 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryDriver.cs` | 关闭写回后再次记账，独立记录实际箱子剩余和背包，没有新的游戏指令入口 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidPersistenceEvidence.cs` | 启动/结束复制当前隔离仓库 JSON，导出实际物品定义和仓库尺寸；仅复制证据，不实例化另一个仓库服务或调用会关闭 UI 的结算方法 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs` | 编排持久化证据捕获，仍由原观察器拥有业务事件 |
| Create | `tools/agent-repro/SceneRaid.Contracts.psm1` | 独立核对完整回合的双方移动、真实背包操作、真实战斗伤害、暂停恢复、撤离/结算集合、前后仓库数量和布局，输出逐项覆盖与失败原因 |
| Create | `tools/agent-repro/Test-SceneRaidContracts.ps1` | 构造完整证据、丢物/增物/重叠/未知定义/假撤离/缺交战/缺会话/人工指令等反例，无 Unity 进程即可检验报告可靠性 |
| Extend | `tools/agent-repro/SceneRaid.Report.psm1` | 组合已有证据完整性和新完成契约，只有全部满足才允许 gameStatus=PASS；性能验收仍独立 |
| Extend | `tools/agent-repro/Invoke-SceneRaid.ps1`、`scene-raid-cases.json`、`README.md` | 增加 SC03 正式 1 倍速自主回合，默认 360 秒墙钟；SC02 保持 4 倍速，更新说明和清单数量 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidStorageTests.cs` | 平面物品证据保留内嵌层级和数量、实际持久化证据文件可读的定向验证 |

依赖仍为 Automation → Gameplay，PowerShell 只读证据。没有新生产公共接口，也不通过探针推进背包/撤离。仓库位于同一 runId 的隔离产品目录，输出初始和最终快照便于独立回读。装备按正式可结算的六个装备槽观察，背包/弹挂容器本体不计为可带出物品，内部物品依规则展开。

## 验收与边界

- 完成契约要求 required/extracted/settled 都是 `{1,2}`，任务成功且未失败；两人均有移动、真实会话和成功转移，至少一场有具体敌人生命下降的真实交战。容量撤离、受击中断/恢复单列覆盖，没有发生的回合不伪造命中，必要构造已有独立证据。
- 每次会话关闭后的实际剩余与关闭前一致；最终仓库等于初始仓库加两人的最后背包和可结算装备，逐 ItemID 核对数量，检验定义、正数量、页面越界、重叠和特殊格。完整回合期间不能出现 ManualTargetClick 请求。
- 旧证据缺少新文件仍为 NOT_FULL_RAID_VALIDATED，不能回填虚构字段。新契约缺覆盖和逻辑失败分别列出；证据损坏不能成为游戏通过。
- 程序化报告故障构造先跑，再做相关 NUnit，之后 SC02/SC03 原场景复跑。1 倍速性能只能使用真实帧样本，最终 60 FPS 门禁和 Player 构建仍归 P4/P5，不随游戏契约 PASS 自动通过。

## 实施结果

- `20260911-224349-505` 完成契约故障构造 23/23 通过，涵盖缺文件、错 runId、丢失/增加/重叠/越界/未知物品、错仓库所属、特殊格、假完成、缺移动/交战/转移/开关证据、写回变化、装备缺失、非法 parentIndex 和人工指令。
- `20260911-224229-562` 既有报告构造 47/47 通过，旧证据不会被新契约自动升格。
- `20260911-224207-518` Storage 8、Inventory 9，共 17 项 Play Mode 构造通过。14 层内容经平面行序列化后完整回读，原 UI 会话、部分堆叠、保存回滚、失败终态保持通过。
- SC02 新契约整局复跑已启动，运行期间冻结源输入。
- SC02 `20260911-224354-827` 两人 68.76 秒墙钟、196.24 秒游戏时间撤离，0 错误、1 次搜索 NoProgress。首轮契约遇到空箱数组的 PowerShell Measure-Object 无 Sum 属性，保留原 FAIL；改为显式累加，并增加空箱正常证据构造。`20260911-224649-627` 共 24/24 契约探针通过，原始回合离线重判写入独立 `contracts.recheck.json` 为 PASS，原报告不覆盖，NoProgress 仍是玩法问题。
- 原场景没有再出现平面 Ledger 的序列化深度告警，告警从 71 降为 69；其余场景告警没有隐藏。SC03 正常速度完整复核启动，最终结果待记录。

### SC03 报告边界调整

`20260911-224707-038` 正常速度 220.61 秒墙钟完成两人撤离，11 次实际背包会话，零错误、零失败指令、零停滞。仓库逐 ID 数量相等。报告失败来自两次 focus 后、Interact 前的范围失效：Driver 的 Close 在没有 session 时仍发出 closed。修正归属仍是 `SceneRaidInventoryDriver.cs` 的日志语义，不改变游戏交互条件；未打开记 canceled。`SceneRaid.Contracts.psm1` 按每个 Agent 的 focus/open/close 顺序和资源/指令身份验证，兼容旧日志中有匹配 focus 且未 opened 的 interaction_invalidated，不能直接忽略所有关闭。`Test-SceneRaidContracts.ps1` 增加取消后正常打开、无归属关闭、错误身份、漏关和重复打开等构造。原始报告保留，离线重判另存。

`20260911-225402-910` 完成契约 31/31、既有报告 47/47 通过。SC03 独立重判 `contracts.recheck.json` PASS，双角色移动/背包、真实战斗伤害、暂停恢复、满包撤离、反击恢复全部命中；20 件物品正确入库（此前文字合计误记为 21，逐 ID 契约和原始仓库没有变化）。原始报告的失败保留。回合原始证据路径和 SHA-256 见 `p3_diagnostics.json`。

1 倍速性能初步分析：固定预热 10 秒后 22,355 帧，平均 106.47 FPS、P99 17.96 ms、1% Low 48.78 FPS、最大 39.81 ms、422 帧超过 16.667 ms。不能判性能通过；原三热点平均 Discovery/Pawn/Zone 为 0.051/0.187/0.257 ms，Cluster LateUpdate 平均 0.224 ms，后续需解释其外的整帧时间。计时器为包含子调用的 Total，不能相加当 Self。

最终冻结源输入复跑 `20260911-225442-257`：4 倍速 69.66 秒墙钟、10 次背包会话，两人自主撤离；原始 evidenceStatus、gameStatus、completionContracts 均 PASS，0 错误、0 失败/拒绝指令、0 停滞告警。它是新契约从运行到报告的直接通过证据，1 倍速历史重判不再是唯一依据。Editor PID 15476 已退出本轮 Play Mode、保留空闲。P3e 完成，可以提交，间歇导航和性能进入下一小阶段。

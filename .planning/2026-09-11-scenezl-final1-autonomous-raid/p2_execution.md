# P2：正式背包链路的无人操作驱动

日期：2026-09-11。状态：小规划，沿用户已确认的大规划实施。资源事件已提前进入 P1；测试只替代玩家的背包操作，目标选择、寻路、攻击、撤离仍归游戏。

## 范围和验收

SC02 在同一场景按 2× 游戏时间运行，物理 fixedDeltaTime 保留 0.02 秒。背包搜索继续使用正式 unscaledDeltaTime；打开/关闭背包由原 UI 暂停和恢复时间，控制器不每帧覆盖 timeScale。最终性能仍在 1×、4K、高画质验证。

本阶段跑到自主闭环或第一个真实阻断点，不能把容量不足、停滞或超时当成功。构造验证搜索未完成时不可转移，合法旋转放入，空间不足时保留物品，关闭后箱内数据守恒，事件失效后停止操作。两角色共享 UI 按真实到达顺序公平排队，焦点、命令、资源和会话归属必须一致。

## 文件和职责边界

| 决策 | 文件 | 职责和接口 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Backpack/DraggableItemUI.Drag.cs`、`DraggableItemUI.State.cs` | 将现有快捷转移整理为公开 TryQuickTransfer，点击也调用同入口，检查搜索/揭示、拖拽、会话、网格策略和实际可放位置 |
| Create | `Assets/Scripts/Gameplay/Backpack/InventoryQuickTransferFailure.cs` | 共用失败原因枚举，独立于 UI 文本和 Automation，区分仍在搜索、无会话、非法来源、规则拒绝、无空间 |
| Reuse | `InventoryScreenController.cs`、`InventoryGridInteractionPolicy.cs`、`InventoryUIController.cs`、`InventoryItemRuntimeState.cs`、`LootBoxEntity.cs`（均在 `Assets/Scripts/Gameplay/Backpack/`） | 正式切换/打开/关闭、布局和物品状态，不在测试复制装箱算法 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryDriver.cs` | 消费实际等待事件，排队、切焦点、开箱、等待真实搜索、逐件合法转移、关闭，校验当前命令/资源/会话；不提交目标或撤离指令 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryLedger.cs` | 按物品配置身份和数量核对来源/背包/剩余箱子守恒，记录布局和运行时物品 ID；不以 UI 重建后的实例 ID 代替数量核对 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs`、`SceneRaidBootstrap.cs`、`SceneRaidRunController.cs` | SC02 配置、有限运行、驱动生命周期、最终检查点和模式分离 |
| Extend | `tools/agent-repro/Invoke-SceneRaid.ps1`、`scene-raid-cases.json`、`SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1` | 允许显式 Autonomous 模式；完整采集、行为失败和未完成整局分开判定；故障探针拒绝伪自主成功 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidInventoryTests.cs` | 在隔离 Play Mode 构造真实 UI 和物品，覆盖正式共用入口、所有权、搜索、布局和剩余物品；不修改正式资产 |
| Extend | `tools/agent-repro/cases.json`、`README.md` | 定向测试清单和运行说明 |

Gameplay 只发布业务事实、提供普通玩家也能调用的受约束入口；依赖方向仍为 Automation → Gameplay。队列和超时归 Driver，数量核对归 Ledger，场景生命周期归 RunController。

## 驱动控制流

等待真实 WaitingForInventory → 验证活动 Search 的 AgentId/CommandId/Resource → 按到达顺序入队 → 原 Registry 切焦点 → 等一帧让节点/UI 同步 → 实际 LootBox.Interact → 捕获正式会话对象 → 等待搜索和揭示结束 → 按格子顺序尝试全部候选 → 校验数量 → 正式 CloseInventory → 校验剩余箱子和时间恢复 → 观察游戏推进。

每个操作前都重查角色存活、命令匹配、资源有效和等待事实。Left/Completed 清理原等待；旧事件不能清掉新命令的排队项。一次只操作一个 UI 会话；取消后不能打开或转移旧箱子。UI 搜索暂停游戏时仍按墙钟有界运行。

## 已知边界和后续调整

- 源码现有快捷转移只寻找矩形空位，没有合并堆叠；P2 保持该规则，不能声称快捷转移已覆盖所有堆叠可能。真正“满包自主撤离”属于 P3，届时必须区分单件过大、全部合法候选不可放、可堆叠/整理等条件，不能由 Driver 直接发撤离指令。
- 搜索树前置 MoveToTargetActionNode 未成功时，搜索节点尚未发等待事件。Driver 必须继续等待/报告移动阻断，不能按距离自行开箱。
- 原场景 Agent 1、2 的世界缩放都约 3，身体包围盒宽约 3 米，NavMesh radius 分别 1.5 和 0.5；需要 P3 对照实际避障/Collider 定向定位，不能只修改探针数值消除告警。
- P1 的 Editor 原生退出挂起暂未定位；报告必须保持进程失败。背包构造用例可独立运行，不能用其通过代替整局验收。

## 实施结果

尚未实现。每次首次失败、修复和复跑结果在此追加。

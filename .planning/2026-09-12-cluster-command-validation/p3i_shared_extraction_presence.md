# P3i：多人共用撤离点的触发器状态覆盖

## 现场、范围和归属

第五版固定矩阵的 1× Player `Logs/SceneRaid/20260912-073808-127/PlayerRun` 暴露新问题。约 170 游戏秒，两人前往雨林同一 ExtractionPoint；到 265 秒仍在中心约 1–2 米处移动，未结算。两人身体水平直径都是 3 米，导航在避让，但 3 秒撤离迟迟不完成。此前 10 个手动槽位和 SC02 均通过，不能用这些绿灯替代最终 Player。

源码中 `RaidFlowController.cs` 已按 AgentId 保存独立 `AgentExtractionProgress`；`ExtractionPointController.cs` 却只存 `_playerCollider`、`_playerAgentId` 和一个 inside 布尔值。不同角色的 OnTriggerStay 会先报告旧角色离开，再切换为新角色，持续删除对方读条。旧 `SceneRaidTerminalTests.cs` 直接调用 SetAgentInsideExtractionPoint，验证了流程结算，绕过了这层物理入口。先构造证明，不只根据源码推断全部现场机制。

本阶段修复范围是既有撤离触发器的多角色、多 Collider 状态，不增加新撤离玩法，不扩大范围、缩短倒计时、改变避让/生命或移动角色来掩盖实局问题。若修复触发器后正式指令仍不能完成，再据新证据评估动作层，暂不改 `ExtractActionNode.cs` 或通用导航。

## 文件、设计和小步骤

- Reuse `Assets/Scripts/Gameplay/Raid/RaidFlowController.cs`：保留现有每角色计时和结算入口。
- Extend `Assets/Scripts/Gameplay/Raid/ExtractionPointController.cs`：按 AgentId 保存 Collider 集合和最后上报状态，某个 Collider 离开不清掉同角色其他 Collider，更不能清队友。Update 清理已销毁/禁用 Collider，失去最后有效 Collider 才离开；组件关闭清所有 presence，关闭期间的触发消息不得重新进入。仍按既有有效水平范围、状态变化通知，保留 Agent 与 legacy Player 入口区别。私有 presence 数据仍属于本组件，没有独立新业务职责，因此不创建泛用触发器框架或移动文件。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ExtractionPresenceTests.cs` 和 meta：独立拥有真实 Trigger/Physics 层用例，与直接流程调用的终态测试分开。包含双人同时停留与真实结算（1×/4×）、一人离开不影响另一人、同角色多个 Collider、禁用/销毁与重新进入、正式 Dispatcher 双人同点。必要时加载真实场景的雨林记录位置验证，仅夹具准备可定位/关闭无关实体，实际局不改状态。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs`：既有 Raid 快照增加每角色实际撤离点、读条秒数、配置时长，复用原有私有字段读取缓存，纯读取 `RaidFlowController` 的字典，不增加生产公开接口。构造验证快照和实际进度一致，不通过探针启动或重置读条。
- Extend `tools/agent-repro/cases.json`：登记新组/参数，已有终态、撤离中断/恢复、手动改令、库存结算按影响选择回归。

控制流为真实物理回调 → 按角色聚合 presence → 原 RaidFlow 独立计时/结算；Automation 只读取。碰撞体集合只在进出或失效时修改，普通刷新 O(当前重叠 Collider 数)，复用清理缓冲，不逐帧扫描全场 Agent。

先写夹具并保留红灯，修复后跑新组和相关撤离/库存/反击回归，检查两人各自读条、停止/改令/受击清理和最终仓库。第五版 Player 继续按原 600 秒上限自然收尾，保留失败；运行期间只修改本规划，不修改冻结源码。修复完成后提交并重新冻结最终矩阵，避免混用不同输入的 FPS 或绿色结果。

## 实际结果

Player 按原 600 秒上限收尾，原生退出码 0，验证 FAIL：94 次额外指令失败、18 次停滞嫌疑，无运行异常。162232 个真实渲染帧超过固定 100000 帧采集容量，最终状态为 HARNESS_FAILED / Evidence buffer overflow，不能计算合格 FPS；报告中的 no_graphical_frames 是终态证据无效后的聚合分类，不代表没有实际渲染。第五版完整 11 槽位和自主回归归档为 `p4_runs_superseded_a6e97aa.json`。帧容量问题另列后续采集治理，不通过改写本次报告处理。

94 次失败全部为 NoProgress，两人反复尝试相同撤离点。14 项物理用例已实现，首次 `075451-024` 为夹具误用不存在的 IsMissionCompleted 属性而编译失败；改为已有 IsInputLocked 加实际快照 missionCompleted/settledAgents 验收，未添加生产接口。

红灯 `075630-563`：14 项中 5 通过、9 失败。真实 Prefab 的双人 Dispatcher 撤离在 1×/4×均超时，静态双人存在记录缺 A，禁用脚本后仍收到物理消息重新产生进度，多 Collider 全部禁用后残留进度，范围边缘角色还会清掉有效队友。按本规划在原 Controller 内改为每角色集合，保持状态变化通知和原水平几何；只读读条快照复用 ReadModel 的缓存字段读取，增加 Public 标志以读取私有进度类型的公开字段，字段缺失仍抛错。

修复后 `075915-262` 12/14 通过，真实 Prefab 的两种倍速双人指令均完成实际结算，无需更改动作层、导航或场景尺寸。静态双人用例已能独立读条，但因夹具禁用 Pawn 导致从 Registry 注销、库存未初始化而结算失败；修正为保持 Pawn 注册，仅关闭 NavMeshAgent（默认无指令、Discovery 关闭），不绕过正式库存快照检查。

最终新组 `080130-340` **14/14 通过**。相邻回归 `080734-227` **80/80 通过**，逐个完整 NUnit 名称对照 cases.json：SceneInventory 9、SceneTerminal 4、SceneRetaliation 6、SceneCommandHarness 19、ClusterCommandTransition 31、ClusterCommandInventory 11，无缺失。包括撤离受击恢复、手动改令、双人库存和结算，未修改原行为断言。

架构审查见 architecture_review.md 的 P3i。未改场景、撤离动作或 NavMesh；真实整局最终验证将在 P3j 完成后冻结同一源码重跑，当前构造通过不能替代最终 Player。

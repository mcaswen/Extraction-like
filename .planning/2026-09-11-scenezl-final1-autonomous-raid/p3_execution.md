# P3：4 倍速自主搜打撤闭环

日期：2026-09-11。用户已确认继续后续阶段，逻辑测试改为 4 倍速，复用隔离 Editor。沿用主规划的模块边界、满包自主撤离规则和剩余物品保留要求。

## 小阶段和验收

1. P3a 基线：Extend `tools/agent-repro/scene-raid-cases.json`，仅将 SC02 的 simulationSpeed 改为 4。Reuse `SceneRaidRunController.cs` 和 `Invoke-SceneRaid.ps1`，保持原物理步长和正式背包暂停，先运行 120 秒墙钟有界回合，读取事件、角色位置、敌人生命和失败堆栈。性能验收仍为 1 倍速。
2. P3b 会话正确性：检查 `Assets/Scripts/Gameplay/Agent/AI/Actions/SearchResourceActionNode.cs`、`Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs`、`InventoryScreenSessionContext.cs`、`LootBoxEntity.cs` 的会话身份及关闭结果。具体接口在源代码扫描后补齐，修复无关会话导致搜索完成的问题，构造反例验证。
3. P3c 容量到撤离：正式 Backpack 产生按 Agent 隔离的容量事实，Agent 搜索和目标发现消费该事实；Automation 只做原 UI 操作，不代发撤离。补齐所有合法候选、旋转/堆叠、剩余物品和另一角色继续搜刮的测试。具体文件边界在实现前补充。
4. P3d 原场景复跑：依次定位实测战斗、导航、撤离/结算阻断，必要时直接修复场景接线。每项修复先记录文件职责，再做定向回归。完整回合覆盖、库存/仓库守恒未满足时不能报整局通过。

依赖方向保持 Automation → Gameplay；游戏不引用测试代码。复用当前进程，源输入在每轮运行期间冻结。NUnit 构造回归使用独立工作区，避免覆盖存活 Editor 的项目。每阶段写入实现结果和 architecture_review.md，验证后使用中文正文提交。

## 实施结果

### P3a 首次基线及倍速修正

`20260911-214640-588` 复用 PID 23544，56.78 秒墙钟、42.85 秒游戏时间，7 次背包会话，在 Agent 2 容量阻塞处结束。4 条错误均来自 `Assets/Art/VFX/ZombieTentacleCorrosionVfx.cs:1031` 在播放时设置 duration。无指令失败，证据完整，玩法仍有问题。

实际快照 timeScale=1，原因是 BeforeSceneLoad 的设置被 `RaidFlowController.Awake()` 重置。Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs`：将倍速应用移到 Start（场景 Awake 之后），仅一次应用，记录请求/实际速度、fixedDeltaTime；不每帧覆盖正式暂停。复用原场景复跑确认 4→0→4，禁止只凭配置宣称加速生效。

### P3b 文件和接口细化

- Extend `InventoryScreenSessionContext.cs`：记录 `SourceObject`、打开时的 `AgentId`、`IsClosed`、`CloseResult`。会话对象本身承载生命周期，避免 UI 重开或焦点变化覆盖旧完成事实。
- Extend `LootBoxEntity.cs`：创建会话时填写真实源对象。
- Extend `InventoryScreenController.cs`：在打开时绑定角色，关闭写回成功后记录该次结果；控制器不判断 Agent 搜索和撤离策略。
- Extend `SearchResourceActionNode.cs`：只消费当前等待资源、当前角色匹配的会话；仅关闭已观察的那次会话才能推进。位移后只关闭该资源对应的会话。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidInventoryTests.cs`：用正式 UI 构造普通背包、另一箱子、另一角色不能结束原搜索，正确会话可以结束。原四项背包构造回归仍运行，双 Agent 场景改用 4 倍速验证暂停恢复。

这属于既有 Backpack 会话和 Agent 搜索边界内的修复，不添加反向依赖或全局搜索状态。

P3a 修正后 `20260911-214912-023` 同 PID，28.57 秒墙钟、57.33 秒游戏时间，7 次会话；快照只有 4 和正式暂停 0，暂停恢复正确。4 条粒子断言未修；新增 27 次 Unreachable（Agent 2 为 26 次），反击目标包含根坐标高于地面的 AnchorSentinel，留待 P3d 定向处理，不将加速轮称为逻辑通过。

P3b 红灯 `20260911-214901-479` 在“Plain backpack is not the waiting box”断言失败，证明普通背包会误完成当前资源。修复后 `20260911-215340-615` 5/5 真实 Play Mode 构造通过，包括普通背包、无关箱子拒绝，匹配箱子关闭完成，保留未拿物品，双角色库存隔离和 4 倍速暂停恢复。

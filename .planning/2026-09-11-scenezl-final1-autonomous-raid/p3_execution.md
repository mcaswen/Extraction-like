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

### P3c 文件、接口和验收细化

| 决策 | 具体文件 | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Gameplay/Backpack/InventoryStackTransfer.cs` | 查询同物品且已搜索、无内嵌内容的可合并堆叠，执行有限数量搬运。正式快捷转移和容量查询共用，允许部分堆叠，保存源剩余 |
| Create | `Assets/Scripts/Gameplay/Backpack/InventoryLootCapacityAssessment.cs` | 只读评估当前会话剩余物品，区分未搜索、可转移、整理后可转移、容量不足、规格/策略不兼容；复用正式网格和整理算法，不采用背包占用率阈值 |
| Extend | `Assets/Scripts/Gameplay/Backpack/DraggableItemUI.Drag.cs`、`DraggableItemUI.State.cs` | 快捷入口先尝试合法堆叠，公开只读可交互事实，沿用原空位/旋转放置路径 |
| Extend | `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs`、`InventoryScreenSessionContext.cs` | 关闭清理前计算会话容量结果，写回后才提交已关闭事实；不选择撤离点 |
| Extend | `Assets/Scripts/Gameplay/Agent/Data/AgentBlackboardKeys.cs`、`AI/Actions/SearchResourceActionNode.cs` | 匹配会话报告容量阻塞后，保存本 Agent 的本局撤离意图，结束当前搜索指令，不完成或清空资源群 |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs`、`Decision/AgentTargetDecisionController.cs` | 自主目标选择在容量撤离意图下仅考虑可达出口，保留手动指令和受击反击优先级，沿用既有反击后恢复撤离 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryDriver.cs` | 全部候选尝试后调用正式整理入口（每会话至多一次），关闭容量阻塞会话后继续观察游戏；检查每次实际搬运数量，不能代发撤离 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs` | 快照公开容量撤离意图，定位选择和执行链路 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidInventoryTests.cs`、`TargetDecisionTests.cs`、`tools/agent-repro/cases.json` | 堆叠守恒、候选扫描、规格/策略拒绝、整理、真实容量关闭→自主出口、第二角色不受影响及两种选择器回归 |

容量不足定义为：真实搜索已结束，至少一个候选本身适配该背包，当前布局及正式自动整理后都没有可用位置或堆叠余量。单件尺寸超过空背包、网格策略拒绝单独返回不兼容，不能假报满包。整理只使用已有启发式算法，不声称解决任意矩形装箱；不会删除放不下的物品。角色的容量撤离意图保留到本局结束，新的显式手动指令仍可覆盖自动选择，反击优先级不变。

首次编译 `20260911-215956-152` / `20260911-220007-405` 失败：Ledger.Amount 返回 long，驱动局部变量误用 int，已修正。保留 Editor 的旧入口未处理编译失败拒绝进入 Play Mode，导致 entering 等待；Extend `SceneRaidEditorEntry.cs` 对编译失败和进入取消写明确失败。为恢复这轮已经编译失败的会话，向隔离副本同步两处修正，不关闭 Editor；该轮源输入变化及失败证据照常保留，后续必须重新冻结输入复跑，不能把恢复动作记为成功回合。

恢复补充：旧程序集处于 armed/entering，后台未刷新修正文件。已取消外部等待器，PID 23544 留给用户关闭，未伪造完成文件。后续使用独立 `AnomalySearchScene` 工作区，复制已退出的 NUnit 工作区的 Library 缓存；新图形 Editor 继续常驻复用。新增进入拒绝检查已经通过实际编译，旧卡住进程并未因此被宣称恢复。

`20260911-220159-381` 15 项中 14 项通过，碎片化夹具的 3×2 物品可旋转成 2×3 放入，测试预设错误。修正夹具为 3×3 后，`20260911-220327-759` 全部 15 项通过（背包 9、目标决策 6），没有放宽生产规则。

评估和堆叠规则各自独立文件，Agent 黑板只保存本局撤离意图。自动化合法关闭会话后继续运行，未调用撤离指令；源剩余由正式箱子回调保存。特殊格不通过旧整理入口清除，单件超规格和网格策略拒绝没有冒充容量不足。

### P3d-1 追击目的地和粒子初始化

实测 AnchorSentinel 根节点 y≈3，居中 Capsule 高约 6，脚下 NavMesh y≈0；追击把根坐标交给半径约 1 的采样，反复 Unreachable。TidalAberration 还带 NavMeshAgent baseOffset。不能扩大所有导航查询的容差，否则可能把真正的高台断路当可达。

- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentCombatNavigationTarget.cs`：只解析敌人的地表追击位置；导航实体扣除实际缩放后的 baseOffset，静态实体优先脚底，必要时在身体覆盖的高度内作限制水平偏移的 NavMesh 采样。目的地仍交给原完整路径检查。
- Extend `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveValidationService.cs`、`Targeting/AgentTargetCandidateCollector.cs`、`AI/Actions/EngageEnemyActionNode.cs`：接受、发现和追击共用该解析，射程/视线/枪口继续使用真实身体瞄准点，跨高低差射击规则不变。
- Extend `Assets/Art/VFX/ZombieTentacleCorrosionVfx.cs`：新建粒子后先 StopEmittingAndClear，再设置 duration，配置完成后 Play；不删除或关闭视觉效果。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCombatTests.cs`：同地面、偏高根节点的实际追击/伤害，断开的高台仍拒绝，腐蚀粒子初始化无断言且仍播放。Extend `tools/agent-repro/cases.json` 收录该组。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs`：敌人快照增加地表追击目的地，避免只有身体位置无法定位。

必要回归：新组、既有 Navigation 和跨高度远程组；原场景 4 倍速复跑验证，不调整 HP、装备、敌人数量或射程。

构造红灯 `20260911-220647-153`：偏高根追击被错误拒绝，粒子 duration 触发断言，断开高台正确拒绝。修复后 `20260911-221008-896` SceneCombat 3 项和 RangedSpatial 12 项全部通过，`20260911-221156-191` Navigation 8 项全部通过。共 23 项，不把曾误填的不存在 NavigationTests 过滤器算作覆盖。

原场景 `20260911-220657-484` 首次两人自主撤离并结算，63.72 秒墙钟、169.85 秒游戏时间、11 次背包会话，仍有 3 条粒子断言、1 次 Unreachable、1 次 Superseded 拒绝。修复后 `20260911-221156-196` 运行 180.80 秒墙钟、203.87 秒游戏时间、12 次会话，0 运行时错误、0 Unreachable、3 次 Superseded 拒绝。Agent 2 已撤离，Agent 1 撤离中死亡，missionFailed=true、timeScale=0，测试器未识别失败终态而等到期限。该轮证据 PASS，玩法 ISSUES_OBSERVED，不能算完整回合通过。

后续发现：旧图腾仍出现在正式掉落中，却被运行时数据库排除；首个完成回合的仓库出现同格记录重叠和缺定义警告。P3d 后续必须验证真实存档回读、数量和布局，不能只凭 RaidFlow 的 settled 集合宣布结算正确。

### P3d-2 指令交接和失败终态小规划

- Extend `Assets/Scripts/Gameplay/Agent/Runtime/AgentManualDirectiveLock.cs`：高优先级请求尚在黑板时继续持锁，生命周期控制器统一完成、清理并恢复撤离；不允许发现器在敌人死亡但 Lifecycle 尚未 Tick 的同帧窗口插入新请求。Reuse `AgentDirectiveLifecycleController.cs` 的完成/恢复职责，不改变指令优先级。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/DirectiveLifecycleTests.cs`：构造撤离→受击→销毁敌人→生命周期 Tick 前发现器检查，先复现提前释放，再检查 Tick 后恢复原撤离。必要回归 Lifecycle、F1。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs`：读取已有 snapshot.missionFailed，尽快以 BEHAVIOR_BLOCKED 结束并明确记录任务失败，停止在正式失败暂停中空等。成功判据仍保留仓库/覆盖待验收状态，不把正常死亡直接认定为代码错误。
- Extend `tools/agent-repro/cases.json` 登记构造，原场景复跑验证拒绝次数和终态。死亡轨迹显示真实受击 107→85→71→57→39→25→2→0，反击后确实恢复撤离；是否有额外战斗缺陷需要进一步证据，禁止通过改血量或消除伤害掩盖。

构造红灯 `20260911-221807-649` 在生命周期 Tick 前持锁断言失败。修复后 `20260911-221901-079` Lifecycle 5 项、F1 2 项全部通过。

原场景 `20260911-221944-910` 76.72 秒墙钟、215.87 秒游戏时间、12 次会话，两名角色自主撤离，0 运行时错误、0 Superseded 拒绝。另有 1 次 Search/TargetCompleted、2 次动态反击追击 Unreachable，继续保留玩法问题，不能把这三项归为已修复的交接竞争。该轮未死亡，失败终态提前结束分支尚需单独构造验证。

### P3d-3 正式掉落的存档兼容小规划

真实引用为 `Assets/Resources/Loot/SO_SceneResourceLootRuleSet.asset` → `Assets/SO/ItemData/Table/equip_totem_green.asset`、`equip_totem_blue.asset`、`equip_totem_gold.asset`；`LootBox_1.prefab` 和相应 World Prefab 也引用这些资产。旧资产在 e448564 被排除出数据库，但掉落未迁移。保留现有掉落内容、权重、价格和属性，不用换掉落来掩盖结算丢失。

- Extend 上述三个具体 ItemData asset：恢复 IncludeInRuntimeDatabase=1，IncludeInTotemShop 仍为 0。Extend `Assets/Resources/Inventory/InventoryItemDatabase.asset` 添加它们的真实 GUID；既有数据库构建器会依据该标记重建，不新增全局动态注册器。
- Extend `Assets/Scripts/Gameplay/Backpack/PlayerStorageService.cs`：批量追加前验证每个 ItemID 能回解到原定义，不允许不可持久化输入部分写入。复用原 BuildPageLayoutModel，已有记录未知、越界或重叠时不把该页当空页，保留原记录，改用后续正常页面；同时应用原格子状态。仍由该服务拥有存档和仓库布局，不向 RaidFlow 复制装箱算法。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidStorageTests.cs`：所有正式掉落定义真实写盘→新服务重载→数量和布局核对；批量含未知定义不能部分追加；未知/重叠旧页不得继续覆盖，特殊格必须保留。该文件独立承担持久化契约，避免把存档夹具堆入 UI 会话测试。Extend `tools/agent-repro/cases.json` 登记组。
- 后续 P3d-4 单独处理保存失败时的事务和 RaidFlow 完成顺序，补齐整局仓库契约。P3d-3 不宣称磁盘故障、任意损坏历史存档或仓库 UI 编辑都已恢复。

红灯 `20260911-222259-395`：五项全部失败，分别证实三种旧图腾不可回解、未知输入被错误接收、未知/重叠旧页继续覆盖、特殊格被忽略。修复后 `20260911-222420-091` 五项全部通过；正式掉落表共 27 个唯一物品定义均可真实写盘和新服务回读，数量、身份和布局通过。三个旧图腾的价格、属性和商店排除标记未变。

### P3d-2b 已完成资源和瞬时失败探针

原场景 53.18 秒箱子取空并关闭，下一帧群标记完成，生命周期却把当前 Search 判为 Failed/TargetCompleted。Extend `AgentDirectiveLifecycleController.cs`：已接受 Search 的 TargetCompleted 是正常完成，失效/禁用仍失败；新提交到已完成目标的请求仍由验证器拒绝。Extend `DirectiveLifecycleTests.cs` 构造当前群完成、新请求拒绝的区别。

两次动态追击只持续一帧，定时快照未覆盖。Extend `SceneRaidReadModel.cs`：按反馈中的请求采集角色和敌人，不使用已被清除的新 Active 请求；增加敌人导航状态、baseOffset、nextPosition、身体范围。Extend `SceneRaidObserver.cs` 在接战接受和失败/拒绝时调用只读探针，独立记录 diagnostic.directive；Extend `SceneRaidRunController.cs` 连接读模型委托，探针异常导致 HARNESS_FAILED，不能向 Gameplay 事件回调抛异常改变执行顺序。继续 Automation → Gameplay，禁止探针移动、下指令或修正状态。

`20260911-222622-431` 先复现正常搜索被报 Failed；修复后 `20260911-222747-407` Lifecycle 6、F1 2、SceneCombat 4，共 12 项通过，包括生命周期清理后仍按反馈请求保留敌人身份的只读探针构造。

原场景 `20260911-222841-457`：78.34 秒墙钟、220.69 秒游戏时间，12 次背包会话，两人自主撤离，0 运行时错误、0 指令失败/拒绝、0 停滞疑点。离线核对两人最后背包合计 20 件物品和隔离仓库逐 ItemID 数量完全一致，含 4 个旧绿图腾；没有未知定义、越界或重叠。测试未下达目标/撤离命令。动态追击错误本轮未再次出现，不能认定已定位根因；报告仍为 NOT_FULL_RAID_VALIDATED，因为覆盖/仓库契约尚未集成进每次运行。71 条告警主要为重复目标 ID、出生点 NavMesh 采样、TerrainCollider，另有 Ledger 递归 DTO 的序列化深度警告，后续分项治理。

### P3d-4 保存失败和撤离提交顺序小规划

源码显示 RaidFlow 先标记已撤离，再调用 void 结算，即使仓库追加返回 false 仍销毁角色。必须以正式保存成功为提交前提。

- Extend `Assets/Scripts/Gameplay/Backpack/PlayerStorageService.cs`：批量追加使用当前角色仓库的临时副本，写盘成功后提交；IOException/权限失败返回 false，恢复内存和输出计数。Save 在同目录临时文件写入后原子替换，避免覆盖半份 JSON；文件操作仍归该服务，不引入 Gameplay → Automation 依赖。
- Extend `Assets/Scripts/Gameplay/Raid/RaidFlowController.cs`：结算改为私有 Try 方法，成功后才加入 extracted、销毁角色和完成任务。明确失败则保留背包和角色，走现有失败终态，说明物品未清除；不会重复入库。无 Inventory 组件的原白盒兼容仍按空背包处理，有组件却缺 Agent 快照不能假报成功。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidStorageTests.cs`：隔离存档锁定目标文件模拟真实写盘拒绝，检查旧文件、内存、数量保持不变，释放锁后重试只入库一次；构造正式背包带不可持久化物品的真实撤离计时，检查角色和物品保留，任务未成功。复用 Canvas prefab，不让测试写正式存档。
- 新增文件仅在已有 UI 夹具需要跨测试复用时提取到 `Assets/Scripts/Editor/AgentReproduction/World/InventoryFactory.cs`，负责正式 Canvas 实例和移除无关 RaidFlow，原 `SceneRaidInventoryTests.cs` 改为调用该工厂。不把工厂放入生产代码。
- 回归 Storage、Inventory 和 Combined 的真实撤离用例，随后原场景验证库存→仓库回读。此阶段不实现断电恢复或任意损坏历史存档迁移。

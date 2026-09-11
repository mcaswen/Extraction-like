# P3f：共享资源耗尽后的移动节点错误终止

## 现场与职责判断

最终矩阵第二版槽位 9，`Logs/SceneRaid/20260912-060139-446`，种子 1731。98.0046 游戏秒，Actor2 的自主 Search 在奇点塔 ResourceCluster_B 报 InvalidTarget；同帧 Actor1 对该群正常 Completed。错误堆栈明确来自 `MoveToTargetActionNode.Tick` 的目标位置解析失败分支，非导航 Motor，也不是战死。

`AgentDirectiveLifecycleController.Tick` 先读群缓存状态；`ResourceClusterAuthoring.TryGetNearestReachableIncompleteResource` 才从实际箱内状态刷新成员和聚合完成状态。最后一箱被取空但聚合尚未刷新时，前者仍认为任务有效，后者找不到剩余目标且更新为完成。SearchResourceActionNode 已正确处理这种完成，MoveToTargetActionNode 却统一发布 InvalidTarget。

## 小规划和具体文件

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ResourceCommandCompletionTests.cs` + meta：单独测试资源数据刷新与命令执行的时序，不承担 UI 或新导航算法。复用 `TestNavMeshBuilder.cs`、`AgentFactory.cs`、`TargetFactory.cs`、`RuntimeWait.cs`。先走正式 Dispatcher 和 Lifecycle，然后在构造边界消耗 WorldLootItem，模拟已观察到的真实箱内耗尽；同帧执行真实移动节点，断言一次 Completed、无 Failed、解除任务后新正式任务继续移动。对照仍有其他成员、资源仍存在但不可达，覆盖 1×/4×。不修改真实场景脚本。
- Extend `tools/agent-repro/cases.json` 登记六项精确参数。先运行原代码获取红灯，不能先把报告放宽。
- Extend `Assets/Scripts/Gameplay/Agent/AI/Actions/MoveToTargetActionNode.cs`：仅当资源解析失败后，正式 ValidateTarget 已确认 Search 的 TargetCompleted 时，以原 Finish 入口完成旧任务，停止本帧序列继续消费已释放指令。其余无效/不可达结果保持失败。不在 Lifecycle 强制逐帧刷新所有资源，不复制成员聚合规则，不新增公共接口。

依赖保持 Action → 既有 Validation / 生命周期；测试 → Gameplay。没有新的状态所有者、事件或场景修改。此修复恢复原有“他人搜完共享资源也正常完成”的规则，不改变玩法和战死语义。

## 测试和后续

构造红绿后跑原 Inventory 11 项、相关导航执行 11 项，覆盖旧开包改令、双人共享、库存初始化、空间不可达和有界恢复。再按原 MC01-R / 1731 实局复跑，检查原始失败、终态和仓库。第二版九局保留为失效版本记录，不覆盖历史；最后重新冻结源文件执行全部 11 槽位和共享导航的 SC02 回归。完成后回写架构审查再提交。

## 结果

待构造红灯。

红灯 `Logs/AgentReproduction/20260912-060731-087`：6 项中 4 通过，耗尽资源的 1×/4× 两项失败；群已聚合为 completed=True，但实际发出 Failed:InvalidTarget，没有 Completed。仍有成员和实际不可达四项对照原本通过。按小规划在移动节点的解析失败分支增加正式 TargetCompleted 复核，原生命周期 Finish 清理并返回 Running，避免本帧搜索子节点继续使用已清除的指令。未改其他失败判定。

绿色构造 `Logs/AgentReproduction/20260912-060835-208`：28/28，包含 ResourceCommandCompletion 6、ClusterCommandInventory 11、NavigationExecution 11。按 `cases.json` 与 XML 完整参数名称再次核对，三组零缺失。耗尽分支只完成一次，新命令实际行走；未耗尽或无法到达没有被误完成。额外验证只针对受影响分组，没有跑全项目测试。

原 MC01-R / 1731 真实复跑 `Logs/SceneRaid/20260912-061033-040`：证据 PASS，EXPECTED_DEATH，三个步骤 COMPLETE，原始失败/运行错误/停滞均为 0，正常死亡和幸存者仓库结算通过。没有更换种子、目标选择或生命数值。真实回合证明原流程可正常收尾，精确刷新边界由前述红绿构造证明。

实现只在已发生的解析失败后增加一次 O(1) 正式目标校验；没有每帧遍历群成员或新增寻路。现有 SetAggregatedState 通知只刷新 Zone 聚合，不在本调用中提交其他命令，仍通过原 Finish 身份检查收尾。完成后保留第二版矩阵九局，再冻结完整最终验收。

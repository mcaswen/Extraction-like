# 实施架构审查

## P1 玩家任务恢复

- 职责仍属于 `AgentDirectiveLifecycleController.cs`，没有增加第二个任务恢复所有者或在 Combat/Discovery 内重发指令。保存量始终至多一份，执行请求保留原身份。
- Submit 先校验新令再清除挂起，拒绝不会丢旧任务。持续伤害优先保持当前反击；恢复通过原 Validation 和 Activate，黑板事实、导航重置和存储仍走同一入口。
- 新令、取消、死亡清除挂起任务并发布一次 Cancelled；恢复前清除挂起引用，旧反击回调仍由 CommandId 拒绝。通用只读状态是补充诊断能力，原 SuspendedExtraction 视图保留旧语义。
- 不新增 Update、扫描或逐帧分配，仅提交/终态多一次布尔判断与事件。没有 Gameplay → Editor/Automation 的依赖。
- 新测试独立拥有复现场景，复用正式 Dispatcher、伤害入口和既有工厂；实际移动测试不伪造反击完成。13 项新测试、45 项相邻回归均通过，红绿日志见 p1_execution.md，P1 审查完成。

## P2 同群接战

待实现审查。

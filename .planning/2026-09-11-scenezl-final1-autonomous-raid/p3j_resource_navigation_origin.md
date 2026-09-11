# P3j：资源路径起点与实际 NavMeshAgent 不一致

## 证据与目标

最终矩阵 `20260912-012842-945` 两人撤离、仓库正确，但 Agent 1 在 9.20 墙钟秒收到 Search/Unreachable。不是死亡问题。探针显示角色位置 `(217.944, 3.342, -37.327)`、正常速度约 8、目标为员工食堂 ResourceCluster_B；失败回调后用 NavMeshAgent.CalculatePath 对上次目的地仍得到完整路径，首角点高度 0.342，角色 nextPosition 高度 3.342。

源码中 ResourceCluster 的正式候选查询使用 `NavMesh.CalculatePath(nextPosition, ...)`，调试查询及移动使用 `NavMeshAgent.CalculatePath(...)`。怀疑静态查询把带 baseOffset/缩放的可视起点重新投射，和已绑定的 Agent 实际地面多边形不一致。当前仅为有证据的假设，先构造偏移/缩放和断开区域对照，不能因一次成功复跑就判已修复。

## 文件归属

- **Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidResourceNavigationTests.cs` + meta**：用真实 NavMesh、Agent Prefab、ResourceCluster 构造高度偏移的可达资源和真正断开的资源，比较资源解析与实际 Agent 路径；必要时用实际场景位置作补充证据。测试独立于性能采样，允许在微夹具中设置测试 Transform，不向正式整局注入位置或指令。
- **Reuse `TestNavMeshBuilder.cs`、`AgentFactory.cs`、`TargetFactory.cs`、`ReproductionTestFixture.cs`**：原有图形/Play Mode、导航、目标和隔离能力。
- **Extend `Assets/Scripts/Gameplay/Targets/Authoring/ResourceClusterAuthoring.cs`（仅假设构造确认后）**：既有路径检查传递实际 NavMeshAgent，复用已有 NavMeshPath。就绪 Agent 使用自身已绑定的导航起点、类型和区域，未就绪分支保留原行为；调试与正式查询采用同一语义。候选顺序、范围、缓存、目标策略不改变，不引入 Targets→Agent 依赖。
- **Extend `tools/agent-repro/cases.json`**：登记定向测试。
- **Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidObserver.cs`**：仅 Failed/Rejected 时附带托管调用栈，区分资源解析失败和 Motor 的 SetPath 失败；不在普通帧采样栈、不修改指令或导航。
- **Extend 本目录 `task_plan.md`、`architecture_review.md`、P6 报告**：记录红/绿证据、修改后的 Gameplay 哈希和重新验证范围。

## 实施与验证

先跑定向构造，若未复现则补原场景精确位置，保留各次失败/假设。确认后实施最小语义修复，回归资源选择、缓存失效、真实不可达和相关导航用例。原矩阵问题轮不删除、不算通过。Gameplay 变更后重新冻结最终矩阵，死亡仍是正常结果；不通过放宽失败门槛或重复到绿色来完成验收。

## 结果

`20260912-013302-109`：6 项中 5 通过，baseOffset=6 的可达资源确定失败；同一 Agent.CalculatePath 返回完整路径。0/1/3 偏移和真实断开区域对照正常。增加实际场景、记录位置后，`013443-413` 仍只在偏移 6 失败；`013715-955` 补齐日志中两个已完成箱子的状态，实际位置用例通过。因此已确认高偏移资源查询缺陷，但不能声称它就是 012842 单帧失败的唯一根因。

按已确认的查询语义修复高偏移缺陷，保持路径缓冲复用。另补失败调用栈，以便后续若再发生单帧失败能明确区分解析/移动分支；原场景瞬时 Unreachable 仍标为未独立复现，不会用该构造冒充完全复现。

修复后 `013859-648` 34 项中 33 通过，7 项新导航构造全绿。唯一失败是旧 `ResourceDisplacementTests.cs`：它仅打开普通背包，没有真实资源会话，却期望关闭后完成搜索，已不符合此前确认并实现的 SourceObject/AgentId 会话归属。**Extend 该测试文件**，复用 `InventoryFactory.cs` 创建真实界面、正式焦点和 LootBox.Interact 打开会话；按到达/离开事实作有界等待，保留位移后不完成、返回后新会话才能完成的原断言，不放宽 Gameplay。R5 组标记需要图形运行，随后定向回归资源导航、R5 和背包会话。

`20260912-014259-772` 定向复核 **18/18 通过**：7 项资源导航、2 项 R5、9 项真实背包；正常退出，源哈希未变。结合前轮已通过的 11 项范围/缓存、8 项导航、6 项交战，没有发现本次查询语义修复引入新的逻辑失败。每次完整路径仍复用群内 NavMeshPath，没有恢复逐候选分配，候选数和查询次数边界保留。

高偏移修复、过期夹具修正和调用栈探针完成实施/审查，准备提交。Gameplay 已变化，最终矩阵全部重新冻结执行；此前绿色回合转为历史证据，瞬时 Unreachable 与 NoProgress 的独立因果仍未确认。

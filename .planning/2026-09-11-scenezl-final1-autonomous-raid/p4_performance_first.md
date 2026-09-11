# 优先调整：先治理三个 Update 热点

2026-09-11，用户明确要求先提高真实性能和测试吞吐，再继续自主闭环。已完成的 P2 背包入口、驱动及四项构造验证保留；当前 SC02 只收尾已有有界运行，不追加慢速长回合。

新的执行顺序：**P4a 目标发现 → P4b Pawn/导航执行 → P4c Zone 范围更新 → 相同 SC01 图形对比 → 恢复 P2/P3 搜打撤和后续验收**。每项单独记录实现、必要测试、审查和提交。主规划的画质、空间约束、受击响应、完整路径、实际库存和自主撤离要求不变。提高 timeScale 不能代替降耗。

## P4a：减少无效发现查询

问题：CollectWorldTargets 对每个资源群先执行全部成员路径查询，之后才以选中成员距离判断范围；Discovery 还收集未使用的 EnemySource 和尚未需要的远出口，资源被二次寻路确认。源码路径和 P1 数据说明应先减少这些查询，暂不引入新的扫描状态机。

| 决策 | 具体文件 | 职责 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Targets/Authoring/ResourceClusterAuthoring.cs` | 在成员层按真实三维范围预筛，再查询可达性；保留原无范围重载供动作执行；记录实际资源路径计算次数，范围外不得执行路径查询 |
| Extend | `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs` | 明确区分普通资源/来源/出口收集，Decision 默认仍得到完整候选；资源调用带范围重载，范围内未达成员不阻止其他合法成员 |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs` | 不收集策略未使用的 EnemySource，只有敌人/资源均无可执行候选才查询出口；原距离比较和当前资源保持规则不变 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidPerformanceTests.cs` | 构造远群近成员、范围外大量资源、不可达近成员/合法后备，比较选择结果并断言真实查询次数；后续承接三个热点的相邻性能契约 |
| Extend | `tools/agent-repro/cases.json` | 添加定向 ScenePerformance 组，继续复用 Decision 的 5 项语义回归 |

依赖仍为 Discovery/Decision → CandidateCollector → Target/Navigation。暂时保留资源最终导航确认，避免没有统一 Agent 类型/高度约束就删除验证。若范围预筛后仍有超预算单次查询，再进入已批准的预算/缓存方案，不能仅把大卡顿挪到下一帧。

## P4b：实际移动查询与每帧监视分开

Reuse `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationResult.cs`；Extend `AgentNavigationQuery.cs`、`AgentNavigationMotor.cs`（同目录），以及证据明确需要的 `Assets/Scripts/Gameplay/Agent/AI/Actions/SearchResourceActionNode.cs`。Query 负责路径事实和可复用查询缓冲，Motor 保存每 Pawn 的重算截止时间、路径有效性、到达/进展监视。目标变化、导航失效、过期或必要的动态重算立即/有界刷新；生命、受击、冷却、Brain 不降频。

实现前再细化路径所有权和失效条件，禁止共享一个可被覆盖的全局 NavMeshPath。已有 Navigation 8 项、R5 2 项及本次静态移动/动态断路/坡面构造是必要回归。采样确认身体事实不是主要成本时，不为了改 Pawn 文件而改动属性系统。

## P4c：状态和几何更新分开

### P4b 实现前细化

- `AgentNavigationQuery.cs` 内增加调用方持有的 Buffer（NavMeshPath 和可增长角点数组），使用 GetCornersNonAlloc；默认查询保留独立结果所有权，Motor 和 CandidateCollector 显式复用各自缓冲，返回的 Path 仅在该缓冲下次查询前有效。
- `AgentNavigationMotor.cs` 每 0.1 游戏秒查询一次，与原 SetPath 节奏相同；命令/目标、导航参数变化，丢失路径、路径陈旧/不完整立即失效。每帧继续检测导航可用、剩余距离/到达和停滞，暂停不启动重算。公开实际查询次数供构造断言。此阶段保留原进展阈值，场景抖动根因归 P3。
- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentResourceNavigationResolver.cs`：每个行为节点独立持有的短期资源查询结果，不决定策略。0.1 秒到期、Agent 明显移动、目标群/导航器改变、选中资源移动/失活/完成立即查询。Extend `AgentActionNodeBase.cs` 和 `SearchResourceActionNode.cs`（`Assets/Scripts/Gameplay/Agent/AI/Actions/`）调用同一封装，保留现有行为树结构。
- Extend `ResourceClusterAuthoring.cs`：复用候选列表，但每次实际查询更新候选坐标，修正旧候选只依赖 resource 引用、仍保留首个 Agent ClosestPoint 的问题；不跨 Agent 缓存路径。
- Extend `SceneRaidPerformanceTests.cs`：同帧多次移动只算一次、两个 Motor 缓冲互不覆盖、目标变化立即重算、资源完成/移动失效。回归 Navigation、R5、Decision 和动态断路用例。

依赖保持动作节点 → 资源查询封装 → ResourceCluster；Motor → Query。无新的场景服务、全局路径缓存或 AI 策略变化。

Extend `Assets/Scripts/Gameplay/Targets/Authoring/TargetZoneAuthoring.cs`、必要的 `GameplayTargetClusterAuthoringBase.cs`（同目录）、`Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetShapeUtility.cs`。Zone 状态聚合继续响应业务变化；几何以实际源轮廓、Transform、Collider 和配置变化失效，复用点缓冲和正式投射算法，避免静态输入逐帧重新构形、投射和 SetPosition。动态范围变化必须及时更新，Gizmos 使用同一缓存，不能把 Update 成本移到 OnDrawGizmos。

静态不重建、成员移动/增删、Collider 变换/尺寸、保存重载/冷缓存都要验证；地表改变的失效方式在该阶段细化。保留画面配置，不通过关闭范围线或降低图形质量提速。

## 测量和剩余限制

每项先做程序化必要回归，再比较相同场景、1×、4K、同采样配置的 Update/子段耗时、GC 和连续帧。当前 P1 的 12 个计数器保持解释一致；新增局部计数单独标明。完整 120 FPS 验收仍要求真实完整回合和正常退出。

P1 原生 Editor 退出故障仍是独立未解问题，不能把超时回收算正常通过。允许先做可正常退出的定向 Play Mode 优化测试；收尾 SC02 的事实、背包结果和失败日志会保留。

## 实施结果

P4a 已实现成员范围预筛，Discovery 按需查询出口，Decision 默认完整候选保持不变。`ScenePerformance` 4/4 通过（`Logs/AgentReproduction/20260911-202129-746`），`Decision` 5/5 通过（`Logs/AgentReproduction/20260911-202217-623`），均正常退出且源工程输入未变化。64 个范围外资源连续扫描 10 次，实际资源路径计算为 0；无范围执行入口仍能取得远资源。图形耗时在三项治理后用同一个 SC01 比较，不将该次数断言当作 FPS 验收。

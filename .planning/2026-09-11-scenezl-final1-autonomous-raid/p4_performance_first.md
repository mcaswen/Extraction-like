# 优先调整：先治理三个 Update 热点

2026-09-11，用户明确要求先提高真实性能和测试吞吐，再继续自主闭环。已完成的 P2 背包入口、驱动及四项构造验证保留；当前 SC02 只收尾已有有界运行，不追加慢速长回合。

新的执行顺序：**P4a 目标发现 → P4b Pawn/导航执行 → P4c Zone 范围更新 → 相同 SC01 图形对比 → 恢复 P2/P3 搜打撤和后续验收**。每项单独记录实现、必要测试、审查和提交。主规划的画质、空间约束、受击响应、完整路径、实际库存和自主撤离要求不变。提高 timeScale 不能代替降耗。

后续调整：用户关闭 FFT 后指出 Cluster.LateUpdate 热点，并明确要求范围约每 0.05 秒更新一次，已进入 [P4d 小规划、实现和测量](p4d_cluster_lateupdate.md)。本文件下方 40.77 FPS 对比属于此前开启 FFT 的历史配置，不能直接拿来证明本次限频的收益。

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

### P4c 实现前细化

- Create `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRangeCache.cs`：每个 Zone/Cluster 独立持有的输入快照、未投射点和投射点缓存。几何输入/配置用准确比较，静态不重建凸包。保留未投射点用于地表重投射，避免高度累计偏移。0.5 游戏秒周期重投射补获动态地表，源轮廓变化当帧重建；公开强制刷新仍立即生效。该延迟只作用于显示轮廓，不改变碰撞/感知/导航。
- `GameplayTargetShapeUtility.cs` 增加可复用 HullBuffer，Zone/Cluster 独占 scratch 列表，避免动态轮廓的临时凸包列表 GC。
- `TargetZoneAuthoring.cs` 每帧聚合状态、获取真实子轮廓或 Collider 世界角点，缓存判断后才 Build/Project/Apply；LineRenderer 只在轮廓/样式变化时写回。Gizmos 复用相同缓存。计数器分别记录几何重建和地表投射，不能把定期投射隐藏成“零查询”。
- `GameplayTargetClusterAuthoringBase.cs` 对成员位置或 custom shape 使用相同缓存路径；在 LateUpdate 检查静态成员变更，保证手动移动 LootBox 后 Zone 源轮廓仍能变更，保留 completed 隐藏和冷缓存初始化。
- `SceneRaidPerformanceTests.cs` 增加静态 30 帧不重建/不重复写线、资源成员移动、注册/注销、Collider 位置/尺寸/旋转、地表抬升在 0.5 秒内重投射；复用 `TargetHierarchyRepairTests` 的保存重载/幂等覆盖。输入缓存只在 Targets 模块内共享，无 Gameplay.Agent 反向依赖。

## 测量和剩余限制

每项先做程序化必要回归，再比较相同场景、1×、4K、同采样配置的 Update/子段耗时、GC 和连续帧。当前 P1 的 12 个计数器保持解释一致；新增局部计数单独标明。完整 120 FPS 验收仍要求真实完整回合和正常退出。

P1 原生 Editor 退出故障仍是独立未解问题，不能把超时回收算正常通过。允许先做可正常退出的定向 Play Mode 优化测试；收尾 SC02 的事实、背包结果和失败日志会保留。

## 实施结果

P4c 已实现每 Owner 独立几何/投射缓存及 HullBuffer。静态成员逐帧只比较输入，成员/Collider 变动重新构形，地表在 0.25–0.5 游戏秒内错峰重投射，投射相同时不重复写线。公共 RefreshRangeShape 仍强制立即更新；运行时 Gizmos 只读正式轮廓。删除 `ActiveEnemyClusterAuthoring.cs` 已失效的 RefreshRangeEveryFrame 覆盖，Cluster 统一检测实际变化。冷缓存测试改为清空新的缓存对象，并先断言输入确实为空，避免旧字段反射造成假通过。

性能构造 8 项及层级/材质/冷缓存 7 项 **15/15 通过**（`Logs/AgentReproduction/20260911-203121-550`），真实图形模式、正常退出、源工程未变。首次误用 nographics 调用了既有 ReadPixels 渲染用例，原生渲染崩溃，故该次无完整 NUnit XML、判失败（`20260911-203006-500`）；已纠正测试启动方式，不作为游戏缓存缺陷或成功证据。

P4b 已实现独占且延迟创建的路径缓冲，移动按原 0.1 秒节奏实际重算，资源节点短期复用解析结果，资源候选坐标在真实查询时更新。首次测试暴露 Unity 原生路径不能在 MonoBehaviour 字段初始化期间创建，已修正为首次查询创建。随后 22 项中 21 项通过；R5 暴露暂停背包时不能直接把已到达结果改成 Moving，已恢复暂停时的到达/位移事实检测，暂停仍不提交移动。最后 R5 2/2 通过（`Logs/AgentReproduction/20260911-202729-655`）。其他 20 项通过证据位于 `20260911-202548-931`，含性能构造 6、Navigation 8、Decision 5、动态断路 1。均正常退出、源输入未变。同帧 101 次相同 Move 仅计算 1 次路径；两个 Motor 的 Path 不共享，资源完成/移动立即失效。

P4a 已实现成员范围预筛，Discovery 按需查询出口，Decision 默认完整候选保持不变。`ScenePerformance` 4/4 通过（`Logs/AgentReproduction/20260911-202129-746`），`Decision` 5/5 通过（`Logs/AgentReproduction/20260911-202217-623`），均正常退出且源工程输入未变化。64 个范围外资源连续扫描 10 次，实际资源路径计算为 0；无范围执行入口仍能取得远资源。图形耗时在三项治理后用同一个 SC01 比较，不将该次数断言当作 FPS 验收。

## 三项优化后的正式场景对比

后续源码复核补充了每段时间复杂度、剩余重复验证/线性检查/群内平方去重，以及整帧归因缺口，见 [复杂度和剩余耗时分析](p4_complexity_analysis.md)。首轮优化不等于这些路径已全部治理。

SC01 `20260911-203250-166`，提交 `8522bca`：真实普通图形 Editor，Scenezl_Final 1，seed 731，1×，3840×2160，High Fidelity，Profiler 录制关闭，局部 Recorder 保持 12 项。60.06 秒，2399 帧，运行时错误 0，指令失败 0，停滞探针 0；两 Agent 都自主到达同一个箱子的不同停靠点，等待背包，没有测试下达指令。此次只观察，不构成自然搜打撤完成。

以下取墙钟 10 秒之后的样本，与 P1 三次同配置（非 Profiler 录制）的每轮均值之中位数比较。原始三轮和本轮全时段、预热后分布都保存于 [p4_diagnostics.json](p4_diagnostics.json)，没有拿用户另一套 Profiler 截图的 Self 时间直接当均值比较。

| 指标 | 优化前三轮中位数 | 本轮 | 变化 |
| --- | ---: | ---: | ---: |
| Discovery Update，ms/帧 | 1.3593 | 0.1218 | -91.0% |
| 两个 Pawn Update 合计，ms/帧 | 0.4249 | 0.2923 | -31.2% |
| 八个 Zone Update 合计，ms/帧 | 3.3873 | 0.9218 | -72.8% |
| Navigation.Check，ms/帧 | 0.1830 | 0.0225 | -87.7% |
| 全帧 GC，KiB/帧 | 99.46 | 90.11 | -9.4% |
| 连续帧平均 FPS | 35.24 | 40.77 | +15.7% |

需要保留的限制：

- 各轮自主轨迹不同，当前两 Agent 稳定等待背包；均值是相同输入配置的运行诊断，不代表完全相同行为轨迹的微基准。查询次数、所有权、动态失效由独立构造断言验证。
- 全时段平均 40.43 FPS，p99 33.74 ms，启动最大 509.67 ms；10 秒之后 p99 32.74 ms。**未达到 >120 FPS**，三项降耗不等于完整 P4 验收通过。
- 仍有 Pawn.Facts 单帧 12.46 ms（frame 2016）、Zone.Shape 单帧 6.64 ms（frame 709）。当前 Marker 只能定位到子段，不能据此断言是某个属性算法、GC 或线程调度；下一轮若继续性能治理，需要补 CPU/GPU/GC 和子段证据，不能盲改属性系统。
- 原生 Editor 退出挂起仍复现，退出 Play Mode、卸载场景后 60 秒由启动器回收自己创建的 PID 37556，process exit=1，sourceUnchanged=true，整轮 evidenceStatus=FAIL。不能以 0 游戏错误覆盖环境失败。

本次优先级调整的三项实现、必要测试、审查和提交闭环已完成；大规划 P4 保持“首轮优化完成，最终性能验收未通过”。后续恢复 P3 的满包自主撤离、会话归属和完整自然回合，再根据真实整局瓶颈继续性能治理。用户不需要自行运行 Play Mode。

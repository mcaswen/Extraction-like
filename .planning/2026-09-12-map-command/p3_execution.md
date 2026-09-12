# P3 真实群路线执行

基线：`99dbb61`。状态：P3a 完成，进入 P3b。遵循已经确认的根路线/子指令分层，继续自主闭环，不新增架构审批点。

## 分步顺序

1. P3a：不可变请求、结果和群事实契约，有预算的入图规划，用构造图验证。
2. P3b：Binding 的真实群事实适配，出生结果，单群执行和原生命周期关联/恢复。
3. P3c：RouteController、Pawn/Router 接口组合，原子改令、等待/容量/终态，构造 Play Mode 验证。
4. P3d：补边界和依赖审查，提交后进入 P4 全入口迁移。真实 HUD 在 P5，整局在 P6。

## P3a 小规划：请求、事实和入图规划

读取大规划第 3/5/7/9 节、Agent 框架、原生命周期/手动锁/Pawn、群 DirectiveFactory、P1 寻路和导航查询。当前没有真实路线所有者，旧地图路径只是显示推测，不能复用成执行权威。

- Create `Assets/Scripts/Gameplay/Agent/Routes/AgentRouteRequest.cs`：不可变根请求、Player/Autonomous 来源、目标 nodeId 或原 TargetRef，独立根身份。P3b 将在 `Agent/Data/AgentDirectiveRouteContext.cs` 定义显式子步骤身份，由原 Directive 保存，避免低层 Commands/Data 反向依赖 Routes；不复用 PayloadId。
- Create `AgentRouteResult.cs`：根规划/接受/拒绝/终态和原因，关联 Agent/根请求；不发布全局事件或操作 UI。
- Create `IAgentRouteTargetResolver.cs`：Agent 所有的节点规范化、锚点/群状态及单群处理候选接口；同时定义只读事实值。后续 Binding 实现，不让 Routes 引用 Binding、Targets 作者组件或 View。
- Create `AgentRoutePlan.cs`：复制可执行节点序列、入图点、入图长度、图/成本/绑定版本，区别于包含游标的执行状态。独立文件原因是一次规划结果不持有执行进度，后续 State 不得反向修改此结果。
- Create `AgentRoutePlanner.cs`：一次规划任务固定图/成本/原点/候选锚点，分批查询入图距离，复用 `AgentNavigationSegmentQuery.cs` 和 `MapGraphPathfindingService.cs`。新入图选实际导航长度最小的可达群（同长稳定 ID）；选择独立于终点，选定后只在作者图内寻路，断图不换一个远处终点当入口。途中改令由 Controller 限定原当前边两端或当前处理群作为候选，不能直接跳任意群。节点序列包含尚需实际到达的入口，确保入口也是步骤。
- 输入图/成本快照固定，resolver 版本变化或取消不发布结果；没有完整路径时没有部分执行序列。每次 Advance 限制查询次数，查询缓存归任务。纯图 Dijkstra 复用既有 O(V²+E)，入图最多 N 次实际导航查询；不做全对扫描。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/AgentRoutePlanningTests.cs`，Extend `tools/agent-repro/cases.json`：最近入口与终点隔断、限定边两端、实际绕路长短、反向成本、同节点仍需到达、重复/错误候选、过期/取消/预算、不可变结果。构造导航委托只替换入图测量，正式 NavMesh 调用在后续真实场景/单群用例验证；不把此组当完整搜打撤通过。

目录新增 `.meta`；类型只依赖 Agent Data/Navigation 和 MapGraph Config/Runtime，生命周期和世界作者配置暂不修改。P3b 才接显式步骤身份，不将未接线接口冒称已经驱动 Agent。

验收：查询次数受预算控制、相同输入确定、断图/过期无部分结果、每条路线只使用允许连接且入口不受终点诱导。测试结束写结果和架构审查，提交后继续。


### P3a 实施结果

- `Logs/AgentReproduction/20260913-014755-242` **8/8 PASS**，正常退出，源输入未变。独立长度样例验证双向成本、最近入口不受终点影响、断图不能远处重入、改令候选限制和同节点实际到达步骤。
- 预算为每次至多指定次数的测量尝试，失败采样也计数；同长稳定 ID，不同步长结果一致。取消、测量过程中改绑定、完成后改绑定均不公开旧结果。结果节点序列复制并只读。
- 入图查询仅在请求任务中执行，复用 P1 导航和 Dijkstra，无 Update 全对查询、世界修改或第二个执行状态。测试反射仅用于访问内部测量注入和构造导航结果，生产代码无反射或测试依赖。
- 本步没有驱动真实 Pawn，也未改 HUD；后续 P3b/P3c 接实际群成员和原指令生命周期，真实导航/到达另行验证。

## P3b1 小规划：原生命周期保存路线步骤身份

- Create `Assets/Scripts/Gameplay/Agent/Data/AgentDirectiveRouteContext.cs`：只含根 ID、路线版本、节点、步骤索引及玩家来源标记。归低层 Data，Commands 不依赖 Routes，也不复制根路线序列。
- Extend `Agent/Data/AgentDirectiveRequest.cs`：两个构造及 With 复制接口保留上下文，旧调用默认无上下文；PayloadId 原义保持。
- Extend `Agent/Commands/AgentDirectiveLifecycleController.cs`：所有路线子任务都可以由有效伤害挂起；增加实例结果事件，沿用唯一 `_active`/`_suspendedDirective`。独立 `SubmitRouteStep` 只接受有效上下文，先验证新指令，再替换活动/挂起任务，失败保留原任务。低层普通自主请求不得覆盖正在执行的路线步骤；重复判据包括完整步骤身份。
- Extend `Agent/Runtime/AgentManualDirectiveLock.cs`：显式玩家步骤可识别，不要求沿用旧鼠标命令前缀；跨步骤根锁在 P3c 的只读路线接入后补齐。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/AgentRouteLifecycleTests.cs`，Extend `tools/agent-repro/cases.json`：使用真实 Pawn/NavMesh 和伤害入口，验证自主路线恢复、根/步骤复制、拒绝改令保留挂起、接受改令清理旧身份、旧回调无效、同伴完成被挂起敌人、实例事件不串 Agent。原 DirectiveLifecycle 组定向回归。

这一步只补生命周期契约，不加第二个挂起所有者；群就绪适配、通用 MoveTo 的 Brain 接线和单群 Executor 在后续 P3b 小步实施。结果事件处理方只记录状态，不能在生命周期发布中重入提交下一步骤。

## P3b2 小规划：真实群事实及处理候选适配

- Create `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteTargetResolver.cs`：实现 Routes 所有的只读接口，包装现有 Binding、Registry 和 DirectiveFactory。按直接对象身份规范化来源/活跃/成员，拒绝歧义 TargetId；不使用注册表的同名来源近邻猜测。只在解析新请求时按成员查归属，处理时查询当前群，不在 Update 全场搜对象。
- Extend `Assets/Scripts/Gameplay/Enemy/EnemySpawnPoint.cs`：记录 NotStarted/Spawning/Completed/Failed、失败原因和来源注册结果，保留 HasSpawned 的尝试语义和原 Spawn 返回值；不为测试删出生物或修改敌人配置。来源群必须所有出生点生成且成功注册后才可按存活数判断清空。
- Extend `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRegistry.cs`：原出生注册增加返回实际接收 Source 引用的重载，旧签名继续转发。出生点保存真实注册来源，Resolver 精确核对，避免仅凭注册到某个群的布尔值误认归属；不新增第二次按名称/距离查找。
- Extend `Assets/Scripts/Gameplay/Targets/Authoring/EnemySourceClusterAuthoring.cs`：暴露当前已配置/已解析的 Active 引用，不在只读查询中自动创建群。生成、注册和生命周期仍归原系统。
- Extend `Assets/Scripts/Gameplay/Targets/Authoring/ActiveEnemyClusterAuthoring.cs`：提供包括禁用/失活实体的存活计数，复用原成员集合。禁用的活敌人不能因为不在可执行候选列表中而算被击杀。
- Resolver 对资源复用聚合完成状态，对敌人来源同时读取出生结果及整群生命，对独立活跃群保留未注册/已注册区别，对撤离仅给候选，不执行 presence/结算。Source 的处理候选委托原 Active 群的 DirectiveFactory，避免重复写敌人候选扫描算法。
- 版本负责配置、绑定和锚点，成员死亡不触发全图重算。读取锚点时检测变化，另提供有预算的锚点刷新供安装器调度；规划接受前再次确认当前路线节点，旧结果失效。此责任是适配器内部世界输入观察，不回写 SO。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphRouteTargetTests.cs`，Extend `tools/agent-repro/cases.json`：真实出生缺配置/缺健康/未注册、尚未 Start、成功生成/注册/死亡，来源与活跃同节点，重复 TargetId 的具体成员、禁用活敌人不算完成、资源/撤离候选、锚点变化/禁用/丢失的区分。出生资产配置定向回归，不全项目重跑。

验收为只读事实可定位、无凭空出生/清群/取物，以及候选继续经过原验证服务。新 Resolver 归 Binding 适配，Routes 不反向引用此类。执行器后续消费这些事实，不能根据单个 Engage 结果自行断言群完成。

P3b2 构造发现 `EnemyHealthController.IsAlive` 本义为“可作为战斗目标”，包含 enabled/active 条件，不能用作死亡事实。Extend `Assets/Scripts/Gameplay/Enemy/EnemyHealthController.cs` 增加 `HasLivingHealth`，保留 IsAlive 原筛选语义；Extend `ActiveEnemyClusterAuthoring.cs` 的存活计数及成员完成刷新改读生命事实。否则禁用活敌人不仅会让新路线误判，也会使原聚合永久标记完成。测试增加等待聚合刷新后仍未完成，以及重新启用恢复候选，原战斗目标筛选保持。

## P3b3 小规划：实际到达群锚点的移动子指令

源码当前只有 `MoveTo + EnemySource` 的侦查状态，`MoveTo + Location` 虽能通过导航验证，却没有行为树驱动。因此先在原 Brain 工厂接通通用位置移动，再由 StepExecutor 包装；不能把资源/撤离群伪装成敌人来源来强行复用旧宏状态。

- Extend `Assets/Scripts/Gameplay/Agent/Data/AgentMacroStateId.cs`：尾部增加 Navigate，保留既有枚举数值。它表示具体位置移动，不保存路线。
- Extend `Agent/AI/Factories/AgentBrainStateFactory.cs`、`AgentBrainStateMachineFactory.cs`、`AgentBrainTransitionRules.cs`：复用原 MoveToTargetActionNode 创建 Navigate，入口只认当前有效 MoveTo/Location，所有原状态可在命令切换时进入，伤害后按原指令恢复。其他宏状态的既有优先级/感知规则保持。
- Extend `Agent/AI/Actions/MoveToTargetActionNode.cs`：新增默认关闭的到达完成选项，仅 Navigate 开启；原 Motor 返回 Arrived 后按本次 CommandId 完成生命周期。原搜索/撤离 Sequence 和来源侦查不提前清指令。停止距离复用 MoveStoppingDistance，测试记录实际 Transform/到锚点导航距离。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/AgentRouteMovementTests.cs`，Extend `tools/agent-repro/cases.json`：4× 真正移动至锚点、途中有效伤害及恢复、同宏状态改令不继续旧目标、到达只产生一次对应身份的完成。保留 1× 伤害恢复对照，复用既有原导航回归按影响选择。

没有新增导航马达或并行 Tick 行为树，Routes 仍只能提交具体动作，Brain 执行动作，生命周期拥有完成事实。随后 P3b4 的单群执行器才能据此进入处理阶段。此步无新地图视觉，截图在 P5 新 HUD 接通时继续。


### P3b1 实施结果

- `015451-813` 首轮 1/5，原因是测试将移动点设为 None 类型，正式验证服务正确拒绝；改为 Location，补充前置接受断言。`015619-388` 路线生命周期 5/5 PASS。
- 审查发现新非法步骤入口未补齐目标 AgentId，已让拒绝也经原身份适配。`Logs/AgentReproduction/20260913-020053-245` 最终 **5/5 PASS**；`015910-382` 原生命周期 **6/6 PASS**。均正常退出，源输入未变。一次误写组名未启动测试，不计验证成绩。
- 真实 Pawn/导航/伤害验证自主路线子任务挂起及恢复，连续伤害不重复挂起，拒绝改令保留旧任务，接受改令清除旧挂起，旧完成回调无效，同伴清掉被挂起敌人仍返回正确步骤身份。实例结果事件不串 Agent。
- Context 归 Agent/Data，原 Request 的两种构造和 With 复制均保留字段；PayloadId 保持。Lifecycle 仍只有原有活动/挂起存储，未新增路线队列或第二份恢复数据。
- 本步没有补通用位置 MoveTo 的 Brain 执行或整群队列，测试没有冒称真实群路线已经完整运行。继续 P3b2 事实适配。


### P3b2 实施结果

- `020951-162` 测试枚举拼写编译失败，修为现有 EnemySource/ActiveEnemy；`021117-840` 7/9，暴露 IsAlive 混入可用性的真实问题，另一个为 NUnit 对 Unity 已销毁对象的 null 判定。生命事实拆分后 `021407-067` 8/9，剩余是此 EditMode 入口进入 Play Mode 后仍不支持 WaitForSeconds 的测试调度限制，改用有墙钟截止的逐帧等待。
- `Logs/AgentReproduction/20260913-021607-570` 最终 **10/10 PASS**：出生未就绪、缺 Prefab/健康/注册、实际注册错群、来源/活跃/成员规范化、重复 TargetId、禁用活敌人和失活子物体生命、锚点版本/预算、资源/敌人/撤离候选、无查询副作用。空可执行敌人列表不会把旧 Factory 的群对象兼容结果当成真实敌人提交。
- `021740-143` 真实 Scenezl_Final 1 配置 **2/2 PASS**，30 个出生点全部 Completed，保存的实际注册 Source 正确，30 名敌人/出生点数量保持。`022003-002` 受影响的敌人目标选择 **16/16 PASS**。最终共 28 项定向通过，均正常退出、源输入未变。
- `EnemyHealthController.HasLivingHealth` 提供死亡事实，原 IsAlive 仍筛选 enabled/active；Active 群计数和聚合完成改读生命事实，临时禁用不永久标记完成。生命/出生/归属事实各由原拥有者提供，Binding 适配不造敌人、不标记取物或结算。
- Resolver 只在新请求规范化时查成员归属，当前节点事实不全场扫描；锚点观察有预算、不查询 NavMesh，成员死亡不重算全图。未改场景或正式地图布局，无新增视觉状态。P3b3 继续通用位置移动。

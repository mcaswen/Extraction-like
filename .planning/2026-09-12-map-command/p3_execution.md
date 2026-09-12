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

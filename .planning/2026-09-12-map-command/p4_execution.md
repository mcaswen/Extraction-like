# P4 全入口统一和正式场景安装

状态：**P4a1 缓存完成，进入 P4a2 正式安装。** 延续已确认的大规划，不改变 Routes、Binding、Raid、View 的依赖方向。

### P4a2a 共享环境实施结果

- `MapGraphRouteEnvironmentService.cs` 拥有按 profile 共享的冻结图/成本/目标环境，全局每帧最多补 2 条已保存边（4 次有向查询），轮换 profile。锚点变化只失效相关已有边，导航重建暂停补算，未使用 profile 可释放；无 Update 全场扫描。
- Environment 冻结目标修订，新请求拒绝接受旧成本。当前路线只在环境变化时检查剩余路径，无关成本暂缺不会关背包、替换根或重发动作。审查补充 Resolver 身份检查：群绑定适配器替换即重规划，不能让旧步骤继续持有旧对象。
- 初轮 `034336-595` 6/6，补身份用例后 `Logs/AgentReproduction/20260913-035013-636` **7/7 PASS**；受影响的根执行 `035138-466` **18/18 PASS**，群事实 `035335-739` **10/10 PASS**。均正常退出、源输入不变，原始 JSON/XML 留在对应目录。
- 本步没有正式安装或新可见 HUD，不将这些构造结果算作 P4/P5/P6 验收。后续安装器仅负责组合和生命周期。

### P4a2b 场景安装实施结果

- `RaidMapCommandInstaller.cs` 在 SceneLoaded 对本场景层级一次定位正式图，重复安装复用已有实例；自动接入已注册/迟到的 Agent。Registry 提供集合变化事件，安装器早期 Tick 组合服务，按引用解除自己拥有的环境，旧安装器不能移除替换环境。
- 导航拥有者报告 pending、building 和真实完成修订，指纹只在初始化/修订变化时重新捕获。每 0.5 秒无分配比较 Agent 配置，确实变化才创建 profile，成本总预算不乘 Agent 数。原导航初次延迟协程补齐启停，防止重新启用后永久 pending。
- `035804-990` 初轮字段名误用导致编译失败，改为原契约 Reason。`035925-224` 正式安装断言通过，收尾先移除 NavMesh 再留敌人运行导致 TearDown 报错；改为正式卸载场景再清测试导航，未屏蔽日志、未修改敌人行为。`040141-089` 8/8，追加初次烘焙恢复用例后 `Logs/AgentReproduction/20260913-040358-507` **9/9 PASS**，正常退出、源输入不变。
- 真实 `Scenezl_Final 1.unity` 的 28 群绑定、两名 Agent、共享成本补验和卸载完成；此处只验证安装，P4b 全入口迁移、P5 HUD、P6 整局仍继续实施。

P3 已提交 `aec9964`。先实施 P4a1 可验证导航缓存，再接 P4a2 安装调度，避免把旧缓存假定有效。`MapGraphNavigationBakeBuilder.cs` 是实际最终烘焙提交文件；新增 `Editor/AgentReproduction/Tests/MapCommandNavigationCacheTests.cs` 验证同输入指纹、真实链接/网格变化、profile/锚点不匹配、缺签名补验、保存读回。正式 PlayerInputManager 路径为 `Assets/Scripts/Gameplay/Targets/Input/PlayerInputManager.cs`。

P4a1 审查：本项目 Unity 版本未公开 NavMeshData.agentTypeID，改读 Surface 的导航类型，实际查询仍使用完整 profile。NavMeshLink/OffMeshLink 的已提交原生端点也没有足够的只读公开事实，autoUpdate=false 时不能用组件 Transform 冒充原生链接状态。因此活动链接场景保留带 `live-links:` 标记的输入指纹用于诊断，运行时仍预算补验已有边；静态无链接场景可验证缓存后零补算。新增活动链接不得直接复用缓存的构造，不修改链接或生成假通路。

### P4a1 实施结果

- 首轮 `032306-059` 编译发现该 Unity 版本没有公开 NavMeshData.agentTypeID，按已有 Surface/profile API 修正。`Logs/AgentReproduction/20260913-032533-089` **6/6 PASS**：实际网格及链接配置变化、静态缓存零补算、缺签名/动态链接预算补验、profile/锚点不匹配、实际 Asset 保存/卸载/读回。
- `032707-569` 原成本/绑定 **11/11 PASS**；`032940-054` Editor 生成编排 **13/13 PASS**，均正常退出、源输入未变。采集增加独立 runtime 指纹，生成应用前比较该输入，最终 BakeBuilder 写入；原 Editor 场景/资产指纹不冒充 runtime 验证。
- 指纹捕获不产生导航移动/查询或修改链接，ProfilerMarker 可定位开销；只有生成/保存和后续安装/导航变更调用，没有新增 Update 全场扫描。原生三角形顺序变化保守失效，活动链接补已有边，均不创建假双向通路。
- 正式图资产尚未补新签名；P4 安装对旧资产预算补验已有 28 条边，后续正式保存时补签名。此步无新视觉，P5 接新 HUD 后继续截图。

## P4a 小规划：共享环境和正式安装

读取 P1/P2 成本/绑定/场景保存，P3 Controller/Environment、当前 Registry、Raid 初始化、RuntimeNavMeshSurfaceBuilder。当前只有构造测试显式注入环境；真实场景 Binding 已保存，但不会自行启动路线。运行时不能拿 SO 的烘焙指纹冒充当前世界的验证结果。

- Create `Assets/Scripts/Gameplay/Raid/RaidMapCommandInstaller.cs`：场景组合根，在 sceneLoaded 一次定位本场景的正式 Binding，安装到已注册 Agent，处理迟到注册/注销、地图和导航变更，卸载时解除订阅/终止所属任务。只在有正式图的场景启用，旧无图夹具保留兼容。构造可显式 Configure，不在 Binding 反向查找 Raid。图服务/世界 Resolver 共享，同导航配置的成本服务/环境共享；每帧仅预算补已保存边，不做全对生成。
- Extend `Agent/Runtime/AgentRuntimeRegistry.cs`：补注册集合变化事件，保持原焦点事件语义，供安装器和反馈订阅现有/迟到 Agent，不新增第二套 Agent 注册表。
- Extend `Gameplay/Raid/RuntimeNavMeshSurfaceBuilder.cs`：提供当前重建状态和修订，原拥有者在实际重建后推进修订；安装器据此失效成本。执行导航遇到变化仍由原 Motor 完整路径校验兜底。
- Create `Gameplay/MapGraph/Binding/MapGraphNavigationFingerprint.cs`：一次性读取当前导航三角形、区域及场景导航连接配置，形成运行时也可验证的导航指纹。生成/启动/真实导航变更时调用，禁止 Update 反复完整扫描。
- Extend `MapGraph/Config/MapGraphNavigationBakeData.cs`：追加运行时导航指纹，原 Editor 的场景/资产指纹保持。Extend `MapGraph/Binding/MapGraphNavigationCostService.cs`：允许用真实运行时指纹、profile 和逐边锚点/端点匹配来使用烘焙；缺少新指纹的旧资产以有预算的已保存边补验启动，不假定缓存有效。
- Extend `Editor/MapGraph/MapGraphSceneCollector.cs`、对应 `MapGraphSceneSnapshot.cs` 及最终烘焙提交处：采集一次运行时可验证指纹，随生成/保存写入烘焙；不把 AssetDatabase 带入运行时。正式资产在最后集成时补保存，用户场景/NavMesh 改动继续隔离。
- Extend `Agent/Routes/AgentRouteEnvironment.cs`/`AgentRouteController.cs`：冻结成本所对应的目标锚点修订，过期不接受；安装器观察锚点变化后立即失效受影响成本、有限补验再发布。环境版本和 costRevision 分开，服务重建不能以重复的数值版本骗过旧请求。
- Create `Editor/AgentReproduction/Tests/MapCommandInstallationTests.cs`：有图/无图模式、延迟出生/注销、共享成本和预算、静态缓存复用/缺签名补验、锚点/导航/图变化失效、卸载清理。定向回归 P1 Binding/成本相关用例。

## P4b 小规划：世界点击、自主候选、容量撤离

P4b 接口细化：Create `Assets/Scripts/Gameplay/Agent/Routes/AgentRouteDirectiveAdapter.cs`，集中兼容旧高层 Directive 到群根请求及返回状态的转换，Pawn 仅转发；内部 RouteContext 步骤和伤害指令继续走原 Lifecycle。Extend `Agent/Commands/AgentDirectiveResult.cs` 在枚举末尾增加 Planning，使旧返回类型能诚实表示“已排入规划，尚未接受”，不发布假的子指令接受事件。正式世界点击和自主组件直接使用根接口。

失败记忆由 `AgentRouteFailureMemory.cs` 保存，Controller 记录根结果；IAgentReadOnly 暴露可否自主选择该群的只读查询。候选过滤不删除可见敌人的风险事实，手动路线不读自主冷却。图/成本/目标上下文或 Agent 位置真实改变后允许重新尝试，容量退出不写失败冷却。

P4a2 接线细化：Registry 提供 AgentRegistered/AgentUnregistered 实例事件，安装器只标记集合变动，实际组合放在自己的早期 Tick；反馈只挂接订阅，不在注册回调里规划。Pawn 增加按预期环境引用解除组合的入口，避免旧安装器移除新安装器已替换的服务。Environment 冻结 Targets.Revision，规划接受前必须匹配；Resolver 的 Source 可用性同时观察已配置 Active 的启用状态，区分业务存活数和可用性变化。安装器发现目标修订变化时先检查已有边锚点，保留未移动边成本，仅补失效边；构造主动查询引起修订变化时也不能先接受旧锚点成本。

`RaidMapCommandInstaller` 通过 SceneLoaded 一次查找正式 Binding，默认执行顺序早于 Pawn/目标决策，按 profile 共用环境。导航指纹只在安装或显式导航重建时计算，RuntimeNavMeshSurfaceBuilder 增加 IsBuilding/HasPendingBuild/NavigationRevision，重建 pending 期间环境不就绪。动态执行失败通过实例根结果通知安装器失效当前边；MapGraph 的失效/补验预算独立，不由 UI 重绘触发。

P4a2 文件归属再细化：Create `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteEnvironmentService.cs` 独立拥有共享图/Resolver、按 profile 的成本及环境缓存、全局查询预算和失效发布；RaidMapCommandInstaller 仅处理场景/Registry 生命周期及注入，不堆入缓存调度。Create `Editor/AgentReproduction/Tests/MapRouteEnvironmentTests.cs` 先验证这一能力，再做安装集成。

P4a2b 审查修正归属：RuntimeNavMeshSurfaceBuilder 的初次延迟协程需要由自身启停管理，否则烘焙前 GameObject 停用会终止协程，重新启用后仍永久 pending。扩展同文件的 OnEnable/OnDisable 和初次工作引用，不把重启计时放入安装器；新增实际停用/恢复后自动烘焙用例。正式场景测试在基类清测试导航前先正式卸载本场景，避免故意移走导航却留下敌人继续 Update 的夹具错误。

环境变化须区分新请求和已有路线：新请求等待完整成本快照，已有路线若剩余边/目标/当前锚点仍有效则继续，不因无关边补验而关闭玩家背包或重启任务。Extend `AgentRouteController.cs` 在环境变化时只读验证剩余路径，仍有效则采用新环境；失效才走原有界重规划。该检查无 NavMesh 查询，仅在环境修订时运行。新增无关边失效不打断已接受路线/背包的构造，避免共享成本成为多 Agent 互相打断的来源。

- Extend `Targets/Input/AgentTargetCommandDispatcher.cs`：增加正式群路线入口，规范具体群 TargetRef，已完成群允许移动；焦点/指定 Agent 通过 Router。旧低层 out Directive 入口在无图场景保持兼容，在有图场景转发根路线且不再造单敌人整任务。更新实际 `PlayerInputManager.cs` 调用正式返回值，Planning 不冒称成功。
- Extend `Agent/Decision/AgentTargetDecisionController.cs`、`Agent/Runtime/AgentTargetDiscoveryController.cs`：保留候选发现范围、射线、风险和原容量选择，选定后提交 Autonomous 路线。有效根和待规划请求阻止重复自动刷新/覆盖；关闭决策模块不能清掉仍有效的根步骤。容量根结束后重新选择撤离，沿途资源免处理由原容量事实驱动。
- Extend `Agent/Core/AgentPawnRoot.cs`：正式安装场景的无来源高层群 Directive 不再直接执行，具体反击/内部带 RouteContext 的子动作维持原层次。兼容调用若表达群目标，规范转发根请求；无法表示的无来源请求明确拒绝，不能静默绕图。
- Create `Agent/Routes/AgentRouteFailureMemory.cs`：每 Agent 的有界根失败记录，目标身份按规范 nodeId。断图/缺通路在世界位置/图/成本/绑定改变前不反复规划，暂时无候选有冷却。手动请求不受自主失败记忆限制。向候选收集暴露只读可选性，避免原策略一直选择同一个根规划失败目标；不改变风险集合或扩大感知范围。
- Extend `Agent/Targeting/AgentTargetCandidateCollector.cs` 的最终可执行过滤，复用 Resolver 的规范身份和根失败记录接口。生产 Routes 不依赖 Targeting/Binding，查询由 ReadOnly 提供；原单敌人短期失败记忆保留。
- Create `Editor/AgentReproduction/Tests/MapCommandEntryTests.cs`：同目标世界/Router/自动入口的等价序列、玩家优先、两个自主组件、目标完成后恢复自主、满包自主选出口且余物保留、断图不循环发令、图变更恢复候选、低层不能绕图、感知约束保持。

## P4c 小规划：根反馈

- Extend `Targets/Presentation/AgentCommandFeedbackPresenter.cs`、`AgentCommandFeedbackText.cs`：订阅 Pawn 的实例根结果，注册/注销时同步订阅；只显示玩家根接受/拒绝/失败，Planning 和内部重规划/步骤切换不刷成功。复用原正式字体、淡入淡出和有界队列。
- Extend `AgentCommandRouter.cs` 的实例路由失败事件：仅承接不存在 Agent 的即时拒绝，解决没有 Pawn 可发布实例事件时的反馈；不是全局路线事件总线。世界/地图同一结果只入队一次。
- Create `Editor/AgentReproduction/Tests/MapCommandFeedbackTests.cs`：真实 Handler 一击一请求、Planning 无成功、Accepted 一次、失败原因、子步骤不刷屏、迟到 Agent/改焦点/注销、已修正“指令”文本保持。新地图实际视觉在 P5 截图。

## 验收和审查

每小步先实现、必要构造测试和 Review，再提交。P4 完成才将主场景称为统一执行入口；旧真实场景 MC 验证器的单敌人/低层 ID 假设不得拿来冒充新路线已验收，P6 使用新的 MR 脚本。每阶段记载测试模式、源输入是否变化和未覆盖分支。作者图决定路线，实际导航决定执行可达性，显示仍只读。

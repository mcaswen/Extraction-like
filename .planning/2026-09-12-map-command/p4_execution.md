# P4 全入口统一和正式场景安装

状态：小规划准备，P3 最终测试/提交后实施。延续已确认的大规划，不改变 Routes、Binding、Raid、View 的依赖方向。

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

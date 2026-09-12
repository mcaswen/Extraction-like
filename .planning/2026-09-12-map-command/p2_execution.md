# P2 场景采集、自动生成和编辑

基线：`8d76e38`。状态：P2a–P2d 完成。保留用户当前场景全部修改，不恢复旧值。沿用已确认大规划的横竖单段、真实群转向、连线决定路线、人工覆写保留和持续截图要求。

## P2a 小规划：真实场景快照和导航候选证据

先得到真实输入、锚点和双向可达矩阵，再实施联合拓扑/布局，避免在猜测的场景数据上调算法。此步不运行玩法、不出生敌人、不改变 Agent Transform，也不自动保存场景。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphSceneSnapshot.cs`：Editor 暂存的区域、规范静态群、世界范围、Prefab 来源、稳定 GlobalObjectId、成员候选、Agent profile 和输入指纹；数据与采集算法分开，场景引用仅存在 Editor 快照/场景 Binding，不进入 SO。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphSceneCollector.cs`：从已保存的当前场景采集 Zone/群和真实 Prefab 引用，按稳定场景身份排序，来源/已配置 Active 群合并为一个静态节点；记录未分区/归属冲突、空 Zone 和无效数据。采集 actual NavMeshSurface 数据资产/变换，Agent 导航参数通过已有快照 API 读取，不假造 live Agent。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphSceneNavigationScan.cs`：可分批/取消的候选采样和全对双向查询；每个工作项为一次锚点采样或一对双向查询。记录选择的成员/候选、采样失败、完整/Partial/单向、实际路程、profile、计数及耗时。复用 P1d 双向测量，不包含布局或资产保存。
- Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationSegmentQuery.cs`：公开同一采样/高度校验的只读 `TrySampleAnchor`，避免 Editor 重写采样判据或为了单纯采样先算一条零路径。
- Extend `Assets/Scripts/Gameplay/Targets/Authoring/ResourceClusterAuthoring.cs`：增加一个只读候选复制入口，包装现有私有 `FillResourceNavigationCandidates`，只对真实成员取碰撞体边缘/底部/外圈候选；不调用调试物体创建、资源状态刷新或取物。现有 geometry 归属保持，不另写一套箱子接近算法。
- Reuse `AgentNavigationProfile.cs`、`MapGraphNavigationCostService.cs`、原 Source/Extraction 成员配置和 Zone/Cluster.RangePoints。来源锚点优先真实出生点，撤离优先真实成员；高度不匹配明确失败，不用扩大采样半径跨层找点。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphSceneCollectionTests.cs`，Extend `tools/agent-repro/cases.json`：使用既有隔离 runner，但本组直接在 Edit Mode 开场景，保存/重开仅作用于构造副本；真实场景只读。检查 8 Zone/28 规范群、稳定身份、profile/Prefab/锚点、756 有向查询每 profile、重复采集稳定及预算/取消。原始证据写入本轮 Logs。

新建 `Editor/MapGraph` 目录和每个脚本同时补 `.meta`。新增 Snapshot/Scan 是大规划 SceneCollector/生成控制器的数据与导航调度职责细分，不改变 Editor → Gameplay 单向依赖。生产代码不依赖测试报告或场景审计工具。

本组采用独立的 Editor 测试 setup/teardown，验证隔离 company/product、恢复临时场景并写 CaseArtifactWriter；不继承会自动进入 Play Mode 的 ReproductionTestFixture，因为 GlobalObjectId 和场景作者配置应在 Edit Mode 读取。后续真实逐群执行仍按 4× Play Mode 验证。

## 后续子阶段

- P2b：基于采集证据实现布局初稿、联合连线/横竖约束求解、独立校验及评分，先形成可重复生成的全图。首次有可见布局时导出/查看 Editor 预览，密集区域和跨区连接调整后复拍。
- P2c：生成控制器、Undo/保存、画布拖动/端点/增删线和人工覆写合并，用序列化与固定拓扑反例验证编辑规则。Editor 使用与预览一致的画布绘制，真实 HUD 的最终自然程度留给 P5 完整画面复核。
- P2d：对当前真实场景保存正式图/绑定（仅在生成结果通过校验后），保留修改前后布局证据，不混入用户相机/Gizmos 改动提交。

## P2c1 小规划：可取消的编辑器生成任务

先接通窗口所需的完整计算任务，再实现 Undo 文档和画布。每个任务独占采集输入、扫描和求解进度；结果只读，计算不写 SO/场景。窗口后续替换任务时先取消旧任务，按请求身份和文档版本应用结果。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphGenerationController.cs`：组合现有 Collector → NavigationScan → ConnectionGenerator 或固定边集 Solver → 独立验收，提供工作项/短时间片双预算、阶段/计数/耗时、取消及结构化失败。采集和最终复核是不可再分的只读工作，分别记录耗时；不承诺每个原子工作都能被时间片抢占。每个 Advance 限制导航工作项，坐标继续逐轮推进。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphGenerationResult.cs`：复制有效草稿、采集快照、测量锚点/矩阵、请求 ID 和输入文档版本，供画布预览及后续事务消费；数据不拥有任务推进或资产写权限。
- Reuse `MapGraphSceneCollector.cs`、`MapGraphSceneNavigationScan.cs`、`MapGraphLayoutGenerator.cs`、`MapGraphConnectionGenerator.cs`、`MapGraphOrthogonalLayoutSolver.cs` 及独立校验器。固定拓扑重排直接使用原边集，不进入补边；场景删群、改归属、区域孤儿先返回同步诊断，不在普通重排中静默清理。保存前再采集当前场景和导航指纹，变化后旧结果失效；设置在任务开始复制，后续窗口参数编辑不影响正在计算的任务。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphGenerationControllerTests.cs`，Extend `tools/agent-repro/cases.json`：构造真实小 NavMesh，验证全部阶段、每次工作/导航预算、扫描中和求解中取消、失败不发布、场景/导航过期、设置冻结、请求身份和固定边集不变。内部采集委托只替换快照来源以构造过期情形，导航扫描和生产求解保持真实；另用当前场景执行完整 Controller，独立核对最终几何/导航、输入不变和分批耗时。

上述为已确认生成控制器的数据/调度职责细分，Editor → Gameplay 单向依赖不变。Undo/保存事务后续放入独立作者文档/事务文件，由 Controller/窗口调用；本步不把写资产混入算法，也不接 Agent 运行时。无新画布和视觉布局时不重复截图，P2c 画布首次可见后立即实际截图检查。

接入前审查发现旧场景指纹没有包含名称、群中心、Agent profile 和初始位置，而这些会影响标签或导航候选选择。Extend `MapGraphSceneCollector.cs`，在原指纹职责中补齐这些输入及层级/Prefab 来源；不在 Controller 另写一套场景比较。追加真实场景修改名称、Agent 位置/导航参数的只读采集反例，测试修改仅发生于隔离副本且不保存。

### P2c1 实施结果

- `232448-751` 生成控制器 **13/13 PASS**，无编译、行为、基础设施或源输入变化错误。覆盖采集/扫描/生成/验收各阶段、导航单批限制、设置冻结、三阶段取消、场景/导航失效、已完成预览再次检查、固定边集和手工样式保持、同步差异拒绝及真实场景。
- 真实场景完整任务为 7 Zone、28 群、28 条横竖边；400 个搜索状态、2,176 次坐标迭代、791 次真实导航查询，最终几何和各 profile 导航验收通过。3,523 个工作项合计 4,598.83 ms，Advance(64, 6 ms) 最长 **13.25 ms**，最长原子工作 13.20 ms。采集及复核合计 18.27 ms；时间片不抢占原子工作，因此没有声称严格不超过 6 ms。
- 图数据和实体未写回。新增指纹输入使当前场景指纹变为 `7d82f625e8f68e6f3e47c689c03905c1`，导航仍为 `17e80a2593b3808e59919818228226fd`；这是指纹规则补全，未更改用户场景。实际名称、Agent 初始位置和导航参数变化均能使指纹变化。
- 此步没有新画布，沿用已检查的第四张布局证据。P2c2 继续编辑文档/Undo/保存事务，之后接窗口和操作画布；当前不能通过 UI 编辑或使用正式地图指挥。

## P2c2a 小规划：临时作者文档和 Undo

先完成独立于窗口的编辑会话，再接正式资产/Binding 保存。直接复用现有 SO 作为临时工作副本，避免另一套节点/区域序列化格式；原资产只在后续明确保存入口写入。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditorDocument.cs`：拥有 HideAndDontSave 的 SO 工作副本、单调递增的编辑版本、当前生成请求和 Undo/Redo 刷新。新请求取消旧任务，完成结果必须同时匹配请求身份、文档版本和当前场景指纹；应用作为一个 Undo 操作。关闭只销毁自己持有的临时对象和任务。原资产序列化内容留作保存冲突基线，外部 Inspector 修改即使未递增 SO.Revision 也能识别。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphNavigationBakeBuilder.cs`：由已验收的全对矩阵构造当前选中边的正式烘焙，按作者边的 ID/端点方向重排两个方向的长度及锚点；明确选择一个 profile，不混用不同 profile 的锚点。SO 首版仍保存一个 profile，其他运行时 profile 复用 P1 成本服务重新查询。
- Extend `MapGraphGenerationResult.cs`/`MapGraphGenerationController.cs`：结果附带本次冻结的设置 JSON，作者文档应用该份设置；不从已被窗口修改的参数重新取值。Reuse `SO_MapGraphDefinition.cs`、`MapGraphLayoutDraft.cs` 和 Unity Undo；暂不新增通用历史系统。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorDocumentTests.cs`，Extend `tools/agent-repro/cases.json`：实际调用 Unity Undo/Redo，核对第一次生成可撤回为空、字段和烘焙恢复、Undo 后版本仍单调递增、旧请求不能覆盖新编辑、取消/关闭不改原资产、原资产外部修改冲突、方向相反的作者边烘焙和不可用方向拒绝。

文件仍在 Editor 模块，文档管理会话/事务身份，Controller 管计算，BakeBuilder 管数据适配。P2c2b 再实现经过验证的正式资产/场景 Binding 保存、失败回滚和重开；P2c3 接画布及拖动/连线操作。本步不提前修改正式场景绑定。

### P2c2a 实施结果

- `233812-549` 作者文档 **8/8 PASS**，实际调用 Unity Undo/Redo，第一次生成撤回后恢复空工作副本，重做恢复完整图和烘焙。连续名称/设置操作分别撤回、重做，文档版本始终递增，不随 Undo 回退。
- 旧请求被替换、生成后继续编辑、Undo、新场景输入等情况均不能覆盖当前文档。关闭会话停止其任务、移除自身 Undo 订阅并销毁临时对象；正式资产内容、磁盘重读结果和 dirty 状态保持。外部 Inspector 只改名称且 SO.Revision 不变时仍检测到冲突。
- 正式烘焙保留作者自定义边 ID，反向定义正确交换实测 12/17 的两个方向成本及世界锚点，旧显示长度 999 不参与计算。错误 profile、缺锚点、不可达方向、测量与绑定锚点不一致均拒绝。
- 生成结果附带冻结设置，应用前构造并校验烘焙，再以单个 Unity Undo 操作替换临时 SO。原资产尚未写回，保存后重开以及正式 Binding 的事务验证留在 P2c2b；本步没有新画布，不把这些测试当作可见编辑器完成。

## P2c2b 小规划：正式资产和场景绑定保存

- Create `Assets/Scripts/Editor/MapGraph/MapGraphAuthoringTransaction.cs`：保存前核对作者版本、原资产内容、当前场景/导航及全部绑定引用，复用 BakeBuilder；将 SO 和场景 Binding 的内容作为一个 Unity Undo 操作应用。只保存目标资产和其场景，磁盘失败保留具体错误并回滚本次内容，不操作其他场景。新资产路径必须为 Assets 下未占用的 .asset；不得覆盖其他文件。
- Extend `MapGraphEditorDocument.cs`：提供重新核对当前测量输入的保存入口，保存成功后更新来源基线，保留工作副本的 Undo 历史。Extend `MapGraphGenerationController.cs`/`MapGraphGenerationResult.cs` 增加只校验模式，打开已保存图后重新扫描而不移动布局或改变边；不以重排代替保存验证。
- Reuse `MapGraphBindingAuthoring.cs`、`MapGraphTargetBinding.cs`、`MapGraphZoneBinding.cs`、`SO_MapGraphDefinition.cs`；资产只含稳定 ID/值，真实对象仍只在场景 Binding。保存使用当前图内容和实测导航，不复制旧烘焙长度。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphAuthoringTransactionTests.cs`，Extend `tools/agent-repro/cases.json`：隔离场景保存/重开，核对目标/区域绑定、稳定 GUID、横竖布局、烘焙方向，验证外部修改冲突和失败不污染已有配置。保存事务的 Undo 恢复内存内容/绑定；磁盘持久化遵循 Unity 保存语义，Undo 后再次保存才更新文件。

此步骤完成后接入画布，再通过窗口生成当前场景正式资产。用户当前 Scene/NavMesh 改动继续保留，正式场景写入只增加本功能配置，不恢复旧场景版本。

### P2c2b 实施结果

- `20260912-234913-829` 真实场景副本保存测试 **2/2 PASS**：28 群、7 区域直接绑定，重复保存保持图资产 GUID，重新打开场景后布局、节点/边身份及名称一致；全部烘焙边直接复用，0 次补充导航查询。只校验模式的方向搜索和坐标迭代均为 0，原布局不变。
- 外部 Inspector 修改即使未递增业务版本，也会拒绝保存，原场景文件和既有绑定内容保持；非法资产路径在写入前拒绝。源资产已保存后再次写入会递增正式版本，成功后更新文档来源基线。
- `20260912-235138-425` 受影响的作者文档 **8/8 PASS**，Undo/Redo、请求版本、关闭隔离和方向烘焙仍通过。本阶段共 10 项定向测试通过，没有基础设施或源输入变化异常。故障恢复代码检查了内存 Undo 回滚和已写磁盘的备份恢复；磁盘 I/O 失败后的恢复分支尚未用故障注入自然触发，不扩大本轮覆盖口径。
- 正式目标场景尚未写入 Binding，本轮写入均在隔离测试副本。继续 P2c3 编辑操作/画布，随后 P2d 保存正式图。
- 用户于 2026-09-13 要求后续命令使用 Ubuntu，已切换到 WSL Ubuntu 26.04，项目为 `/mnt/d/Unity-Projects/Extraction-like`。安装原生 git-lfs 3.7.1，仓库本地 Git 换行/文件模式及提交身份沿用现有工作树口径；未修改工作文件以消除跨平台误报。Windows Unity 和既有验证适配器通过 WSL 互操作调用，源 Editor 继续保留。

## P2c3a 小规划：作者操作和约束预览

- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditOperations.cs`：纯数据生成节点移动、区域移动/缩放、锁定、加线/删线/端点重绑和样式操作的作者意图。显式拖动锁定新位置，区域移动携带成员；保留其他锁，删除边写禁连记录，重新手动加入时解除该对禁连。坐标/样式归 Config，不移动世界物体。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditOperation.cs`：持有一次编辑的原草稿、意图草稿、文档版本和有界 Solver；直接有效的样式/锁/删除操作立即完成，其余复用固定边集求解。独立复核几何、人工意图和原始节点/区域身份，失败不发布。
- Extend `MapGraphEditorDocument.cs`：计算前保存当前文档版本，应用前复核场景输入、版本、每 profile 导航和当前草稿身份，再以一个 Undo 操作应用。Extend `MapGraphNavigationValidation.cs`/`MapGraphNavigationBakeBuilder.cs`/`MapGraphGenerationController.cs`：作者主动删桥可以形成断开的图并显示警告，保存/只校验/固定拓扑重排不偷偷补边；自动生成仍严格要求保留候选连通性。每条保留边必须真实双向可达。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditOperationTests.cs`，Extend `tools/agent-repro/cases.json`：验证拖动联动和冲突拒绝、区域带动成员、删线禁连/重加解除、端点重绑/样式、固定拓扑和预算/取消，独立断言几何与身份。

P2c3b 随后 Create `MapGraphEditorWindow.cs`、`MapGraphEditorCanvas.cs`，只编排上述命令和绘制预览，加入选择、拖动、缩放/平移、参数/锁/端点/样式、生成/校验/保存入口。首次显示即捕获实际窗口并检查；本小步先完成可程序化验证的操作规则。

Extend `MapGraphLayoutDraft.cs` 增加缓存的内容指纹，检查编辑预览是否基于当前草稿；作者文档缓存 Layout，编辑/Undo 后失效，避免 OnGUI 每次读取都重建图索引。

### P2c3a 实施结果

- `20260913-001253-193` 作者操作 **7/7 PASS**：拖动联动、锁冲突、区域携带锁定成员、删线禁连及显式重加、端点重绑保留身份/样式、非法宽度拒绝、取消和工作项预算均通过。首轮 `001056-126` 仅因新测试缺少约束构造参数编译失败，补齐后验证通过。
- `20260913-002056-716` 作者文档 **10/10 PASS**：新增实际 Undo/Redo 撤回作者删线，导航输入仍有效；新编辑取代旧预览后，旧结果不能写入工作副本。两轮最终报告均正常退出，无基础设施失败和源输入变化。
- 手工断开图可以保存，但显示 `AuthoredGraphDisconnected` 警告；自动重建仍严格检查候选分量连通性，固定拓扑重排和只校验不补回删线。边的真实双向导航、几何和身份约束未放宽。
- 操作只生成意图，EditOperation 负责有界预览，Document 负责版本复核及单次 Undo，正式持久化仍由 AuthoringTransaction 所有。未修改用户场景或 NavMesh。本步没有新可见画面，继续 P2c3b 画布及实际截图。

## P2c3b 小规划：可见编辑窗口及实际截图

- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditorWindow.cs`：持有作者文档，按 Editor 更新推进生成/编辑，显示进度和新增/删除边预览，提供应用、取消、Undo/Redo、保存入口；关闭停止自己持有的任务。工作草稿在域重载时保留序列化恢复数据，恢复后重新验证导航，不复用旧计算请求。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditorCanvas.cs`：只拥有视口变换、选择和拖动状态，绘制深色矩形区域、中央名称、群图标及横竖线，负责命中和四向端口拖线；通过回调提出作者操作，不能写 SO、场景或查询导航。视口变换提供独立数值测试入口，拖动预览限频，鼠标释放提交最新位置。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphEditorInspector.cs`：选择项的属性面板，调用既有作者操作完成群/区域位置、行列锁、对齐、端点重绑和线条留白/样式；生成设置编辑写文档，不能在面板另写求解规则。该文件将属性控件从任务编排和画布交互中分离。
- Extend `MapGraphEditorDocument.cs`：恢复未验证的工作副本、保存来源冲突基线；重载后仍需新的场景验证。Extend `MapGraphEditOperations.cs` 为未知选择返回明确错误，加入显式解除人工覆写操作；普通生成继续保留作者意图。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorCanvasTests.cs`：视口往返/缩放固定点、命中优先级、重载工作副本恢复、工作草稿来源冲突。Create `Assets/Scripts/Editor/AgentReproduction/MapGraphEditorPreviewEntry.cs`：隔离 Editor 打开真实场景，调用正式窗口生成、显示并截图，记录实际画布范围/节点线数量/耗时，测试入口不进入 Gameplay。
- Create `tools/agent-repro/Invoke-MapGraphEditorPreview.ps1`：复用现有隔离工作区和 Unity 定位，在用户已授权的可见测试窗口中执行上述入口，保留源码哈希与截图；从 Ubuntu 通过 WSL 互操作调用 Windows Unity。只管理自己的测试进程，用户 Editor 保留。

验收为定向数值/重载测试通过、实际编辑窗口可读、所有可见连接横竖单段、区名居中，生成失败/取消不覆盖草稿，截图实际打开检查。窗口编排、属性控件、画布几何和持久化职责独立，沿用已确认 Editor → Gameplay 依赖，没有增加运行时生成或新的路线所有者。

可见验证调整：桌面屏幕读取受到前台窗口遮挡，原截屏不作为地图证据。验证入口改读 Unity 2022.3 `GUIView.GrabPixels` 的本窗口渲染表面（[Unity 官方源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GUIView.bindings.cs)），D3D 读回按 UV 原点修正行方向，增加非空深色画布检查；反射限定在 Editor 测试入口，API 不存在时明确失败，不进入正式窗口/Gameplay。首轮窗口还暴露空候选图的 `AuthoredGraphDisconnected` 警告被传播到生成结果：Extend `MapGraphConnectionCandidates.cs` 只过滤这一条不适用的中间图警告，最终图仍严格验收，实际窗口入口增加反例断言。

### P2c3b 实施结果

- 窗口入口 `Tools/Anomaly Search/地图指挥编辑器`，生成先展示增删线差异，作者应用后进入可编辑工作副本。画布支持滚轮固定点缩放、中键平移、选择群/边/区域、区域右下角缩放和四端口拖线；属性面板支持行列锁、对齐、端点/轴向重绑、线宽/留白/颜色、人工覆写显式重置。Undo、求解和保存复用前序组件。
- 工作副本和来源基线可序列化恢复；恢复时不复活旧任务或旧导航验证。未知节点/线操作返回明确错误。属性面板的设置临时副本按文档版本缓存，未逐次 OnGUI 实例化整图。
- `20260913-003531-153` 视口/命中/恢复/来源冲突/重置 **5/5 PASS**；`004658-664` 文档回归 **10/10 PASS**。早期 `003323-982` 测试入口访问受保护的 `hasUnsavedChanges` 导致编译失败，改用公开的 DiscardChanges；`003413-632` 新恢复测试缺少必填约束/烘焙，补齐构造后通过，未修改生产契约以迎合测试。
- 可见启动器初轮中文注释的 UTF-8 无 BOM 被 Windows PowerShell 错误解析，改成 ASCII；补显式进程句柄和退出码记录。`004556-432` 窗口生成/退出完成，读回图片上下翻转；最终 `004834-668` 正常退出、源输入不变、截图成功且实际打开检查。全图/局部归档到 `outputs/map-command/visual/P2c/`。
- 最终 7 Zone、28 群、28 横竖边，中央区名可读，选中四端口和属性面板清晰，局部缩放不越过画布边界。仍保留真实龙骨礁撤离物理断连警告，可选补边预算耗尽只说明搜索停止。未新增对游戏帧率或真实路线执行的验收结论。
- P2c 完成，继续 P2d 补齐未分区/标签同步约束后生成正式资产、绑定当前场景；正式 Scene/NavMesh 的用户修改本步仍未写入提交。P3–P6 继续按大规划推进。

## P2d1 小规划：正式落地前的同步和保存边界

当前真实场景所有群均有区域，但大规划要求未分区群也能显示；另外已保存图重新生成时仍沿用旧名称。先补齐这两个配置边界，再保存正式场景图。磁盘写入后的回滚覆盖一并补齐，避免把未验证的保存恢复留到交付末尾。

- Extend `Assets/Scripts/Gameplay/MapGraph/Config/MapGraphZoneDefinition.cs`、`Binding/MapGraphZoneBinding.cs`：显式记录 `IsSynthetic`，区分未分区显示区域和已丢失的真实 Zone 引用，默认 false 兼容现有图。Extend `Binding/MapGraphBindingAuthoring.cs`：仅允许显式合成区域持有 null Zone，且成员必须确实未绑定真实区域；真实区域引用丢失仍报错。
- Extend `Assets/Scripts/Editor/MapGraph/MapGraphSceneSnapshot.cs`、`MapGraphSceneCollector.cs`：未分区群归入由场景 GUID 派生身份的“未分区”矩形，范围为这些群世界范围的并集；保持真实节点身份，不创建或绑定世界 Zone。指向其他场景区域、归属冲突等错误仍拒绝，不能用合成区域掩盖错误引用。
- Extend `Assets/Scripts/Editor/MapGraph/MapGraphLayoutGenerator.cs`、`Assets/Scripts/Gameplay/MapGraph/Config/MapGraphNodeDefinition.cs`：普通生成/重排从场景刷新区域和群名称、层级描述，保留原位置、锁、图标和样式；“只校验”保持原图，不隐式改显示数据。Extend `MapGraphLayoutIntentValidation.cs` 保持真实/合成区域身份属性。
- Extend `Assets/Scripts/Editor/MapGraph/MapGraphAuthoringTransaction.cs`：保存显式合成绑定；在原保存事务内提供仅 Editor 内部可用的阶段回调，测试可以在真实资产写入后或场景写入后抛出异常，验证既有 Undo/磁盘恢复。回调按调用传入，不设全局故障开关、不增加 Gameplay 测试依赖。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphSceneSynchronizationTests.cs`：实际场景副本解绑一个群、生成合成区域、配置绑定/序列化重读，验证未分区显示不移动世界对象、不容许真实 Zone 缺失；纯快照重命名保持作者布局/样式。Extend `MapGraphAuthoringTransactionTests.cs` 增加已有资产/新建资产的晚期写入故障，断言磁盘字节、内存和绑定恢复。

文件均沿原配置、场景绑定、Editor 同步和事务职责扩展。合成区域仅为地图表示，不修改世界归属和正式 Gameplay 决策。验收通过后 P2d2 使用已验收事务在隔离场景生成正式资产和绑定增量，核对源场景输入哈希未变化后精确应用本功能增量，保留用户现有场景/NavMesh 编辑；随后读回源场景复验。

### P2d1 实施结果

- `20260913-010347-240` 同步构造 **4/4 PASS**：实际解绑一个资源群后生成稳定“未分区”区域，原群身份和世界位置保持；合成标记序列化恢复，真实区域引用缺失/冒充合成均被拒绝。重命名快照刷新名称和描述，原坐标、锁、人工边及样式保持。
- `010529-083` 正式保存 **5/5 PASS**：已有资产在资产写入后、场景写入后抛出 I/O 异常，磁盘字节、内存图和 Binding 完整恢复；新资产场景写入后失败，删除本次新资产及其 meta、新 Binding，保留待保存的作者修改。正常保存重开仍为 28 群、7 区域，0 次烘焙补算。
- `010728-855` 受影响的运行时绑定 **11/11 PASS**：直接引用、区域所有权、重复 ID 拒绝、烘焙命中、成本失效、锚点变化和有界补算保持。三轮最终报告正常退出、源输入不变，本步共 20 项定向验证通过。首轮 `010219-237` 因新测试遗漏 NUnit using 编译失败，补齐后通过。
- 配置增加默认 false 的合成标记，既有场景没有未分区群，因此正式布局未改变，不重复截图同图。此前的磁盘晚期失败覆盖缺口已由真实写入后的故障注入补齐；没有声称覆盖设备断电或不可恢复的磁盘故障。
- P2d2 继续生成并应用当前真实场景的正式图及绑定。此提交没有修改 Scene/NavMesh。

## P2d2 小规划：当前场景正式图和最小绑定增量

- Create `Assets/Scripts/Editor/AgentReproduction/MapGraphSceneInstallEntry.cs`：仅显式隔离 batch 入口，复用 Document/Controller/AuthoringTransaction 生成、保存、重开当前场景；输出保存前后场景、正式 SO/meta、场景/导航指纹和 Binding 验收。验证目标身份、区域数量及 0 次烘焙补算，不修改源 Editor 会话。
- Create `tools/agent-repro/Invoke-MapGraphInstall.ps1`：复用现有工作区、源输入清单和指定 Unity，运行上述入口，记录准确的进程退出、输出哈希和失败。Ubuntu 通过 WSL 互操作调用 Windows Unity。
- Create `tools/agent-repro/Apply-MapGraphScenePatch.py`：对照隔离副本保存前后的 Unity YAML 块，限定接受 MapGraphBinding 根对象/Transform/组件、必要的 prefab stripped 引用和 SceneRoots 新引用；原场景其余块逐字保留。源场景、meta 和导航数据必须仍匹配输入，资产路径仅允许当前专用图，已存在时拒绝无意覆盖。默认先输出可审查差异，显式 apply 才写入；源码实际调用和审查由编码 Agent 完成，不要求用户操作。
- Create `Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset` 和 meta；Extend `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity` 仅加入此图的 Binding。真实目标和区域直接引用复用已有组件，不更换 Prefab、不修改 NavMesh、相机或场景布局。

源文件应用后再次使用同步到隔离副本的源资产/场景读回验证，不能只相信生成副本。提交仅包含本功能资产及绑定增量，用户既有场景和 NavMesh 编辑继续保留；若绑定增量依赖用户场景新增身份，先记录依赖并检查实际索引版本，不机械提交无效的局部 YAML。既有 P2c 截图已证明同一布局，正式 HUD 留 P5 集成后复拍。

读回审查补充：正式资产已存在时，InstallEntry 在任何扫描/应用前验证原 Binding 和原烘焙可直接复用，随后只校验和重开，不保存或修复副本。首次读回 `012025-138` 的逻辑完成，但退出挂起已清理；保留该基础设施异常，新增纯读前置契约后重新执行。正式场景已有 Binding，原“新建保存”构造须在自己的隔离场景副本移除专用绑定根再 SaveAs，不能覆盖正式图或让用例因已有 Binding 失去原覆盖。

## P2b1 小规划：布局数据、独立几何校验和评分

先写验收器再接求解器，避免自动生成器通过自我放宽条件获得假成功。本步为纯 Editor 算法，不导航、不改场景、不生成正式资产；没有可见布局时不截图旧 UI。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphLayoutDraft.cs`：复制 Config 中的区域/节点/边/对齐和禁连数据，表示一次待验证的不可变生成/编辑结果；独立于 SO 事务，取消不会污染原资产。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphGeometry.cs`：平面矩形、轴向线段和相交判据；校验、评分、编辑器和后续画布共用数值规则，不包含拓扑或编辑状态。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphValidation.cs`：独立检查拓扑、有限数值、单段横竖、合法端点/端口/留白、节点/区域重叠、穿节点/名称、安全区包含、对齐一致性、人工锁/边/禁连意图。另提供测量矩阵校验，区分物理断连、人工排除和几何/求解失败；不以断连为由伪造边。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphLayoutScore.cs`：按统一参考布局计算方位、距离、位移、区域面积、交叉和总体紧凑度，返回可解释分项；分数只排序通过硬约束的候选，不参与运行时寻路成本。
- 校验器内部按独立规则拆分：Create `MapGraphValidationResult.cs`（带身份和严重度的只读诊断）、`MapGraphLayoutIntentValidation.cs`（原布局锁、人工边/样式/禁连保留）、`MapGraphNavigationValidation.cs`（测量矩阵和分量对照），路径均为 `Assets/Scripts/Editor/MapGraph/`。`MapGraphValidation.cs` 保留几何和对齐规则，避免一个文件同时堆入几何、导航和人工意图判断。
- Reuse `MapGraphService.cs`、P1 Config 的不可变条目和 `MapGraphLayoutConstraints.cs`，不重复写业务身份/寻路代码。上述 Draft/Geometry 是已确认生成器与校验器职责内的数据和数学原语细分，没有增加 Gameplay → Editor 依赖。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphLayoutValidationTests.cs`，Extend `tools/agent-repro/cases.json`：构造合法跨区横竖图、斜线/重叠/穿节点/中心名称/端口复用/非法留白/锁定破坏/人工禁连/方向不可用和断图反例；独立断言错误类型及评分相对关系。使用 Edit Mode 纯测试，不启动玩法。

验收：固定输入输出稳定；全部拒绝项有具体节点/边/区域身份，不把一般线交叉当换乘节点；不误拒绝真实群处转向，不接受坏数值；原布局和人工记录未变。通过后提交，再实现联合拓扑/布局求解和第一版可见预览。

### P2b1 实施结果

- 7 个生产 Editor 文件分别承接草稿、几何、只读诊断、几何/对齐校验、人工意图、导航矩阵和评分，没有修改运行时图成本或 Agent 执行。
- `Logs/AgentReproduction/20260912-211706-195` 首轮 19/19 PASS。审查补充线宽不得超过图标端口尺寸、无效旧图不得进入人工意图遍历；最终 `Logs/AgentReproduction/20260912-211925-830` **20/20 PASS**，无编译错误和源输入差异。
- 构造验证证明：合法跨 Zone 横竖转向通过，斜线/穿节点/中心名称/重叠/不合法留白和非有限值被拒绝；一般交叉不产生路径换乘；手工删线/位置锁/手工或样式覆写边保留；固定拓扑重排和显式改线的规则可区分。
- 导航校验区分“矩阵未测全”“某方向不可达”“真实物理孤岛”“人工禁连切断”“生成器丢了本应连通的桥”。前两种和丢桥是错误，物理孤岛及人工切断保留明确警告；绝不退回旧 LengthUnits。
- 对齐校验同时要求边两端共享行/列身份，0.001 容差只容纳局部坐标回写再相加的浮点误差；可见端点共用轴坐标。评分分项只用于生成方案比较，硬约束独立验收。
- 本步仍无可见布局，第一张新地图预览在后续生成结果产生时捕获。

## P2b2 小规划：参考布局和固定候选边集的横竖求解

继续已确认的联合生成设计，先把固定候选边集的几何可行性做成可分批独立步骤，后续 ConnectionGenerator 据此换自动边、补边。真实导航矩阵初始 MST 有 26 条边、最大度数 4，另外一个撤离节点为物理孤岛；MST 只是待试种子，不作为固定最终图。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphLayoutGenerator.cs`：以世界 XZ、Zone/群范围和统一比例建立参考数据；真实节点 ID/Zone 不变，地图最小图标/区域尺寸与世界参考位置分开保存。完成坐标求解后写回 Zone 局部位置和全图行列约束，保持旧人工锁/展示覆写。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphOrthogonalLayoutSolver.cs`：固定边集的四向端口分配，世界主要方向优先，必要时尝试另一轴；显式方向搜索状态数、每次 Advance 预算、可取消，保留少量通过独立验收的候选按 P2b1 评分比较。不能改边集、查 NavMesh 或保存资产。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphLayoutCoordinates.cs`：把横边的 Y、竖边的 X 合并为对齐组，固定位置/行列锁归入同一数值约束，投射边间距、图标分离、Zone 包含和区间留白；迭代有上限，无法满足返回具体冲突，不把未收敛坐标算成功。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphZoneLayout.cs`：按真实群占位求区域矩形，在有界中心候选中给中央名称留白，矩形仍以名称位置为几何中心；只调整地图表示，锁定区域不擅自扩张。与离散方向搜索、导航候选挑选分开。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphLayoutSolverTests.cs`，Extend `tools/agent-repro/cases.json`：单区 L 形、跨区、重合参考点、四端口、固定锁冲突、预算耗尽/取消/重复生成稳定；最终结果必须经过独立验收器。真实场景的固定 MST 可失败并保留诊断，不能以这种失败声称完整联合生成无解。

原有 `MapGraphLayoutDraft.cs`/几何/校验/评分复用。生成过程尚未写入 SO 或场景；坐标求解只做 Editor 平面约束，不替代实际移动。连续约束/Zone 适配是大规划 Solver 和 LayoutGenerator 中独立职责的文件细分，模块边界不变。下一小阶段加入联合连线和实际预览导出，首次可见产物即渲染查看，最终自然度还需 P5 全 HUD 截图。

### P2b2 实施结果

- `Logs/AgentReproduction/20260912-213912-784` 的固定边集求解构造 **9/9 PASS**：L 形、重合参考点、四端口/五端口拒绝、跨区、人工锁冲突、取消/预算、不同 Advance 步长的确定性、空区域和世界身份/旧锁保留均通过。
- 方向搜索按状态推进，坐标组每候选最多按配置迭代；结果须经几何校验器再验并按分项评分排序。停止原因区分状态预算、端口/锁/区域冲突，不把搜索用完当数学无解。只有完整结束后才公开 Result，取消清掉内部候选。
- 复核人工行列锁时发现，仅保留原锁条目还不够：节点若被改绑到同坐标的新行，旧锁会成为无用别名。追加 `LockedAlignmentMembershipChanged` 校验和独立反例，定向回归正在执行。
- 追加后的 `Logs/AgentReproduction/20260912-214142-947` 布局验收 **21/21 PASS**；P2b2 共 30 项定向测试通过。未保存新 SO/场景，用户场景改动保留。

## P2b3 小规划：真实候选骨架诊断和第一版布局预览

先让真实场景经过已测试的固定骨架求解，定位联合生成器需要交换哪些边。此处得到的 MST 仅为候选，不保证可画，不直接保存正式图。

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphSceneLayoutTests.cs`：复用 Collector/Scan/Reference/Solver，读取完整实际导航矩阵，测试侧构造导航 MST 种子；记录矩阵、每个候选的阶段预算/失败类别/坐标和实际连通性。固定种子几何失败只形成待处理诊断，不能等同整项地图验收通过。
- Create `tools/agent-repro/Render-MapGraphPreview.py`：从 Unity 导出的真实布局 JSON 生成静态 PNG，读取项目字体，检查深色底、矩形区域中央名称、群图标及横竖单段的位置；这是算法布局审阅图，不冒称游戏/HUD 实拍。Editor 窗口和正式 HUD 接入后另拍真实截图。
- Extend `tools/agent-repro/cases.json` 登记单独的实际场景布局诊断组。原始 JSON/矩阵/失败报告留在 Logs，有有效几何候选即渲染到 `outputs/map-command/visual/P2b/` 并实际查看。

之后 Create `Assets/Scripts/Editor/MapGraph/MapGraphConnectionGenerator.cs`，由完整可达候选提出多个骨架、替换未锁定自动边，复用分批 Solver 比较可行性；补边先试已有行列再试有限调整，保留手工边/样式/禁连。每个合法导航分量仍须连通，不能为通过几何删桥。新组件只管理拓扑候选，不计算导航或自行移动节点。实际联合算法的小范围参数和失败反例在固定种子证据出来后继续细化，属于已确认大规划的职责。

### P2b3 第一张预览和调整依据

- `Logs/AgentReproduction/20260912-214518-209` 完成真实候选诊断：固定导航 MST 的 26 条边可解，51 个搜索状态、8 个可行候选、194.60 ms 求解工作，最终没有线交叉；几何和实际导航分量校验通过。物理断连仍只有龙骨礁撤离节点，未伪造边。
- 已实际查看 `outputs/map-command/visual/P2b/01-fixed-seed.png`（真实 Unity 布局 JSON 渲染，非游戏 HUD 实拍）。深色底、中央区名、群图标和横竖连线可读，但**空“渔村2”被推到实验室北侧，渔村整体落到奇点塔南侧**，位置保真不能验收。首张证据保留，不覆盖。
- 用户随后确认“渔村2”是场景 bug，并已自行修复。旧图保留作历史证据，撤下针对该错误空区域的求解调整；现阶段没有实施这些调整。重新采集用户保存的场景，更新 `MapGraphSceneCollectionTests.cs` 的真实场景基线，再复拍布局。纯构造中的空区域支持保持。
- 联合生成器将比较 4 个确定性种子：实际导航长度、增加弱/强次要轴位移罚项、地理距离混合。它们只决定作者连接候选，运行时成本仍为实际导航路程。手工边/样式先固定，禁连排除，四端口上限在候选阶段预筛；每个种子再调用 Solver 独立验证，按统一世界参考评分选择。这样能尝试替换把渔村拉到南侧的自动桥边，仍保留物理连通性。

## 实施结果

## P2b5a 小规划：连续求解按迭代推进

P2b4 实测单次 Advance 最长 675 ms，原因是一个方向搜索叶子仍同步执行最多 128 次坐标迭代。本步先解决这个明确的 Editor 调度问题，数学规则、选边、评分和结果发布语义保持；补充环路随后在 P2b5b 单独实施、验证。

- Extend `Assets/Scripts/Editor/MapGraph/MapGraphLayoutCoordinates.cs`：把同步 `TrySolve` 改成私有初始化/单轮迭代和有界 `Advance`，持续保存轴组/区域工作区，公开内部只读完成、取消及结果；每个工作项最多执行一轮连续约束。精确停滞和总迭代上限继续生效，取消不得发布草稿。
- Extend `Assets/Scripts/Editor/MapGraph/MapGraphOrthogonalLayoutSolver.cs`：方向搜索叶子逐次推进坐标任务，每轮让出控制权，结束后再评分/验收；外层取消释放正在执行的坐标工作区。原状态数、坐标数分别统计，Advance 的单位明确为工作项，不能仍声称每工作项只是一条方向状态。
- Reuse `MapGraphConnectionGenerator.cs` 的逐工作项编排，不将连续求解移入窗口、Update 或 Runtime。Extend `MapGraphLayoutSolverTests.cs`/`MapGraphConnectionGeneratorTests.cs`：断言每次 Advance 的坐标增量也受预算限制；补坐标执行中取消，保持跨步长的同布局/同状态/同迭代计数。
- Extend `MapGraphSceneLayoutTests.cs`：实际采集每次 Advance 的最大坐标增量和耗时，保留原真实几何/导航验收；与 P2b4 的同输入结果对照，截图应保持一致。只改生成调度没有新样式，不重复制作不同外观。

文件职责和 Editor → Gameplay 依赖不变。本步不新增设置资产或界面，不移动用户场景；小规划、结果与审查按原文档回写。验收为 1 工作项最多 1 次坐标迭代、取消不继续工作、实际输出与 P2b4 一致，以及实测单次耗时明显降低；编辑器完整交互仍在 P2c 验证。

### P2b5a 实施结果

- `224717-160` 固定求解 12/12 PASS；增加坐标执行中取消，所有推进同时验证方向状态和坐标迭代增量之和不超过工作项预算。内层保存自身迭代状态，外层只编排、验收和评分，原数学规则没有改变。
- `224948-348` 真实场景 2/2 PASS，同一场景/导航指纹下，JSON 的 zones、nodes、edges、constraints 与 `223226-432` **完全一致**。仍为 Geographic、229 个方向状态、1,280 次坐标迭代、分数 4.3938700704；图形没有变化，继续使用第三张预览，不把同图复拍当新视觉成果。
- 每次 Advance(8) 最多 8 次坐标迭代，最大耗时 **22.43 ms**，总求解工作 **2,465.79 ms**。上轮 675.20 ms 的整候选阻塞已消除；这次属于相同搜索/结果的调度拆分，不声称游戏帧率已验证。P2c 窗口将按更小工作项及短时间片推进。
- `225316-369` 联合候选 10/10 PASS，本步最终共 **24 项定向验证通过**，无基础设施失败或源输入变化。本步未改动 Scene、NavMesh、图 SO 或正式玩法。

## P2b5b 小规划：具有导航收益的可选补边

在有效骨架上补充可走环路。只增加真实群间连接，不引入虚拟拐点；近邻/候选排序影响作者路网，正式最短路依然使用实测导航成本。可选补边失败可以保留已验收骨架，不能破坏必需连通性或人工意图。

- Extend `Assets/Scripts/Editor/MapGraph/MapGraphConnectionCandidates.cs`：按 profile 及端点索引实测方向长度，包装 `MapGraphCostSnapshot.cs` 为当前图创建方向成本快照；调用已有 `MapGraphPathfindingService.cs` 计算绕行收益，不使用 LengthUnits 或屏幕长度。
- Extend `Assets/Scripts/Editor/MapGraph/MapGraphLayoutGenerator.cs`：为已横/竖对齐的端点构造加边草稿，合并对应行/列身份，位置不变；仍由独立几何/人工意图验收拒绝穿群、名称遮挡或锁冲突。这个坐标回写能力可供后续 Editor 复用，不直接应用 SO。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphShortcutGenerator.cs`：在有效骨架上按实际导航绕行比例（至少 1.5）挑选补边；先试不移动布局的横/竖连接，再对少量候选复用逐步 Solver 调整未锁位置。每次加边前重算当前图收益，防止上一次补边后收益已消失。记录端点、收益、是否重排、预算和拒绝原因。
- Extend `MapGraphConnectionGenerator.cs`：骨架完成后编排上述可选补边，最终一起发布；只累计工作/搜索/坐标计数，不混入收益算法或几何操作。额外边上限为骨架边数 × ExtraConnectionRatio 向上取整，0 表示关闭；重排候选次数由 CandidateNeighbors 限制，补边共用一份 `max(单候选迭代上限, 32 × 群数)` 坐标总预算及剩余全局状态预算。
- 重排验收沿用原硬约束，按原世界参考的总布局分数最多恶化 15% 加 0.1 小量余量；这只是可选边的保真筛选，不能放宽横竖/四端口/连通性/人工锁。普通同排同列补边不改变位置，仍检查合并行列后所有约束。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphShortcutTests.cs`：独立构造矩形绕行闭环、低收益跳过、人工禁连/锁/穿点拒绝、方向成本及旧 LengthUnits 不影响收益、预算/取消、不同步长确定性；Extend `MapGraphConnectionGeneratorTests.cs`、`MapGraphSceneLayoutTests.cs`、`tools/agent-repro/cases.json` 验证集成及最终边数/成本/身份。真实结果生成第四张预览并实际查看。

文件继续位于已有 Editor 生成模块；ShortcutGenerator 是可选环路选择职责，独立于骨架、几何和窗口。成本/寻路能力 Reuse P1，不向 Gameplay 加 Editor 依赖。当前单 profile 场景按真实长度比较，多 profile 时只考虑共同可用边，任一方向的收益可提名但所有 profile 的连通和几何必须有效。取消丢弃新结果，预算耗尽保留已经独立验收的候选及明确诊断。

### P2b5b 实施结果

- 补边复用正式最短路及实测双向成本；先查原路线/直达成本比值，达到 1.5 才尝试。每接受一条边就刷新路径，重查后续候选收益。已对齐的连接先试，其他候选最多做配置数次有界重排；独立几何、导航、人工意图和布局失真上限全部通过才接受。
- `230434-309` 可选补边 **9/9 PASS**：3 倍绕行缩短、反向成本、旧显示长度不参与选择、低收益不补、人工禁连/锁/样式、不能穿节点或锁定名称、动态收益复核、预算/取消和分批一致性。首轮 `230219-571` 发现最终诊断覆盖了直接连接被拒的原始原因，已改为保留两次失败原因，名称反例明确锁定该区域。
- `230841-197` 真实场景 **2/2 PASS**，同一输入仍为 7 Zone、28 群。Geographic 骨架后补入 2 条连接，28 条最终边全部横竖单段，几何错误 0、交叉 0；两条新增连接原绕路比分别为 2.6153 和 2.4766。51 个后续候选因收益消失跳过，有限重排预算耗尽后保留已经验收的结果。导航孤岛仍明确报告。
- 本轮联合工作 4,333.51 ms，400 个方向状态、2,176 次坐标迭代；Advance(8) 最长 22.81 ms，单批坐标迭代不超过 8。总工作包括可选补边，不是运行时 Update 开销，也不代表穷尽全部可能路线。
- 已打开第四张 `outputs/map-command/visual/P2b/04-with-shortcuts.png`：新增环路可读，名称留白保持，渔村仍在奇点塔西侧。补边使布局分数 4.3939 → 4.8906、节点方位反转 49 → 56，在预设失真上限内换取路径收益；不把补边描述为所有视觉指标都改善。区域留白及小尺寸可读性继续在正式窗口/HUD 复核。
- `231413-920` 联合候选 **10/10 PASS**，逻辑、编译和源输入检查完成，但隔离 Editor 测试后退出挂起，由既有 runner 清理，报告保留基础设施失败。最终合计 **21 项逻辑验证通过**，不把这轮退出异常写成正常退出。用户 Editor、场景和 NavMesh 修改保留，未写正式 SO/Binding。

## P2b4 小规划：导航候选和横竖布局联合比较

目标是将 P2b3 的单骨架诊断推进为可复用的生产联合生成器。先完成骨架候选选择、人工意图保留和预算闭环；环路补边单列 P2b5，之后进入 P2c 编辑器。暂不写正式资产、不改变场景、运行时寻路成本或任务入口。

- Create `Assets/Scripts/Editor/MapGraph/MapGraphConnectionCandidates.cs`：复制并核验各 profile 的完整测量矩阵，按无向端点归一候选，保留双向实际路程，提供所有 profile 都可用的候选索引。区分缺矩阵、人工边不可达、物理分量和不同 profile 的连通冲突；不查询 NavMesh。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphConnectionSeedBuilder.cs`：固定手工边/样式覆写边，排除人工删线记录，按四种确定排序构造度数不超过 4 的森林。复用可达候选；若端口筛选丢失本应连通的分量，交由独立导航验收拒绝，不能把较少的边算成功。该文件只选边，不调整坐标。
- Create `Assets/Scripts/Editor/MapGraph/MapGraphConnectionGenerator.cs`：编排候选准备、不同骨架的 Solver、全 profile 独立验收和统一评分；同一总搜索预算分配给四个种子、每次 Advance 工作预算、取消不发布。只有完整结束才公开最佳有效结果，记录每种子边身份、状态数、可行候选、得分/冲突和耗时。复用 `MapGraphOrthogonalLayoutSolver.cs`、`MapGraphValidation.cs`、`MapGraphNavigationValidation.cs`、`MapGraphLayoutScore.cs`。
- 四种生成排序为实际导航长度、导航长度加弱/强次轴位移代价、地理距离混合。它们仅排序作者图候选，生成边的运行时成本仍由 `MapGraphNavigationCostService.cs` 从实测数据提供。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphConnectionGeneratorTests.cs`：构造确定性/顺序无关/分批、预算/取消、5 度 MST 可被替换、手工边/样式/锁/禁连、断岛保留、缺矩阵和 profile 冲突。Extend `MapGraphSceneLayoutTests.cs` 增加真实联合生成证据，必须得到非空且全部硬约束通过的结果；复用 PNG 工具生成第三版并打开检查。
- Extend `tools/agent-repro/cases.json` 登记新组。数据索引、选边策略和调度是已确认 ConnectionGenerator 的独立职责细分，仍为 Editor → Gameplay 单向依赖，不引入 Gameplay → Editor 或测试依赖。每个新脚本同步 `.meta`。

验收：不同 Advance 步长和输入枚举顺序得出一致图；每条线横竖单段、四端口和真实分量保持，人工覆写不变；候选阶段不执行新导航查询，搜索/迭代有界；真实 7 Zone、28 群保持，截图对照方位改善程度，不用总分下降替代目视审阅。

P2b4 首轮调整：`221918-007` 的 9 项纯构造通过，隔离 Editor 的退出阶段挂起由既有 runner 清理，报告保留基础设施失败。`222214-945` 真实联合生成在 120 秒截止时只比较完前两个种子，第三种子反复处理几何冲突；不能将已找到的内部候选当完成结果发布。审查发现 `MapGraphLayoutCoordinates.cs` 在坐标完全不变、仍然穿节点时会重跑 128 次相同校验。Extend 此文件，按完整连续状态的精确相等识别停滞/循环，返回具体冲突，不改变硬约束或浮点容差；Extend `MapGraphOrthogonalLayoutSolver.cs`/联合诊断计数坐标迭代。增加静止穿点反例，以及四端口筛选不得丢必要桥的构造，再跑受影响组和真实场景。用户同时更新的 NavMesh 由最新场景测试重新测量，不恢复旧烘焙。

第二轮 `222755-534` 显示停滞检测不能处理持续漂移的难解候选：120 秒执行 61,725 次坐标迭代，仍未完成第三种子。追加**每种子坐标迭代总预算**，默认 `max(单候选迭代上限, 32 × 群数)`；当前 28 群为 896 次。Solver 接受可选总坐标预算，单候选只使用剩余配额，耗尽后按正常搜索结束发布内部最佳有效解或明确未解，不能 Cancel 丢弃有效解。保持原搜索状态总上限；两种预算都属于既有 Editor 求解调度，不改变硬约束、玩法或运行时性能标准。追加独立预算断言，重新测量真实数据的总工作和每次 Advance 耗时。

### P2b4 实施结果

- `20260912-223226-432` 真实场景固定种子/联合生成 **2/2 PASS**。当前场景指纹 `436c7a5c609ed5fe76a489060eb18e84`、导航指纹 `17e80a2593b3808e59919818228226fd`，用户新的烘焙已参与全部矩阵查询；仍为 7 Zone、28 群、27 群主分量和龙骨礁撤离独立分量。
- 四种策略结果：Navigation 58 状态/146 次坐标迭代、分数 4.9505；AxisWeak 62/139、5.5655；AxisStrong 53/896、坐标总预算耗尽未解；Geographic 56/99、**4.3939，最终选用**。联合生成合计 229 状态、1,280 次坐标迭代、2,452.81 ms，候选阶段没有新导航查询。这里的耗时改善包含工作预算控制，不能称相同搜索空间的纯执行加速或全局最优。
- 第三张 `outputs/map-command/visual/P2b/03-joint-geographic.png` 已实际查看。渔村从奇点塔南侧调整为西侧略偏北，节点方位反转计数 59 → 49，26 条边保持横竖单段、0 交叉，中央名称无遮挡。各区域仍有较多留白，正式小图字号、比例和 HUD 协调度需后续实际视图验证。
- 本轮每次 Advance 推进 8 个工作项，最大 675.20 ms，主要来自单个连续候选仍可做多轮约束。这是 Editor 自动生成，不在游戏 Update；P2c 接入窗口时需要按坐标迭代进一步分批，避免操作时明显卡顿，不能把 2.45 秒总工作当成已达到交互响应验收。
- 停滞构造及原求解回归 `222648-658` 10/10 PASS。最终 `223332-267` 联合候选 **10/10 PASS**；`223608-152` 求解 **11/11 PASS**，追加证明坐标预算耗尽仍保留先前独立验证的有效候选。连同真实场景 2 项，P2b4 最终 **23 项定向验证通过**，3 轮最终报告均无基础设施失败和源输入变化。未保存正式 SO 或 Binding，场景和 NavMesh 文件的用户修改全部保留。

### P2b3 修正场景复核和阶段结论

- 用户修复后的真实数据由 `20260912-220831-959` 采集回归确认：7 Zone、28 群，各 Zone 均有群，4/4 PASS。未改动用户场景；旧“渔村2”数据不再参与生成，也没有为保留错误区域引入补丁。
- `20260912-220928-484` 固定种子诊断 1/1 PASS：26 条真实导航候选边，58 个搜索状态、8 个几何有效候选，工作耗时 801.49 ms。几何错误 0，线交叉 0，仍保留真实导航 2 个分量的警告。时间是 Editor 求解工作，不能换算为游戏帧率。
- 已打开检查 `outputs/map-command/visual/P2b/02-corrected-scene-seed.png`。错误空区域消失，7 个区域中央名称和群图标均可读，所有连接为单段横竖线；渔村仍被固定 MST 拖到奇点塔南侧，联合生成需继续改善方位。这是算法静态预览，不是正式 HUD 截图，当前不宣称视觉完成。
- 本阶段共 5 项定向测试通过。图形证据和复现方法见 `outputs/map-command/visual/P2b/README.md`；诊断骨架构造仍只在测试中，后续生产 ConnectionGenerator 必须处理人工意图、完整导航分量及预算，不直接把测试 MST 用作正式生成器。

P2a 真实场景复核中。

- 追加候选职责：Extend `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs`，通过只读 `ProjectPositionToGround` 复用已有 `GameplayTargetShapeUtility.ProjectPointToGround` 和作者探测高度/距离/坡度设置。Collector 为真实出生点保留原位置和地表投射候选，记录候选派生类型，再由共用导航采样严格校验；不调用 Spawn，不修改敌人 Transform，不增加 NavMesh 采样半径。出生点高度与群路线到达地面是不同数据，不能直接混用。
- 追加真实出生语义复用：Extend `Assets/Scripts/Gameplay/Enemy/EnemySpawnPoint.cs`，`TryGetNavigationGroundCandidate` 用作者固定偏移和原有 3 米出生采样获得只读候选，内部与真实 Spawn 共用采样实现，不消耗 Random、不实例化敌人。场景证据显示龙骨礁 Range 碰撞体顶面约 10 米、导航面约 6.8 米，单靠物理投射不能代表现有出生导航语义。Editor 最终仍以 Agent profile 严格采样和双向完整路径为接受条件；宽半径失败邻域探针只写测试报告，不参与接受。
- 采样后的审查发现：局部采样成功不能证明入图可达。Scan 增加分批的初始 Agent → 候选完整路径检查，优先采用可达候选，不能在第一个小导航孤岛上提前停下。所有候选均不可从初始 Agent 到达时，保留第一个合法采样锚点、显式标注 `ReachableFromAgentOrigin=false`，后续全对矩阵仍据实描述物理分量；不凭空加边。初始位置检查与 756 次边查询分开计数，统一工作项预算。增加断岛候选回退构造，真实场景输出初始位置和不可达事实。

- 首轮 `Logs/AgentReproduction/20260912-203822-714` 为 1 PASS / 2 FAIL。失败定位在 Edit Mode 不能调用未绑定 NavMesh 的 Agent.GetAreaCost，并非场景不可达。已核实实际 Agent Prefab 的序列化字段和项目 `NavMeshAreas.asset`：实例保存 agentType/WalkableMask/Radius/Height/BaseOffset，生产代码没有 SetAreaCost 覆写；Editor 采集改为读 SerializedObject 的实例配置和 NavMesh.GetAreaCost 项目成本。运行时不同成本仍由 P1d profile 一致性检查拒绝旧烘焙，不能借此假装已读取 live Agent 状态。

- `204319-581` 定位 12 个来源群的作者出生高度不等于导航地面；`205038-252`、`205212-160` 的失败邻域证据定位龙骨礁 Range 碰撞体与实际导航面的高度差。分别复用已有群地表投射和出生采样后，`205515-373` 的 3 项测试通过，28 个锚点和 756 次边查询完整。
- 审查追加“局部合法但不可达”的候选回退；`205952-211` 真实场景通过，新增构造中测试地面高度容差忽略了 NavMesh 体素误差，修正测试容差后最终 `Logs/AgentReproduction/20260912-210109-489` **4/4 PASS**。重复采集的 ID/指纹稳定，真实 Transform、序列化群配置、物体数量和 Random 状态不变；取消/每工作项预算及断岛候选回退通过。
- 本次场景采集仍为 8 Zone、28 规范群、2 Agent 共用 1 profile。最终 38 次候选采样、35 次初始可达查询、756 次有向边查询；原始数据为该轮 `scene-collection.json`、`scene-navigation.json`。扫描耗时见原始报告（约 150 ms 总查询工作，分批执行，不是每帧开销）。
- 候选回退使雨林资源 B 和 Boss 群选到可从初始角色到达的导航点。**龙骨礁撤离点仍只有一个作者成员候选，其导航面可采样但从两名初始角色均不可达**，位置约 `(385.6,10.508,239.23)`。这是后续图生成必须保留的物理断连诊断，不伪造连线、不删除撤离群；实际撤离交互和场景修复需结合 P3/P6 进一步验证。当前不能声称全图可通行或正式地图已完成。
- P2a 没有可见布局，因此尚未截取新地图；视觉检查从 P2b 的真实生成预览开始。
- 出生链受影响回归 `Logs/AgentReproduction/20260912-210301-176` 的 SceneEnemyConfiguration **2/2 PASS**，实际场景 30 个敌人 Prefab 槽位和出生归属保持；P2a 合计 6 项最终定向测试通过。最终查询工作总耗时 142.93 ms，已按预算分批，不含场景加载或 Editor 导入。


### P2d2 实施结果

- `Logs/MapGraphInstall/20260913-011828-781` 在隔离副本生成、保存、重开通过，正式资产为 `Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset`，GUID `dcbcd9140c33c2640b3f8de1becc3cc3`。7 Zone、28 节点、28 边，35 个场景直接引用，布局沿用已目视检查的 P2c 窗口结果。
- 经源场景、meta、NavMesh 文件哈希检查后应用增量：仅新增 MapGraphBinding 根物体的 3 个 YAML 块及 SceneRoots 引用，35 个目标全部复用现有对象。原有场景块字节保持。提交也只包含这份新增绑定，用户场景和 NavMesh 修改继续留在工作区；烘焙依据当前工作场景，不把旧 HEAD 几何冒称同一验证基线。
- `20260913-012025-138` 第一次读回行为检查通过，但 Editor 退出挂起，隔离启动器清理自身 PID。审查发现检查前仍可能更新烘焙，已改成验证原资产后只读扫描、不保存。
- 最终 `Logs/MapGraphInstall/20260913-012638-681` **纯读回 PASS，正常退出，源输入未变**：先验原 Binding/烘焙，0 次补算，再完整扫描核对布局。内容指纹 `b47d41bedd885eab324e6c414780f874`，场景 `7d82f625e8f68e6f3e47c689c03905c1`，导航 `17e80a2593b3808e59919818228226fd`。读回工作 0.801 秒。
- `Logs/AgentReproduction/20260913-013004-334` 保存事务 **5/5 PASS**，测试副本先拆除已有正式 Binding 后生成独立测试资产，避免测试覆盖正式 SO。无基础设施异常。
- 实体导航仍有 2 个分量，龙骨礁撤离孤岛如实保留。未伪造连线或删除撤离群。P2 完成的是正式作者配置，P3–P6 继续实现执行、入口、HUD 和真实场景验收；未宣称游戏地图或性能已完成。

# P2 场景采集、自动生成和编辑

基线：`8d76e38`。状态：P2a 实现/回归完成，进入 P2b1。保留用户当前场景相机/Gizmos 修改，不恢复旧值。沿用已确认大规划的横竖单段、真实群转向、连线决定路线、人工覆写保留和持续截图要求。

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

## 实施结果

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

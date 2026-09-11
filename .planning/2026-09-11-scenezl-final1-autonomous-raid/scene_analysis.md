# Scenezl_Final 1 场景调查，规划依据

日期：2026-09-11。源码基线：`c8b1a4c`，调查开始时工作区干净。本轮完成了场景 YAML、prefab 依赖、配置和源码的静态检查，**尚未为本轮启动 Play Mode，尚无本轮自主搜打撤实测结果**。下文的 Profiler 数据由用户提供。

主规划见 [task_plan.md](task_plan.md)。逐层追踪 Prefab、实例覆盖、SO 的补充调查见 [prefab_configuration_audit.md](prefab_configuration_audit.md)，引用边和源文件哈希见 [serialization_audit.json](serialization_audit.json)。**本文描述原 SHA256 快照；用户随后已修改场景并加入撤离群，当前层级修复见 [hierarchy_repair_execution.md](hierarchy_repair_execution.md)。** 本文记录事实和待验证推断，不把源码疑点当作已经复现的运行时 bug。

## 1. 已确认的研究对象

- 用户确认场景为 `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`，不是 `Scene_ZL/Scene_Zl_Final.unity`，也不是旧回归的程序生成场景。
- 场景 GUID：`69063e03d98f74ebfb96e57bcf01f6e9`。
- 调查时场景文件 SHA256：`70da01cd5c578f74d5f655dc7028989e4e2dc20c1b367a0215d3fabad49d1087`。
- 用户确认自动化边界：**Agent 自主选目标，测试程序代替玩家操作背包**。不新增游戏内自动拾取系统，不由测试程序下达搜、打、撤指令。
- 用户已确认满包行为：**Agent 自主撤离，保留箱内剩余物品**，不把容量阻塞伪装成资源全局完成。
- 引擎为 `2022.3.62f2c1`，URP `14.0.12`，AI Navigation `1.1.7`，Test Framework `1.1.33`。
- 当前 `EditorBuildSettings.asset` 没有收录本场景。Editor 直接打开场景不受此限制；后续独立 Player 验证必须在隔离副本中显式指定该场景。结算后的按 buildIndex 重开另有风险，不能为了测试悄悄改正式构建入口。

## 2. 场景内容

以下为原始场景序列化内容和递归 prefab 引用检查。**数量不是运行时激活对象总数**：prefab 中还有嵌套对象，运行时还会生成敌人、目标群、UI；`stripped` 记录不应当作另一份组件。后续必须用 Unity 加载后的层级清单补齐。

| 内容 | 静态检查结果 | 对本次验证的影响 |
| --- | --- | --- |
| Agent | 2 个 `Assets/Prefabs/PlayerPrefab/Agent.prefab` 实例，名为 `Agent_01`、`Agent_02` | 必须逐 Agent 记录资源、战斗、撤离和库存归属 |
| 初始位置 | 根 Transform 覆盖约为 `(221.412,16.001,82.032)`、`(203.2,15.851,23.5)` | 保留原出生点，不能用传送到箱子或出口的方式验收整局 |
| Pawn 配置 | 冰系 300 HP/10 防御/移动 8，土系 270 HP/80 防御/移动 12；双方发现范围 200、扫描间隔 0.5 秒、交互距离 0 | 大范围扫描、不同移动速度、零交互距离都是实际负载与边界条件；最终数值仍以运行时配置解析为准 |
| 目标策略 | 基础 Agent prefab 的 `_enableDecisionModule: 0`，Pawn 配置开启 TargetDiscovery | 首先检查默认 Discovery 链，不能启用另一套评分策略代替修复默认链 |
| 资源 | 12 个 `ResourceCluster.prefab` 直接实例，26 个 `LootBox_1.prefab` 直接实例 | 搜索多个箱子的累计负载、两个角色访问同一资源、满包后的推进都需要覆盖 |
| 敌人来源 | 11 个 `EnemySourceCluster.prefab`、1 个 `BossSourceCluster.prefab` 直接实例；还直接引用 2 个 Tidal 和 1 个 Sentinel 出生点 prefab | 这不是活敌人数。按运行时实际生成、存活、来源群绑定和敌人类型建立清单 |
| Zone | 场景直接定义 7 个 Zone；另有员工宿舍2、员工食堂2、雨林2、渔村2、奇点塔2、实验室2 六个 Prefab 实例，源根和 Zone 未被覆盖删除/禁用 | 静态来源可解释 7+6=13 个 Zone，与用户采样相符；六对区域共用 TargetId，运行时需核对重生告警和各自群归属 |
| 撤离点 | `Extraction_A`、`Extraction_B`，持续时间均为 3.2 秒，`DetectionHorizontalScale=0.08` | 校验实际有效范围，覆盖同点双人、跨高差、读条打断 |
| 撤离点局部位置 | A `(206.1,16.6,354.2)`，B `(322.8,0.12,-132.7)`，局部缩放均 `(5,0.35,5)` | 运行时导出世界 Bounds、NavMesh 落点和连通性，不能仅按 Transform 距离判定 |
| 撤离群 | 工程有 `Assets/Prefabs/Cluster/ExtractionCluster.prefab`，但本场景没有实例化它；场景及递归 Prefab 来源中撤离群脚本引用为 0。A/B 及父级为普通场景对象，没有群或 Zone | 缺少群和成员接线，不是已有群仅漏绑 Zone；全局群注册不以 Zone 非空为前提。详见补充调查和 S01 |
| NavMesh | 1 份直接挂载的 NavMeshSurface，引用 `Scenezl_Final 1/NavMesh-NavMesh Surface.asset`，AgentType 0、所有层、RenderMeshes 几何 | 先使用原烘焙数据，检查角色与箱子入口、出口是否连通。没有发现此依赖链挂载 RuntimeNavMeshSurfaceBuilder，不能默认卡顿来自运行时重烘焙 |
| 地形 | 9 条 Terrain、9 条 TerrainCollider 序列化记录，Terrain 均 enabled、`m_DrawInstanced=0` | 核实可见地形数量、树草、阴影和 Collider 负载 |
| 墙体 | 场景直接出现 217 条 `PerspectiveFadeWall` 脚本引用，另有遮挡检测控制器 | 材质实例、透明队列、射线分配可能放大场景成本 |
| 海面 | 3 个 OceanFFTGenerator：`Sea`、`Sea (3)`、`Sea (5)`；64×64，逐帧更新，共用材质 GUID `aca6c9a2543a16c478c1faedbf31138e` | 三份 CPU FFT、纹理上传和材质写入需要分别计时；共享材质最后写入者还可能影响海面原点和纹理 |
| 局内流程 | 直接场景对象、`Canvas.prefab`、`RaidFlowController.prefab` 都引用 RaidFlowController | Awake 有重复实例销毁保护；需检查实际胜出实例、配置和执行顺序，不能静态断言三个控制器同时工作 |

## 3. 用户提供的已知性能热点

以下完整数据替代此前复制后列标题不完整的片段。场景由用户指定，采样时的分辨率、帧号、Profiler 模式、游戏阶段、Deep Profile 开关尚未随数据提供，后续运行会一并记录。不能据此推出整局平均 FPS。

| Profiler 条目 | Total | Self | Calls | GC Alloc | Time ms | Self ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PlayerLoop | 97.8% | 0.1% | 3 | 98.5 KB | 131.45 | 0.15 |
| Update.ScriptRunBehaviourUpdate | 85.1% | 0.0% | 1 | 93.5 KB | 114.44 | 0.00 |
| BehaviourUpdate | 85.1% | 0.0% | 1 | 93.5 KB | 114.44 | 0.09 |
| AgentTargetDiscoveryController.Update() [Invoke] | 63.5% | 63.5% | 1 | 7.4 KB | 85.43 | 85.36 |
| AgentPawnRoot.Update() [Invoke] | 11.5% | 11.5% | 2 | 2.8 KB | 15.55 | 15.55 |
| TargetZoneAuthoring.Update() [Invoke] | 7.9% | 6.9% | 13 | 23.1 KB | 10.69 | 9.38 |

判断：

1. 三个业务入口是**用户已观测到的性能问题**，不是仅凭源码提出的猜测。首先处理 Discovery，其次是 Pawn 执行链和 Zone 范围更新。
2. 120 FPS 的整帧预算约为 8.333 ms。三个条目的耗时都值得单独拆解；这不意味着三个相同预算可以相加使用。
3. 父子条目的时间不可重复相加。Calls=2/13 的数值是该条目的合计，不是每个 Pawn 或每个 Zone 都消耗表中的时间。
4. `[Invoke]` 的 Self 很高，并不能说明耗时就在 Update 外层几行。未单独建立采样标记的托管子调用可能归入入口。新增细分 Marker，再按 Total、Self、调用次数、查询次数和分配量判断。
5. 保留这个坏样本，后续同一源码、种子、出生点、相机阶段复跑。优化结论需要绝对耗时和整局尾部帧耗时，不能只比较百分比。

## 4. 默认搜打撤源码链

```text
AgentPawnRoot.OnEnable → 初始化/注册 → 默认 Discovery 调度
  → CollectVisibleEnemies / CollectWorldTargets
  → 具体成员、视线、范围、可达性校验
  → SubmitDirective → 生命周期 → Brain 状态机/行为树
      SearchResourceActionNode → 箱子生成内容 → 靠近具体成员 → 等待背包
      EngageEnemyActionNode → 追击/空间交战 → 伤害/击杀/掉落
      ExtractActionNode → RaidFlow 按 Agent 计时 → 结算库存 → 销毁角色
  → 所有要求撤离的 Agent 已撤离 → 成功 UI → 本局结束
```

默认 Discovery 比较可执行敌人与资源成员，没有普通候选才尝试撤离。它不会主动选择 EnemySource。扫描间隔 0.5 秒不等于每次扫描开销有限；`_maxAgentScansPerFrame` 限制角色数量，没有限制每个角色扫描的成员数和路径查询次数。

背包是独立的玩家交互环节。`LootBoxEntity.ApplyResourceClusterTier()` 会开启资源点规则；`SearchResourceActionNode` 对这些箱子要求 `IsResourcePointLooted` 才完成。单纯打开、关闭背包且留有物品会继续等待。背包打开暂停游戏时间，搜索进度由 UI 用 unscaled time 推进。

相关现有能力：

- `AgentRuntimeRegistry.TrySetFocusedAgent`、`FocusedAgentChanged` 可供测试操作角色焦点。
- `InventoryScreenController` 已公开 `ActiveInventoryAgentId`、`ActiveSessionContext`、`ActiveExternalGrid`、`ActivePlayerGrid`，可以检查会话归属。
- `LootBoxEntity.Interact/CreateInventorySessionContext`、`InventoryScreenController.OpenLootBox/CloseInventory` 是正式开关入口。
- `DraggableItemUI` 有正式拖拽事件和搜索状态；快捷转移实现目前是私有 `ExecuteQuickTransfer`，点击入口依赖 `Input.GetKey(Control)`。
- `AgentDirectiveFeedbackChannel` 可以观察指令结果；`AgentRuntimeConsoleDebugDumper` 能打印导航、黑板和资源细节，适合失败时一次性快照，不适合逐帧输出。
- `RaidFlowController` 维护要求撤离、已撤离、已结算的 Agent 集合；`PlayerStorageService` 写入共享仓库。角色被销毁本身不等于已经正确入库。

## 5. 优先验证的问题

| ID | 证据及置信度 | 构造/观察方式 |
| --- | --- | --- |
| S01 撤离点没有进入候选注册链 | 静态接线缺口明确：工程有群 Prefab，目标场景没有群实例/组件，A/B 是普通场景点；Collector 只扫描已注册的群。Zone 为空只告警，不是此次根因 | 原场景加载后只读检查两个出口及其群映射；无普通候选时观察 Discovery 的真实输出，不能由测试补群后冒充原场景通过。正式修复保留 A/B 原配置 |
| S02 满包后等待资源取空 | 源码规则冲突明确，是否在本局发生取决于 loot 和容量；尚未实测。用户已确认满包自主撤离，保留箱内剩余物品 | 使用实际 5×6 背包、自然 loot、正式转移接口；记录无法继续放入后的推进、另一 Agent 的继续取物能力和剩余物品守恒 |
| S03 触发器的单角色缓存 | ExtractionPointController 只持有 `_playerCollider/_playerAgentId`，OnTriggerStay 换人会先汇报旧人离开；ExtractActionNode 又逐帧续写 presence | 同点双人、多 Collider、先后离开、反击后回来；检查每个 Agent 的独立计时，防止互相清零或无指令即撤离 |
| S04 会话开关关联不完整 | Search 等待逻辑检查角色 ID 和背包开关，但仍需确认打开的是当前实际资源；普通资源和资源点有不同完成规则 | 相同 Agent 打开无关背包/另一个箱子，两个角色轮流处理同一箱子；不能完成错误资源 |
| S05 运行时 ID 重生与单例配置漂移 | 同一资源/敌人群 Prefab 多次复用，场景没有 `_targetId` 覆盖；六对 Zone 也共用 ID。三处 RaidFlow 的真实来源已确认，Canvas 的 MissionName 为 MVP Raid，另外两处为 Large Island Wall Layout | 输出场景身份到运行时 ID 的映射、实际存活控制器、初始化告警。重复 ID 不直接等同于注册失败；源码会销毁多余 RaidFlow，不能从 YAML 顺序推断胜出者 |
| S06 寻路失败、停靠点失效或持续换目标 | 完整大场景、坡面、建筑、两个角色不同起点均未被旧夹具完整覆盖；资源候选点已有缓存，但需验证位置/几何变化后的失效 | 从真实失败坐标裁剪用例，覆盖出生不在 NavMesh、Partial、动态障碍、箱体移动、成员禁用和不同 Agent 到达同箱 |
| S07 死亡/撤离/结算组合 | 两个角色的注册、要求撤离集合、死亡、销毁和持久化是不同状态 | 一人先撤、另一人死亡或继续；仓库写入失败；禁止只看场景中还剩几个人判成功 |
| S08 无命令却有玩家输入效果 | 顶部指令提示和世界点击输入在正式场景安装，测试不能靠关闭这些系统掩盖误发 | 记录输入来源、请求优先级和 CommandId。完整自主运行不出现 ManualTargetClick 请求，背包事件不能透传成世界指令 |
| S09 资源群父级和显式 Zone 不一致 | 实例 `672118188`（场景行 40146）父 Transform 为龙骨礁 `279692724`，显式 `_zone` 却为员工食堂 `922218180`。源码优先使用显式引用 | 核对区域内容、实际注册和轮廓。跨层级绑定不必然是 bug，先确认是否为有意配置；审计不能按父级误算归属 |
| S10 资源群唯一成员被覆盖为空 | 渔村 ResourceCluster_B，实例 `1566286469`（场景行 98904），源数组长度 1，场景把第 0 项 `_entityObject` 明确覆盖为 0，没有扩大数组 | 检查实际箱子到群映射、资源是否因此不进入候选、空群是否一直未完成。该空引用不同于有效 Prefab stripped 引用；影响尚未运行验证 |

## 6. 三个已知热点的细分方向

### H01：AgentTargetDiscoveryController.Update，85.43 ms / 7.4 KB

具体文件：`Agent/Runtime/AgentTargetDiscoveryController.cs`、`Agent/Targeting/AgentTargetCandidateCollector.cs`、`Targets/Authoring/ResourceClusterAuthoring.cs`、`Agent/Navigation/AgentNavigationQuery.cs`、`Perception/TargetVisibilityQuery.cs`、`Perception/CombatAimPointResolver.cs`。

- `CollectWorldTargets` 对资源群先调用群内可达成员查询，再检查距离是否在 200 范围内。范围外的群也可能先付出路径查询成本。
- 群内逐个资源、逐个停靠候选调用 SamplePosition/CalculatePath；已经缓存部分几何候选、复用部分 NavMeshPath，不应重复“修复”为已有缓存。
- 找到成员后 Collector 又调用 `AgentNavigationQuery.Check`，正式 Submit 验证还可能再次计算路径。
- `AgentNavigationQuery.Check` 新建 NavMeshPath，读取 corners；感知调用 GetComponentsInChildren 和 RaycastAll，也会分配。
- 同目标重复请求在生命周期进行 SameTarget 判断之前先校验，可能重复支付空间验证成本。
- ResourceCluster 基础 Prefab 的 `_showNavigationCandidateDebugObjects=1`，本轮核查的直接实例没有关闭它；采样增加候选调试物体创建次数和耗时，不能漏算当前真实配置的附加成本。

观测量：每次扫描群数/成员数/几何候选数、提前剔除数、路径查询数、射线数、完成耗时、分配量、候选年龄、接受/拒绝原因。优化须保持成员级选择、合法高低差攻击、墙体阻挡和失败缓存语义。

### H02：AgentPawnRoot.Update，两个角色合计 15.55 ms / 2.8 KB

具体文件：`Agent/Core/AgentPawnRoot.cs`、`Agent/Core/AgentBrainController.cs`、`Agent/Commands/AgentDirectiveLifecycleController.cs`、`Agent/AI/Actions/SearchResourceActionNode.cs`、`Agent/AI/Actions/EngageEnemyActionNode.cs`、`Agent/Navigation/AgentNavigationMotor.cs`。

- Pawn 每帧更新属性/图腾、身体事实、指令有效性、Brain、外部位移，需区分两名角色分别处于什么状态。
- Search 每 Tick 重新选择群内可达资源。已到箱边等待背包时仍可能查询整群。
- NavigationMotor 每 Move 都先 `AgentNavigationQuery.Check`；其 0.1 秒阈值只约束 SetPath，不约束前面的 CalculatePath。
- 生命周期 Tick 有目标有效性/可见性检查；战斗节点也会检查发射条件，需区分必要的开火安全检查和重复发现扫描。

观测量：每个 Agent 的 Brain 状态/节点、Search/Engage/Extract 子耗时、CalculatePath 与 SetPath 次数、属性刷新是否实际变化、每帧分配、暂停期间是否仍执行昂贵查询。

### H03：TargetZoneAuthoring.Update，13 次合计 10.69 ms / 23.1 KB

具体文件：`Targets/Authoring/TargetZoneAuthoring.cs`、`Targets/Authoring/GameplayTargetClusterAuthoringBase.cs`、`Targets/Authoring/ActiveEnemyClusterAuthoring.cs`、`Targets/Runtime/GameplayTargetShapeUtility.cs`。

- Zone 每 Update 都聚合状态、重建范围，遍历子群轮廓，再生成外轮廓、投射地面、更新显示。
- BuildSmoothRange 的凸包等路径创建集合；静止 Zone 也可能重复几何计算。
- 不能直接关闭所有 Zone Update：目标状态和点击轮廓也依赖它。建议拆开状态变化、范围几何变化、显示更新，按变化驱动；动态敌人群设置有界刷新频率。

观测量：每 Zone 的成员数、输入点数、重建次数、地面射线次数、LineRenderer 更新次数、真正发生的变更次数。静态区域应在初始化后不再按帧重建；动态区域仍正确包围成员。

## 7. 次级性能候选

- `PerspectiveWallFadeController.cs` 每 LateUpdate 创建 HashSet 并使用 RaycastAll；`PerspectiveFadeWall.cs` 获取 renderer.materials，配置透明材质。测量分配、实例数、draw calls 和 overdraw，修改前保留画面参考。
- `OceanFFTGenerator.cs` 每帧 CPU 频谱/IFFT/法线计算，上传两张纹理；`FFT2D.cs` 每次 IFFT 新建行列数组。三份生成器共用材质还存在输出所有权问题。先测每实例成本，若需要共享模拟，单独列文件、缓存键和原点职责供 Review。
- `RaidMinimapController.cs` 每 0.75 秒全场 FindObjectsOfType 并重建临时集合；RaidFlow 会自动安装它，不能因为场景 YAML 未直接引用就认为不存在。
- 高画质 URP 启用 4×MSAA、SSAO、全局描边、Sobel 描边，阴影距离 150、4 级联；Terrain 没开启 instancing。是否为 GPU 瓶颈需要采样，不能仅为达标直接降画质。
- `BehaviorTreeDebugTrace` 用有界 List、RemoveAt(0) 保存记录；只有确认处于热点再优化，不把全部 Core 系统纳入重构。

## 8. 测量环境和既有工具的边界

本机只读查询结果：AMD Ryzen 9 9950X3D（16 核/32 线程），NVIDIA GeForce RTX 5090 D，约 128 GiB RAM；NVIDIA 显示输出 3840×2160、240 Hz，Windows 驱动版本 `32.0.15.9186`。还存在核显和虚拟显示设备，后续必须由 Unity SystemInfo 确认实际渲染 GPU，不能只凭操作系统设备列表判断。

项目当前质量索引 2（High Fidelity），VSync=1，Frame Timing Stats 关闭，runInBackground=0。桌面分辨率不等于 Game View 渲染分辨率。主规划拟以本机 4K、高画质、RenderScale=1 为性能目标配置，首次运行记录原设置，再在受控副本中解除帧率上限进行性能测量。

既有 `tools/agent-repro/Invoke-AgentRepro.ps1` 可复用 Unity 版本检查、快照、隔离存档、进程超时和结果完整性。它用 `-batchmode -runTests -testPlatform EditMode`，由协程进入 Play Mode；普通用例使用 `-nographics`。`ReproductionTestFixture` 会新建空场景并搭建 World，不能直接拿它承载本次原场景整局。

正式性能必须有真实图形输出。Editor Game View 与独立 Player 分别报告，不能用无图形测试的帧率替代。FrameTimingManager/ProfilerRecorder 只作真实可用指标采集，检查有效性、单位和自身成本。Unity 文档说明 Frame Timing Stats 的启用方式及采样开销，见 [FrameTimingManager](https://docs.unity3d.com/2022.3/Documentation/Manual/frame-timing-manager.html) 和 [ProfilerRecorder](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html)。Editor batchmode 下不能依赖 WaitForEndOfFrame 驱动截图或结束条件，见 [WaitForEndOfFrame](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/WaitForEndOfFrame.html)。

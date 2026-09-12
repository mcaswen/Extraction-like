# P1 图数据和纯路径

基线：`453d793`。状态：P1a、P1b、P1c 实现、测试和审查完成；P1d 继续实施。P0 的静态场景证据已归档，P1/P2 继续核实真实导航路径，不能用静态组件统计代替可达性。

## P1a 小规划：拓扑、成本和确定性最短路

先交付纯图层的独立闭环，再进入带 Unity 导航依赖的 P1b 及 Zone/布局序列化。这个拆分不改变已确认模块边界。

- Extend `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphService.cs`：保留 SO 入口，补供生成/测试使用的只读数据入口；构建时诊断重复节点/边、悬空、自环及重复连接，建立稳定排序的图索引。不能让坏配置静默覆盖。
- Extend `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphPathfindingService.cs`：接成本接口，复用搜索工作区、稳定处理同成本路线；先检查节点存在再允许起终点相同。返回不可变结果快照，调用方不能修改下一次路径或缓存。
- Create `Assets/Scripts/Gameplay/MapGraph/Runtime/IMapGraphCostProvider.cs`：仅给出具体边方向的非负有限成本；旧 LengthUnits 作为明确的兼容提供者，正式导航图使用快照。
- Create `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphCostSnapshot.cs`：按稳定边 ID 保存正反向成本及配置/修订标识，复制输入后只读；不引用 Agent、NavMesh 或 Editor。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphPathfindingTests.cs`：独立验证纯图最短路/配置边界/快照，避免把它塞进后续自动布局测试；复用既有验证工作区/测试夹具。
- Extend `tools/agent-repro/cases.json`：增加 MapGraphPathfinding 定向组；不全量重跑旧玩法测试。

核心契约：最终选择由传入成本决定，图上坐标/画线长短不改变代价；缺失/负数/非有限成本不能走；起点或终点不存在必须拒绝；相同输入不依赖序列化列表顺序；结果和成本输入不能被外部集合修改。

## P1a 测试/审查

小图覆盖成本替代、正反向、断图、零成本环、同成本稳定性、坏拓扑、未知同名起终点、集合隔离、旧 MVP 图兼容，以及枚举所有简单路径的独立最优成本对照。保留精确登记清单、NUnit XML 和源哈希。该阶段不改可见地图，无需重复抓旧小地图截图。

## 实施结果

- 已实现纯图成本接口、正反向不可变成本快照、拓扑诊断、稳定节点索引和可复用 Dijkstra 工作区。结果复制为只读集合；邻接表和输入集合不泄漏可修改别名。
- 修正原代码对 `start == target` 提前成功的边界，未知同名起终点现在拒绝；坏拓扑不再静默覆盖节点或连接。正式场景将显式传导航快照，旧 MVP 的 LengthUnits 兼容保留。
- `Logs/AgentReproduction/20260912-193242-122`：10/10 PASS，NUnit 执行 7.86 秒，0 缺失/失败/超时，验证工作区源快照一致。包含 20 张五节点图的 500 个起终点组合，与独立简单路径枚举的可达性/代价逐一对照。
- 同时检查旧 MVP 资产 25 节点从起点均可规划、方向成本、零成本环、非法成本提供者、列表顺序变化和返回集合隔离；原 Projection 的 AddRange 调用编译兼容只读结果。
- 架构审查见 `architecture_review.md`。本阶段没有 NavMesh 查询、正式地图替换或样式资产变更；未据此宣称新地图/真实逐群执行已验收。

## P1b 小规划：共用导航路段查询

基线：`4aa2681`。本步落实大规划的共用导航能力，不改移动所有权或群执行规则。现有 CombatApproach 会读取失败查询留下的部分路径，Motor 使用查询缓冲的角点和诊断；这些兼容契约必须保留。

- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationProfile.cs`：从 SegmentQuery 拆出的不可变导航参数，保存 agentType、areaMask、采样半径/高度约束和 32 个区域成本；可从 live Agent 快照，Editor 使用相同参数。独立文件避免配置数据混进查询算法。原生 filter 不向公共调用者暴露可变成本数组。
- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationSegmentResult.cs`：只读路段结果，包含有效性、起终点、实际角点路程、角点数和失败原因，不拥有任务或移动状态。
- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationSegmentQuery.cs`：共用采样、高度约束、完整性和非分配角点测量；提供 profile + origin + destination 查询，另保留 live Agent.CalculatePath 入口以完整继承原生 Agent 成本。每个调用方独占可复用 Buffer，不静态共享 Path。
- Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationQuery.cs`：包装共用查询，继续负责 readiness、到达容差，保留 Buffer 兼容接口、原失败字符串和计数。路径首角点作为地面位置；无角点才用带缩放的 baseOffset 修正。
- Reuse `AgentNavigationMotor.cs`、`AgentCombatApproachQuery.cs`：保留执行和部分路径接近能力，不向地图层转移。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphNavigationTests.cs`，Extend `tools/agent-repro/cases.json`：构造真实 NavMesh，核对绕行长度、坡面、断岛、起终点采样/高度、区域 mask/cost、缓冲隔离和查询不改变 Agent 状态。

测试先跑 MapGraphNavigation，再跑原 Navigation（到达/失联/停滞/失败恢复）及必要的资源导航、高处交战定向用例。原测试若使用实际场景单个记录路径，可以通过登记小组避免全量长测。每轮运行冻结源码，保留 XML/日志/源哈希。该步仍不改变可见地图；P2 初始生成预览开始图片复核。

## P1c 小规划：可持久化的地图、人工约束和烘焙数据

P1c 只建立 P2 生成/编辑需要的数据契约，之后 P1d 接真实场景绑定和成本缓存；不会在 SO 中加入导航查询或地图生成。

- Extend `Assets/Scripts/Gameplay/MapGraph/Config/SO_MapGraphDefinition.cs`：显式 schema 版本及修订号、Zone/参数/约束/烘焙集合；旧资产缺版本仍按 legacy 读取，只有明确生成/迁移才进入正式图版本。Editor 的事务替换入口复制集合，运行时只有读取入口。
- Extend `MapGraphNodeDefinition.cs`：保留原构造兼容，增加 Zone ID、局部坐标、占位尺寸、行列 ID、位置锁和源对象身份；正式节点 Position 表示 Zone 内局部偏移，图坐标统一由 Service 解析。布局修改产生新定义，不修改搜索已经引用的对象。
- Extend `MapGraphEdgeDefinition.cs`：明确单段 Horizontal/Vertical、人工/生成来源、端点留白、宽度/颜色覆写；不存拐点。LengthUnits 只属 legacy 兼容，正式成本独立。
- Create `MapGraphZoneDefinition.cs`：区域矩形、名称中央安全区和人工锁，独立保存范围数据。
- Create `MapGraphGenerationSettings.cs`：参数默认值和有限搜索预算，不放算法。
- Create `MapGraphLayoutConstraints.cs`：行列对齐坐标/锁、禁止连接（人工删除记录），各条记录只持有稳定节点 ID；不把删除记录误当实际边。
- Create `MapGraphNavigationBakeData.cs`：导航 profile 参数、输入指纹、版本和双向实际路程，缺失/失败方向有显式有效性，不能序列化成零成本有效通路；仍不引用 Agent/场景对象。
- Extend `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphService.cs`：Zone 索引、正式节点局部转全图坐标，未知 Zone/重复 Zone 诊断；搜索不受显示矩形移动影响。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphDefinitionTests.cs`，Extend `tools/agent-repro/cases.json`：保存/重读 schema、区域/节点/边/锁/删除记录/导航成本，验证旧资产不被自动迁移、坏绑定诊断及集合隔离。

具体新增数据类型都留在上列负责该数据的文件，不创建新的通用状态层。Theme 颜色和具体 UI 尺寸在 P5 随真实截图校准，避免现在提交尚未显示过的正式美术资产。P1c 不修改场景或 Prefab，不迁移旧 MVP 文件。

## P1b 实施结果

- 完成 Profile、SegmentResult、SegmentQuery 和原 Query 包装。静态查询采样两个地面锚点，live 查询保留 Agent.CalculatePath；共用完整性/高度/非分配角点长度测量。参数及结果不可变，缓冲独占，不调用 SetDestination/SetPath。
- `Logs/AgentReproduction/20260912-194451-677`：MapGraphNavigation 9/9 PASS。实际区域成本可使路径绕行，输出仍是物理路程；mask/断岛/错误高度均拒绝，起终点相同有效，查询不改变原生移动。预热后 100 次查询，GC.Alloc 记录为 0。
- `Logs/AgentReproduction/20260912-194559-347`：原 Navigation 17/17 PASS，覆盖平地/坡面/零停止距离、导航丢失、停滞、恢复及 1×/4× 分支。
- `Logs/AgentReproduction/20260912-194743-560`：兼容组 10/10 PASS，复用原高处敌人接近 7 例、真实实验室两条记录路径、同步性能探针校准。部分路径读取、缓冲隔离、原 Check 计数和分配语义均保持。
- 共 36/36，三轮均无失败/缺失/超时，源快照一致。没有重跑全工程；未改正式场景/地图资产，P2 仍需对全部群锚点和双向连接实测。

## P1d 小规划：场景直接绑定和有预算的导航成本缓存

- Extend `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphTargetBinding.cs`：显式构造直接目标引用、源场景身份、目标 Transform 下的导航锚点；显示中心和导航锚点分开，保留旧 TargetId/fallback 的兼容字段。群中心因成员完成而变化时，不让既有静态锚点漂移。
- Create `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphZoneBinding.cs`：Zone ID 到场景区域直接引用的数据项，独立于节点绑定。
- Extend `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphBindingAuthoring.cs`：Zone/节点/目标引用索引、重复/孤儿诊断，明确配置替换/重建入口和只读集合；直接对象匹配不依赖重复的运行时 TargetId。旧 ID 查询检测歧义，不静默取第一个。来源/活跃群的规范化及成员事实仍由 P3 的 ClusterResolver 负责，不塞进绑定索引。
- Create `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphNavigationCostService.cs`：共用 Profile/SegmentQuery，烘焙 profile/指纹/边方向/锚点一致才载入成本；缺失、过期成本进入有界补算队列，每条边最多两个有向查询。失效立即撤下旧成本，补算发布新只读快照，旧快照不变。首版两方向都完整才作为双向可走连接，不回写资产。
- Reuse `AgentNavigationProfile.cs`、`AgentNavigationSegmentQuery.cs`、`MapGraphCostSnapshot.cs`：不复制 NavMesh 长度算法或图最短路。生成器全对查询也调用同一测量入口，运行时只补已保存图的边。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphBindingTests.cs`，Extend `tools/agent-repro/cases.json`：重复 TargetId 的直接对象辨认、重复/孤儿绑定、区域引用、锚点移动、成本快照载入零查询、profile/指纹失效、补算预算、双向路径失败及旧快照隔离。

缓存失效不等于立即全对重建；服务只提供显式失效和有限锚点复核入口，P3/P5 的安装/路线层选择调用时机。P2 负责采集实际场景 profile、挑选合法锚点和完整有向查询证据。P1d 不尝试通过群中心的大半径投影掩盖无可行锚点。

## P1c 实施结果

- 完成版本化 SO、Zone 矩形/中央安全区、节点局部布局/稳定源身份、单横/竖段的端点留白/人工来源/样式、行列锁及禁连记录、独立导航烘焙 profile/双向有效性。旧资产保留 schema 0，未自动迁移；正式图 schema 2 由明确 Editor 事务写入。
- Service 增补 Zone 索引和局部转全图坐标，复制图索引后不随原 SO 的新替换变化；显示平移不改变导航路径成本。未知/重复 Zone、缺少归属和未来 schema 明确诊断。
- `Logs/AgentReproduction/20260912-195835-658`：初版 7/7 PASS。审查增加嵌套 JSON 覆盖后的只读缓存刷新、显式失败不能保留有效数值成本，按规划默认值将近邻数设为 4。
- 最终 `Logs/AgentReproduction/20260912-200036-277`：MapGraphDefinition 8/8 PASS，包含 Unity 资产实际保存/卸载/重读、GUID 保持、空 Zone、人工位置锁/删线/端点样式、失败方向、原子替换及旧资产只读加载。
- `Logs/AgentReproduction/20260912-200218-429`：MapGraphPathfinding 10/10 PASS。最终 18/18，无缺失/失败/超时，源快照一致。数据层不引入 NavMesh、Agent、场景引用或生成算法；没有新地图画面，截图从 P2 初次生成开始。

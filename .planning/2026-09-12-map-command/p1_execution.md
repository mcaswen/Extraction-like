# P1 图数据和纯路径

基线：`453d793`。状态：P1a 实现、测试和审查完成；P1 后续子阶段待执行。P0 的静态场景证据已归档，P1/P2 继续核实真实导航路径，不能用静态组件统计代替可达性。

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

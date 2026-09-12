# 地图指挥实施架构审查

## P0 场景和契约基线

- 复用既有 SC00 和保留 Editor，未引入第二套启动/审计框架；Unity 解析的 Prefab 引用与实例覆写是场景证据来源。
- 规范图节点身份与运行时 TargetId 分开。已定位群 Prefab 的重复 ID，规划中的场景直接引用和 GlobalObjectId 可避免布局指向随机运行 ID。
- 保留 EnemySpawnPoint 和 Source/ActiveCluster 的出生、注册、完成所有权；记录来源配置优先于出生点直接引用，不在地图层创建敌人。
- 明确静态审计不能证明全部导航可达，锚点/双向路径验证移交 P1/P2 的共用查询，避免误报。空 Zone 保留，既有两个 Missing Script 仅记录，不无依据删除。
- 本阶段只改文档，不存在新增生产耦合或公共接口改变。SC00 证据 PASS，源输入未变；并未宣称新地图功能或整局验收通过。

结论：P0 完成；后续阶段继续检查职责边界、程序化验证及实际截图。证据见 `p0_execution.md` 和 `scene_baseline.md`。

## P1a 纯图拓扑和成本

- `IMapGraphCostProvider` 隔离成本策略，`MapGraphCostSnapshot` 仅复制边 ID/方向/数值，不依赖 Agent、Targets、NavMesh、UI 或 Editor。旧配置成本在明确兼容适配器中保留，正式导航数据不会混进显示线长。
- `MapGraphService` 保有图索引、拓扑诊断和只读邻接表；`MapGraphPathfindingService` 只搜索，工作区实例私有、不支持并行重入，返回结果与工作区隔离。未给 Overlay/AgentPawnRoot 堆入图算法。
- 同成本取点依固定 Ordinal 索引；零成本环不写回已访问节点。先校验图及端点再允许空路径完成，负数/NaN/Infinity 成本被屏蔽，不可达不返回部分可执行序列。
- 测试使用独立简单路径枚举核对 500 个组合，并验证旧图、坏拓扑、方向和别名。没有调用同一 Dijkstra 计算预期结果，也没有用测试驱动推进生产路线。
- 10 个定向用例全部通过，既有投影调用编译兼容。改动仅位于已确认 Runtime 文件边界及 Editor 测试/清单，没有生产场景/Prefab 改动。当前阶段没有可见样式变化，不重复截图旧地图。

结论：P1a 审查通过，继续 P1 的序列化/共用导航能力。

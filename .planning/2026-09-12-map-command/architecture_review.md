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

## P1b 共用导航查询

- 导航 profile 和数值结果拆为各自的数据文件，SegmentQuery 只执行采样/路径/长度测量，旧 Query 保留 readiness 和 arrival。没有把路径计算塞进图算法、生成器、PawnRoot 或 UI。
- Profile 独占复制的成本数组；公开结果不带可变 Path；原生路径及角点由调用方 Buffer 独占。旧 CombatApproach/NavigationMotor 通过兼容入口继续读取部分路径和诊断，未引入静态全局路径缓冲。
- live 查询继续使用 Agent.CalculatePath，静态查询使用完整 profile filter，避免丢失每角色区域成本。地面角点到达语义保持，fallback 的 baseOffset 包含实际缩放。
- 测试独立构造高成本区域、断岛、坡面和错误楼层，验证实际路径改变及数值长度；原导航、真实场景记录路径和部分路径交战共 36/36 PASS。原 Profiler 计数仍一调用一样本，预热段查询不分配。
- 新增 API 没有改变移动或任务所有权，文件边界符合 P1b 小规划；无资产替换、无可见样式变化。P1c 数据结构先记录规划，后续生成/运行态仍由各自模块负责。

结论：P1b 审查通过，继续 P1c。

# P0 场景和契约基线

日期：2026-09-12。状态：静态基线审计和审查完成。基线 `fcb6df1`。用户已确认大规划，要求实施中持续截图检查地图效果和自然程度。

## 小规划和文件归属

本阶段重新运行现有 SC00 场景审计，读取真实 Prefab、实例覆写、Zone/群/出生和 NavMesh 配置，整理规范地图节点、绑定与初始化契约。先复用既有证据工具，不创建第二套 Unity 启动器，不为审计先替换生产地图。

- Reuse `tools/agent-repro/Invoke-SceneRaid.ps1`、`SceneRaid.EditorSession.psm1`：隔离同步、已有 Editor 会话、请求/结果交接和源码哈希。
- Reuse `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidSceneAudit.cs`：在 Unity 解析场景对象、Prefab 覆写、引用和组件；不靠场景 YAML 的 PrefabInstance 条目猜测最终配置。
- Reuse `Assets/Scripts/Gameplay/Targets/Authoring/TargetZoneAuthoring.cs`、`EnemySourceClusterAuthoring.cs`、`ActiveEnemyClusterAuthoring.cs`、`Assets/Scripts/Gameplay/Enemy/EnemySpawnPoint.cs`：核对规范群身份、出生尝试/注册时序和完成事实。
- Create `.planning/2026-09-12-map-command/scene_baseline.md`：归档本次真实静态场景和配置、P1 接入点及待导航查询核实的边界；此文件不是运行时数据配置。
- Extend 本文、大规划实施结果：记录实际审计/验证结果和必要的阶段调整。

## 验证和阶段界限

审计证据必须来自当前源快照，确认 Editor 返回空闲、源文件未变。核对静态群数量、来源/活跃群对应、重复 ID、缺失绑定和单向导航配置；发现配置问题先定位真实 Prefab/覆写再决定修正。P0 不宣称新路网或截图已完成，当前还没有新地图可见产物；首版生成布局从 P2 起实施持续截图。

完成本阶段后写架构审查并提交文档/所需审计补充，再进入 P1；如现有审计缺少导航锚点查询能力，明确补充计划或将相关验证纳入负责导航查询的 P1，不能把未检查项写成通过。

## 实施结果

- `Logs/SceneRaid/20260912-191826-063`：SC00 证据 PASS、gameErrors=0；Editor PID 11628 返回空闲并保留，sourceUnchanged=true。该模式仅静态审计，报告明确为 NOT_FULL_RAID_VALIDATED。
- 8 个 Zone、28 个静态群、30 个出生点、32 箱、2 撤离点、2 Agent。层级和 Zone 列表双向绑定错误为 0；空 Zone-渔村2 保留为区域，不生成假群。
- 28 群只有 3 个不同 TargetId，重复来自 Prefab；直接对象绑定/GlobalObjectId 生成稳定 nodeId 属于必要能力，不依赖 Play Mode 自动改 ID 后才保存地图。
- 唯一 NavMeshSurface、agentTypeID=0、GenerateLinks=false，有明确烘焙资产；当前没有 NavMeshLink/OffMeshLink 组件。完整群间可达/距离需 P1/P2 的导航查询，现有 SC00 不支持该证据，已明确前移到相应能力验收而不新增重复审计系统。
- 核对来源群的实际敌人 Prefab 配置和 Spawn 的优先级，非空来源槽位均为 Enemy/Pawn；场景不含旧 RoomUIManager/AIIntentController 或 MapGraph。
- 详细配置、范围和遗留问题见 `scene_baseline.md`。本阶段没有生产或场景修改，没有新的可见地图可供截图。

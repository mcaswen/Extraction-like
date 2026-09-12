# 地图指挥实施架构审查

## P0 场景和契约基线

- 复用既有 SC00 和保留 Editor，未引入第二套启动/审计框架；Unity 解析的 Prefab 引用与实例覆写是场景证据来源。
- 规范图节点身份与运行时 TargetId 分开。已定位群 Prefab 的重复 ID，规划中的场景直接引用和 GlobalObjectId 可避免布局指向随机运行 ID。
- 保留 EnemySpawnPoint 和 Source/ActiveCluster 的出生、注册、完成所有权；记录来源配置优先于出生点直接引用，不在地图层创建敌人。
- 明确静态审计不能证明全部导航可达，锚点/双向路径验证移交 P1/P2 的共用查询，避免误报。空 Zone 保留，既有两个 Missing Script 仅记录，不无依据删除。
- 本阶段只改文档，不存在新增生产耦合或公共接口改变。SC00 证据 PASS，源输入未变；并未宣称新地图功能或整局验收通过。

结论：P0 完成；后续阶段继续检查职责边界、程序化验证及实际截图。证据见 `p0_execution.md` 和 `scene_baseline.md`。

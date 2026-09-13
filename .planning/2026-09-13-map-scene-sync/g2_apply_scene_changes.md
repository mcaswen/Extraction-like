# G2 将场景身份变化同步到地图草稿

2026-09-13。状态：实现、42 项定向验证和架构审查完成。用户明确说明删除群、修改归属是其场景编辑，要求编辑器同步；此前关于是否保留删除的歧义已消除。补齐原地图大规划第 202、209 行要求的场景同步能力，不以关闭身份校验解决问题。

## 目标和规则

当前只检测 NodeSynchronizationRequired，没有应用差异入口。以当前加载场景为群身份、归属、类型和名称的依据：缺失群和区域从草稿移除，只移除端点已不存在的边/禁连；存活节点、区域和人工线的身份、布局、样式和锁保留。换区群映射到新区域，保留其在原区域内的相对位置；新增群按世界相对位置放到对应地图区域，位置可能仍需作者用网格调整。用户世界物体不被移动，已发布 SO/Binding 不被直接改写。

自动检测场景层级和保存事件，合并后列出同步记录，支持 Undo；打开窗口和生成/微调/校验前也同步。同步只写独立草稿，场景快照变化、错误场景、无效采集或过期请求均拒绝。导航/几何验收仍在生成和发布阶段；无效草稿继续保留。重复同步应无变化，无新增后台逐帧扫描。用户撤销地图同步后，不因同一个场景事件立即反复重做同步。

## 文件边界和依赖

| 判断 | 具体文件 | 职责 |
| --- | --- | --- |
| Create | Assets/Scripts/Editor/MapGraph/MapGraphSceneSynchronizationResult.cs | 输入快照、原草稿指纹、合并结果和逐条变化记录，只读提案，不写资产 |
| Create | Assets/Scripts/Editor/MapGraph/MapGraphSceneSynchronizer.cs | 纯场景身份合并和新/换区节点位置规则，清理失去端点的边、禁连；不采集导航、不持久化 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorDocument.cs | prepare/apply 的版本和快照复核，Undo 写草稿、清除旧导航证据，不放宽普通摆放的身份门控 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorWindow.cs | 同步入口、场景事件去抖、生成前编排、同步记录展示，保留当前编辑器和草稿 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphDiagnosticFormatter.cs | 同步变化的可读提示 |
| Reuse | Assets/Scripts/Editor/MapGraph/MapGraphSceneCollector.cs、MapGraphLayoutGenerator.cs、MapGraphGridPlacement.cs | 真实 Prefab/场景身份采集、新节点默认配置、行列规范化 |
| Reuse | Assets/Scripts/Editor/MapGraph/MapGraphAuthoringTransaction.cs、MapGraphGenerationController.cs | 完整生成校验和正式发布，不直接写无效图 |
| Create | Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphSceneMergeTests.cs | 构造删除、增群、跨区、重命名、端点清理、锁样式保持、错误场景/过期拒绝、Undo/恢复和重复幂等 |
| Extend | Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorCanvasTests.cs、tools/agent-repro/cases.json | 真实场景删除/换区后同步、生成、发布、重载，截图与测试登记 |

依赖继续为 Window → Document → 纯合并/采集，正式写入仅经原事务。新增能力属于既有 Editor 同步模块，不变更运行时图 schema、Agent 指令或路径语义。

## 验收和审查

Agent 在 Regression 隔离副本构造，用户不需跑 Play Mode。明确验证用户示例的“一个群删除，一个群从雨林移到实验室”；源地图不被同步直接改写，剩余作者数据保持，重复同步不新增 Undo。加入真实场景变更、发布重开以及必要的原文档回归。原场景只读核对，未保存的用户编辑不以磁盘旧版本覆盖。完成后记录结果、截图和架构审查，中文提交。

## 实现结果

实施前接口补充：同步后，旧已发布工作图可能包含已删除群，不能再拿旧图做 Shift 连线的导航补证据。Extend MapGraphGenerationController.cs、MapGraphGenerationResult.cs 的 Editor 生成模式，增加 NavigationEvidence：使用当前草稿身份只扫描真实导航，结果只能供连接验证，不允许应用为工作图或保存。Document 的应用/发布入口显式拒绝此模式。原几何生成和发布合同不放宽，避免“同步后必须先自动生成成功才能手画线”的循环依赖。相关 guard 纳入构造验证。

验证中调整：用户最新已保存正式场景为 27 群。MapGraphGenerationControllerTests 的正式场景用例生成成功，但旧数量断言硬编码为 28，运行 20260913-194231-665 中 12/13 通过。该断言改为逐项核对捕获的真实 nodeId/zoneId 集合，保留生成不得漏群的严格要求；新增正式场景只读同步核对，确保每个群的来源、归属和类型一致，源 SO 和场景不被测试写入。真实窗口补验层级/保存事件自动同步，以及生成前捕获字段改名。

最终结果：MapGraphSceneMerge 9/9、MapGraphEditorCanvas 6/6、MapGraphGenerationController 13/13、MapGraphEditorDocument 14/14，共 42 项通过。正式保存场景只读采集为 27 群、7 区域，准确合并删除的 222710 群、3 条失去端点的边，以及 3e5541 群从雨林到实验室的归属和层级变化；源 SO 和场景未被测试写入。

真实窗口用例通过层级/保存事件自动同步删除与换区，在生成前同步字段改名，再完成摆放、生成、发布、重载。已检查实际截图，深色画布、区域、群和横向连接保持原风格，同步记录可逐项阅读。保留一次旧数量断言失败及最终 XML、轨迹、截图，详见 [验证报告](../../outputs/map-grid-authoring/scene-sync/validation_report.md)。测试范围仅为编辑器同步及其发布依赖，没有重复运行游戏 Play Mode 或宣称新的运行时 FPS 结果。

最终边界符合原规划：纯合并无导航和 UI 依赖，文档负责版本/Undo，窗口负责事件与展示，正式保存仍归原事务。自动同步仅在事件后 0.3 秒去抖执行，没有新增逐帧全场景扫描；运行中或尚有待审预览时延后。同步不解决作者草稿本身的几何冲突，新区域/换区落点可能仍需网格调整。未保存的新对象需先取得稳定身份。审查见 [architecture_review.md](architecture_review.md)。

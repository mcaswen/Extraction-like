# G6 允许连线穿过区域名称框

2026-09-13。状态：实现、65 项定向回归和架构审查完成。用户明确允许线穿过名称框，沿用既有地图模块和自主实施、验证、提交授权。

## 目标和边界

手动添加连接、固定位置生成、有限微调、全图布局及补线统一允许横竖连线穿过区域名称框。名称框继续避让群图标，线仍须避让非端点群，保持横竖、端口、留白、导航和发布检查。无需修改场景、正式地图资产或运行时数据结构。测试使用独立 Regression 工作区，不覆盖用户正在编辑的草稿。

## 文件归属

| 判断 | 文件 | 职责 |
| --- | --- | --- |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphValidation.cs | 删除 EdgeThroughName 拒绝条件，保留图标和其他几何约束 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphZoneLayout.cs | 名称空位只检查群图标，不接收连线数据 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphLayoutCoordinates.cs | 适配内部接口，删除仅为名称避让分配的线段数组 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphDiagnosticFormatter.cs | 删除不再产生的 EdgeThroughName 诊断文案 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorCanvas.cs | 明确图标避让提示，区域名称在线条之后绘制，保持可读；不拥有校验规则 |
| Extend | Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphLayoutValidationTests.cs、MapGraphPlacementConnectionsTests.cs、MapGraphShortcutTests.cs、MapGraphLayoutSolverTests.cs、MapGraphDiagnosticTests.cs、MapGraphEditorCanvasTests.cs | 将旧名称阻挡用例改为允许穿过的正向验收，保留图标阻挡等反向验证，补充锁定布局及实际窗口截图 |
| Extend | tools/agent-repro/cases.json、README.md | 同步用例目录和当前操作说明 |
| Reuse | Assets/Scripts/Editor/MapGraph/MapGraphEditOperations.cs、MapGraphPlacementConnectionPlanner.cs、MapGraphShortcutGenerator.cs、MapGraphEditorDocument.cs | 继续通过统一校验器验证连接，复用草稿、Undo、导航和发布链路，不创建第二套规则 |

依赖维持编辑器编排 → 纯布局/校验；运行时不依赖 Editor。此次修改是现有规则调整，无新增模块或重要架构变化。

## 验证和风险

运行 LayoutValidation、PlacementConnections、Shortcut、LayoutSolver、Diagnostic 和 EditorCanvas 定向组。验证名称穿线可接受且不强迫移动锁定布局，群图标遮挡名称、线穿群、斜线及不可达仍拒绝；真实窗口构造名称穿线，检查生成、绘制和发布路径，截图审查。只调整与旧规则冲突的断言，不降低其他验收。移除线段/名称嵌套检查和布局迭代分配，无运行时性能成本；不据此宣称 FPS 提升。

## 实现结果

两处限制均已移除：严格校验不再产生 EdgeThroughName，ZoneLayout 的名称候选只检查图标。LayoutCoordinates 删除仅供名称避让的线段数组，不保留无用参数或重复规则。Canvas 将名称文字放在线条之后绘制，更新底部提示；Runtime 无需改动。

最终 LayoutValidation 21/21、LayoutSolver 13/13、PlacementConnections 10/10、Shortcuts 9/9、Diagnostic 3/3、EditorCanvas 9/9，共 65 项通过。横竖穿线、锁定区域、固定位置人工边和自动边、补线、图标遮挡及不可达反向条件均覆盖。真实窗口经过场景增删同步后，Shift 添加穿名连接，再生成、保存、重载；程序核对同一边身份和名称框相交关系仍成立。12 和 09 两张实际截图已检查，名称保持居中，连接可见，深色样式保持。

窗口测试初次在异步导航补证据完成前断言，第二次使用延迟执行的嵌套枚举器读取了已被消费的任务。最终改为最长 10 秒等待草稿中实际出现连接，窗口组复跑通过。两次失败只调整测试时序，未放宽生产导航门控；其他五组生产源码及各自测试哈希未变化，沿用其已通过结果。失败和最终证据一并归档。

移除严格校验 O(E×Z) 的线/名称检查，以及布局每次坐标迭代的 O(E) 临时线段数组；区域名称候选仍在有界 49 个中心中检查图标。既有 28 节点交互预算用例通过，无新增运行时开销。本轮不宣称 FPS 结果。源场景、正式地图资产在所有验收快照中哈希一致，未发布用户草稿。

完整运行编号、XML、事件、截图和源码哈希见 [名称穿线验收](../../outputs/map-grid-authoring/name-crossings/validation_report.md)。架构审查记入同目录 architecture_review.md 第 7 节，无需调整既有模块方案。

# G5 已有连接不可见，Shift 只报重复

2026-09-13。状态：实现、41 项定向回归和架构审查完成。沿用地图作者模块、自主修复和中文提交授权。

## 问题与验收

截图中两群已垂直对齐，Shift 报 ConnectionAlreadyExists，但画布无线。源码链路确认：GridPlacement 保留旧边身份和 Axis；Geometry.TryGetVisibleSegment 在端点不符合旧 Axis 或留白吞掉线段时返回 false，Canvas 绘制和命中均忽略该边。AddEdge 判重仍读取真实拓扑，因此用户无法从画布选中这条边修复。两者读取同一 AuthoringLayout，问题是失效边完全没有编辑表示和重复选择入口。

补齐作者交互：Shift 选中两群已有的边，不增加重复边或导航查询；显示当前不可见原因。仍能横竖表示的失效边显示琥珀色警示虚线，可点击选中。对角或图标重叠不画斜线/折线，可通过 Shift 双选打开原边的属性、删除或重选端点。旧 Axis 与当前对齐不符时提供“按当前对齐修正方向”，只修边的方向元数据，保留身份、端点、人工来源、留白、线宽和颜色。透明样式在编辑器提供警示表示，源样式不自动改写。

不关闭判重、不删旧边绕过冲突，不改变正式图的横竖规则、场景群位置、导航成本或发布校验。修改当前草稿必须可撤销，未验证草稿不能发布。

## 文件归属和依赖

| 判断 | 具体文件 | 职责 |
| --- | --- | --- |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphGeometry.cs | 提供纯两点横竖方向判断，原严格可见线段/发布几何语义保持 |
| Create | Assets/Scripts/Editor/MapGraph/MapGraphEditorEdgePresentation.cs | Editor 专用只读表示：实际可画线段、警示原因和修正方向能力。独立于严格发布几何，不写模型、不查询导航，使用值类型避免逐边分配 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorCanvas.cs | 统一使用上述表示绘制、命中和诊断高亮；警示虚线不变成运行时连接 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorInspector.cs | 显示待修正数量和选中边原因，提供方向修正入口，继续通过现有文档编辑回调写草稿 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditorWindow.cs | Shift 对已有端点优先选中原边，直接反馈可读原因；新增和显式重绑仍走原连接验证 |
| Extend | Assets/Scripts/Editor/MapGraph/MapGraphEditOperations.cs | 显式按当前端点对齐修正 Axis，保留其他作者数据，不承担视图或导航职责 |
| Reuse / Extend | Assets/Scripts/Editor/MapGraph/MapGraphConnectionSelection.cs | 复用 Geometry 方向原语，保留原双选/重绑/版本失效规则 |
| Extend | Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorCanvasTests.cs、tools/agent-repro/cases.json | 构造轴失配、留白过大、透明、对角和重叠，检查绘制/命中一致、Undo/源隔离；真实窗口 Shift 选择、修正、删除再连接及截图 |
| Reuse | Assets/Scripts/Editor/MapGraph/MapGraphEditorDocument.cs、Assets/Scripts/Editor/MapGraph/MapGraphPlacementState.cs、Assets/Scripts/Editor/MapGraph/MapGraphValidation.cs | 现有草稿/Undo/发布门控，不增加第二套状态或放宽原规则 |

依据：已读 G1/G2/G3/G4、Scene Sync 的规划和审查、框架文档及 Geometry/Canvas/Inspector/Window/Document/EditOperations/Selection 邻近实现。依赖继续为 Window/Inspector/Canvas → 表示或文档，纯 EditOperations → Geometry；Runtime 无 Editor 依赖。

## 验证计划

先在 Regression 隔离项目构造“原水平边，移动后垂直对齐，线段不可见但判重命中”，保存修复前证据；然后检查警示线段与 Hit 一致，修正/Undo 后身份和样式不变，已有边双选只选中、不新建、不扫描导航。保留不对齐时禁止斜线的规则，覆盖留白/透明与端点重叠的反馈。真实窗口截图检查深色画布、警示表示和属性面板，最终执行已有生成、发布、重载链路。

按变更范围运行 Canvas、ConnectionSelection、EditOperations 对应组；发现实际依赖问题时才扩展。检查每条边表示为 O(1)，不增加导航、求解或逐帧场景扫描。结果和架构审查记录在本规划及同目录 architecture_review.md。

## 实现结果

修复前构造运行 20260913-202941-860 的 1 项窄筛选通过，证明原问题确实可复现：正式资产中 edge_cb16fe6e2d6138de2adb3f829b774c69 原为水平边，端点改为垂直排列后，严格线段返回 false，但 AddEdge 判重命中。这是确认缺陷条件的通过记录，不是缺陷修复通过。最终回归显式构造旧水平方向，不要求以后正式资产必须永久保留这个旧错误。

实现补充：Geometry 的原严格线段入口委托内部带方向/留白参数的只读重载，警示表示使用当前方向和零留白，避免每帧创建临时 Edge 对象。严格入口仍使用原边参数。为复核这个共享原语，最终范围加入已有 MapGraphLayoutValidation 21 项；其他组为 Canvas 9、ConnectionSelection 4、EditOperations 7，共 41 项。

最终 Canvas 9/9（20260913-203819-891）、ConnectionSelection 4/4（20260913-203857-576）、EditOperations 7/7（20260913-203927-613）、LayoutValidation 21/21（20260913-203949-408），共 41 项通过。正式资产端点复现、轴失配/留白/透明的可绘制和可命中一致性、对角/重叠不造线、修正后全部其他作者数据保持、Undo/Redo 和源 SO 隔离均已验证。

真实窗口通过 Shift 选择原边，重复选择不增加边、不推进文档版本或启动导航扫描；调用面板同一文档修正操作后，方向从水平改为垂直，保留线宽 3 和两端留白 4/7，再验证 Undo/Redo。随后完成既有删除重连、生成、发布、重载和场景同步闭环。已检查 10/11 两张实际窗口截图，警示连接可见、属性原因及修正按钮可读，修正后恢复原色实线。

证据归档：[验收报告](../../outputs/map-grid-authoring/hidden-connections/validation_report.md)。新增表示每条边 O(1)，只进行现有图的字典查找和数值判断；Inspector 待修正数量为 O(E)，无导航、求解或全场景扫描。严格几何的 21 项回归保持原拒绝标准。源地图和用户内存草稿未被测试覆盖，修正仍进入草稿，正常校验后才能发布。

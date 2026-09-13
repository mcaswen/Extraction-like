# G4 摆放冲突反馈修复

日期：2026-09-13。状态：实现、定向验证和架构审查完成。沿用已确认 G3 的“失败指出对应群、约束，保留草稿”要求，不改变布局、发布和运行时边界。

## 问题与目标

用户在校验、生成连接、微调后均看到 EdgeThroughName、EdgeThroughNode、NodeOverName、NonOrthogonalEdge。当前底部固定 76 像素的 HelpBox 直接拼接 ToString，错误被截断且只有哈希 ID；名称避让矩形没有可见边界，作者无法判断具体障碍。校验检查现有连线，固定生成丢弃非人工旧线但不移动节点，微调只在预算内搜索，因此真实摆放冲突可以同时导致三个入口失败。

另需修正失败诊断坐标上下文：微调失败目前报告搜索中 best 候选的冲突，画布仍显示原草稿，可能指向作者画面上不存在的冲突。失败应报告原摆放对应的问题，搜索改善程度作为独立文字保留。

## 文件职责

| 判断 | 文件（Assets/Scripts/Editor/MapGraph/） | 职责 |
| --- | --- | --- |
| Create | MapGraphDiagnosticFormatter.cs | 纯诊断文本、稳定 ID 到群/区域/连线端点名称的解析，中文处理建议；保留原始 Issue 供日志，不持有窗口或改图 |
| Extend | MapGraphEditorWindow.cs | 已有任务反馈编排增加滚动逐条诊断、定位按钮、过期清理；按任务输入快照解析，不重复校验或每帧扫描 |
| Extend | MapGraphEditorCanvas.cs | 名称避让矩形可视化，定位错误对象和相关障碍；不新增连线、移动群或放宽校验 |
| Extend | MapGraphPlacementConnectionPlanner.cs | 微调失败使用初始摆放的诊断，保留有界搜索统计；搜索策略和结果发布规则不变 |
| Reuse | MapGraphValidation.cs、MapGraphGeometry.cs、MapGraphLayoutIntentValidation.cs | 几何合同、相交与人工边保护；不关闭校验来使错误图发布 |

测试归属：新增 Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphDiagnosticTests.cs，负责四类冲突及名称解析、未知错误回退、定位只读；扩展 MapGraphPlacementConnectionsTests.cs 检查失败诊断确实对应原始摆放；扩展 MapGraphEditorCanvasTests.cs 的真实窗口闭环，触发失败、检查错误可读/定位/编辑后清理，输出截图。cases.json 只登记定向组。

## 验收

1. 构造四类几何错误，原始机器代码仍可追踪；用户面向的文字包含明确对象和建议。
2. 固定生成不移动节点，真实名称遮挡仍拒绝发布；旧自动斜线可重建，人工线仍保留约束。
3. 三个入口的失败都保持摆放，微调失败不把未接受候选当作当前布局报告。
4. 窗口逐条滚动、定位主体并显示相关障碍；Undo、拖动、新任务后不残留旧错误高亮。拖动和重绘没有新增 NavMesh/搜索。
5. Agent 在隔离 Regression 副本运行必要测试，实际查看错误截图。保留用户 Editor 和 Final 副本。本次提交按用户最新授权一并包含已保存的相关场景、NavMesh 修改，不改写其内容。

## 实现、测试与审查

追加定位（实施中 Review）：窗口撤销检查 20260913-190243-325 失败，进一步新增 MapGraphGridPlacementTests.UndoReturningToAnEarlierLayoutKeepsDraftAndWorkingGraphIndependent，20260913-190632-323 稳定失败。草稿撤销使未发布工作图节点 b 的位置从 (0,80) 变成 (0,0)。MapGraphPlacementState.SetDraft 只复制 List，内部节点、区域和边对象仍与工作图共用，Unity Undo 回填这些对象时跨越了原已确认的隔离边界。

小规划调整：Extend MapGraphPlacementState.cs，在接收布局时取得独立序列化数据副本；该文件本就拥有草稿序列化状态，修复不增加模块、运行时依赖或公共接口。Extend MapGraphGridPlacementTests.cs，通过 8 轮冲突→恢复→Undo/Redo 检查草稿和工作图分别正确，不用引用是否相同代替行为验证。新增范围属于修复原 G1 数据隔离承诺。

最终结果：诊断、线路、网格、文档、真实窗口共 39/39 PASS。保留 Undo 修复前失败 XML；最终窗口运行 20260913-190842-331，源数据哈希匹配。两个本轮截图已打开核对，详细结果和原始证据归档 outputs/map-grid-authoring/diagnostics/validation_report.md。

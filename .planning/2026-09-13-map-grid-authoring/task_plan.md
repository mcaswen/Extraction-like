# 网格摆放优先的地图编辑流程

日期：2026-09-13。状态：G1–G3 实现、自动验证和架构审查完成。沿用用户要求的“小规划→实现→定向验证/审查→提交”闭环。本次只改变地图作者操作，不修改运行时指令、群执行、导航或资产身份；用户正在编辑的场景、NavMesh 和验证副本保留。

## 1. 功能概述

把当前“每次拖群都立即求解旧路网”改成 **先在网格上摆群，再生成连接，必要时预览小幅调整，最后发布**。操作者先决定空间关系，算法辅助完成线路。

- 网格摆放：显示可调间距的背景网格、吸附位置和行列辅助线，拖动只改变选中群的地图位置，区域可以整体移动；不为保住旧边而联动其他群，不每次拖动调用导航或整图求解。
- 固定位置生成线：按操作者的摆放位置，只选择同一行/列、没有图标/名称阻挡且世界导航双向可达的连接；保留人工连接及禁连意图，尽量使实际可达分量连通。
- 画布选点连线：按 Shift 依次点选两个群，以选择顺序指定起点和终点；也支持普通点击起点后 Shift 点击终点。不从节点列表选择端点，水平/垂直方向按摆放位置确定。已有线通过“重选端点”进入同样的画布操作。
- 微调建议：显式按钮触发，以当前网格摆放为基准，在有限格数内移动未固定节点，预览前后位置和连线差异。取消不改变摆放，接受后才应用；无解时指出断开的群或冲突，不大幅重排。
- 草稿与发布：摆放阶段允许线路暂时失效，清楚标记“待生成线路”。有效线路生成并通过原正式校验后才允许保存到游戏使用的图资产；草稿支持 Undo/Redo 和窗口/编译恢复，不因没有连通就丢失摆放成果。

布局只改变小地图，世界物体不动。运行时依然读取已发布图；每条连接为单个水平或垂直直段，不增加斜线、折线、虚拟群或绕过 NavMesh 的假连接。

## 2. 源码依据和根因

已读：地图大规划、P2 作者会话和生成记录、框架职责文档、`MapGraphEditorWindow.cs`、`MapGraphEditorCanvas.cs`、`MapGraphEditorInspector.cs`、`MapGraphEditorDocument.cs`、`MapGraphEditOperations.cs`、`MapGraphEditOperation.cs`、`MapGraphLayoutGenerator.cs`、`MapGraphGenerationController.cs`、`MapGraphConnectionGenerator.cs`、`MapGraphLayoutIntentValidation.cs` 及相关测试。

目前 Canvas 拖动每 0.15 秒提交预览，MoveNode 默认锁定新位置却保留旧行列和所有边；EditOperation 为维持旧拓扑立即调用正交求解器，Document 还要求已有导航验证。因而“先摆群、暂时不管线”没有合适的数据状态，网格参数虽已有，但没有实际拖动吸附。

连线当前由 `MapGraphEditorInspector.ConnectionFields` 的起点/终点 NodePopup 和方向列表提供，新建与重绑共用这套列表；Canvas 另有端口拖线，尚无有序双选。新流程将画布有序选点作为统一端点输入，复用 `MapGraphEditOperations.AddEdge/RebindEdge` 的人工来源、禁连更新及样式保持，不新建第二套图修改逻辑。

注意数学限制：任意网格位置并不一定存在全图连通的横竖直边。例如两个节点既不同行也不同列，禁止折线时无法直接连接。因此“只生成线”保持位置并报告断连；“微调建议”才允许有限移动，不能偷偷放宽原连线要求。

## 3. 架构和文件职责

依赖方向保持：`Window/Canvas/Inspector → EditorDocument → Editor 草稿/纯操作/规划器 → 现有场景采集与导航快照`；发布复用 `AuthoringTransaction → SO + Binding`。Runtime、Agent 和 Raid 不反向依赖草稿，也不新增另一种路线执行语义。

所有下列路径相对 `Assets/Scripts/Editor/MapGraph/`，测试与工具另列。

| 判断 | 具体文件 | 职责 |
| --- | --- | --- |
| Create | `MapGraphPlacementState.cs` | Editor 专用可序列化草稿、布局阶段和网格设置，参与 Undo 与恢复；未验证草稿不成为运行时 SO |
| Create | `MapGraphGridPlacement.cs` | 纯网格吸附、单群移动、区域整体移动、行列重建和基本几何诊断；不查询导航、不求解旧线 |
| Extend | `MapGraphEditorDocument.cs` | 拥有摆放草稿和已验证工作图，版本隔离旧任务，应用/取消候选，阻止未验证发布 |
| Create | `MapGraphConnectionSelection.cs` | 画布临时有序双选，保存起点/终点、新建或明确重绑模式，负责取消/过期；独立于持久化图和算法 |
| Extend | `MapGraphEditorCanvas.cs` | 画网格/辅助线、吸附拖动、Shift 选点及起终点高亮、候选差异；仅提交编辑意图，不承担连通算法 |
| Extend | `MapGraphEditorWindow.cs` | 三步入口及工作阶段、当前工程/场景说明，编排后台有限计算，不修改真实物体 |
| Extend | `MapGraphEditorInspector.cs` | 网格间距、节点固定、微调范围、双选端点只读说明、明确重选端点入口及断连/冲突信息；删除连线起终点和方向列表，保留线宽/颜色/留白控件 |
| Reuse | `MapGraphEditOperations.cs` | AddEdge、RebindEdge、DeleteEdge、StyleEdge 继续拥有人工连接及禁连语义，新输入不绕过现有文档事务 |
| Create | `MapGraphPlacementConnectionPlanner.cs` | 按固定摆放选择合法横竖连接，有界搜索局部格点调整；输入冻结的导航测量，不在候选循环增加路径查询 |
| Extend | `MapGraphGenerationController.cs`、`MapGraphGenerationResult.cs` | 接入摆放生成模式/候选来源，沿用取消、输入版本和过期检查，不移除场景身份保护 |
| Reuse | `MapGraphConnectionCandidates.cs`、`MapGraphGeometry.cs`、`MapGraphValidation.cs`、`MapGraphNavigationValidation.cs`、`MapGraphLayoutIntentValidation.cs` | 原可达、四向端口、几何、人工意图与发布校验 |
| Reuse | `MapGraphAuthoringTransaction.cs`、`MapGraphNavigationBakeBuilder.cs` | 验证通过后更新现有图和场景绑定，保存失败回滚 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphGridPlacementTests.cs` | 吸附、单群独立移动、区域移动、Undo/恢复、草稿发布门控、无导航副作用 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphPlacementConnectionsTests.cs` | 固定位置连线、不可达/禁连/固定点、有限微调、无解、取消和输入过期 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphConnectionSelectionTests.cs` | 有序 Shift 双选、新建/重绑模式隔离、自环/重复/第三次选择、取消和版本失效；不模拟真实游戏点击 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorCanvasTests.cs`、`MapGraphEditorDocumentTests.cs`（同目录）、`tools/agent-repro/cases.json` | 新操作流程、窗口状态和真实画布截图，按改动登记定向组 |

采用“草稿→候选预览→正式提交”的已有事务思想；纯格点操作和线路规划独立，避免 Canvas 或 Document 再增加独立算法。不把一套脆弱的模式分支塞回原即时求解的 MoveNode。

## 4. 交互默认值与规则

1. 进入编辑器优先允许摆放已有节点，网格使用当前配置间距（现有默认 80 地图单位，可调），拖动落点就近吸附。拖动不会隐式把整个布局重新生成。
2. 旧边在布局阶段仅作为参考；失去横竖/端点合法性时标记待重新生成，不画成新的正式斜线。原游戏发布图在正式保存前保持不变。
3. “生成连接”固定全部摆放位置；若不能连接同一物理可达分量，保留草稿并列出断连。用户可手动再摆，也可点击“微调建议”。
4. 微调默认最多离当前摆放 1 格，可设置有限范围。明确固定的群/区域不动；候选有位移上限和计算预算，取消或失败不覆盖草稿。保持人工连线和禁连时无解，要指出对应约束。
5. 发布复用完整导航/几何合同。群或 Zone 身份增删仍需单独同步，不能以新摆放模式默默删除场景节点；此前验证副本删除群的意图仍未确认，本轮不自动处理。

### 4.1 Shift 选点连线（用户追加）

- 第一次选中的群标记“起点”，按 Shift 选择第二个群标记“终点”；支持连续两次 Shift 点击，也支持普通点击 A 后 Shift 点击 B，顺序固定为 A→B。Shift 选点不会误触发群拖动。
- 新建模式选满两个不同群后提交一次人工连线请求，水平/垂直方向自动判定，不再选方向列表。图本身仍为双向通路，起终点顺序只决定端口和两端留白，不新增单向导航语义。
- 未同行/同列时保留两个高亮并提示先对齐或使用微调建议，不偷偷挪动节点或生成斜线。已有连接、端口占用、穿图标/名称、不可达等失败说明具体原因，原图保持。
- 成功后选中生成的线，侧栏直接编辑线宽、颜色和起终点留白。选择失败后第三次 Shift 点击替换终点，可以继续尝试，不连成无限链；点击同一群不创建自环。
- 重绑已有线必须先选线并点击“重选端点”，再在画布选择两个群。普通 Shift 新建不因之前选中过某条线而意外改线；重绑成功复用原边 ID 和样式，失败保留原边。
- Esc 或点击空白取消临时起终点，切换场景/草稿版本、节点失效时清理过期选择。删除选中的线继续支持 Delete，人工禁连仍由原 DeleteEdge 记录；后续显式重新连接该对群时沿用 AddEdge 的解除禁连语义。
- 端点下拉列表从新建及重绑界面删除；画布端口拖线可作为兼容快捷方式，走同一个 AddEdge/RebindEdge 入口。两种输入都不直接保存 SO，也不增加第二种校验口径。

## 5. 阶段闭环和验证

| 阶段 | 范围 | 验收 |
| --- | --- | --- |
| G1 网格草稿 | Editor 状态、纯网格操作、拖动和 Undo/恢复，取消即时整图联动 | 拖一个群只改变其位置，负坐标/缩放吸附准确；世界、源 SO 和导航查询不变；无效草稿不能发布 |
| G2 线路辅助 | 固定摆放生成、局部微调、独立可达/几何/意图校验 | 固定模式零位移，每边横竖单段；真实可达分量连通或明确无解；微调不超格数、不移动固定点、不复活禁连 |
| G3 窗口流程和交付 | 三步入口、Shift 双选连线/明确重绑、断连提示、候选前后对照、保存重开和文档 | 自动驱动布局→生成→发布→重载；验证双选顺序、单次提交、自环/重复/不对齐拒绝、失败保留、Esc 和过期清理；起终点高亮及线样式画面由 Agent 打开检查 |

构造优先使用独立小图和测试拥有的 NavMesh，不依赖用户正在改动的正式场景。只在必要时运行正式图兼容/编辑后路线用例，不重跑全部搜打撤或性能矩阵。交互验收要求拖动期间无 NavMesh 计算/整图搜索，生成按既有短时间片推进，保留耗时/工作项计数，不用“没卡死”替代测量。

## 6. 实现与审查记录

用户以“ok，开始实现吧”确认方案后，已按 G1→G2→G3 完成实现及闭环。

| 阶段 | 状态 | 结果 |
| --- | --- | --- |
| G1 | 完成，提交 3a5d78c | 独立网格草稿、吸附、Undo/恢复、发布门控 |
| G2 | 完成，提交 30131c6 | 固定位置线路、有限微调、真实导航证据与候选版本隔离 |
| G3 | 完成，本次提交 | Shift 有序双选、明确重绑、三步窗口、暂存关闭、真实截图、发布重载、使用说明 |

最终定向验证 38/38 PASS：网格 5（20260913-182342-011）、线路 9（20260913-182408-333）、选择 4（20260913-182434-240）、文档 14（20260913-182500-240）、真实窗口 6（20260913-182834-300）。每次测试均在独立 Regression 副本，无需用户参与。

28 群构造图固定连线约 29.50 ms，35 个可让出的工作项，最长约 8.76 ms；该数据不含场景导航扫描。6 张最终窗口截图已实际打开检查并归档。最终 Review 修复了初次导航扫描阻塞摆放的问题，去除窗口旧的即时求解编排；未改变已确认的模块边界。

每阶段详细记录见 g1_execution.md、g2_execution.md、g3_execution.md；架构审查见 architecture_review.md。使用流程和归档证据见 ../../outputs/map-grid-authoring/validation_report.md。用户正在修改的场景、新旧 NavMesh 和 Final 验证副本保持，没有作为本轮编辑器提交的一部分。

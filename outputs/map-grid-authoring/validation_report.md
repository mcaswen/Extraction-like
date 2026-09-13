# 网格地图编辑验收

日期：2026-09-13。实现于主工程 `D:/Unity-Projects/Extraction-like`；自动测试使用独立 Regression 工程。

## 已完成的操作

1. 群在可调网格上吸附，拖动只改变选中群；区域平移携带成员，调整大小保持成员全图位置。鼠标拖动期间只画预览，释放时一次提交 Undo。
2. “生成连接”保持摆放位置，只选合法横竖直线。导航、图标/名称遮挡、四端口、人工连接、样式和禁连沿用原验证。
3. “微调建议”默认每个坐标轴最多移动 1 格，可调至 4 格，保留固定群、固定区域和行列锁。有限候选先考虑较小位移；检查预览后才接受，取消保持草稿。
4. Shift 按顺序选两群连线，成功后选中线编辑样式。起终点不再使用下拉列表。失败保留双选，第三次 Shift 替换终点，Esc 或空白取消；已有线需明确点击“重选端点”。
5. 草稿可暂时不连通，支持 Undo/Redo、编译恢复及同一次 Editor 会话内“暂存关闭”。有效预览应用后才可发布图资产和 Binding。

## 自动验证

| 组 | 通过数 | 主要证据 |
| --- | ---: | --- |
| MapGraphGridPlacement | 5 | 负坐标吸附、独立移动、区域平移/缩放、真实 Undo/恢复、源图隔离和发布门控 |
| MapGraphPlacementConnections | 9 | 固定零位移、导航岛、禁连、人工样式、名称阻挡、位移/锁/预算、28 群成本 |
| MapGraphConnectionSelection | 4 | 普通+Shift、双 Shift 顺序、失败替换终点、自环拒绝、明确重绑、取消/版本失效 |
| MapGraphEditorDocument | 14 | 原事务回归、真实 NavMesh 候选接受、Undo、来源冲突、输入过期、重连/重绑、仅补导航证据 |
| MapGraphEditorCanvas | 6 | 真实 IMGUI 拖动、Shift、失败反馈、草稿关闭重开、候选、保存后重载、实际窗口截图 |
| 合计 | **38** | 无需用户跑 Play Mode 或人工点击 |

原始 NUnit XML、组报告、成本事件和截图校验值归档在 [evidence](evidence/manifest.json)。正式场景只做已有地图的只读窗口外观检查；发布与重载使用测试拥有的场景、3 个群和 NavMesh 资产。测试核对源地图在发布前不变、世界 Transform 不变，未覆盖用户场景或新 NavMesh 工作。

## 成本和边界

- 3 群构造图连续 1000 次吸附移动合计约 **18.13 ms**；拖动事件本身不启动整图搜索或 NavMesh 查询。
- 28 群、7 区域构造图的固定连线计算约 **29.50 ms**，分为 **35 个工作项**，最长工作项约 **8.76 ms**。此数值是冻结导航矩阵后的线路算法，不包含场景扫描或 Unity 启动。
- 微调队列宽度 8，最多检查 512 个布局，原配置可进一步降低预算。未发现候选表示预算内未找到，不保证全局最优，也不等价于数学无解。
- 同一行/列不存在合法连接时，固定模式不挪节点、不增加折线/虚拟群。继续修改摆放或使用微调建议即可。
- 场景身份增删仍需核对同步，位置编辑不会静默删除旧节点。“暂存关闭”使用 Editor SessionState，不承诺崩溃或断电后的磁盘恢复。

## 截图检查

以下均为真实 Unity 窗口渲染表面，由自动用例捕获，Agent 已逐张打开检查。深色网格、矩形 Zone、中央名称和群图标保持原地图风格；绿色/金色分别标记起终点，位置参考线有专门图例。

![正式地图的网格编辑窗口](visual/06-project-map-grid-editor.png)

![Shift 双选不对齐时保留起终点及原因](visual/02-shift-alignment-feedback.png)

![微调建议预览，金色仅表示位置变化](visual/03-adjustment-preview.png)

![连接成功后的线条样式编辑](visual/04-shift-line-style.png)

另有 [初始网格](visual/01-grid-ready.png)、[发布并重开的地图](visual/05-published-reopened.png)。详细文件边界、闭环记录见 [规划](../../.planning/2026-09-13-map-grid-authoring/task_plan.md) 和 [架构审查](../../.planning/2026-09-13-map-grid-authoring/architecture_review.md)。

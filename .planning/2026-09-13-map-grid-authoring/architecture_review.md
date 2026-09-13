# 网格地图作者流程架构审查

日期：2026-09-13。当前结论：G8 已修复 G7 的场景交战阻塞和缓存未命中，12 项定向通过，两轮实际路线/终局合同通过；自主轮保留一次已恢复的 LostSight 原始诊断。下文保留各阶段历史证据。

## 1. 文件归属和依赖

- `Assets/Scripts/Editor/MapGraph/MapGraphPlacementState.cs` 拥有 Editor 专用可序列化草稿，Runtime SO 不存无效中间态。
- `MapGraphGridPlacement.cs` 只做坐标和行列处理，不查询导航、求解旧拓扑或写世界对象。
- `MapGraphPlacementConnectionPlanner.cs` 只消费冻结矩阵，复用 Candidates、Geometry、Validation 和 IntentValidation；固定连线和有限微调共用一条验收路径。
- `MapGraphConnectionSelection.cs` 只拥有临时有序端点和明确重绑模式，不实现另一份 Add/Delete/Rebind。
- `MapGraphEditorDocument.cs` 继续拥有作者事务、版本、Undo、工作图和导航证据。新增连接复用 EditOperations，失败不写草稿；只刷新导航证据不会清空恢复的摆放。
- Window 只编排任务、会话恢复和交互，Canvas 只显示及提出意图，Inspector 不自行写源 SO。发布继续调用原 AuthoringTransaction。

未改变 Runtime、Agent 根路线、目标身份或世界场景；没有由 Gameplay 反向引用 Editor。新增独立职责均放在规划指定文件，没有将搜索塞进 Canvas/Window，也没有复制持久化事务。

## 2. 正确性和保留规则

失效草稿不能发布。发布前必须经过有效候选、场景身份和导航指纹复核；生成、Undo 或新编辑使旧任务过期。固定算法零位移，微调受初始位置最大格数、节点/区域/行列锁和预算约束；人工边、样式、禁连由原规则独立验收。

Shift 的起终点来自画布顺序；底层新建仍拒绝同点、重复、不对齐及新增几何错误，旧图保持。G5 起，普通 Shift 选择已存在的端点对只选中原连接，不再次发起新建。普通新建不会重绑选中的旧边。显式重绑保留 ID 和样式，删除仍记录禁连，显式重连解除对应禁连。草稿已有几何错误可以保留，但新边自身错误不可借用基线错误通过。

## 3. 性能与限制

鼠标拖动只更新瞬态预览，MouseUp 一次提交 Undo，无 NavMesh 或搜索。网格重建主要为 O(N²)，绘制按缩放省略次级网格。线路候选约 O(N²)；每条尝试复用完整几何校验，固定图最坏约 O(N⁴)，通过逐边工作项让出执行，实际有效候选受同行同列和四端口限制。微调在此基础上最多检查 512 个布局，队列宽度 8；这是有限启发式，未找到不等于不存在解，不承诺全局最优。

未连通草稿支持 Undo、编译恢复和本次 Editor 会话内“暂存关闭”；不将 SessionState 描述为跨崩溃的磁盘备份。玩家地图只读取最终发布图；本轮不重新宣称新的运行时 FPS 或完整搜打撤结论。

## 4. 验证和资产保护

真实窗口闭环 6/6 PASS，6 张截图逐张检查；包括同一 Editor 会话内实际关闭/重开草稿，以及保存场景、重新加载图和 Binding。原始窗口输出为 Logs/AgentReproduction/20260913-182834-300。最终逻辑组为网格 5、线路 9、选择 4、文档 14，报告和成本事件归档 outputs/map-grid-authoring/evidence。28 群构造图耗时约 29.50 ms、35 个工作项、最长工作项约 8.76 ms；不包含导航扫描。

用户当前场景、删除的旧 NavMesh 以及新 NavMesh 资产保留为用户工作；未暂存进阶段提交。用户正在编辑的 Final 副本不进行目录同步。测试只使用独立 Regression 工作区。

## 5. G4 冲突诊断和 Undo 隔离补充审查

2026-09-13。小规划见 g4_diagnostics.md，最终 39/39 PASS。新增 MapGraphDiagnosticFormatter.cs 只负责纯文本与名称解析；Window 拥有任务输入快照、逐条列表和过期清理，Canvas 拥有名称框和定位高亮，不新增校验或世界查询。Planner 失败诊断回到原始摆放，不报告未应用候选；算法、锁定规则和发布标准保持。

新增 Undo 构造样例捕获 PlacementState 内部对象浅拷贝跨越草稿/工作图所有权边界。独立序列化副本归属原 PlacementState，不在 Runtime 增加草稿概念，也不引入全局深拷贝工具。补跑网格 6、文档 14、窗口 6，覆盖 8 轮 Undo/Redo 和发布重载，源码与最终运行哈希一致。原始失败与成功证据均保留。

未发现新的反向依赖、重复算法或资产直接写入。两张本轮实际截图已检查，诊断名称、说明、定位和名称避让框可见。按用户最新指示，本次一并提交其已保存的场景、NavMesh 修改；保留内容，未覆盖 Final 工作副本。

## 6. G5 隐藏连接和已有端点选择审查

依据：[g5_hidden_connections.md](g5_hidden_connections.md)。缺陷来自旧连接被严格绘制判断忽略，判重却正确保留拓扑，缺少可见的编辑入口。新建 `Assets/Scripts/Editor/MapGraph/MapGraphEditorEdgePresentation.cs` 独立拥有 Editor 只读表示，Canvas 的绘制、Hit 和诊断高亮共用同一表示；Inspector 仅展示原因和触发文档编辑。没有在严格校验器中返回“伪合法”线段，也没有让运行时读取警示表示。

`MapGraphGeometry.cs` 的内部方向/留白重载复用原裁剪算法，原公开严格入口仍传入原边参数。独立 `TryGetAxis` 供表示、Selection 和 EditOperations 共用。表示使用 readonly struct、常量提示和现有图查询，不逐帧创建临时 Edge；每边 O(1)，待修正计数 O(E)。

Window 只在普通 Shift 双选时只读查找既有连接，选择动作不写草稿、不查询导航；显式重绑与真正新增仍走原 Document 验证。`MapGraphEditOperations.AlignEdgeToNodes` 只改变 Axis，保持边 ID、端点、来源、长度和全部样式，通过原文档的独立草稿/Undo 提交。源 SO、场景及发布事务边界不变。

41 项定向通过，包括原严格几何 21 项；程序化核对轴修正以外的全部边数据相等。真实窗口完成选择、修正和撤销，再继续既有发布重载，已检查两张实际截图。对角或重叠不画斜线/折线；留白和透明颜色提供警示而不自动覆盖作者参数。结论：没有新增反向依赖、重复持久化或导航逻辑，符合既有模块边界。完整证据见 [隐藏连接验收](../../outputs/map-grid-authoring/hidden-connections/validation_report.md)。

## 7. G6 连线穿过名称框审查

依据：[g6_edges_through_names.md](g6_edges_through_names.md)。本次明确修改既有规则：线可穿名，群图标仍须避让名称框。`MapGraphValidation.cs` 拥有最终验收规则，`MapGraphZoneLayout.cs` 拥有名称空位搜索，两处同步删除线/名称阻挡，未在 Window、Planner 或 Runtime 增加开关和第二套校验。

`MapGraphLayoutCoordinates.cs` 删除失去用途的线段数组，内部 TryFit 不再接收连线；职责更明确，既有锁定、区域包含和节点分离保持。`MapGraphDiagnosticFormatter.cs` 删除已无生产者的报错文案。`MapGraphEditorCanvas.cs` 只改变文字绘制层次和帮助说明，不改连接端点、拓扑、世界对象或源 SO。

65 项定向通过，保留 NodeOverName、EdgeThroughNode、横竖、端口和不可达反向条件。真实窗口 Shift 穿名连接经导航补证据后完成，保存重载保持原边身份；测试用最终可观察草稿状态等待异步完成，未引入绕过导航的测试入口。两次测试时序失败及最终成功证据可追溯。已检查手动连线和重载两张实际截图。

性能变化仅减少 Editor 校验和坐标求解的检查、分配；没有新增导航、运行时扫描或跨层依赖。源场景和正式地图未修改。结论：符合已确认模块边界，不需要新系统或方案调整。见 [验收报告](../../outputs/map-grid-authoring/name-crossings/validation_report.md)。

## 8. G7 发布图验证审查

依据：[g7_published_map_validation.md](g7_published_map_validation.md)。新增 MapCommandPublishedGraphTests.cs 独立承担实际发布资产的只读验收，复用 SceneCollector、Binding、NavigationSegmentQuery 和预算成本服务。Floyd 仅作为测试中的独立最短路径对照，不进入运行时，也不替换生产 Dijkstra。测试成本为 O(N³ + N² 次路线查询 + E 次双向导航)，只在定向验收执行。

MapCommandPresentationTests.cs、MapCommandVisualTests.cs 用实际发布资产的完整身份集合替代旧数量断言，没有放宽漏节点/漏边检查。缓存是否零补算仍由未修改的 RuntimeCache 用例裁决，发布图功能审计记录初始失效原因，再验证原预算补算后的成本和路线，避免同一缓存失效派生数百条假不可达。两者证据和状态独立保留。

SceneRaid 继续复用原指令驱动、背包驱动、探针及合同；没有新增 Gameplay 诊断接口或更改玩家优先级。截图经过实际 SRP 渲染后采集，未用模型图替代。定向 12 通过、1 缓存失败；两轮真实场景均复现存活 Agent 在龙骨礁敌人群处理失败后无法继续，不能用采集 PASS 替代玩法 PASS。正常战死和容量撤离按已确认规则处理。

没有新增 Runtime→Editor 依赖、重复持久化、场景生成副作用或生产热路径开销。用户地图和场景原样保留，最终哈希对应运行输入；独立验证工作区不覆盖用户编辑副本。结论：测试实现通过架构审查，当前发布图的完整功能未通过，两个未修复问题及后续定位文件见 [验证报告](../../outputs/map-grid-authoring/published-map-validation/validation_report.md)。

## 9. G8 场景阻塞和缓存修复审查

依据：[g8_route_and_cache_fixes.md](g8_route_and_cache_fixes.md)。逐敌人探针证明问题属于场景中的辅助碰撞体，修复只关闭 fileID 959548491，父 Torus 的真实碰撞保持；没有把场景错误转成放宽射线、跳过敌人或增加路线重试。与 48bbaa3 的完整场景文本对照，除此以外无其他变化。

MapGraphNavigationGeometrySignature.cs 独立拥有纯三角网规范表示，不查询世界、不缓存 Agent、不写资产。它保留顶点索引共享关系，以坐标和有向邻接面内容排序，同坐标顶点不合并，仍有歧义时保守失效。审查中发现的坐标相同/连接不同反例和原生重复顶点排序问题均已补回测试，失败中间态保留。Fingerprint 继续拥有场景 Surface/Link 组合，CostService 的预算、profile/锚点失效和活动链接策略不变。

维护沿用 Editor 测试隔离边界。旧 ValidateOnly 产物因重新选择锚点被导入保护拒绝；新的显式维护入口只对保存绑定调用 CostService，原配置回写到隔离 SO，断言场景字节不变。导入逐字段核对，最终仅地图版本和导航证据改变，不影响作者布局、连接、节点身份或世界锚点。

SceneRaidEnemyProcessingProbe.cs 在独立自动验证文件内拥有失败诊断，原 RouteEvidence 只触发和写事件。探针有 Agent/群去重及全局上限，正常帧不增加查询，不修改实际指令/导航；非自动验证 Player 不编译该代码。定点测试恢复盒开关，正式保存态由一字段修复提供，没有偷偷改场景后再将结果称为原配置成功。

最终 12 项定向、1 项维护通过，52 方向/702 路线及两张真实截图已复核。SC02 双撤离结算、SC10 玩家脚本和正常死亡终态均满足独立路线/终局合同。自主原报告的一条 LostSight 计数保持，事件证明同群处理恢复及同根路线撤离，未放宽合同来变绿。当前修复范围完成，不把正常战死扩大成新玩法缺陷。

签名时间/空间有界，连续邻接缓冲避免逐顶点 List 分配，复杂度约 O((V+T) log(V+T))、空间 O(V+T)，仅沿用原启动/导航变更入口。正式测得一次 Capture 56.83 ms，有启动成本，不能将零边补算直接等同于整体性能提升。没有新的反向依赖、职责堆积或热路径扫描。结论：符合既有边界，审查通过。见 [最终验收](../../outputs/map-grid-authoring/route-and-cache-fixes/validation_report.md)。

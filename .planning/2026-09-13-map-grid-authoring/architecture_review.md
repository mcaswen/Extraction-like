# 网格地图作者流程架构审查

日期：2026-09-13。结论：G1–G3 完成，38/38 自动验证通过，文件边界和依赖符合已确认规划。

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

Shift 的起终点来自画布顺序；同点/重复/不对齐/新增几何错误拒绝，旧图保持。普通新建不会重绑选中的旧边。显式重绑保留 ID 和样式，删除仍记录禁连，显式重连解除对应禁连。草稿已有几何错误可以保留，但新边自身错误不可借用基线错误通过。

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

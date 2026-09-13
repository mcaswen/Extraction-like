# 场景身份报错诊断审查

日期：2026-09-13。当前结论：G2 同步实现通过审查和 42 项定向验证，用户最新说明已解除删除意图的歧义。下方首先保留 G1 历史审查，G2 结果见文末。

MapGraphGenerationController 只细化自己已有的冻结场景差异诊断，原缺失、来源、类型和区域校验不变。没有把同步或保存塞入生成控制器，也没有恢复已删实体、重写人工连接或清空图资产。

MapGraphSceneSynchronizationTests 增加正式绑定和真实 Unity 采集的只读对照，报告完整节点身份；仍在隔离 Regression 副本执行，退出不保存场景。实际主项目 1/1 PASS。生成控制器原有缺失反例增补可读信息断言，1/1 PASS；没有为修诊断跑完整游戏或性能矩阵。

错误根因由打开进程的 projectPath、实际保存的场景 YAML、Binding 空引用和主项目运行对照共同证明。用户当前编辑的 Final 副本已成为需要保留的工作输入，不允许沿用旧 Runner 的整目录同步覆盖。原始两份场景和差异有本地备份；后续迁回主项目、保留删除或恢复群应根据用户意图单独执行，当前不代替用户作选择。

## G2 场景同步实现审查

范围依据：[g2_apply_scene_changes.md](g2_apply_scene_changes.md)。本次用户要求把已删除、换区的场景变化同步到地图，采用当前场景作为身份依据，保持已确认的 Editor / Runtime 分层。

- **职责归属**：新建 MapGraphSceneSynchronizer 只做身份合并、相对位置映射及失效端点清理，结果数据独立放在 MapGraphSceneSynchronizationResult。复用 SceneCollector、LayoutGenerator 和 GridPlacement；未复制场景或导航采集，也不引用窗口诊断格式器。
- **依赖和状态**：Window → Document → 纯合并/采集。Document 在 prepare/apply 间复核版本、草稿指纹、场景和导航快照，拒绝外部源资产冲突、错误场景、身份冲突和过期提案；通过现有 PlacementState 独立副本与 Undo/恢复保存草稿。原 WorkingDefinition 和源 SO 不被同步直接改写。
- **导航与发布**：当前同步草稿可用 NavigationEvidence 扫描导航，避免旧已发布地图的缺失节点阻塞手画线。该模式显式不证明布局可发布，TryApplyGeneration 和保存证据入口均拒绝；正式线路生成、校验和 AuthoringTransaction 的写入边界保持。相应拒绝路径由构造测试覆盖。
- **作者数据**：存活节点和区域保留人工位置、锁、图标配置和样式，换区按归属变更映射相对位置；只删除失去端点的边/禁连，保留其余人工连线。重复合并幂等，世界位置变化不重置手工布局。同一场景输入不会因 Undo 后的事件立即重新应用同步。
- **调度**：只在打开、场景事件或生成前采集；场景事件 0.3 秒去抖，生成运行或待审预览期间延后。没有新增 Runtime Update 或逐帧全场景扫描。未把 Editor 的事件同步耗时冒充游戏 FPS 验收。
- **验证**：42 项通过，正式场景 27 群和 7 区域逐项核对；真实窗口自动完成场景变更、同步、生成、发布、重载，检查 2 张最终截图。旧正式场景测试写死 28 群的断言改成真实 ID 集合相等，保持不漏群的约束。证据见 [同步验收](../../outputs/map-grid-authoring/scene-sync/validation_report.md)。

结论：符合原地图模块架构意图，无新增 Runtime 依赖或图 schema 变更。同步后的几何冲突继续留在作者草稿中，通过现有网格调整及线路校验处理；本次没有发布或覆盖用户当前内存中的地图草稿。

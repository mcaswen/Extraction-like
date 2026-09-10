# P5 小规划：交付文档与最终架构审查

## 文件归属

- Extend `README.md`：从游戏工程完成度更新指令执行、反击/撤离、空间交战和已实现的无人值守回归入口；保留现有资产盘点、菜单/仓库等尚未接通的产品边界。
- Extend `Assets/Docs/GameplayAgentFrameworkDesign.md`：替换已过期的直线移动/直接扣血兜底、目标优先级、撤离身份描述；记录 Commands/Navigation/Targeting 的实际文件和状态所有权。
- Extend `Assets/Docs/EnemySystemOverview.md`：更新候选扫描、原子绑定、眼点/枪口、三维弹体与已有招式规则，修正旧 prefab 路径及旧选择顺序。
- Extend `tools/agent-repro/README.md`：准确写明隔离运行、组和重复、图形自动启用、结果判读、场景构造边界以及无需用户跑 Play Mode。
- Extend 本目录 `task_plan.md`、`repair_design.md`、`architecture_review.md`、`p0_execution.md`–`p4_execution.md`：各阶段结果及采用的文件边界；历史拟定目录和实际合并/省略文件需明确区分，避免误称全部草案文件已创建。
- Create `outputs/implementation_validation_report.md`：F1–F7/R1–R5 到实际测试组/方法的验收映射、失败定位与修复、三次重复结果、原始报告入口、图形证据和未覆盖范围。
- Create `outputs/agent_repro_validation.json`：保存最终重复的逐例结果和原始 XML/manifest 哈希，便于只拿到仓库的审阅者核对统计；完整日志仍保留在 Logs。
- Extend `outputs/agent_target_execution_combat_review.md`：顶部补修复状态及验收链接，保留原审查作为修复前历史证据，F3 等已由用户决定的规则不继续列为未决 Bug。
- Copy 图形证据至 `outputs/feedback/`：保存渲染得到的原始 PNG，不重画/修改图片内容。

## 验收

所有正式变更均已编译并完成对应测试；同一最终生产代码完成三轮全量运行。文档区分夹具错误、已复现生产 Bug、用户规则变化和已修复结果。检查无业务反向引用 Editor 测试、无源工程/正式存档被运行器改写、无未归属新模块。按阶段提交，最终工作区干净，不推送远端。

## 实际结果

- P4 已提交 `c840e20`。最终批次 `20260911-030638-823`，75 例 × 3 共 225/225 通过；报告层 7/7 通过。
- README、两份系统文档、主规划/修复设计和原审查已按实际代码更新，保留资产盘点与菜单/仓库产品缺口。旧的直接伤害/直线移动兜底、旧选择顺序和单主角撤离描述均已修正。
- `outputs/agent_repro_validation.json` 保存 225 次逐例结论、42 份原始 XML 哈希及 manifest 哈希；导出时再次校验全部 4,958 个源输入文件一致。
- 四张正式提示 PNG 已由 Agent 读取，原样保存在 `outputs/feedback/`，没有重绘。原审查明确标为修复前历史证据，并链接最新验收。
- 架构审查、元数据/GUID、文档链接、差异空白检查通过；无 Gameplay/VFX 反向引用 AgentReproduction/NUnit。P5 只回写文档和证据，未再修改已验证的生产行为。
- P0–P5 工作完成；本阶段以 `docs: record completed agent repairs and automated validation` 提交。所有提交仅在本地 dev 分支，未推送远端。

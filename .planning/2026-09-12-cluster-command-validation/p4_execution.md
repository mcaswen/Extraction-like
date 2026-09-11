# P4：冻结输入后的最终矩阵

## 范围、文件和标准

P3 故障闭环后冻结源文件哈希，按 `p0_target_matrix.json` 的 11 个槽位运行：六个 4×/731 基础脚本，MC02/MC03 同种子重复，MC01-R/1731，MC02 的 1× Editor、MC01-R 的 1× Player。MC01-X 墙钟 300 秒，其余 4× 480 秒；1× 最多 600 秒。保留常驻 Editor，Player 显示窗口并自行退出。

Reuse `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1`，构建复用 SC07。Create `outputs/cluster_command_validation_report.md` 和 `outputs/cluster_command_validation.json`：分别面向阅读和机器追溯，列阶段提交、每局输入/证据哈希、步骤覆盖、原始失败/预期拒绝/额外失败、终态和库存数量核对。Extend `tools/agent-repro/README.md`：补运行命令和报告口径。规划及架构审查回写现有文档，不修改其他任务的 active_plan。

终局平均 FPS >60 是 1× Editor/Player 性能标准，4× 和尾部帧耗时仅诊断；全部图形帧、源码未变、计数器、运行错误和命令反馈都需完整。已提交任务不能仅 Accepted，没有发生的前提独立列覆盖缺失，正常死亡单列。若最终矩阵中再次确认真实缺陷，返回 P3 修复并按影响重跑，不复用失效版本的旧绿灯。

## 最终结果

尚待 P3 完成后填写。

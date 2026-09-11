# P6：最终矩阵、报告和交付审查

## 文件归属与验收

- **Reuse `Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1`、`SceneRaid.Report.psm1`、`SceneRaid.Contracts.psm1`**：继续同一常驻 Editor 的正式场景和可见 Player 验证。目标由游戏选择，自动化只操作背包。
- **Create `outputs/scenezl_final1_validation.json`**：保存有限矩阵、逐轮玩法结局、终态/仓库契约、性能、完整输入和原始证据 SHA256，区分历史失败与当前有效证据。
- **Create `outputs/scenezl_final1_validation_report.md`**：解释工程完成状态、修复链、热点前后对比、最终 1× 帧率、真实覆盖和未解决限制。
- **Extend 本目录 `task_plan.md`、`p5_player_verification.md`、`architecture_review.md`**：回写大规划完成情况和最终审查。此阶段不再修改 Gameplay，也不把文档生成器移入运行时代码。

矩阵采用同一修正后场景/Gameplay：731 三轮 4×，1731/2731 各一轮 4×，731 的 1× Editor 和 1× Player。P5c 只改变自动化协议/分类，完整源码哈希存在不同版本时明确记录，另对排除 Automation、Editor 测试、tools/agent-repro 的其余全部输入验证一致，不仅比较场景文件。

每轮必须证据完整、正常结束、零程序错误/失败指令/停滞，全部撤离或经严格核对的正常战死均有效；至少一轮真实出现完整搜打撤与全部已定义覆盖。1× Editor 和 Player 平均 FPS 均严格 >60，尾帧只诊断。Player 必须真实 4K / High Fidelity 渲染、源码与二进制未变、正常退出。历史退出/刷新故障及稀有 NoProgress 不因为当前通过而声称已修复。

## 结果

待最终矩阵结束后记录。

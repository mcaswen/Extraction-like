# P4：冻结输入后的最终矩阵

## 范围、文件和标准

P3 故障闭环后冻结源文件哈希，按 `p0_target_matrix.json` 的 11 个槽位运行：六个 4×/731 基础脚本，MC02/MC03 同种子重复，MC01-R/1731，MC02 的 1× Editor、MC01-R 的 1× Player。MC01-X 墙钟 300 秒，其余 4× 480 秒；1× 最多 600 秒。保留常驻 Editor，Player 显示窗口并自行退出。

Reuse `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1`，构建复用 SC07。Create `outputs/cluster_command_validation_report.md` 和 `outputs/cluster_command_validation.json`：分别面向阅读和机器追溯，列阶段提交、每局输入/证据哈希、步骤覆盖、原始失败/预期拒绝/额外失败、终态和库存数量核对。Extend `tools/agent-repro/README.md`：补运行命令和报告口径；Extend 根 `README.md` 的联调验收和文档导航，同步工程完成度结论，不承载测试算法。规划及架构审查回写现有文档，不修改其他任务的 active_plan。

终局平均 FPS >60 是 1× Editor/Player 性能标准，4× 和尾部帧耗时仅诊断；全部图形帧、源码未变、计数器、运行错误和命令反馈都需完整。已提交任务不能仅 Accepted，没有发生的前提独立列覆盖缺失，正常死亡单列。若最终矩阵中再次确认真实缺陷，返回 P3 修复并按影响重跑，不复用失效版本的旧绿灯。

## 最终结果：已完成

P3i `9d7195a` 和 P3j `5e89d48` 已提交，新物理组 14 项、相邻回归 80 项、容量组 4 项和报告反例 79 项通过。第六版矩阵以 `5e89d48` 冻结，原 11 个槽位及 SC02 **全部通过**；没有修改场景脚本、种子、期限和死亡规则，原始索引为 `p4_runs.json`。

第六版 7 局双人撤离、4 局正常死亡且幸存者结算；原始 Failed、额外失败、运行错误、停滞均为 0。MC02 同种子重复完整 7/7、MC03 两轮均 8/8。4 个自然缺口分别是 MC01-E/enemy-far-1、MC01-X/exit-near-2、MC04-N/next-valid-task 和 reject-completed；保留各自死亡/提前收尾/前提期限原因，正式构造覆盖对应的首次接近、重复、改令与完成拒绝，不冒充实局触发。

SC02 `082842-992` 严格自主回归通过，双人结算、12 个库存会话。首次启动 `082742-077` 被 Editor 空闲保护拒绝，没有同步或进入 Play Mode；确认同一 PID 11628 新 idle 心跳后从 SC02 继续。内部短暂心跳失效机制仍未证明，未绕过或放宽保护。它和第五版的启动拒绝均保留为基础设施记录。

1× Editor `083021-981` 双人撤离，整局 **106.24 FPS**，活动区间 105.75，暂停 110.71；1× 可见 Player `083437-670/PlayerRun` 一人正常战死、另一人结算，整局 **214.56 FPS**，活动区间 213.72，暂停 225.03。Player 39356 个采样帧、39355 个真实渲染帧，容量 600000，证据完整并正常退出。两轮均在 4K / High Fidelity / FFT 关闭下达到平均大于 60 的门槛，保留启动及尾部慢帧。常驻 Editor 11628 保留。

追加 [P4b](p4b_command_cost_probe.md) 成本测量，13/13 和 120 次正式提交通过，提交 `d61c0e4`。它只新增 Editor 测试、meta 和登记清单，逐文件哈希证明运行代码未变，场景矩阵仍明确使用 5e89d48。原实局没有单次 GC 字节数据；校准拒绝失效的 Mono 字节 API，按准确分配次数记录，不伪造字节。

交付：[阅读报告](../../outputs/cluster_command_validation_report.md)、[机器报告](../../outputs/cluster_command_validation.json)。机器报告逐项核对原 11 槽位、全部源清单一致、脚本 SHA、终态/仓库、计数器、帧完整性和 1× FPS；另按精确 NUnit 名称关联 14 个相关组的 177 项分阶段 Passed 记录，追加的 13 项成本校准单独归档。集合范围、覆盖缺口、旧失败和测量限制完整保留。

收尾检查通过：报告统计、UTF-8、Markdown 围栏及本地链接有效，git diff --check 无内容问题。链接检查按 URI 转义解码路径，未将 README 中的 `%20` 空格编码误判为缺文件；常驻 Editor 实时状态为 idle，测试 Player 已无运行进程。

## 先前版本和返回修复的记录

第五版基线 `a6e97aa`：前 9 个 4× 槽位通过，SC02 自主回归双人撤离、10 个库存会话通过，1× Editor 双人撤离、整局 107.50 FPS 通过。但最后 1× Player 两人在同一雨林撤离点反复避让，触发器只保存一个 Collider，实际进度被相互清除；产生 94 次 NoProgress、18 次停滞嫌疑，600 秒仍未结算。162232 个渲染帧又超过 100000 帧采集容量，因此该局证据和玩法均 FAIL，不计算验收 FPS。已返回 [P3i](p3i_shared_extraction_presence.md)、[P3j](p3j_frame_capacity.md)，不能将前面绿灯替代完整验收。

前五版依次保留于 `p4_runs_superseded_e590671.json`、`p4_runs_superseded_b6519cb.json`、`p4_runs_superseded_0c2bdae.json`、`p4_runs_superseded_9d52840.json`、`p4_runs_superseded_a6e97aa.json`，分别保存 2、9、4、2、11 槽位，未覆盖或删除历史失败。

槽位 1 `071622-119` PASS/COMPLETE。启动槽位 2 的 `071756-289` 被保留 Editor 空闲保护拒绝，未同步文件或进入 Play Mode，没有生成配置/运行报告，不能算一次游戏失败或种子重抽。随后读取同一 PID 11628 的实时 idle 心跳确认已空闲；仅在 Logs 下的阶段编排脚本增加已通过前缀和提交身份校验，从槽位 2 继续。运行入口和 Gameplay 源码未改，第一次保护拒绝的内部时序尚未确认。

P3d 修改了共享导航执行，因此最终基线额外安排一局 SC02 / 731 / 4× 自主回归，使用原严格自主契约，验证 ManualCluster 的预期终止分类没有泄漏到自主模式。该局是受影响回归，不替换或增加原 11 槽位的覆盖分母。原有常驻 Editor 和隔离保存流程保持。

交付前按 `cases.json` 的完整 NUnit 名称核对阶段 XML：ClusterCommandReachability 10/10、ClusterCommandTransition 31/31、ClusterCommandInventory 11/11、SceneCommandHarness 19/19 均能在实际 Passed 记录找到。合并红绿运行时按唯一名称计数，不把失败后单例复跑重复计数，也没有因 TestFilter 标签而漏掉参数。

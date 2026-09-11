# Scenezl_Final 1 自主搜打撤验收报告

日期：2026-09-12。最终有限矩阵 **7 轮通过**：5 轮全员撤离，2 轮为经终态和结算核对的正常战死。各轮均零程序错误、零失败/拒绝指令、零停滞疑点，证据完整，源码输入未变。

正常速度完整回合：Editor 平均 **95.23 FPS**，独立 Player 平均 **236.22 FPS**，均满足用户最新要求的平均 FPS >60。结论限于下列配置、种子和已运行路径，不代表任意场景或无限运行都没有问题。

## 配置和自动化边界

- 场景：`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`；Unity 2022.3.62f2c1，3840×2160 / High Fidelity，DX11，Ryzen 9 9950X3D / NVIDIA GeForce RTX 5090 D。
- FFT 由用户关闭，三处场景 inactive 改动保留在工作区，未纳入修复提交。小地图优化已按用户要求取消。
- 4× 只验证逻辑，1× 验证正常时序和帧率；物理步长保持原游戏值。背包搜索使用正式 unscaled 时间，打开时按原规则暂停。
- 固定种子用于约束随机输入，NavMesh 避让和帧时序仍可能变化，不将相同种子当作逐帧确定性重放。
- 游戏 Agent 自主选择目标、移动、战斗、反击和撤离。测试只负责正式焦点、背包交互、搜索等待和合法转移，没有整局目标指令、位置/血量写入或伪造结算。
- 战死是正常玩法结果，不因战死进入修复。只有确认每个角色已撤离或死亡、存活者结算正确，才记 EXPECTED_DEATH；死亡不会掩盖异常或冒充全员撤离。
- Player 为 Development 构建，显式显示窗口，无 Profiler 连接、无 VSync/帧上限，采集实际 Game 相机渲染回调。Editor 复用同一进程，Player 自行正常退出。
- 已检查真实关卡、角色和 HUD 的渲染画面：[4K Player 截图](../Logs/SceneRaid/20260912-023645-651/PlayerRun/render-check.png)。画面检查不代替逐帧渲染和玩法契约。

## 最终矩阵

| 运行证据 | 模式 | 种子 | 结局 | 墙钟秒 | 背包会话 | 结算物品数量 |
| --- | --- | ---: | --- | ---: | ---: | ---: |
| [20260912-022333-491](../Logs/SceneRaid/20260912-022333-491/report.json) | Editor 1× | 731 | 正常战死，撤离者保留结算 | 212.30 | 12 | 8 |
| [20260912-022815-586](../Logs/SceneRaid/20260912-022815-586/report.json) | Editor 4× | 2731 | 两人撤离 | 63.74 | 9 | 22 |
| [20260912-022940-945](../Logs/SceneRaid/20260912-022940-945/report.json) | Editor 4× | 731 | 正常战死，撤离者保留结算 | 71.99 | 12 | 8 |
| [20260912-023121-096](../Logs/SceneRaid/20260912-023121-096/report.json) | Editor 4× | 731 | 两人撤离 | 65.91 | 10 | 18 |
| [20260912-023254-005](../Logs/SceneRaid/20260912-023254-005/report.json) | Editor 4× | 731 | 两人撤离 | 67.97 | 10 | 18 |
| [20260912-023449-874](../Logs/SceneRaid/20260912-023449-874/report.json) | Editor 4× | 1731 | 两人撤离 | 80.50 | 14 | 27 |
| [20260912-023645-651/Player](../Logs/SceneRaid/20260912-023645-651/PlayerRun/report.json) | Player 1× | 731 | 两人撤离 | 167.90 | 9 | 23 |

最终矩阵在 P3l 后重新冻结输入，所有选入轮次的完整源码/资产指纹一致。优先复核曾出现阻挡的正常速度 731，种子集合和轮数保持不变。之前的绿色回合、战死回合和失败回合均保留为历史证据，没有混入最终次数。逐轮输入、原始文件 SHA256、两名角色最后观察到的血量/位置和结算集合见 [机器报告](scenezl_final1_validation.json)。

## 逻辑和资产完成情况

| 范围 | 已完成内容 | 证据/设计 |
| --- | --- | --- |
| 场景装配 | 按层级收集 Point/LootBox，修复范围线，补 Zone 注册和绑定，统一 RaidFlow 所有者；当前有两个有效撤离群 | [场景修复](../.planning/2026-09-11-scenezl-final1-autonomous-raid/hierarchy_repair_execution.md) |
| 敌人 Prefab | 5 个来源群的 11 个误填引用改为对应真实 Pawn，未删除槽位或敌人；实际 30 个出生点、30 个敌人全部归属来源群 | [P3i](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3i_enemy_prefab_binding.md) |
| 搜刮和满包 | 真实会话按 Agent/资源/命令隔离，处理搜索和转移，满包由 Agent 自主撤离，箱内余物保留 | [P3](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3_execution.md) |
| 结算和存档 | 修复旧物品定义缺失、仓库布局和写盘提交顺序；成功保存后才标记撤离，失败不丢物品；死亡/撤离顺序均能终结回合 | [完成契约](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3_completion_contracts.md) |
| 目标和交战 | 具体敌人优先解析，跨高差仍按真实距离/视线开火；有效反击目标保持，反击结束恢复同一原撤离指令 | [反击修复](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3h_retaliation_progress.md) |
| 导航 | 完整可达攻击点、路径缓冲和查询缓存；高 baseOffset 的资源解析沿用 Agent 绑定起点，真正断开的区域仍拒绝 | [P3g](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3g_combat_approach.md)、[P3j](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3j_resource_navigation_origin.md) |
| 箱边双人阻挡 | Agent 1 场景导航半径从误填的 1.5 恢复为 Prefab 的 0.5，匹配实际身体；两种速度的双人构造先失败后通过 | [P3k](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3k_resource_avoidance.md) |
| 同箱停靠空间 | 正常尺寸的两名角色仍可能挤在相邻目标点；候选加入实际导航身体的占位检查，复用缓冲并继续扫描后备点 | [P3l](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3l_resource_occupied_approach.md) |
| 特效 | 复用 Mud/Robot 攻击特效缓冲，明确停用、重启和销毁归属 | [P4g](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4g_vfx_buffers.md) |
| 测试工程 | Editor 请求与完成标记分开写入，避免旧轮覆盖新轮；保留式运行、渲染验证、退出期限、独立存档及 SHA 校验 | [P5c](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p5c_request_and_outcomes.md) |

每轮初始快照核对 30 个已注册敌人；矩阵中自然覆盖两名角色移动、搜刮、真实敌人掉血、背包暂停恢复、满包撤离、受击反击后恢复撤离。覆盖逐项关联原始 runId，不要求反击恢复与全员生还恰好在同一轮。仓库逐 ItemID 核对数量，检查布局、越界、重叠和定义；每次背包关闭核对箱内剩余物品与会话守恒。没有要求清空整张地图或击杀所有 Boss。

## 性能证据

以下是各阶段相同条件下的 CPU Marker 每帧均值；Total 包含子调用，不能相加当 Self，也不能与用户最初单帧的 85.43/15.55/10.69 ms 直接计算优化比例。FFT 开启阶段与关闭阶段分别比较。

| 指标 | 优化前 ms/帧 | 优化后 ms/帧 | 变化 |
| --- | ---: | ---: | ---: |
| Discovery Update | 1.3593 | 0.1218 | -91.0% |
| 两个 Pawn Update 合计 | 0.4249 | 0.2923 | -31.2% |
| 八个 Zone Update 合计 | 3.3873 | 0.9218 | -72.8% |
| 无 FFT：42 群 Cluster LateUpdate | 1.0320 | 0.3561 | -65.5% |

前三项通过限制昂贵查询、复用缓冲和缓存几何降低工作量。范围普通刷新最多每 0.05 实际秒一次，成员状态和死亡隐藏仍逐帧执行；单次算法复杂度没有因此变成常数。阶段原始条件、失败状态和复杂度见 [首次性能治理](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4_performance_first.md)、[LateUpdate](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4d_cluster_lateupdate.md)、[复杂度分析](../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4_complexity_analysis.md)。

| 最终 1× | 平均 FPS | P99 ms | 最大帧 ms | 1% Low FPS | 实际渲染/采样帧 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Editor | 95.23 | 19.13 | 474.00 | 42.17 | 20060/20061 |
| Player | 236.22 | 8.05 | 1365.26 | 71.47 | 39331/39332 |

按采样 timeScale 分类的诊断均值：Editor 模拟运行 93.90 FPS，背包/终态暂停 107.36 FPS；Player 模拟运行 236.01 FPS，背包/终态暂停 238.31 FPS。该分类不增加新的性能门槛。

平均 FPS 使用完整采样区间所有可计算帧间隔，包含正常背包暂停，仅首帧没有前驱间隔；没有删掉中途慢帧。机器报告额外按 timeScale 记录运行/暂停帧的诊断均值。P99、1% Low 和最大帧保留诊断，不作为用户当前验收门槛。采样和日志开销包含在实测中，不能将平均通过解释为完全没有卡顿。

## 自动测试与诊断

按改动范围执行真实 Play Mode 构造，阶段结果详见机器报告及各小规划：P3l 的 3 项占位边界、6 项旧箱边对照通过，新增双人持续到达的 1×/4×最终复核通过，37 项相邻导航、性能缓存、库存和位移回归通过。此前反击相关 21 项、导航交战 52 项、范围刷新 18 项、VFX 8 项、库存/持久化 17 项、请求协议 5 项通过，会话探针 9 项、终态/仓库探针 44 项、报告探针 78 项通过。计数包含不同阶段的重叠回归，不能相加当一次完整测试总数；本次没有机械重跑全部历史用例。P3j/P3l 的夹具修正及首次失败在阶段文档中保留。

快照包含角色 Transform/缩放、当前/挂起指令、路径角点、实际/期望速度、停滞计时、离目标距离、敌人血量/护盾/视线/射击结果，普通每秒一次、战斗每 0.25 秒一次。失败时额外记录附近 Collider/NavMeshAgent/Obstacle 和调用栈。`hasEnemy`、`hasResource` 等有效标记必须先检查，不能将 JsonUtility 默认零值当作真实数据。

复跑入口见 [自动验证说明](../tools/agent-repro/README.md)。编码 Agent 已执行 Play Mode、背包和 Player 验证，用户无需再手工操作。

## 保留的问题和范围限制

- 历史 Editor 原生退出/AssetDatabase.Refresh 挂起尚无确定根因；请求交接故障已独立修复，不能用它解释所有原生挂起。Editor 按用户要求保留。
- 箱边双人阻挡分别由 P3k/P3l 独立复现和修复，仍不把所有历史瞬态都归为同一原因。另一次瞬时 Search/Unreachable 未稳定复现；P3j 高 baseOffset 缺陷有明确红/绿构造，但不是该瞬时失败的因果证明。最终矩阵未再观察到这些失败。占位检测覆盖带实体碰撞体的导航角色，无碰撞体的外部角色不在当前 helper 范围内。
- 场景仍有两处缺失脚本（Spline 对象），重复 TargetId 自动重生成、TerrainCollider/出生点 NavMesh 回退等告警仍如实保留，不能将零程序错误说成零告警。
- 结论限于当前机器、4K 高画质、Development Player、指定种子和自然路线，未覆盖所有 Boss、全部区域、发行构建或其他硬件。

架构审查和阶段闭环见 [审查记录](../.planning/2026-09-11-scenezl-final1-autonomous-raid/architecture_review.md)、[大规划](../.planning/2026-09-11-scenezl-final1-autonomous-raid/task_plan.md)。

# Agent 自动复现与回归

编码 Agent 负责构造、启动 Unity、读取结果、定位修复和复跑。用户无需搭场景、点击 Play Mode、拖装备或收集日志。

```powershell
# 完整验收：所有组，同种子重复三次
./tools/agent-repro/Invoke-AgentRepro.ps1 -Mode Regression -Suite All -Repeat 3

# 定位某个子系统
./tools/agent-repro/Invoke-AgentRepro.ps1 -Mode Regression -Group EnemyTargets

# 基础环境检查
./tools/agent-repro/Invoke-AgentRepro.ps1 -Mode Regression -Suite Smoke
```

## 环境与隔离

运行器读取工程版本并核对 Unity 可执行文件版本，当前为 `2022.3.62f2c1`；复用 Test Framework `1.1.33`。在带所有权标记及独占锁的 `D:/Unity-Projects/.agent-repro/AnomalySearch/Project` 副本运行，保留 Library 缓存。可用 `-UnityPath`、`-WorkspaceRoot` 覆盖位置。

副本使用独立 company/product 和 persistentDataPath，源工程输入在运行前后按 SHA256 核对。只回收自己启动的进程，不操作用户编辑器或正式存档。运行期间请勿修改 Assets、Packages、ProjectSettings 和 tools；快照不一致会报告环境失败。成功及失败副本和每次证据均保留。

测试通过预定义 Editor 程序集发现，命令行平台为 EditMode；协程实际进入 Play Mode，运行真实 NavMesh、Physics、MonoBehaviour。隔离副本禁用 Domain Reload，并在每例末尾检查完成标记，防止协程重载造成假通过。没有增加业务 asmdef，也不是独立 Player 构建测试。

## 分组

| Group | 数量 | 覆盖 |
| --- | ---: | --- |
| Smoke | 1 | 发现、运行态、导航、物理与存档隔离 |
| F1 | 2 | 撤离受击反击后恢复、无伤害对照 |
| F5 | 1 | 静态不可达拒绝 |
| Lifecycle | 6 | 重复伤害、新命令、取消、死亡、资源受击、恢复点失效，反击交接锁和正常搜索完成 |
| Navigation | 8 | 零/小容差、坡面、导航丢失与无进展 |
| R5 | 2 | 位移后重新验证背包交互 |
| Feedback | 1 | 接收/拒绝结果、原因与反馈生命周期 |
| EnemyTargets | 16 | 七类正式敌人换人、失效变体、五类巡逻候选 |
| RangedSpatial | 12 | 双方高低差、枪口、三类弹体与薄墙 |
| Perception | 5 | 范围、射线、完整候选与范围技能 |
| Decision | 6 | 成员距离、实际防御、风险、撤离后备、失败目标短期排除，容量撤离 |
| Cooldown | 8 | 属性/装备/配置刷新、技能重排、重复 SkillId、普通攻击锁 |
| Combined | 10 | 多 Agent、实际撤离、动态路径、无效输入、护盾和缺失攻击配置 |
| Graphics | 1 | 正式顶部反馈 prefab 的成功/失败/消退 PNG |
| ScenePerformance | 11 | Discovery 范围预筛，路径查询次数及所有权，资源和轮廓缓存失效，20 Hz 范围刷新及即时死亡隐藏 |
| SceneInventory | 9 | 正式背包搜索、旋转/空间/策略、堆叠和整理，双 Agent 会话归属，容量自主撤离 |
| SceneCombat | 6 | 地表追击、断开高台拒绝、具体成员绑定、腐蚀粒子初始化、只读失败邻域探针 |
| SceneApproach | 7 | 断开导航面可达射击位置、伤害反击后恢复、隔墙/超程拒绝、只读查询和独立缓冲 |
| SceneVfx | 8 | 经分配校准的正式特效热路径、线条独立/点数变化/端点、禁用攻击清理、重新启用材质 |
| SceneStorage | 8 | 正式掉落写盘重载、异常页和特殊格、保存回滚、结算失败终态、平面物品证据 |
| SceneTerminal | 4 | 先死亡后撤离、先撤离后死亡、双人同帧和顺序撤离的正式终态 |

清单以 [cases.json](cases.json) 为准，共 132 例。`-Suite Core`、`Risks` 自动包含 Smoke；`All` 包含全部。图形组根据清单自动启用图形设备，其他组默认 `-nographics`；`-IncludeGraphics` 强制所有选中组保留图形设备。图形测试从真实 Camera/Canvas 导出 PNG，由 Agent 读取检查。

## 结果与定位

`Logs/AgentReproduction/<run-id>/` 保存 `manifest.json`、`summary.json`、`report.md`，`groups/<group>-<repeat>/` 保存 Editor.log 和原始 NUnit XML。`cases/<完整测试名哈希>/<repeat>/` 保存 `trace.jsonl`、`case.json`、`nunit-final.json`，图形用例另保存四张 PNG。

NUnit XML 是最终通过/失败权威，清单中缺失的测试不会算通过。`case.json` 是生命周期中的即时快照，最终结论读 `nunit-final.json`。默认每组进程硬期限 2700 秒（含导入），可用 `-TimeoutSeconds` 调整；用例内另有墙钟期限，暂停不会无限等待。

- Regression：任一行为失败返回 1；缺失结果/测试、超时或环境故障返回 2；全部通过返回 0。
- Diagnose：0 仅表示完成证据收集，行为失败仍在 XML 和报告中，不能解释为游戏无 Bug。
- `-TestFilter` 支持临时窄筛选，此时不核对该组完整清单，不能作为全量验收。

故障探针：`-Suite Smoke -FaultProbe Assertion` 故意断言失败；`-Suite Smoke -FaultProbe Timeout -Repeat 2 -TimeoutSeconds 30` 仅挂起首次，用于验证回收和第二次继续。正常回归不用这些参数。

`./tools/agent-repro/Test-AgentReproReport.ps1` 独立构造 7 类 XML/进程结果，检查正常、异常退出、断言失败、Diagnose、缺失、超时及清理失败。它不启动 Unity，不计入 132 个游戏用例。

实际证据和覆盖边界见 [验收报告](../../outputs/implementation_validation_report.md)。

## 目标场景层级修复

`./tools/agent-repro/Invoke-TargetHierarchyRepair.ps1` 在隔离副本中运行 7 项定向 EditMode 测试，再修复 `Scenezl_Final 1.unity`，保存并重载验证；检查源输入没有变化后回写场景和范围线共享材质。结果位于 `Logs/TargetHierarchyRepair/<run-id>/`，包含原始 XML、实际渲染 PNG、对象绑定、修改记录和修复前备份。失败时保留日志，不把未经验证的场景写回。

编辑器菜单 `Tools/Gameplay Targets/Repair Active Scene From Hierarchy` 提供同一修复能力，支持 Undo，修改后场景保持 dirty 供正常保存。工具按最近所属层级收集 LootBox/撤离点/出生点，修正 Zone 双向绑定，补齐 LineRenderer 和缺失材质，复用正式轮廓算法；不每帧扫描，也不在导入或普通游戏启动时自动修场景。
# Scenezl_Final 1 原场景诊断

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/agent-repro/Invoke-SceneRaid.ps1 -Case SC00
powershell -NoProfile -ExecutionPolicy Bypass -File tools/agent-repro/Invoke-SceneRaid.ps1 -Case SC01
powershell -NoProfile -ExecutionPolicy Bypass -File tools/agent-repro/Test-SceneRaidReport.ps1
```

SC00 在 Unity 展开正式场景，记录 Prefab 来源、实例覆盖、SO/对象引用、成员与缺失脚本。SC01 自动进入正常 Domain Reload 的图形 Play Mode，完全零输入观察 60 秒，保留自主指令、每秒快照、连续帧和初始化日志。输出在 `Logs/SceneRaid/<runId>/`；`report.json` 的 `evidenceStatus=PASS` 只表示采集完整，`gameStatus` 单独报告问题，`performanceAcceptance` 当前始终为 false。

现在默认保留隔离 Editor：完成后退出 Play Mode、卸载测试场景，报告 `editorLifecycle=EDITOR_RETAINED`、`processExitCode=null`。下次同一命令复用同一 PID，先确认空闲，再同步文件、等待编译和进入下一轮；各轮 runId、存档产品名、输出独立。用户可以自行关闭编辑器，后续发现原 PID 已退出时才新建进程。全会话原生日志在 `Logs/SceneRaidSession/`，每轮 `Editor.log` 只截取对应字节段。

仅在明确调查退出问题时使用 `-ExitEditor`，此模式仍检查退出码和超时。默认保留模式超时也保留进程供检查，缺少有效清理标记仍失败。`-ObserveSeconds 10` 可构造短生命周期复跑，只验证会话，不算完整回合或性能验收。保留的工作区不能再交给 NUnit 批处理覆盖；独立回归用 `Invoke-AgentRepro.ps1 -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchRegression ...`，或者在用户关闭后使用原工作区。会话所有权、忙碌/过期状态和日志分段可通过 `Test-SceneRaidEditorSession.ps1` 验证。

用户最新性能目标为 **60 FPS**，4K 高画质保持，测量仍不限帧。平均、滑动 1 秒和 1% Low 至少 60 FPS，P99 不超过 16.667 ms，卡顿上限 33.333 ms；报告明确记录目标和预算。旧 120 FPS 报告不覆盖，门槛调整及退出调查见 [P4e 记录](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4e_60fps_shutdown.md)。

SC02 使用 **4×** 逻辑速度，默认 120 秒墙钟上限；SC03 使用 **1×**、360 秒上限，用于正常速度和性能复核。正式 UI 暂停后恢复各自速度，物理步长保持不变。驱动只操作焦点、背包和物品，Agent 自主选择目标，容量不足时自主撤离，剩余物品留在箱内。

当前常驻场景 Editor 使用 `-WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchScene`，NUnit 使用独立 `AnomalySearchRegression` 工作区。旧工作区的卡住 Editor 保留供用户关闭，不对忙碌项目强制同步。

```powershell
./tools/agent-repro/Invoke-SceneRaid.ps1 -Case SC02 -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchScene
./tools/agent-repro/Invoke-SceneRaid.ps1 -Case SC03 -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchScene
./tools/agent-repro/Test-SceneRaidContracts.ps1
```

`-Seed 1731` / `-Seed 2731` 可覆盖不同掉落组合；默认仍为 731。显式 `-Profile` 打开普通 CPU Profiler，额外输出 `cpu-hierarchy.json`，记录主/渲染线程 Total、Self、调用数和已采样最慢 60 个线程帧的局部层级。每 0.25 秒读取最新两个相邻历史帧，覆盖 Editor/游戏帧，报告跳过的 Profiler 帧数；它用于抽样归因，不能作为 FPS 验收或完整慢帧清单。读取过程中暂停 Profiler，避免递归计量自身分配。诊断期间记录 Editor 工作，结束恢复该设置，保留 Editor 进程。

```powershell
./tools/agent-repro/Invoke-SceneRaid.ps1 -Case SC03 -Profile -ObserveSeconds 90 -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchScene
./tools/agent-repro/Measure-SceneRaidPerformance.ps1 -RunPath Logs/SceneRaid/<runId>
```

性能重算先校验完整 CSV，再取固定 10 秒预热后的连续区间，保留所有慢帧和相关事件，输出独立 `performance-warm10.json`；文件已存在时拒绝覆盖。`TIMING_THRESHOLDS_MET` 仅表示该轮时间指标满足，完整玩法、环境和重复矩阵仍单独验收。

`warehouse-initial.json`、`warehouse-final.json`、`item-definitions.json` 和平面 `inventory.ledger` 支持独立数量/布局核对。`contracts.json` 核对两人的移动、真实转移、交战伤害、暂停恢复、撤离集合、箱子写回和仓库守恒。只有证据完整、无错误/失败指令且完成契约通过才报告 `gameStatus=PASS`；性能是否达标单独判定。缺少新证据的旧回合仍是 `NOT_FULL_RAID_VALIDATED`。

每轮保持输入冻结，具体修复和首次失败见 [P3 实施记录](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/p3_execution.md)。

入口复用外部隔离副本，包含工作区当前资产，按输入哈希验证没有改动源项目，只管理自己启动的进程。固定 4K / High Fidelity，速度由用例配置；当前关闭普通 Profiler 会话，采集 21 个具名计数器，包含 Cluster/Range/Ground 分段，耗时和调用次数写入 `counters.csv`，缺失不能视为零。计数器尚未注册时每秒重试，全部找到后停止扫描；无样本仍明确失败并保留其余统计。`profile`、`binaryProfile` 可显式启用原始 Profiler，文件较大，诊断 FPS 不作最终验收。

范围快照每秒记录各群输入/输出点数、重建/投射/写线累计次数。Cluster 的普通显示更新最多每 0.05 实际秒一次，加速模拟不加速这一显示频率；成员状态及死亡隐藏仍逐帧执行。具体无 FFT 对比见 [P4d 记录](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/p4d_cluster_lateupdate.md)。

快照包含 Transform/缩放、身体尺寸、NavMesh 位置、实际/期望速度、目标和路径距离、进展计时，以及当前敌人的血量/护盾/可见性/射击结果。普通快照 1 秒一次，有具体交战对象时 0.25 秒一次；资源到达/等待/离开/完成按状态变化记录。读取 `hasEnemy`、`hasResource`、`hasDirectivePosition` 后再解释对应字段，不能把 JsonUtility 产生的默认 0 当作真实血量或距离。

搜索节点尚未接管的前置移动期只有真实导航目的地，`hasResource=false`；不会猜测箱子或替游戏发送指令。结果出现后仍有有限退出期限，原生进程挂起会自动回收并报告失败。32 项报告故障探针和 2 项 R5 定向 Play Mode 测试已运行；最新证据及已知问题见 [P1 记录](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/p1_execution.md)。后续自动背包和完整回合按 [大规划](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/task_plan.md) 继续。

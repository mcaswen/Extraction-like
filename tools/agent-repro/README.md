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
| Lifecycle | 4 | 重复伤害、新命令、取消、死亡、资源受击、恢复点失效 |
| Navigation | 8 | 零/小容差、坡面、导航丢失与无进展 |
| R5 | 2 | 位移后重新验证背包交互 |
| Feedback | 1 | 接收/拒绝结果、原因与反馈生命周期 |
| EnemyTargets | 16 | 七类正式敌人换人、失效变体、五类巡逻候选 |
| RangedSpatial | 12 | 双方高低差、枪口、三类弹体与薄墙 |
| Perception | 5 | 范围、射线、完整候选与范围技能 |
| Decision | 5 | 成员距离、实际防御、风险、撤离后备、失败目标短期排除 |
| Cooldown | 8 | 属性/装备/配置刷新、技能重排、重复 SkillId、普通攻击锁 |
| Combined | 10 | 多 Agent、实际撤离、动态路径、无效输入、护盾和缺失攻击配置 |
| Graphics | 1 | 正式顶部反馈 prefab 的成功/失败/消退 PNG |

清单以 [cases.json](cases.json) 为准，共 76 例。`-Suite Core`、`Risks` 自动包含 Smoke；`All` 包含全部。图形组根据清单自动启用图形设备，其他组默认 `-nographics`；`-IncludeGraphics` 强制所有选中组保留图形设备。图形测试从真实 Camera/Canvas 导出 PNG，由 Agent 读取检查。

## 结果与定位

`Logs/AgentReproduction/<run-id>/` 保存 `manifest.json`、`summary.json`、`report.md`，`groups/<group>-<repeat>/` 保存 Editor.log 和原始 NUnit XML。`cases/<完整测试名哈希>/<repeat>/` 保存 `trace.jsonl`、`case.json`、`nunit-final.json`，图形用例另保存四张 PNG。

NUnit XML 是最终通过/失败权威，清单中缺失的测试不会算通过。`case.json` 是生命周期中的即时快照，最终结论读 `nunit-final.json`。默认每组进程硬期限 2700 秒（含导入），可用 `-TimeoutSeconds` 调整；用例内另有墙钟期限，暂停不会无限等待。

- Regression：任一行为失败返回 1；缺失结果/测试、超时或环境故障返回 2；全部通过返回 0。
- Diagnose：0 仅表示完成证据收集，行为失败仍在 XML 和报告中，不能解释为游戏无 Bug。
- `-TestFilter` 支持临时窄筛选，此时不核对该组完整清单，不能作为全量验收。

故障探针：`-Suite Smoke -FaultProbe Assertion` 故意断言失败；`-Suite Smoke -FaultProbe Timeout -Repeat 2 -TimeoutSeconds 30` 仅挂起首次，用于验证回收和第二次继续。正常回归不用这些参数。

`./tools/agent-repro/Test-AgentReproReport.ps1` 独立构造 7 类 XML/进程结果，检查正常、异常退出、断言失败、Diagnose、缺失、超时及清理失败。它不启动 Unity，不计入 76 个游戏用例。

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

入口复用外部隔离副本，包含工作区当前资产，按输入哈希验证没有改动源项目，只管理自己启动的进程。固定 4K / High Fidelity / 1×；当前关闭普通 Profiler 会话，采集 12 个具名计数器，耗时和调用次数写入 `counters.csv`，缺失不能视为零。`profile`、`binaryProfile` 可显式启用原始 Profiler，文件较大，诊断 FPS 不作最终验收。

快照包含 Transform/缩放、身体尺寸、NavMesh 位置、实际/期望速度、目标和路径距离、进展计时，以及当前敌人的血量/护盾/可见性/射击结果。普通快照 1 秒一次，有具体交战对象时 0.25 秒一次；资源到达/等待/离开/完成按状态变化记录。读取 `hasEnemy`、`hasResource`、`hasDirectivePosition` 后再解释对应字段，不能把 JsonUtility 产生的默认 0 当作真实血量或距离。

搜索节点尚未接管的前置移动期只有真实导航目的地，`hasResource=false`；不会猜测箱子或替游戏发送指令。结果出现后仍有有限退出期限，原生进程挂起会自动回收并报告失败。32 项报告故障探针和 2 项 R5 定向 Play Mode 测试已运行；最新证据及已知问题见 [P1 记录](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/p1_execution.md)。后续自动背包和完整回合按 [大规划](../../.planning/2026-09-11-scenezl-final1-autonomous-raid/task_plan.md) 继续。

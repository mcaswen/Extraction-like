# P4d：Cluster LateUpdate 定位和修复

## 范围、证据和边界

用户在编辑器采样中看到 GameplayTargetClusterAuthoringBase.LateUpdate 42 次、合计 1.51 ms、0 B GC；FFT 约 8.96 ms 已由用户关闭。本轮工作树仅正式场景有用户修改（3 个 GameObject 从 active=1 改为 0），运行使用当前保存状态，不恢复 FFT，也不把用户场景改动混入代码提交。

沿已确认 Targets 显示缓存边界，先补证据，再以相同无 FFT 场景前后比较。避免把 Zone 降耗转移到 Cluster，保持动态敌人范围、成员增删、死亡隐藏、Collider 轮廓、地形变更和正式选择数据可用。目标是消除有证据的 LateUpdate 重复工作；FPS 和最大帧按实测报告，不预先宣布达标。

## 文件归属

| 决策 | 具体文件（相对工程根） | 职责 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs` | LateUpdate/状态/输入/缓存分段 Marker，公开现有缓存的输入量和累计计数，只读事实 |
| Extend | `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRangeCache.cs` | 构形/投射/写线 Marker；后续优化仍归每 Owner 的显示缓存，不能降低战斗/状态检测频率 |
| Extend | `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetShapeUtility.cs` | 射线和命中分类分段计时；若确认过滤或投射热点，在此复用地面查询能力 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidFrameSampler.cs` | 保留原 12 项，加 Cluster 4 项、共享 Range 3 项、Ground 2 项，共 21 项逐帧计数器 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs` | 低频快照按对象记录输入点数、输出点数、重建/投射/写线累计值，区分静态和动态群；不逐帧全场反射 |
| Extend | `tools/agent-repro/SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1` | 更新必须存在的计数器契约，保留失败、缺样本和异常退出检查 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidPerformanceTests.cs`、`tools/agent-repro/cases.json` | 动态范围、地面遮挡/坡面/高度变化及缓存失效构造，必要范围回归 |

共享 Range/Ground Marker 包含 Zone 和 Cluster 的子调用，不与 Cluster.Range 简单相加；每群计数用于归因，避免同名聚合造成错判。

## 执行和验收

1. 增加只读探针，跑报告故障构造；SC01 采无 FFT 基线，实际 1×、4K、High Fidelity，保留退出故障。
2. 按分段耗时和每群查询量写下修复细化；优先减少重复计算，避免未经证据直接降低动态轮廓刷新频率或放松正确性。
3. 构造动态成员/多群、地面分类和缓存失效边界，回归既有范围 8 项、必要层级/材质用例。
4. 同一场景、同采样配置 SC01 对比，记录 Cluster.State/Input/Range、投射/过滤次数、Zone、总帧，审查后独立提交。

## 实施结果

### 用户调整后的实施小规划

用户明确要求直接降频，约 0.05 秒更新一次。本阶段按该决定收敛，不再引入地面分类缓存、异步射线或新的调度模块。

- Extend `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs`：范围显示调度归 LateUpdate 所有者，输入收集前检查每实例截止时间，运行时每 0.05 **实际秒** 至多刷新一次。使用 unscaled 时间，避免加速逻辑测试时再次变成逐帧刷新；不追赶补帧。成员状态仍逐帧更新，完成后的隐藏检查放在限频之前。公开强制刷新、OnEnable 和编辑器预览立即生效，强制刷新同时重置下一次截止时间。
- Reuse `GameplayTargetRangeCache.cs`：保留准确输入比较、未投射点、定期地表重投射和 LineRenderer 脏检查；本轮仅增加计时，不改变投射算法。Zone 继续原调度，读取 Cluster 的缓存，因此不会额外叠加另一个 0.05 秒窗口。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidPerformanceTests.cs`：构造同帧移动多次不重建、8×模拟下按实际秒限频、到期更新最新坐标、强制刷新和重新启用立即更新、窗口内死亡仍完成并隐藏。已有资源移动回归由固定帧等待改为有界等待，适配明确批准的 50 ms 显示延迟。
- Extend `tools/agent-repro/cases.json`：登记新增构造用例。回归 ScenePerformance 及层级/材质用例，随后同样的无 FFT SC01 采样。

范围点及其缓存中心允许约 50 ms 加一帧的显示/选圈延迟；战斗感知、伤害、成员状态、导航目的点不使用此显示调度。此处不把更新频率下降描述成算法复杂度降低。

测量工具跟进修正：`SceneRaid.Report.psm1` 对无有效样本的计数器保留 null 和明确失败，不再让空集合 Average 读取中断其他计数器统计；新增故障构造验证。较高帧率暴露 `SceneRaidRunController.cs` 只在第 3/30 帧发现 Marker，会漏掉之后才首次调用的 Navigation.Check。Extend `SceneRaidFrameSampler.cs` 负责每秒重试尚缺失的 Recorder，全部找到后停止扫描；Controller 每帧调用这一有内部节流的入口。不主动调用游戏导航来伪造可用样本，最后仍缺样本则如实失败。用正式场景捕获启动较晚的真实导航计数器验证。

### 定位证据

首轮无 FFT SC01 `20260911-205234-571`，预热后 Cluster.LateUpdate 合计平均 1.0442 ms，State 0.0547 ms、Input 0.1090 ms、Range 0.8238 ms。14 个动态敌人群在后约 50 秒累计重建 19684 次，静态群未重建。首轮新增 Ground.Filter Marker 错放到凸包函数，该项不能用于地面分类归因，已修正；其他分段及范围计数不受此标记位置影响。

修正 Marker 后 SC01 `20260911-210055-347`：Cluster.LateUpdate 1.0320 ms，State 0.0497 ms、Input 0.0969 ms、Range 0.8322 ms；共享投射 1.1129 ms，其中射线 0.2598 ms、命中过滤 0.7174 ms，分别约 334 次射线、594 次命中过滤/帧。重复地表命中的逐点层级/组件检查是主要成本，用户选择直接限制调用频率。该轮进程正常退出，但出现行为失败 1 次、Navigation.Check 无样本和报告空集合错误，整体证据 FAIL；只将完整存在的范围计数用于诊断，不算整局通过。首轮整体也因原生退出超时 FAIL。

### 已执行构造验证

- `Logs/AgentReproduction/20260911-210524-598`：11 项 ScenePerformance 和 7 项层级/材质回归，**18/18 通过**，图形模式运行、正常退出、源输入检查通过。其中 3 项新增用例实际构造了同帧 100 次移动、8×模拟、强制刷新、重新启用和真实伤害死亡。没有靠用户观察范围线判断。
- `Logs/SceneRaidReportProbes/20260911-210520-806`：**36/36 通过**。无样本的导航 Marker 保持失败和 null 统计，其余 20 项完整统计不丢失。
- 临时投射成本诊断 `20260911-205759-548` 正常执行：10000 次空地投射约 3.80/4.42 ms（未录制/录制），命中平面约 16.62/16.90 ms。该轮过滤 Marker 位置尚未修正，只能说明当时射线探针没有毫秒级单帧额外成本，不用于修正后命中过滤归因；临时诊断方法未保留到正式测试目录。

### 降频后的第一轮

SC01 `20260911-210625-530`：相同无 FFT 场景，1×、3840×2160、High Fidelity、21 个计数器名。运行时错误 0、失败指令 0、停滞告警 0，进程正常退出，源输入不变；但 Navigation.Check 仍无样本，证据 FAIL。报告已正确保留其余计数器，未再抛空集合异常。

预热 10 秒后，Cluster.LateUpdate 平均 0.3719 ms（较修正 Marker 基线下降 64.0%），Input 调用从 42 降至 8.84 次/帧，Zone.Update 从 0.5292 降至 0.2334 ms。14 个动态群重建从约 50 秒内 21086 次降到 4333 次。该轮平均 85.50 FPS，基线 86.63 FPS，**整体帧率没有得到可确认的提升**。Cluster 的 p99 由 1.8227 变为 2.1585 ms，降频减少了总工作量，多个群同帧到期的峰值仍存在，不能只报平均值改善。

### 成本和行为边界

LateUpdate 消息仍每群每帧进入，状态刷新和完成隐藏仍逐帧运行；降低的是输入收集、缓存检查、构形、地面投射及写线的频率。单次复杂度没有改变：m 个成员构形至多 O(m log m)，q 个输出点各做一次射线并过滤至多 16 个命中，成本可表示为 O(m log m + q × (射线成本 + 命中数 × 层级分类成本))。未到期时显示分支为 O(1)，状态分支仍按成员数遍历。原来可在 80–120 FPS 下每秒执行 80–120 次重活，现在运行时普通刷新最多约 20 次，低帧时不补做积压更新。

### 最终采样和结论

SC01 `20260911-210825-662`：已捕获全部 21 项计数器，Navigation.Check 从观察帧 32 起有 5238 个有效样本，其中 1153 帧有真实调用，证实每秒重试覆盖了原第 30 帧之后出现的 Marker。运行时错误 0、失败指令 0、停滞告警 0，两个 Agent 自主到达资源并等待背包。

以下均为同一无 FFT 场景、1×、4K 高画质，预热 10 秒后的每帧均值；不将首轮 FFT 开启时的约 41 FPS 混入比较。原始配置、有效指标、失败状态和证据哈希见 [p4d_diagnostics.json](p4d_diagnostics.json)。

| 指标 | 限频前 `210055` | 最终 `210825` | 变化 |
| --- | ---: | ---: | ---: |
| 42 群 Cluster.LateUpdate 合计，ms/帧 | 1.0320 | 0.3561 | -65.5% |
| Cluster.State，ms/帧 | 0.0497 | 0.0527 | 状态频率保持 |
| Cluster.Input，ms/帧 | 0.0969 | 0.0230 | -76.3% |
| Cluster.Range，ms/帧 | 0.8322 | 0.2597 | -68.8% |
| Zone.Update，ms/帧 | 0.5292 | 0.2383 | -55.0% |
| 共享地面射线，次/帧 | 333.94 | 107.51 | -67.8% |
| 共享命中过滤，次/帧 | 594.24 | 191.49 | -67.8% |
| 动态群重建，后约 50 秒累计 | 21086 | 4491 | -78.7% |
| 平均 FPS | 86.63 | 89.84 | 单轮诊断，尚未达标 |
| Cluster.LateUpdate p99，ms | 1.8227 | 2.2595 | 峰值未改善 |

该阶段完成了用户要求的 0.05 秒限频和相关构造验证。没有采用异步物理、地面分类新缓存、场景质量调整或关闭范围线。多群同帧到期仍会集中执行，平均成本下降不能证明尖峰消失；若继续针对峰值治理，错峰调度是后续候选，需要独立测量。

最终整帧 p99 为 20.53 ms，**不满足 >120 FPS**。本轮清理后再次发生原生 Editor 退出挂起，60 秒后只回收本轮拥有的进程，exit=1、sourceUnchanged=true，整体 evidenceStatus=FAIL，唯一证据问题为 `process_exit:1`。前两轮正常退出不代表这一间歇故障已修复。观察模式没有操作背包或走完整撤离，不能算自主搜打撤验收。

用户关闭 FFT 的 3 处场景 active 改动保留在工作区，不随本次代码和文档提交。

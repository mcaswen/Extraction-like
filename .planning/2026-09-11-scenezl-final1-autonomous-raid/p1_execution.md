# P1：定位停滞、分解三处热点

日期：2026-09-11。状态：待 P0 复核提交后实施。延续主规划既定边界，不改变目标策略、导航算法、画面或背包规则。

## 已有证据和本阶段目标

P0 首轮 `20260911-191648-254` 在 4K / High Fidelity / 1× 图形 Editor 观察 60 秒，零人工输入。平均 32.81 FPS、P99 54.52 ms、最大 769.78 ms（包含首帧间隔），采集开启普通 Profiler，存在源 Editor 竞争，不作为最终性能成绩。

两名 Agent 自主选择员工食堂 ResourceCluster_B；Agent 2 曾交战成功后恢复搜索，随后在墙钟 37.88、56.13 秒以 NoProgress 失败并重选同群。Agent 1 长期保留同一 Search，距导航终点约 5 米。需要识别具体资源、导航局部避障、有效进展和搜索等待，不能仅靠宏状态判定。

脚本自动 Invoke 样本不能通过当前 ProfilerRecorder 名称直接获取，已明确标记 unavailable；PlayerLoop 和 GC 有效。P1 在业务所有者加具名 Marker，采集调用次数和耗时；不以缺失值 0 冒充已优化。

## 文件边界

| 决策 | 具体文件 | 本阶段职责 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs` | `Anomaly.Discovery.Update` 和扫描阶段 Marker，仅测量 |
| Extend | `Assets/Scripts/Gameplay/Agent/Core/AgentPawnRoot.cs` | Pawn 总耗时、身体事实、生命周期、Brain 分段 Marker |
| Extend | `Assets/Scripts/Gameplay/Targets/Authoring/TargetZoneAuthoring.cs` | Zone 总耗时、聚合、几何 Marker |
| Extend | `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationQuery.cs` | 共享路径查询 Marker，可解释 Discovery/Pawn 的重叠父子关系 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidFrameSampler.cs`、`SceneRaidScenarioConfig.cs` | 收集实际 Marker 描述/单位/有效性、分阶段帧信息、正常速度和诊断模式标志 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs` | 缓存只读反射，增加导航监视器的截止时间/进展位置、实际尺寸/速度/避障参数；不暴露写接口 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidObserver.cs`、`SceneRaidIdentityMap.cs` | 结构化指令信息，保留销毁前目标身份，记录运行时目标 ID 和稳定身份关联 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidContracts.cs` | 只读进展诊断：长时间位置摆动、路径剩余距离不下降、失败后重选；正常背包等待在取得业务等待证据后区分 |
| Extend | `tools/agent-repro/SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1`、配置 JSON | 原始慢帧、窗口/分位数统计、计数器缺失和数据失真故障测试；采集成功不等于游戏通过 |

Marker 定义归各业务所有者，仅依赖 Unity.Profiling；Gameplay 不依赖 Automation。帧统计归报告层，契约归只读 Automation；没有将帧采样或诊断策略塞进 Pawn。

P2 将按原主规划补资源交互事件，作为驱动开箱的唯一依据。P1 不通过反射修改节点等待标记，不用距离猜测直接开箱。若 P1 已能确定导航根因，在 P3 用构造例修复，原始失败证据保留。

## 执行和验收

1. 三处 Update 及导航查询添加 Marker，限定扫描/采样频率；保持原游戏行为。
2. 一轮相同 SC01 输入、普通 Profiler 关闭，捕获 Marker 自身可用性、两个 Agent 的导航轨迹和重复失败。
3. 自动统计真实帧间隔、Marker 总耗时/调用数，缺失/单位错误明确失败，父子不相加；启动和慢帧全保留。
4. 报告构造用例验证缺失、截断、非单调帧、非法数值、无渲染及伪 120 FPS；比较采样模式成本后决定最终模式。
5. 记录根因证据、未解问题、架构审查、提交，再进入 P2 背包链。

## 实施结果

待执行填写。

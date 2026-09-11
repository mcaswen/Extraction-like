# P1：定位停滞、分解三处热点

日期：2026-09-11。状态：探针实现和构造验证完成，完整图形运行存在原生退出故障，未通过整轮验收。P0 已在 `afc6be3` 提交。延续主规划既定边界，不改变目标策略、导航算法、画面或背包规则。

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

### 用户补充探针后的范围调整

用户要求加入 Transform、交战怪物血量和目标距离等探针。`SceneRaidReadModel.cs` 扩展姿态/缩放、NavMesh nextPosition、目的地与资源实体两套距离、实际/期望速度、当前敌人 HP/护盾、射击结果和只读可见性。空间查询只在低频诊断快照执行；不调用会改写 LastShotResult 的 `CanShootAt`，不通过最近对象猜测实际箱子。

因此将主规划已经确认的三个 P2 文件提前到 P1：Create `Assets/Scripts/Gameplay/Agent/Data/AgentResourceInteractionEvent.cs`、`Assets/Scripts/Gameplay/Agent/Runtime/AgentResourceInteractionChannel.cs`；Extend `Assets/Scripts/Gameplay/Agent/AI/Actions/SearchResourceActionNode.cs`。正式节点报告真实选中的资源、导航落点、接近/等待/离开/完成，按状态去重；没有自动转移或容量策略。`SceneRaidObserver.cs` 订阅并保存最近事实，ReadModel 通过只读委托取该事实。等待事件在焦点过滤前发出，保证尚未被 UI 聚焦的 Agent 也能被诊断；事件不改变等待规则。P2 继续实现消费事件的背包驱动。

## 执行和验收

1. 三处 Update 及导航查询添加 Marker，限定扫描/采样频率；保持原游戏行为。
2. 一轮相同 SC01 输入、普通 Profiler 关闭，捕获 Marker 自身可用性、两个 Agent 的导航轨迹和重复失败。
3. 自动统计真实帧间隔、Marker 总耗时/调用数，缺失/单位错误明确失败，父子不相加；启动和慢帧全保留。
4. 报告构造用例验证缺失、截断、非单调帧、非法数值、无渲染及伪 120 FPS；比较采样模式成本后决定最终模式。
5. 记录根因证据、未解问题、架构审查、提交，再进入 P2 背包链。

## 实施结果

### 中途调整

- 用户追加授权直接修场景配置，RaidFlow 唯一所有者修复提前执行，独立小规划见 [raidflow_scene_repair_execution.md](raidflow_scene_repair_execution.md)。不把修复藏在只读审计入口。
- `20260911-192746-879` 的图形 Editor 在执行入口前返回 0，但没有任何运行证据，报告正确拒绝；没有当成成功。
- `20260911-193222-820`、`20260911-193800-568` 均完成 60 秒采集，12 个计数器有效，重复 NoProgress 和小位移刷新进展被记录；但 Editor 在输出 Shutdown/License disconnected 后没有退出。第一次确认进程父链后终止本轮 PID，第二次由新增的 60 秒退出期限终止；两轮均保持 `process_exit:1`，不能作为干净完成。
- 退出故障目前只出现在关闭 Profiler 会话、保留 Recorder 的新配置。先将会话开关与二进制写盘分开，试验“开启普通会话、不写二进制”，验证是否能恢复干净退出。此时尚未证明因果，不称已修复。
- 进程层新增 `process-start.json`、终止原因和有限退出期限，日志仅输出摘要，完整慢帧保留在报告文件。30 个报告构造测试已通过，包括伪时长、假快帧、缺帧、计数器名称/单位/零调用及 40 ms 卡顿不能被平均值掩盖。

补充验证归属：Extend `Assets/Scripts/Editor/AgentReproduction/Tests/ResourceDisplacementTests.cs` 和 `tools/agent-repro/cases.json`，构造无背包控制器时的实际资源到达、事件去重、位移后离开/重新接近；沿用原位移后重开背包测试验证观察没有改变完成条件。普通 Profiler 开启但无二进制输出的第三轮同样发生退出超时，因此该配置不是修复。下一轮恢复 P0 的二进制输出配置进行对照。

最新对照 `20260911-195445-778` 恢复二进制 Profiler 后仍在原生退出阶段挂起，不能归因于普通 Profiler 开关。60 秒采集完成，0 个游戏错误，3 次指令失败、2 次停滞怀疑；外层保持失败。观察总成本 83.97 ms / 2226 帧，不含未归入该计时段的业务事件回调和退出写盘，不能视为完整测量开销。

探针发现 JsonUtility 会将内联空引用写为默认对象；ReadModel 新增 hasEnemy/hasResource/hasDirectivePosition 标志，构造测试覆盖序列化后的缺失语义，避免将无目标的默认 0 血误认为死亡。正式场景采到敌人 210→138→98→26 血量变化。搜索树先执行 MoveToTargetActionNode，因此 SearchResourceActionNode 的资源事件只覆盖真正进入搜索节点之后的资源选择；前置移动期只有导航目的地，明确标为 hasResource=false，不猜测箱子。

退出诊断调整 `SceneRaidEditorEntry.cs` 的隔离 Editor 清理：退出 Play Mode 后显式卸载被测场景，再隔两个 Editor update 请求退出，保存 cleanup.json 阶段。此操作发生在结果完成之后，不改正式场景或玩法。`20260911-195919-537` 确认完成场景卸载，但原生退出仍挂起；没有把它称为修复。

### 本阶段验证、结论和下一步

- `20260911-195819-616`：两项 R5 真实 Play Mode 构造测试通过，进程正常退出、源快照一致。包括无 InventoryController 时仍能报告真实到达、10 帧等待去重、位移后 Left/Approaching、原有背包完成条件保持，以及显式缺失标记经过 JsonUtility 后仍正确。
- `20260911-200234-691`：32 项报告构造测试通过。审查补齐固定 12 个计数器名单，每类每帧与原始帧序列对齐；整类缺失和重复帧也不能通过。
- 完整原始证据保留在 `Logs/SceneRaid/<runId>/`；结构化摘要见 [p1_diagnostics.json](p1_diagnostics.json)。最新一轮 2056 帧、约 60 秒，平均 34.71 FPS，1 条粒子系统断言、2 次指令失败、2 次停滞怀疑。进程超时导致 evidenceStatus=FAIL，不能写成“0 错误、无 bug”。
- Marker 已将 Zone 几何更新和状态聚合分开。代表轮 `193222-820`：Zone 总平均 4.354 ms，其中 Shape 4.323 ms；Discovery 总平均 1.936 ms，扫描单帧总最大 48.895 ms；Pawn 总平均 0.539 ms。父子 Marker 重叠，不能相加。不同轮有原 Editor 竞争和诊断配置变化，均不是最终性能成绩。
- 新探针定位到同一 ResourceCluster_B/LootBox[3]，角色导航目的地与指令群中心是两套坐标，不能仅看群中心距离判断是否到达。Agent 1 在前置移动期约 5 米处停滞，尚未进入搜索节点；Agent 2 曾等待、战斗、返回，随后也出现 NoProgress。
- 已观察到自然交战中的 210→138→98→26 HP。敌人消失后的具体死亡/掉落归因、出生点稳定身份、完整库存台账将在后续补齐；当前快照只证明所记录的血量变化，不声称覆盖每次伤害。
- 原生退出故障未找到根因，Profiler 开关和显式场景卸载都未消除。保留有界回收和失败报告，后续独立定位，不阻止可正常运行的背包微用例。下一阶段按 [p2_execution.md](p2_execution.md) 接入真正背包操作；停滞、场景尺寸和粒子断言归后续定向修复，不靠测试传送/清怪绕过。

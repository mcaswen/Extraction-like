# P3：原场景脚本执行和问题定位

## 范围与小阶段

沿用已确认的六个冻结脚本、两名真实角色、4× 逻辑速度和不改写业务状态的边界。先完成 P2 MC01-X 及 SC02 冒烟，然后验证 MC01-R/E、MC02、MC03、MC04-N 的自然触发情况。发现问题先核对原始事件与实际对象；新游戏修复另列具体文件的小规划，不以修改脚本选择器、更换种子或强制存活制造通过。

本阶段 Reuse `tools/agent-repro/Invoke-SceneRaid.ps1`、`SceneRaid.ClusterCommands.Contracts.psm1`、`SceneRaid.Settlement.psm1` 和 `cluster-command-scenarios.json`；没有预设生产文件修改。证据归档 `Logs/SceneRaid/<runId>`，诊断结果写本文件。若证据探针/契约本身与正式规则不一致，修改相应 Automation/工具文件，保留原始输出，先构造回归再复跑受影响场景。

重点检查：正式 Dispatcher 的角色/群/具体成员，原活动和挂起指令，实际移动/掉血，改令后的旧会话关闭，两人共享资源/敌人，指令停止后的自主接续，未开箱早撤与常规搜刮后的结算。正常死亡只核对终态和存活者结算，不改战斗数值。

验收包括每条已提交指令的同步结果和异步终态、没有额外 Failed/Rejected、仓库和箱内余物守恒、没有运行异常。前提未出现记 PARTIAL 并列具体步骤，使用 P1 的确定性边界结果补充，不能冒充真实场景已覆盖。

## 实际结果

P2 提交 `5f4b748` 后继续执行，源码和场景输入保持。

| 运行 | 脚本 | 实际结果 | 结论 |
| --- | --- | --- | --- |
| `Logs/SceneRaid/20260912-050012-188` | MC01-R，4×/731 | 三条预定 Search 全部接受并有动作和终态；8 次库存会话，Actor1 自然战死，Actor2 撤离 | evidence PASS、game EXPECTED_DEATH，coverage COMPLETE，0 errors/Failed/Rejected/停滞；死者不结算，存活者仓库和会话守恒通过，不做战斗修复 |
| `Logs/SceneRaid/20260912-050551-844` | MC01-E，4×/731，类型修正后 | 两条 Engage 正式执行，Actor1 曾对近敌造成伤害，后自然战死；Actor2 撤离 | evidence PASS、game EXPECTED_DEATH，0 errors/Failed/Rejected/停滞；Actor1 第三步缺少存活/交战完成前提，coverage PARTIAL |
| `Logs/SceneRaid/20260912-051104-552` | MC02，4×/731 | 6 次改令/重复任务，旧任务收尾、最后 Search 完成，两人撤离 | evidence/game PASS，0 errors/Failed/Rejected/停滞；Retaliating 门槛在预定期限内未发生，coverage PARTIAL；该轮与短 NUnit 导航回归并行，仅作逻辑诊断 |
| `Logs/SceneRaid/20260912-051818-780` | MC03，4×/731，销毁身份修正后 | 焦点 1/2、异焦点显式 Actor1、共享资源、打开背包时改令、共享敌人共 8 个步骤触发，两人撤离 | evidence/game PASS、coverage COMPLETE，0 errors/Failed/Rejected/停滞，目标反馈身份和会话/仓库守恒通过 |
| `Logs/SceneRaid/20260912-052011-781` | MC04-N，4×/731 | 单成员敌人命令被有效反击替换，之后自主双人撤离 | evidence/game PASS、0 errors/Failed/Rejected/停滞；没有出现原命令 CombatCompleted，后两步未触发，coverage PARTIAL。真实场景负例尚缺覆盖，P1 已完成确定性拒绝和保留旧任务验证 |

下一项 MC01-E `20260912-050154-739` 已启动。

## P3a：敌人指令枚举别名导致驱动误判

MC01-E 原始 attempt 中的类型为 `EnemyTarget`。`Assets/Scripts/Gameplay/Agent/Data/AgentDirectiveType.cs` 保留 `EnemyTarget = Engage` 的序列化兼容别名，Enum.ToString 在当前 Unity 运行时返回该旧名称。命令证据却把这个字符串作为稳定脚本协议，Driver 的 CombatCompleted 门槛和离线类型匹配要求 Engage，因而误判。

文件归属：Extend `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandEvidence.cs`，在序列化边界按值明确输出 Search/Engage/Extract；Reuse 正式枚举值，保持 `AgentDirectiveType.cs` 兼容别名，不修改生产 API。Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCommandHarnessTests.cs`，已有真实击杀用例增加稳定类型断言，类型驱动属于 Automation 的协议职责。没有新增抽象或依赖。

测试顺序：保留 MC01-E 真实红灯完整输出，修正后运行受影响 Harness 用例，再以原 MC01-E 脚本复跑。其他已通过脚本的记录保留，最终矩阵重新冻结。

P3a 类型回归 `Logs/AgentReproduction/20260912-050400-134` 1/1 通过；原 MC01-E 实际另有两次失败：Actor1 在 6.389 秒观察到敌人，9.986 秒已丢视线，11.885 秒 LostSight；Actor2 的自主 Search 于 130.306 秒 Unreachable。后者与即时诊断中的完整路径矛盾，不能归到枚举别名或自然死亡。原脚本复跑 `050551-844` 正在进行。

## P3b：实验室路径矛盾的定点构造规划

先 Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidResourceNavigationTests.cs`、`tools/agent-repro/cases.json`：加载实际场景，在夹具准备阶段将真实 Actor2 的 NavMesh 绑定到日志位置 `(-430.34558,3.00834,197.98416)`，保持真实尺寸、偏移、NavMesh 和几何；对目标 `(-370.77042,0.00834,197.92053)` 运行正式 `AgentNavigationQuery` 和 `AgentNavigationMotor`，记录采样、CalculatePath/SetPath 和沿路失败。与实际自主局分开标记为构造，不假装无状态改写。

Reuse `AgentNavigationQuery.cs`、`AgentNavigationMotor.cs`，暂不改生产结果或加重试；若原位置不足复现，再在对应导航文件记录最窄失败分支，避免把失败后重新计算成功误当作失败时也成功。路径失败与 LostSight 分开定位，不以延长追踪时间或改场景墙体掩盖已认可的遮挡规则。

定点构造 `Logs/AgentReproduction/20260912-050737-422` 1/1 通过，真实尺寸和原路径实际到达。MC01-E 类型修正后的复跑 `050551-844` 也未再出现导航失败，但这不足以认定原故障已修复。继续 Extend `AgentNavigationQuery.cs` 的调用方私有 Buffer：内部记录本次拒绝发生于采样、高度、CalculatePath 或路径状态，使用固定字符串，无公共接口变化。Extend `AgentNavigationMotor.cs`：只在实际执行失败时输出该分支，SetPath 返回 false 单独标记；日志只编译到 Editor/验证 Player，不改状态、不重试、不为候选扫描打印。Extend 原导航测试，构造断开岛验证分支日志准确。后续场景脚本保留此探针，若再现可直接定位。

断开岛分支日志自检 `050951-685` 1/1 通过。文件审查将这一通用导航断言归入已有 `Assets/Scripts/Editor/AgentReproduction/Tests/NavigationExecutionTests.cs`，真实实验室坐标构造仍归 SceneRaidResourceNavigationTests；因此只扩展前者和对应 cases 清单，不把通用 Motor 验证继续放进场景专用文件。

迁移后 `Logs/AgentReproduction/20260912-051104-507` 9/9 导航执行回归通过，覆盖到达容差、斜坡、导航丢失、无进展和失败分支。P3a 已修复证据协议别名；P3b 仅完成现场构造和诊断细化，原偶发 Unreachable 仍是未确认根因，不能写成已修复。LostSight 原始时序符合已有有限追踪规则，没有修改生产超时或扩大白名单，原严格报告的失败记录保留。

## P3c：目标销毁后的证据身份丢失

MC03 `Logs/SceneRaid/20260912-051358-825` 八个步骤实际触发，0 errors/Failed/Rejected，死者和存活者结算正确，但 Trace 报 `missing_or_corrupt_command_feedback`。原始序号 251 的 Completed 保留敌人层级身份；相应 command.feedback 为空。Observer 已按 CommandId 缓存死亡前身份，Evidence 却再次从已销毁 Unity 对象取身份，造成两个证据流不一致。

Extend `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandEvidence.cs`：销毁后身份为空时复用已有 `_byCommand` 内该 attempt 的目标身份，不创建第二份缓存、改变 Gameplay 事件或放宽离线核对。Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCommandHarnessTests.cs`：构造真实接受后目标对象先销毁、下一帧 lifecycle 完成，验证两条流的目标仍一致；构造销毁只用于采集边界测试，不冒充真实击杀/整局。

Extend `tools/agent-repro/cases.json` 登记该用例；`SceneRaid.ClusterCommands.Contracts.psm1` 将损坏反馈的错误标记为 invalid 前缀，让 Report 同时报告证据失败。保持数据正确性门槛，不把原失败改判为绿灯。小阶段跑新身份用例、既有真实击杀及 PS 故障探针后，原 MC03 脚本复跑。

`Logs/AgentReproduction/20260912-051717-120` 2/2 通过，新销毁边界和原真实击杀都保持目标身份、反馈序号、血量证据一致；`Logs/SceneRaidCommandProbes/20260912-051717-118` 53/53 通过。MC03 正在按原脚本复跑。

MC03 复跑 `051818-780` 完整通过，8/8 步骤、两人撤离。P3c 闭环完成，原红灯 `051358-825` 保留。后续 MC04-N `052011-781` 运行中。

## P3 收尾与最终验证入口

六个脚本均已有实际执行记录；两项采集/驱动缺陷完成修复。MC04-N 前提未自然形成，没有对游戏强制伤害、补发命令或换种子凑负例。进入 P4 冻结输入后的 11 槽位矩阵。

原 MC01-E 的单次 Unreachable 保留为未确认根因：现场定点构造使用的是诊断记录的上一移动目的地，原事件回调发生在本次移动黑板回写之前，不能确定它与失败瞬间目标完全相同。新增 Motor 日志现在直接记录失败调用的目标和分支，后续若再现即可消除这项歧义。未改行为或声称已修复，最终报告需保留该限制。原一次 LostSight 已有“观察后丢失约 2 秒”的时序证据，符合既有规则，严格报告仍保留原失败计数。

## 最终矩阵触发的后续闭环

上面的“未修复”描述对应当时证据，后续阶段均保留原始记录：

- [P3d](p3d_path_assignment.md) 建立固定交付期限；[P3e](p3e_expected_lost_sight.md) 仅按精确证据区分已认可的有限追踪终止，原始 Failed 不删除。
- [P3f](p3f_resource_completion_race.md) 修复队友耗尽资源与移动节点聚合刷新之间的完成时序，六项新边界及相关库存/导航回归通过。
- [P3g](p3g_elevated_retaliation.md) 补高处敌人的合法地面射击位置，阻止不可行伤害来源不断替换旧任务，12 项新边界及原脚本完整 7/7 通过。
- [P3h](p3h_native_path_rejection.md) 复现 SetPath 持续拒绝，证明普通重查不足；在完整空间校验后加入有界原生目的地恢复，真实地形构造实际到达，65 项相关回归通过。C++ 内部拒绝机制仍未证明，业务恢复不是靠延长超时或强制改位置。

- [P3i](p3i_shared_extraction_presence.md) 修复多人撤离触发器相互覆盖进度，增加只读读条探针。14 项真实物理构造和 80 项相关回归通过，提交 `9d7195a`。
- [P3j](p3j_frame_capacity.md) 修复固定十万帧采集与 600 秒长局预算不匹配；4 项容量构造和 79 项报告反例通过，超限仍失败，提交 `5e89d48`。

当前最终验证基线 `5e89d48`，P4 按原 11 槽位重新冻结。正常死亡和自然缺前提不触发重抽，前五版停止矩阵完整归档；最新结果以 P4 为准。

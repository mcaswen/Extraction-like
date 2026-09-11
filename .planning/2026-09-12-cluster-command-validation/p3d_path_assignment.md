# P3d：完整路径交付失败被立即误判为空间不可达

## 复现和判断

最终矩阵第一版停在槽位 2：`Logs/SceneRaid/20260912-052540-029`，Actor2 自主搜刮实验室时再次 Unreachable。新增日志明确为 SetPathRejected，时间 130.269 秒，实际位置 `(-430.254456,3.008339,197.984161)`，本次目标 `(-370.770416,0.008338928,197.920532)`。空间采样和 CalculatePath 已通过，原 Motor 把交付失败直接作为 Unreachable 发布并结束任务。

Unity 2022.3 的 [SetPath 文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMeshAgent.SetPath.html) 区分路径成功赋给 Agent，并说明赋值失败会清除路径。完整查询不等于交付必成功；具体引擎拒绝原因尚未证明，不能声称 NavMesh 已断开或 Unity 内部某个已知 bug。现场定位已足够证明：目前没有给这种交付失败任何恢复窗口，实际可达任务被一帧失败终止。

## 文件、职责和实现

- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentPathAssignmentBudget.cs` + meta：独立的小型期限状态，只接收交付成功/失败、当前游戏时间，返回 Moving/NotReady/Unreachable。一次连续失败窗口固定为原 readiness timeout，成功或任务/显式停止重置；重算路径和更新目标不能续期。它不调用 Unity 导航、不选择任务、不写日志。
- Extend `AgentNavigationMotor.cs`：在唯一 SetPath 调用处使用该预算。首次或暂时交付失败清缓存，下帧重新执行正式完整查询后再交付，保持当前命令；成功继续实际移动。达到固定期限才返回失败。保留真正采样/高度/路径失败的立即失败规则和原进展期限；不 Warp、直接移动 Transform、强制完成链接或盲目 SetDestination。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/NavigationExecutionTests.cs`、`tools/agent-repro/cases.json`：构造一次失败后成功、连续失败到期限、重复查询不能延长期限、暂停时间不走和 Reset 清理预算。复用原实际导航回归，验证断开岛仍失败、无进展仍结束、真实移动仍到达。
- Extend `SceneRaidResourceNavigationTests.cs`：原记录点前方增加正常行走的起点，记录是否经过 OffMeshLink，仅用于排查交付上下文，不预设其一定为根因。实际整局仍用原 MC01-E 重跑，不能用构造替代。

依赖保持 Motor → 导航查询/交付预算；Gameplay 无测试引用，现有公共命令和导航接口不变。选择小状态对象是因为期限规则可独立构造失败序列，避免为强迫原生 SetPath 返回 false 增加业务回调注入或篡改真实场景。

## 验收和后续

先验证预算规则和原导航回归，再跑实际记录点路径与 MC01-E。恢复窗口必须有限；最终不能仅把失败日志降级而没有真实后续移动、搜刮和终态。首次矩阵两局保留为被后续修复取代的记录，新版本重新冻结并执行 11 个原槽位，脚本/种子/阈值不变。

## 结果

`Logs/AgentReproduction/20260912-053107-110`：13/13 通过，包含 11 项导航执行/交付预算回归、两个真实实验室起点实际到达。NUnit 的 float 用例名称序列化为 -430.3456f，清单已按 XML 校正，不改构造坐标。

当前处理保留空间查询失败和固定进展期限，交付成功才清恢复预算，持续失败不能因重复查询或目标变化续期。接下来使用原 MC01-E 实局验证。

首轮修复后 MC01-E `Logs/SceneRaid/20260912-053329-987` 未触发 SetPath 拒绝，但出现已认可的观察后 LostSight，原严格报告仍为 ISSUES_OBSERVED，没有重写它。有限追踪的证据裁决调整见 `p3e_expected_lost_sight.md`，与路径交付恢复分开处理。继续原脚本观察实际恢复分支。

固定目标的现场构造尚未触发交付恢复。追加同文件的真实资源解析构造：只保留日志中实验室 Box[2] 为未完成候选，每帧用正式群查询重新计算接近点，再交给 Motor 正常行走；这覆盖原场景接近点随位置变化的条件。限定夹具准备阶段标记其他成员，实际整局脚本不变，不要求一定发生原生失败才认为路径能到达。

最终构造 `Logs/AgentReproduction/20260912-054315-452` 1/1 通过，动态资源接近点实际到达，记录 retries=0。合计 14 项相关构造通过。三种真实路径构造没有触发原生失败，恢复期限分支由纯状态反例验证；不声称已复现或解释引擎内部拒绝原因。原脚本复跑 `054002-520` 未出现导航失败，正常死亡/幸存者结算通过。最终矩阵继续保留实际分支诊断。

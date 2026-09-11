# P3h：交替伤害来源和反击进展诊断

## 已知事实和设计约束

`20260911-234403-063` 中 Agent 2 已撤离，Agent 1 满包撤离途中进入反击，死亡前在 AncientStrander [116]/[130] 之间切换；HP 36→22→8→0，敌人确有被击伤，因此不能直接说该轮完全无法开火，也不能把死亡自动归类为代码缺陷。

源码 `AgentDirectiveLifecycleController.Submit` 对同一敌人的重复伤害保持原 CommandId，对另一敌人会 Cancel + Activate，重置 Motor。原已确认设计 `../2026-09-11-agent-reproduction/repair_design.md` 第 6 节明确“反击中再次受击：可更新反击目标”，所以不能未经说明把当前实现直接定为违反既有规则。

先构造足以区分行为边界的对照：一/两个敌人在两侧射程外，对同一角色每 0.25 游戏秒施加带真实 source 的有效伤害，Agent 的追击、移动、开火均正常执行。要求反击在 8 秒窗口内有真实投射物伤害进展。单一来源通过、交替来源失败才证明存在可构造的切换饥饿风险，不据此推断所有原场景死亡都由它造成。

## 本步文件和边界

- **Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidRetaliationProgressTests.cs` + meta**：只负责连续伤害与实际追击/投射物进展，不直接写 Active 指令、敌人受伤结果或导航位置。保留运动范围、伤害次数、CommandId 变化等结构化证据。
- **Reuse `World/AgentFactory.cs`、`EnemyFactory.cs`、`TestNavMeshBuilder.cs`、`TargetFactory.cs`、`Infrastructure/RuntimeWait.cs`、`Reporting/CaseArtifactWriter.cs`**：隔离场景和正式刺激接口，测试高生命仅沿用构造工厂，原场景属性不变。
- **当前不改 Gameplay**：结果用于明确策略讨论，未确认策略前不修改 `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveLifecycleController.cs`。如后续选择修复，仍由该文件拥有反击身份和撤离恢复，不把策略分散到伤害接收者、NavigationMotor 或 Shooter。

## 待 Review 的具体备选

推荐优先完成当前有效反击：再次遭其他来源命中时仍正常受伤，但不替换当前 CombatDamage 指令；当前目标死亡/失效/追击失败时仍按原链路结束并恢复撤离，之后再受击可以开启新的反击。新手动命令仍立即覆盖，原撤离记录仍只有一份，不新增敌人队列。

备选是维持最近伤害源优先，承认两侧交替攻击会影响进展；或增加最小锁定时长/危险优先级，但会新增参数和策略判断，不能用没有实测依据的时间值直接替换现有规则。

当前只执行构造诊断，取得结果后再明确是否需要上述策略调整。

## 实施结果

`Logs/AgentReproduction/20260911-235559-851/`：实际 Play Mode 执行两项，单一来源通过，交替来源失败；没有基础设施失败。

| 条件 | 游戏时间 | 有效受伤次数 | 不同反击 CommandId | x 位置范围 | 敌人受到真实投射物伤害 |
| --- | --- | --- | --- | --- | --- |
| 单一来源 | 1.3586 秒 | 6 | 1 | 0～6.2859 米 | 第一敌人生命比例 1→0.997 |
| 两侧交替来源 | 8.0003 秒 | 32 | 32 | -0.0079～0.4957 米 | 两名敌人生命比例均保持 1 |

两项均保留原撤离 CommandId。交替组失败发生在“8 秒内必须实际伤害敌人”的断言，证明当前规则存在反复切换导致追击没有进展的可构造边界。用例使用真实伤害接收入口、Motor 和 Shooter；高生命只用于防止构造中的 Agent 提前死亡，没有修改正式场景属性。

这不证明原场景那次死亡完全由切换引起：原场景中存在实际伤害进展，仍可能涉及敌人数量、攻击频率和正常死亡。建议采用上文“完成当前有效反击”规则，待用户确认后实施生命周期修复、相邻回归及原场景复跑；当前没有 Gameplay 改动，也没有将该红灯用例纳入常规通过目录。

夹具标为 NUnit `Explicit`，作为需明确选择的已知失败诊断提交，不混入 132 项已修复回归。策略确认后移除该标记、修复并纳入目录；不能把跳过 Explicit 解释为这个问题通过。

`20260912-000854-150` 复跑验证显式选择类名仍会实际运行两项（skipped=0），结果再次是单来源通过、交替来源失败。入口：`Invoke-AgentRepro.ps1 -Group Smoke -TestFilter 'SceneRaidRetaliationProgressTests' -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchRegression`；退出码 1 是已知行为红灯，不是测试没有执行。

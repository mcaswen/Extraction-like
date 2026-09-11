# P3e：区分已认可的有限追踪终止和非预期失败

## 问题和既有约束

MC01-E 中两次观察到“已经看见敌人，随后连续丢失视线约 2 秒”的 LostSight：`050154-739`、`053329-987`。这是 P1 已确认保留的行为，与从未见过远敌就提前放弃不同。严格报告目前只允许预期 Rejected 和自然死亡，因此把这种已认可终止也判作需要修复的错误；若通过重复相同场景直到不触发它来凑绿灯，会变成筛选结果。

不修改追踪规则、脚本目标、种子或等待门槛。将通过独立证据确认的有限追踪终止单列，保留原始 Failed 计数，其他失败仍使验收失败。历史缺字段日志不回填、不改判。

## 具体文件和判定

- Extend `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandEvidence.cs`：Progress 保存实际 `CombatLostSightTimeout`，可见性变化时立即记录，不再只随位移/血量改变记录。Probe 只读取正式黑板中的可见性，不写观察/追踪状态。
- Extend `tools/agent-repro/SceneRaid.ClusterCommands.Contracts.psm1`：仅对已归属、已接受的手动 Engage 和唯一 LostSight 终态检查：活动命令期间实际见过该目标，最后一次可见之后有持续不可见样本，终止时游戏时间确实达到记录的原有超时，终态探针证明锁已释放。缺失字段、未见过目标、过早失败、重新可见、仍持有旧锁均不豁免。仅该原始 sequence 计入 `expectedExecutionFailures`，不按原因全局过滤，Autonomous 不改变。
- Extend `SceneRaid.Report.psm1`：原始 behaviorFailures 不减少，另列 expectedRejections、expectedExecutionFailures 和 unexpectedBehaviorFailures。游戏裁决只排除被精确证明的两类预期结果，步骤目标未击杀或后续门槛未形成仍保留实际覆盖。
- Extend `Test-SceneRaidClusterCommands.ps1`：正常连续丢视线、过早/从未见过/重新见到/未释放锁/额外失败等故障反例；C# 既有真实命令证据用例验证超时字段。

依赖不变，判定仍属于离线命令契约，RunController/Gameplay 不认识报告分类。没有引入通用异常白名单或改变游戏失败事件。

## 验收

Extend `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1` 的终端摘要，展示三个分项计数和覆盖状态；Extend 同目录 `README.md` 说明裁决口径。仅输出和文档职责，不改变运行或成功条件。

先让新增反例拒绝错误日志，再按原 MC01-E 实局检查分类、库存和终态。首轮矩阵保留，修复后的最终矩阵不筛选死亡或自然追踪结果。

`Logs/SceneRaidCommandProbes/20260912-053843-545` 61/61，通过原 53 项和新增 8 项有限追踪终态反例；`Logs/SceneRaidReportProbes/20260912-053843-542` 78/78 原报告回归通过；`Logs/AgentReproduction/20260912-053843-513` 1/1 真实击杀/证据字段回归通过。MC01-E 按原脚本继续实际验证。

真实 MC01-E `Logs/SceneRaid/20260912-054002-520`：证据 PASS，游戏 EXPECTED_DEATH，原始失败 1，证据确认的有限追踪终止 1，额外失败 0。可见性变化、原有超时和释放均满足，存活角色仓库结算通过。保留 `053329-987` 原失败报告，没有事后篡改历史日志。CLI 只增加计数摘要，PowerShell 语法解析通过。

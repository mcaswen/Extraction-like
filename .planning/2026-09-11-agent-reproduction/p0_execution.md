# P0 小规划：自动运行与取证入口

- 状态：实现和测试／Review 完成，已提交 `29ec57c`。用户已确认大架构，并授权自行完成“小规划 → 实现 → 测试／Review → 调整 → 提交 → 下一阶段”。
- 目标：在本机实际发现 Editor 测试，自动进入 Play Mode，运行真实 NavMesh／Physics 冒烟，输出 NUnit XML 和独立证据。
- 文件边界：`tools/agent-repro/` 负责工作区、进程和汇总；`Assets/Scripts/Editor/AgentReproduction/Infrastructure/` 负责测试上下文、生命周期与等待；`Reporting/` 负责落盘；`Tests/HarnessSmokeTests.cs` 只负责基础验收。不改 Gameplay。
- Reuse：现有 UTF 1.1.33、NavMeshBuilder、Physics、既有 Editor 程序集；Wrap：受限私有字段读取；Create：下面对应文件。暂不预建后续未使用的空抽象。

## 本阶段实现文件

| 文件 | 职责 |
| --- | --- |
| `tools/agent-repro/Invoke-AgentRepro.ps1` | 参数、Unity 进程启动、超时与运行退出码 |
| `tools/agent-repro/AgentRepro.Workspace.psm1` | 快照同步、版本匹配、文件哈希和公司／产品存档隔离 |
| `tools/agent-repro/AgentRepro.Report.psm1` | XML 与用例证据完整性、Markdown／JSON 报告 |
| `tools/agent-repro/cases.json` | 测试组和预期用例标识 |
| `tools/agent-repro/contracts.json` | 用户已确认行为和来源 |
| `tools/agent-repro/README.md` | 运行与输出说明 |
| `Assets/Scripts/Editor/AgentReproduction/Infrastructure/TestRunContext.cs` | 重载后从运行清单恢复上下文 |
| `Assets/Scripts/Editor/AgentReproduction/Infrastructure/ReproductionTestFixture.cs` | 自动 Play Mode、清理和结果记录 |
| `Assets/Scripts/Editor/AgentReproduction/Infrastructure/RuntimeFixtureAccess.cs` | 集中配置和读取测试对象字段 |
| `Assets/Scripts/Editor/AgentReproduction/Infrastructure/RuntimeWait.cs` | 有界条件等待 |
| `Assets/Scripts/Editor/AgentReproduction/Reporting/CaseArtifactWriter.cs` | 单用例 JSON 与连续 JSONL 证据 |
| `Assets/Scripts/Editor/AgentReproduction/Tests/HarnessSmokeTests.cs` | 发现、Play Mode、导航与物理基础验证 |

## 调整与理由

1. 本机 Assets 约 1.38 GiB、Library 约 3.49 GiB，C 盘仅约 15 GiB 可用。隔离副本放在 D 盘项目同级的专用 `.agent-repro` 目录，避免挤满系统临时目录。
2. 同一任务复用具有所有权标记的隔离副本和其 Library 缓存；仅在没有本工具 Unity 进程运行时同步输入，每次运行保存新的源哈希与清单。保持存档隔离，同时避免每次修复完整重新导入。
3. 正式源文件由 Agent 实施修复，测试进程仅写副本。新文件的 `.meta` 作为实现产物保留稳定 GUID，不把生成元数据视为测试对源工程的隐式写入。

## 验收及结果

- `20260911-004521-745`：测试发现成功；setup 在 Play Mode 重载后重入导致准备失败。已增加运行状态守卫，未修改业务程序集。
- `20260911-005021-567`：1/1 通过，真实 Play Mode、完整 NavMesh 路径、墙体 Physics.Raycast、公司／产品与 persistentDataPath 隔离通过。
- `20260911-005202-650`：故意断言失败 1/1，NUnit XML／独立证据保留；Diagnose 报告明确 businessFailed=true，退出 0 仅表示收集完成。
- `20260911-005252-479`：第一轮故意卡住并被 30 秒看护终止；缺失 XML 按 TIMED_OUT 记录，第二轮自动继续且 1/1 通过；总体基础设施退出码 2。
- 所有运行源文件哈希保持一致；原先其他项目的 Unity 未被操作。证据位于 `Logs/AgentReproduction/<上述 run-id>/`。
- Review：生产代码没有测试依赖；生命周期负责重载，报告以最终 NUnit 结果校正 teardown 前快照；私有字段接入集中；已修正 Windows PowerShell 插值与原子文件替换。临时副本按任务复用且只镜像所有权标记范围。
- 提交：`29ec57c`；P1 随后修正跨重载协程的完成检查，P4 补齐异常退出报告测试，见各阶段记录。

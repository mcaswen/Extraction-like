# P4e：60 FPS 门槛调整和 Editor 退出挂起调查

历史阶段记录：本文组合门槛已被用户最新“只要求平均 FPS >60”取代，当前口径见 [P4i](p4i_average_fps_scope.md)。下方保留当时方案和原始执行结论。

## 需求和边界

用户将整局性能目标从 120 FPS 改为 60 FPS，并要求调查退出挂起。这里调整验收门槛，不给游戏或测量进程加 60 帧上限；仍保持 4K 高画质、真实渲染和原始慢帧证据。历史 120 FPS 报告不回写为通过。

统计门槛改为平均/滑动 1 秒/1% Low 均不低于 60 FPS、P99 不超过 16.667 ms、单帧卡顿上限 33.333 ms。数值边界只允许 CSV 序列化的微小舍入误差，不用平均值掩盖慢帧。完整自主回合、覆盖和退出仍独立验收。

## 文件归属和实施顺序

| 决策 | 具体文件 | 职责 |
| --- | --- | --- |
| Extend | `tools/agent-repro/SceneRaid.Report.psm1` | 60 FPS 默认统计门槛，结果明确标记目标和预算；原始统计不删帧 |
| Extend | `tools/agent-repro/Test-SceneRaidReport.ps1` | 验证 60/80 FPS 通过、50 FPS 和单次卡顿失败，原报告故障约束保留 |
| Extend | 本目录 `task_plan.md`、`architecture_review.md`，`tools/agent-repro/README.md` | 更新当前门槛，历史结果保留，记录调查证据和限制 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs` | 只在显式隔离自动化运行中记录退出阶段、Editor quitting、Domain 卸载等顺序，按证据实施最小清理修复 |
| Reuse | `tools/agent-repro/Invoke-SceneRaid.ps1`、`AgentRepro.Workspace.psm1` | 现有副本、PID 所有权、SHA 校验和期限；不重写一套场景启动系统 |
| Create（按需） | `tools/agent-repro/SceneRaid.ShutdownDiagnostics.psm1` | 若需要长期保留，独立负责拥有 PID 的退出等待、线程/转储取证，避免向场景逻辑塞 Windows 调试职责 |
| 临时实验 | `Logs/ShutdownInvestigation/` | 调试器、转储、线程栈、最小空场景/关闭窗口对照脚本；不进入普通 Player，不改用户编辑器或场景 |

先统计历史成功/失败模式，再在隔离副本重现，捕获超过正常清理时间后的进程证据。优先区分项目生命周期、原生 Editor 清理、渲染/驱动、后台工具和窗口问题。官网 issue 仅作为候选，不能以日志末行或近似 issue 代替本机线程证据。不升级源项目 Unity、不删 Library、不停用户许可证服务。

## 验证

报告构造按修改范围运行。退出调查使用有界运行；收集实际 Play Mode 退出、场景卸载、调用 Exit、原生进程结束四个阶段，若复现再捕获线程状态/栈。修复后重复最小触发条件和正式场景；偶然正常退出不宣布修复，强制回收始终失败。没有复现或符号不足时，明确证据能够支持到哪一层。

## 实施结果

### 用户随后调整：保留编辑器

用户明确表示无需每轮启动/关闭进程，由用户关闭编辑器。终止继续下载调试器，不再为了捕捉间歇退出故障反复开关。刚发起的调查轮 `20260911-211804-881` 已自行正常退出：Play Mode 停止、卸载场景、Exit、quitting、DomainUnload 均已记录，退出约 6.6 秒，未复现挂起。历史失败保留，不能据此确认修复或许可证根因。

实现边界随授权调整为保留同一隔离 Editor，下一轮在空闲会话内重新进入 Play Mode：

- Extend `SceneRaidScenarioConfig.cs`：显式 enabled/keepEditorOpen 标志，完成后禁用旧请求，防止用户手动 Play 时重跑旧自动化。Editor 读取新请求后配置该轮独立 productName，Runtime 仍严格验证隔离身份。
- Extend `SceneRaidEditorEntry.cs`：完成只退出 Play Mode、卸载测试场景，写带 runId/PID 的 ready 标记；空闲时接收新请求，先刷新源文件并等待编译结束，再开始下一轮。状态文件描述空闲/编译/Play 状态，不主动关闭用户编辑器。退出路径仅供显式诊断参数使用。
- Create `tools/agent-repro/SceneRaid.EditorSession.psm1`：会话 PID/启动时间/路径匹配，空闲状态验证、原生日志分轮切片；该职责独立于场景及报告。
- Extend `Invoke-SceneRaid.ps1`：默认保留并复用会话，仅向确认空闲的隔离副本同步文件；每轮仍持有工作区锁和源 SHA 检查。`-ExitEditor` 才执行退出诊断，默认即使失败也保留进程供调查。可显式缩短观察时长验证生命周期，不将短诊断当性能验收。
- Extend `AgentRepro.Workspace.psm1`：镜像文件前拒绝其他运行器覆盖仍有保留 Editor 的工作区。Create `Test-SceneRaidEditorSession.ps1`：验证 PID 复用、过期/忙碌状态和镜像前拦截、日志分段；`Test-SceneRaidReport.ps1` 增加保留模式的错误 PID、runId、清理状态和伪造退出码构造。
- Extend `SceneRaid.Report.psm1` / 构造探针：区分 EDITOR_RETAINED 和 PROCESS_EXITED；保留模式必须有匹配的 idle/成功清理标记，不把存活进程伪装成 exit=0。旧退出失败不重新归为成功。

验证同一 PID 至少连续两轮，独立 runId、输出和存档产品名，第二轮正常 Domain Reload；校验空闲/忙碌判定和报告故障。此后不再主动关闭保留的 Editor。NUnit 的单独批处理副本不能覆盖这个打开的工程，应使用其他工作区或用户关闭后再运行。

### 60 FPS 实现和验证

默认统计目标已改为 60，报告返回 targetFps、frameBudgetMs 和 maximumBudgetMs；显式传入 `-TargetFps 120` 仍可比较历史门槛。最初精确 60 FPS 构造出现 59 帧窗口，根因为 CSV 以微秒舍入后恰好落在滑动窗口边界，现以同一个半微秒偏移处理左右两端，不额外多算一帧。

47 项报告构造通过（`20260911-212558-918`），9 项会话保护构造通过（`20260911-212600-368`）。没有运行整项目测试。首次启动保留模式在启动 Unity 前遇到 PowerShell 将 File.Replace 的 null 备份路径转为空字符串，已改为明确的 NullString；首次失败日志保留，不算通过。

对既有 `20260911-210825-662` 原始帧重新按 60 FPS 计算，预热 10 秒后平均 89.84 FPS、最低滑动 1 秒 73 FPS、最大帧 27.38 ms，均满足对应新门槛；但 P99 20.53 ms、1% Low 43.56 FPS、超过 16.667 ms 的 150 帧仍说明尾部不达标。见 [重算数据](p4e_60fps_reanalysis.json)，没有改写历史报告，也不宣布整体性能通过。

### 退出调查当前能够支持的结论

历史挂起发生在退出 Play Mode、场景卸载以后；加阶段探针的本轮正常退出，说明既有日志末尾出现 Input System shutdown / Licensing disconnected 与成功、失败都相容，不能据此归因到这两者。当前仅能定位为间歇性的整个 Editor 退出阶段问题，尚无挂起时的原生线程栈。按用户新决定，保留该限制，停止以反复启动/关闭的方式继续取证。

查阅的官方依据：[EditorApplication.Exit API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorApplication.Exit.html) 描述直接退出编辑器；[Unity Windows 调试指南](https://docs.unity3d.com/2022.3/Documentation/Manual/WindowsDebugging.html) 和 [Microsoft 调试工具说明](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/debugger-download-tools) 用于确定原生线程/符号取证方法。近似的 [浮动窗口失焦退出 issue](https://issuetracker.unity.com/issues/9474) 覆盖 Unity 6，当前项目是 2022.3.62f2c1，不认定为同一根因。项目也没有该类另一个候选 issue 的 ECS 包，不据相似标题升级依赖。

### 保留和复用验证

- `20260911-212649-538`：启动 PID 23544，完成 10 秒原场景观察后进入 idle，evidence PASS，editorRetained=true、exitCode=null、sourceUnchanged=true。
- `20260911-212801-082`：复用 PID 23544，正常重载后第二轮观察，evidence PASS；21 项计数器齐全、0 游戏错误、0 失败指令。两轮的 persistentDataPath 分别以各自 runId 命名，没有沿用首轮存档产品名。
- Review 将跨进程读取的 idle 状态改成临时文件加原子替换，避免读取到正在写入的半份 JSON；`20260911-212946-882` 在同一 PID 完成代码刷新和第三轮。但我在运行中补写了受输入 SHA 守卫保护的工具 README，导致 source_changed，整轮保留 FAIL；游戏错误为 0，不能把它记成成功或游戏缺陷。冻结输入后再复跑。

保留会话的完成判定只说明这一轮已退出 Play Mode、清理且空闲；不声称修复了原生退出挂起。上述 10 秒观察是生命周期验收，不能替代完整搜打撤或稳态性能轮。

最终冻结输入后的 `20260911-213118-863` 再次复用 PID 23544，evidence PASS、0 游戏错误、0 失败指令、21 项计数器齐全、sourceUnchanged=true。独立脚本断言四个保留轮次均为同一 PID、四个存档产品名各不相同；三轮 PASS，一轮因我修改 README 而保留 FAIL。代码变更已在这个 Editor 中刷新并复跑。PID 23544 仍保持打开、idle，交给用户决定何时关闭。

结构化配置、阶段时间、进程/报告结果和证据哈希见 [p4e_session_results.json](p4e_session_results.json)。47 项报告构造、9 项会话构造、原场景多轮生命周期验证和架构审查完成，可以提交本阶段。后续恢复 P3 的自主流程与慢帧治理，不再将“必须每轮关闭 Editor”作为保留模式的成功条件。

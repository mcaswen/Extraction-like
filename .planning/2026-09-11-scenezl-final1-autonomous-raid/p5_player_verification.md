# P5：独立 Player 构建和场景验证

最新约定（2026-09-12）：用户确认战死为正常玩法结果，不再因为死亡停止矩阵或进入修复。后续按 [P5c](p5c_request_and_outcomes.md) 验证正常死亡终态、实际撤离者结算、运行错误与性能；以下曾因死亡暂停的内容保留为历史过程，不再是待执行的死亡调查任务。

## 最终实施结果

最新修复提交 `804e78d` 后重新执行同一输入的完整七轮矩阵：七轮全部通过，5 轮双人撤离，2 轮正常战死结局。Editor 平均 95.23 FPS，Player 平均 236.22 FPS，两项正常速度均满足平均 FPS >60。零运行错误、零失败/拒绝指令、零停滞，逐轮终态和仓库契约通过。

Editor 证据为 `20260912-022333-491`，可见 Player 为 `Logs/SceneRaid/20260912-023645-651/PlayerRun`。Player 正常退出，实际 4K / High Fidelity Game 相机渲染、源码和二进制一致性检查通过；Editor PID 60224 保留。结果、覆盖、帧尾和历史限制见 [最终报告](../../outputs/scenezl_final1_validation_report.md)。以下记录保留开发过程，不再是待执行事项。

## P5b：2026-09-12 实跑规划

P3h 规则已确认，修复及 21 项相关构造通过。接续完成 Player 运行，不再等待旧确认项。

- **Create `tools/agent-repro/Invoke-SceneRaidPlayer.ps1`**：接收明确的成功构建目录，复用 Workspace 的源清单和 Report 的证据/玩法核对。构建输入与当前源码必须一致，一个构建产品名只运行一个 `PlayerRun`，防止历史仓库串局；使用独占锁、实际退出码、原始 Player.log、进程超时、源码和可执行文件哈希。不承担游戏目标或库存策略。
- **Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs`**：增加显式 `quitPlayerWhenComplete` 和运行平台、开发构建、图形 API、帧限制结果字段；旧 Editor 默认行为保持。
- **Extend `SceneRaidBootstrap.cs`**：仅验证 Player 在初始化前应用已有 High Fidelity、4K 全屏窗口配置。已查本机实际桌面为 3840×2160，最终仍核对 Screen 和实际相机像素。
- **Extend `SceneRaidRunController.cs`**：写完原子结果、释放观察器后，仅显式配置的 Player 自动退出。常驻 Editor 仍由原 EditorEntry 收尾和保留，不增加游戏中的退出逻辑。
- **Extend `SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1`**：显式 PlayerRun 要求结果为 Player / Development、实际 High Fidelity、无 Profiler、无垂直同步或帧率上限；帧/计数器/背包/仓库规则复用。缺少平台证据不能冒充独立 Player。
- **Extend `README.md`**：补构建→一次 Player 运行的命令和独立结论；运行完成自动退出的是本轮验证 Player，Editor 保留。

控制流：SC07 成功构建 → 验证源和产物 → 生成 Autonomous / 1× 的 Player config → 实际图形 Player 自动背包 → 结果后正常退出 → 原证据、完成契约和平均 FPS >60 核对。每次都保留失败，不以退出码 0 代替玩法成功。

验收：报告边界构造、至少一轮正常速度 Player 完整搜打撤和平均 FPS >60。最终有限矩阵为最终玩法代码下 731 的三轮、1731/2731 各一轮 4×，再加一轮 1× Editor 和一轮 1× Player；同一常驻 Editor 复用同时覆盖重载。早期失败/已修复证据保留，不混作最终通过次数。

### 首次 Player 实跑发现，调整诊断

`20260912-002215-329/PlayerRun` 两人实际完成撤离，226.01 秒、11 次背包会话、0 程序错误，进程正常退出、源码及 exe/程序集哈希未变。但是渲染计数和相机像素为 0，循环超过 10 万帧导致采样溢出，最终 **HARNESS_FAILED**。因此数百次/秒的 Update 不能当作实际 FPS。现阶段尚不能区分隐藏窗口未渲染、管线未启动或观察事件问题，不修改画质来猜测修复。

离线汇总处理 210 万条计数器超过四分钟还未完成，已记录 postprocess-error.json 后仅停止该已结束 Player 的汇总进程。后续对零渲染等已确定无效环境先明确失败，保留原始数据，不继续昂贵计数器聚合。

具体调整：**Create `Assets/Scripts/Automation/SceneRaid/SceneRaidRenderEvidence.cs` + meta**，只读保存运行管线、相机启用/显示/RT、像素、焦点和渲染节流状态；**Extend `SceneRaidFrameSampler.cs`** 使用 URP 14 调用的 endContextRendering 事件、记录上下文次数；**Extend `SceneRaidRunController.cs`** 启动后 10 秒仍零渲染便保存诊断并明确失败；**Extend Player 入口** 支持 ObserveOnly 的短图形用例，先验证真正渲染后再花时间跑完整局。Source/Gameplay 不增加测试职责。暂不扩大帧缓冲，先解决零渲染的根因。

`20260912-003400-232/PlayerRun` 短用例在 10 秒明确失败：Main Camera 启用、4K、目标显示器 0、无 RT，URP-HighFidelity 资产存在，renderFrameInterval=1，但运行管线实例始终 null、上下文回调 0。切到 endContextRendering 后仍相同，说明不是旧回调单独漏报。隐藏启动是目前需对照验证的环境因素。新增显式 `-ShowWindow` 开关供可见窗口验证，默认仍隐藏；进程启动工具规定显示交互窗口需要用户明确指示，已准备具体开关后请求一次确认，不让用户自己操作游戏。正常场景 Editor 的后续矩阵可独立继续。

用户随后明确回复“允许显示测试窗口（推荐）”，可见 Player 对照已获授权，后续无需重复询问。先完成正在运行的 Editor 矩阵，再运行短可见对照，避免同时占用图形资源。屏幕截图只用于确认真实画面，帧率仍由连续原始采样和实际相机回调核对。

### 可见对照和矩阵首次结果

`20260912-004403-442/PlayerRun` 显式显示窗口，15 秒 Observe 通过，2912 个采样帧、2911 个实际渲染帧，管线实例为 UniversalRenderPipeline，4K / High Fidelity / DX11，截图确认场景、角色、HUD 和小地图正常绘制。与隐藏短用例相比，仅启动窗口方式改变后恢复渲染，定位到本机隐藏启动的环境问题。短观察均值 206.41 FPS 只用于检查，不当完整性能验收。

`20260912-004617-938/PlayerRun` 首次完整可见 Player **PASS**：244.15 秒，58555 帧、58554 实际渲染，平均 **241.40 FPS**；两人撤离、完整仓库契约通过，0 程序错误、0 指令失败、0 停滞，真实进程正常退出、源码和产物未变。无 Profiler、无帧限制，4K / High Fidelity，Development 构建。该结果属于 P3i 场景引用修复前配置；新场景仍需重新完成矩阵，不直接沿用为最终验收。

最终玩法下 731 三轮（`001748-171`、`003650-034`、`003834-557`）、1731（`004010-235`）完整通过。2731（`004159-072`）70.79 秒失败：Agent 2 撤离，Agent 1 血量为 0，0 程序错误、0 指令失败、0 停滞；停止矩阵，不以新种子或重复到通过替换原失败。

2731 的撤离指令在游戏时间 151.69 秒建立，155.39–161.90、164.18–170.26 秒分别中断反击，敌人真实掉血并完成，两次均 Resumed 同一原指令；201.48 秒第三次受击中断，205.14 秒死亡。后段伤害日志为鱼骨横扫 14、撕咬 4、水柱 23，最后剩余 6 血被横扫扣完，未出现交替重置追击。此证据证明新规则实际触发，不足以直接把死亡归为某个代码缺陷。下一步保留原失败，额外用相同 2731 做 1× 诊断，检查加速和正常速度差异；未经确认不改生命/伤害、反击规则或策略阈值。

## 目标和边界（P5a 历史规划）

承接已确认大规划第 4.2、8.1 和 P5：用同一 Scenezl_Final 1 场景及 Runtime Automation 复核独立 Player。先完成可重复构建入口，再运行 Player；当前反击策略尚待确认，构建成功不代表行为验收通过。

复用已保留的图形 Editor 和隔离副本，不新增常驻进程或改用户 Build Settings。构建阶段用 `Audit + buildPlayer=true` 请求，仍导出本次 prefab 展开审计；它不会进入 Play Mode，也不会操作背包或游戏目标。构建产物和机器可读结果放在对应 runId 的 Logs 目录中，源文件哈希照常前后核对。

## 具体文件和依赖

- **Create `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidBuildEntry.cs` + meta**：独立拥有 BuildPipeline 调用、临时 PlayerSettings、构建结果及选项。复用 `SceneRaidSceneAudit.cs` 的本轮审计，不把构建过程混进 Runtime Controller。
- **Extend `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs`**：在加载、审计后分派 Build，沿现有 Finish 返回空闲状态，保留 Editor。
- **Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs`**：增加 Audit 的构建标记，其他模式禁止构建。`SceneRaidBootstrap.cs` 沿用 Audit 不安装运行控制器的规则，不需要修改。普通 Player 仍不编译 Automation，验证 Player 仍需明确配置和隔离产品名。
- **Extend `tools/agent-repro/Invoke-SceneRaid.ps1`、`scene-raid-cases.json`**：增加 SC07 构建入口，复用现有隔离、锁、哈希、过程日志及空闲交接。
- **Extend `tools/agent-repro/SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1`**：核对构建结果、实际 exe、runId、场景、选项，不把仅有审计的空报告当成成功构建。
- 后续 Player 启动/退出、同一运行控制器的结果报告在构建验证后补充本文件，不先拼接未经验证的整套启动器。

依赖保持 PowerShell → Editor 构建 → Runtime Automation → Gameplay；没有 Gameplay → Automation 或 Runtime → UnityEditor。

## 构建决策

1. Windows 64 位、当前 Mono 后端、原场景单场景列表。本机已安装 Windows Standalone Support。
2. 4K / High Fidelity，保留用户 FFT 关闭的当前场景输入。隔离副本中的临时分辨率设置在 finally 恢复，正式源场景和项目设置不写入。
3. 首轮采用 Development 构建，但不启用 Autoconnect Profiler、Deep Profile 或脚本调试。原因是现有 21 项计数器包含自定义 ProfilerMarker，Unity 会在非 Development 构建移除相关调用；本轮首先保证同一证据契约可比较，明确报告构建类型，不冒充发行版结果。平均 FPS >60 仍是唯一时间门槛。
4. 用 `BuildPlayerOptions.extraScriptingDefines` 为该构建加入 `ANOMALY_SCENE_AUTOMATION`，不修改持久化 scriptingDefineSymbols，不触发 Editor 宏变更链。
5. productName 继续绑定单一 runId；本步不引入跨 runId 的通用 Player 存档配置，避免改动已验证的隔离契约。

API 依据：[Unity extraScriptingDefines](https://docs.unity.cn/ScriptReference/BuildPlayerOptions-extraScriptingDefines.html)、[非 Development Marker 可用性](https://docs.unity3d.com/cn/2022.3/ScriptReference/Unity.Profiling.LowLevel.MarkerFlags.AvailabilityNonDevelopment.html)。

## 验证

- 报告构造：有效构建，缺失/失败结果、错误 runId/场景、缺失 exe、错误构建选项均可判定，已有报告故障回归保持通过。
- 真实 SC07：实际 BuildPipeline 成功，有产物、构建耗时/大小/告警，Editor 仍可返回空闲，源输入未变。
- 构建失败保留具体编译/资产错误，再按归属提出小修复；不通过排除正式场景内容掩盖构建问题。
- Player 实际运行和矩阵仍待后续步骤，构建完成不能被统计为搜打撤成功。

## 实施结果

首轮 `20260912-000153-211` 没有进入构建：旧程序集先验证新 `Build` 模式，随后才可能触发 Refresh，导致请求被拒绝。保留 `build-attempt.json`，仅结束该轮等待进程，Editor PID 15476 保留。改为向后兼容的 `Audit + buildPlayer`，旧校验允许 Audit 后先刷新，重载的新入口再识别新增标记。报告仍必须看到真实构建结果，不能因旧入口仅审计便通过。

`20260912-000352-733` 首次编译发现构建 DTO 的告警/错误计数误写成 uint，而 API 返回 int，已按实际类型修正；此轮没有执行构建，保留原编译失败。62 项报告构造通过，等待真实构建结果。

`20260912-000427-007` 实际构建成功，61.10 秒、621,775,946 字节、0 错误、36 告警。但 Unity 版本中 `BuildOptions.Development.ToString()` 输出包含多个已废弃的零值枚举别名（包括 Il2CPP），导致字符串校验误拒绝。实际调用明确只传 Development，后端已单独检查 Mono2x。改为同时记录请求和 BuildReport 的整数位掩码，严格核对均为 1；名称仅作可读说明，不再用 Flags.ToString 判断构建选项。旧报告不覆盖。

最终 `20260912-000700-938`：构建和证据检查 **PASS**，热构建 11.43 秒，产物 621,775,946 字节；请求/实际选项均为 1，0 错误、36 告警，源文件哈希未变，Editor PID 15476 返回空闲并保留。63 项报告构造通过（`Logs/SceneRaidReportProbes/20260912-000638-705`）。

告警包括 P0 已记录的 Spline 两个缺脚本、Terrain tree 材质提示和 Shader 编译建议。Spline 的两个 GUID 在当前 Assets/Packages 元数据中均无对应，不能视为有效组件；本步只记录，没有为了消除告警删除场景内容或改材质。构建成功不包含实际 Player 搜打撤运行；P3h 策略待确认，确认后再以最终 Gameplay 源码开展 Player 和重复矩阵验收。

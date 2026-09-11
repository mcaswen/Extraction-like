# P5a：独立 Player 构建和验证准备

## 目标和边界

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

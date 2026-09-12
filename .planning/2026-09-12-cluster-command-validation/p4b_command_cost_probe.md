# P4b：补齐单次下令的成本归因

## 范围和依据

最终矩阵已记录 Submit 墙钟耗时、候选目录和帧级 GC/导航计数器，但这些帧级数据不能当作单次指令的分配和路径查询数，总规划 5.3 的成本归因仍缺一项。追加 Editor 构造测量首次/重复同目标、附近/远处、不同成员数和有限连发，随后验证最新任务实际移动。它不替代 1× 实局 FPS，也不改变最终矩阵的指令脚本。

已扫描 `AgentTargetCommandDispatcher.cs`、`TargetClusterDirectiveFactory.cs`、`AgentDirectiveValidationService.cs`、`AgentNavigationQuery.cs`、`AgentCombatApproachQuery.cs`、`ResourceClusterAuthoring.cs` 和原 SceneRaidVfxPerformanceTests。通用导航标记为 Anomaly.Navigation.Check，ResourceCluster 另有 NavigationPathCalculationCount；既有 VFX 测试已证明主线程 GC.Alloc Recorder 可以包围同步调用，需在新探针中再次校准，不能读取上一帧冒充当前调用。

## 文件与职责

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandCostTests.cs` 和 meta：独立拥有同步提交成本夹具、校准与扁平测量记录，避免在已有转换/库存大文件中堆性能职责。私有测量辅助只服务本测试，使用 Recorder 的当前线程逐样本模式、Stopwatch 和托管线程分配字节计数；校准后才解释数据，不新增生产统计接口。
- Reuse `AgentFactory.cs`、`EnemyFactory.cs`、`TargetFactory.cs`、`TestNavMeshBuilder.cs` 构造真实导航/成员；Reuse `AgentTargetCommandDispatcher.cs` 正式提交；Reuse `CaseArtifactWriter.cs` 写测量完成后的证据。
- Extend `tools/agent-repro/cases.json` 登记独立组。Extend `outputs/cluster_command_validation.json`、`outputs/cluster_command_validation_report.md` 汇总单次测量与环境边界，原实局逐帧数据仍分开。

不修改 Gameplay、Automation 运行组件、场景、Profiler 总开关或运行器。运行中的输入冻结完成后才写 Editor 测试/清单。测试新增不会改变已验证的实际游戏实现；最终场景报告仍明确基线 5e89d48，单独记录追加测试的源码差异与 XML，不能声称全部来自同一提交。

## 步骤和验收

1. 先校准空调用、已知托管分配、一次实际 Navigation.Check；Recorder 未注册、溢出、跨帧或不匹配立即失败。一般 Check 次数与资源实际路径计算分列；只有完全有效平面夹具能按源码分支解释实际路径数，不推广到所有失败分支。
2. 资源/敌人/撤离三类，各用 Near/Far 和单/多成员构造。每例记录该夹具首次调用、同目标重复调用、8 次有限連发，每次同步前后没有 yield、移动或目标变更。缓存“冷”只指本群未被查询，进程 JIT/其他系统未承诺冷启动。
3. 时间、主线程托管分配字节、GC.Alloc 样本数、通用导航检查、资源计算差值及成员数写入证据，计时之外才格式化/输出。检查每次正式接受、最新身份保持、旧完成不能清最新任务，最后实际移动；不预设未经测量的毫秒/分配硬门槛。
4. 执行新组，审查边界，合入最终报告和提交。实局已通过的数据不因测试新增而重复运行，也不借构造改善或替换 1× FPS。

API 依据：[Unity ProfilerRecorderOptions](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.html)，[当前线程同步采样示例](https://docs.unity3d.com/kr/2022.2/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)。关闭帧内合并才能读取逐调用样本，实际版本还需校准证明。

## 实际结果

最终矩阵已完成，运行基线仍为 5e89d48。新增 Editor 构造后 `084117-437` 为 12/13：12 组正式接受、重复/连发和后续移动通过，校准失败，已知 4096 字节分配的 GC.GetAllocatedBytesForCurrentThread 返回 0，整批字节读数不能采信。补诊断 `084337-220` 确認 GC.Alloc 的 UnitType 是 TimeNanoseconds，已知一次分配得到 1 个样本，其 Value=200 是时间而非字节；两次 Navigation.Check 得到 2 个同步样本。

据证据调整测量口径：保留准确的 GC.Alloc **分配次数**，删除不可用的线程字节指标，绝不将其 0 或样本耗时误写成分配字节。另以空调用、一次/两次已知分配校准计数。总规划的 GC 成本归因以单次分配次数加实局帧级字节分别呈现；不为取得额外字节指标引入全 Profiler 切换、帧树解析或生产 Hook，避免扩大本测试职责和干扰提交成本。该调整仅是测量单位/证据有效性的修正，不改变游戏、验收阈值或固定矩阵。

最终 `084528-638` **13/13 通过**，完整 NUnit 名称与登记一致。空调用零分配/零导航，已知一次/两次分配分别捕获 1/2 个样本，两次通用检查与 Buffer.CalculationCount=2 一致。12 组正式提交各保留 10 次记录，共 **120 次**；每次接受和当前身份通过，旧完成不能清最后任务，最后实际移动。资源组首次调用前还断言原路径计数为零。

单次最大 5.7247 ms，GC 25–66 次分配；同目标后续成本不能称零分配。资源组原生计算随成员从 1 增至 8，重复提交仍分别为 1/8；敌人/撤离在第一候选可执行的这批夹具中，通用检查分别是 4/2。候选排序和复杂空间失败仍有其他成本，不能从平面通过例推导全部最坏耗时。本阶段未发现需要扩张游戏性能修复范围的证据。

`p4b_cost_results.json` 保存 120 条原始度量、校准、XML/轨迹哈希和源码差异。与第六版全部场景输入对比，变化严格只有新测试 .cs、meta 和 cases.json，Gameplay、Automation 运行代码、场景、运行器和报告算法完全相同，因此保留 5e89d48 的最终场景成绩，不重跑未受影响矩阵。成本测试为独立 1× 无图形 Editor 夹具，实际图形 FPS 分开报告。

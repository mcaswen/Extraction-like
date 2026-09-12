# P3j：采集容量与长局期限不匹配

## 已确认问题

第五版 Player `073808-127/PlayerRun` 在 600 秒里实际渲染 162232 帧，`SceneRaidFrameSampler.cs` 固定只保存 100000 帧，结束时以 Evidence buffer overflow 判采集失败。物理撤离卡住仍是独立业务问题，不能用容量修复将该局改判通过。当前数据必须保留：帧 CSV 截断，FPS 不发验收。

## 文件、边界和实施

- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidFrameSampler.cs`：容量预算显式由本局墙钟上限推导，至少保留原 100000 帧，下限之外按每秒 1000 帧预留，600 秒上限对应 600000 帧。列表按实际数据增长，不预分配全部容量；仍有限上限，超限继续 FAIL。1000 是采集预算，不限制游戏帧率，也不是声称任意帧率都无限支持。现有 Recorder、CSV 格式和采样时钟不变，不在采集线程增加格式化/磁盘写入。
- Extend `SceneRaidRunController.cs`：传入已校验的 observeSeconds，只负责组合，不能因为当前卡住延长时限。
- Extend `SceneRaidScenarioConfig.cs` 中既有 `SceneRaidRunResult`：记录 frameCapacity，便于判断截断；不新增用户配置开关或弱化旧溢出判定。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidFrameSamplerTests.cs` 和 meta：构造 160001 次采样，证明原容量失败；修复后保留全部记录，低期限仍有容量上限，非法期限拒绝。纯采样数量构造不冒充真实渲染、FPS 或游戏帧序列。
- Extend `tools/agent-repro/cases.json` 登记新组，复用现有报告负例确保帧丢失/溢出仍拒绝。
- Extend `tools/agent-repro/Test-SceneRaidReport.ps1`：在原报告故障集合中加入采集溢出产生的 HARNESS_FAILED 终态，确认完整 CSV 也不能将采集失败改判通过；不修改报告接受规则。

职责仍为采样器拥有存储预算，RunController 只传配置，结果 DTO 只记录事实。没有新 Gameplay 依赖、线程、流式写入框架或性能口径变更。大于 60 FPS 的正式验收继续使用完整真实渲染帧，最大帧/GC 突刺保留诊断。列表增长的额外内存仅出现在超过旧 10 万帧的长局，预算是有限的；不为已完成的短局扩大预分配。

按 P3i 完成后，执行本阶段红灯 → 修复 → 边界/报告回归 → 审查 → 提交，再冻结最终场景矩阵。

## 结果

`081056-645` 容量构造在旧实现失败，160001 次采样触发 Overflow。实现后 `081210-309` **4/4 通过**：保留超过旧容量的全部记录、60/120 秒各自有限容量最后一帧可保留而下一帧拒绝、零/负/超限/NaN/无穷期限均在开始采集前拒绝。构造连续调用 Sample 只证明存储边界，不声称形成真实渲染帧或 FPS。

构造沿用 ReproductionTestFixture 的隔离 Play Mode 和完成检查。采样器默认 600 秒保持已有无参调用兼容；正式 RunController 显式传本局期限，结果记录 frameCapacity。原增长策略、Recorder、CSV、溢出失败判定和游戏时钟保持。原 78 项报告反例 `081228-167` 通过，随后补入精确 HARNESS_FAILED 反例进行最终回归。

最终报告回归 `081325-065` **79/79 通过**。审查确认采样器仍唯一拥有容量、帧和计数器存储，RunController 只传配置并记录事实，DTO 没有行为。额外分支为 O(1)，未修改逐帧工作量；上限增长只支持更长保留，仍可能因超过预算而明确失败。第五版截断证据不改判，P4 将在新提交后重新冻结全部原槽位。

# P4f：整帧慢帧归因

## 问题、目标和边界

SC03 `20260911-224707-038` 的 1 倍速整局逻辑核对通过，但固定预热 10 秒后 P99 17.96 ms、1% Low 48.78 FPS、最大 39.81 ms。最慢帧已有 PlayerLoop 计时 13.09 ms、Zone 1.66 ms，单靠已有 Marker 不能解释全部间隔。下一步先捕获 Unity 实际调用层级，区分业务、渲染、Editor 工作和等待，不依据平均值推断剩余瓶颈。

这是已确认 P4 诊断层内的小阶段，没有新的 Gameplay 架构或公共接口。保持 1 倍速、原画质和场景，只对显式 Profile 轮打开普通 CPU Profiler，禁用 Deep Profile，不拿开启后的 FPS 验收。常规 SC02/SC03 默认采样行为不变。性能验收仍另跑无 Profiler 的完整回合。

## 文件归属和实施

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidProfilerCapture.cs` | 读取 Unity RawFrameDataView，聚合主/渲染线程的 Total/Self/调用次数，保留慢帧局部层级和线程名，输出有界的诊断 JSON；仅存在于 Editor |
| Extend | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs` | 在显式 Profile 回合接线，域重载后初始化采样、停止前保存，结束恢复 Editor profiling 设置；不塞入统计算法 |
| Extend | `tools/agent-repro/Invoke-SceneRaid.ps1` | 提供 Profile 和 Seed 参数，复用已有隔离配置、源校验和进程所有权；Seed 也用于后续 731/1731/2731 回归矩阵 |
| Create | `tools/agent-repro/Measure-SceneRaidPerformance.ps1` | 复用现有已测试帧统计，按固定预热和原始慢帧输出独立 performance.json，整理计数器和临近事件，不覆盖历史报告 |
| Extend | `tools/agent-repro/README.md`、本目录审查和诊断记录 | 记录诊断命令、实际瓶颈和精度边界，后续修复先补具体文件小规划 |

依赖：Editor → Unity Profiler API / Automation Config；PowerShell → 原始证据 / 既有统计函数。采样有帧数和慢帧保存上限，保存截断/未采样数量。Raw Profiler 帧索引和观察时游戏 frameId 分列，绝不把二者假定成同一索引。Self 按立即子样本扣除计算，Total 保留原值；父子 Total 不相加。不自动更新任何生产调度或碰撞设置。

API 已核对 Unity 2022.3 官方 [RawFrameDataView](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Profiling.RawFrameDataView.html) 和 [ProfilerDriver 2022.3 源定义](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Modules/ProfilerEditor/Public/ProfilerAPI.bindings.cs)。数据视图显式 Dispose，停止时保存后解除引用。诊断轮的结果明确标识 performanceAcceptance=false。

## 验证和完成条件

先以历史原始 CSV 重算已知结果，随后编译并运行有界 1 倍速场景诊断，确认至少获得 Main Thread/Render Thread、PlayerLoop/EditorLoop、非负 Self 和真实层级。采样缺失、保存截断或异常单独报告；无数据不推断瓶颈。根据实际数据选择下一小修复，测试按该修复范围决定。原场景 NoProgress/动态追击的间歇失败仍保留，不能因一次完整通过关闭问题。

## 首次诊断和调整

- 新的性能脚本对 `224707-038` 原始 CSV 重算，精确重现平均 106.4709、P99 17.9609、1% Low 48.7757、最大 39.8062、最低 1 秒 60 FPS，状态 PERFORMANCE_FAIL。原始 CSV 连续性先校验，没有跳过中途慢帧。
- `230011-103` 90 秒 CPU 诊断未取得历史帧。Runtime Profiler.enabled 为 true，但 Editor 的历史消费开关未开启；无数据不是通过。补显式 ProfilerDriver.enabled 和连接/首末帧状态，零样本保存后报告错误。
- `230321-603` 短探针取得实际 Main/Render Thread，但采样器自己生成的 GC.Alloc 被下一次完整遍历，再产生更多采样，11 次读取消耗 30 秒。此轮受自测反馈污染，不能拿其慢帧归因到游戏。读取历史时暂停 Runtime Profiler，finally 恢复，打断递归计量；采集开销仍单独记录，诊断模式不进入 FPS 门禁。

### 有效层级取证和采样降频

`230512-733` 暂停自身计量后不再递归放大，但只读 latest 会偏向 Editor 周期，游戏层级覆盖不足。`230619-193` 改为读取相邻历史获得完整 Update/渲染调用，0 负 Self、0 丢失，但整轮采集本身耗时 54.3 秒，开销过大；不能据该轮 GC 或 FPS 给游戏定性。最终改为每 0.25 秒只读取最近两个帧，明确为抽样，不用它作为完整帧时间统计。统计帧数按是否有 PlayerLoop 子调用计一次，避免同帧多个同名父节点重复累计。

最终 `230922-236`：20 秒正常速度自主诊断，144 个线程帧索引取得主/渲染层级，跳过 878 个历史帧、0 负 Self、无截断、0 游戏异常/失败指令。采集共 1823.5 ms，仍有约 9% 诊断开销，不能做 FPS 门禁。主要证据：

- `Assets/Art/VFX/MudTidalAberrationVfx.cs` Update 合计 Self 181.21 ms / 144 个调用帧，约 1.26 ms/帧；8 个实例各自重算待机触手，BuildTentaclePath 每次 new 数组。
- `Assets/Art/VFX/RobotAnchorBeamVfx.cs` Update 合计 Self 61.08 ms，约 0.42 ms/帧；圆环、三角形和横线逐帧 new 数组。
- `Assets/Scripts/Gameplay/Raid/RaidMinimapController.cs` Update 最大 Total 5.27 ms，周期扫描目标和 Renderer 包围盒，运行中全图隐藏仍更新对应坐标。优化需保留动态图标和地图范围语义。
- 较长层级轮的开箱帧出现 Canvas 布局/PreRender、File.Read 和 SoundManager.LoadFMODSound；这是后续定向 Marker/缓存检查的线索，不能拿受采样污染的绝对 GC 峰值冒充未连接 Profiler 的结果。

阶段结论：可按具体文件继续性能治理。当前 CPU 采样明确标记诊断、不将丢失帧隐藏、不更改 60 FPS 门槛，保留 Editor。后续先收敛 P3f 的两项可构造逻辑边界，再分阶段优化上述热点，最终正常速度无 Profiler 复测。

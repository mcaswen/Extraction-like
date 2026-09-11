# P4g：持续特效路径缓冲复用

## 问题和范围

P4f 实测潮汐触手、锚点光束是持续脚本开销的重要来源。源码确认待机触手及其高光边、圆环、横线、三角形、喷流/光束的多条路径每帧分配数组。先消除这些确定分配，不降特效刷新率、不关闭特效、不改伤害或攻击时序；CPU 几何运算和小地图峰值留给后续实测选择，不能把数组复用说成解决全部慢帧。

## 文件职责

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Extend | `Assets/Art/VFX/MudTidalAberrationVfx.cs` | 本组件持有路径缓冲，TentacleArm 持有各自高光边缓冲，喷流的外层/中心/喷雾使用独立数组；长度变化才重分配 |
| Extend | `Assets/Art/VFX/RobotAnchorBeamVfx.cs` | 圆环/三角/横线、锁定线和四种光束各自使用已有实例的缓冲；同一帧同时需要的四种形状不能共享覆盖 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidVfxPerformanceTests.cs` | 正式特效实例的热路径分配、端点/线条独立性、改变点数后的缓冲适配；与 Agent 决策测试分离 |
| Extend | `tools/agent-repro/cases.json`、`README.md`、本目录结果/审查 | 添加定向用例和正常速度前后数据 |

缓存属于可视化实例，不新增全局池、公共接口或 Gameplay 依赖。路径生成仍是原方法的数学计算，不提取只有两个调用方、没有独立业务含义的通用框架。每条同时使用的路径有明确所有者；LineRenderer.SetPositions 完成复制后才复用临时圆环缓冲。Coroutine 跨 yield 不持有会被另一条同时活动路径覆盖的待消费数据。

## 实施和验证

先构造正式 VFX 热路径连续 100 次刷新，使用缓存委托调用现有方法、GC.GetAllocatedBytesForCurrentThread 量化当前分配，初始化和断言放在测量区间外。红灯后修改缓冲，再验证无逐帧数组分配、不同线条不别名、移动目标后端点正确、点数改变可更新。伤害仍由原敌人行为处理，选相关已有真实战斗回归。测试只测具体回调，不能把这个数值当整场景 GC。

完成后关闭 Profiler 跑 1 倍速完整场景，保持原画质和内容，比较原始 GC/帧时间尾部。若 GC 改善而尾部未达标，继续小地图/UI/音频的下一阶段，不改变验收标准。

### 扫描补充：中途禁用的 VFX 所有权

`MudTidalAberrationVfx.cs` 的攻击触手和喷流根对象只保存在 Coroutine 局部变量，直接创建在场景根；OnDisable 停止 Coroutine 后未销毁这些局部根，后面的正常结束清理不会执行。补“攻击中禁用组件”构造，先确认残留；红灯后由同一 VFX 组件字段持有本轮攻击根/触手，在正常结束和 OnDisable 均清理自己的对象，不全局查找删除，不改变攻击规则。这是已有特效所有权的修复，不新增业务边界。

补充重新启用构造：Mud OnDisable 销毁材质，Update 重建触手时未重新 EnsureRuntimeMaterials；Robot 的 EnsureVisuals 已包含材质恢复，保持原逻辑。只在 Mud 既有触手重建入口恢复其自有材质。

## 实施结果

- `20260911-233916-021` 攻击中禁用构造确认触手根残留，两个几何对照通过。四个分配测试均读到 0，但源码仍明确分配，不能信任未校准的 GC.GetAllocatedBytesForCurrentThread；本轮分配结论无效，保留原证据。
- 改用当前线程 `GC.Alloc` ProfilerRecorder，先要求能检测到显式 4096 字节分配，再在无 yield、无日志/反射开销的 100 次缓存委托区间记录分配次数；缺计数直接失败，不把 0 当通过。用法来自 [Unity 2022.3 当前线程分配示例](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread.html)。正式代码优化前重新取得有效红灯。
- `20260911-234111-661` 校准后有效红灯：MudIdle 1200、RobotGlyph 500、RobotLock 200、RobotBeam 1000 次分配/100 次调用；攻击禁用后残留、重新启用后材质丢失也均失败，两个几何对照保持通过。
- Mud 复用触手临时路径、每只触手的 rim 路径和三份独立喷流数组，Robot 复用固定圆环/符文/锁定线和四份光束路径；所有原数学公式、点数、逐帧更新频率不变。Mud 保存攻击根的所有权，OnDisable 主动清理；OnEnable 复用触手重建入口恢复材质，支持外部行为在首次 Update 前再次触发视觉。
- `20260911-234308-017` 8/8 通过，四个热路径分别 0/0/0/0 次分配/100 次调用，每例校准仍能检测已知分配。端点、不同线条、动态点数、攻击清理和重新启用均通过。该数字仅代表被调用的几何更新，不代表整场景或完整攻击无 GC。
- 开始 1 倍速 SC03 原场景完整回合，关闭 Profiler，保持 4K/High Fidelity 和用户关闭 FFT 的现状；下一步比较固定预热后的连续帧与 GC，不能以局部零分配宣称 60 FPS 通过。
- `20260911-234403-063` 1 倍速轮：181.97 秒，12 次背包会话，零运行错误、零失败/拒绝指令、零停滞；Agent 2 已结算撤离，Agent 1 满包撤离途中的反击战死亡，RaidFlow 正确进入失败终态，仓库保留 Agent 2 结算。该轮不是成功闭环，不用局部测试通过覆盖实际战死。
- 固定预热 10 秒后：平均 93.83 FPS，P99 18.3402 ms，1% Low 48.01 FPS，最大 41.6366 ms，最低一秒 61 FPS，PERFORMANCE_FAIL。不同战斗路线/死亡长度不能直接比较平均 FPS；本轮确认单纯数组复用不足以达到尾部门槛。继续 P4h 小地图无效扫描治理，战斗伤亡和交替伤害源切换保留诊断，不修改生命或伤害以凑成功。

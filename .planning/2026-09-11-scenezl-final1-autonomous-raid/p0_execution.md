# P0：真实场景零输入基线

日期：2026-09-11。状态：P0 采集基线通过，游戏问题已复现，未作整局或性能通过结论。用户已授权开始主规划，沿用已确认的 Automation → Gameplay 单向依赖和逐阶段执行闭环。

## 问题、范围和验收

以 `3f068d6`（包含最新地形、NavMesh）为输入，在隔离副本加载 `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`。先执行 SC00 的只读展开审计，再执行 SC01 的正常 Domain Reload、60 秒零输入 Play Mode。观察器在场景 Awake 前安装，不发指令、不操作背包、不修场景，不把正常等待开箱认定为故障。

P0 的通过条件是证据链可信：实际场景、两名角色和撤离注册可核对，初始化日志保留，运行有墙钟期限、帧样本、终态和进程结果，输入哈希前后一致。游戏异常独立报告，不能因采集成功就写游戏通过。60 秒不足以证明搜打撤完成或稳定 120 FPS。

## 文件归属和设计

以下路径均是主规划已确认文件。本阶段只创建实际需要的部分，不提前创建背包驱动和业务事件。

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Reuse | `tools/agent-repro/AgentRepro.Workspace.psm1` | 隔离、独占锁、版本和输入快照 |
| Create | `tools/agent-repro/Invoke-SceneRaid.ps1` | 明确配置、图形 Editor 进程、硬期限、前后哈希和报告调用 |
| Create | `tools/agent-repro/SceneRaid.Report.psm1`、`Test-SceneRaidReport.ps1` | 证据完整性、观察结束和游戏问题分离，故障注入验证报告拒绝伪成功 |
| Create | `tools/agent-repro/scene-raid-cases.json`、`scene-raid-profiles.json` | SC00/SC01、种子、期限、分辨率、诊断配置 |
| Create | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidEditorEntry.cs` | 显式启动、Game View、正常重载后接续、退出和恢复设置 |
| Create | 同目录 `SceneRaidSceneAudit.cs` | Unity 实际展开、稳定身份、Prefab/覆盖/引用/SO、组件及绑定审计，不调用修复服务 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs` | 配置和报告 DTO、明确会话参数校验 |
| Create | 同目录 `SceneRaidBootstrap.cs`、`SceneRaidRunController.cs` | Awake 前日志/种子、运行状态与终止协调，只有显式隔离会话才启动 |
| Create | 同目录 `SceneRaidIdentityMap.cs`、`SceneRaidReadModel.cs`、`SceneRaidObserver.cs` | 冻结层级身份映射、公开接口和集中只读反射、命令事件与低频快照 |
| Create | 同目录 `SceneRaidFrameSampler.cs`、`SceneRaidEvidenceWriter.cs` | 单调帧间隔、可用计数器及单位、有界事件/帧缓存、批量证据输出 |
| Extend | `tools/agent-repro/README.md`、本阶段文档和 `architecture_review.md` | 可重复入口、实际结果、风险和后续修订 |

Editor/Runtime 不交叉引用。只读反射严格限定 RaidFlow 的集合/状态字段，不写私有状态。身份使用带同级索引的场景层级键，审计映射到 GlobalObjectId 和 Prefab 源；后续动态出生关联在 P1 扩展，不能伪称 InstanceID 能跨运行重放。业务事件复用 `AgentDirectiveFeedbackChannel.Published`。

控制流：源快照 → 隔离同步 → 显式运行配置 → Editor 打开场景并审计 → BeforeSceneLoad 安装日志/事件 → Play Mode 初始化 → 低频状态快照和连续帧记录 → 观察时限结束 → 输出完成标记 → 退出 Play Mode、恢复编辑器设置、退出进程 → 报告门禁。

## 测量、测试和风险

- SC01 使用 1×、4K、High Fidelity、VSync 0，正常图形 Editor；进程层不使用 `-batchmode` / `-nographics`。Game View 固定尺寸并记录实际 Camera pixelRect/Screen 尺寸、渲染回调数；没有渲染证据不能作为有效图形采样。
- P0 可启用普通 Profiler 诊断，记录其状态；不启用 Deep Profile。脚本自动采样名称以实际 available descriptors 为准，缺失明确写 unavailable，P1 再补精确 Marker；本轮数据不作为最终性能验收。
- 观察期日志、快照、帧数据有容量上限；缓冲溢出、异常退出、缺失终态、源变化均拒绝证据通过。固定间隔心跳支持定位挂起，硬超时只终止本轮拥有的进程树。
- 场景审计和 Play Mode 分别保留日志；正常 Domain Reload 通过命令行配置、Editor SessionState 和初始化钩子恢复，不依赖旧微测试夹具关闭重载。
- 报告构造测试覆盖结果缺失、截断、零帧、零渲染、错误模式/运行 ID、事件丢失和游戏错误；真实 SC00/SC01 验证实际 API、场景生命周期和保存隔离。
- 源 Editor 仍打开，P0 记录进程竞争，不擅自关闭用户进程；因此本轮也不颁发性能达标结论。

## 实施结果

### 自动执行结果

| 运行 | 结果 |
| --- | --- |
| `20260911-191648-254` / SC01 | 正常 Domain Reload、60 秒零输入 Play Mode，1939 帧、1938 次游戏渲染，0 Error/Exception、69 Warning，2 次 NoProgress |
| `20260911-192037-982` / SC00 | 独立只读展开审计通过，精确定位丢失脚本对象 |
| `20260911-192117-201` / SC01 | 补充初始出口路径和注册表快照后复跑，1913 帧、1912 次渲染，0 Error/Exception、69 Warning，3 次 NoProgress；报告明确 `ISSUES_OBSERVED` |
| `20260911-192018-165` / 报告构造 | 17/17 通过：缺失/损坏结果、运行身份、帧/渲染/分辨率、丢失事件、序号、期限、预期角色、源变化、进程失败、游戏错误分层 |

三轮真实运行均进程退出 0、输入哈希前后一致。运行目录为 `Logs/SceneRaid/<runId>/`，包含启动参数、完整输入清单、Editor 日志、场景审计、事件、帧 CSV、结果和进程门禁；SC01 另保留普通 Profiler 原始文件。归档摘要见 [p0_baseline.json](p0_baseline.json)。没有修改场景、地形、NavMesh 或正式游戏逻辑。

### 当前场景与实际问题

1. 当前 Unity 展开为 **8 Zone、28 静态 Cluster、32 LootBox、2 ExtractionCluster/Point、2 Agent**；运行时多出 14 个实际敌群，注册 Cluster 共 42，不能误报静态数量不符。
2. 两个出口群都注册。初始位置查询雨林出口为完整路径，Agent 1/2 路长约 704/650 米；龙骨礁出口为部分路径，需在后续导航阶段核对接驳和有效落点。这里只读 CalculatePath，没有给角色 SetPath 或移动。
3. **三处 RaidFlow 的胜出者不稳定**：首轮 `Canvas/GameManager` 的 `MVP Raid` 生效，复跑变成根对象上的 `Large Island Wall Layout`。两轮均捕获要求撤离集合 `{1,2}`，但生命周期所有者仍需明确修复。
4. **零输入重复 NoProgress 已复现**：两个 Agent 选择员工食堂 `ResourceCluster_B`，Agent 2 交战完成后恢复搜索，随后反复失败/重选；Agent 1 长期处在相同 Search，距落点约 5 米。P1/P2 补实际资源等待和导航进展证据，再归因到避障、落点或监视器；不把所有静止都当成 bug。
5. `Spline[29]` 上有 **2 个丢失脚本组件**。初始化还有重复 TargetId 重生、TerrainCollider/MeshCollider、敌人出生/静态模型退化相关信息。完整告警保存在 Editor 日志，结构化重复项采用计数，不静默丢弃。

### 性能基线和限制

实际 RTX 5090 D、3840×2160 游戏相机、High Fidelity、1×、VSync 0，正常图形 Editor。两轮平均 **32.81 / 32.41 FPS**，P99 **54.54 / 57.47 ms**，最大 **769.78 / 825.71 ms**，保留启动间隔。平均按已测帧间隔总时长计算，P99 使用 nearest-rank；加载到首个可测间隔的时间另包含在运行墙钟中，不捏造首帧 FPS。

这些是带普通 Profiler、源 Editor 竞争的诊断数据，**不是最终性能验收**。PlayerLoop 和 GC Recorder 有有效值；三个自动 Invoke 名称的 Recorder 不可用，明确输出 unavailable，原始 Profiler 保留。下一阶段在业务所有者加精确 Marker，并关闭大体积二进制捕获复测。观察器自身 LateUpdate 累计成本也输出，但尚未包括所有事件回调，因此不宣称它是完整观察开销。

### Review 和后续调整

职责保持既定边界：Editor 审计/启动，Runtime 只读观察，进程层隔离，报告层区分采集与游戏问题。P0 未提前引入背包策略或发指令。正常 Domain Reload 已实际跨越，终态最后原子落盘，事件流序号/数量通过。P1 继续完善帧数据有效性、具名 Marker、结构化指令和停滞契约；P2 提供正式资源交互事件，避免通过距离猜测开箱；P3 明确修复多 RaidFlow，构造复现资源停滞及出口落点问题。

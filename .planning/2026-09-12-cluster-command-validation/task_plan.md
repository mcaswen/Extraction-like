# Scenezl_Final 1：玩家 Cluster 指令自动验证规划

日期：2026-09-12。状态：**用户已确认规划和两项行为基准，授权按小阶段闭环实施；P0–P3 已完成，P4 在 P3h 原生路径交付恢复后重新冻结 11 个最终运行槽位。前四版结果完整保留。**

用户已明确：只验证 Cluster，不验证 Zone；不需要模拟鼠标，可以直接模拟向周围或远处的目标群下达指令。沿用编码 Agent 自行运行、记录、定位和复核的方式。后续已确认远敌首次接近和同群后备扫描两项行为，按本规划逐阶段实施、测试、审查和提交。

## 1. 依据、问题和目标

### 1.1 已读依据和当前基线

- 遵循本会话提供的 AGENTS.md：规划展示目录、文件职责、依赖和关键决策，确认后实现；按小阶段实施、测试、审查、回写和提交。
- [Agent 框架](../../Assets/Docs/GameplayAgentFrameworkDesign.md)、[敌人概览](../../Assets/Docs/EnemySystemOverview.md)、[前轮修复设计](../2026-09-11-agent-reproduction/repair_design.md)。
- [自主搜打撤大规划](../2026-09-11-scenezl-final1-autonomous-raid/task_plan.md)、[最终验收](../../outputs/scenezl_final1_validation_report.md)、[自动验证入口](../../tools/agent-repro/README.md)。
- 阅读 P3h 反击、P3j 资源导航、P3k/P3l 双人停靠、P5c 请求交接和正常死亡的阶段记录，保留此前真实失败和未确认根因。
- 本轮调查工作树干净，HEAD 为 `3cbf8e4`；上轮最终运行的 Gameplay 基线是 `804e78d`。最新提交将三处场景关闭配置纳入版本。实施前重新审计实际场景、Prefab、配置、NavMesh 和输入哈希，不直接借用旧场景快照。
- `.planning/.active_plan` 属于其他历史任务，本规划不改写该指针。文档沿用 `.planning/<日期-任务>/task_plan.md`、`pN_execution.md`、`architecture_review.md` 的既有归档方式。

### 1.2 验证必要性

此前七轮限定游戏自主选目标，运行契约明确拒绝任何 `ManualTargetClick` 指令。现有构造已经覆盖基础不可达拒绝、撤离受击恢复、若干改令和双 Agent 生命周期，但没有完整覆盖真实场景中的远距离手动任务、连续改令、焦点路由和背包会话交接。

手动目标会持有独立优先级和锁，能选择自主发现范围以外的目标，还可能在搜索、交战、撤离中覆盖正在执行的任务，因此必须独立验证。

目标是回答：玩家发出的合法 Cluster 指令是否落到正确角色、正确目标，得到可解释的接受/拒绝结果，随后正确执行或终结；重复和改令后是否继续有进展；停止干预后能否恢复正常自主流程；全过程是否保留库存、结算和性能正确性。

### 1.3 范围和非目标

验证对象仅为 `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity` 中的资源群、活跃敌人群、撤离群。`EnemySourceClusterAuthoring` 虽有工厂分支，但正式 Picker 明确排除它，不将直接调用来源群的能力混入“玩家可点击群”的成绩。

不模拟鼠标、屏幕坐标、相机拖动、圈选重叠、UI 遮挡，不验证 Zone，不增加区域探索、清图、任务队列或新的玩家操作方式。直接调用 Dispatcher 只证明选中目标之后的指令链；相机外目标用例标记为命令接口验证，不声称该目标当时能被屏幕点击。

不以战死作为修复理由，不改生命、伤害、装备、掉落和生存策略，不强制每局生还。不重新开展 FFT、小地图或全项目优化。

## 2. 当前链路、规则和待验证风险

### 2.1 正式链路

场景使用 `Assets/Prefabs/TargetInputManager.prefab`，实例覆盖相机引用；Prefab 的 `_targetAgentId` 为空，因此正式点击默认走当前焦点角色。

```text
PlayerInputManager → VisibleTargetClusterPicker → AgentTargetCommandDispatcher
    → TargetClusterDirectiveFactory → IAgentCommandReceiver.TrySubmitDirective
    → AgentDirectiveLifecycleController → Validation / Brain / Navigation / Combat
    → AgentDirectiveFeedbackChannel → 正式顶部提示、只读诊断
```

本轮从 `AgentTargetCommandDispatcher.TrySubmitClusterCommand(cluster, targetAgentId, out request)` 接入。不得直接构造最终 Search/Engage/Extract 请求取代工厂，不直接写 Blackboard、NavMesh destination、Transform、血量或撤离结果。

| 群类型 | 当前生成的指令 | 验证语义 |
| --- | --- | --- |
| ResourceClusterAuthoring | Search，绑定资源群 | 正式解析可达未完成成员，走实际搜索和背包；满包自主撤离，余物保留 |
| ActiveEnemyClusterAuthoring | Engage，优先绑定最近存活敌人 | 当前语义是绑定一个具体敌人；目标结束后核对指令收尾和自主接续，不擅自要求一次点击清空整个群 |
| ExtractionClusterAuthoring | Extract，绑定最近可用撤离点 | 正式移动、presence、计时和仓库结算；有效伤害反击结束后恢复同一原撤离 |

手动改令通过时覆盖旧任务，旧回调不得清除新指令；新命令被拒绝时保留旧有效任务。资源任务不会被单纯看见敌人覆盖，但能被有效伤害中断；只对撤离保存恢复记录。连续受击保留当前有效反击对象；新有效手动命令清除旧撤离恢复记录。

### 2.2 两个优先构造风险

| 编号 | 源码依据和风险 | 本轮处理 |
| --- | --- | --- |
| R-C1 | `AgentDirectiveValidationService.cs` 可接受通向远敌的完整路径；`EngageEnemyActionNode.cs` 对尚未观察到的目标也启动丢失视线计时，可能尚未进入感知范围就 LostSight | 构造“从未见到但可达的远敌”和“已经见到、后来丢失”的对照，先保留现状红/绿证据 |
| R-C2 | `TargetClusterDirectiveFactory.cs` 对敌人/撤离群先取最近成员再校验，没有明确的同群后备扫描 | 构造最近成员不可执行、次近成员可执行，以及全群不可执行的对照；真实场景是否有该布局另行记录 |

**建议的行为基准，随本规划供 Review：**玩家已明确选中的远处敌人，尚在初次接近且持续有合法路径进展时，不应仅因还没进入感知范围而按“丢失视线”提前终止；首次观察后仍保留既有丢失视线和有限追踪约束。玩家选的是整个群，最近成员不可执行时建议继续尝试同群可执行成员，完全不可执行才拒绝。这两项目前不是已实测确认的 bug，不预先写修复代码；若 Review 希望保留当前有限追近/最近成员规则，先修改预期，不把规则分歧算程序失败。

## 3. 架构和文件归属

### 3.1 分层、数据流和边界

增加显式 `ManualCluster` 验证模式，原 `Audit`、`Observe`、`Autonomous` 的默认行为和验收保持。新模式只在当前 Editor 或定义 `ANOMALY_SCENE_AUTOMATION` 的验证 Player 中存在，普通 Player 不安装测试驱动。

```text
tools/agent-repro 配置、源快照、进程期限
    ↓ 冻结的场景脚本，随本次 config 归档
SceneRaidRunController：会话生命周期、采样、调用顺序
    ├─ Commands/SceneRaidClusterCommandDriver：有限步骤状态机
    │    ├─ SceneRaidClusterCatalog：只读枚举、身份和距离证据
    │    └─ 正式 AgentTargetCommandDispatcher：唯一目标命令出口
    ├─ SceneRaidInventoryDriver：既有正式背包操作
    └─ Observer / ReadModel / CarriedInventoryEvidence：观察和证据
    ↓ 原始事件、步骤结果、帧和库存快照
SceneRaid.Report → ClusterCommands.Contracts + 共享结算契约
    ↓
逐步骤、逐局、矩阵覆盖报告
```

依赖为 Automation → Gameplay、Editor → Automation、PowerShell → 原始文件。Gameplay 不依赖测试驱动、脚本 ID、报告或 Editor。只在复现证明确有缺陷后，以小规划修改正确归属的生产文件。

采用有限状态机表达 WaitingPrecondition → SubmitOnce → ObserveOutcome → Next/Stop；用组合复用现有会话和背包能力，用命令标识和结构化事件关联行为。无需通用脚本语言、依赖注入框架、通用事件总线或新业务 asmdef。

### 3.2 Reuse / Extend / Wrap / Create

下表路径均相对项目根目录。所有新增 C# 文件和目录一并生成 Unity `.meta`，没有文件移动或删除计划。

| 判断 | 具体文件 | 职责、改动理由 |
| --- | --- | --- |
| Reuse | `Assets/Scripts/Gameplay/Targets/Input/AgentTargetCommandDispatcher.cs`、`TargetClusterDirectiveFactory.cs` | 正式玩家命令入口和群到具体目标的解析；不先绕开疑似缺陷 |
| Reuse | `Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeRegistry.cs`、`AgentManualDirectiveLock.cs` | 正式焦点切换、Agent 身份、手动指令识别 |
| Reuse | `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveFeedbackChannel.cs`、`AgentDirectiveResult.cs` | 获取实际接受、拒绝、挂起、恢复和终态 |
| Reuse | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryDriver.cs`、`SceneRaidInventoryLedger.cs` | 原有开箱、搜索等待、转移和守恒，命令步骤不进入背包算法 |
| Reuse | `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs`、`SceneRaidIdentityMap.cs`、`SceneRaidNavigationEvidence.cs`、`SceneRaidItemEvidence.cs` | 角色/敌人/导航快照、稳定层级身份和物品编码；只在缺具体证据字段时扩展相应文件 |
| Reuse | `Assets/Scripts/Automation/SceneRaid/SceneRaidFrameSampler.cs`、`SceneRaidRenderEvidence.cs`、`SceneRaidRequestFile.cs` | 真实渲染、性能、请求交接；不另建一套进程或采样系统 |
| Create | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandScenario.cs` | 仅保存强类型场景脚本、步骤、触发条件和预期结果，不执行命令 |
| Create | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidClusterCatalog.cs` | 只读收集可测群、成员来源、当前距离和路径证据，稳定排序和解析目标，不下令 |
| Create | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidClusterCommandDriver.cs` | 持有步骤状态、一次下令、焦点操作、时间期限和停止条件；不负责库存或最终报告裁决 |
| Create | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandEvidence.cs` | 命令尝试及同步反馈的关联、序列化记录、前后状态证据；不选择目标、不判定整局通过 |
| Wrap | `Assets/Scripts/Automation/SceneRaid/SceneRaidCarriedInventoryEvidence.cs`（新增） | 将正式 UI 格子/装备、非当前角色保存快照包装为只读携带证据，支持没有开过箱的直接撤离，不依赖最终仓库反推预期 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs` | 显式 ManualCluster、脚本配置和校验；拒绝缺脚本、未知版本和不合法倍速 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs` | 仅组合新驱动、安排单帧动作归属、收尾；脚本解析、目标扫描和断言不得堆入此文件 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidObserver.cs` | 关联必要的瞬时事件和前后快照，保留原始失败，不全局屏蔽 Rejected/Failed |
| Extend | `tools/agent-repro/Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1` | 增加显式命令场景参数、归档完整脚本，沿用隔离工作区、哈希和期限；Player 不再硬编码只可 Autonomous/Observe |
| Create | `tools/agent-repro/cluster-command-scenarios.json` | 冻结脚本 ID、类型、距离分类、步骤、预期及硬期限，禁止嵌入可执行 C#/PowerShell |
| Extend | `tools/agent-repro/scene-raid-cases.json` | 新增 SC08（ManualCluster 4×）、SC09（ManualCluster 1×）；原 SC00/01/02/03/07 不重定义 |
| Create | `tools/agent-repro/SceneRaid.ClusterCommands.Contracts.psm1` | 从原始尝试/反馈/快照核对步骤、改令、路由和覆盖，独立于驱动自报状态 |
| Create | `tools/agent-repro/SceneRaid.Settlement.psm1` | 从现有契约提取共享终态、物品数量/布局和会话守恒核对，两个模式共用一份规则 |
| Extend | `tools/agent-repro/SceneRaid.Contracts.psm1`、`SceneRaid.Report.psm1` | 自主覆盖留在原入口；报告按显式模式组合共享结算和各自覆盖，保存原始失败数、预期拒绝数及非预期失败数 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandReachabilityTests.cs` | R-C1/R-C2、近远目标的确定性构造，单独承担空间和成员选择验证 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandTransitionTests.cs` | 焦点路由、改令/重复、背包交接和群完成后的执行链验证 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCommandHarnessTests.cs` | 驱动一次提交、期限/暂停、选择固定、证据关联和只读库存包装自检；与业务用例分离 |
| Reuse | `Assets/Scripts/Editor/AgentReproduction/Infrastructure/ReproductionTestFixture.cs`、`World/TestNavMeshBuilder.cs`、`World/AgentFactory.cs`、`World/TargetFactory.cs`、`World/EnemyFactory.cs`、`World/InventoryFactory.cs` | 现有真实 Play Mode、NavMesh、角色、目标和背包夹具；多成员构造若缺能力，只扩展对应 Factory |
| Extend | `tools/agent-repro/cases.json`、`Test-SceneRaidContracts.ps1`、`Test-SceneRaidReport.ps1` | 登记新增组，保证自主规则在公共提取后不退化 |
| Create | `tools/agent-repro/Test-SceneRaidClusterCommands.ps1` | 构造命令日志的正常/异常样例，防止模式、拒绝匹配或覆盖错误导致假通过 |
| Extend | `tools/agent-repro/README.md` | 新命令示例、模式区别、预期拒绝口径；清单数量以 cases.json 实际登记为准 |
| Create | `outputs/cluster_command_validation_report.md`、`outputs/cluster_command_validation.json` | 完成后交付人读/机器结果，关联版本、原始证据哈希和全部未覆盖项 |

同一表格单元格内未重复目录的文件，继承该单元格首个文件目录；World 文件继承 `Assets/Scripts/Editor/AgentReproduction/`。实施小规划仍列最终实际完整路径。

### 3.3 关键接口和控制顺序

建议接口仅属于 Automation：Catalog `Capture/Resolve`、Driver `Tick/Dispose`、Evidence `BeginAttempt/EndAttempt`；所有执行入口仍只有正式 Dispatcher。步骤包含 `scenarioId/stepId`、显式 AgentId 或 Focused 路由、目标选择描述、前置条件、等待的业务证据、游戏时间和墙钟期限。

新目录仅有 `Assets/Scripts/Automation/SceneRaid/Commands/` 和本规划目录；构造测试继续放已有 `Assets/Scripts/Editor/AgentReproduction/Tests/`，脚本和契约继续放 `tools/agent-repro/`。启动器把选定脚本完整嵌入当轮配置，序列化显式指定足够深度，保存脚本版本和 SHA256；Editor/Player 读取同一归档配置，不运行中再读可变化的清单。配置版本迁移必须保留旧三种模式的读取能力，新模式没有完整步骤时立即失败。深层步骤截断和未知字段/枚举另做往返探针。

RunController 每帧先消费已发生的反馈，判断是否有到期的脚本动作；若本帧需切焦点/下令，先执行该动作再进入观察，不让 InventoryDriver 在同帧另切焦点。其余帧按既有背包驱动工作。这只分配测试动作顺序，不推迟或冻结游戏 Update，不赋予驱动直接关闭/完成业务任务的权力。

整局内允许的写入仅为正式焦点切换、正式 Cluster 下令和正式背包操作。停止干预后的尾段不补发撤离命令，不靠高频重试促成通过。定向夹具允许在开始前构造 NavMesh/墙体/成员和位置；执行后使用正式状态机和物理。布局构造与无改写整局证据分开计数。

## 4. 目标选择、脚本和证据

### 4.1 近远分类和固定选择

P0 枚举当前真实群，记录场景/Prefab 来源、层级键、成员键、TargetId、Agent 起点、感知范围 R、平面/三维距离、完整路径长度和视线状态。动态活跃敌人群同时记录来源群/出生点及实际成员，不能只用运行时随机 TargetId 或 GetInstanceID 跨轮匹配。

附近定义为可交互距离外、目标成员平面距离不大于当前 R；远处定义为目标成员平面距离至少 2R。记录完整路径长度，不把群中心作为距离或可达性的唯一依据。敌人目标另设跨高差、起初不可见/观察后丢失两种标签。R 非正值或实际场景没有某类候选时，标记缺少前提，不能改阈值凑数。

每步骤触发时按冻结选择器和稳定身份排序确定目标，提交前保存全部候选及被选原因。入口只能选择群，不能把分析器找出的可达成员直接塞进最终指令。负例保留原目标，正例可按明确的可达条件筛选，但 R-C2 必须独立验证未经过成员预筛的群入口。发生失败后不得临时改选“能过”的对象。

所有跨轮的场景选择规则在 P0 固定。因目标自然死亡、被另一 Agent 搜完而缺前提，记为 coverage missing 或由预先声明的候选规则选择下一个；不能在看到结果后修改规则。真实场景缺少的边界由确定性夹具补充，报告明确两者来源。

### 4.2 定向构造矩阵

以下是用例族，不先把参数组合冒充已实现测试数量。P1 登记实际 NUnit 名称和参数数目。

| ID | 构造 | 核心判定 |
| --- | --- | --- |
| CC01 | 三种群 × 附近/远处基础路径 | 经 Dispatcher 生成正确类型和成员关系；有实际执行和正确终态，不以 Accepted 代替完成 |
| CC02 | 远敌：从未看见但路径持续前进；对照：已看见后消失/隔墙 | 验证 R-C1，记录首次可见、进展、计时起点；不取消真实遮挡、射程或失败期限 |
| CC03 | 敌人群：最近不可接近且无法开火，次近可执行；全部不可执行 | 验证 R-C2 的同群候选与拒绝；远程可跨高差，有真实射线/范围约束 |
| CC04 | 撤离群：最近断开，次近可达；全部断开 | 同群后备和拒绝；原场景每群若只有一个点，采用夹具验证多成员 |
| CC05 | A→A、A→B→A；Search/Engage/Extract 同类目标替换及跨状态替换 | 最新有效命令持有正确状态/路径，旧 CommandId 的完成不能结束新任务；有限操作后必须有进展 |
| CC06 | 撤离反击过程中改发资源/敌人/新撤离 | 新手动任务覆盖，旧挂起撤离清理；保持旧目标和恢复规则的既有回归作为对照 |
| CC07 | Focused 路由先 1 后 2；显式 AgentId 与当前焦点不同 | 路由到正确角色，另一角色的活动/挂起指令不被误改；死亡/已撤离角色拒绝且不转投其他人 |
| CC08 | 两角色分别去不同群、同一资源群、同一敌人群 | 指令和库存隔离，目标被另一人完成后及时收尾，不永久持锁、不重复结算 |
| CC09 | 打开箱 A 时程序改到箱 B/敌人/撤离，再按正式关闭流程推进 | 旧会话可控失效，箱 A 余物守恒，不能完成 B；暂停只冻结游戏时间，恢复为原 1×/4× |
| CC10 | 无效/禁用/已完成/不可达群，保留一个正在执行的合法命令 | 拒绝原因和提示正确，旧任务及挂起记录保持，无身份信息的早期拒绝也能准确归档 |
| CC11 | 直接撤离且未开箱；满包后手动资源；两人死亡/撤离不同顺序 | 正常玩法终态，预置装备/已有物品仍核对；满包不伪造资源完成，不把战死当 bug |
| CC12 | 同帧按顺序发 8 次指令，之后完全停止 | 最后有效命令执行、反馈队列有界，收尾无订阅泄漏；不要求 8 条提示全部逐一显示 |

CC02、CC05、CC09、CC12 和复现出来的时序缺陷保留 1×/4×对照；纯前提校验、候选成员和序列化边界不机械重复两种速度。旧 F1/Lifecycle/Navigation/Feedback/R5/SceneResourceApproach 等仅选受改动影响的组。

CC09 在背包打开期间直接注入命令，属于命令接口和会话交接的边界验证，不声称被 UI 遮挡的鼠标当时也能下令。CC12 同理属于有限连发压力，不模拟持续每帧刷命令。

### 4.3 真实场景脚本矩阵

所有脚本包含冻结的触发条件和有限指令预算，不按固定每隔几秒盲发。SC08 是逻辑配置，SC09 是正常速度配置，具体玩法脚本由 `scenarioId` 区分。

| 脚本 | 目标和步骤 | 结束方式 |
| --- | --- | --- |
| MC01 近远基础 | 从真实起点验证两名角色的附近/远处资源、活跃敌人；实际处理后指定撤离。自然战斗/死亡可能打断后续覆盖，按实际记录 | 观察 Raid 正式终态和仓库 |
| MC02 执行中改令 | 搜索移动 A→B、交战→资源、撤离反击→新目标，另覆盖一次相同群重复下令；各步骤有真实状态前提 | 脚本完成后停止发令，观察自主接续至终态 |
| MC03 焦点和双人 | 默认焦点路由、显式路由对照，两人共享资源群/敌人群，包含背包会话中的改令边界 | 完成步骤后自主接续至终态 |
| MC04 拒绝和早撤 | 对真实已失效/完成群做有前提的拒绝检查；独立新局验证未搜刮即明确下达撤离。不存在负例前提时不得强改正式场景 | 正常拒绝核对、两人正式死亡/撤离和结算 |

MC04 的早撤与动态失效检查拆为不同子脚本/新局，防止先搜刮再声称“未开箱撤离”。MC01 若一次回合无法自然提供全部类型/距离，按 P0 冻结的分局清单执行，不要求角色先清图再撤离。

初始执行矩阵：各冻结脚本 731、4×一轮；MC02、MC03 在同一常驻 Editor 再各重复一轮以检查静态/会话泄漏；MC01 的完整链使用 1731、4×一轮；MC02 做 731、1× Editor 对照；MC01 做 731、1×可见 Player。实际局数在 P0 根据分局清单登记后冻结，不能边跑边删失败步骤；不机械重跑原自主七轮。

若 Gameplay 修复涉及自主路径/发现或公共结算，补一轮 SC02 自主冒烟和受影响的已有构造。若修改了模式/报告公共部分，原自主故障探针必须仍能识别手动指令污染。

### 4.4 事件、身份和观测

保留现有 `events.jsonl`、`frames.csv`、`counters.csv`、场景审计、原始日志、进程/渲染/源码证据，新增 `command-catalog.json`、`command-attempts.jsonl`、`command-steps.json`、`carried-inventory.jsonl`。

每次尝试记录 runId/scenarioId/stepId/attemptId、触发事件、帧/游戏时间/墙钟、要求的 AgentId、提交前后焦点、群稳定身份、成员候选、距离/路径/可见性、实际 request CommandId/TargetObject、返回 bool 和同步反馈。异步执行用 CommandId 继续关联。

Dispatcher 的早期拒绝可能发布 default request，没有 CommandId。Evidence 必须在同步调用前建立 attemptId 并订阅本次调用内的反馈，以调用边界/序号关联，返回后校验唯一对应；不能按“这一帧有个 Rejected”或相同中文文案猜配。双 Agent 同帧下令仍顺序调用、分别闭合证据区间，额外或无法归属反馈直接报关联异常。

复用角色位置/缩放、实际/期望速度、当前/挂起指令、导航终点/角点、目标距离、进展计时、敌人生命/护盾/可见性/射击结果，补记命令前后即时快照。普通低频快照不能替代首次可见、改令和终态事件；关键等待条件在 Driver 每帧轻量观察，只有变化才写记录，不每帧扫描全场或导出所有物品。

## 5. 验收规则和防止假通过

### 5.1 分开报告三个结果

1. **证据完整性**：请求/脚本/源指纹一致，帧/日志/身份完整，真实运行、真实渲染，正常收尾。驱动超时、丢事件、未知成员、缺结算快照属于 harness/evidence failure。
2. **业务正确性**：每条命令接受、拒绝、执行、中断、恢复、取消或终结均符合该步骤预期；没有未解释错误、永久持锁、无进展、串角色或串库存。
3. **覆盖完成度**：实际发生的类型、近远、状态切换、焦点、双人和终态逐项列证据。死亡使尚未发生的步骤记为未覆盖，死亡本身不变成业务失败，也不能自动补齐覆盖。

保留原始 `behaviorFailures` 总数，另列 `expectedRejections` 和 `unexpectedBehaviorFailures`。只豁免预先登记、唯一匹配 attemptId/Agent/群/原因/阶段的预期拒绝；未知 Failed、其他 Agent 的拒绝和时窗外事件不可一并忽略。诊断 R-C1/R-C2 的现状失败不得通过“预期拒绝”规则变成最终修复通过。

步骤不能仅凭 `Accepted` 通过，至少要有对应动作证据和合法终态。资源需正确箱子/会话/物品变化，战斗需目标生命变化或明确被另一角色完成的归因，撤离需真实计时和入库；被新命令替代的旧步骤可以以预定 Superseded 结束，但新步骤仍须执行。

### 5.2 共享结算，保持原自主门槛

原 `Test-SceneRaidCompletion` 同时负责结算不变量和自主活动覆盖，直接复用会把手动指令、提前撤离和未开箱视作失败。提取共享结算函数到 `SceneRaid.Settlement.psm1`，自主入口继续要求原有活动覆盖且禁止手动指令；ManualCluster 入口使用本轮步骤覆盖。不得增加一个全局 `ignoreFailures` 或关闭仓库核对。

直接撤离仍可能携带预置装备，因此不能把“没有开箱账本”等价为零物品。CarriedInventoryEvidence 在角色库存就绪、合法库存变化后和指定撤离前保存只读快照，活动 UI 与非当前角色快照分开读取。已检查 `InventoryScreenController.TryCollectExtractableItemsForAgent` 会经过结算快照同步，不把它未经验证当作纯只读探针；包装优先读取格子/装备和 `_inventorySnapshotsByAgentId`，对私有结构做一次类型校验，深拷贝为证据，不回写、不切焦点、不创建缺失库存。若需要新增生产公开只读接口，必须先在小规划列明边界供确认。

最终预期仓库 = 初始仓库 + 已撤离角色最后有效携带快照的可结算物品；独立按 ItemID、数量、容器规则、装备计算，不能读取游戏返回的最终结算量作为预期。库存修改只允许正式背包驱动，携带证据由相同变化触发及时更新；若角色消失前没有可信快照或有未核对变更，证据失败，不能按空库存通过。每个实际会话仍验证箱内余物、转移、关闭写回和守恒。

正常死亡沿用现有判定：所有必需角色均已死亡或撤离，死亡角色不入库，已撤离角色已结算，终态 UI/暂停正确。不为覆盖修改角色生命，不因死亡开始平衡或策略修复；缺步骤用固定独立用例补齐，不能无限挑选生还种子。

### 5.3 期限和性能

- 逻辑 4×，物理 fixedDeltaTime 保持游戏原值；背包和提示采用既有 unscaled 时间，不逐帧强写 timeScale。
- 每步骤有游戏时间和墙钟双期限，等待前置条件也有限；长路径根据提交前路径长度/实际速度推导预算，再加明确裕量，战斗另列上限，不能统一 2 秒判定远途失败。冻结预算不改变生产超时规则。
- 单局墙钟上限先沿用配置允许的 600 秒以内；如 P0 路径预算超出，预先拆局，不为某次失败临时延长。外层进程沿用有限退出期限，常驻 Editor 保留，Player 正常退出。
- 1×正常速度整局平均 FPS 严格 >60；P99、1% Low、最大帧保留诊断。4×数据只用于逻辑/诊断，不发性能通过。
- 额外记录单次 Submit 耗时、候选数、路径查询次数、GC 和有限连发后的进展，比较附近/远处及冷/热查询；不先承诺某个未经测量的毫秒阈值。若有昂贵突刺，先区分游戏校验与测试采集。
- 沿用 4K / High Fidelity、关闭 FFT 的当前场景、不限帧；可见 Player 已获授权，复用原渲染证据。平均性能不能靠长时间停在背包/结算页稀释，继续额外报告运行与暂停区间均值。

### 5.4 自动化自身测试

驱动验证：每步骤只提交一次、目标解析可复查、未知版本/缺前提明确失败、暂停不误触发游戏时间超时、停止后不再发令、所有事件订阅清理。反射只读库存验证调用前后库存/焦点/会话/时钟完全不变。

日志构造覆盖：合法接受并完成；预期拒绝；原因/角色/目标错误；多余拒绝；丢失 CommandId 或同步关联；旧回调清新任务；伪造只有 Accepted；死亡前覆盖缺失；结算不匹配；Autonomous 混入手动命令；无渲染、截断文件、重复步骤、超时。必须先证明报告会拒绝这些异常，再运行正式矩阵。

## 6. 实施阶段与闭环

| 阶段 | 小规划与工作 | 阶段验收、实际结果 |
| --- | --- | --- |
| P0 真实目标清单和冻结 | `p0_execution.md`：复用 SC00，先实现只读 Catalog 及其场景枚举自检入口，核对可点击类型/来源、两人起点、近远路径，固定场景脚本及各局期限 | 每个步骤有具体身份或确定选择规则，列明场景缺少的前提。**已完成** |
| P1 确定性指令构造 | `p1_execution.md`：CC01–CC12 按风险拆参数；先运行 R-C1/R-C2 和关键改令，保留现状，不先改生产逻辑 | 已确认规则有红/绿证据，失败能区分实现、场景、夹具和玩法分歧。**已完成** |
| P2 场景驱动和报告 | `p2_execution.md`：ManualCluster 模式、Commands 文件、只读携带证据、共享结算提取、故障探针，最短真场景冒烟 | 原自主报告仍严格，新模式能识别预期拒绝且抓住额外错误，一次真实下令到终态证据闭合。**已完成** |
| P3 原场景执行与定位 | `p3_execution.md`：运行冻结 4×脚本和必要 1×对照，采集实际故障，按小修复计划逐项红→修复→绿→相关回归→审查→提交 | 所有失败有状态，正常死亡不进入 bug 修复，未确认机制不静默改动。**已完成** |
| P4 最终验证和交付 | `p4_execution.md`：修复后冻结输入、复跑受影响脚本和覆盖矩阵、1× Editor/Player 性能，必要 SC02 自主回归 | 步骤/业务/证据/覆盖均满足约定，平均 FPS >60；输出最终报告和架构审查。**进行中** |

每阶段先写实际完整文件清单和职责判断，阶段结束回写首次结果、调整理由、复跑证据、架构审查和提交号。提交标题采用项目既有前缀，正文使用自然中文，不默认推送。修改影响相关历史绿色覆盖时重跑相应覆盖，其他旧测试不机械全跑。

生产修复的预期归属：初次接近/丢失视线在 `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs` 及其必要独立状态抽象；群成员选择在 `Assets/Scripts/Gameplay/Targets/Input/TargetClusterDirectiveFactory.cs` 和既有可达候选/查询层；任务交接在 `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveLifecycleController.cs`；背包有效性在 `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs` 和 `InventoryScreenSessionContext.cs`；实际场景配置错误才修改对应 Scene/Prefab。这里只指定职责方向，不预先承诺所有文件都需修改。

## 7. 风险、备选和 Review 要点

| 风险/歧义 | 判断和处理 |
| --- | --- |
| 远敌首次接近和追丢采用同一计时 | CC02 先构造，建议区分首次接近与观察后丢失；若改追踪语义须按第 2.2 节确认，不直接无限延长超时 |
| 点击群是否要求清空全部成员 | 按当前具体敌人任务收尾，群全清不是本轮验收要求；不引入命令队列 |
| 敌人目标会移动，种子相同轨迹不同 | 冻结选择规则和触发条件，记录实际身份/位置，不把随机种子当确定性重放 |
| 负例早期反馈没有命令身份 | 用同步调用边界建立 attempt 关联；不按原因全局忽略，不为测试先改生产公共接口 |
| 背包驱动与焦点/下令互相影响 | 单帧测试动作所有权由 RunController 安排，游戏 Update 正常推进；专门测试打开中的改令，不偷偷总等关包 |
| 从公共结算提取后弱化旧自主验收 | 先运行原故障探针，保持旧模式拒绝手动指令和原活动覆盖；无开箱撤离另有可信携带证据 |
| 无法可靠读到非焦点角色携带状态 | 证据失败，优先只读包装；需要公开接口时重新展示设计，不用打开背包/切焦点改变被测行为补数据 |
| 整局没有自然出现某种状态或角色提前战死 | 明示未覆盖，独立构造补边界；不改生命、强制伤害、刷生还轮次 |
| 测试过度泛化、运行时间膨胀 | 仅有限脚本和强类型配置；不造通用脚本平台，按风险选回归和 1×对照 |

本次 Review 的关键架构选择是：在既有 SceneRaid 下增加隔离的 ManualCluster 模式；命令驱动、候选目录、证据和契约各自独立；共享结算不变量，自主/手动覆盖分开；新模式通过正式 Dispatcher，保持 Gameplay 单向被调用。行为建议是第 2.2 节两项，其他规则沿用已确认结果。

## 8. 本次规划交付

已完成源码入口、当前 Prefab/场景引用、已有测试、运行器、报告和库存读取边界的调查，形成上面的实施文件清单、构造矩阵、场景脚本和验收口径。规划自审见 [architecture_review.md](architecture_review.md)。

原规划交付时没有修改代码或启动测试。后续实施：P0 已提交 `fa2b981`，完成真实目录和 11 个最终运行槽位冻结；P1a 已获得两项风险红灯，修复后 10 项构造、23 项相邻回归通过，见 [p1_execution.md](p1_execution.md)。P1b/c、P2、P3 已完成，细节见对应阶段文档；最终矩阵第一次遇到真实 SetPath 拒绝后停止，按 P3d/P3e 完成修复和反例，P4 重新冻结全部 11 槽位。不能把构造或开发期冒烟替代最终场景验收。

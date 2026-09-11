# P3k：箱边双角色避让停滞

## 证据和小规划

最终矩阵 `20260912-015149-132`（2731 / 4×）两人撤离，但同一帧 918 两次 Search/NoProgress，矩阵停止；前四轮不再被当作已完成的七轮验收。两名角色血量分别 300、224，本次不涉及战死修复。

失败调用栈分别来自 `MoveToTargetActionNode`、`SearchResourceActionNode` → `AgentNavigationMotor`。员工食堂 ResourceCluster_B 的同一箱子旁，两人的完整路径终点相距约 4.5 米，角色中心相距约 6 米。两角色缩放都是 3，导航 radius 分别 1.5 / 0.5，实际 CapsuleCollider 世界宽均约 3 米。Agent 2 离终点约 0.20 米，Agent 1 约 1.40 米，实际速度与期望方向不符。

当前假设：导航避让半径和真实身体尺寸不一致，造成相邻停靠点不能同时到达。先用真实场景构造单人/双人、1×/4×对照，不能只据半径数字改配置，也不放宽到达范围或延长停滞期限来掩盖阻挡。

## 文件归属与验证边界

- **Reuse `AgentNavigationMotor.cs`、`AgentNavigationQuery.cs`**：构造调用正式寻路和移动，只观察 Arrived/Stalled、位置、速度与路径。此阶段不预设修改通用 Motor。
- **Reuse `ReproductionTestFixture.cs`、`RuntimeWait.cs`、`CaseArtifactWriter.cs`**：隔离存档、真实 Play Mode、有界运行和证据输出。
- **Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidResourceApproachTests.cs` + meta**：独立负责真实场景箱边移动/避让重放。只在构造夹具中布置记录位置、隔离非被测行动；完整搜打撤运行保持无位置、血量或指令写入。
- **Extend `tools/agent-repro/cases.json`**：登记确认后的构造用例。
- **候选 Extend `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`**：仅在对照确认实际导航尺寸误配后修正对应 Agent 的序列化覆盖。用户已授权修复场景配置错误，仍保留用户 FFT inactive 改动。
- 如证据指向资源停靠点选择而非配置，先补充 `ResourceClusterAuthoring.cs` 的具体职责方案，再实现；不新增全局避让或资源预约系统。

验收：先保存真实红灯，修正后相同构造到达，单人和两种速度对照通过，旧导航/位移用例保持通过。随后重跑失败种子及最终冻结矩阵。历史 NoProgress 是否同源只按实测证据判断，瞬时 Unreachable 仍单独保留。

## 实施结果

红灯 `Logs/AgentReproduction/20260912-015617-514`：6 项中单人 1/2 各 1×/4×全部通过，双人 1×/4×均为 Agent 1 Stalled、Agent 2 Arrived。说明本问题不需要 4×或战斗才能发生。夹具隔离了整局其他决策，所以不声称重现了原日志中两人同帧失败的全部时序。

场景精确覆盖为来源 GUID `80344fa5a46b42f408819a2cfecf5f9d`、NavMeshAgent fileID `5491856885365467676` 的 `m_Radius=1.5`；对应 `Assets/Prefabs/PlayerPrefab/Agent.prefab` 原值是 `0.5`，同一 Pawn 的 CapsuleCollider radius 也是 `0.5`，场景 Agent 2 沿用该值。实施决定：只把 Agent 1 此项场景覆盖恢复为 `0.5`，保留缩放、实际身体、速度、到达判定、避让模式和停滞期限，避免重复施加三倍半径。

修正后 `Logs/AgentReproduction/20260912-015741-084` **15/15 通过**：相同箱边构造 6 项、P3j 资源导航 7 项、P3i 敌人配置 2 项，进程正常退出，输入未变。双人 1×/4×均 Arrived，单人不退化，敌人仍为 30 个且全部注册。

历史 `224354-827` 的 Agent 1 阻挡位置约 `(243.67, 3.01, -51.76)`，与本次重放停滞位置相近。这提高了同源判断的可信度；独立构造确认的是半径误配所致箱边双人阻挡，不声称复刻旧轮每个帧间事件。瞬时 Search/Unreachable 仍没有因果证明。

最终矩阵按相同七轮内容重新开始，先执行 2731，随后 731 三轮、1731、一轮 1× Editor 和一轮 1× Player。调整顺序是优先复核已观察到的问题，不改变种子集合或通过门槛。

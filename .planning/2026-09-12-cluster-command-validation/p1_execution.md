# P1a：远敌初次接近、群内后备成员

状态：P1a 实现、红绿验证和相邻回归完成。P0 已提交 `fa2b981`。

## 文件和职责

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandReachabilityTests.cs` + meta：先增加远敌初次接近 1×/4×、敌人群后备/全不可达、撤离群断开/禁用/已完成成员的七项构造，全部通过正式 Dispatcher。之后追加首次观察后丢失的对照和到达/交战终态验证。
- Extend `tools/agent-repro/cases.json`：登记 ClusterCommandReachability，保存旧实现红灯，再修复和复跑。
- Extend `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs`：区分玩家指定敌人的初次接近和已经观察过后的丢失视线；该节点本来拥有追踪计时，新增每命令阶段事实仍归该节点。自主/伤害反击继续既有有限追踪，不改射线/射程。初次接近使用首次完整路径长度和当前速度估算固定预算 `2 * pathLength / max(speed, 0.1) + lostSightTimeout`，只初始化一次；导航失效和无进展仍由原 Motor 有界失败，目标持续移动也不能无限延长预算。
- Create `Assets/Scripts/Gameplay/Targets/Input/TargetClusterDirectiveCandidateSelector.cs` + meta：仅对已构造的候选指令按传入距离排序，使用正式 Validation 选第一个可执行成员；全不通过时仍返回最先候选，让生命周期发布真实拒绝原因，不在 Selector 发布反馈或写指令。
- Extend `Assets/Scripts/Gameplay/Targets/Input/TargetClusterDirectiveFactory.cs`：枚举实际敌人/撤离成员生成候选，交给上述选择器；Resource、EnemySource 分支不变。Dispatcher 公共接口不变，正式接受前的校验仍保留。

## 构造与验收

远敌位于 80 米，R=20，移动速度 8，正式射程小于 R，首次接近超过原 2 秒超时；用独立临时配置和被动敌人避免死亡/数值干扰。近处不可执行成员放在断开导航面，远处可执行成员在同一完整面；最近敌人同时用墙/射程避免跨高差可射击的合法例外。全群不可达必须拒绝，不能为了通过接受任意路径。禁用和已完成出口需要跳过。

红灯后确认定位，再修改上述生产文件。实际掉血作为远敌接近成功证据，1×/4×均不能出现中途 LostSight。保持实际观察后丢失的原超时、伤害反击有限追踪、跨高差合法开火和完全断开拒绝的相关回归。

## 结果

首次红灯 `Logs/AgentReproduction/20260912-033303-482`：7 项中 5 项失败，2 项通过。远敌 1×/4×在约 2.02/2.01 游戏秒、位置 x=14 时 LostSight，尚未造成伤害；敌人群有后备成员、撤离最近成员断开/禁用也遭拒绝。全敌群不可达、跳过已完成撤离成员原本通过。

按上述文件归属实现两项修复，追加观察后丢失 1×/4×、动态远离目标固定期限三项对照。绿色 `Logs/AgentReproduction/20260912-034437-314`：**10/10**。远敌首次真实伤害在 9.610/9.609 游戏秒，角色 x=72.31/72.40；动态目标在 22.010 秒有界失败，期间角色实际移动到 x=174，没有无限续期。

相邻回归 `Logs/AgentReproduction/20260912-034906-923`：**23/23**。通过分号 TestFilter 选择 SceneRaidRetaliationProgressTests、F1ExtractionInterruptTests、DirectiveLifecycleTests、SceneRaidCombatApproachTests、F5UnreachableDirectiveTests、CommandFeedbackTests；报告 Group 沿用启动标签，23 项是这些实际 XML 测试，不能读成新组有 23 项。源指纹检查通过，无测试缺失或基础设施失败。

候选选择只在显式群命令入口执行；排序 O(n log n)，最多 n 次正式 Validation，路径查询复杂度仍依赖导航网格，不是逐帧新增扫描。首次接近预算每个手动命令只查询/读取一次路径角点，动态追踪继续既有 0.1 秒缓存。没有本阶段真实场景 FPS 验收结论。

其他 CC05–CC12 的状态切换用例在 P1b 小规划继续，不将以上十项写成所有用例族已经完成。

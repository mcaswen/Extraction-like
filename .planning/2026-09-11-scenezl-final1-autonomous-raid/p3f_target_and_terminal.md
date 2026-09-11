# P3f：具体敌人绑定、伤亡后的回合终态

## 待构造问题

1. `EngageEnemyActionNode.TryResolveEnemyTarget` 先用包含父级搜索的泛型方法找 ActiveEnemyCluster，再找 EnemyHealth。若指令明确绑定某个群内敌人，可能改成群中距离最近的另一只，造成验证目标、执行目标不一致。先构造指定较远成员、较近成员受墙遮挡的真实攻击，要求指定成员掉血、其他成员不被替换；保留群指令可选择成员的行为。
2. `RaidFlowController` 捕获 required 集合后，一人死亡时仍有存活角色会继续运行；存活者随后撤离，集合永远不全，销毁后没有新的死亡通知，可能既不成功也不失败。构造两种结束顺序和两人同帧完成，验证已有“所有 required 都撤离才成功”的规则、独立结算不回滚、无角色可行动时有失败终态。不把伤亡算作成功，不调整角色生命或敌人伤害。
3. 原场景搜索 NoProgress 和瞬间追击 Unreachable 的原因尚未确认。增加失败当帧近场碰撞体、NavMeshAgent/Obstacle 探针，保持失败，后续依据现场构造，不先改容差、避障优先级或瞬移角色。

## 文件边界和实施顺序

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCombatTests.cs` | 明确敌人身份和群成员选择的实际投射物反例 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidTerminalTests.cs` | 伤亡/撤离顺序的正式 RaidFlow 终态构造，独立于存储文件事务测试 |
| Reuse | `World/AgentFactory.cs`、`World/EnemyFactory.cs`、`World/TargetFactory.cs`、`World/TestNavMeshBuilder.cs`、`Infrastructure/RuntimeWait.cs` | 建造隔离场景，真实受击、移动和撤离计时，不直接写胜负结果 |
| Extend（红灯后） | `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs` | 明确成员的目标解析优先于其祖先群，群级解析保持原行为 |
| Extend（红灯后） | `Assets/Scripts/Gameplay/Raid/RaidFlowController.cs` | 统一判定剩余可行动角色，在死亡和单人撤离提交后检查失败终态；忽略已提交撤离、尚未 Destroy 的角色 |
| Create | `Assets/Scripts/Automation/SceneRaid/SceneRaidNavigationEvidence.cs` | 失败现场的只读邻域取证，独立于常规角色快照和游戏导航计算 |
| Extend | `SceneRaidReadModel.cs`、`SceneRaidObserver.cs` | 仅失败/拒绝时附加近场证据，正常快照不执行全场或邻域扫描 |
| Extend | `tools/agent-repro/cases.json`、`README.md`、本目录规划/审查 | 添加新定向用例和红绿证据，原场景失败不得被构造通过掩盖 |

Gameplay 的目标选择仍归既有动作节点，RaidFlow 仍拥有胜负和结算；无新公共接口、无跨模块反向依赖。探针依赖正式 Gameplay，不写黑板、Collider、速度、路径或血量。若证据显示需要改变游戏规则或模块边界，另行提出，不借本次修复扩大范围。

## 验证

先跑两个缺陷的构造获得真实失败，再实现。相关 Combat、Lifecycle、独立撤离/仓库失败回归按影响面选择，不全项目重复。随后 4 倍速种子 731/1731/2731，全部逐轮保留错误、指令终态、最终结算；需要判断时间/物理差异的触发条件回到 1 倍速。角色正常战死应明确报告失败，与程序卡死分开，不宣称保证任意战斗随机过程都存活。

## 实施结果

- `20260911-231201-227` 红灯：明确敌人根/子物体两种绑定均未伤到指定成员；DeathThenExtraction 未进入终态。ExtractionThenDeath、BothSameFrame、BothSequential 三个对照通过。
- `EngageEnemyActionNode.cs` 优先解析目标自身/父级 EnemyHealth，再按原逻辑解析群；明确成员不再被祖先群重选。射程、视线、墙体、伤害和群级成员选择不变。
- `RaidFlowController.cs` 复用一个私有剩余存活角色检查，死亡和成功提交单人撤离后调用；排除已提交但尚未延迟销毁的角色。保留原 required 集合，伤亡不会变成全员成功，已结算数据不回滚。
- `SceneRaidNavigationEvidence.cs` 在失败/拒绝时记录 10 米邻域 Collider、导航 Agent/Obstacle 和独立 CalculatePath 结果；Collider 缓冲满明确标记。由 ReadModel 适配、Observer 接线，常规快照和正常接受不做邻域扫描。已有探针构造增加位置/命令/路径不变断言。
- `20260911-231405-009` 共 14/14 Play Mode 通过：SceneCombat 6、SceneTerminal 4、F1 2、真实独立撤离后敌人重新攻击 1、仓库提交失败终态 1。首轮过滤器中写了不存在的 LifecycleBoundaryTests，没有匹配额外生命周期测试，实际数量按 XML 14 记录，不宣称覆盖该类。
- 4 倍速种子 1731 原场景复跑启动，源文件冻结。
- `20260911-231503-314` 种子 1731：88.86 秒墙钟两人撤离，原始 evidence/game/contracts 全部 PASS，零错误/失败/拒绝/停滞。
- `20260911-231722-756` 种子 2731：约 64.4 秒墙钟两人撤离，原始 evidence/game/contracts 全部 PASS，零错误/失败/拒绝/停滞。两轮均保留原场景内容和正常容量限制，没有发玩家目标指令。
- `20260911-231932-084` 种子 731：76.89 秒两人撤离，但 2 次反击 Unreachable，矩阵为 2 PASS / 1 ISSUES_OBSERVED，不能标记全部通过。失败探针独立 CalculatePath 同样为 PathPartial，末端距敌人平面约 2.84 米；反击接受时已在攻击范围外，是 Lifecycle 对已知伤害源的允许追击规则，不能归因于下一帧被击退。下一阶段构造断开导航面上的可达射击位置。
- 本轮暴露探针对 Terrain/非凸 Mesh 调用 ClosestPoint 的告警；修正为显式标记的 bounds 近似，凸体仍保留精确值，增加非凸 Mesh 构造。探针另记录 agentTypeID/areaMask，避免仅凭高差推断导航类型。原始告警日志保留。
- `20260911-232557-898` 修正后的非凸网格探针构造通过，无新增告警。当前阶段两个明确修复和诊断提交，导航末端问题移入 P3g，不把矩阵失败改写成成功。

# P3g：断开导航面上的远程接近位置

## 证据、目标、边界

731 / `20260911-231932-084` 两次反击接受时已超攻击距离；Lifecycle 允许对已知伤害源发起有界追击，执行下一帧直接计算敌人脚下位置得到 PathPartial，立即 Unreachable。探针的独立路径末端距敌人平面约 2.84 米。先以构造确认：敌人所在导航面无法走到，不等于没有可达射击位置。

修复目标是保留跨高差远程交战规则，使用已有导航面内可达的接近位置。非目标：跨越断层、放宽射程、穿墙、改变受击中断规则、忽略真正失败、修改敌人生命或测试场景结果。仅使用部分路径末端这一有界候选，不引入全地图战术寻点；没有合适候选仍明确失败。

## 文件归属和控制流

| 决策 | 具体文件 | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Gameplay/Agent/Navigation/AgentCombatApproachQuery.cs` | 无副作用的战斗接近位置查询，持有调用方独立缓冲。先检查敌人地表点完整路径，只有 PathPartial 才尝试末端；末端必须有完整路径、预测身体瞄准点和面向敌人的枪口均通过原射程/视线检查。它不设置 NavMeshAgent 路径、不改指令状态 |
| Reuse | `AgentCombatNavigationTarget.cs`、`AgentNavigationQuery.cs`、`TargetVisibilityQuery.cs`、`CombatAimPointResolver.cs` | 敌人脚下点、完整路径约束、范围和墙体检查保持单一实现 |
| Extend | `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationQuery.cs` | SamplePosition 显式限定调用 Agent 的 agentTypeID 和 areaMask，避免选到另一种导航面；完整路径、到达和高度规则不变 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/AgentCombatShooter.cs` | 新增只读的给定位置/朝向枪口预测检查，配置/枪口坐标解析由 Shooter 所有，不写 LastShotResult，也不旋转角色 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs` | 使用自己的查询缓冲和最多 0.1 游戏秒的接近位置缓存；新指令、敌人明显移动、导航参数变化、路径失效时重查。正常开火仍检查真实身体/枪口，移动仍由 Motor 负责 |
| Extend | `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveValidationService.cs` | 具体敌人（含其子物体）请求的接近验证使用同一查询，避免接受和执行规则分裂 |
| Extend | `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs` | 已经过发现范围/LOS 的敌人，使用同一可执行性查询；目标选择和失败记忆仍归原处 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCombatApproachTests.cs` | 断开低平台可射击、隔墙和远距拒绝，真实移动/投射物，查询无副作用、缓冲独立、失败后的恢复路径 |
| Extend | `tools/agent-repro/cases.json`、`README.md`、本目录审查 | 登记构造和红绿结果、731 原场景复跑 |
| Extend | `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs` | 新接近查询可能在 Motor 前失败；反击的独立路径取证改用当前敌人脚下点，避免读取上一条撤离目的地，常规快照仍保留 Motor 原始目标 |

依赖为 Commands/Targeting/Actions → Navigation 查询 → Perception 和只读 Shooter；Shooter 不依赖 Navigation/Commands。查询和调度分开，不把战斗策略放入通用 Motor。新增 Shooter 方法仅查询假想枪口，实际开火流程不改。该局部边界属于已授权导航/交战修复范围。

## 验证和风险

1. 构造两片断开导航面：角色起点在射程外，己方末端在射程内；旧代码必须失败，修复后实际移动并造成投射物伤害。包含不同高度和缩放/baseOffset。
2. 相同几何加墙、目标移至垂直/水平射程外，仍拒绝。已有 RaisedRoot 断开高台用例继续通过，不把所有 PathPartial 当成功。
3. 查询不得移动/旋转角色、设置路径或改 Shooter 上次射击结果，重复查询缓冲独立。真实枪口变化每帧继续验证，缓存过期重新计算。
4. 相关 SceneCombat、Lifecycle、F1 和新构造按影响面回归，随后原场景 731 4 倍速；时间/物理边界构造按 1 倍速执行。保留任何伤亡、NoProgress 和新失败，不能只凭最终撤离判通过。

主要风险：路径末端的浮点采样可能落入另一导航类型；采样应使用 Agent 的 agentTypeID/areaMask，提交路径仍要求 PathComplete。预测使用当前身体偏移和朝向敌人的枪口，动画后实际发射仍进行二次检查；动态墙体不会被预测结果绕过。

## 实施结果

- `20260911-232809-435` 初始 4 项构造中两个可射击布局（根/子物体绑定）被旧代码拒绝，隔墙和过远两个对照通过，红灯确认。
- 先实现部分路径末端的完整可达/预测射击验证，接入执行、接受和候选扫描，正在跑新旧 Combat 回归。
- `20260911-233013-139` 10 项中实际移动/投射物已成功，两个新用例末尾错误地假设角色根低于 2 米而失败；正式 Prefab 为 3 倍缩放、baseOffset=1，根高约 3.13 米合法。将断言改为相对初始高度保持、平面不越过己方导航面，保留失败证据，不修改角色参数。补充伤害反击后恢复、枪口超程、查询不写状态、独立缓冲和新增动态墙体检查。
- `20260911-233239-908` 测试夹具向旧 Dispatcher 传 AgentId 导致编译失败，没有执行测试；改为已有 AgentIdValue 字符串接口，修正导航测试类名后重新运行，未将基础设施失败计作逻辑结果。
- `20260911-233358-411` 共 52/52 Play Mode 回归通过：新接近 7、原 SceneCombat 6、Lifecycle 6、F1 2、Navigation 8、RangedSpatial 12、ScenePerformance 11。真实投射物跨断开低平台命中，隔墙、远距和枪口超程拒绝；旧高台拒绝、双方跨高差、路径缓存边界保持通过。
- `20260911-233616-350` 原场景 731 / 4 倍速：evidence/game/contracts 直接 PASS，零运行错误、零失败/拒绝、零停滞，两名角色自主撤离。随机种子固定不意味着依赖帧时序的战斗路线完全相同，因此该轮是整局回归，确定性根因修复由断开平台构造证明；旧 NoProgress 尚无新复现，不宣称根治。

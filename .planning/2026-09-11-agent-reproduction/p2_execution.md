# P2 小规划：目标绑定、候选扫描与空间交战

- 前置：P1 通过后提交，再实施本阶段。沿用已确认的大方案边界。
- 目标：修复 F2、F6、R2/R3；敌人和 Agent 使用真实三维范围、射线/墙体约束；远程跨高低差可以索敌和造成真实弹体伤害。
- 非目标：调整敌人数值/技能配置、改为全新感知调度框架、迁移程序集。

## 文件职责与依赖

- Create `Assets/Scripts/Gameplay/Enemy/EnemyCombatTargetBinding.cs`：目标 Transform、ICombatDamageReceiver、IExternalMovementReceiver 同一身份的原子解析与有效性检查；不可绑定死亡、禁用或已离场对象。
- Create `Assets/Scripts/Gameplay/Enemy/EnemyTargetSelector.cs`：复用 AgentRuntimeRegistry 候选列表，逐个应用空间查询，稳定选择当前可见候选；保留兼容旧 Player 的有效目标入口。
- Extend `Assets/Scripts/Gameplay/Enemy/Player/PlayerTargetResolver.cs`：复用绑定有效性，避免旧 Player 标签回退重新捞起死 Agent。
- Extend `Assets/Scripts/Gameplay/Enemy/{Enemy,RangedEnemy,AnchorSentinel,HunterBoss,ModernStrander,TidalAberration,AncientStrander}BehaviorController.cs`：所有具体控制器接入绑定；巡逻扫描所有候选，战斗保留仍有效的当前目标；失效时重新绑定并清空旧接收器。Boss 减速走 IExternalMovementReceiver。
- Wrap `Assets/Scripts/Gameplay/Enemy/EnemyVisionUtility.cs`：保留既有调用签名和 Gizmos，真实空间查询委托给 `Gameplay/Perception/TargetVisibilityQuery.cs`。
- Create `Assets/Scripts/Gameplay/Perception/ProjectileSweepQuery.cs`：纯 Unity 物理层的扫掠碰撞排序与发射者排除；不持有 Agent/Enemy 业务身份。
- Extend `Assets/Scripts/Gameplay/Agent/Combat/AgentCombatShooter.cs`、`Assets/Scripts/Gameplay/Enemy/RangedEnemyBehaviorController.cs`、`Assets/Scripts/Gameplay/Enemy/{BulletController,EnemyBulletController,HunterBossAnchorProjectile}.cs`：三维瞄准、枪口至瞄准点遮挡、每段轨迹扫掠、墙体消耗弹体、一次命中。技能/持续伤害的空间门禁位于已批准的 Combat 工具/运行时文件。
- Create `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidate.cs`、`AgentTargetCandidateCollector.cs`：明确成员目标、身份、位置、距离、可见/可达事实；供 Discovery 和下一阶段 Decision 共享。此层依赖 Registry/Perception/Navigation，后者不反向依赖选择策略。
- Extend `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs`：候选经范围、射线、可达性过滤；挂起反击不再冒充真实“可见”事实。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/EnemyTargetBindingTests.cs`、`PerceptionCandidateTests.cs`、`RangedSpatialTests.cs`：真实控制器换目标、A 遮挡/B 可见、3D 目标/墙/枪口/高速子弹和跨导航岛场景。

## 实施与验收

1. 先构造死目标仍留场、巡逻 A 近但遮挡而 B 可见、远程跨高度、墙体阻挡，保存修复前的真实失败。
2. 完成绑定/候选选择，验证三元组归属和死亡/销毁/禁用/离场分支。
3. 接入空间门禁和弹体扫掠，验证正反控制：空路能伤害、墙体不伤害、上/下坡可射击、不可达敌人无需贴近脚下。
4. 检查无直伤兜底穿墙、无射线水平化、无过期接收器；将实际运行结果写回本文件及架构审查。

## 实际结果

- P1 提交：`1700e67`。
- 首轮基线 `20260911-012745-579`：Ranged 存在空接收器；AnchorSentinel/TidalAberration 被真实 VFX 运行时 Assert 阻断。修复前记录保留。测试中的 Transform 身份断言改用 SameAs，避免 NUnit 将 Transform 当可枚举集合比较而丢失身份语义，修正后再跑基线。
- 附带小修边界：Extend `Assets/Art/VFX/RobotAnchorBeamVfx.cs` 的 `CreateParticleSystem`、`Assets/Art/VFX/MudTidalAberrationVfx.cs` 的 `ConfigureParticleSystem`，先 StopEmittingAndClear 再配置 duration。责任仍是各自 VFX 创建，不改控制器和技能规则。由真实预制体自动运行暴露，不能通过忽略 Assert 让测试虚假通过。
- 远程基线 `20260911-012851-912`：跨高度玩家弹体未伤害目标、枪口发射未拒绝墙后目标。薄墙用例将补空路命中对照，未将一次无伤害直接当作阻墙已正确。
- 修正身份断言后的基线 `20260911-013032-036`：明确复现 Ranged、AnchorSentinel、HunterBoss 死亡留场不换目标，以及 F6 最近目标遮挡；其他四种死亡分支控制组通过。
- 绑定与射击首轮：`20260911-013515-306` EnemyTargets 8/8；`20260911-013602-683` RangedSpatial 3/3。继续补正反控制与空间/技能集成。
- 文件边界补充：Extend `Assets/Scripts/Gameplay/Targets/Authoring/ActiveEnemyClusterAuthoring.cs`，新增只读 `CopyAliveEnemiesTo`，由持有初始/运行时成员的 Authoring 负责枚举，不让 CandidateCollector 反射私有成员或只看 InitialEnemies。
- 归属补充：`Assets/Scripts/Gameplay/Enemy/EnemyHealthController.cs` 内现有 CombatDamageUtility 的接收器解析使用 parent/当前 target 子树，移除 scene root 的跨兄弟子树回退；`PlayerTargetResolver.cs` 同步。避免墙和其他无接收器 Collider 在共同场景父节点下误绑定第一个 Agent。仍由既有伤害解析层负责，不混入空间算法。
- 审查补齐：七类控制器换目标时撤销旧攻击阶段/缠绕，不转移旧锁定；近战与持续缠绕也重检遮挡。范围技能的遮挡查询允许调用方提供 Collider 排除谓词，业务层排除敌人躯体（避免爆心处敌人挡住整个 AOE），墙体仍阻挡；Perception 不依赖 Enemy 类型。
- `20260911-015217-842` 远程扩展 8/8 通过；`20260911-015451-642` 目标绑定扩展 12/12 通过，包含禁用、销毁、离场及共用场景父节点。先前小平台 NavMesh 构造失败与两处编译错误已经修正，均保留失败日志。
- `20260911-015358-302` 空路 DOT 对照暴露爆心处敌人躯体遮挡其他范围受害者；修正上述排除策略。发现目标测试在 NavMesh 首帧定位之前查询地板中的眼点，补真实一帧初始化后断言。
- 最终感知/技能回归 `20260911-015641-271` 4/4 通过：范围技能射程、爆心遮挡、DOT 动态墙正反对照、发现候选去重、已知攻击者不冒充可见、三维范围及 Trigger 排除。P2 共 24 个用例通过，进入 P3。

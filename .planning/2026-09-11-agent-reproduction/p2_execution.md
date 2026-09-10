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

- 待 P1 提交后开始。

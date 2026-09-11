# 目标选择、Agent 执行与交战：修复架构方案

- 日期：2026-09-11。
- 状态：**架构已确认并完成 P0–P5 实现。最终 75 例三轮 225/225 通过，实际文件调整与验证见阶段文档和验收报告。**
- 主规划：[task_plan.md](task_plan.md)。原始证据：[逻辑审查](../../outputs/agent_target_execution_combat_review.md)。
- 本文负责生产代码修复架构；主规划负责构造样例、运行取证和分阶段验收，两者共同约束实施。

## 1. 已确认规则与验收目标

| 编号 | 用户已确认的行为 | 验收结果 |
| --- | --- | --- |
| F1 | 受击中断撤离并反击，反击结束继续撤离 | 原撤离目标与指令身份可恢复；连续受击不丢失恢复记录；不发生 Combat／Extraction 振荡 |
| F2 | 明确 Bug，修复目标失效与重绑定 | 死亡、禁用、销毁或撤离后换到有效目标，Transform／伤害接收器／移动接收器始终属于同一实体 |
| F3 | 手动资源指令允许被伤害中断 | 有效受击进入反击；仅发现敌人仍不能抢走有效手动资源任务；不再测试“手动资源一律不可打断” |
| F4 | 明确 Bug，修复选择不稳定 | 相同目标输入采用相同成员和距离口径，新选与保持不会交替产生相反排序 |
| F5 + R1 | 合并处理不可达、到达容差和指令结果提示 | 可执行才接受；失败释放任务／锁；零交互距离不会要求浮点精确重合；画面上方显示成功或带原因的失败提示 |
| F6 | 明确 Bug，加入候选扫描 | 巡逻扫描有效候选，缓存 A 不妨碍发现可见 B |
| F7 | 明确 Bug，修复换装重置冷却 | 更新属性不重建同一技能的运行状态；原冷却截止时间保持 |
| R2 + R3 | 合并修复范围、射线、墙体约束；双方远程可跨高低差索敌 | 有范围和遮挡约束；无遮挡且射程内的上下层目标能被发现并命中；墙体不能被发射兜底或飞行中的子弹绕过 |
| R4 | 明确 Bug，修复评分输入与候选 | 使用实际防御、完整有效风险敌人集合、具体成员位置；恢复远处撤离兜底 |
| R5 | 明确 Bug，修复外部位移后的到达缓存 | 被移出交互距离后停止远距离交互并重新接近；不能靠缓存继续搜刮 |

“不需要人工”明确指：**由编码 Agent 构造场景、启动 Unity／Play Mode、读取日志与结构化证据、定位和修改代码、重跑回归。用户无需搭场景、点击 Play、操作角色或替 Agent 取日志。** CLI 是执行工具，不是把调试工作交回用户；也不在 CLI 中另写一个自动修改源码的系统。

本次范围包含修复，不再以“仅生成诊断报告”为完成条件。F3 原先“覆盖手动指令即 Bug”的判断按本次规则纠正，保留编号追踪受击打断链。原审查是历史静态证据，后续由真实运行结果修订。

## 2. 修复前代码调查与职责判断

- `AgentPawnRoot.RecordCombatDamageInterrupt` 直接覆盖指令并零散写事实；`AgentInterventionController` 只是 Blackboard 存取层，不应把完整恢复策略堆进该类。
- `AgentActionNodeBase` 已同时承担目标解析、NavMesh 查询／移动、清理任务；增加失败监控前应抽出导航职责。资源群已有可达成员查询，应复用其候选点计算。
- `AgentTargetCommandDispatcher` 当前返回 bool，先写事实再 Submit；`PlayerInputManager` 对失败直接返回，没有结果 UI。
- `EnemyVisionUtility` 已有水平视角和三维遮挡射线；`PlayerTargetResolver` 只先选最近 Agent，再由敌人检查这一个目标。这不足以扫描“最近不可见、次近可见”的候选。
- `EngageEnemyActionNode` 在 `TryShootEnemy=false` 时直接扣血；如果 false 新增“被墙遮挡”含义，会产生穿墙伤害，必须同时修复。
- `AgentCombatShooter` 压平瞄准高度；`BulletController`／`EnemyBulletController` 主要处理目标命中，需要补齐障碍阻挡与高速飞行检测。
- `AgentTargetDecisionController` 使用默认防御和群中心；Pawn 已公开实际 `Defense`，无需为此新建一套属性系统。
- `AgentCombatController.ApplyConfig` 将属性刷新与技能重建绑定；技能运行状态已经由 `AgentCombatSkillBase` 持有，应延续这一所有权。

已阅读 `Assets/Docs/GameplayAgentFrameworkDesign.md`、`Assets/Docs/EnemySystemOverview.md`、现有审查和相关邻近文件。维持项目已有的 Registry 查询、CommandReceiver 写入、Blackboard 内部事实、Enemy／Inventory／Raid 各自权威的边界。

## 3. 分层、目录与依赖

```text
Assets/Scripts/Gameplay/
├─ Agent/
│  ├─ Commands/      # 新增：指令校验、生命周期、结果；不绘制 UI
│  ├─ Navigation/    # 新增：路径与到达查询、移动状态；不选任务
│  ├─ Targeting/     # 新增：具体成员候选，供 Discovery／Decision 共用
│  ├─ Core/         # 现有：Pawn 组合组件、Brain／Blackboard 承载
│  ├─ AI/Actions/   # 现有：调用导航和战斗，报告任务终态
│  └─ Combat/       # 现有：攻击许可、施法与技能运行状态
├─ Perception/      # 新增：双方共用的纯空间查询，只依赖 Unity 类型
├─ Enemy/           # 现有：敌人候选选择和身份绑定；行为仍归各敌人
└─ Targets/Presentation/ # 新增：消费指令结果，显示顶部提示
Assets/Resources/HUD/Pfb_AgentCommandFeedback.prefab # 新增：提示布局和动画参数
```

依赖方向：

1. Input → Dispatcher → CommandReceiver → 指令生命周期 → 导航／目标查询；接受成功后才同步当前任务及事实。
2. AI Action → 导航／战斗执行 → CommandReceiver 报告完成或失败；不直接删除另一条指令的 Blackboard 值。
3. Discovery／Decision → AgentTargetCandidateCollector → Targets Registry／Authoring、Navigation、Perception。
4. EnemyTargetSelector → AgentRuntimeQuery、EnemyCombatTargetBinding、Perception；各敌人控制器消费绑定结果。
5. Agent／Enemy → Perception；Perception 不依赖 Agent、Enemy、Raid、UI 或其伤害接口。
6. 指令结果发布 → Presentation 订阅。业务不引用 Presenter、Canvas 或具体中文文案；测试可独立订阅结果。

这不是通用 AI 框架重构：只抽出本次多个入口确实共享的指令生命周期、导航查询、候选和空间检测。保留各敌人的专属战斗状态、招式和巡逻实现。

### 3.1 Reuse：具体保留复用的文件

以下路径均相对工程根目录；“复用”表示默认不修改该文件，使用其现有接口。

| 文件 | 复用内容与原因 |
| --- | --- |
| `Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeRegistry.cs` | Agent 注册、存活与身份查询，继续作为权威索引 |
| `Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeQuery.cs` | `CopyAgentsTo` 收集候选；不在敌人中新增场景遍历 |
| `Assets/Scripts/Gameplay/Agent/Core/AgentInterventionController.cs` | 底层指令存取；由生命周期控制器编排，保持其职责窄小 |
| `Assets/Scripts/Gameplay/Enemy/EnemyHealthController.cs` | 复用生命/伤害结算；其中 CombatDamageUtility 的角色解析在 P2 作局部 Extend，删除场景根兄弟对象兜底 |
| `Assets/Scripts/Gameplay/Targets/Authoring/ResourceClusterAuthoring.cs` | 真实资源成员、可站立点及完整路径筛选；查询层增加严格前提检查，避免无 NavMesh 的旧兜底被解释为验证成功 |
| `Assets/Scripts/Gameplay/Targets/Authoring/ExtractionClusterAuthoring.cs` | 撤离群与具体撤离点解析 |
| `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRegistry.cs` | 群注册、完成状态与成员关联 |
| `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs` | 实际装备修正构建、背包开关及会话入口，F7 测试不模拟最终修正结果 |
| `Assets/Scripts/Gameplay/Backpack/EquipmentSlotUI.cs` | 程序调用实际换装入口 |
| `Assets/Scripts/Gameplay/Raid/RuntimeNavMeshSurfaceBuilder.cs` | 已有运行时导航就绪／重建机制；不在导航失败时无限请求重建 |
| `Assets/Scripts/UI/AdaptiveCanvasScaler.cs` | 复用现有 Canvas 尺寸适配 |

### 3.2 Extend / Wrap：具体修改文件

| 选择 | 文件 | 修改职责 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/Agent/Core/AgentPawnRoot.cs` | 组合生命周期与导航组件；转发受击、提交、结束和死亡事件；继续提供实际 Defense，不承载新策略算法 |
| Extend | `Assets/Scripts/Gameplay/Agent/Interfaces/IAgentCommandReceiver.cs` | 增加带结构化结果的提交和按 CommandId 报告终态接口；保留旧入口并转发同一实现 |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentCommandRouter.cs` | 路由结果／终态；统一走接收接口，不能绕过校验 |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentManualDirectiveLock.cs` | 只根据当前有效任务持锁；失败／取消／完成不继续锁；区分手动选择和伤害反击 |
| Extend | `Assets/Scripts/Gameplay/Agent/SO/AgentPawnConfig.cs` | 到达容差、导航就绪／无进展期限及 Agent 感知参数；旧资产缺省值有有效默认值 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainTransitionRules.cs` | 当前指令与状态保持一致；受击打断优先于资源保护；撤离只在恢复后进入 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/AgentActionNodeBase.cs` | 抽出导航实现后只作调用适配；任务清理由明确终态接口完成 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/MoveToTargetActionNode.cs` | 消费结构化移动结果，区分等待、到达和失败 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/SearchResourceActionNode.cs` | 每次交互验证距离，位移后撤销到达缓存；报告完成／失败 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/ExtractActionNode.cs` | 同一容差／任务终态规则，保留 Raid 结算权威 |
| Extend | `Assets/Scripts/Gameplay/Agent/AI/Actions/EngageEnemyActionNode.cs` | 反击结束报告、远程开火条件、移除遮挡失败后的直接扣血；攻击节奏状态迁入战斗控制器 |
| Extend | `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs` | 新选与保持共用成员候选；敌人必须满足感知；自动提交也走一致任务入口 |
| Extend | `Assets/Scripts/Gameplay/Agent/Decision/AgentTargetDecisionController.cs` | 读取 Pawn.Defense，使用共同候选和去重风险敌人，补远处撤离兜底 |
| Reuse | `Assets/Scripts/Gameplay/Agent/Decision/AgentTargetDecisionService.cs` | 实际无需改评分算法；Controller 修正输入和后备候选，继续使用原风险阈值与公式 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatController.cs` | 属性刷新与技能集合变更分离；保留技能状态；持有普通攻击／动作锁时间，不因节点 OnEnter 重置 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatSkillBase.cs` | 保持技能冷却权威；仅在技能集合确需重建时支持受控状态迁移 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/AgentCombatShooter.cs` | 三维瞄准、发射位置遮挡／射程复检；返回可区分拒绝原因的结果 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatSkillUtility.cs` | 面向目标技能及范围伤害复用空间约束，避免只限制普攻却仍由技能穿墙命中 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatSkillContext.cs` | 传递施法空间查询参数，不在通用查询层反查 Pawn |
| Reuse | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatSkillTarget.cs` | 现有三维目标数据足够，实际无需修改；瞄准/遮挡在共用查询和执行层处理 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Runtime/AgentCombatAreaDamageOverTime.cs` | 每次持续伤害按实际作用原点重检遮挡，动态墙不能被初次筛选缓存绕过 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Skills/AgentAreaDamageSkillConfig.cs` | 文件内实际技能执行接入范围／作用原点约束，不改伤害配置 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Skills/AgentAreaDamageOverTimeSkillConfig.cs` | 文件内实际技能执行传递正确三维落点和空间上下文 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Skills/AgentConeDamageSkillConfig.cs` | 文件内扇形执行显式区分形状和遮挡，避免水平角计算抹掉远程瞄准高度 |
| Extend | `Assets/Scripts/Gameplay/Agent/Combat/Skills/AgentWallSkillConfig.cs` | 文件内墙体／冲击执行检查合法施放点和阻挡；不改变既有技能形状 |
| Extend | `Assets/Scripts/Gameplay/Targets/Input/AgentTargetCommandDispatcher.cs` | 创建指令并接收结构化接受／拒绝结果；移除先写事实后提交的顺序；早期失败也形成反馈事件 |
| Extend | `Assets/Scripts/Gameplay/Targets/Input/PlayerInputManager.cs` | 消费新提交结果，保持鼠标输入职责；空白点击／UI 点击不刷失败提示 |
| Wrap | `Assets/Scripts/Gameplay/Enemy/EnemyVisionUtility.cs` | 保留现有调用与 Gizmos 接口，空间判定委托给共用 Perception 查询 |
| Wrap | `Assets/Scripts/Gameplay/Enemy/Player/PlayerTargetResolver.cs` | 保留旧目标／组件解析入口；新敌人选择调用统一候选服务；旧 Player 兜底也必须活着且可受击 |
| Extend | `Assets/Scripts/Gameplay/Enemy/EnemyBehaviorController.cs` | 巡逻扫描可见候选，身份绑定一致 |
| Extend | `Assets/Scripts/Gameplay/Enemy/RangedEnemyBehaviorController.cs` | 死亡重选、候选扫描、三维远程瞄准与开火复检 |
| Extend | `Assets/Scripts/Gameplay/Enemy/AnchorSentinelBehaviorController.cs` | 无效绑定整体失效，重新选人；远程招式验证空间条件 |
| Extend | `Assets/Scripts/Gameplay/Enemy/HunterBossBehaviorController.cs` | Transform／伤害／位移接收器原子替换；锚弹等定向远程招式保留高度差 |
| Extend | `Assets/Scripts/Gameplay/Enemy/ModernStranderBehaviorController.cs` | 巡逻候选扫描和统一感知接入 |
| Extend | `Assets/Scripts/Gameplay/Enemy/TidalAberrationBehaviorController.cs` | 巡逻候选扫描和统一感知接入 |
| Extend | `Assets/Scripts/Gameplay/Enemy/AncientStranderBehaviorController.cs` | 巡逻候选扫描和统一感知接入 |
| Extend | `Assets/Scripts/Gameplay/Enemy/BulletController.cs` | 主角弹体处理墙体和沿飞行段的最先有效碰撞 |
| Extend | `Assets/Scripts/Gameplay/Enemy/EnemyBulletController.cs` | 敌方弹体同样受墙体／高速碰撞约束 |
| Extend | `Assets/Scripts/Gameplay/Enemy/HunterBossAnchorProjectile.cs` | 锚弹不穿墙；接触顺序、命中与位移效果保持一致 |
| Extend | `Assets/Docs/GameplayAgentFrameworkDesign.md` | 回写指令生命周期、导航和观察接口的实际边界 |
| Extend | `Assets/Docs/EnemySystemOverview.md` | 回写候选扫描、身份绑定与感知／攻击关系 |

`AgentCombatSkillUtility.cs` 不能替代逐技能调用点核查，已将四类具体技能及持续伤害执行文件列入清单。若必须扩展当前清单之外的生产文件，在对应阶段先补入文件归属。当前不把所有专属范围招式改写成投射物，也不修改其范围／伤害平衡。

### 3.3 Create：新增文件及独立存在理由

| 文件 | 唯一职责与独立理由 |
| --- | --- |
| `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveResult.cs` | CommandId／AgentId／阶段／原因码／来源的数据结构；不含文案、UI 或行为策略 |
| `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveValidationService.cs` | 接受前验证角色、目标、可执行位置；无副作用，不先改当前任务 |
| `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveLifecycleController.cs` | 当前任务与一个被反击挂起的撤离任务；提交、打断、恢复、终止和事实同步的唯一编排者 |
| `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveFeedbackChannel.cs` | 只发布不可变指令结果；不缓存任务或解释业务，不成为第二个指令管理器 |
| `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationResult.cs` | Moving／Arrived／Pending／失败原因等结构化结果，避免 bool 混淆 |
| `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationQuery.cs` | 可站立点、完整路径与到达容差的共同查询；预检和执行复用同一规则 |
| `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationMotor.cs` | 每 Agent 的移动、路径刷新和无进展状态；由动作节点驱动，不自行决定任务或重复 Tick |
| `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidate.cs` | 群与成员身份、实际位置、导航点和比较距离，作为候选快照 |
| `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs` | 收集具体成员；Discovery／Decision 共享候选定义，策略各自保留 |
| `Assets/Scripts/Gameplay/Perception/TargetVisibilityQuery.cs` | 三维范围、水平视角、射线遮挡；只收 Unity 空间参数 |
| `Assets/Scripts/Gameplay/Perception/TargetVisibilityResult.cs` | 实际为 Visible/Invalid/OutOfRange/OutsideView/Occluded 结果枚举，诊断几何由测试按需记录 |
| `Assets/Scripts/Gameplay/Perception/CombatAimPointResolver.cs` | 实际取首个有效非 Trigger 碰撞体中心，缺失时回退位置加高度；双方共用，不参与目标选择 |
| `Assets/Scripts/Gameplay/Perception/ProjectileSweepQuery.cs` | 检查上一帧至下一位置的飞行段，返回最早有效碰撞；不造成伤害 |
| `Assets/Scripts/Gameplay/Enemy/EnemyCombatTargetBinding.cs` | 同一目标的 Transform／伤害／位移引用及活性检查，防止半更新 |
| `Assets/Scripts/Gameplay/Enemy/EnemyTargetSelector.cs` | 查询已注册候选、过滤有效性与视野并稳定排序；不承载敌人状态机 |
| `Assets/Scripts/Gameplay/Targets/Presentation/AgentCommandFeedbackPresenter.cs` | 订阅手动指令结果，排队与淡入淡出；不验证路径或读取 Blackboard |
| `Assets/Scripts/Gameplay/Targets/Presentation/AgentCommandFeedbackText.cs` | 原因码到用户可读中文的映射；与 UI 动画独立，可直接测文案 |
| `Assets/Scripts/Gameplay/Targets/Presentation/AgentCommandFeedbackInstaller.cs` | 在有正式指令输入的游戏场景安装唯一提示实例，处理切场景订阅；不侵入 Pawn 或测试启动器 |
| `Assets/Resources/HUD/Pfb_AgentCommandFeedback.prefab` | 顶部居中位置、字体、CanvasGroup、动画参数；正式游戏可加载，不能只存在于测试夹具 |

抽取使用“新文件承接职责，原文件改为调用”的迁移方式，保留原文件 GUID；默认不移动或删除旧脚本，不加业务 `.asmdef`。新增脚本和预制体配套 `.meta`。除新增提示预制体外，不计划修改正式场景；若现有关卡墙体层／Collider 配置无法表达阻挡，在阶段结果中列出具体资产差异后纳入修复，不以清空遮挡 Mask 获得通过。

## 4. 指令、打断与反馈链

### 4.1 提交必须有明确结果

实际接口按同一职责收敛为：

- `TrySubmitDirective(request) → AgentDirectiveResult`：验证后接受；失败保持原有效指令与事实。
- `FinishDirective(commandId, failure = None) → bool`：统一完成/失败，仅操作 ID 匹配的当前任务，旧回调不能清掉新任务。
- Pawn 的实际伤害入口调用生命周期 `Submit(request, damageInterrupt: true)`，保存一份挂起撤离并同步任务事实。
- `ClearDirective()` 转发生命周期 `Cancel()`，清理活动及挂起任务；死亡同样取消。

旧的 void Submit／Clear 接口保留兼容适配，但所有本次涉及的正式调用点迁入新接口。`ClearPendingDirective` 不再绕过生命周期直接改 Blackboard。

任务事实与瞬时感知事实分开：Extract 是当前任务时才有活动的撤离意图；挂起撤离只保存在生命周期状态中。收到伤害表示知道攻击来源，不等于该来源当前无遮挡；不得继续无条件把 `HasVisibleEnemy` 写成 true。状态选择同时核对活动指令，不能让两个互斥任务条件同时长期为真。

### 4.2 F1 的恢复规则

1. Extract 中有效受击：保存原请求／目标／CommandId，活动任务变为反击 Engage。
2. 反击中再次受击：**2026-09-12 用户确认改为保持当前有效目标**，不替换反击指令、不重置追击。目标结束后恢复原撤离，不为其他伤害来源建立队列。旧“可更新反击目标”规则已由 [P3h](../2026-09-11-scenezl-final1-autonomous-raid/p3h_retaliation_progress.md) 的确认和实测替代；唯一撤离记录、手动覆盖和有限追踪规则保持。
3. 当前反击目标死亡、销毁或失效：结束反击，重新验证原撤离目标与路径，通过后恢复同一撤离请求。
4. 失去视线但目标仍活着：在有限追踪窗口内按合法路径处理，不穿墙开火；超出窗口或明确不可达后结束反击，再恢复撤离。
5. 新的人工指令覆盖、显式取消、角色死亡：撤销挂起记录；新命令不能被旧反击完成回调覆盖。
6. 恢复时撤离目标已不可用：清理原任务，返回明确失败原因，不无限挂起。

实际工程默认值：丢失视线追踪宽限 2 秒、连续无导航进展 3 秒、导航初始就绪等待 2 秒、到达数值容差 0.1m。导航参数经 PawnConfig 配置，丢失视线宽限在 Engage 节点中；这些是工程选定值，不是用户指定数值。暂停、准备导航与正常交互等待不计入“应移动但无进展”。

F3 只明确“可受击中断”。本轮保持反击结束后进入正常自主选择，不额外承诺恢复原资源任务；只有 F1 撤离具备用户明确要求的恢复语义。护盾完全吸收、无效伤害来源与死亡命中分别测试，不扩大成“所有接触均反击”。

### 4.3 F5／R1 的导航与提示

- 接受前：资源／撤离必须有可执行交互点和完整合法路径；导航未就绪属于明确可诊断的状态，不能当作直线穿越成功。
- 远程 Engage 例外：若当前位置已满足三维射程与无遮挡攻击条件，无需能走到目标脚下；若必须靠近，验证的是合法攻击位置，而非一律要求站到敌人原点。
- 执行中：区分计算中、正常移动、已到达、目标失效、路径不可达、持续无进展。明确失败释放任务和锁；Collector 组合 AgentTargetFailureMemory，短期排除同 Agent 的失败目标。3 秒游戏时间到期后重检路径，Agent 或目的地移动超过 0.5m 可提前解除；不做全局永久黑名单，手动选择不受该缓存阻挡。
- R1：在实际可交互点上应用统一到达容差，同时核查高度和完整路径；不能只按 XZ 距离让楼上楼下隔板交互。资源 0 配置可用；不是无差别加大交互范围或强行完成任务。
- R5：每次继续交互前检查当前距离与目标有效性；离开后撤销到达状态和本节点的背包开关观察记录，再重新靠近，不能把一次关闭动作误记为搜完。只有确认属于同一 Agent／资源的 UI 会话才通过现有 API 关闭，不干扰其他 Agent 的背包。原资源成员仍有效时保留任务身份。

顶部提示：

- 接受手动指令后：`指令下达成功`。
- 接受失败或后续执行失败：`指令下达失败：目标不可达` 等具体原因；机器结果同时保存失败阶段。
- 原因至少涵盖未选中有效角色、角色已死亡、目标失效／已完成、无可用导航、路径不可达、持续无法靠近。
- 成功表示已接受并开始执行，不等于已完成；后续动态堵路仍能出现失败提示。
- 一条 CommandId 的同一终态只提示一次；自动发现不刷屏。正常受击打断是状态转移，不显示错误的“下达失败”。
- Canvas 顶部居中、淡入 0.15s／停留 1.5s／淡出 0.25s（建议默认），使用 unscaled time；不遮挡点击，中文字体可读。重复事件去重、连续不同命令有界排队。

## 5. F2／F6 与 R2／R3 的联合修复

### 5.1 候选扫描与身份绑定

巡逻按既有感知节奏扫描 Registry 的存活候选，逐个检查距离、角度、遮挡，再按距离和稳定身份排序。不得“先拿最近 A，A 看不见就结束”。Combat 中有效目标保持，避免无条件每帧切换；失效后重新扫描。

`EnemyCombatTargetBinding` 在候选身份变化或接收器死亡／失效时整体清空／替换。用伤害接收器的根确认 Transform 身份，同时刷新 Boss 位移接口；不能保留 A 的接收器配上 B 的 Transform。没有候选时进入敌人既有无目标流程，不攻击尸体。

### 5.2 空间条件与高度差

- 自动敌人发现：存活 → 三维距离范围 → 水平视角（Agent 默认 360°、敌人保留各自角度）→ 碰撞体采样点射线无遮挡。
- 不额外加入“必须同高度”或“必须同一 NavMesh 岛”的远程索敌条件。高度进入真实距离和瞄准向量；上下层无墙且射程内可交战，楼板属于有效遮挡。
- 目标采样取真实有效碰撞体中心；没有碰撞体时采用固定高度回退。排除自身/目标身体和 Trigger，射线及弹道仍检查实体墙；不通过 `mask=0` 规避问题。多采样点暴露度判定未作为本轮必须算法扩展。
- 开火前从实际发射点复检；视觉起点能看见但枪口被近墙挡住时不能造成伤害。
- 发射后沿真实三维方向运动，取消 Y 压平；以飞行段扫掠补充碰撞回调，按最早有效撞击处理墙和目标，避免薄墙／高速穿透及同帧重复伤害。
- 发现时无遮挡、发射前／弹体飞行中新增墙体也是回归用例，不能仅在选目标时测一次射线。
- `TryShootAt` 的失败必须区分遮挡／超射程／目标无效／组件缺失；这些失败不能触发直接扣血。正式远程发射配置缺失输出可定位错误，不以瞬间伤害掩盖资产问题。
- 面向目标的技能和范围伤害也需要明确空间作用路径：直接命中要求合法范围和无遮挡；范围技能按其实际作用原点到受影响目标做障碍校验。保留既有招式形状，不把跨高度远程要求错误套成近战无限竖直攻击。

已知资源和撤离点属于任务目标，不因“敌人视野半径”而消失；仍受导航／交互合法性约束。这保证 R4 的远处撤离兜底不会被感知修复再次截断。

## 6. F4／R4 与 F7 的修复边界

F4／R4：共享候选保存具体成员身份、实际位置、可执行导航点和比较距离；新选／保持／Decision 不混用群中心与成员距离。策略可以有各自评分，但输入事实必须相同。风险收集遍历所有符合感知规则的存活敌人成员并按实体去重，不以“每群最近一个”代替整群。防御从 Pawn 实际值读取；没有正常任务时，从有效可达撤离点中选兜底，不受敌人发现半径裁掉。默认发现与 Decision 分别验证，保持互斥启用。

F7：同一技能集合仅更新属性／图腾修正时不清空重建；技能和普通攻击的运行时冷却由战斗层持有。集合变化时，按稳定 SkillId 和运行类型保留仍存在技能的状态；同一列表重复 SkillId 仅保留首次定义，避免第二份冷却。新增技能正常初始化、删除技能移出集合，不把冷却放入 SO 或静态全局字典；节点重入不重置攻击时间。

## 7. 自动测试增量与代理执行闭环

主规划原有测试文件继续使用；新增具体文件：

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Editor/AgentReproduction/Tests/CommandFeedbackTests.cs` | 接受／拒绝／动态失败、原因文本、事件去重、取消后旧回调、顶部位置和淡入淡出 |
| `Assets/Scripts/Editor/AgentReproduction/Tests/DirectiveLifecycleTests.cs` | 撤离挂起恢复、新手动命令覆盖、死亡取消、多次受击不叠栈和延迟完成保护 |
| `Assets/Scripts/Editor/AgentReproduction/Tests/PerceptionCandidateTests.cs` | 共享范围/射线、Trigger/墙、完整候选与范围技能；高差和弹道见 RangedSpatialTests |

UI 自动验收分两层：普通 Play Mode 断言结果事件、文字、RectTransform 顶部锚点、CanvasGroup alpha 和 unscaled 动画；图形模式单独自动启动、输出淡入／显示／淡出截图，由编码 Agent 检查截图。用户无需点 Play 或判断截图，`-nographics` 不冒充真实 UI 渲染验证。

每个修复阶段必须由编码 Agent 执行：

1. 构造最小样例，跑修复前基线并保存实际失败轨迹。
2. 排除准备失败，依据真实状态／路径／遮挡物／冷却时间定位代码；若原推断错误，先纠正文档和样例。
3. 修改本阶段已评审的生产文件，检查职责边界；测试算法不复制业务实现。
4. 重跑同一个 Case 和对照，确认对应行为恢复；再跑邻近受影响组。
5. 阶段完成后跑跨组回归和重复性检查，记录源提交／diff／配置／证据。
6. 回写主规划阶段结果、原审查和架构审查。最终交付修复代码与实际运行证据，不只交脚本。

规则均按用户本次确认进入回归；不能继续把 F3、R2／R3、R4、R5 标为“需人工确认所以不判失败”。仅新发现、未在范围内的额外设计问题单独列项，不阻塞已授权修复。

## 8. 实施顺序与架构 Review 要点

按主规划 P0–P5 执行：最小自动运行闭环 → 指令／导航／提示 → 感知／敌人绑定／三维攻击 → 候选／评分／冷却 → 综合回归及图形证据 → 文档和架构审查。每阶段都包含“复现 → 修复 → 验证”，不先建完所有基础设施再开始定位业务问题。

本次已获用户确认并实施的架构是：

1. `Agent/Commands` 统一当前任务、撤离挂起恢复和结果；新增 CommandReceiver 结果／终态接口。
2. `Agent/Navigation` 从 Action 基类抽出查询和执行，接受预检与实际执行共用规则。
3. `Gameplay/Perception` 供双方共用；候选选择分别属于 Agent Targeting 和 Enemy，保持依赖方向。
4. `Targets/Presentation` 单向订阅结果，正式提示预制体独立于测试。

用户在阅读方案后明确回复“ok”，授权自行按小规划、实现、测试/review、调整、提交推进全部阶段。因此正常定位与边界内增补由 Agent 持续完成；各次文件判断、失败与修正记录在阶段文档中，最终审查见 [architecture_review.md](architecture_review.md)。

### 实际实现中的文件增补

| 具体文件 | 归属及原因 |
| --- | --- |
| `Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainStateFactory.cs` | Combat 树直接执行 Engage，避免先走到敌人脚下阻断高差原地攻击 |
| `Assets/Scripts/Gameplay/Enemy/Player/PlayerTargetResolver.cs` | 与 CombatDamageUtility 一致，只沿命中对象所属角色解析，删除场景根兄弟对象回退 |
| `Assets/Scripts/Gameplay/Targets/Authoring/ActiveEnemyClusterAuthoring.cs` | CopyAliveEnemiesTo 对外复制初始和动态存活成员，封装内部集合 |
| `Assets/Art/VFX/RobotAnchorBeamVfx.cs`、`MudTidalAberrationVfx.cs`、`TracerAnchorVortexVfx.cs`、`PlayerElementalSkillVfx.cs` | 正式控制器集成测试暴露粒子 duration 配置时序 Assert；在既有粒子创建责任点先 Stop，再配置 |
| `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetFailureMemory.cs` | P4 Review 追加，短期失败缓存独立于候选扫描算法；依既有结果事件更新，Commands 不反向引用 |

具体测试文件、合并的草案文件、实际接口及证据格式见 [主规划第 8–9 节](task_plan.md#8-实际目录结构与文件职责)。

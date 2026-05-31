# Enemy System Overview

## 模块定位

敌人系统现在主要服务 Raid MVP：负责敌人出生、巡逻、发现玩家、追击、攻击、死亡掉落，并把敌人相关目标同步给 Agent。它已经不是一个很轻的白模敌人脚本集合，而是由生成、目标群、生命、巡逻、视觉感知、怀疑调查、战斗行为和调试可视化组成的一套中等偏复杂系统。

当前主链路可以理解为：场景或出生点产生敌人，敌人被注册到活跃敌人群，Agent 发现活跃敌人群后进入接战；如果没有活跃敌人，但还有敌人来源群，Agent 会把来源点当作侦查目标。敌人自身则按 Patrol / Chase / Attack 这类状态运行，必要时通过视野、声音刺激或调查状态打断普通巡逻。

## 目录与资源位置

敌人运行时代码主要在 `Assets/Scripts/Gameplay/Enemy`，配置类在 `Assets/Scripts/Gameplay/Enemy/Config`。敌人目标群相关代码在 `Assets/Scripts/Gameplay/Targets/Authoring` 和 `Assets/Scripts/Gameplay/Targets/Runtime`。Agent 寻敌相关逻辑主要在 `AgentTargetDiscoveryController` 和 `EngageEnemyActionNode`。敌人 prefab 主要放在 `Assets/Prefabs/EnemyPrefab`，敌人 ScriptableObject 配置资源应放在 `Assets/SO` 下。

## 生成与目标群

`EnemySpawnPoint` 是出生点入口。它在 Play Mode 下可以通过 `_spawnOnStart` 自动生成敌人，也可以通过 `Spawn All` 手动生成。每条 `EnemySpawnEntry` 描述一种敌人 prefab、生成数量、位置/旋转偏移、随机半径、NavMesh 采样半径，以及可选的巡逻路线。敌人生成后会自动补或获取 `EnemyPatrolRouteFollower`，并把配置好的路线分配进去。

生成出来的敌人不会直接归出生点管理，而是通过 `GameplayTargetRegistry.TryRegisterSpawnedEnemy` 找到包含该出生点的 `EnemySourceClusterAuthoring`。来源群再把敌人转交给绑定的 `ActiveEnemyClusterAuthoring`。这套拆分的意义是：出生点表达“敌人可能来自哪里”，活跃敌人群表达“现在场上有哪些敌人”。场景里预先放好的敌人，则可以手动放进活跃敌人群的 `Manual Enemy Members`。

Agent 当前的目标发现优先级是活跃敌人群、敌人来源群、资源群、撤离点群。有存活敌人时，Agent 会优先接战；没有存活敌人但来源群未完成时，Agent 会去侦查来源点；两者都没有时才继续找资源或撤离点。

## 生命、死亡与掉落

`EnemyHealthController` 负责敌人的生命、受伤、护盾、死亡和死亡掉落。敌人配置会通过 `EnemyHealthConfigBase` 或其子类提供最大生命值和死亡掉落配置。敌人死亡时会通知 `GameplayTargetRegistry`，让所属 `ActiveEnemyClusterAuthoring` 标记该敌人成员完成，同时通知 `RaidFlowController` 记录击杀，并按配置生成死亡掉落容器。

这里有一个需要注意的生命周期点：目标系统可能早于敌人的 `Start` 查询敌人状态，所以敌人生命初始化不能只依赖 `Start`。当前 `EnemyHealthController` 已经提供 `IsAlive`，并在被查询时保证生命值完成初始化，避免活跃敌人群在场景启动早期把敌人误判成已死亡。

## 巡逻模块

巡逻系统支持两种模式。`RandomRadius` 是简单随机巡逻，敌人在初始位置附近找 NavMesh 点移动，适合 MVP 默认敌人。`FixedRoute` 使用 `EnemyPatrolRoute` 和 `EnemyPatrolRouteFollower`，可以配置路线点、Loop 或 PingPong、起点策略、等待时间覆盖、等待时看向的目标，以及等待扫描角覆盖。

`EnemyPatrolRoute` 更偏场景配置，保存路线点并负责 NavMesh 采样和 Gizmo 预览。`EnemyPatrolRouteFollower` 更偏运行时状态，保存当前路线索引、移动方向和当前等待参数。`EnemySpawnPoint` 生成敌人时会把 `EnemySpawnEntry` 中的路线交给 follower。整体上，巡逻模块功能已经比较完整，复杂度中等偏上，但职责还算集中。

## 视觉与怀疑感知

视觉判定的核心是 `EnemyVisionUtility`，它只做距离、水平角度和视线遮挡判断。敌人行为脚本通过 `IEnemyVisionSource` 暴露视野 Transform、玩家 Transform、检测距离、视角、遮挡层、眼睛高度和目标高度。`EnemyVisionVisualizer` 会根据这些数据绘制视野 Mesh，`EnemyVisionDisplayController` 负责运行时开关显示，`EnemyVisionRuntimeInstaller` 和 `EnemyVisionBootstrapper` 则负责自动创建或配置可视化组件。

怀疑感知是更复杂的扩展链路。`EnemySuspicionStimulusBus` 是全局刺激事件入口，枪声、脚步、弹着点、搜刮、敌人受伤、玩家最后出现位置等都可以变成刺激。`EnemySuspicionSensor` 挂在敌人身上，按范围、强度、遮挡、冷却和误差半径筛选刺激，并保存当前最强记录。`EnemyPatrolAwarenessController` 会消费这些记录，让敌人在 Patrol 之外进入 Suspicious、Investigate 或 Search 状态。

这套怀疑/调查系统已经接近潜入类 AI 的雏形。它不是当前最小敌人主链路的必需品，更适合先作为可选能力保留。默认敌人如果只是为了支撑战斗、资源和撤离验证，用视觉发现加简单巡逻就足够了。

## 敌人行为脚本

普通敌人行为脚本大多采用 Patrol / Chase / Attack 的结构。`EnemyBehaviorController` 是基础近战敌人，`RangedEnemyBehaviorController` 是远程敌人，`ModernStranderBehaviorController` 有触手吸附和腐蚀液，`AncientStranderBehaviorController` 有专属近战命中逻辑，`TidalAberrationBehaviorController` 同时有近战电击和远程水流。`AnchorSentinelBehaviorController` 和 `HunterBossBehaviorController` 更像独立特殊敌人或 Boss，内部状态和技能逻辑单独维护。

这些普通敌人脚本的攻击差异已经比较明显，但巡逻/感知骨架重复很多：应用配置、找玩家、初始化 NavMeshAgent、初始化路线、巡逻中看玩家、追击、攻击、丢失目标后回巡逻。后续如果继续新增敌人，最好不要继续复制这一整套骨架，而是把共用状态和巡逻/感知逻辑抽成基类或组合组件，让具体敌人只实现自己的攻击策略。

## 关键类概览

生成和目标相关的关键类是 `EnemySpawnPoint`、`EnemySpawnEntry`、`EnemySourceClusterAuthoring`、`ActiveEnemyClusterAuthoring` 和 `GameplayTargetRegistry`。它们决定敌人从哪里来、当前归属哪个群、Agent 应该把哪个敌人群当目标。

配置相关的关键类是 `EnemyConfigBase`、`EnemyHealthConfigBase`、`EnemyPatrolConfigBase`、`EnemyPatrolSettings` 和 `EnemyDetectionSettings`。普通敌人的可调参数应尽量通过这些 ScriptableObject 配置，而不是散落在场景实例上。

巡逻和感知相关的关键类是 `EnemyPatrolRoute`、`EnemyPatrolRouteFollower`、`EnemyLookController`、`EnemyVisionUtility`、`EnemySuspicionSensor` 和 `EnemyPatrolAwarenessController`。其中视觉和巡逻可以作为主链路保留，怀疑/调查建议按玩法需要再启用。

生命和战斗相关的关键类是 `EnemyHealthController`、`EnemyStatusEffectController`、`EnemySkillDamageLogger`、`EnemyBulletController` 和各敌人 `BehaviorController`。其中 `EnemyHealthController` 是死亡、掉落和目标群完成状态的关键连接点。

## 当前复杂度判断

当前敌人系统是中高复杂度。它的复杂不是单点算法复杂，而是模块数量和状态链路多：出生点、目标群、敌人生命、死亡掉落、巡逻路线、视觉判定、运行时视野、声音刺激、怀疑调查、多敌人攻击脚本和 Agent 目标发现都已经接在一起。这个规模对 MVP 来说偏重，但还没到不可维护的程度。

主要风险在三个地方。第一，运行时自动安装器会隐藏 prefab 依赖，排查“组件为什么存在/不存在”时不直观。第二，多个敌人行为脚本重复维护同一套巡逻/感知骨架，后续新增敌人会放大维护成本。第三，目标群和生命状态依赖 Unity 生命周期，启动顺序一旦处理不好，就可能出现敌人被误判完成这类问题。

推荐后续方向是先稳住默认主链路：生成、活跃敌人群、生命死亡、简单巡逻、视觉发现、追击攻击。怀疑/调查系统可以先保留但不继续扩张。等玩法真的需要“听到枪声去调查”“丢失玩家后搜索区域”时，再把这部分变成明确可配置的能力，而不是所有敌人的默认负担。

## 推荐配置流程

场景中已经存在的敌人，放进 `ActiveEnemyClusterAuthoring` 的手动成员列表即可。确认敌人 prefab 上有 `EnemyHealthController`、对应行为脚本和必要的 NavMeshAgent。需要固定路线时，在场景中配置 `EnemyPatrolRoute`，并让敌人或出生配置引用它。

通过出生点生成敌人时，在 `EnemySpawnPoint` 上配置 `EnemySpawnEntry`，再用 `EnemySourceClusterAuthoring` 保存这些出生点，并绑定一个 `ActiveEnemyClusterAuthoring`。运行时生成的敌人会自动注册到活跃敌人群。验证 Agent 行为时，看 `AgentRuntimeDebugView`：有活跃敌人时目标应为 Enemy；没有活跃敌人但有来源群时目标应为 EnemySource；两者都没有时才会转向资源或撤离。

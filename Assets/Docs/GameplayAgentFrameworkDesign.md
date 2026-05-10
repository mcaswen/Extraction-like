# Gameplay Agent 框架设计文档

## 0. 阅读导航

1. 1 - 3 部分：主要信息、模块定位、总体架构
2. 4 - 7 部分：Agent 运行时、实体、指令目标、Brain 状态机与行为树
3. 8 - 9 部分：其他系统与 Agent 的交互边界、MVP 关键链路
4. 10 - 12 部分：已知风险点、扩展点与关键链路总结

## 1. 主要信息

### 1.1 模块定位

Gameplay Agent 模块用于：

1. 在运行时表示一个或多个可被 AI / 系统驱动的 Agent 实体
2. 为其他 Gameplay 系统提供统一的 Agent 查询、只读访问与命令入口
3. 承接敌人、战利品/背包、撤离点、全局胜负状态等系统发来的事实与目标
4. 通过状态机 + 行为树驱动 Agent 自主决策，并把具体动作落到现有 Gameplay 系统

当前阶段桌游系统不再作为主要集成对象。Agent 模块主要服务 Raid MVP：

1. Agent 能识别并击杀一个敌人
2. Agent 能前往并搜索一个箱子
3. Agent 能在满足条件后前往撤离点
4. 全局状态能根据击杀、拾取、撤离结果正确刷新胜负

### 1.2 设计模式

1. 核心架构：运行时注册表 + 命令路由 + 分层状态机 + 行为树
2. 架构关键词：
   1. Runtime Registry：Agent 实例的运行时权威索引
   2. Command Router：外部系统影响 Agent 的统一入口
   3. ReadOnly Interface：外部系统读取 Agent 状态的边界
   4. Command Receiver：外部系统写入 Agent 事实/指令的边界
   5. Blackboard：Agent Brain 内部事实与指令缓存
   6. State Machine：Agent 当前宏观决策阶段
   7. Behavior Tree：宏状态内的具体执行逻辑

### 1.3 关键设计原则

1. Agent 查询不散落：
   - 外部系统查 Agent 应通过 AgentRuntimeRegistry / AgentRuntimeQuery
   - 不应该到处 FindObjectOfType<AgentPawnRoot>
   - 不应该依赖某个单例 Player 作为唯一主角
2. Agent 写入有统一入口：
   - 外部系统对 Agent 下命令应优先走 AgentCommandRouter
   - 外部系统不直接写 AgentBrainController 或 Blackboard
   - AgentPawnRoot 是最终承接命令的实体入口，但不应该被外部随意持有内部字段
3. 读写分离：
   - 读取 Agent 状态走 IAgentReadOnly
   - 影响 Agent 行为走 IAgentCommandReceiver / AgentCommandRouter
   - Blackboard 属于 Agent 内部事实层，不作为跨系统公共 API
4. 目标表达统一：
   - “做什么”由 AgentDirectiveType 表示
   - “对谁/哪里做”由 AgentTargetRef 表示
   - 具体场景对象与未来抽象资源点/敌人点都使用同一套目标结构
5. 其他 Gameplay 系统保持各自权威：
   - 敌人生命、死亡与死亡掉落仍由 EnemyHealthController 负责
   - 背包、战利品存取仍由 Backpack/Inventory 系统负责
   - 胜负、敌人击杀数、撤离结果仍由 RaidFlowController 负责
   - Agent 只发起动作，不越权改写其他系统内部状态

### 1.4 模块位置

1. Agent 代码目录：
   - Assets/Scripts/Gameplay/Agent
2. Agent 配置脚本目录：
   - Assets/Scripts/Gameplay/Agent/SO
3. Agent 设计文档目录：
   - Assets/Docs
4. 当前主要相关系统：
   - Assets/Scripts/Gameplay/Enemy
   - Assets/Scripts/Gameplay/Backpack
   - Assets/Scripts/Gameplay/Raid

## 2. 背景与需求

### 2.1 背景问题

现有项目里同时存在几类“主角/玩家/Agent”相关需求：

1. 早期 Enemy/Player 脚本仍以单体 Player 为目标
2. 新 Agent 框架需要支持多 Agent，不应继续绑定单例 Player
3. 敌人、战利品、撤离点、全局状态都需要知道“谁是当前 Agent”
4. 行为树和状态机已经搭起来，但宏状态内还缺少真实执行节点
5. MVP 不需要完整抽象生态，但需要清晰边界，避免之后接多 Agent 时重构爆炸

### 2.2 核心需求映射

1. 外部系统查 Agent：
   - AgentRuntimeRegistry
   - AgentRuntimeQuery
   - AgentRuntimeHandle
   - IAgentReadOnly
2. 外部系统影响 Agent：
   - AgentCommandRouter
   - IAgentCommandReceiver
   - DamageRequest
   - AgentDirectiveRequest
3. Agent 接收具体目标：
   - AgentDirectiveType
   - AgentTargetRef
   - AgentTargetKind
   - AgentTargetBindingType
4. Agent 自主决策：
   - AgentBrainController
   - AgentBrainStateMachineFactory
   - AgentBrainTransitionRules
   - AgentBrainState
5. MVP 执行动作：
   - 战斗执行节点
   - 搜索资源执行节点
   - 交互战利品执行节点
   - 撤离执行节点

## 3. 总体架构概览

### 3.1 运行时身份层

运行时身份层解决“场景里有哪些 Agent，以及某个系统要找哪个 Agent”的问题。

1. AgentId：每个 Agent 的稳定运行时标识
2. AgentRuntimeRegistry：注册、注销、按 AgentId 查询 Agent
3. AgentRuntimeQuery：对外查询封装，包括主 Agent、最近 Agent、全部 Agent
4. AgentRuntimeHandle：对外暴露只读接口和命令接口的句柄

### 3.2 外部交互层

外部交互层解决“别的系统怎么影响 Agent”的问题。

1. AgentCommandRouter 是推荐入口
2. IAgentCommandReceiver 是 Agent 实体承接命令的接口
3. AgentPawnRoot 实现 IAgentReadOnly 与 IAgentCommandReceiver
4. AgentInterventionController 将外部指令写入 Blackboard

外部系统推荐调用链：

```csharp
AgentCommandRouter.GetOrCreate()
    .TrySubmitDirective(agentId, directiveRequest);
```

不推荐调用链：

```csharp
FindObjectOfType<AgentPawnRoot>().Blackboard.SetValue(...);
```

### 3.3 Agent 内部决策层

Agent 内部决策层解决“Agent 收到事实/目标后怎么决定当前行为”的问题。

1. AgentBrainController 持有 Blackboard、行为树上下文、状态机上下文
2. AgentBrainStateMachineFactory 组装宏状态层级与转移规则
3. AgentBrainTransitionRules 根据黑板事实判断状态转移
4. AgentBrainState 在进入状态时同步当前宏状态
5. 行为树执行节点负责把宏状态转为具体动作

### 3.4 其他 Gameplay 系统层

其他 Gameplay 系统不归 Agent 管理，但会与 Agent 发生交互。

1. Enemy 系统：
   - 提供敌人生命与死亡结算
   - 可通过 AgentRuntimeQuery 找目标
   - 可通过 AgentCommandRouter 对 Agent 造成伤害或设置感知事实
2. Backpack/Loot 系统：
   - 提供箱子、地面掉落、背包存取
   - Agent 搜索/拾取时应通过公开接口或适配层操作
3. Raid 系统：
   - 持有胜负、击杀数、战利品数、撤离进度
   - Agent 不直接伪造胜负，只通过合法击杀、拾取、进入撤离区域触发刷新
4. UI 系统：
   - 可以读取 Agent 状态用于显示
   - 不应直接修改 Agent 内部 Blackboard

## 4. 运行时身份与查询

### 4.1 AgentId

用途：

1. 表示一个 Agent 的运行时身份
2. 避免继续使用 GameObject 名称、Player 单例或场景查找作为寻址依据
3. 支持多 Agent 场景下按 ID 路由指令

注意点：

1. AgentId 不能为空
2. 同一场景中 AgentId 必须唯一
3. AgentPawnRoot 支持在注册前 TryAssignAgentId
4. 注册后不允许直接改 ID，避免 Registry 悬挂索引

来源：

- Assets/Scripts/Gameplay/Agent/Runtime/AgentId.cs

### 4.2 AgentRuntimeRegistry

用途：

1. 运行时 Agent 注册表
2. 维护 AgentId 到 AgentRuntimeHandle 的映射
3. 提供主 Agent、按 ID 查询、只读接口查询、命令接口查询

注意点：

1. AgentPawnRoot 在 OnEnable 注册，OnDisable 注销
2. 若出现重复 AgentId，Registry 会拒绝后注册的 Agent
3. GetOrCreate 当前会自动创建 `[AgentRuntimeRegistry]`
4. 后续正式场景建议将 Registry 放在 Managers 或 Bootstrap 节点下，避免运行时隐式创建对象不易排查

来源：

- Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeRegistry.cs

### 4.3 AgentRuntimeQuery

用途：

1. 作为外部系统查询 Agent 的轻量入口
2. 支持按 AgentId 查找
3. 支持获取 Primary Agent
4. 支持按世界坐标查最近 Agent

推荐使用场景：

1. 敌人选择最近 Agent 作为仇恨目标
2. UI 面板显示当前主 Agent 状态
3. 地图、任务系统查询某个 Agent 位置

来源：

- Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeQuery.cs

### 4.4 AgentRuntimeHandle

用途：

1. Registry 对外暴露的安全句柄
2. 将 PawnRoot、IAgentReadOnly、IAgentCommandReceiver 打包给外部系统
3. 外部系统可以拿 ReadOnly 读状态，也可以拿 CommandReceiver 发送命令

注意点：

1. 外部系统应优先依赖 ReadOnly / CommandReceiver 接口，而不是 PawnRoot 实例
2. PawnRoot 暂时暴露在 Handle 中是为了调试与过渡
3. 长期应减少外部系统对 PawnRoot 具体类的依赖

来源：

- Assets/Scripts/Gameplay/Agent/Runtime/AgentRuntimeHandle.cs

## 5. Agent 实体与内部 Brain

### 5.1 AgentPawnRoot

用途：

1. Agent 实体总入口
2. 承载身体层事实，如位置、朝向、生命值、死亡状态
3. 创建并驱动 AgentBrainController
4. 创建 AgentInterventionController
5. 实现 IAgentReadOnly 与 IAgentCommandReceiver
6. 将身体事实同步到 Blackboard

注意点：

1. AgentPawnRoot 是 Agent 自身权威入口，但不是跨系统随意写字段的入口
2. 外部系统应通过 AgentCommandRouter 间接调用它
3. Update 中只负责同步身体事实并 Tick Brain
4. 具体战斗、搜索、撤离动作不应继续堆进 PawnRoot

来源：

- Assets/Scripts/Gameplay/Agent/Core/AgentPawnRoot.cs

### 5.2 AgentBrainController

用途：

1. 持有 Agent 的 Blackboard
2. 持有 BehaviorTreeContext 与 StateMachineContext
3. 装配并驱动分层状态机
4. 提供 SetFact 作为内部事实写入入口

注意点：

1. SetFact 目前是 public，但它应该被视为 Agent 内部接口
2. 外部系统不要直接拿 BrainController 写事实
3. 外部事实应经 IAgentCommandReceiver / AgentCommandRouter 进入

来源：

- Assets/Scripts/Gameplay/Agent/Core/AgentBrainController.cs

### 5.3 AgentInterventionController

用途：

1. 接收 AgentDirectiveRequest
2. 将待处理指令缓存到 Blackboard
3. 维护 HasPendingDirective 与 PendingDirectiveRequest

注意点：

1. 它不解释业务含义
2. 它只负责把外部意图变成 Brain 可读事实
3. 真正执行由行为树节点完成

来源：

- Assets/Scripts/Gameplay/Agent/Core/AgentInterventionController.cs

### 5.4 AgentPawnConfig

用途：

1. Agent 身体层静态配置
2. 当前包含最大生命值、低血量恢复阈值等基础参数

注意点：

1. ScriptableObject 只承载静态配置
2. 运行时生命值在 AgentPawnRoot 中维护，不写回 SO

来源：

- Assets/Scripts/Gameplay/Agent/SO/AgentPawnConfig.cs

## 6. 指令与目标模型

### 6.1 AgentDirectiveType

用途：

1. 表示 Agent 要执行的意图类型
2. 当前核心类型：
   - Search：搜索资源
   - Engage：攻击敌人
   - MoveTo：移动到位置
   - RequestBuff：请求增益或辅助
   - Extract：撤离

注意点：

1. DirectiveType 只描述“做什么”
2. 具体目标由 AgentTargetRef 描述
3. 旧枚举 ResourceTarget / EnemyTarget / AreaTarget / BuffRequest 已作为兼容别名保留

来源：

- Assets/Scripts/Gameplay/Agent/Data/AgentDirectiveType.cs

### 6.2 AgentTargetRef

用途：

1. 表示 Agent 指令目标
2. 支持当前 MVP 中的具体 GameObject
3. 支持未来抽象资源点、敌人点、区域点

目标字段：

1. Kind：目标语义，如 Resource / Enemy / Location / Extraction
2. BindingType：目标绑定方式，如 ConcreteObject / AbstractPoint
3. TargetObject：具体场景对象
4. TargetPosition：目标位置
5. TargetId：抽象目标 ID 或调试 ID

注意点：

1. 具体敌人、具体箱子优先使用 ConcreteObject
2. 未来刷怪点、资源点、撤离区域可使用 AbstractPoint
3. 行为树节点应先判断 TargetRef.IsValid，再解析具体组件

来源：

- Assets/Scripts/Gameplay/Agent/Data/AgentTargetRef.cs

### 6.3 AgentDirectiveRequest

用途：

1. 外部系统提交给 Agent 的完整指令数据
2. 包含目标 Agent、指令类型、目标引用、命令 ID、优先级
3. 提供 MVP 便捷构造方法：
   - SearchConcreteResource
   - EngageConcreteEnemy
   - SearchAbstractResourcePoint
   - EngageAbstractEnemyPoint

注意点：

1. TargetAgentId 为空时，Router 会路由给 Primary Agent
2. TargetRef 是新统一目标字段
3. TargetObject / TargetPosition / PayloadId 保留旧调用兼容，不应作为新逻辑首选

来源：

- Assets/Scripts/Gameplay/Agent/Data/AgentDirectiveRequest.cs

### 6.4 DamageRequest

用途：

1. 外部系统对 Agent 造成伤害时传入的请求数据
2. 包含伤害数值、命中点、命中方向

注意点：

1. 敌人攻击 Agent 时应通过 AgentCommandRouter.TryApplyDamage
2. 不应直接修改 AgentPawnRoot.CurrentHealth

来源：

- Assets/Scripts/Gameplay/Agent/Data/DamageRequest.cs

## 7. Brain 状态机与行为树

### 7.1 宏状态

AgentMacroStateId 表示 Agent 当前处于哪个宏观决策阶段。

当前状态：

1. None
2. Explore
3. Combat
4. SearchResource
5. InteractLoot
6. Extraction

来源：

- Assets/Scripts/Gameplay/Agent/Data/AgentMacroStateId.cs

### 7.2 状态机装配

AgentBrainStateMachineFactory 负责：

1. 创建 Root / Raid / Explore / Combat / SearchResource / InteractLoot / Extraction
2. 建立层级关系
3. 给状态添加转移规则
4. 返回 HierarchicalStateMachine

当前转移优先级大致为：

1. 战斗优先级最高
2. 搜索资源其次
3. 撤离在 ShouldExtract 为 true 时进入
4. 没有目标时回到 Explore

来源：

- Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainStateMachineFactory.cs

### 7.3 转移规则

AgentBrainTransitionRules 从 Blackboard 读取事实：

1. HasVisibleEnemy
2. HasResourceTarget
3. HasInteractableTarget
4. ShouldExtract
5. AgentIsDead

注意点：

1. 转移规则只判断能不能进某个宏状态
2. 不在转移规则里执行伤害、拾取、移动
3. 具体动作由宏状态绑定的行为树节点执行

来源：

- Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainTransitionRules.cs

### 7.4 行为树执行节点

当前已存在：

1. MaintainMacroStateActionNode
   - 维持状态树 Running
   - 同步 CurrentMacroStateId / CurrentMacroStateName
   - 主要用于框架接通验证
2. AgentActionNodeBase
   - 提供 Agent 上下文读取、PendingDirectiveRequest 解析、目标位置解析、移动、清理指令等通用逻辑
3. MoveToTargetActionNode
   - 输入：AgentTargetRef
   - 行为：驱动 Agent 朝 TargetPosition / TargetObject.position 移动
   - 输出：到达后返回 Success，未到达返回 Running
4. EngageEnemyActionNode
   - 输入：Engage 指令中的敌人目标
   - 行为：解析 EnemyHealthController，对目标造成伤害
   - 输出：敌人死亡或目标失效后清除战斗事实
5. SearchResourceActionNode
   - 输入：Search 指令中的 LootBoxEntity 或资源点
   - 行为：前往资源目标，触发资源搜索/预生成，并尝试收纳第一件可用战利品
   - 输出：收纳成功、空箱或目标失效后完成搜索
6. ExtractActionNode
   - 输入：Extract / MoveTo 指令中的撤离点目标
   - 行为：前往撤离点并把进入撤离范围的状态桥接给 RaidFlowController
   - 输出：等待 RaidFlowController 完成撤离

注意点：

1. 执行节点应尽量只依赖公开接口或 Agent 专用适配层
2. 不建议在节点里堆大量 UI / 背包 / 敌人细节
3. 当前节点已先实现 MVP 直连版本，后续可以继续把背包/撤离桥接逻辑抽成 Adapter

来源：

- Assets/Scripts/Gameplay/Agent/AI/Actions
- Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainStateFactory.cs

## 8. 其他系统与 Agent 的交互边界

### 8.1 全局状态 / 胜负系统

当前相关类：

- Assets/Scripts/Gameplay/Raid/RaidFlowController.cs

RaidFlowController 权限：

1. 敌人击杀计数
2. 战利品获取计数
3. 撤离进度
4. 胜利 / 失败状态
5. 输入锁定

Agent 与 Raid 的边界：

1. Agent 不直接设置任务成功或失败
2. Agent 不直接改 _enemiesKilledCount / _lootCollectedCount
3. Agent 通过击杀 EnemyHealthController 间接触发 NotifyEnemyKilled
4. Agent 通过合法拾取/存入背包间接触发 NotifyLootCollected
5. Agent 通过进入撤离点并满足条件间接触发撤离完成

推荐交互方式：

1. Raid 系统要命令 Agent 撤离：

```csharp
AgentCommandRouter.GetOrCreate()
    .TrySetShouldExtract(agentId, true);
```

2. Raid 系统要指定撤离点：

```csharp
AgentDirectiveRequest request = new AgentDirectiveRequest(
    AgentDirectiveType.Extract,
    AgentTargetRef.FromConcreteObject(
        AgentTargetKind.Extraction,
        extractionPoint.gameObject));

AgentCommandRouter.GetOrCreate().TrySubmitDirective(agentId, request);
```

### 8.2 敌人系统

当前相关类：

- Assets/Scripts/Gameplay/Enemy/EnemyHealthController.cs
- Assets/Scripts/Gameplay/Enemy/EnemyBehaviorController.cs
- Assets/Scripts/Gameplay/Enemy/Player/PlayerHealthController.cs

Enemy 系统权限：

1. 敌人血量
2. 敌人死亡
3. 死亡掉落
4. 敌人 AI 行为

Agent 与 Enemy 的边界：

1. 敌人查找攻击目标应逐步从 PlayerHealthController.Instance 迁移到 AgentRuntimeQuery
2. 敌人对 Agent 造成伤害应通过 AgentCommandRouter.TryApplyDamage
3. Agent 攻击敌人应通过 EnemyHealthController.TakeDamage 或后续 Combat Adapter
4. 敌人死亡后的战利品生成仍由 EnemyHealthController 负责
5. Agent 不直接 Destroy 敌人对象

推荐交互方式：

1. 敌人选择最近 Agent：

```csharp
AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();
if (registry.Query.TryGetNearestAgent(transform.position, out AgentRuntimeHandle target))
{
    Transform targetTransform = target.CachedTransform;
}
```

2. 敌人攻击 Agent：

```csharp
DamageRequest damageRequest = new DamageRequest(
    damageAmount,
    hitPoint,
    hitDirection);

AgentCommandRouter.GetOrCreate().TryApplyDamage(agentId, damageRequest);
```

3. 外部感知系统告知 Agent 看到敌人：

```csharp
AgentCommandRouter.GetOrCreate().TrySetVisibleEnemy(agentId, true);
AgentCommandRouter.GetOrCreate().TrySubmitDirective(
    agentId,
    AgentDirectiveRequest.EngageConcreteEnemy(enemyObject));
```

### 8.3 战利品 / 背包系统

当前相关类：

- Assets/Scripts/Gameplay/Backpack/LootBoxEntity.cs
- Assets/Scripts/Gameplay/Backpack/WorldLootItem.cs
- Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs
- Assets/Scripts/Gameplay/Backpack/InventoryUIController.cs

Backpack 系统权限：

1. 箱子战利品生成
2. 地面掉落物拾取
3. 背包格子与装备槽状态
4. 物品进入角色容器
5. 战利品拾取时通知 RaidFlowController

Agent 与 Backpack 的边界：

1. Agent 不直接改背包格子模型
2. Agent 不直接改 LootBoxEntity 的私有保存列表
3. Agent 不应为了 AI 搜索强制打开玩家 UI
4. Agent 搜索箱子应通过 LootBoxEntity 的公开方法或后续 Loot Adapter
5. Agent 自动拾取应通过 InventoryScreenController / 专用无 UI 服务完成

当前可用接口：

1. LootBoxEntity.PrecalculateLootIfNeeded
2. LootBoxEntity.GetSavedItems
3. LootBoxEntity.SaveItems
4. InventoryScreenController.TryPickupItem
5. InventoryScreenController.TryStoreWorldItem

建议补充的 Agent 专用适配层：

1. AgentLootInteractionService
2. TrySearchLootBox(LootBoxEntity lootBox)
3. TryCollectFirstAvailableLoot(LootBoxEntity lootBox)
4. TryCollectWorldLoot(WorldLootItem worldLootItem)

原因：

1. 当前 Backpack 强依赖 UI 操作链
2. AI 搜索不应该打开玩家背包 UI
3. Adapter 可以把“AI 自动拿一件战利品”的规则集中起来，避免行为树节点直接操作复杂背包细节

推荐交互方式：

```csharp
AgentDirectiveRequest request =
    AgentDirectiveRequest.SearchConcreteResource(lootBox.gameObject);

AgentCommandRouter.GetOrCreate().TrySubmitDirective(agentId, request);
AgentCommandRouter.GetOrCreate().TrySetHasResourceTarget(agentId, true);
```

### 8.4 撤离点系统

当前相关类：

- Assets/Scripts/Gameplay/Raid/ExtractionPointController.cs
- Assets/Scripts/Gameplay/Raid/RaidFlowController.cs

Extraction 系统权限：

1. 判断实体是否在撤离点有效区域
2. 将进入/离开状态通知 RaidFlowController
3. RaidFlowController 累计撤离进度并完成胜利

Agent 与 Extraction 的边界：

1. Agent 不直接调用 RaidFlowController.CompleteExtraction
2. Agent 应移动到撤离点，并让撤离系统自然检测进入
3. 当前 ExtractionPointController 使用 CompareTag("Player")，这对多 Agent 不够友好
4. 后续应允许识别 AgentPawnRoot 或 IAgentReadOnly，而不是只识别 Player Tag

推荐交互方式：

```csharp
AgentDirectiveRequest request = new AgentDirectiveRequest(
    AgentDirectiveType.Extract,
    AgentTargetRef.FromConcreteObject(
        AgentTargetKind.Extraction,
        extractionPoint.gameObject));

AgentCommandRouter.GetOrCreate().TrySubmitDirective(agentId, request);
AgentCommandRouter.GetOrCreate().TrySetShouldExtract(agentId, true);
```

### 8.5 UI / 调试系统

UI 可读取：

1. AgentId
2. Position
3. CurrentHealth / MaxHealth / HealthRatio
4. IsDead
5. CurrentMacroStateId / CurrentMacroStateName

UI 不应修改：

1. AgentPawnRoot 私有字段
2. AgentBrainController 内部状态
3. BehaviorBlackboard
4. StateMachineContext / BehaviorTreeContext

推荐读取方式：

```csharp
AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();
if (registry.Query.TryGetPrimaryAgent(out AgentRuntimeHandle handle))
{
    IAgentReadOnly readOnly = handle.ReadOnly;
}
```

## 9. MVP 关键交互链路

### 9.1 AI 击杀一个敌人

目标：

1. 外部系统或测试脚本提交 EngageConcreteEnemy
2. Agent 进入 Combat
3. Combat 行为树执行攻击
4. EnemyHealthController.TakeDamage 结算伤害
5. 敌人死亡后 EnemyHealthController.Die 触发 RaidFlowController.NotifyEnemyKilled
6. EnemyHealthController 生成死亡战利品箱

推荐链路：

1. 目标系统调用 AgentCommandRouter.TrySetVisibleEnemy(agentId, true)
2. 目标系统调用 AgentCommandRouter.TrySubmitDirective(agentId, EngageConcreteEnemy)
3. AgentInterventionController 写入 PendingDirectiveRequest
4. AgentBrainTransitionRules.CanEnterCombat 返回 true
5. Combat 状态行为树执行 EngageEnemyActionNode
6. EngageEnemyActionNode 解析 EnemyHealthController
7. EngageEnemyActionNode 调用 TakeDamage
8. 目标失效或死亡后清理 HasVisibleEnemy / Directive

边界提醒：

1. EngageEnemyActionNode 不直接 Destroy 敌人
2. EngageEnemyActionNode 不直接通知 RaidFlowController 敌人死亡
3. 敌人死亡掉落仍由 EnemyHealthController 负责

### 9.2 AI 搜索一个箱子

目标：

1. 外部系统或测试脚本提交 SearchConcreteResource
2. Agent 进入 SearchResource
3. Agent 前往箱子位置
4. Agent 搜索箱子并生成/读取战利品
5. Agent 拿到至少一件战利品后 RaidFlowController 记录 loot count

推荐链路：

1. 目标系统调用 AgentCommandRouter.TrySetHasResourceTarget(agentId, true)
2. 目标系统调用 AgentCommandRouter.TrySubmitDirective(agentId, SearchConcreteResource)
3. Agent 进入 SearchResource
4. MoveToTargetActionNode 前往 LootBoxEntity
5. SearchResourceActionNode 调用 LootBoxEntity.PrecalculateLootIfNeeded
6. InteractLoot 状态或 Loot Adapter 尝试把物品放入角色容器
7. Backpack 系统成功收纳后通知 RaidFlowController.NotifyLootCollected

边界提醒：

1. AI 搜索不应强制打开玩家背包 UI
2. 若当前没有无 UI 背包接口，应先补 AgentLootInteractionService
3. 行为树节点不直接改 _savedItems

### 9.3 AI 前往撤离点

目标：

1. 外部系统提交 Extract 指令或设置 ShouldExtract
2. Agent 进入 Extraction
3. Agent 移动到 ExtractionPointController
4. ExtractionPointController 通知 RaidFlowController Agent 在撤离范围内
5. RaidFlowController 在满足战利品条件后累计进度并完成胜利

推荐链路：

1. AgentCommandRouter.TrySetShouldExtract(agentId, true)
2. AgentCommandRouter.TrySubmitDirective(agentId, Extract)
3. Agent 进入 Extraction
4. ExtractActionNode 驱动移动
5. Agent 到达撤离点并保持在范围内
6. RaidFlowController 完成胜利

边界提醒：

1. 当前撤离点只识别 Player Tag
2. 多 Agent 化前需要让撤离点支持 AgentPawnRoot 或 Agent Tag
3. ExtractActionNode 不直接设置任务完成

## 10. 边界情况、风险点与已知问题

### 10.1 执行节点已落地但仍是 MVP 版本

现状：

1. 宏状态树已接通
2. 状态转移规则已存在
3. Combat / SearchResource / InteractLoot / Extraction 已绑定最小执行节点
4. SearchResourceActionNode 当前会直接尝试从 LootBoxEntity 收纳第一件可用战利品
5. ExtractActionNode 当前会通过 RaidFlowController.SetPlayerInsideExtractionPoint 桥接撤离进入状态

风险：

1. 搜索与撤离节点仍包含跨系统桥接逻辑
2. 背包 UI 驱动链路较重，AI 自动收纳失败时会持续等待
3. 撤离点仍没有真正支持多 Agent 的进入者身份

建议：

1. 后续补 AgentLootInteractionService，把收纳与 Raid 通知从节点里抽走
2. 后续补 AgentExtractionPresenceService，让撤离点识别 AgentPawnRoot
3. 保持 AgentBrainController 只负责装配与 Tick，不把具体业务塞回 Brain

### 10.2 旧 Player 单例依赖尚未完全替换

现状：

1. Enemy/Player 下仍有 PlayerHealthController、PlayerMovementController、PlayerShootingController
2. 部分敌人脚本仍可能使用 PlayerHealthController.Instance 或 PlayerTransform

风险：

1. 多 Agent 时敌人只攻击旧 Player
2. Agent 受到伤害链路无法统一
3. Agent 与手控玩家职责混在一起

建议：

1. 敌人目标选择迁移到 AgentRuntimeQuery
2. 敌人伤害输出迁移到 AgentCommandRouter.TryApplyDamage
3. 手控 Player 脚本保留为 legacy 或输入驱动层，不作为全局主角权威

### 10.3 ExtractionPointController 只识别 Player Tag

现状：

1. 撤离点通过 other.CompareTag("Player") 判断进入者

风险：

1. Agent 如果没有 Player Tag，撤离不会触发
2. 多 Agent 撤离时无法区分是谁进入

建议：

1. 支持 GetComponentInParent<AgentPawnRoot>
2. 或将撤离检测抽为 AgentExtractionPresenceService
3. RaidFlowController 后续应能记录具体 Agent 的撤离状态

### 10.4 Backpack 当前偏 UI 驱动

现状：

1. LootBoxEntity.Interact 会打开 Inventory UI
2. 物品转移主要由拖拽 UI 完成

风险：

1. AI 搜索箱子会被迫打开玩家 UI
2. 行为树节点若直接改背包内部数据，容易破坏背包一致性

建议：

1. 补 AgentLootInteractionService
2. 提供无 UI 的搜索与自动收纳接口
3. 由 Backpack 系统自己负责 NotifyLootCollected

### 10.5 Router / Registry 运行时自动创建对象

现状：

1. AgentRuntimeRegistry.GetOrCreate 会创建 `[AgentRuntimeRegistry]`
2. AgentCommandRouter.GetOrCreate 会创建 `[AgentCommandRouter]`

风险：

1. 场景层级里可能出现隐式对象
2. 管理对象不一定在 Managers / Bootstrap 下

建议：

1. 正式场景中显式放置 Registry 与 Router
2. GetOrCreate 保留为 MVP 兜底

## 11. 扩展点与后续工作

### 11.1 执行节点适配层

可继续抽出的适配层：

1. AgentMovementMotor
2. AgentCombatInteractionService
3. AgentLootInteractionService
4. AgentExtractionPresenceService

建议实现方式：

1. 目标解析继续统一从 PendingDirectiveRequest 读取
2. 移动逻辑从节点中下沉到 AgentMovementMotor
3. 交互细节通过 Adapter 调用 Enemy / Backpack / Raid
4. 行为树节点只保留流程控制和结果判断

### 11.2 Agent 感知层

当前 SetVisibleEnemy / SetHasResourceTarget / SetHasInteractableTarget 需要外部系统主动设置。

后续可扩展：

1. AgentPerceptionController
2. 敌人扫描
3. 资源扫描
4. 可交互扫描
5. 感知结果自动生成 AgentDirectiveRequest

边界：

1. 感知层负责发现目标
2. Brain 负责选择状态
3. ActionNode 负责执行动作

### 11.3 多 Agent 任务分配

后续可扩展：

1. AgentSquadCoordinator
2. 资源目标分配
3. 敌人目标分配
4. 撤离点分配

边界：

1. Coordinator 只下发 Directive
2. 不直接操作 Agent Blackboard
3. 不直接执行敌人/背包/撤离逻辑

### 11.4 运行时调试面板

后续可扩展：

1. AgentRuntimeDebugPanel
2. 显示 Agent 列表
3. 显示当前宏状态
4. 显示 PendingDirectiveRequest
5. 手动下发 Engage / Search / Extract 测试指令

边界：

1. 调试面板读 AgentRuntimeQuery
2. 调试面板写 AgentCommandRouter
3. 不直接写 Blackboard

## 12. 关键交互链路总结

### 12.1 外部系统下发指令

1. 外部系统构造 AgentDirectiveRequest
2. 调用 AgentCommandRouter.TrySubmitDirective
3. AgentCommandRouter 通过 AgentRuntimeRegistry 找到目标 Agent
4. AgentPawnRoot.SubmitDirective 接收指令
5. AgentInterventionController 写入 Blackboard
6. AgentBrainTransitionRules 根据事实进入对应宏状态
7. 行为树执行节点读取 PendingDirectiveRequest 并执行动作

### 12.2 外部系统读取 Agent

1. 外部系统获取 AgentRuntimeRegistry
2. 通过 registry.Query 查 Primary / Nearest / 指定 Agent
3. 拿到 AgentRuntimeHandle
4. 通过 handle.ReadOnly 读取位置、生命值、宏状态
5. 不直接访问 AgentBrainController 或 Blackboard

### 12.3 敌人攻击 Agent

1. Enemy 系统通过 AgentRuntimeQuery 找目标 Agent
2. Enemy 系统计算伤害
3. Enemy 系统构造 DamageRequest
4. Enemy 系统调用 AgentCommandRouter.TryApplyDamage
5. AgentPawnRoot.ApplyDamage 更新生命值
6. AgentPawnRoot 同步生命值事实到 Blackboard

### 12.4 Agent 攻击敌人

1. 外部系统提交 EngageConcreteEnemy
2. Agent 进入 Combat
3. EngageEnemyActionNode 解析 EnemyHealthController
4. 调用 EnemyHealthController.TakeDamage
5. EnemyHealthController 负责死亡、掉落、NotifyEnemyKilled
6. Agent 清理战斗事实或等待下一个目标

### 12.5 Agent 搜索箱子

1. 外部系统提交 SearchConcreteResource
2. Agent 进入 SearchResource
3. MoveToTargetActionNode 移动到箱子
4. SearchResourceActionNode 触发 LootBoxEntity.PrecalculateLootIfNeeded
5. Loot Adapter 尝试将战利品放入角色容器
6. Backpack 成功收纳后通知 RaidFlowController

### 12.6 Agent 撤离

1. 外部系统设置 ShouldExtract
2. 外部系统提交 Extract 目标
3. Agent 进入 Extraction
4. ExtractActionNode 移动到撤离点
5. ExtractionPointController 识别 Agent 进入范围
6. RaidFlowController 累计撤离进度
7. RaidFlowController 完成胜利

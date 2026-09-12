# 地图指挥、自动路网和统一群路线：大规划

日期：2026-09-12。源码基线：`fcb6df1`。状态：**用户已确认方案，P0、P1a–P1d、P2a 完成，进入 P2b 几何校验/自动生成。** P1a 10/10、P1b 36/36、P1c 18/18、P1d 11/11、P2a 6/6 定向测试通过。实施中持续截图、检查样式及 HUD 协调性，发现问题调整后复拍。当前尚未替换正式地图；用户新增相机/Gizmos 场景改动原样保留。

本文各处“建议/拟定/供 Review”记录方案形成时的设计表述；当前版本作为首版实现基线执行。重要边界变化仍重新说明，阶段内按已授权闭环推进。

## 1. 功能模块概述

### 1.1 要做成什么

为 `Scenezl_Final 1.unity` 制作一套地图指挥功能：**玩家在地图上选择想去的群，系统计算需要经过哪些群，Agent 按顺序前往、处理，最终到达终点；地图持续显示每名 Agent 的真实目标、路线和执行进度。** Agent 自主选择目标时也使用同一套路线机制。

这张地图开局全图可见，采用**深色简约风格**，以矩形表示 Zone，**Zone 名称显示在矩形中央**，以资源、敌人、撤离图标表示区域内的 Cluster。Agent 行进时显示在路线连线上，根据到当前目标群的距离确定线上位置；处理群时移到群图标旁，稍微错开。它将替换现有小地图和 M 放大地图，两种视图共用布局、图标和状态表现。下述内容是本期拟交付功能，尚未实现。

### 1.2 包含哪些功能模块

| 功能模块 | 要提供的能力 | 玩家或关卡制作者能得到什么 |
| --- | --- | --- |
| 地图浏览 | 深色简约地图显示全部区域、群、连接和 Agent，Zone 名称居中；提供常驻小地图和 M 放大地图 | 看清哪里有资源、敌人和撤离点，以及各角色正在去哪里 |
| 地图自动生成 | 从真实场景读取 Zone、群的大小、位置和导航可达关系，自动生成区域矩形、群布局和连接；每条连接只有一个水平或垂直直段 | 得到可直接继续编辑的初始地图，减少手工摆图标和画线；在横竖约束下尽量保留整体方位、邻近和远近关系 |
| 地图编辑 | 编辑区域/群的显示位置，增删群间连接，调整线的端点、留白和样式；支持对齐预览、人工锁定、保存和重开 | 调整地图布局及允许规划的通路；重新生成时保留人工修改，显示位置编辑不会移动真实场景对象 |
| 统一路线规划 | 地图点击、世界点击群和 Agent 自主目标统一计算群路线；首版按实际导航路程选择，预留成本模型接口 | 指定一个终点即可得到经过群的顺序；各种指定方式使用相同规则 |
| 逐群执行和任务恢复 | 按路线依次移动、搜索、清群和撤离；玩家路线优先，受击反击结束后恢复剩余路线 | 命令会落实为真实行动；资源点等待玩家背包操作，敌人群清完再继续，反击不会丢掉原玩家路线 |
| 多 Agent 状态及指令反馈 | 行进角色按到目标的距离定位在线上，处理群时在群图标旁错位显示；展示当前步骤、剩余路线、终点及等待/反击状态；自动/玩家目标共用表示，反馈接受或失败原因 | 从线上位置看接近目标的进度，从群旁标记看谁正在处理该群，角色与群图标互不遮挡 |

群间连线同时代表允许规划的连接和地图上的显示线。一条连接中间不折弯；路线需要转向时，在真实群节点处转向。实际场景里的移动继续由 NavMesh 处理墙体、道路和高低差。

### 1.3 一次完整使用流程

以玩家希望角色前往某个撤离群为例：

1. 玩家在小地图或 M 地图查看目标，向选定 Agent 指定撤离群。首版默认给当前焦点 Agent 下令，具体默认行为见第 2.3 节。
2. 系统结合角色位置、地图连接和真实导航路程，计算合法群路线，例如“资源群 A → 敌人群 B → 撤离群 C”，显示路线及指令结果。示例顺序由实际路网决定，不预设每条路线都包含这三类群。
3. Agent 前往资源群 A，按既有搜索流程到箱子旁等待玩家操作背包；群处理完成后继续下一站。
4. Agent 前往敌人群 B，处理整群存活敌人。途中若受伤触发反击，反击结束后继续原路线。
5. Agent 最终到达撤离群 C，执行正式撤离和结算。地图同步更新当前步骤、等待状态和终态。

Agent 自主选目标也会生成并执行这样的群路线。玩家路线保留执行优先级，自动和玩家路线在地图上使用同一种表达。容量不足、不可达、改令及正常战死等分支沿用后文明确的处理规则。

### 1.4 完成后的交付形态

本期交付包括：绑定当前真实场景的指挥地图、可反复生成和编辑的地图工具、统一的群路线规划/执行能力，以及覆盖多 Agent 和任务中断的自动验证。玩家在游戏内使用地图下令，关卡制作者在编辑器维护地图；编码 Agent 负责构造、运行和检查测试，验证时由测试程序代替玩家操作背包。

## 2. 依据、范围和验收目标

本规划遵循会话提供的 AGENTS.md。目录、文件职责、依赖方向和关键决策先供人类 Review，确认后按小规划 → 实现 → 定向测试/架构审查 → 调整 → 提交 → 下一阶段推进。`.planning/.active_plan` 属于历史工作，不覆盖。

布局修订：用户要求生成的线只能水平或垂直，不使用折线，同时尽量保留场景位置关系。本版据此将原“独立生成骨架，再摆布局”修订为**导航候选连接和无折点横竖布局联合求解**。单条边必须满足横竖约束，方位/间距保真作为优化目标；不会以“尽量”为由自动退回斜线。

### 2.1 已读依据及源码事实

- [源码调研和需求确认](../../outputs/map_command_research.md)、[Agent 框架](../../Assets/Docs/GameplayAgentFrameworkDesign.md)、[项目 README](../../README.md)。
- [自主搜打撤规划](../2026-09-11-scenezl-final1-autonomous-raid/task_plan.md)、[玩家 Cluster 验证规划](../2026-09-12-cluster-command-validation/task_plan.md)、[玩家任务恢复和群接战审查](../2026-09-12-group-aggro-manual-resume/architecture_review.md)、[自动验证工具](../../tools/agent-repro/README.md)。历史文档中的旧规则以本轮明确确认和最新修复记录为准。
- 扫描了 `Gameplay/MapGraph` 的 Config、Runtime、Binding、View，现有地图 Prefab/SO 和两个 Editor 工具；阅读了目标 Authoring、Registry、正式点击入口、自主选择/发现、指令生命周期、导航查询、资源等待和顶部反馈。
- 当前正式地图是 `RaidMinimapController.cs`。已有 MapGraph 的路径和 Agent 状态属于展示投影，**不驱动真实 Agent**。旧 MVP 图有 25 节点、31 条边，不能当成当前关卡路网。
- `Enemy/EnemySpawnPoint.cs` 在 Start 调用 Spawn，已有 HasSpawned，但在确认 Prefab 和成功注册之前就置 true。因此 HasSpawned 只能证明尝试过出生，不能单独证明群已就绪或已清空；就绪适配须结合配置、注册结果及失败诊断。
- 当前目标场景为 `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`。已有 `Logs/SceneRaid/20260912-165307-641/scene-audit.json` 记录 8 个 Zone、28 个静态群（12 资源、14 敌人来源、2 撤离）、2 名 Agent、32 个箱子。本次没有重跑审计；P0 重新核对真实场景、Prefab 覆写、出生和导航配置，运行时活跃敌人群不直接叠加成地图节点。

### 2.2 用户已确认的功能

| 项目 | 实施约束 |
| --- | --- |
| 地图结构 | Zone 显示为矩形，资源/敌人/撤离群显示为图标；开局全图可见 |
| 地图入口 | 替换当前小地图和 M 放大地图，共用数据、布局和视觉规则 |
| Agent 地图位置 | 行进时显示在连线上，到当前目标点的距离映射为线上位置；正在处理群时在群图标旁小幅错开 |
| 地图美术 | 深色简约风格，Zone 名称显示在区域矩形中央 |
| 连线语义 | 线决定可规划的群连接；每条生成边只允许一段水平线或垂直线，无斜线、无中途折点；真实移动仍由 NavMesh 绕障碍 |
| 自动生成和编辑 | 根据 Zone/群的范围、大小和距离联合生成布局、连接，在横竖约束下平衡位置保真和可读性；可移动区域/群，增删连接，调整端点和样式 |
| 指令统一 | 玩家点击地图、世界点击群、自主选目标均生成真实执行的群路线 |
| 优先级 | 自动/玩家使用相同目标和路线表示；玩家路线仍优先，伤害反击后恢复剩余玩家路线 |
| 群处理 | 敌人群清掉整群后继续；资源群到箱旁等待玩家操作背包，不新增正式自动取物 |
| 路径成本 | 首版按真实导航路程，保留未来成本计算模型接口 |
| 自动验收 | Agent 自行构造并运行 Play Mode，测试程序代替玩家操作背包；逻辑 4×，性能 1× 平均大于 60 FPS，战死是正常玩法终态 |

### 2.3 本方案建议默认值，随架构一起 Review

1. 点击群给当前焦点 Agent 下令，地图显示全部 Agent；新合法终点替换该 Agent 剩余路线，拒绝保留旧路线。首版不做框选、多终点排队或跨局保存正在执行的路线。
2. 非撤离终点完成后恢复自主选择；中途经过撤离节点只通行，到指定撤离终点才执行撤离。容量不足沿用自主撤离、保留箱内余物，显式结束原资源路线，不能保留一个永远无法完成的玩家锁。
3. 已完成群保留图节点和连线，途经时免处理；允许将其作为移动终点，界面显示“已完成，前往此处”。禁用/丢失绑定和不可达要区别于已完成。
4. 路线执行期间自主候选可以继续被感知，但自主路线不每个决策 Tick 替换自身。正常情况下在路线结束后重新选择，路径失效/容量撤离通过明确事件处理；不把“全图可见”解释为修改自主发现范围、射线或风险算法。
5. 首版连线为双向，生成和手工新增都校验两个方向。单向通路明确报告为不支持自动接入，不画一条假双向线；若场景确实依赖单向通路，P0 提交方向语义的补充设计再实现。
6. 敌人来源群及其运行时活跃群共用一个节点；独立且没有来源的活跃群可以单独建节点。敌人生命和出生事实仍由原系统管理。

### 2.4 非目标和验收

不制作迷雾、物品自动取走、Zone 级命令、任意世界坐标指令编辑、曲线/斜线/边内折点、战术模型、关卡地形重排；不创建虚拟群或隐藏接头来伪装横竖线，不用图上直线替代 NavMesh，不改变伤害/装备/掉落以保证生还，不迁移整个 Obsolete BoardGame。

验收必须同时证明：场景生成绑定正确，保存重开可复现布局；编辑边会改变真实路线；玩家/自主走同一入口；真实逐群完成顺序和地图一致；受击/改令/多 Agent/背包不破坏身份和结算；正常速度真实渲染平均帧率大于 60。未自然触发的场景分支记录覆盖不足，用确定性构造补证，不把“进过 Play Mode”当成功。

## 3. 架构及职责边界

### 3.1 依赖方向

```text
Editor/MapGraph ──→ 场景 Authoring + 导航只读查询
       │           生成配置、绑定、诊断，不写运行时任务
       ↓
MapGraph/Config ← MapGraph/Runtime（拓扑、成本快照、纯寻路）
                        ↑
                 Agent/Routes（每 Agent 路线状态和逐群编排）
                        │ 经接口读取群事实，经原 Receiver 执行子指令
                        ↓
                 Agent/Commands → 原 AI Actions / Navigation / Combat

MapGraph/Binding → Agent/Routes 的世界目标适配接口 + Targets/Authoring
Raid/安装器 → 组合图、Binding、Agent 路线服务、地图视图
MapGraph/View → 只读地图/路线快照 + AgentCommandRouter 高层命令入口
Automation / Editor Tests → 正式接口和只读证据
```

依赖按子目录约束：`MapGraph/Config`、`Runtime` 不引用 Agent、Targets、UI 或 Editor；`Agent/Routes` 不引用 `MapGraph/Binding`、`View` 或具体场景对象查找逻辑，通过其自有适配接口获取目标事实。Binding 实现接口，Raid 安装器注入。不能形成“Agent Routes → Binding → Agent Routes”环。

**所有权：** SO 是关卡布局和逻辑连接的权威；运行时图快照持有可用连接/成本；每个 Agent 的 RouteController 唯一持有路线、游标和根请求；原 DirectiveLifecycle 唯一持有活动子指令和受击挂起记录；战斗、背包、撤离各自保留最终事实所有权；UI 不持有另一份可推进任务。

采用组合和适配器接现有单目标执行，状态机表达路线阶段，策略接口隔离成本，事件/只读快照驱动显示。使用版本号关联过期结果；不引入通用工作流引擎、第二套 Blackboard、全局万能事件总线或新业务 asmdef。

### 3.2 四种数据必须分清

| 数据 | 内容和归属 |
| --- | --- |
| 世界事实 | 真实 Zone/群引用、世界范围、可到达锚点、成员和完成状态；Binding/原 Gameplay 提供 |
| 地图布局 | Zone 矩形、群局部坐标及全图行列对齐关系、图标、边水平/垂直轴向、端点留白和样式；SO 保存，拖图标不会移动场景对象 |
| 逻辑拓扑和成本 | 哪两个群可以相连、双向有效性、导航折线路程、配置/导航/成本版本；编辑连线影响此层 |
| Agent 路线事实 | 根请求、来源、终点、节点序列、当前索引、子指令 ID、等待/反击/终态；Agent/Routes 持有 |

场景对象引用保存在场景 Binding，不写进独立 SO。地图 nodeId 首次生成后稳定，不能用名称、数组下标或运行时新生成的 TargetId 作为唯一键。Editor 用场景对象引用和 GlobalObjectId 对照更新，运行时建立目标 ID、来源群、成员到 nodeId 的索引，重复/孤儿绑定生成可定位诊断。

Editor 校验后的导航长度、方向、配置及输入指纹作为独立烘焙数据结构存入图 SO，只含稳定 ID/数值，不含场景对象。运行时从它创建只读成本快照，动态失效和补算只影响本局快照，不回写资产。这样打开地图或每局启动不需要重复全对导航计算。

### 3.3 关键契约（拟定接口，P1 固化签名）

| 契约 | 责任和限制 |
| --- | --- |
| `AgentCommandRouter.TrySubmitRoute(agentId, AgentRouteRequest)` | 唯一高层群命令入口；请求包含目标节点/规范群身份、Player/Autonomous 来源、根 requestId；返回规划中、接受或拒绝及原因 |
| `IAgentCommandReceiver.TrySubmitRoute(...)` | 由 AgentPawnRoot 接到自身路线控制器；原具体 Directive 接口保留给路线子步骤、战斗反击和低层兼容调用 |
| `IAgentRouteTargetResolver` | Binding 提供规范节点、可达世界锚点、成员事实、群状态和具体子指令候选；不推进路线，不修改背包/生命 |
| `IMapGraphCostProvider` | 从冻结的图/成本上下文提供非负有限代价和可用性；首版读 NavMesh 路程快照，后续模型只替换成本提供者 |
| `MapGraphPathfindingService.Resolve(...)` | 输入图快照、起点、终点、成本策略，输出不可变路径结果；不查场景、不调用 UI 或动作 |
| `AgentRouteSnapshot` / 路线结果事件 | 只读 requestId、版本、来源、终点、节点序列、游标、当前边/方向/目标锚点、阶段、子指令/挂起关联和失败原因；供 UI、测试读取。地图进度及其距离采样不推进此游标 |

未来计算模型可先异步产出成本快照，再用同一寻路接口；本期只保留这个边界，不实现模型、网络或后台计算。负数、NaN、缺失成本不能进入搜索。过期快照不得覆盖较新命令。

## 4. 自动生成算法和编辑体验

### 4.1 场景采集和导航锚点

1. 显式从当前场景的 Zone/Cluster 层级及已绑定列表采集，解析真实 Prefab 实例和覆写；生成的是规范群节点，不能把出生点 Prefab 当敌人或额外节点。
2. Zone 范围优先取既有 RangePoints/配置碰撞体，群范围来自成员和现有范围能力。未分区群输出未分区矩形，重复归属要求修正，不静默随机选择。
3. 在实际 NavMesh 上为群生成可复核锚点。资源/撤离优先合法成员接近位置，敌人来源优先出生区域的可行走位置；中心落墙内、空中、桥下时不能盲目大半径采样到另一层。保存采样高度和所属导航配置证据。
4. 导航查询提取无副作用的 origin/destination/profile 能力，复用路径缓冲和完整路径校验；Editor 不能为算连线移动角色，也不能假造一个正在运行的 NavMeshAgent。记录 agentType、areaMask 和代价配置，不同能力的 Agent 不共用未经验证的成本。

### 4.2 自动连线：可达候选 → 带布局约束的骨架 → 可对齐的补充连接

**推荐首版默认：最近邻数量 k=4，额外短路改善阈值 1.5，采用中心行列对齐和上/下/左/右四个连接方向。** 每方向先只接受一条不重叠的边，避免共线遮挡或穿过另一个群。目标密度可调低，不能单纯调高度数绕过端口/几何有效性。此前“人工/骨架边可以超过度数目标”的自由连线方案在此作废；无法容纳的固定人工连接要报告冲突。

| 步骤 | 方法 | 输出/约束 |
| --- | --- | --- |
| 候选排序 | 用真实世界锚点距离和 Zone 邻近关系优先枚举群对；先查每节点 k 个近邻，同 Zone 优先只影响查询顺序 | 人工图上拖动不改变物理可达候选和导航成本；自动生成挑哪些候选还要考虑布局可行性 |
| 可达性和长度 | 对候选正反两方向求完整 NavMesh 路径，长度为路径拐点三维距离之和 | 绕墙可通则合法；断层无通路则不连；只得到 Partial 不算合法 |
| 完整校验 | 当前 28 节点规模继续查完剩余群对，共最多 378 对、756 个有向查询/导航配置（不含采样重试） | 得到可达图，避免 k 近邻遗漏两个局部集合间的真实出口；Editor 分帧、可取消、缓存 |
| 连接骨架 | 以最小生成森林及地理邻近方案作为候选种子，纳入人工保留边、排除禁连边；尝试水平/垂直方向分配、节点/Zone 调整以及有限的自动边交换 | 连通性、真实可达和横竖单直段同时验收；MST 只是初始候选，不承诺任意 MST 都能无折点画出 |
| 额外连接 | 在布局可行的前提下加入近邻短边，或加入能明显减少图上导航绕行的边；新边先试同排/同列，再有限调整未锁定布局 | 位置畸变过大或不能对齐的可选边不加入；不得画斜线或给一条边补 L 形弯来凑数 |
| 诊断和预览 | 输出分量、不可达/单向/人工排除、几何约束冲突、搜索预算耗尽，以及布局保真分项和边数/查询耗时 | 物理断图和布局求解失败分开；未解决骨架连通性时保留草稿及旧有效图，不能把断开的新图标为生成成功 |

最小生成树只用来提出初始拓扑，**不是玩家下令时的寻路算法，也不是最终固定边集**。生成器可以替换尚未锁定的自动候选边，换取可对齐且保留方位的路网，差异在生成事务里展示。已保存图的普通拖动/“仅重排布局”不得偷偷换边；人工边始终保持。运行时在最终有效编辑图上选最短路线，布局评分不混入运行时实际路程成本。

较大地图先局部候选，再扩展跨分量候选；未完成全对验证标记“尚未完整验证”。当前场景走完整导航校验；布局采用有界启发式，不承诺任意固定图都存在满足全部条件的解，也不把预算耗尽当成数学上的无解证明。

这里的“连接”表示允许从群 A 任务接续到群 B。路线可以在**真实群节点**处转向，例如 A 横向到 B，再由 B 纵向到 C，B 必须是真实被经过/处理的群，不能只为画线临时插入。实际导航可能绕过其他区域；两条显示线交叉不生成节点、不得在那里换线，物理经过非路线群附近也不自动插入其搜刮任务，伤害反击仍正常发生。

### 4.3 最短路成本口径

首版边权为合法世界锚点之间的真实 NavMesh 折线长度，起步加 Agent 实际位置到入图锚点的路程，**不使用屏幕连线长度或世界直线距离**。它代表沿当前作者定义路网的最短预计移动路程，不包含追逐移动敌人、逐箱搜索的未来位移，也不声称是任意地形路径中的全局最优任务序列。

入图点独立于所选终点：优先当前已到达节点；Agent 处于边上时比较该边两个合法端点；没有路线时按所在区域/邻近世界锚点依真实可达距离选择起点。不能为使路径最短而把当前位置临时连接到全图所有节点，那会直接绕过作者连线。反击产生偏移时重新验证返回当前步骤或入图路径，原根请求/剩余任务优先保留。

沿用已有 Dijkstra，首版 28 节点规模使用 O(V²+E) 的实现即可，加入确定性同成本排序和复用查询工作区。将成本从 `LengthUnits` 的显示语义中剥离；是否上堆由后续节点规模/测量决定，不为算法名先引入新容器。路径代价一致性有独立小图穷举对照。

### 4.4 Zone 矩形和无折点横竖布局

**硬约束：** 每边端点满足 `yA = yB` 或 `xA = xB`，且长度大于零；边内没有任何折点；节点不重叠，线不穿非端点节点，群属于真实 Zone；人工锁/人工边/禁连关系保留；有效拓扑仍达到可达候选分量要求的连通性。不通过放大误差容忍把斜线当横竖线。保存的是行列约束，投影后两端从同一坐标来源取值，避免各自像素取整造成斜线。

**软目标：** 在满足硬约束的可行结果里，优先少颠倒原世界的左/右、上/下顺序，保留 Zone 的整体方位和邻接关系，再平衡群的相对远近、归一化位移、Zone 面积比例、地图紧凑度及线交叉数。明显的方位反转比某个点从“右上”吸附到“右侧”惩罚更高；不能为了整齐把全图压成一条横线。水平线和垂直线数量不强求一半一半，原场景总体走向优先。

两点原本位于斜向，直接单直段连接后至少要弱化一个轴的相对差异，因此不承诺精确复刻每个角度和距离。多个真实群可通过横向/纵向序列表达整体向右上或左下延伸。这个权衡在地图布局层完成，实际世界位置和导航路程始终保持。

拟定生成步骤：

1. 以世界 XZ 位置、范围和间距生成参考布局；Zone 的最小矩形受群数量、图标/标签间距约束，中央预留 Zone 名称安全区，图标及连线布局避开该区域。不先在互相独立的 Zone 局部坐标里锁死所有位置。
2. 在参考布局上建立行/列候选；对候选边按世界主要方向优先尝试水平或垂直，接近对角线时保留两种方案。横边合并 y 对齐组，竖边合并 x 对齐组，对有向左右/上下间隔施加最小间距。
3. 对方向选择执行固定顺序、有候选宽度和展开次数上限的搜索，坐标求解在每个候选内交替投影行列、边界和间距约束；有界迭代后仍冲突则回退该候选。保留多个可行方案，按统一评分选择，固定输入可重复生成。
4. **跨 Zone 边使用全图坐标求解。** 可以调整未锁定 Zone 的位置/大小及内部群，保持区内包含和区间留白；不能各区布局完成后再拉一根斜线连接。求解出的全图位置最后再写回各 Zone 局部坐标。
5. 固定候选骨架不可行时，在允许生成连接的操作中尝试有限交换自动边；加入补边也要重验约束。允许扩大未锁定矩形、降低可选边密度，不能删必要桥边后假报成功，也不能动人工锁。
6. 输出分项评分：方位反转、斜向关系压平、近邻距离失真、节点归一化位移、Zone 面积变化、交叉数、总面积和横/竖边数；“均衡”配置作为初始参数，在当前场景预览及构造对照后校准，不预先宣称已达到最优。

几何校验独立于求解器，最终候选必须再次通过：单段横竖、合法端点、无穿节点/共线覆盖、Zone 包含、中央名称安全区、导航连通、人工意图和身份不变。一般线交叉尽量减少，不能靠创建假节点消除；现阶段不额外承诺所有输入都能无交叉。

算法边界参考：普通正交图常允许一条边由多段横竖线组成，“无折点”是额外约束，不能直接拿最少折点算法承诺零折点。[正交图绘制研究](https://arxiv.org/abs/1910.11782)、[无折点直角平面绘制研究](https://arxiv.org/abs/2208.12558)。本项目还加入 Zone 和世界位置保真，以上研究用于核对概念，**并未证明这里提出的有界启发式必然找到解**。

### 4.5 编辑器操作及再生成规则

工具窗口提供“采集/同步场景 → 导航候选校验 → 联合生成连接/布局 → 验证 → 保存”入口，也提供固定边集的“仅重排布局”。重新生成连接会展示自动边的增删差异，普通拖动和仅重排保持拓扑。大操作先在临时结果中计算，成功后一次应用到 SO/场景 Binding，支持 Undo/Redo、取消不污染资产、保存后重开校验。

- 画布支持选择、拖动 Zone/节点，缩放/平移，显示真实对象和行列对齐辅助线。拖动时吸附行列，预览联动的未锁定节点/Zone，松开后以事务应用；受人工锁限制无法维持横竖时保留原有效布局，显示具体冲突。自由拖动草稿不能直接成为可运行配置。
- Zone 移动带动内部群，跨区连接需要联动对齐或限制可移动方向；缩放也必须重验全图约束。单独移动节点不修改业务 Zone 归属，运行时不会重新排列布局。
- 从上/下/左/右端口向另一群加边，或重绑定既有端点，触发同一约束预览。端点留白只沿该边轴向伸缩；若支持横向通道偏移则两端使用同一偏移且仍落在节点合法端口范围内，不能独立拖两端制造斜线。加边失败不接受悬空端点或自动补弯。
- 线宽、颜色和端点留白允许主题默认及单边覆写。每条边始终是一个水平/垂直直段；路径高亮、多 Agent 色条也维持同一轴向，不新增弯折或斜接头。
- 布局锁、边来源 Generated/Manual、视觉覆写和**人工禁连对**分别保存。增量生成保留手工新增、拖动和删线意图；不得下一次点击生成就把删线补回。只有显式“重置人工覆写”才恢复自动布局/禁连默认值。
- 删除场景群不会重用 nodeId；工具列出孤儿及受影响边，提交清理差异。场景/NavMesh/配置变化使烘焙成本失效，要求重新验证，不悄悄采用过期长度。

## 5. 统一命令和真实路线执行

### 5.1 提交、接受和替换

世界点击的 `AgentTargetCommandDispatcher`、地图点击和自主 Decision/Discovery 全部调用 Router 的高层路线入口。Dispatcher 保留焦点解析/点击兼容职责，**不再自己生成整条玩家任务的单个 Engage**。`TargetClusterDirectiveFactory` 的具体成员构造供路线子步骤使用，不能反过来再次调用高层入口。

新请求按 agentId + 根 requestId 校验图、目标、可达入图点和完整路径。需要补算成本时返回 Planning，旧有效路线继续，**不提前显示“指令下达成功”**；图/成本已可用时可以同帧完成规划。新路线就绪且首步骤可激活后，原子替换旧路线/挂起状态，发布一次 Accepted。失败返回具体原因，旧路线/背包会话不被预先清空。过期规划和旧子任务回调通过 routeVersion + commandId 丢弃。

低层 Directive 的职责保持可解释：移动/交战/资源/撤离动作以及有效伤害反击不是新的“指定群”操作。正式高层目标调用必须全部迁移；低层兼容入口在已安装路网的正式场景不允许无来源的群命令绕过路线。未绑定路网的旧夹具/旧场景保留明确兼容模式，当前主场景不静默退回旧单目标执行。旧 `Targets/Input/UIMap.cs`、`Targets/Input/AIIntentController.cs` 只审计是否被当前场景实际启用，若有活跃输入则适配或停用重复入口，不整包重写历史模块。

### 5.2 状态和步骤完成

```text
Planning → Travelling → Processing → 下一节点 / Completed
                         ├─ WaitingInventory → Processing
活动阶段 ─有效伤害─→ Retaliating ─反击结束─→ 恢复原阶段/剩余路线
路径或绑定失效 → Replanning → 恢复 / Failed
任意阶段 → 新有效指令替换 / CapacityExtraction / Dead / Extracted / Cancelled
```

状态表示路线层事实，实际单目标动作状态仍由原 Brain/生命周期管理；不要新增一个每帧重发 MoveTo/Engage 的并行行为树。

每个路线节点先经原 MoveTo/导航链到达其合法锚点，再进入群处理；若已处于该节点有效到达范围则省去重复移动。远程提前清掉某群也不能跳过作者要求经过的节点，已完成节点仍验证到达；最终节点同样有位置到达条件。处理群成员造成的位移由真实导航记录，下一段从实际位置接续，不瞬移回锚点。若群可被射击但不存在合法可到达节点，则属于战斗可交互、路线不可通行的不同条件，不能凭远程射程制造导航连接。

| 节点 | 处理和完成条件 |
| --- | --- |
| 资源 | 复用 SearchResourceActionNode 的群内成员搜索及背包会话。WaitingInventory 是有意等待，不按普通移动 NoProgress 超时；关闭/拿取/容量判定沿用正式语义，测试驱动使用既有背包入口 |
| 敌人 | 规范来源群对应的活跃成员逐一生成合法 Engage；每个子 Engage 结束重新读整群存活事实，仍有可执行敌人则接续。清群后才推进游标；全部候选暂不可执行时有限重试/重规划，不能冒充清完 |
| 敌人出生尚未就绪 | 不能用“这一帧列表为空”判群完成；区分场景注册/出生完成和成员死亡。P0 核对现有出生链，缺少只读就绪事实则在原出生所有者补事件/状态，小规划列出具体文件 |
| 撤离 | 中间节点只导航通过，终点复用 Extract 和正式 presence/计时/结算；伤害反击后恢复当前有效撤离，保持此前规则 |
| 已完成节点 | 仍按拓扑通行，到合法锚点或已满足步骤条件即可继续，不重复开箱/击杀/结算；相邻逻辑边不能因省处理而凭空跨越 |

路线控制器先判断群事实，再处理子指令结果，避免“最后一个敌人被同伴击杀→恢复校验报目标失效→整条路线失败”。反过来，其他成员仍存活也不能因一个敌人死亡而结束群。多 Agent 可处理同群，不新增全群独占锁；资源会话和停靠占位复用既有所有权。

### 5.3 手动锁、反击及容量撤离

- 玩家锁扩大到整个有效玩家路线，包括两个子任务之间和背包等待。原 `AgentManualDirectiveLock` 同时读取路线只读事实，不能仅依赖当前 Blackboard 中一条 PendingDirective。
- 唯一受击挂起记录继续留在 `AgentDirectiveLifecycleController`；RouteController 只冻结游标，监听 Suspended/Resumed/终态，不再保存另一份可恢复 Directive。连续受击保持当前有效反击目标，反击结束恢复原玩家路线。
- 路线管理的自主子任务在反击后也要能恢复剩余路线；通过显式 routeId/step 身份让原生命周期识别，避免把“所有自主 Engage 完成”都当路线完成。路线步骤身份不靠复用 PayloadId 或猜测 CommandId 前缀。
- 新合法玩家路线清理旧活动/挂起路线关联；拒绝不清理。死亡、撤离和场景卸载收尾一次，取消旧订阅/待规划结果，不能旧回调复活任务。
- 背包容量不足是独立策略事件：原资源路线发布清晰结束原因，解除玩家锁，统一入口生成撤离路线，途中已选撤离则继续当前有效目标；箱内余物不标记成已拿走。不存在可达撤离点则报告明确失败，不循环下令。

### 5.4 路径失效和性能调度

生成后的图和成本共享；每 Agent 只持有其请求、路径和查询工作区。初始化/地图版本改变重建索引，成员死亡更新业务状态而不重算全图。规划由新目标、真实路径失效、导航版本变化等触发，相同自主目标去重，失败记忆按原策略冷却。

NavMesh 缓存键至少包含节点/锚点版本、导航配置、导航修订和方向；动态障碍由执行时完整路径验证兜底。重试有次数和墙钟上限，有进展和有意等待分开判定。Editor 生成做全对查询，运行时只补受影响边/入图路径，分帧预算，不能每帧扫描全部群、重新生成 MST 或开销失控地校验所有成员路径。

## 6. 地图展示和风格

### 6.1 深色简约主题和 Zone 名称

小地图和 M 放大图读取同一个布局、主题和路线快照。美术采用深灰底、低对比区域填充/细边框、细灰色横竖连接和有限的强调色，使用简洁资源/敌人/撤离图标。Agent 以稳定颜色和身份区分，当前路线比背景连线突出，玩家/自动来源使用小标识；不靠整块高饱和区域、厚重装饰或持续闪烁表达信息。

Zone 名称锚定在区域矩形几何中心，水平/垂直居中，在两种地图上都保留。生成布局为名称预留安全区，文字底下不叠群/Agent 图标或路线；长名称采用可读字号范围内的缩放/换行，小地图必要时省略显示，放大图仍在中心显示完整名称。矩形过小则扩展未锁定区域或报告布局冲突，不能自行把名称移到左上角来省空间。

复用正式 HUD 字体及适合此主题的现有图标资产，通过统一 Theme 控制颜色、线宽、字号、偏移和间距。紧凑视图默认全图，减少群标签文字，突出焦点路线和其他 Agent 终点；放大视图支持缩放/平移和完整状态。P5 使用真实 HUD、两个地图尺寸以及等待/反击截图校准可读性。

### 6.2 行进 Agent：距离映射到当前连线

**本次用户要求替代原“反击时允许脱离线段显示”的草案。** 有有效当前边时，行进 Agent 的标记中心始终在该边线上。世界坐标用于计算真实距离，不能再直接投影成线外的位置；地图表达的是任务路段进度。

“目标点”取**当前正在前往的群的导航锚点**，不是整条路线最终终点，也不是反击时临时追逐的敌人。具体距离度量建议使用到该锚点的剩余导航路程，与路线成本口径一致，避免隔墙/上下层直线距离很小而看似已到达。这个度量选择及下列校准规则是实现建议，用户已确认的是距离映射和线上显示要求。

正常从边起点出发时，设开始该段的有效剩余距离为 `d0`，当前剩余距离为 `d`，正式到达容差为 `r`：

```text
t = clamp01((d0 - d) / (d0 - r))
地图位置 = Lerp(该边出发端口位置, 该边目标端口位置, t)
```

分母过小、非有限值或没有有效路径时走下面的明确状态分支，不直接除零。两个端口位置复用边视图经过群图标裁边后的实际端点，同一水平/垂直坐标来源保证角色在线上。正反向行进按实际出发/目标方向取端点，不能把边资产的 from/to 固定当作角色方向。

- `d0` 在进入当前段时保存，不每帧随当前位置重设；距离增加时允许 `t` 后退，不能为了看起来持续前进取历史最大值。接近目标时，是否进入处理状态仍由正式到达/步骤事件决定，UI 不能自己调用完成边/推进路线。
- 同一条边中途重规划或恢复有效距离采样时，可保留当前 `t0`，用新基准 `d0` 校准 `t = clamp01(t0 + (1-t0) × (d0-d)/(d0-r))`。校准只在明确的路径版本/采样恢复事件发生，不每次采样重置；保留旧/新路径版本和基准用于诊断。
- 距离有效性带 routeId、边/步骤 ID、目标锚点和路径版本。导航正在去同一锚点时优先读已有剩余距离；反击时 NavMesh destination 已换成敌人，不能误读该值当原群距离。需要时复用只读导航查询，独立限频/缓存；地图每次重绘不重新算路径。
- 路径暂缺、pathPending、距离 NaN/Infinity 或路径失效时，保留最后有效线上位置并标明重规划/定位暂不可用；不沿用现有代码“缺数据就按时间匀速前进”的兜底。
- 可在相邻有效样本之间平滑 `t`，平滑后仍严格在线上；无新样本不外推，不因图标看似到端点就推进真实任务。不同边切换使用新步骤事实重置采样，不能沿旧边补间穿过画面。

### 6.3 处理目标群、反击和多 Agent

| 实际状态 | 地图显示规则 |
| --- | --- |
| 当前边行进 | 按到当前群的距离定位在边线上；前进/后退只来自有效距离 |
| 处理资源/敌人群、等待背包、撤离计时 | 在当前群图标旁小幅错位显示，不与群图标重合；显示处理/等待/撤离状态，不把群内逐箱/逐敌移动误画成跨群路线 |
| 行进时受击反击 | 保留原当前边，用到原路线锚点的有效距离更新线上位置，叠加反击状态；缺有效距离则暂留原线上位置，反击结束继续该路线 |
| 群处理中受击反击 | 保留原群旁的偏移位置，显示反击状态；恢复后仍对应原群，不随临时敌人位置跳到别的图标 |
| 初始化/待入图/没有有效路线 | 作为尚无可映射路段的边界状态，绑定入图/最近已知节点旁显示待入图或待命；不生成假边，不标记为已到达或正在处理。无法解析节点时显示无定位状态 |
| 死亡/撤离 | 按原终态停止进度、撤销活动路线高亮；保留终态信息或移除在场标记，不继续沿线移动 |

处理中偏移取图标半径之和加少量 Theme 间距，优先使用不遮住入/出连线和 Zone 中央名称的固定角落位置。即使只有一个 Agent 也要偏移；多 Agent 使用稳定槽位分配，某人离开时其他标记不随排序重排。偏移属于屏幕显示，不修改目标位置或路线终点。

同边多个 Agent 仍分别用自己的距离得到线上位置。重叠时使用组合身份标记/颜色分区及人数提示，标记锚点仍在线上；不要沿边法线分“车道”，也不为了拉开间距随意改变距离进度。处理中才允许移到群图标旁；身份标签可另排，不能把角色核心标记挪离行进线。

地图显示实际存在的每 Agent 路线，不为执行玩家路线的角色另造一个竞争的自动路线。原 `AgentGraphProjectionController` 只采集权威路线和距离事实，显示进度由独立纯计算负责；去掉旧的 UI 自寻路、匀速推进及按距离阈值自行推进图上节点游标的行为，真实步骤推进仍由路线执行层独占。

### 6.4 点击和根指令反馈

地图 UI 拦截射线，点地图不能再触发世界点击；小地图和大图只由一个 Presenter 处理一次请求。顶部提示在根玩家请求接受/拒绝/执行失败时显示，群内切换下一个敌人不重复提示成功；复用已修正的“指令”字体和淡入/淡出规则。

## 7. 目录、文件归属和 Reuse / Extend / Wrap / Create

下面是供 Review 的文件边界，不代表文件已创建。表内文件名组合仅为减少重复路径，每个新增文件有独立职责。所有新 C#、目录和 Unity 资产同时生成并提交 `.meta`。不移动现有脚本、不重建已有资产 GUID；目前没有删除生产文件的计划。

### 7.1 图配置、世界绑定、导航和搜索

本表相对 `Assets/Scripts/Gameplay/`。

| 判断 | 具体文件 | 职责、独立存在或修改理由 |
| --- | --- | --- |
| Extend | `MapGraph/Config/SO_MapGraphDefinition.cs` | 图版本、Zone 列表、生成参数、行列约束、人工覆写及导航烘焙数据；纯资产，不引入场景/Agent 引用 |
| Extend | `MapGraph/Config/MapGraphNodeDefinition.cs` | 稳定节点身份、Zone ID、局部布局/占位范围、行列关联和锁定标记；旧节点坐标作显式版本迁移 |
| Extend | `MapGraph/Config/MapGraphEdgeDefinition.cs` | 逻辑双向端点、水平/垂直显示轴向、生成/人工来源、受轴向约束的端点留白及样式覆写；分开历史 LengthUnits 和导航成本 |
| Create | `MapGraph/Config/MapGraphZoneDefinition.cs` | Zone 矩形、中央名称安全区、布局锁及显示配置；区域是独立数据类型，不塞进节点可选字段 |
| Create | `MapGraph/Config/MapGraphGenerationSettings.cs` | 布局评分、对齐/间距、密度、导航查询、搜索预算及人工禁连参数；无生成算法 |
| Create | `MapGraph/Config/MapGraphLayoutConstraints.cs` | 序列化全图行列对齐、轴向锁/位置锁和最小间距；与纯显示坐标分离，便于增量/拖动维护，不包含拐点数据 |
| Create | `MapGraph/Config/MapGraphNavigationBakeData.cs` | 可序列化的边长/方向/导航配置和输入指纹，独立于本局可变缓存及显示端点 |
| Create | `MapGraph/Config/SO_MapGraphTheme.cs` | 深色简约配色、字体、中央名称排版、群/Agent 尺寸、处理时偏移间距、线宽及状态样式；两图共用，不在 Controller 内硬编码美术 |
| Reuse | `MapGraph/Config/MapGraphNodeIconSet.cs`、`MapGraphNodeKind.cs` | 现有资源/敌人/撤离图标和类型；来源/活跃敌人规范化属于绑定层 |
| Extend | `MapGraph/Runtime/MapGraphService.cs`、`MapGraphPathfindingService.cs` | 图索引和纯最短路；接成本提供者、稳定排序和工作区，保留旧 MVP 明确迁移/兼容能力 |
| Create | `MapGraph/Runtime/IMapGraphCostProvider.cs` | 纯图成本查询契约；算法不绑定 NavMesh 或未来模型 |
| Create | `MapGraph/Runtime/MapGraphCostSnapshot.cs` | 不可变的可用边、成本、导航配置和版本快照，实现首版成本查询；不自己扫描世界 |
| Extend | `MapGraph/Binding/MapGraphBindingAuthoring.cs`、`MapGraphTargetBinding.cs` | 增补 Zone 绑定、世界锚点及索引，保持场景引用所有权和稳定 ID |
| Create | `MapGraph/Binding/MapGraphZoneBinding.cs` | P1d 将区域直接引用单独存为数据项，避免混进节点绑定或独立地图 SO |
| Create | `MapGraph/Binding/MapGraphClusterResolver.cs` | 规范 EnemySource/ActiveEnemy/成员身份，实现 `IAgentRouteTargetResolver` 的世界事实和子指令候选适配；不持有路线 |
| Create | `MapGraph/Binding/MapGraphNavigationCostService.cs` | 调用导航查询生成/补充成本快照，缓存失效、预算和诊断；算法/Editor/Agent 不各写一套路径长度测量 |
| Create | `Agent/Navigation/AgentNavigationSegmentQuery.cs` | 从既有 Query 提取 profile + origin + destination 的无副作用路径查询/采样/长度能力，供运行时和 Editor 共用 |
| Create | `Agent/Navigation/AgentNavigationProfile.cs`、`AgentNavigationSegmentResult.cs` | P1b 将导航参数快照及只读路段事实独立为数据文件；前者复制 32 个区域成本，后者不暴露可变原生 Path，避免查询算法兼任数据所有权 |
| Extend | `Agent/Navigation/AgentNavigationQuery.cs` | 委托共用段查询，仍负责针对 live Agent 的 readiness/arrival 等校验，不破坏原导航失败语义 |
| Reuse | `Agent/Navigation/AgentNavigationMotor.cs`、`AgentResourceNavigationResolver.cs` | 实际移动及资源接近位置能力；不向图算法转移导航执行所有权 |
| Reuse | `Targets/Authoring/TargetZoneAuthoring.cs`、`GameplayTargetClusterAuthoringBase.cs`、`ResourceClusterAuthoring.cs`、`EnemySourceClusterAuthoring.cs`、`ActiveEnemyClusterAuthoring.cs`、`ExtractionClusterAuthoring.cs`、`Targets/Runtime/GameplayTargetRegistry.cs` | 既有范围、群归属、成员及完成事实；只在 P0 证明缺少只读事实时列出局部扩展，不默认重写完成语义 |
| Reuse / 按需 Extend | `Enemy/EnemySpawnPoint.cs` | 原出生尝试和注册流程；优先复用现有事实，若需区分成功/失败，只在本文件补只读出生结果，不让地图创建敌人或猜测固定等待时长 |

### 7.2 Agent 路线和命令迁移

本表相对 `Assets/Scripts/Gameplay/`。

| 判断 | 具体文件 | 职责、独立存在或修改理由 |
| --- | --- | --- |
| Create | `Agent/Routes/AgentRouteRequest.cs` | 根请求、目标来源和显式步骤上下文的数据结构；不借用业务 PayloadId |
| Create | `Agent/Routes/AgentRouteState.cs` | 内部路线状态和对外只读 Snapshot，节点游标/版本/子任务关联；不查询世界或执行动作 |
| Create | `Agent/Routes/AgentRouteResult.cs` | 结构化接受、拒绝、终态和原因；统一 UI/日志读取，区别于具体子指令结果 |
| Create | `Agent/Routes/IAgentRouteTargetResolver.cs` | Agent 路线层要求的世界事实/候选契约；Binding 实现，防止 Agent 引用地图场景适配器 |
| Create | `Agent/Routes/AgentRoutePlanner.cs` | 入图点解析、调用纯图搜索、规划版本和结果准备；不启动动作、不绘制路径 |
| Create | `Agent/Routes/AgentRouteController.cs` | 每 Agent 唯一路线生命周期，提交原子替换、游标、受击冻结、根结果及实例结果事件；不做群扫描算法和 UI，不新增全局路线事件总线 |
| Wrap | `Agent/Routes/AgentClusterStepExecutor.cs`（新增） | 将群事实和原具体 Directive 执行包装为逐群步骤；持有当前步骤结果关联，不持有整条剩余路线，不保存第二份反击任务 |
| Extend | `Agent/Core/AgentPawnRoot.cs` | 组合路线控制器，接收/暴露路由和只读状态，按既有生命周期销毁；不堆入路径算法 |
| Extend | `Agent/Interfaces/IAgentCommandReceiver.cs`、`IAgentReadOnly.cs` | 增加高层路线请求和只读 Snapshot 边界；必要配置通过 PawnRoot 组合入口注入 |
| Extend | `Agent/Runtime/AgentCommandRouter.cs`、`AgentManualDirectiveLock.cs` | 统一高层路由、焦点解析及整条玩家路线锁 |
| Extend | `Agent/Data/AgentDirectiveRequest.cs`、`Agent/Commands/AgentDirectiveLifecycleController.cs` | 显式根路线/步骤关联；原生命周期继续独占活动/挂起 Directive、验证和恢复 |
| Extend | `Targets/Input/AgentTargetCommandDispatcher.cs`、`PlayerInputManager.cs` | 世界点击适配高层路线请求、结果及 UI 拦截；不在点击层执行寻路或清群 |
| Extend | `Targets/Input/TargetClusterDirectiveFactory.cs` | 提供规范群的具体子步骤构造，复用原候选选择；根请求提示移交路线结果 |
| Reuse | `Targets/Input/TargetClusterDirectiveCandidateSelector.cs`、`Agent/Commands/AgentDirectiveValidationService.cs`、`AgentDirectiveFeedbackChannel.cs` | 成员可执行性、前提校验和子指令事实；路线层不跳过它们 |
| Extend | `Agent/Decision/AgentTargetDecisionController.cs`、`Agent/Runtime/AgentTargetDiscoveryController.cs` | 自主选中群后统一提交路线，尊重在途路线锁、完成/失败和容量撤离；评分/感知仍留原处 |
| Reuse / 按需 Extend | `Agent/AI/Actions/SearchResourceActionNode.cs`、`EngageEnemyActionNode.cs` | 保留单目标动作；仅补足步骤结果/容量/等待事件传递，不在动作节点嵌入图队列 |
| Reuse | `Agent/Runtime/AgentRuntimeRegistry.cs`、`AgentResourceInteractionChannel.cs` | 多 Agent、焦点和背包交互事实；不另建地图 Agent 注册表 |

`AgentClusterStepExecutor` 与 Resolver 的区分：前者决定何时提交/等待/结束当前步骤，后者只解析真实对象和候选。生命周期与 RouteController 的区分：前者管理一条当前动作及伤害恢复，后者管理整条任务序列；只以结构化事件关联，禁止互相猜测 Blackboard 状态来推进。

### 7.3 Editor 自动生成和编辑

本表相对 `Assets/Scripts/Editor/`。Editor → Runtime 单向依赖，运行代码不使用 AssetDatabase/GlobalObjectId/Undo。

| 判断 | 具体文件 | 职责、独立存在或修改理由 |
| --- | --- | --- |
| Create | `MapGraph/MapGraphSceneCollector.cs` | 场景/Prefab 实例快照、稳定身份和来源规范化采集，不生成 UI 或保存资产 |
| Create | `MapGraph/MapGraphLayoutGenerator.cs` | 世界参考布局、Zone 初始矩形/中央名称安全区/群占位及局部坐标回写数据；不兼任离散方向搜索 |
| Create | `MapGraph/MapGraphOrthogonalLayoutSolver.cs` | 固定候选边集的水平/垂直方向搜索、行列对齐、间距/Zone 包含和有界约束求解；不查询导航、不修改边集或资产 |
| Create | `MapGraph/MapGraphLayoutScore.cs` | 纯函数计算位置/方位/距离/面积/交叉等分项，统一方案比较和诊断；不决定候选边或放宽硬约束 |
| Create | `MapGraph/MapGraphConnectionGenerator.cs` | 导航候选、骨架种子、有限自动边交换、补边和断连诊断；通过布局可行性结果决定保留候选，不在固定拓扑重排模式改边 |
| Create | `MapGraph/MapGraphGenerationController.cs` | 组合连接候选、布局求解和评分，分帧/取消/预算，人工覆写合并及 Undo 事务保存；计算层不直接改资产 |
| Create | `MapGraph/MapGraphValidation.cs` | 独立校验绑定、导航/拓扑、零长度/横竖/无折点、共线遮挡/穿节点、Zone 包含/中央名称安全区和人工约束；不以求解器自报成功作验证 |
| Create | `MapGraph/MapGraphEditorWindow.cs` | 工具窗口、参数和命令编排；不承担生成算法或坐标操作状态 |
| Create | `MapGraph/MapGraphEditorCanvas.cs` | 画布坐标、行列吸附/拖动联动预览、四向端口、缩放/平移；通过 Controller 验证/应用，不能偷偷补弯或删边 |
| Extend | `MapGraphUguiPrefabFactory.cs` | 生成/更新正式地图 Prefab 的新子视图和主题绑定，保留 GUID，不覆盖已绑定人工资源 |
| Reuse | `MapGraphDefinitionConverter.cs` | 仅保留旧图转换入口；不把场景生成追加到旧 BoardGame 导入器 |

### 7.4 表现、安装和资产

本表相对项目根目录。

| 判断 | 具体文件 | 职责、独立存在或修改理由 |
| --- | --- | --- |
| Extend | `Assets/Scripts/Gameplay/MapGraph/Binding/AgentGraphProjectionController.cs` | 组合权威路线、距离采样和纯投影计算输出显示快照；取消 UI 自寻路/自行完成节点/时间推进，不能直接按世界坐标画到线外 |
| Create | `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteDistanceSampler.cs` | 只读采样当前路线锚点剩余距离，校验 Agent 导航目标身份、反击时原目标距离、有效性及查询限频/缓存；不决定显示偏移或移动角色 |
| Create | `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphAgentPositionProjection.cs` | 纯数值的方向/距离比例/基准校准/无数据保持/线段插值；输入图数据和数值状态，不引用 Agent、NavMesh 或 UI，不推进真实任务 |
| Extend | `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphRuntimeState.cs`、`MapGraphAgentRuntimeState.cs` | 只读展示缓存，增加路线段身份、距离有效性、投影基准/进度和处理/等待模式；不复用为路线权威 |
| Extend | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphOverlayController.cs` | 只承担视图对象装配和刷新；现文件已约 35 KB，状态汇总/输入/布局必须分出下列文件 |
| Create | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphPresenter.cs` | 订阅读模型、两种视图共用状态、高层节点命令和反馈，不绘制具体直线 |
| Create | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphViewport.cs` | 小/大地图切换、缩放平移及坐标换算；不执行群或重算路径 |
| Create | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphZoneView.cs` | 深色区域矩形、中心锚定的名称和长名称排版，独立于可点击群视图 |
| Create | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphAgentMarkerLayout.cs` | 保持行进标记在线上，处理群时分配稳定偏移槽位、避开名称及连线；重叠组合身份显示，替代原 Overlay 的法线车道偏移，不计算世界距离 |
| Extend | `Assets/Scripts/Gameplay/MapGraph/View/MapGraphNodeView.cs`、`MapGraphEdgeView.cs`、`MapGraphAgentView.cs` | 节点点击、统一横竖裁边端口/高亮、多 Agent 身份和状态；边只按 0°/90° 绘制，Agent 使用同一线段及处理偏移，不能任意旋转或挪离行进线 |
| Create | `Assets/Scripts/Gameplay/Raid/RaidMapCommandInstaller.cs` | 场景组合根，装配定义/Binding/成本/Agent 路线服务/Prefab，统一释放；不拥有路线状态 |
| Extend | `Assets/Scripts/Gameplay/Raid/RaidFlowController.cs`、`RaidMinimapController.cs` | 主场景安装新地图，旧地图保留为明确的历史兼容入口，新配置下不得重复启动 |
| Extend | `Assets/Scripts/Gameplay/Targets/Presentation/AgentCommandFeedbackPresenter.cs`、`AgentCommandFeedbackText.cs` | 根路线结果显示及中文原因映射，过滤路线子指令；保留旧场景低层反馈兼容 |
| Extend | `Assets/Scripts/Gameplay/Backpack/ShopScreenController.cs`、`StorageScreenController.cs`、`Assets/Scripts/Editor/ShopCanvasPrefabBuilder.cs`、`StorageCanvasPrefabBuilder.cs` | 这些文件当前按 RaidMinimapController/Canvas 名称清理地图；改为识别新安装根，防止返回商店/仓库残留或重复销毁 |
| Extend | `Assets/Prefabs/MapGraph/UI/Pfb_MapGraphOverlay.prefab`、`Pfb_MapGraphNodeView.prefab`、`Pfb_MapGraphEdgeView.prefab`、`Pfb_MapGraphAgentView.prefab` | 复用并扩展现有资产，绑定主题/点击/状态，维持 GUID |
| Create | `Assets/Prefabs/MapGraph/UI/Pfb_MapGraphZoneView.prefab` | 深色区域背景和中心对齐的名称标签，不在每次打开地图时拼装不同样式 |
| Create | `Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset`、`SO_MapGraphTheme_Raid.asset` | 当前真实场景图及深色简约主题，包含中心名称和群旁 Agent 偏移参数；保留旧 MVP 图不混写 |
| Extend | `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity` | 保存安装根、真实 Zone/群绑定和正式配置；按当前实例及 Prefab 覆写操作，保留场景已有对象 |

### 7.5 验证、诊断和文档

| 判断 | 具体文件（相对根目录） | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphGenerationTests.cs` | 生成可达性、稀疏连接、布局/增量结果及成本对照 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphPathfindingTests.cs` | P1a 的纯拓扑/成本接口、确定性路径和独立穷举对照，独立于导航和布局生成测试 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphOrthogonalLayoutTests.cs` | 正交硬约束、位置保真对照、跨 Zone 对齐、固定图冲突和搜索预算；与导航/生成控制流程测试分离 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphEditorPersistenceTests.cs` | Undo/Redo、取消、保存重载、真实绑定/Prefab 覆写 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/AgentRouteExecutionTests.cs` | 实际逐群移动/战斗/资源/撤离契约 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/AgentRouteTransitionTests.cs` | 替换/拒绝/过期回调/反击/多 Agent/容量终态 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandPresentationTests.cs` | 正式 UI 点击路由、大小图状态、截图和根提示语义 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphAgentProjectionTests.cs` | 距离映射/双向/缺数据/重规划的纯计算，真实导航和反击目标身份采样，群旁单人/多人偏移与线上重叠约束；与点击及整体截图验证分离 |
| Reuse | `Assets/Scripts/Editor/AgentReproduction/Infrastructure/ReproductionTestFixture.cs`、`World/TestNavMeshBuilder.cs`、`World/AgentFactory.cs`、`World/TargetFactory.cs`、`World/EnemyFactory.cs`、`World/InventoryFactory.cs` | 原 Play Mode/NavMesh/物理/背包夹具；新增群图测试场景由测试所有，不改正式平衡 |
| Create | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidRouteEvidence.cs` | routeId/图版本/节点序列/子指令/坐标/距离/敌人血量等只读关联，独立于驱动和最终裁决 |
| Extend | `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidCommandScenario.cs`、`SceneRaidClusterCommandDriver.cs`、`SceneRaidCommandEvidence.cs`、`Assets/Scripts/Automation/SceneRaid/SceneRaidObserver.cs` | 新路线预期及采样接入原调度，不让旧单目标脚本默认冒充路线验证 |
| Reuse | `Assets/Scripts/Automation/SceneRaid/SceneRaidInventoryDriver.cs`、`SceneRaidFrameSampler.cs`、`SceneRaidRenderEvidence.cs` | 正式背包代理、真实帧采样/渲染证据，禁止 Gameplay 反向引用 |
| Create | `tools/agent-repro/SceneRaid.Routes.Contracts.psm1`、`Test-SceneRaidRoutes.ps1`、`map-command-scenarios.json` | 以原始事件独立核对路线/步骤/终态和假通过对照；冻结新场景脚本 |
| Extend | `tools/agent-repro/cases.json`、`scene-raid-cases.json`、`SceneRaid.Report.psm1`、`Invoke-SceneRaid.ps1`、`Invoke-SceneRaidPlayer.ps1` | 登记定向组/新脚本，复用隔离环境和进程会话，不重写 Runner |
| Extend / Create | `Assets/Docs/GameplayAgentFrameworkDesign.md`、`README.md`、`outputs/map_command_validation_report.md`（新增） | 实施后更新正式架构、工程完成情况及原始测试证据链接 |
| Create（逐阶段） | `.planning/2026-09-12-map-command/pN_execution.md`、`architecture_review.md` | 小规划、实际结果、边界审查和调整记录；没有运行的阶段不得预填通过 |

## 8. 分阶段闭环

| 阶段 | 小规划/实现范围 | 阶段验收和提交边界 |
| --- | --- | --- |
| P0 场景和契约基线 | 重审计真实群/来源/出生就绪/Prefab/导航配置，核对单向通路和旧输入；输出待生成节点、锚点及边样本诊断，冻结请求/完成口径 | 确认来源不重复、全图绑定可生成；若发现必须改关键接口/方向规则，先补 Review。本阶段不先替换正式地图 |
| P1 数据和纯路径 | 图/Zone/边版本、稳定绑定、共享导航段查询、成本快照和策略接口、纯图最短路；用小图接入明确配置 | 距离而非屏幕线长决定路径；断图/高差/旧 SO 迁移/确定性通过；导航提取后定向旧空间回归通过 |
| P2 自动生成和编辑 | 世界参考布局、导航候选、无折点横竖求解、自动边交换、人工约束、窗口、Undo/保存；生成当前场景专用图资产 | 每条有效边为横/竖单段；身份/连通性/人工边保持；首版布局及每轮视觉调整后截图复核，不以铺成单行当均衡；再生成/保存重开一致 |
| P3 真实群路线 | 每 Agent RouteController、步骤执行器、根/子身份和单一伤害恢复，先用构造图经 Router 运行，不依赖 UI | 资源等待、整群接续、途经/终点撤离、失效/反击/容量/改令/双人等确定性契约通过；审查没有第二个挂起所有者 |
| P4 全入口统一 | 正式世界点击、Decision/Discovery、自主容量撤离接路线，迁移原手动锁和根反馈 | 同目标不同入口生成等价路线；来源优先级不同但状态表达一致；旧子任务回调不影响新路线；同群重复请求去重 |
| P5 地图替换和场景集成 | Presenter/Viewport/ZoneView、深色简约主题、中央名称、距离采样/线上投影/处理偏移、点击和安装清理，绑定主场景 | 每个可见状态完成后截图检查，重要样式调整前后对照；UI 序列等于执行序列，行进在线、处理偏移、中央名称可读；同时检查完整 HUD 的协调性 |
| P6 真实场景和交付 | 4× 自主/手动路线自动场景，1× 渲染性能，对发现的问题写小修复规划再复跑；补完整结果和工程文档 | 终态/背包/结算/路线证据闭合，平均帧率大于 60，明确自然覆盖缺口/剩余问题，最终架构审查后提交 |

每阶段先写实际文件和可独立运行的验证用例，不机械一次创建所有文件。各阶段完成实现、必要测试和 Review 后提交，正文采用自然中文。依赖不变的阶段内修复按既有授权自主闭环；涉及重要新边界再说明确认。不提交未验证场景为最终交付，不自动推送。

视觉检查贯穿实施：每次首次产出地图布局/样式、接入新的显示状态或明显修改视觉参数时，就捕获并实际查看截图；发现问题后调整，再截图对照。不会等 P6 才第一次检查，也不会把截图文件生成成功当成视觉通过。纯数据阶段没有新可见结果时不重复抓相同画面。

## 9. 自动验证方案

### 9.1 确定性构造矩阵

| 组 | 必须覆盖的行为和判定 |
| --- | --- |
| G01 图拓扑 | 双通路选择、断图、同成本稳定排序、禁止自环/重复/悬空边；小图枚举全部简单路径作代价/序列对照，不以同一 Dijkstra 再算一次当真值 |
| G02 空间成本 | 近点隔墙但可绕、远点实际更短、楼层重叠、坡道/桥、单向链接、Partial；断开的导航面不可自动连，远程跨高差战斗约束保留 |
| G03 自动路网 | 近邻小团间远处通路、初始 MST 无法对齐但替换自动边可解、可选补边无法对齐；成功图的连通分量须和有效可达候选一致，人工边/禁连保留；求解失败不能冒充物理断图或发布部分成功 |
| G04 布局绑定 | 大小 Zone、稀疏/密集群、楼层重叠、未分区、重复 ID、来源/活跃同节点；跨 Zone 行列对齐和中央名称安全区；保真评分对照，禁止移动世界 Transform、复制图标或虚拟群满足约束 |
| G05 编辑持久化 | 拖矩形/群的行列联动及锁冲突、换逻辑端点、轴向留白、加/删线、Undo/Redo、取消、保存重开；仅重排不得改边，生成改边有差异；世界 Transform/成本保持 |
| G06 无折点横竖 | 斜向链、四向分支、矩形环、人工固定三角连接/超过端口容量/相互冲突的锁、穿节点/共线覆盖、零长度；独立校验每条边只有两个端点且 x 或 y 相等；草稿失败保留旧图，无斜线/折点兜底 |
| G07 确定性和预算 | 固定输入/参数重复输出；保存重开缩放后的横竖仍一致；小型可行布局枚举作约束/评分对照；预算耗尽、取消、物理不可达和人工冲突分开报告，禁止无界搜索 |
| R01 顺序执行 | A 资源→B 敌人→C 终点，真实走动/血量减少/背包事件证明逐群处理；地图序列和步骤 ID 可关联；不能只断言队列游标增加 |
| R02 清群 | 多敌人逐个完成、同伴击杀最后敌人、最近敌人暂不可执行、来源注册延迟、独立活跃群；不因子 Engage 完成或短暂空列表提前推进 |
| R03 资源 | 正式等待、程序开关背包/取物、部分余物、容量不足转撤离、两个 Agent 同箱会话和停靠、等待被伤害打断恢复；物品数量/归属守恒 |
| R04 替换和反击 | 路线 A→合法 B/非法 B、反击中改令、持续伤害保持有效目标、旧完成回调晚到、规划结果乱序、等待期间改令、死亡/撤离/卸载终结 |
| R05 撤离和通行 | 中间撤离不离场，最终撤离才计时；已完成群保留通路；不可达撤离拒绝/有限失败；反击后恢复原有效撤离，库存结算只发生一次 |
| R06 自主统一 | Decision 和 Discovery 均有根路线，手动锁跨步骤不被自动覆盖；路线完成恢复自主，容量事件不被玩家锁卡住；图可见不使射线/感知约束失效 |
| U01 界面 | 直接调用真实节点点击 Handler/UGUI 事件验证焦点/AgentId/一击一令，无需鼠标坐标；M/小图同源，根成功提示时机、失败原因、子步骤不刷屏 |
| U02 真实显示 | 捕获深色简约正式 Prefab 的全图/紧凑图、居中 Zone 名称、双 Agent/重叠路径、反击/等待/完成截图；核对 routeId/目标/顺序、名称中心坐标及安全区，Agent 检查实际可读性 |
| U03 距离映射 | 剩余 100%/50%/接近到达、反向边、后退、起步基准冻结、路径版本校准、零长度/容差/NaN/Infinity/pathPending；断言核心标记在线且比例符合独立数值样例，时间流逝不能自行推进 |
| U04 处理及重叠 | 单 Agent 处理也和群图标错开，多 Agent 槽位稳定且避开 Zone 名称；行进同向/对向重叠使用组合身份但位置仍在线，入图前/终态有明确模式；UI 状态不推进真实步骤 |
| U05 反击距离身份 | 行进中反击使 NavMesh destination 换成敌人，仍采样原群锚点距离；无原目标有效路径则保留线上位置；处理中反击仍停原群旁，恢复后身份/边方向/游标正确 |
| H01 验证器 | 缺事件、错 Agent/根 ID、跳节点、子完成伪装群完成、缺渲染、日志截断/超时、预期战死样本；报告器不能将这些混成通过 |

逻辑时序用 4× 真实 Play Mode；仅将受倍速影响的恢复/等待/快速改令关键用例补 1× 对照。图算法/Editor 序列化不无意义地重复倍速。旧反击、空间射击、导航、资源容量和反馈测试按受影响组选择；本轮已确认改为整群清除的旧单敌人断言显式迁移并保留规则变更说明，不删除红灯来掩盖缺陷。

### 9.2 Scenezl_Final 1 场景脚本

新脚本拥有自己的路网版本和场景哈希，不沿用旧 MC 脚本结果冒充新功能已通过。逻辑测试不需要模拟鼠标，直接调正式路线入口；背包操作复用测试驱动。

| 脚本 | 行为 | 收尾 |
| --- | --- | --- |
| MR01 自主路线 | 不发玩家目标，观察两名 Agent 自主路线、经过资源/敌人/撤离，测试驱动操作背包 | 正常死亡或撤离，库存/结算及路线终态契约 |
| MR02 远近指挥 | 按真实状态前提给焦点 Agent 指定近/远群、跨 Zone 路线，另一 Agent 保持自主；有限次数改令后停止 | 继续观察到正式 Raid 终态；未触发前提记 PARTIAL |
| MR03 编辑生效 | 固定可验证起终点，复制图配置删除一条关键边/增加一条合法边，对照路径和实际经过群；不污染正式资产 | 构造确定性通过，真实场景可到达范围内补证；不强求随机战斗局所有节点存活完成 |
| MR04 表现性能 | 正常 1×，真实渲染小图/放大图和双人路线，包含规划/等待/战斗状态，冻结窗口/图形设置 | 平均帧率大于 60，真实 frames/time/画面证据；战死只影响自然覆盖长度，不归类执行 bug |

事件探针含 scene/graph/nav/cost 版本、AgentId、根/子请求 ID、起终点/剩余节点/游标、状态变更原因、实际 Transform、NavMesh destination/status/remainingDistance/速度、当前节点锚点距离、敌人身份/血量/存活数、背包会话/容量及最终库存。显示探针另记当前边及方向、采样锚点/有效性、d0/d/r/t、校准路径版本、线上坐标、处理偏移槽位和查询次数，区分导航问题和投影问题。事件即时记录，普通位置快照低频，避免每帧打印把诊断变成瓶颈。

保留 Editor 会话和 Library 缓存，使用现有隔离副本/请求文件流程；渲染验证沿用已允许的可见测试 Player，用户无需操作。所有构造/整局结果保留源哈希、原始 NUnit/事件/帧数据和失败证据。

### 9.3 性能与复杂度验收

| 路径 | 成本控制及检查 |
| --- | --- |
| Editor 全对校验 | O(N² × 单次导航查询成本)，当前 N=28 的群对上界明确；路径查询成本取决于 NavMesh 多边形规模，不能宣传为常数。缓存、分帧、取消是主要控制 |
| 生成算法 | 候选排序约 O(N² log N)，初始森林 O(E log E) 仅是其中一部分；横竖方向/自动边交换存在组合搜索，不能沿用 MST 复杂度代表全流程。记录展开候选数、每候选迭代、约束校验和评分耗时，硬上限控制；简单几何对照校验约 O(V²+VE+E²)，不在 Update 执行生成 |
| 运行时规划 | 现 Dijkstra O(V²+E)，查询触发有界；导航补验独立计时/次数记录，不能混入纯图算法时间 |
| 状态和 UI | 事件更新成员/路线状态，静态节点/线不逐帧重建；位置刷新可按 20 Hz 复用缓冲，已有有效导航距离直接读取，额外只读路径查询独立限频/计数，不按 UI 刷新频率查询全图；缺数据不时间外推 |
| 分配和帧率 | 统计空闲/规划/地图打开三种状态的分配、查询和对象数量，不新增每帧全场 FindObjectsOfType、LINQ 列表或重复材质；最终硬门槛为 1× 真实渲染平均 >60 FPS，不追加未确认的 p99 门槛 |

### 9.4 持续截图和视觉复核

这是用户明确要求的实施闭环，编码 Agent 自行执行，不要求用户截图、操作游戏或逐张批准。复用正式渲染/截图设施和 `MapCommandPresentationTests.cs`，不引入新的 Gameplay 截图系统。

| 检查节点 | 截取内容 | 重点判断 |
| --- | --- | --- |
| P2 初次生成及布局调整后 | Editor 布局全图、密集区域、跨 Zone 连接；运行视图可用后补正式画面 | 方位/远近关系自然，横竖线清楚，区域比例及留白合适，中心名称和群图标不拥挤 |
| P5 静态主题首次可见 | 实际小地图、大地图、含小地图的完整游戏 HUD | 深色简约是否统一；小地图尺寸、明暗、字体和图标是否与游戏画面协调；不能只检查放大的裁剪图 |
| P5 每种状态首次接通 | 线上行进的开始/中段/接近终点、群旁处理、等待背包、反击、双 Agent 重叠、接受/失败提示 | 位置映射直观、标记在线、群旁偏移适量、身份可辨、提示和 Zone 名称无遮挡；移动相关问题采用连续几帧对照 |
| 修改布局/字号/颜色/线宽/偏移后 | 同一状态及相同尺寸的修改前后画面 | 修正当前问题，检查是否引入新的遮挡或风格割裂 |
| P6 实际场景回归 | 正式小/大地图和 HUD，选取真实搜打撤中已发生的关键状态 | 构造截图中的效果能在 Scenezl_Final 1 正式场景成立；未发生的状态仍由确定性构造补证 |

每轮保留原始全画面及必要局部图，关联源码/图版本、阶段、运行 ID、分辨率、UI 缩放、Agent 状态和截图时刻。原始证据放在对应 `Logs/AgentReproduction/<run-id>/` 或 `Logs/SceneRaid/<run-id>/`，阶段评审选图归档到 `outputs/map-command/visual/<stage>/<iteration>/`，在 `pN_execution.md` 写明所见问题、调整和复核结论，最终报告链接代表性图片。

图片由 Agent 实际打开查看，程序同时核对横竖、边上位置、中心对齐和包围盒遮挡。静态截图不能单独证明距离随移动更新，所以运动相关状态还需对应的连续快照/行为测试。截图采集开销单独记录，不把截图卡顿混入用于游戏帧率验收的稳定采样窗口；完整原始记录保留。

## 10. 风险、取舍和 Review 决策

| 风险/歧义 | 本方案选择 | 替代方案及代价 |
| --- | --- | --- |
| 稀疏图影响经过哪些群 | 导航候选、横竖布局联合选择骨架/补边；保存后作者连线决定任务连通性 | 先冻结任意 MST 再排图，可能找不到无折点布局；全连接既难画也削弱途中处理群 |
| 横竖单段和地理位置冲突 | 横竖、无折点及有效连通性为硬约束，方位/距离/紧凑度分项优化；允许压平部分斜向差异，优先不反转主要方位 | 精确固定每个点的位置通常只能画斜线或折线，不满足本次要求；本期不提供这两种自动兜底 |
| 人工固定图/锁导致无解或难解 | 保留旧图/失败草稿，定位冲突或报告预算耗尽；普通拖动不动拓扑，联合生成只交换未锁自动边 | 悄悄删人工边、引入假拐点、穿过节点或断开桥边会改变玩法及作者意图 |
| 真实距离无法预测全部战斗/搜箱位移 | 用合法锚点间 NavMesh 路程，执行中重验，展示预计路程 | 精确任务运动优化需要把成员顺序/战斗/容量建模，本期不扩张到该系统 |
| 双向图和单向导航 | 首版只接真正双向连接，单向依赖在 P0 报告并追加设计 | 直接扩成有向图会影响搜索、端口编辑、箭头样式和旧资产；不能无声兼容 |
| 动态来源短暂没有成员 | 区分出生就绪和清空事实，使用原出生所有者 | 按空列表立即完成会跳过敌群；固定等几秒又会受到 4×/暂停影响 |
| 两套执行/恢复事实 | 路线管理序列，旧生命周期管理唯一活动/挂起子指令 | 在地图投影器里发命令或重新保存挂起任务会产生竞争和旧令复活 |
| 作者编辑被生成覆盖 | 数据记录人工来源、禁连和视觉锁，增量合并 | 每次全量重建操作简单，但违背低人工成本目标 |
| 旧地图迁移漏清理 | 明确修改 Raid、Shop、Storage 和 Prefab Builder 的地图安装/清理 | 只替换显示会留下旧 Canvas、单体扫描和重复输入 |
| 反击距离/多人排布导致错位 | 采样当前路线锚点，行进标记保持在线，处理中使用群旁槽位；数据缺失暂停显示进度 | 直接读临时敌人的 remainingDistance、匀速兜底或法线车道偏移都会违反新的定位要求 |
| Zone 中央名称挤占布局 | 名称中心固定，布局预留文字安全区，必要时放大未锁矩形/提示冲突；处理中槽位同样避让 | 移去角落、遮掉文字或压低到不可读字号不符合居中显示要求 |
| 现有测试语义落后 | 给新路线脚本独立版本，显式更新群完成预期，保留低层指令回归 | 只跑旧单目标测试无法证明逐群路线实现 |

本轮供确认的关键架构决定是：**扩展现有 MapGraph；导航候选和无折点横竖布局联合求解，平衡方位/距离保真；布局和世界导航分离；Agent/Routes 持有真实序列，原 DirectiveLifecycle 持有唯一伤害恢复；所有正式目标入口迁移到高层路线；地图只读执行事实；编辑器与自动验证各有独立职责。** 第 2.3 节列出首版默认行为，一同 Review。横竖单段是用户新增硬要求，具体求解、四向端口、联动编辑和评分方法仍是待 Review 的实现方案。

## 11. 实现结果及架构审查记录

- P0 静态基线完成：`Logs/SceneRaid/20260912-191826-063` SC00 证据 PASS，Editor 保留、源输入未变；当前 28 群沿用 3 组重复 TargetId，生成器需稳定场景身份，详见 `p0_execution.md`、`scene_baseline.md` 和 `architecture_review.md`。锚点/群间真实路径验证归入 P1/P2，未生成正式场景图或验证新功能。
- P1a 纯图成本、只读索引和确定性最短路完成：`Logs/AgentReproduction/20260912-193242-122` 10/10 PASS，包含 500 个起终点组合的独立穷举对照；详见 `p1_execution.md`。后续图数据/导航/生成/真实路线/显示仍待实现，不能用本组成绩替代整个地图验收。
- P1b 共用导航查询完成：新构造 9/9、原导航 17/17、真实场景/高处接近兼容 10/10 PASS。P1c 序列化契约完成：保存重读/坐标/人工约束/失败方向/旧图 8/8，受影响的纯图回归 10/10 PASS。证据和具体文件见 `p1_execution.md`，真实全图连接/逐群执行/UI 尚待后续阶段。
- 已做规划自查：复用已有图/指令/背包/验证设施；列明具体文件；无 Gameplay → Automation/Editor 依赖；不让大 Overlay、PawnRoot 或动作节点承担独立算法；人工生成意图、真实路线身份和唯一伤害恢复都有明确所有者。
- 本轮布局修订仅改文档。重读边视图和节点数据后确认现有任意角度 Image 旋转不足以保证横竖；新增求解器、评分和约束数据的文件归属，补齐固定图冲突、人工锁、跨区对齐和渲染验收，尚未运行求解性能实验。
- 本轮显示修订仅改文档：替换先前线外反击投影建议，确认距离映射到线上、处理时群旁偏移、深色简约主题和中央 Zone 名称；补充原目标距离采样、纯投影、显示排布的文件边界及 U03–U05 验证。尚未制作新 UI 资产或运行这些用例。
- 实现后在各 `pN_execution.md` 回写差异和证据，`architecture_review.md` 逐阶段检查重复逻辑、依赖方向、文件职责、公共接口和计划一致性，最终汇总到验收报告。

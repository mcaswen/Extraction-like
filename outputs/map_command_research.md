# 地图指挥功能：源码调研和需求确认

日期：2026-09-12。调研基线 `fcb6df1`。需求及架构已确认，完整方案见[地图指挥大规划](../.planning/2026-09-12-map-command/task_plan.md)。P0 已复用 SC00 重新完成当前静态场景审计，见[场景基线](../.planning/2026-09-12-map-command/scene_baseline.md)；尚未修改生产代码/场景或运行新地图功能。

## 1. 现有能力及接入缺口

| 现有文件 | 已有能力 | 本需求的缺口 |
| --- | --- | --- |
| `Assets/Scripts/Gameplay/Targets/Authoring/TargetZoneAuthoring.cs` | 群列表、区域轮廓、中心和完成聚合 | 没有地图矩形、布局编辑或区间通路数据 |
| `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs` | Zone 归属、范围点、中心、稳定目标 ID | 没有地图节点位置、连接或路线阶段状态 |
| `Assets/Scripts/Gameplay/MapGraph/Config/SO_MapGraphDefinition.cs`、`MapGraphNodeDefinition.cs`、`MapGraphEdgeDefinition.cs` | SO 节点/边、二维坐标、图标、双向连接和边长 | 尚无 Zone 分组矩形、从实际场景生成图、端点样式覆写 |
| `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphPathfindingService.cs` | Dijkstra 最短节点路径，按配置边长计费 | 不检查真实 NavMesh，不执行群任务；当前实现为线性取最小点，约 O(V²+E) |
| `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphBindingAuthoring.cs`、`MapGraphTargetBinding.cs` | 图节点关联真实目标引用或目标 ID、世界位置查询 | 需生成当前场景绑定、规范来源群和活跃敌人群的同一节点身份 |
| `Assets/Scripts/Gameplay/MapGraph/Binding/AgentGraphProjectionController.cs` | 从全部已注册 Agent 的指令/NavMesh 推断目标，显示图上路径和位置 | 仅投影，明确不驱动真实 Agent；距离推断/匀速兜底不能作为任务完成依据 |
| `Assets/Scripts/Gameplay/MapGraph/View/MapGraphOverlayController.cs`、`MapGraphNodeView.cs`、`MapGraphEdgeView.cs`、`MapGraphAgentView.cs` | UGUI/TMP 节点图标、直线、目标/占用高亮、多 Agent 布局 | 没有点击节点下达正式指令、Zone 背景矩形或同一真实路线状态的读取 |
| `Assets/Scripts/Editor/MapGraphDefinitionConverter.cs`、`MapGraphUguiPrefabFactory.cs` | 旧 BoardGame 配置转换、生成 UGUI Prefab | 不是从 Scenezl 场景生成图，没有可视化布局/连线编辑器 |
| `Assets/Scripts/Gameplay/Raid/RaidMinimapController.cs` | 当前运行时小地图和 M 全图 | 显示单体敌人、箱子、传送点、撤离点；按世界 XZ 投影，单 Player 标记，未接图指挥 |

现有 `Assets/SO/MapGraph/SO_MapGraphDefinition_MVP_Graph.asset` 是 **25 节点、31 条边**的旧 MVP 图。`Assets/Prefabs/MapGraph/UI/` 有节点、边、Agent 和 Overlay 四份正式 Prefab。已核对当前主场景不存在 Overlay、Binding 脚本或 Overlay Prefab 的直接 GUID 引用；README 也记录主场景尚未绑定该 Overlay。

还有 `Targets/Input/UIMap.cs`（实际类名 RoomUIManager）、`AIIntentController.cs` 和 `DB/TargetCluster.cs` 的旧命令链，直接 SetDestination。它使用旧 TargetCluster，不应当作新正式路线执行入口。当前正式鼠标指令走 PlayerInputManager/Dispatcher。

## 2. 当前场景与语义风险

复用最新已有运行前审计 `Logs/SceneRaid/20260912-165307-641/scene-audit.json`：8 个 Zone，28 个静态群（12 资源、14 敌人来源、2 撤离），2 名 Agent，32 个箱子。这个数字是该批次静态审计口径，不是运行时活跃敌人群数量，也不是本次重新运行的结果。

敌人来源群 `EnemySourceClusterAuthoring.cs` 能在运行时创建 `ActiveEnemyClusterAuthoring`，并按来源 ID 注册敌人。地图需要把来源位置和对应活跃成员映射为同一个逻辑敌人节点；不能把来源群、活跃群重复画成两个任务，也不能依赖运行时新建群的随机 ID 保存布局。已有 MapGraph 绑定/投影中的来源解析可复用。

`GameplayTargetRegistry.cs` 会在 Play Mode 遇到重复 TargetId 时重新生成实例 ID。编辑器生成绑定应优先保存实际场景对象引用及稳定节点身份，ID 校验属于工具职责，不要求用户手工对照字符串。

当前正式链路：

```text
PlayerInputManager → AgentTargetCommandDispatcher → TargetClusterDirectiveFactory
    → IAgentCommandReceiver → AgentDirectiveLifecycleController
    → Search / Engage / Extract → 导航、战斗、背包
```

- 资源指令绑定资源群，已有逐成员搜索；`SearchResourceActionNode.SearchLootBox` 仍会等待玩家背包会话。此前自动取物属于 `Automation/SceneRaid` 的验证驱动，不能直接变成 Gameplay 对测试代码的依赖。
- 敌人群当前产生的是针对一个具体敌人的 Engage。要执行“整个群处理完成”，必须明确增补群内接续的任务语义。
- 撤离指令绑定具体撤离点；如果把撤离当作普通途经节点就执行，会在路线终点前离场。因此建议仅选为终点时执行撤离。
- 生命周期持有一个活动指令和一个受击挂起任务，能在反击结束后恢复原玩家目标；尚未持有整条群路线。
- 自主目标入口在 `Agent/Decision/AgentTargetDecisionController.cs`、`Agent/Runtime/AgentTargetDiscoveryController.cs`，直接提交单目标指令。当前玩家锁期间不会生成一个可与玩家路线竞争的新自主目标。

## 3. 建议的模块方向（待确认）

建议基于已有 MapGraph 扩展，而不是把这些职责全部塞进 RaidMinimapController。

1. **地图数据和编辑器**：Zone 矩形、Cluster 节点、逻辑连接、视觉位置/样式分别表达。复用 MapGraph SO、绑定和直线视图；新增职责应放在相应 Config、Binding、Editor 边界，完整文件规划在需求确认后提交。
2. **自动生成**：以场景 XZ 方位和现有范围为参考，联合选择导航可达的候选边及水平/垂直对齐布局，矩形尺寸受群数量/图标间距约束。每边是横竖单直段，方位/相对远近尽量保留，不以 L 形折线或虚拟群兜底。只看中心距离不能区分墙体、断层、跨高低差道路。
3. **路线计算和执行**：上层产出群节点序列；每个 Agent 独立持有路线和阶段，调用既有单目标执行链。子指令完成不等于整个群完成，整个群完成也不等于整条路线结束。
4. **地图展示**：读取真实路线、当前步骤、目标来源和挂起状态。按到当前群目标的距离将行进 Agent 定位在线上，处理群时在群图标旁偏移，不再按自由世界坐标画到线外；UI 不另算路线，也不能沿用“缺数据就匀速前进”的兜底。

地图布局坐标与世界位置分离：编辑器拖动矩形/图标只改显示布局。边的显示长度也不应因美术拖动而改变实际路程成本。自动生成结果应支持锁定人工编辑、Undo/Redo、保存重开和增量同步；不在运行时随着敌人移动或群完成重新排列整个地图。

视觉要求已确认：**深色简约风格，Zone 名称显示在矩形中央。** 紧凑/放大地图共用图标、字体、颜色和线宽；深灰底、低对比区域、细横竖线和有限强调色。群按资源/敌人/撤离区分，Agent 使用稳定身份颜色，行进标记在线、处理时与群图标错开。中央名称预留安全区；多人行进不能用法线车道偏移挪到线外。具体距离度量建议采用当前群锚点剩余导航路程，见大规划第 6 节。

## 4. 需要用户决定的八项

| # | 问题 | 建议选项 | 影响 |
| --- | --- | --- | --- |
| 1 | 编辑器加删线是否改变可规划的群连接，还是只影响显示？ | 线表示逻辑可通行连接，NavMesh 负责实际绕障碍 | 决定图配置是否是路线的权威数据 |
| 2 | 自主目标是否也生成并执行群路线？ | 玩家指令、自主目标都走路线计算 | 决定统一入口的范围，不能只改鼠标入口 |
| 3 | 经过敌人群，何时算处理完成？ | 清掉整群存活敌人后继续 | 决定群级任务终态，区别于现有单敌人 Engage |
| 4 | 经过资源群，谁负责背包取物？ | 将自主搜刮取物正式加入游戏 | 其他选项：等玩家操作；只搜索揭示、保留物品后继续 |
| 5 | 多条路线如何取舍？ | 优先实际路程最短 | 其他选项：低风险；资源收益优先 |
| 6 | 地图是否开局全可见、允许向未发现群下令？ | 全图可见、可下令 | 其他选项：只公开 Zone；随探索揭示 |
| 7 | 自动/玩家目标“同层级”是否只指统一表示？ | 统一表示，玩家路线仍优先，反击后恢复 | 若指同优先级竞争，将改变已确认的玩家指令锁规则 |
| 8 | 新地图与现有小地图是什么关系？ | 替换小地图和 M 全图，使用同一套数据/布局 | 其他选项：另加面板；只保留常驻指挥地图 |

已收到的确认（以此表为准，上表保留初次提问时的建议）：

| # | 用户确认 | 对后续设计的约束 |
| --- | --- | --- |
| 1 | 连线决定可规划的群连接 | 逻辑边增删必须影响路线计算；图标拖动只改变地图布局，实际移动仍走 NavMesh |
| 2 | 玩家指令、自主目标都生成路线 | 鼠标、地图及自主选择共用路线入口；目标来源保留，不由 UI 决定执行 |
| 3 | 清掉整群后继续 | 敌人群任务保持到整群完成，子 Engage 的完成只触发下一个成员或群完成判定 |
| 4 | 到箱子旁等待玩家操作背包 | 不将自动取物加入正式玩法；地图明确显示等待状态、Agent 和对应箱子。自动测试仍可代替玩家操作背包 |
| 5 | 先实际路程最短，预留接口，之后可能接计算模型 | 路径搜索与成本计算分离；首版成本来自真实导航路程，后续模型接入不要求重写地图显示/执行层 |
| 6 | 全图可见 | 开局显示全部 Zone 和群，允许指定尚未被 Agent 发现的群；不因此取消战斗中的射线、距离和墙体约束 |
| 7 | 统一表示，玩家路线仍优先 | 所有 Agent 的目标采用相同节点/路线语义，保留玩家指令优先和反击后恢复剩余路线 |
| 8 | 替换现有小地图和 M 地图 | 紧凑/放大视图共享同一份地图布局、路线状态和视觉配置，不保留两套相互割裂的地图 |

补充确认：**群间连接先通过算法自动生成，减少人工画线成本；生成的边必须水平或垂直，不用折线，同时尽量保留位置关系。** 横竖单段是硬约束，方位、远近和紧凑度作为平衡目标；不是允许少量斜线。

补充确认：**Agent 行进时在线上显示，位置映射到目标点的距离；正在处理目标群时与群图标稍微错开；地图采用深色简约风格，Zone 名称居中。** 对照现有源码：`AgentGraphProjectionController.TryAdvanceGraphMovementByWorldDistance` 已有平面距离比例，但会推断图上到达、缺数据按时间推进；`MapGraphOverlayController.ResolveEdgeLaneOffset` 会把多人沿边法线偏移。这些原显示规则需要按本次要求修订，不能原样复用当成已完成。

据此修订方案：先验证真实导航候选，以 MST 等作为种子，联合选择可对齐的连接和布局，不先固定一张任意图后强拉横竖线。仅在显式重新生成连接时有限交换未锁定自动边，人工边/删线记录/位置锁保留；普通拖动只联动行列位置，不改拓扑。无可用结果时报告布局冲突或搜索预算耗尽，不补弯、不放假群、不冒充物理断图。详见大规划 4.2–4.5、7.3 和 G03–G07；具体四向端口、求解参数和评分方法仍待架构 Review。

以下可作为下一轮规划的默认草案：点击给当前焦点 Agent 下令，地图展示全部 Agent；新合法终点替换该 Agent 剩余路线，拒绝则保留旧路线；反击后继续剩余路线，战死按正常终态处理；非撤离终点处理完后返回自主行为；已完成群保留为可通行节点，是否仍需停靠由任务状态决定；遇到失效群或断路时重新规划，无法继续则反馈失败；满包继续沿用自主撤离、保留余物。路线编辑端点绑定真实节点，视觉留白沿横竖轴调整，不能独立拖端点制造斜线或改变世界导航点。

## 5. 后续验证边界

需求确认后写完整大规划，再拆小阶段：图数据和绑定 → 自动布局/编辑器 → 群路线执行 → 全入口/多 Agent 展示 → 真实场景验证。构造应覆盖断图/墙体/高低差、编辑后路径一致性、整群完成、受击恢复剩余路线、途中改令、多 Agent 共享群和背包容量、保存重开后绑定不丢失。逻辑验证沿用 Agent 自行执行的 4× Play Mode，性能保留 1× 平均帧率大于 60 的口径；不要求用户自己操作测试。

用户补充要求：**实施过程中持续截图并检查小地图是否符合预期、是否自然。** 初次生成、主题接入、新状态及明显样式调整后，由 Agent 自行捕获/打开图片，检查小地图、大地图及完整 HUD；发现问题后修改、复拍，对照记录。截图和复核结论随阶段归档，不延后到最终验收才检查，详见大规划第 9.4 节。

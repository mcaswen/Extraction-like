# P5 地图表现和正式 HUD

状态：**P5 已完成，进入 P6 真实整局、动态场景补证和性能验收。** 本阶段按已确认的大规划实施，无需用户逐步操作。

## P5a 小规划：真实路线和距离投影

现有 `AgentGraphProjectionController.cs` 从黑板猜终点，自行图搜索和推进路径，距离失败后按 2.6 图单位/秒前进；`MapGraphOverlayController.cs` 还会把同行角色移到线外。它们不能作为真实执行位置。已阅读 Routes 快照/Controller/Step、NavMesh 查询和 Motor、MapGraph Runtime/View、正式 UI 工厂。

- Create `Assets/Scripts/Gameplay/MapGraph/Runtime/MapGraphAgentPositionProjection.cs`：独立纯数值进度状态，冻结起步距离、到达容差、同边校准、方向反转、无效距离保持。无 Agent/NavMesh/UI 依赖，不推进节点。
- Create `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteDistanceSampler.cs`：每 Agent 的只读距离采样与原生路径缓冲。有效活动步骤/导航目的地都指向当前群锚点才借用 remainingDistance；反击及路径不匹配时最多每 0.25 秒查询一次原锚点。pathPending、Partial、NaN/Infinity 等显式无效，不用敌人距离或时间兜底。保留采样来源、锚点、有效性和修订用于诊断。
- Create `MapGraph/Binding/MapGraphRouteDistanceSample.cs`：独立的只读距离事实值，包含步骤身份/锚点/有效性/来源/容差/采样修订，供投影和自动诊断使用；不包含查询缓冲或推进逻辑。
- Extend `MapGraph/Runtime/MapGraphAgentRuntimeState.cs`、`MapGraphRuntimeState.cs`：保存只读展示缓存，根/版本/游标、当前群/边方向、剩余序列、进入/行进/处理/等待/反击/终态及距离基准。状态写入归投影器，显示不能自行消费节点列表。
- Rewrite/Extend `MapGraph/Binding/AgentGraphProjectionController.cs`：20 Hz 从 Registry 和 RouteSnapshot 获取实际状态，组合 sampler/纯计算，去掉旧 UI 寻路、猜终点和时间推进。增删 Agent 清理所属缓存，UI 重绘不另发导航查询。
- Extend `Agent/Routes/AgentRouteState.cs`、`AgentRouteSnapshot.cs`、`AgentRouteController.cs`：在当前真实边上改令/重规划时保留入图来源边身份，来自规划工作已有的两个合法入图端点，不猜最近边。首节点移动仍为实际执行步骤，来源端点不追加为要处理的群；再次改令保留这两个真实候选。快照供表现识别折返，成本失效仍检查该实际入口边，不能让 UI 另存一套“当前执行边”。
- Create `Editor/AgentReproduction/Tests/MapGraphPositionProjectionTests.cs`、`MapGraphRouteProjectionTests.cs`：独立数值样例覆盖正反向、退后、起步冻结、无效/恢复、零距离、同边版本变化；真实移动/反击/处理/入图折返、采样预算、状态序列只读。受影响的根路线定向回归。

边的绘制端点由后续 View 统一裁边计算，Agent 以同一端点和进度插值；不能一套端点画线、另一套端点摆角色。纯投影阶段无新视觉，待正式视图首次可见立即截图。

## P5b 小规划：主题和视图

- Create `MapGraph/Config/SO_MapGraphTheme.cs`、正式 `Assets/SO/MapGraph/SO_MapGraphTheme_Raid.asset`：深灰底、细边框/连接、有限强调色，统一字体/字号、图标/处理偏移。复用修正的 `Assets/Font/text-c.ttf`，为 TMP 标签生成对应字体资产，避免默认 LiberationSans 缺中文。新增字体资产由 Editor 工厂维护，不运行时改源 TTF。
- Create `MapGraph/View/MapGraphZoneView.cs`：矩形及几何中心名称；名字安全区同源。Extend `MapGraphNodeView.cs`、`MapGraphEdgeView.cs`、`MapGraphAgentView.cs`：缓存引用和静态布局，节点真实 UGUI 点击；边仅水平/垂直，处理全部状态。避免当前每次 Refresh 都重置布局/SetSiblingIndex。
- Create `MapGraph/View/MapGraphAgentMarkerLayout.cs`：屏幕空间稳定处理槽位，避开名称/线，单人也偏移。行进核心始终在线，重叠使用组合身份，不做法线车道。
- Extend `MapGraphOverlayController.cs`：只装配和刷新子视图，将输入、状态汇总和视口行为拆给 Presenter/Viewport。保留原 Prefab GUID，创建 `Pfb_MapGraphZoneView.prefab`，通过 `Editor/MapGraphUguiPrefabFactory.cs` 明确更新正式资产。
- 定向验证静态横竖、端点/Marker 同源、中心名字、处理偏移/重叠和视图对象不逐帧重建；捕获正式紧凑图、全图和 HUD，实际打开查看后调整。

## P5c 小规划：Presenter、视口和正式安装

- Create `MapGraph/View/MapGraphPresenter.cs`：两种尺寸共用执行状态和节点 Handler，向 Router 提交玩家根，所有 Agent 目标同层显示，焦点不改变路线身份。
- Create `MapGraph/View/MapGraphViewport.cs`：常驻右上紧凑全图、M 放大、缩放/平移、坐标转换和 UI 射线拦截，不移动 Agent/修改图。
- Extend `Raid/RaidMapCommandInstaller.cs` 组合正式 UI Prefab；`RaidFlowController.cs`/`RaidMinimapController.cs` 在正式绑定模式不再启动旧地图。`Backpack/ShopScreenController.cs`、`StorageScreenController.cs`、`Editor/ShopCanvasPrefabBuilder.cs`、`StorageCanvasPrefabBuilder.cs` 清理新显示根，避免残留和重复输入。
- 更新正式图资产的运行时导航签名。场景和 NavMesh 的用户修改继续隔离，仅提交本阶段必要的精确资产增量。
- Create `Editor/AgentReproduction/Tests/MapCommandPresentationTests.cs`：真实 UGUI Handler 一击一根、焦点/M/紧凑同源、UI 路线序列等于执行序列、图标在线/处理偏移、状态与截图对应。先查看静态主题，再按首次出现的行进/等待/反击/多人状态持续截取、核对。

## 验收边界

P5 通过需要代码/真实 Prefab/正式 HUD 都成立，纯算法测试不能代替截图，截图不能代替距离随真实运动变化。P6 另做实际场景整局、MR 证据和 1× 渲染平均 >60 FPS。所有小步记录实现结果、审查和提交，不以当前通过的部分提前宣布整个大规划完成。

## P5a 实现和验收结果（2026-09-13）

已用根快照替换旧投影器：删除黑板猜终点、展示层图搜索、按时间兜底和 UI 消费游标；20 Hz 只读刷新，额外距离查询最多每 Agent 每 0.25 秒一次。借用原生路径前核对步骤上下文及实际导航采样终点。反击查询原群锚点，不修改敌人路径。无效距离保持进度；恢复时冻结新的基准，容差随基准冻结。处理状态退出边态，注销清理投影缓存。

根路线新增 EntryFromNodeId，来自当前真实边的两个合法入图端点。中途折返及连续改令保留入口边，来源端点不追加为待处理节点；剩余成本校验包含当前入口边。

| 验证 | 结果 | 原始证据 |
| --- | --- | --- |
| 距离纯数值、容差冻结、退后、反向、版本校准、无效恢复、零值 | 5/5 | `Logs/AgentReproduction/20260913-044806-420` |
| 真实静止/继续、原生距离零新增查询、反击原锚点预算、连续折返、导航失效/处理/注销 | 5/5 | `Logs/AgentReproduction/20260913-050253-574` |
| 原根路线定向回归 | 18/18 | `Logs/AgentReproduction/20260913-050513-163` |

初轮 `045025-542` 暴露原始锚点与 NavMesh 采样终点高度不一致，导致可借用路径未被识别，已修正。其余静止失败为测试写入 isStopped/黑板速度后被正式 Motor/Pawn 回写；`050001-824` 探针证明世界位置仍移动、native speed=4。最终调整测试私有克隆配置，等待实际速度为零再捕获基准，不放宽断言，不修改正式配置。`045344-997` 为测试缺 SetValue 时间戳的编译失败，已记录并修复。最终各 runner 正常退出、原始输入 SHA 未变化。

本步尚未安装新 HUD，不把投影测试作为视觉验收。下一步根据实际 Prefab 渲染截图核对。

## P5b/P5c 文件边界补充

- Create `Assets/Scripts/Gameplay/MapGraph/View/MapGraphSymbolGraphic.cs`：UGUI 矢量几何图标、环和矩形边框。独立维护网格生成，Node/Agent/Zone 不各自重复图形代码，不引入世界材质或位图生成依赖。
- Presenter 内的 NodeState 仅为已汇总的显示缓存，不作为执行状态；Overlay 消费它，节点事实由共享 Environment 的 Resolver 提供。View 不自行导航、推进或自动取物。
- `MapGraphUguiPrefabFactory.cs` 同时生成保留 GUID 的 Overlay 子 Prefab、Zone Prefab、正式 Resources HUD 根和专用 TMP 字体资产。P5b 与 P5c 的代码需共同编译，但分别验证几何/排布、真实点击/安装/截图，再记录本阶段结果。

## P5b 构造中发现的多人锚点问题（已修复）

`Logs/AgentReproduction/20260913-052245-614`：两名真实 Agent（保留正式 radius/scale/avoidance）同时接受 n0→n1→n2，在 n0 两侧距离约 1.5 米对称停住，根均为 Accepted/Step0/Travelling，未死、未失败。不是地图进度错误。计划先做不同原生 avoidancePriority 对照，确认是否为对称 RVO；之后在 Navigation/场景组合边界修复和回归，不给 UI 放宽到达判定，不禁用正式避让，不用测试修改掩盖产品问题。首轮原始用例和位置/根状态探针保留，整个大规划完成前必须关闭此项。

对照 `052705-167` 将测试两名角色的原生优先级分为 40/60 后，真实移动和全部 7 项 UI 用例通过，确认对称避让是阻塞原因。按已授权的问题修复闭环，追加以下小规划：

- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationAvoidanceAssignment.cs`：导航专属、按角色稳定 ID 消除原生优先级冲突的有状态分配器。原来不同的优先级保留，同值冲突选最近未占用值，预留其他显式值；注册变更时同步，注销/释放按所写值核对后恢复原值。不关避让、不缩半径、不修改目的地、不放宽到达。原生优先级只有 0–99，超过 100 个同时活动的导航角色无法保证互异；当前场景是 2 个。
- Extend `Assets/Scripts/Gameplay/Raid/RaidMapCommandInstaller.cs`：只在已有 roster 变化边界收集导航角色、同步分配，释放时交还。它不包含优先级算法，View/Routes 不感知该策略。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandNavigationAdmissionTests.cs`：保留正式避让/半径的双角色同时入锚点、顺序实际到达，稳定/显式优先级/释放隔离；去掉 UI 重叠用例的临时 40/60 配置，以正式安装链路复测。

视觉证据工具复用 P2 的 GUIView.GrabPixels 实现，抽至 `Assets/Scripts/Editor/AgentReproduction/Reporting/UnityEditorViewCapture.cs`，原 `MapGraphEditorPreviewEntry.cs` 包装调用；新增 `Tests/MapCommandVisualTests.cs` 在真实场景、真实 SRP 和 GameView 上捕获 HUD/展开图及对应根状态。截图工具只属于 Editor 验证层。

## P5b/P5c 实现、截图和验收结果（2026-09-13）

已安装正式 Resources HUD、主题、专用 TMP 中文字体和 5 类子视图资产，保留旧视图 Prefab GUID；源 `text-c.ttf` 未修改。Presenter 汇总目标/路线，Viewport 共用小图和 M 大图，Overlay 只负责装配。行进端点和边绘制同源，同行合并身份仍在线上；处理槽位稳定避让节点、目标身份、中央名称和线。SO 修订触发视图刷新，不移动世界目标、不提交新命令，相同来源的距离缓存保留。已注销角色的视图释放，仍存活但合并的角色视图留在池中。

真实场景暴露旧小地图 Awake 时 scene.isLoaded 为 false，过早创建旧 UI；旧控制器移到 Start 判定，Installer 显式抑制同场景旧地图。商店/仓库清理新 HUD，重复安装不重复显示。导航冲突分配属于 Navigation，Installer 仅按 roster 同步；精确记录所有权，旧安装器不能恢复新拥有者即使相同的写入值。

| 最终定向验证 | 结果 | 原始证据 |
| --- | --- | --- |
| 真实 UGUI/Prefab、焦点输入、视口同源、处理避让、在线重叠、布局修订、清理 | 9/9 | `Logs/AgentReproduction/20260913-060520-123` |
| 真实双人共同入锚点、稳定优先级、外部修改、替换拥有者释放隔离 | 4/4 | `Logs/AgentReproduction/20260913-060720-873` |
| 正式场景安装、卸载、迟到角色、禁用重启、绑定失效 | 10/10 | `Logs/AgentReproduction/20260913-060841-126` |
| 真实距离/反击/折返和只读投影回归 | 5/5 | `Logs/AgentReproduction/20260913-060952-529` |
| 正式场景 SRP/GameView 小图和展开图截图 | 1/1 | `Logs/AgentReproduction/20260913-061127-006` |

各最终 Runner 均正常退出，原始源输入 SHA 未变化。初轮 `051638-051` 是测试断言编译错误；`051744-054` 暴露矢量 Graphic 缺 CanvasRenderer，补组件约束后修复。`051938-973`/`052245-614` 的双人对称停滞已按上文实物对照修复，最终测试没有保留临时 40/60 优先级。`053614-809` 的旧地图生命周期失败已修复；后续实际视觉没有两个地图或重复输入。

已实际打开三轮真实画面：`053928-516` 发现群旁标记重叠、长状态文本拥挤；调整稳定槽位，将状态说明放到地图底部。`055146-286` 复拍确认身份分离，但原 HUD 血条仍覆盖展开图；改为紧凑图排序 200、展开图 300，指令提示 350 保持在顶层。最终 `061127-006` 的完整 HUD 和展开图均已查看：深色背景、薄线、中央名称、资源/敌人/撤离图标和群旁双人标记协调，展开遮罩正确压暗原 HUD。代表性前后画面和同步 JSON 归档到 `outputs/map-command/visual/P5/`。

当前画面为正式自然开局进入/等待状态，真实行进/反击定位由投影测试验证；整局连续状态截图、正常战死或撤离结算和 1× 平均 >60 FPS 留给 P6，不用本轮批处理 GameView 频率代替性能。运行时导航签名保存顺延到 P6 的龙骨礁断连诊断之后，原因和后续验收见 `p6_execution.md`。

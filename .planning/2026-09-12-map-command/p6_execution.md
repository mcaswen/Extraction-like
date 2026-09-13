# P6 真实场景、独立契约和交付

状态：P5 已以 `977bb2c` 提交；P6a 探针和独立契约已实现，首次 MR01/SC02 正在验证。沿用已确认的大规划和用户“直接做完”的授权；逻辑 4×，真实渲染 1× 平均 FPS >60，战死是正常终态。

## P6a：根路线证据接入和自主场景

已重读 SceneRaidRunController/Observer/ReadModel、InventoryDriver、现有 CommandScenario/ClusterCommandDriver、脚本配置和 Runner、RouteSnapshot/Result/Step、Installer/共享 Environment。现有 MC 驱动按具体子指令 ID 判定完成，不能代表多群根路线。本步先补只读证据，保留 SC02/SC03 的自主行为和背包代理。

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidRouteEvidence.cs`：订阅真实 Pawn 根结果和注册事件，保存根/版本/序列/游标、步骤及反击变化；低频记录地图投影、原锚点距离、世界位置和共享查询次数。通过现有只读快照读取，不产生导航查询、目标指令或取物。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs`：在每名角色的既有 Transform/导航/敌人血量快照旁附上根路线记录，不复制原导航和战斗探针。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs`：在场景加载前安装证据订阅，LateUpdate 调度，终态写完后释放；复用现有库存、帧、渲染和结算设施。
- Create `tools/agent-repro/SceneRaid.Routes.Contracts.psm1`、`Test-SceneRaidRoutes.ps1`：从原始路线事件/快照独立检查已接受根、合法相邻边、实际游标顺序、反击身份保持、显示序列/位置和正式终态。缺证据不能记 PASS，接受通知不能代替执行完成；保留拒绝/失败原因供诊断。
- Extend `tools/agent-repro/SceneRaid.Report.psm1`：路线契约独立字段，不拿旧单敌人契约冒充新规则；保留现有库存守恒和死亡判定。
- Reuse `Invoke-SceneRaid.ps1`、`SceneRaidInventoryDriver.cs`、`SceneRaidFrameSampler.cs`：首先执行 MR01（SC02），根据根/子/世界联合证据定位问题，逐项写修复归属后复跑。

新增 `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandRouteEvidenceTests.cs` 单独验证证据的实际序列、写盘、快照拷贝、订阅释放和不改变行为；不混入 UI 视觉测试。`SceneRaidScenarioConfig.cs`、Runner 配置增加 routeEvidenceVersion=1，旧报告构造没有该版本和路线事件时保持旧契约，新场景不得遗漏路线验收。

P6a 首轮测试 `Logs/AgentReproduction/20260913-062028-771` 2/3，通过真实执行但读日志时发生 Windows 文件共享冲突；关闭证据写入后读取，`062235-912` 3/3 通过。独立 PowerShell 合同 18 个正反例通过，旧报告 79 项通过（`Logs/SceneRaidReportProbes/20260913-062922-069`）。同一真实构造日志经独立合同核对 PASS：1 根、5 次状态、9 次帧快照、8 次同步显示、7 次线上位置样本，实际完成 n0→n1→n2。

合同调试保留两个实现修正：PowerShell 重载须显式 double 才不会把 0.5 进度取整，测试辅助函数避免 Copy 内建别名。实际日志末帧根已 Completed 而 20 Hz UI 仍处于上一刷新，属于允许的瞬时延迟；合同按根身份/版本/游标/活动状态同步比较，持续跨采样（>0.35 秒）不同步仍失败，追加正反例，不能无限跳过不匹配样本。它不调用 UI 刷新制造同步，也不把样本完整当作每个中间帧均已验证。

当前进程检查没有运行中的 Unity，旧 Final 工作区也已不存在；复制空闲 Regression 的 Library 缓存到独立且有所有权标记的 Final 工作区，后续常驻场景 Editor 保留。WSL Python 逐小文件复制较慢，停止该缓存复制进程后用从 Ubuntu 启动的 robocopy /E 完成，没有删除或改动源资产。

MR01 首轮 `20260913-063355-897` 已结束：证据 PASS、源码未变、Editor PID 38472 保留，300.82 秒墙钟、23483 帧、0 运行异常、4 个真实背包会话；游戏 BEHAVIOR_BLOCKED，不能验收。320 个失败子指令和持续 NoProgress 正式保留。根合同仅报 `root_failed:2:NoProgress`，1074 次路线帧、2132 次同步显示、74 次行进位置、5 次等待、7 次反击样本未出现图拓扑/距离显示违约；这些通过事实不抵消执行失败。后续修复见下节。

## P6b：远近指挥、编辑生效和导航断连

### MR01 首轮发现：死亡角色继续占用路线锚点（构造已修复，待整局复核）

`Logs/SceneRaid/20260913-063355-897` 在真实游戏时间推进期间记录：1 号已 Dead、血量 0，停在 `(215.699,3.167,-147.380)`；2 号血量 90，停在 `(214.997,3.167,-150.291)`，当前锚点距离约 2.705 米、完整路径、近零速度，反复 NoProgress 后对不同终点重试同一入图群。源码 `AgentPawnRoot.HandleDeath` 只 Stop/ResetPath，没有退出原生 NavMeshAgent 避让；该距离接近两个正式缩放身体的避让尺寸。死亡本身仍是预期行为，本次只调查幸存者被阻塞。

- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandNavigationAdmissionTests.cs`：创建正式身体尺寸的双角色，一名在共享锚点经正式伤害入口死亡，另一名执行真实群路线。先保留红灯和位置/根/原生导航证据；对照仅关闭死亡角色的原生导航，确认幸存者可实际到达。不能关闭存活角色避让、缩半径或扩大到达容差。
- 如对照确认，Extend `Assets/Scripts/Gameplay/Agent/Core/AgentPawnRoot.cs` 的既有 HandleDeath 身体收尾：停止移动后关闭自身 NavMeshAgent。生命/根终态/角色物体仍保留，归 Pawn 已有生命周期职责，不新建通用死亡服务，不把尸体行为塞进 MapGraph 或测试。
- 定向复跑多人导航组、`SceneRaidTerminalTests.cs` 的四种死亡/撤离顺序，再重跑 MR01。只有实际修复结果证明后才关闭此项；若仍有存活敌人/角色占位，分别以真实探针继续定位，不能用死亡修复代替所有占位问题。

构造 `064417-685` 确认原行为在锚点外 3 米以 NoProgress 失败，fallenNavEnabled=True。A/B `064543-069` 仅在测试关闭死亡者 NavMeshAgent，幸存者真实 Completed 到 n1（x=15.732），证明阻塞来源；但测试因 Installer.ProfileMatches 对已禁用原生组件 GetAreaCost 的错误日志仍为 FAIL，不改成通过。追加 Extend `Assets/Scripts/Gameplay/Raid/RaidMapCommandInstaller.cs`：已安装角色的 profile 观察只读取存活且原生导航就绪的组件，暂时未就绪期间保留原快照，恢复后继续原观察；职责仍归安装生命周期。正式修复移除 A/B 测试写入，由 Pawn 死亡收尾释放导航，同时补无效读取保护，复跑多人 5 项、终态 4 项和安装 10 项。

正式修复定向结果：`064857-942` 多人导航 5/5、`064957-547` 死亡/撤离终态 4/4、`065108-446` 安装 10/10，全通过且源输入未变。测试不再关闭死亡者导航，由正式 HandleDeath 完成；幸存者到达精度、尸体位置/物体保留和存活导航启用均有断言。接下来同种子 MR01 整局复核，不能用这些构造替代整局结算。

修复以 `6f55180` 提交后，同种子 MR01 `20260913-065441-522` 在约 64.7 秒达到正式终态：证据 PASS、游戏 EXPECTED_DEATH、0 运行异常、0 失败子指令；1 号正常战死，2 号容量不足后走 9 群撤离路线并发布 Extracted。6 次背包会话，两人实际取物、战斗伤害、暂停恢复、容量撤离和反击恢复均有覆盖；仓库 expected/actual 完全一致。根合同 PASS（容量撤离作为有名控制结果），227 次路线帧、452 次同步显示、128 次线上样本。死亡占位和幸存者撤离闭环在真实场景关闭。普通 state 采样未赶上销毁前 Extracted，原始 route.result 和 route.unregistered 都保存了该终态，不能把采样计数 0 误解为没有撤离。

### 龙骨礁导航岛诊断小规划

Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandNavigationIslandTests.cs`：独立 EditMode 场景用例，复用 `MapGraphSceneCollector.cs` 的真实来源/导航 profile、`MapGraphTargetBinding.cs` 的已保存锚点、原生只读路径查询和 `UnityEditorViewCapture.cs`。在失败断言前导出两名初始角色/邻近群到撤离锚点的路径角点、附近三角化、物理向下射线、Collider/Renderer 来源和边界、SceneView 俯视/斜视画面。可视化只标注真实三角形和路径，不修改世界物体或导航。通过门槛为正式两个撤离点都可从初始区域到达；初始红灯作为诊断证据，不伪造连线。

初步静态发现撤离 Cluster 含可见 MeshRenderer，根位于 y≈10.01，附近主导航面约 y≈6.8；需要实际几何和构建源证据区分悬浮标记、真实台阶或烘焙遗漏，暂不据此移动物体。后续具体修复文件和精确场景增量在诊断后记录。

诊断 `070941-344` 为有效红灯：两名角色到龙骨礁均 Partial，雨林均 Complete。SceneView 两张已查看，圆柱上独立三角形和地面断开；Surface 明确 UseGeometry=RenderMeshes，圆柱顶 y=10.36、烘焙面 10.50，邻近地面导航 6.83。物理射线只有下方 Terrain（可见岛屿 FBX 无 Collider），因此不能用物理射线把标记降到 Terrain y=1.10。首次 `070307-432` 是 SceneView 重载编译错误，修为 orthographic 属性加三参 LookAtDirect，未当作行为红灯。

修复归属：Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandSceneRepairTests.cs`，仅隔离副本允许执行的维护用例，按真实龙骨礁地面 Mesh 三角形交点将现有圆柱底面贴到可见地面，再用同一个 NavMeshSurface 设置烘焙。生成独立 `Assets/Scenes/Scene_DB/Scenezl_Final 1/NavMesh-CommandRoutes.asset`，保留用户原 NavMesh 文件，不覆盖或提交其无关增量。Extend 正式场景仅该实例高度、Surface 导航资产引用及地图 Binding，复用 `MapGraphEditorDocument.cs` / `MapGraphAuthoringTransaction.cs` 更新正式图和绑定；仍由原算法生成真实可达的横竖连接，不手画假边。维护用例不进入 Gameplay，也不在普通测试自动修复。先在副本产生资产/截图/可达证据，通过后精确导出；场景提交用 HEAD 加本次必要增量，不全量收录用户修改。

修复结果：`071413-836` 维护通过，真实地面交点 y=6.712236，圆柱中心从 10.010208 降至 7.062236，生成 29 边并导出准确三个场景块。`071640-527` 独立重载后 8 条路径全部 Complete，修复后斜视图已查看。新增缓存断言在 `071828-845` 发现新烘焙和反序列化后的三角形原生顺序不同，保守指纹正确使缓存失效；维护流程改为保存后重新打开再生成缓存。`072045-845` 仅 RefreshGraphBakeFromSavedScene 定向通过，导出刷新图；最终 `072151-814` 完整诊断通过：两名角色到两撤离群和四个邻近群到龙骨礁均 Complete，29 条缓存边直接加载，0 待补验、0 新导航计算。原用户导航文件未修改，正式导航指纹保存态为 `0d08ddbf700007774f593a480da8040b514c19a493559446099ae6cbaae60981`。这关闭物理断连及启动缓存补算，真实运行新图仍随后续 MR02/MR04 验收。

新路线脚本使用独立版本和目录 `tools/agent-repro/map-command-scenarios.json`；继承已有请求文件、哈希和进程隔离机制。MR02 按真实入图/行进/背包关闭前提，向正式 Router/地图 Handler 有限次下令，另一角色保持自主。旧 MC 脚本继续记录历史子指令含义，不静默改判定。具体驱动文件在 P6a 证据验证后补小规划，仍归 Automation/Commands，不能放入 Gameplay。

### MR02 实施小规划和边界

Create `SceneRaidRouteScenario.cs` / `SceneRaidRouteCommandDriver.cs`（`Assets/Scripts/Automation/SceneRaid/Commands/`）：新版本有限脚本，分别负责严格配置校验、状态前提下通过正式地图 Handler 发令，保存候选实际路径长度/区域/位置、提交前后根、真实移动及接受事件。先近群，在真实移动后替换远处跨区群，随后提交不存在节点验证拒绝保留；给 2 号有限次指令，1 号保持自主。脚本只查选定时的真实路径，分帧限额，不写角色位置、血量、库存或根游标。前提缺失/正常战死导致未完成时标 PARTIAL，不能补发无限命令或判玩法错误。

Extend `SceneRaidScenarioConfig.cs`、`SceneRaidRunController.cs`：独立 ManualRoutes/schemaVersion=3，接入新驱动，继续复用现有 InventoryDriver、路线只读探针和正式结算，旧 ManualCluster 不变。Extend `Invoke-SceneRaid.ps1`、`scene-raid-cases.json`，Create `SceneRaid.RouteConfig.psm1` / `map-command-scenarios.json`：新 SC10 4× 入口，版本和 SHA 校验、冻结原脚本。Extend `SceneRaid.Routes.Contracts.psm1` / `Test-SceneRaidRoutes.ps1` / `SceneRaid.Report.psm1`：从原始提交、结果、快照独立核对近远分类、替换身份、失败保留、真实前进、反击恢复和脚本完成，不以驱动自报通过为最终判定；结算复用自主的物品守恒合同。

Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandRouteScenarioTests.cs`：配置拒绝、有限发令及不干预其他 Agent 的定向构造；场景随机反击有覆盖则计入，无覆盖由已有根恢复构造补证。具体实验如需调选择范围只改脚本并记录，不改变生产战斗能力。

MR02 驱动实现验证 `073348-620` 3/3 通过：错误版本/哈希/无限参数拒绝，真实近群移动后替换跨区远群，第三次无效请求保留根并最终走到远群 x=35±0.4，另一角色未被下令，脚本停止后反复 Tick 不再发令。独立合同原 18 项、新脚本 11 项正反例通过，旧报告 79 项通过（`073454-382`）。PowerShell 5.1 中文注释文件需要 UTF-8 BOM、条件管道返回单元素须显式数组包装，已在测试中修正。后续开始 SC10/MR02，不能把构造当作正式整局。

### MR02 新发现：可导航到达但无法射击的近身落点

`073729-136` 的三次有限指令均完成提交/真实前进前提，远群实际路程 1003.67 米、8 群路线，已推进到第 7 群。目标哨兵位于 `(-388.9192,3.1667,158.9296)`，附近另一个哨兵的胶囊体重叠；角色进入距目标 0.067 米处后身体/枪口射线 Occluded，连续 LostSight，根 NoExecutableMember，幸存者健康 41 后停止推进。源码 `AgentCombatApproachQuery.cs` 对导航可直达的目标中心直接返回成功，只有 NavMesh 路径失败才扫描可射击候选，所以“到得了但打不了”不会寻找侧方射击位置。

Create `Assets/Scripts/Editor/AgentReproduction/Tests/ReachableCombatApproachTests.cs`：按上述真实身体、相邻碰撞体和近身坐标构造阻挡，先验证旧查询返回不能开火的落点，再验证完整正式 Engage 在 1×/4× 可换位并真实伤害。Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentCombatApproachQuery.cs`：可直达的落点同样必须通过既有身体/枪口候选约束，否则继续已有有限环扫描；不跳过遮挡、移动敌人或改血量。接近点选择仍归 Navigation，射击和完成仍归原战斗/生命周期。复跑高处接近和射线范围相关组，再同种子 MR02。另保留首个资源子步骤 NoProgress 和运行时缓存补算（EditMode 缓存虽通过，Play Mode 仍补 58 次）继续定位，不用此修复代替两者。

MR02 首轮结束：证据 PASS、0 运行异常，整局因上述阻塞超时，175 个失败子步骤，不能验收。独立脚本合同 PASS，近/远真实移动、跨群推进、无效保留、另一人自主都有证据，玩家根自然反击 8 次并以同一身份恢复 8 次。4× Editor 诊断均帧 113.78，不作为 1× Player 性能成绩。

运行时缓存补证归属：Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandRuntimeCacheTests.cs`，加载正式场景 Play Mode，记录 Installer 实际导航指纹、保存指纹、实际/保存 profile 和新只读成本服务的失效原因，区别原生顺序变化、profile 初始化差异、真实锚点变化；不因为 EditMode 已通过就略过 Play Mode。诊断自身查询不计入生产每帧开销。

接近查询首轮 `074647-171` 3/3 红灯，确认原查询对完全被另一敌人体包住的中心仍返回“可接近”；加入射击候选校验后 `074925-212` 仍红，原因是构造的两个身体相互包含瞄准点，所有侧向也受挡，并不存在可开火位置。不能以忽略敌人/墙体射线来制造绿灯。将该极端构造的正确预期明确为拒绝无效落点，另用只挡枪口的局部遮挡构造验证有限扫描和真实伤害。

场景实证两个实验室出生点：外层 `EnemySourceCluster/Pfb_EnemySpawn[0]` 为 `(-388.0192,3.1272,157.8237)`，内层 `Env/实验室/EnemySourceCluster/Pfb_EnemySpawn[0]` 为 `(-388.9192,2.2272,158.9296)`，相距 1.426 米但每个哨兵身体半径 1.5 米，均不移动，属于场景重叠配置。Extend `MapCommandSceneRepairTests.cs` 仅在副本将内层这个点向西分离 4 米，先检查同一 NavMesh 的完整可达路径，保留群、数量、Prefab 和战斗配置；更新地图验证指纹。Extend `MapCommandRuntimeCacheTests.cs` 在正式 Play Mode 同时输出出生对应物，断言静止哨兵没有互相包住瞄准点，作为场景红/绿证据。生产接近查询继续拒绝没有射击候选的落点；不更改战斗目标/伤害规则。

`080357-221` 正式 Play Mode 两项中缓存通过、静止哨兵重叠失败；实际指纹/profile 全部匹配、29 边直接加载、0 新计算。`080503-985` 维护因将审计同名索引 `[0]` 误当物体名称失败，改为群路径内按已确认世界坐标唯一定位；`080549-814` 维护通过，只导出 Transform `6573334367530119521` 的一行位置。该点没有 Renderer/Collider，不改变静态 NavMesh；图锚点仍属于另一出生点。

接近构造 `081320-284` 4/4 通过：完全包围拒绝，局部枪口遮挡选择真实可开火候选，1×/4× 正式 Engage 换位后实际造成伤害。局部遮挡只需约 0.1 米加转向即可解除，不强迫继续走满候选距离。扩大的 5 米悬顶实验 `080756-786`、`081036-576` 则记录到角色换位到约 4.72 米、射线 Visible 但弹丸未造成伤害；可能涉及有限体积弹丸擦边，尚未确认原因，作为独立射击边缘问题保留，不能把局部用例通过解释为任意掩体均已验证。

MR03 在测试拥有的图副本删除/添加合法边，比较实际规划和经过序列，结合现有纯图、根重规划和 Editor 保存测试补证。当前龙骨礁撤离锚点可采样但跨区路径 Partial：先导出真实路径角点、附近几何/层级/导航面和画面，判断是锚点、通路还是烘焙配置问题；不得删节点、伪造可达或瞬移。用户已授权有证据的场景修复，只精确提交本次修改，隔离用户场景/NavMesh 增量。

正式图的运行时导航指纹从 P5c 顺延到此步：先关闭真实导航问题，再按最终场景/导航采集签名，避免保存马上失效的缓存；不因此跳过最终缓存复用验证。

## P6c：渲染、截图和最终审查

### MR02 第二轮锚点占位停滞：后续小规划

`082817-582` 的同一玩家根到第 7 群时，角色距锚点约 1.5 米抖动，血量 87，完整路径、速度方向反复变化，持续百秒。`AgentClusterStepExecutor.cs` 当前先要求到达精确锚点才允许处理敌人，锚点若被存活敌人占据就存在互相等待；`AgentNavigationMotor.cs` 只按 0.05 米任意位移重置进展计时，抖动会绕过超时。

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandOccupiedAnchorTests.cs`：实际 NavMeshAgent 敌人占住锚点、没有敌人主动攻击或玩家干预，1×/4× 正式群路线必须先真实击杀，清完整群再实际访问锚点、前往下一群。先保留红灯，不挪锚点或缩身体来通过。
- Extend `AgentClusterStepExecutor.cs`：仅当前敌人群、距离锚点进入攻击范围且候选满足既有发现射线时，可从行进切到群内战斗；仍由原 Lifecycle 提交子步骤。步骤单独记录实际锚点是否已访问，提前开战后必须补到达才能完成。所有权/路线身份不变，反击仍先归原伤害恢复，资源与撤离规则不变；复跑原群步骤和根路线。
- Extend `AgentNavigationMotor.cs`：复用已有完整路径角点的剩余长度来判断实际推进，任意左右摆动不续期；目标实质改变重新建立基线，暂停不消耗时间，不增加导航查询。具体构造覆盖抖动、正常前进、目标移动及绕行，保持原诊断字段。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/NavigationProgressTests.cs`：独立运动进展边界，实际原生 Move 施加局部往复、U 形真实导航绕路、同命令反向移动目标；与原 `NavigationExecutionTests.cs` 的到达/暂停/原生恢复回归互补，断言基于实际位置与终态。
- Extend `SceneRaidContracts.cs` 的一次停滞证据，复用 `SceneRaidNavigationEvidence.cs` 记录附近碰撞体/导航占位；该查询只在报告一次停滞时执行。邻居同时附敌人生命信息，区分存活占位和死亡残留。检查实验室另一对近战出生点的视线互包，只有实际红灯后才继续精确场景修复。

占位构造最初 `083733-612` 的被动 NavMeshAgent 会让位，不能据此声称重现固定占位；改用随敌人物体销毁的实际 NavMeshObstacle 表示固定避让身体。`084102-855` 1×/4× 均 NoProgress 红灯，正式步骤修复后 `084421-844` 2/2 通过：提前真实交战、清两名成员、补完锚点访问、完成下一群。`084142-982` 对全部出生敌人的瞄准点检查通过，因此没有继续修改近战出生点。

导航进展红灯 `084742-316`：1×/4× 局部抖动均可无限续期；修复后 `084908-574` 抖动两项、真实 U 形绕行通过，同命令反向目标在测试自设的 1 秒窗口发生 Stalled。反向转身及减速允许正常短时路程增长，将该项窗口恢复正式值 3 秒；目标反转增加约 30 米，未重建基线仍会超过窗口，不靠延长到能走完整段来放行。`085959-698` 四项全部通过。原导航 `084958-972` 17/17、整群步骤 `085053-280` 10/10、根执行 `085133-437` 18/18、编辑后实际路线 `085235-944` 3/3 全通过。正式场景是否解除占位仍由下一轮 MR02 判断。

同种子正式 MR02 `090113-360` 约 79.7 秒结束：证据 PASS、游戏 EXPECTED_DEATH、0 运行异常、0 行为失败、0 停滞。2 号实际通过此前卡住的第 7 群，到达第 8 群远端资源，容量不足后接受自主撤离路线，继续到其第 5 群时正常战死；1 号先前正常战死，死亡库存不入仓。路线合同 PASS，283 次帧、566 次同步显示、195 次行进、9 次等待、15 次反击样本。没有为通过改变生命、伤害、身体尺寸或锚点到达条件；本轮关闭真实占位阻塞，不以两人战死冒充实际撤离，后者已有 MR01 且继续由 MR04 验证。

具体归属：Create `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidMapVisualCapture.cs`，仅 SC10 编辑器诊断运行启用的有限截图观察器，通过既有 GameView 像素工具记录紧凑、展开、行进、处理、反击、撤离画面及同刻根/显示数据；只切地图视口，不发游戏指令。Extend `SceneRaidEditorEntry.cs` 负责创建、轮询和结束释放，Player 性能运行不安装截图观察器。首次同时记录实际已安装导航签名、profile 和只读成本加载原因，解释整局补算，不改变签名算法。

MR03 Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandEditedRouteTests.cs`：使用真实平面导航和四个已清空的敌人群，作者 API 删除、添加合法直边，保存测试拥有的 SO 后卸载重读，以正式 Installer 和 Pawn 根请求运行。独立采集根实际经过序列及每群锚点位置；同一开始/终点在不同拓扑下必须走不同群。移动图标和样式再保存，世界位置和最短实际路线不变；测试结束删除自己创建的资产。与 P2 编辑器事务/Undo/保存的已有证据互补，不直接给 Pawn 写路线或游标。

MR03 `081933-869` 3/3 通过。作者编辑经正式预览求解/独立几何校验，真实资产保存、卸载、重读，然后实际走 `n0→n3→n2`；恢复连线改为 `n0→n1→n2`，移动区域/线样式不改变世界群位置或经过顺序。前两轮构造修正：`081529-196` 缺失正式行列约束，补用原 Editor 预览流程；`081801-895` 用帧采样错过空群同帧完成，改为观测游标离开时的真实位置，不能只凭最终序列判通过。

场景分离后的 `082035-625` 两项全部通过，9 个正式静止哨兵均未互包瞄准点；缓存仍为 0 新查询。高处兼容 `082101-738` 12 项中 1× 导航恢复墙钟超时、其余 11 项通过；同一失败方法定向 `082321-078` 1×/4× 均通过。保留首次超时，不将一次重跑概括为所有时序均已稳定。

复用 SC07 构建和已授权的可见 Player，MR04 1×、4K/High Fidelity、不限帧，正常处理资源/交战/撤离和预期战死。小/大地图切换只影响显示，不改变任务。截图开销单列，原始帧数据完整保留，平均 >60 为门槛。持续捕获实际行进、等待、反击、多人及撤离画面，打开查看后修正，未自然出现的状态由确定性构造补证。

MR04 具体入口：Create `Assets/Scripts/Automation/SceneRaid/SceneRaidMapViewportDriver.cs`，仅 `exerciseMapViewport` 验收开关启用，在墙钟 10 秒展开、40 秒收起同一个正式视口，写切换前后根身份/版本/游标和 UI 提交计数。Extend `SceneRaidScenarioConfig.cs`、`SceneRaidRunController.cs` 只做配置和编排，Extend `Invoke-SceneRaidPlayer.ps1 -ExerciseMap` 检查两次有限切换、根不变和真实展开采样；不开启时原脚本不变。没有截图循环或 UI 测试写根路线，性能数据保留全部帧。

更新 `README.md`、`Assets/Docs/GameplayAgentFrameworkDesign.md`、`outputs/map_command_validation_report.md`、本规划及 `architecture_review.md`。记录每轮输入/结果、真实覆盖缺口和剩余问题；各小步通过后中文提交，不推送，不提前宣称整项完成。

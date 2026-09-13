# 新发布小地图功能验证

2026-09-13，`Scenezl_Final 1.unity`，发布图 revision 5。**地图显示、交互和静态路线检查通过；完整搜打撤未通过，发现一处可重复的路线执行阻塞，以及导航缓存未命中。两项尚未修复。**

## 1. 验证对象和结论

本次验证用户保存的 `Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset`、`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`，没有重生成布局或改动玩法源码。正式图有 7 个区域、27 个群、26 条连接，单一连通分量。运行时另外生成的 ActiveEnemyCluster 不作为额外发布节点，不能用运行时 40 个 Cluster 组件数量替代发布图的 27 个节点。

| 范围 | 结果 | 证据 |
| --- | --- | --- |
| 保存场景与地图节点身份、区域归属 | 1/1 通过 | `scene-identities`，215420-799 |
| 发布图严格几何、真实导航、最短路线 | 1/1 通过，另记缓存警告 | `published-graph`，215612-633 |
| 点击焦点、展开/缩放/平移、路线显示、Agent 投影 | 9/9 通过 | `presentation`，215639-527 |
| 真实 Play Mode 紧凑图和 M 图 | 1/1 通过 | `visual`，215713-997 |
| 正式运行直接复用保存的导航成本 | 0/1，失败 | `runtime-cache`，215447-172 |
| SC10/MR02 指令流程，4×、300 秒墙钟 | 脚本覆盖通过，整局阻塞 | `sc10-mr02`，220113-067 |
| SC02 纯自主流程，4×、120 秒墙钟 | 整局阻塞，同一节点复现 | `sc02`，221017-510 |

最终定向 NUnit 为 **12 通过、1 失败**。早期诊断尝试不混入最终计数。两次 SceneRaid 均正常完成采集、零运行时 Error；`evidenceStatus=PASS` 只代表证据齐全，玩法结果均为 `BEHAVIOR_BLOCKED`，路线合同均为 `FAIL: root_failed:1:Unreachable`。

## 2. 已通过的功能

保存图的全部 26 条连接分别测量两个方向，**52/52 真实导航路径完整**。用重新测得的边长构建独立 Floyd 对照，正式路线规划的 **702 组不同起终点**均匹配；2 个 Agent 初始位置到 2 个撤离群的 4 条实际导航路径均完整。3 条线穿过区域名称框，符合最新允许穿名规则。上述结论证明群锚点间的静态可达性，不等于群内敌人都可以被处理。

真实 UI 的区域、群、边 ID 集合与发布资产完全相等；已检查 [紧凑 HUD](visual/visual/01-compact-hud.png) 和 [M 地图](visual/visual/02-expanded-map.png)。深色风格、区域名称、横竖直线和图标显示正常，没有出现面板有连接而运行图漏边的情况。构造用例覆盖焦点 Agent 点击提交、无效指令保留原根路线、沿连接投影、处理群时图标错位显示、展开后复用相同视图及禁用清理。

SC10/MR02 使用正式指令入口，测试程序操作背包，用户无需点击。近目标和远跨区目标均实际移动，远路线实际经过中间群；无效目标请求保持原玩家根路线。记录了 **4 次玩家路线反击、4 次恢复**，另一名 Agent 保持自主行为。

远目标路线约在墙钟 34.53 秒触发 `CapacityExtraction`，按既有规则转入自主撤离，2 号随后撤离并结算。因此这轮不能宣称“远目标玩家路线完整到达终点”。期间一次 `MoveTo/NoProgress` 子步骤失败后，同一玩家根路线重规划并继续；原始失败保留，不另报为持续卡死，也不把完整合同改成通过。

## 3. P1：敌人群处理失败，存活 Agent 无法继续撤离

两轮均卡在 **`Zone-龙骨礁/EnemySourceCluster_A`**，节点 `cluster_73607cab556336476bac9995b5e46c2b`。证据见 [阻塞状态与失败时间线](blocking-route-evidence.json)，原始事件在各场景目录的 `events.jsonl.gz`。

| 探针 | SC10/MR02 | SC02 自主 |
| --- | --- | --- |
| 1 号生命 | 68 | 66 |
| 1 号坐标，世界 XYZ | (331.2648, 11.0000, 191.2677) | (331.2545, 11.0000, 191.2645) |
| 离当前群锚点的平面距离 | 0.2572 m | 0.2657 m |
| 群内存活敌人 | 2 | 2 |
| 导航状态 | Arrived，位于 NavMesh，无当前路径，速度为零 | 相同 |
| 根路线最终状态 | Failed / Unreachable，未激活，无待执行路线 | 相同 |
| 首次根路线最终失败 | 墙钟 81.22 秒 | 墙钟 72.67 秒 |
| 观察结束 | 300.96 秒，仍在原位置 | 120.30 秒，仍在原位置 |

角色首先尝试龙骨礁撤离群 `cluster_a84fd1c0b6b5590d95cbf011798b2e2a`，路径起点仍是上述敌人群。重规划耗尽后更换终点，最终又尝试雨林撤离群 `cluster_b232001ecd90d331fc0d2a4844eb5545`；新的路线依然从同一敌人群开始，重复 `Unreachable`，无法脱离。自主轮中还尝试了另一目标，同样失败。

3D 锚点距离约 3.01 m，而角色 `baseOffset=1`、世界缩放为 3，**不能把该 3 m 直接判成高低差导航错误**。平面距离和 `Arrived` 表明已经到达群锚点附近。当前证据把问题定位到“到达群后创建/验证敌人处理指令，以及失败后的重新选路”链路，尚未证明具体是哪名敌人的位置、导航或候选判定失败。

后续定位职责：

- `Assets/Scripts/Gameplay/Agent/Routes/AgentClusterStepExecutor.cs`：群到达、敌人处理、有限重试和失败上报。
- `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteTargetResolver.cs`：群成员解析、处理指令创建及验证。
- `Assets/Scripts/Gameplay/Targets/Input/TargetClusterDirectiveFactory.cs`、`Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveValidationService.cs`：具体敌人选择和可执行性约束。
- `Assets/Scripts/Gameplay/Agent/Routes/AgentRouteController.cs`：失败重规划、终点记忆，避免换终点后持续进入同一个无法处理的入口群。

下一步最小诊断应记录群内每个候选敌人的身份、位置/血量、导航采样、射线结果和拒绝原因，再决定修场景配置还是运行时处理逻辑。当前没有足够证据直接删群、改连线或绕过交战验证。SC02 的 2 号生命为 0、根路线 Dead，属于用户确认的正常战死，不计入此缺陷。

## 4. P2：发布的导航缓存未命中

专用 Play Mode 缓存用例保持原断言，失败原因为 `BakeContextMismatch`。保存的运行时指纹为 `6947f505e7a4a3af037c797e2dec2bb74e9a4e172dfc373feb488361c0aba771`，重新加载后的 EditMode、Play Mode 均为 `ce55d6db3b40b96164e551fdbca60127493baa9a795687081001d875d727511d`。

保存和当前导航 profile 一致。初始化产生 26 条待补算边，功能审计调用原预算接口补算后执行了 **52 次方向查询**，路线成本恢复可用。[原序列化成本对照](saved-bake-comparison.json)显示，52 个方向的保存边长与新测边长最大差异仅约 **0.00003132 m**。这说明本轮没测到实际连接成本变化；无法据此认定全局 NavMesh 完全相同。

相关职责仍归 `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphNavigationFingerprint.cs` 和 `MapGraphNavigationCostService.cs`。指纹当前包含原生三角网数组顺序，属于后续核查方向；本轮未证明数组顺序就是根因，未绕过失效检查或强行信任缓存。额外启动补算确实存在，但没有足够测量把特定卡顿归因于它。

## 5. 复现、性能边界和交付

使用现有 `tools/agent-repro/Invoke-AgentRepro.ps1`，分别运行 `MapCommandPublishedGraph`、`MapCommandPresentation`、`MapCommandVisual`；身份组窄筛选 `SavedSceneAndBoundMapHaveMatchingNodeIdentities`，缓存组窄筛选 `FormalPlayModeReusesSavedCostsWithTheActualNavigationAndProfiles`。不需要运行带场景维护副作用的完整身份组。

SceneRaid 复现参数完整保存在 `sc10-mr02/config.json`、`sc02/config.json`：场景相同，seed 731，simulationSpeed 4，SC10 选择 MR02，SC02 观察 120 秒。通过 Ubuntu 调用现有 Windows Unity Runner；本轮使用独立 `AnomalySearchMapValidation` 工作区，测试 Editor 保留，用户 Editor 和 Final 副本没有关闭或覆盖。

4K、High Fidelity、4× Editor 的全程诊断均帧分别约 123.11、96.14 FPS，包含启动和阻塞等待段。**不作为 1× Player 平均 60 FPS 的新验收结论**，本轮重点是功能。因已获得两轮一致阻塞证据，没有继续延长或重复整局测试。

新增发布图验收测试，原视觉测试的旧 28 群硬编码改为发布资产的身份全集对照；未修改运行时、地图布局或场景内容。按用户已有要求，一并提交其已发布的地图和保存场景。各最终运行的输入哈希与交付资产/测试代码一致，见 [归档清单](manifest.json)。压缩证据解压后与原始文件逐字节相同。

历史目录保留两次非最终尝试：初版图审计未区分缓存未命中与补算后路线，导致派生失败，已调整测试分层；首次独立 Editor 启动直接退出，没有产生有效场景证据，重试成功。均未伪装为功能通过。

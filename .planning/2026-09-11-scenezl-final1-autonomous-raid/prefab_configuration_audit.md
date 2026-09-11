# Scenezl_Final 1：Prefab 来源、实例覆盖和撤离接线核对

日期：2026-09-11。源码基线：`c8b1a4c`。本文保留早期静态核对结果。**此后用户已修改场景，最新场景已有撤离群 Prefab，不能把本文的“没有撤离群”当作当前结论。最新修复记录见 [hierarchy_repair_execution.md](hierarchy_repair_execution.md)。**

**第 3 点的准确结论：工程中存在 `ExtractionCluster.prefab`，但目标场景没有撤离群实例，也没有直接挂载 `ExtractionClusterAuthoring`。场景现有 A/B 是两个独立撤离点，缺少群及成员接线，不是已有撤离群仅仅漏绑 Zone。**

主规划见 [task_plan.md](task_plan.md)，机器可核查的引用边、源文件哈希、组件记录和实例覆盖见 [serialization_audit.json](serialization_audit.json)。

## 1. 如何找到真实配置

本次从 `Assets` 下 2,617 份 `.meta` 建立 GUID 到实际文件的映射，再按 Unity YAML 的 `fileID` 区分对象、组件和实例：

1. 从场景的 `m_SourcePrefab` 找到实际 Prefab 或模型文件。
2. 递归追踪 `.prefab` 内的 `m_SourcePrefab`，包含嵌套 Prefab 来源。
3. 将 `m_Modifications` 的 `target.fileID + guid` 对应到源对象，再应用相应 `propertyPath` 的场景覆盖；不能把子物体的位置覆盖算在根节点上。
4. 核对 `m_RemovedComponents`、`m_RemovedGameObjects`、新增记录和启用状态，避免把被删除的源对象算成场景存量。
5. 对 `stripped` 记录，继续追踪 `m_CorrespondingSourceObject` 和 `m_PrefabInstance`；对 SO 引用，继续打开对应 `.asset` 读取字段。

场景包含 **168 条直接 PrefabInstance 记录**，其中包括 FBX 模型实例。递归扫描覆盖 **31 份不同 `.prefab` 加本场景**，这些文件合计有 **319 条 `m_SourcePrefab` 引用记录，来源路径全部解析成功**。319 是扫描文件中的引用边数量，不是展开后场景物体总数。

JSON 保存全部这些来源边，以及本次重点核查的 Agent、Zone、资源群覆盖。它不是完整 Unity 反序列化器：模型导入结果、运行时生成物、Awake/OnEnable 最终状态和实际导航连通性仍由 P0 的 Unity 加载审计验证。静态能够查明的来源和字段，本轮已经直接查明，不再笼统留到运行时。

## 2. 撤离点和撤离群分别在哪里

### 2.1 场景 A/B 是普通场景对象

场景文件为 `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`，以下行号对应本轮记录的 SHA256。

| 项目 | Extraction_A | Extraction_B |
| --- | --- | --- |
| GameObject fileID / 场景行号 | `162653587` / 12486 | `824603105` / 54909 |
| Transform fileID | `162653588` | `824603106` |
| ExtractionPointController fileID / 行号 | `162653589` / 12522 | `824603107` / 54945 |
| 完整父链 | `IslandWallLayoutRoot/Markers/Extraction_A` | `IslandWallLayoutRoot/Markers/Extraction_B` |
| 局部位置 | `(206.1,16.6,354.2)` | `(322.8,0.12,-132.7)` |
| 局部缩放 | `(5,0.35,5)` | `(5,0.35,5)` |
| ExtractionDurationSeconds | `3.2` | `3.2` |
| DetectionHorizontalScale | `0.08` | `0.08` |
| WorldPromptVerticalOffset | `0.9` | `0.9` |

A/B 的 GameObject、Transform、撤离组件都明确保存 `m_CorrespondingSourceObject.fileID=0`、`m_PrefabInstance.fileID=0`，记录也不是 `stripped`。因此当前序列化状态没有可继续追踪的 Prefab 来源；不能根据外观推断它们仍是某个 Prefab 的实例。它们历史上是否由 Prefab 解包而来，不影响当前接线结论。

父级 `Markers` 的 GameObject/Transform 为 `1124576283/1124576284`，再上级为 `IslandWallLayoutRoot` 的 `1537201406/1537201407`。两个父级均为普通场景对象，只有 Transform，没有 Zone 或撤离群组件。父级位置/旋转为零、缩放为一，因此上表位置也是当前序列化层级计算出的世界位置；运行时 Bounds 和触发范围仍需实际导出。

### 2.2 工程已有可复用的撤离群资产

实际文件：`Assets/Prefabs/Cluster/ExtractionCluster.prefab`。

| 配置 | 实际内容 |
| --- | --- |
| Prefab GUID | `7cc4aa7609c2440da95de0b049b474ee` |
| 根 GameObject / 群组件 fileID | `1315397997066753263` / `9068098947778029321` |
| 群脚本 GUID | `4309bd9920534baeb6db55ef4671d232`，对应 `Assets/Scripts/Gameplay/Targets/Authoring/ExtractionClusterAuthoring.cs` |
| `_zone` / `_autoResolveZoneFromParent` | `fileID=0` / `1`，需要实例配置或有 Zone 的父级 |
| `_extractionMembers[0]._entityObject` | `48492721302697900`，指向其子物体 `ExtractionPoint` |
| `_rangeColliderTarget` | 同一子物体 `48492721302697900` |
| 子物体撤离组件 | `9021171616917303758`，时长 `3` 秒，DetectionHorizontalScale=`1` |

目标场景和上述递归 Prefab 来源中，撤离群脚本 GUID 命中 **0 次**，该撤离群 Prefab 也未被实例化。全项目查询能在其他测试场景找到它，不能把其他场景的实例算进本场景。

源码中 `AddComponent<ExtractionClusterAuthoring>()` 的现有显式创建入口位于 `Assets/Scripts/Editor/AgentReproduction/World/TargetFactory.cs`，属于旧构造测试；没有查到本场景 Gameplay 为 A/B 自动补群的路径。

### 2.3 Zone 绑定与全局注册的区别

| 源码位置 | 实际行为 |
| --- | --- |
| `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetAuthoringBase.cs:41` | OnEnable 调用全局 Registry.RegisterTarget |
| `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs:65` | 先调用基类 OnEnable，再 ResolveZone 并 RegisterCluster |
| `Assets/Scripts/Gameplay/Targets/Runtime/GameplayTargetRegistry.cs:49` | 群先加入全局 `_clusters`，之后校验 Zone 绑定 |
| 同文件 `:481` | Zone 为空只发 warning，不删除已注册群 |
| `Assets/Scripts/Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs:205` | 从 Registry.CopyClustersTo 取得全局候选群 |
| `Assets/Scripts/Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs:61` | 从 ExtractionCluster 的显式成员列表获取撤离点；没有扫描普通裸撤离点的分支 |

因此，一个启用且正常注册的撤离群，即使没绑 Zone，仍可能提供自主撤离候选。本场景的情况更早一步：**没有撤离群供 Collector 扫描**。另一方面，普通撤离点仍能通过触发器联系 RaidFlow，不能据此说撤离计时系统根本不存在或任何方式都无法撤离。

第 3 项据此收敛为以下修复方向，仍需用户 Review 后实施：复用现有 Authoring，为 A/B 建立明确的群、显式成员和 Zone 归属，保留当前位置、外观、触发体、3.2 秒时长及 0.08 范围配置。不能只调整父级而漏填 `_extractionMembers`，也不能把 Prefab 默认的 3 秒和范围 1 无声覆盖到场景点上。P0 先记录原场景缺口，正式修复在 P3 完成，测试工具不在运行前偷偷补群。

## 3. Agent：基础 Prefab、场景覆盖、SO 已对应

基础来源为 `Assets/Prefabs/PlayerPrefab/Agent.prefab`，GUID `80344fa5a46b42f408819a2cfecf5f9d`。

| 字段 | Agent_01 最终序列化配置 | Agent_02 最终序列化配置 |
| --- | --- | --- |
| 场景 PrefabInstance fileID / 行号 | `8495978441366001893` / 124122 | `9217900615391691191` / 124455 |
| 根 Transform 的源 fileID | `2107845040278215411` | 同左 |
| 根位置 | `(221.41153,16.001434,82.0318)` | `(203.2,15.851425,23.5)` |
| 根缩放 | `(3,3,3)` | `(3,3,3)` |
| Pawn 源组件 fileID | `2898261118407030116` | 同左 |
| `_agentId` | 继承基础值 `1` | 场景覆盖为 `2` |
| `_pawnConfig` 实际文件 | `Assets/SO/Agent/SO_Agent__Ice_PawnConfig.asset` | `Assets/SO/Agent/SO_Agent__Earth_PawnConfig.asset` |
| SO GUID | `a932304332ad443e9a3557f6df43bf34` | `1e6887c5fe8e847ed92bbafe8721b948` |
| HP / 防御 / 移速 | `300 / 10 / 8` | `270 / 80 / 12` |
| 发现开启 / 范围 / 间隔 | `1 / 200 / 0.5` | `1 / 200 / 0.5` |
| 停靠距离 / 交互距离 | `0.25 / 0` | `0.25 / 0` |
| 战斗配置 | `Assets/Resources/Agent/Combat/SO_AgentCombatStyle_Ice.asset` | `Assets/Resources/Agent/Combat/SO_AgentCombatStyle_Earth.asset` |
| `_enableDecisionModule` | 继承 `0` | 继承 `0` |

Agent_02 对 Pawn 的 `_pawnConfig`、Combat 组件 `9126527795397233563` 的 `_styleConfig` 均有明确场景覆盖。不能因为二者的 `m_SourcePrefab` 相同，就按两个冰系角色评估 AI 和性能。上表是初始化前的序列化结果；天赋、战斗和运行时配置应用后的属性另外采集。

## 4. Zone：额外六个实例已找全

原场景直接保存七个 Zone：渔村、员工宿舍、龙骨礁、奇点塔、员工食堂、实验室、雨林。下列六个对象另由 Prefab 提供组件，不是这七个组件的 stripped 别名。

| 场景中的实际名称 | 实际 Prefab 文件（位于 `Assets/Prefabs/Rooms/`） | 场景实例 fileID / 行号 | 源 Zone 组件 fileID |
| --- | --- | --- | --- |
| Zone-员工宿舍2 | `Zone-员工宿舍.prefab` | `16266171` / 725 | `6478811322877808816` |
| Zone-员工食堂2 | `Zone-员工食堂.prefab` | `73424252` / 6895 | `6692875077452993065` |
| Zone-雨林2 | `Zone-雨林.prefab` | `85232160` / 8050 | `5975251725602802287` |
| Zone-渔村2 | `Zone-渔村.prefab` | `327631923` / 20679 | `5100383301846709023` |
| Zone-奇点塔2 | `Zone-奇点塔.prefab` | `632161523` / 39001 | `2638425311836981089` |
| Zone-实验室2 | `Zone-实验室.prefab` | `2058718184` / 116912 | `2588706362420184269` |

六个实例的父级均为场景根，源根物体 active、Zone enabled；覆盖没有关闭它们，也没有移除根或 Zone 组件。多个实例确实删除了 Prefab 内的子物体，不能继续按完整源 Prefab 的子物体数量估算负载。

据此静态配置可以解释 **7+6=13 个 Zone**，与用户 Profiler 的 13 次 Update 相符，仍不把静态审计当成对那一帧的运行时捕获。

还发现六对同名区域的 `_targetId` 相同：例如员工宿舍及员工宿舍2 都为 `Zone_f9850db9ca424819b1bf5efafbb38f79`，其余五对同样从源 Prefab 继承同一个 ID。Registry 在 Play Mode 会重生重复 ID 并告警，因此 S05 的范围包含 Zone，不能只查资源群、敌人群。保留两个区域是否为布局意图需要结合实际内容判断；本轮不因重名直接删除任一实例。

## 5. 资源群：成员覆盖和 Zone 覆盖可以直接解析

实际基础文件为 `Assets/Prefabs/Cluster/ResourceCluster.prefab`，GUID `38d4f216b15cd4fe78905cd46307a61a`，群组件源 fileID `5466805027850277127`。基础配置为 `_resourceTier=0`、一个显式资源成员、`_showNavigationCandidateDebugObjects=1`；该成员通过 stripped 引用指向 `Assets/Prefabs/ItemPrefabIn3D/LootBox_1.prefab`。

例如员工宿舍下的 `ResourceCluster_A`，场景实例 `806267694`、场景行 53195，实际覆盖为：

- `_zone={fileID:20146627}`，对应直接场景对象的员工宿舍 Zone。
- `_resourceTier=1`，成员数组长度改为 `4`。
- 四个成员场景 fileID 分别为 `53928295`、`5051451073098777644`、`1479920360`、`1815880655`。
- 四个成员均能通过对应的 stripped 记录，追到 LootBox_1 的源 GameObject `2244447434356904958`、GUID `4971d3cf12ab63c4183454288ef16c4d`。其中嵌套实例同样有来源，长 fileID 不等于损坏引用。

此次逐实例核对还得到两个需要加入 P0 的配置事实：

| 配置项 | 精确位置 | 影响和待验证项 |
| --- | --- | --- |
| 父级区域和显式 `_zone` 不一致 | 实例 `672118188`，场景行 40146；父 Transform=`279692724`（龙骨礁），`_zone=922218180`（员工食堂） | ResolveZone 优先使用非空显式引用，所以业务归属是员工食堂。是否误接线需结合内容；不能仅按 Hierarchy 把它算给龙骨礁 |
| 唯一资源成员被覆盖为空 | 渔村 `ResourceCluster_B`，实例 `1566286469`，场景行 98904；`_resourceMembers.Array.data[0]._entityObject={fileID:0}` | 源数组只有一个成员，场景没有扩大数组，所以最终显式成员为一个空槽。需检查实际箱子是否失去目标归属、群是否永久未完成；不能把零引用误当成未展开的 Prefab |

`_showNavigationCandidateDebugObjects=1` 也属于当前正式配置，后续采样需包含候选调试物体创建的次数和成本，不仅关注路径查询。上述问题分别纳入 S09/S10，当前不宣称已经实测出卡死。

## 6. RaidFlow：三处真实来源和字段已找到

| 来源 | 组件 fileID / 源文件行号 | MissionName |
| --- | --- | --- |
| 场景直接对象 `IslandWallLayoutRoot/RaidFlowController` | `1578503827` / 场景 100495 | `Large Island Wall Layout` |
| `Assets/Prefabs/RaidFlowController.prefab`，场景实例 `2102010822` | `8501682771764213657` / Prefab 36 | `Large Island Wall Layout` |
| `Assets/Prefabs/Canvas.prefab` 内的 `GameManager`，场景实例 `720423942` | `976869042271210373` / Prefab 14039 | `MVP Raid` |

三份序列化配置都是 `RestartKey=114`、`_successScreenPrefab.fileID=0`。场景的两个 PrefabInstance 没有覆盖这些字段，也没有移除这些控制器。`RaidFlowController.Awake()` 对重复项执行 `Destroy(this)`，所以只能由运行捕获确认哪一个先成为 Instance，不能从 YAML 文档顺序推断。三处来源和配置已经明确，剩余未知的是生命周期胜出者。

## 7. 本轮 Review 回写

- 第 1、2、4、5 项已由用户确认，沿主规划边界执行。
- 第 6 项已确认：**满包后 Agent 自主撤离，保留箱内剩余物品**。测试程序只处理背包，不能代发撤离指令或把箱子标为空。
- 第 3 项的事实核对已完成；接线修复方向见第 2.3 节，仍保留为待用户 Review 的决策，未修改场景。
- 后续审计固定采用“真实源文件 + 场景实例覆盖 + SO 配置 + 运行时结果”的链路，并保存移除/新增记录。新增 S09/S10 先取证，不将配置疑点直接升级为已复现 bug。

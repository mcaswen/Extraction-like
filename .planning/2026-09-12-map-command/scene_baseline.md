# P0 真实场景基线

> 当前基线更新（P2b，2026-09-12）：用户已修复错误的空“渔村2”区域。`Logs/AgentReproduction/20260912-220831-959` 重新读取已保存的真实场景，确认 **7 Zone、28 群**，各 Zone 均有群，群数/类型/稳定身份及实际 Prefab 引用保持，归属诊断为空。4 项采集回归全部通过。以下 P0 的 8 Zone 表格保留为历史记录，不能再作为当前场景预期。

更新后的场景指纹为 `c216c62c522bba24b146d1ad3f359938`，导航烘焙指纹仍为 `dd2d6ed39b1e8aadb7aec3785561311f`。本轮 38 次候选采样、35 次初始可达查询、756 次有向群间查询，查询工作 142.55 ms。27 个群可从初始 Agent 到达；龙骨礁撤离群仍是合法采样但初始不可达的独立导航分量。用户此次场景修改由用户持有，Agent 未修改场景或重烘焙 NavMesh。

用户随后更新了导航烘焙。P2b4 的 `20260912-223226-432` 已使用最新文件重做采样、全对测量和实际布局验收：场景指纹 `436c7a5c609ed5fe76a489060eb18e84`，导航指纹 `17e80a2593b3808e59919818228226fd`。7 区域/28 群、龙骨礁撤离独立分量的结论保持。上述旧指纹只对应前一轮证据；本轮未替用户修改或提交场景、导航资产。

审计运行：`20260912-191826-063`，源码 `fcb6df1`。复用保留 Editor PID 11628，SC00 证据 PASS，Editor 返回空闲、源输入未变；没有进入新地图 Play Mode 或验证整局。

## 场景和规范群

`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`：8 个 Zone、28 个静态群、2 名 Agent、32 个箱子、30 个出生点、2 个撤离点。

| Zone | 群数 | 资源群 | 敌人来源群 | 撤离群 |
| --- | ---: | ---: | ---: | ---: |
| Zone-渔村[14] | 3 | 2 | 1 | 0 |
| Zone-渔村2[15] | 0 | 0 | 0 | 0 |
| Zone-奇点塔[16] | 4 | 2 | 2 | 0 |
| Zone-实验室[17] | 4 | 2 | 2 | 0 |
| Zone-雨林[18] | 7 | 3 | 3 | 1 |
| Zone-员工食堂[19] | 3 | 1 | 2 | 0 |
| Zone-员工宿舍[20] | 3 | 1 | 2 | 0 |
| Zone-龙骨礁[21] | 4 | 1 | 2 | 1 |

层级和 Zone 列表双向绑定错误：0。场景静态无独立 ActiveEnemyCluster；运行时的来源/活跃群应规范为同一地图节点。

群 TargetId 共 3 个不同值，存在 3 组重复：

| TargetId | 实例数 |
| --- | ---: |
| `ResourceCluster_6337291b19c540d899f77b3fd15d3d5b` | 12 |
| `EnemySourceCluster_3faf0caa7e9d48e589988c95afb3bac3` | 14 |
| `ExtractionCluster_39c2d8cee6694b72bcb8e98b74ea4c84` | 2 |

地图不能直接拿这些重复 TargetId 作为持久 nodeId。已有 Registry 会在运行时改重复 ID；生成器应以实际场景对象和 GlobalObjectId 建稳定地图 ID、保存直接绑定，避免以名称或运行时随机 ID 关联布局。

## Prefab、出生和导航

- ResourceClusterAuthoring：`Assets/Prefabs/Cluster/ResourceCluster.prefab`
- EnemySourceClusterAuthoring：独立场景对象、`Assets/Prefabs/Cluster/BossSourceCluster.prefab`、`Assets/Prefabs/Cluster/EnemySourceCluster.prefab`
- ExtractionClusterAuthoring：`Assets/Prefabs/Cluster/ExtractionCluster.prefab`

| 出生点直接引用的真实敌人 Prefab | 数量 |
| --- | ---: |
| `Assets/Prefabs/Enemy/Pawn/Common/Pfb_Enemy_Common_AnchorSentinel.prefab` | 14 |
| `Assets/Prefabs/Enemy/Pawn/Common/Pfb_Enemy_Common_TidalAberration.prefab` | 16 |

以上只是出生点本身的直接引用。正式 `EnemySpawnPoint.ResolveEnemyPrefab` 优先取来源群对应槽位，再回退直接引用；来源群实际配置另包含 `Assets/Prefabs/Enemy/Pawn/Common/Pfb_Enemy_Common_AncientStrander.prefab`、`Pfb_Enemy_Common_ModernStrander.prefab` 和 `Assets/Prefabs/Enemy/Pawn/Boss/Pfb_Enemy_HunterBoss.prefab`。所有非空来源槽位均指向 Enemy/Pawn 资产，不能把直接引用表误读为实际只有两种敌人。

P0 当时记录 Zone-渔村2 没有群；该错误区域已由用户在 P2b 期间修复，现已不存在。生成器仍应支持合法空区域，不能把历史错误区域硬编码回来。

唯一 NavMeshSurface：agentTypeID=0，GenerateLinks=False，烘焙数据 `Assets/Scenes/Scene_DB/Scenezl_Final 1/NavMesh-NavMesh Surface.asset`。
当前场景组件统计没有 NavMeshLink/OffMeshLink，有 14 个 NavMeshModifierVolume 和 2 个 NavMeshAgent。这只能排除当前显式单向 Link 配置，不能单凭它证明所有群互相可达。

出生在 EnemySpawnPoint.Start 执行，HasSpawned 先于 Prefab 解析和生成结果置位。EnemySourceCluster 的完成要求 ActiveEnemyCluster.HasRegisteredEnemy 且 HasBeenCompleted，空列表本身不是完成。P3 仍要用来源就绪/注册事实区分失败出生和已经清群，不能靠等待固定几秒。

## 输入和遗留问题

当前静态组件有 1 个 PlayerInputManager；无旧 RoomUIManager/AIIntentController、无 MapGraph 组件。正式迁移对象是当前点击入口及 Decision/Discovery，自主路由不能遗漏。
现有审计仍记录 2 个 Missing Script：`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity/Spline[29] count=2`。该既有问题先记录，不因本次地图功能直接删除未知组件。

## 下一阶段边界

P0 的静态场景/Prefab/双向 Zone 绑定已重新核实。现有 SC00 不采集群锚点到 NavMesh 的实际完整路径或 Agent 的完整 areaCost，因此这些属于 P1 共用导航段查询和 P2 生成器的必验输入；尚不能给出最终群间连通图，未将未查项计为通过。
P1 首先建立可序列化图数据、导航成本与纯路径契约，保持旧 MVP 兼容；真实场景锚点采样/全对成本在同一能力上完成。

原始证据：`Logs/SceneRaid/20260912-191826-063/scene-audit.json`、`report.json`、`process.json`、`editor-ready.json`。

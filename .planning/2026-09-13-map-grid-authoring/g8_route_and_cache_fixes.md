# G8 发布图路线阻塞、导航缓存修复

2026-09-13。依据 G7 实测报告，用户要求修复两项问题，沿用已确认的自主小规划、实现、测试/review、提交闭环。

## 目标、约束和文件边界

修复存活 Agent 在龙骨礁 EnemySourceCluster_A 到达锚点后持续 Unreachable，以及相同已保存导航重新加载后无法复用成本。保持玩家优先、反击恢复、清整群再继续、真实射线/范围/墙体约束。战死属于预期；不删群、不改布局连线、不以跳过敌人掩盖处理失败。

已阅读地图架构审查、G7、路线 Resolver、群指令 Factory/候选扫描、DirectiveValidation、CombatApproach、Fingerprint/CostService、相邻 SceneRaid 探针及缓存/交战测试。按现有职责修复，不引入新的路线系统或反向依赖。

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidEnemyProcessingProbe.cs`（含 meta）：仅自动验证编译条件下，在失败事件记录群内每名敌人的姿态、血量、导航和拒绝原因，独立于原根路线/地图显示证据。诊断候选查询不能修改世界或实际指令。
- Extend `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidRouteEvidence.cs`：失败时调用有次数上限的探针；普通帧采样不增加路径查询。
- Reuse `Assets/Scripts/Gameplay/Targets/Input/TargetClusterDirectiveFactory.cs`、`TargetClusterDirectiveCandidateSelector.cs` 和 `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphRouteTargetResolver.cs`：保留现有候选扫描/适配边界，根据探针决定是否需要修改。
- Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentCombatApproachQuery.cs` 或对应已定位的交战文件：只有程序化证据确定计算缺陷后才修改，继续验证真正能站立和射击的位置。若证据指向场景出生配置，使用隔离维护用例生成精确修正，保留 Prefab 和其他配置。
- Extend `Assets/Scripts/Gameplay/MapGraph/Binding/MapGraphNavigationFingerprint.cs`：核对原生导航数据顺序对指纹的影响；如需规范几何顺序，Create 同目录 `MapGraphNavigationGeometrySignature.cs`（含 meta），纯几何签名独立于场景采集和成本缓存。保持精确几何、区域及链接变化失效，不通过数量/Bounds/近似容差冒充等价。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCombatApproachTests.cs`、`MapCommandNavigationCacheTests.cs`；Reuse `MapCommandRuntimeCacheTests.cs`、`MapCommandPublishedGraphTests.cs`、`MapCommandSceneRepairTests.cs` 的 ValidateOnly 缓存刷新，登记新增用例到 `tools/agent-repro/cases.json`。必要的实际场景定点构造独立放 `MapCommandEnemyProcessingTests.cs`（含 meta），不进入生产流程。
- 仅在缓存实现稳定后通过现有事务刷新 `Assets/SO/MapGraph/SO_MapGraphDefinition_Scenezl_Final1.asset` 的导航证据，程序对照所有布局/节点/连接保持。源场景只在证据确认配置缺陷时改动。

## 实施与验收

1. 先补失败探针，4× SC02 稳定采集当前缺陷；构造最小交战反例和导航注册/保存重载顺序反例，先确认旧实现失败。诊断、结果写入本节。
2. 根据反例实施局部修复，跑受影响的交战、导航缓存用例，保留墙体、过远、错误楼层、真实几何改变、profile/锚点变化等反向条件。若需要调整方案先明确具体职责判断，不静默改规则。
3. 刷新保存缓存，复验实际保存/加载零补算及 52 方向、702 对路线；SC02、SC10/MR02 实际场景闭环，保留 Editor，测试操作背包。不能以采集 PASS 代替整局通过，死亡独立处理。必要时截图核对地图表现。
4. 归档修复前后证据、规划结果和架构审查，中文提交。4× Editor 帧时只诊断，性能变化只在相关热路径有变化时追加必要测量。

## 结果

进行中。候选扫描已存在，当前失败发生在到达群后生成/验证敌人处理指令，先采集逐敌人原因；不得直接归因于缺少扫描或 3 m 的角色 baseOffset。

### G8a 诊断收敛

SC02 `20260913-224234-988` 再次复现，68.56 秒的逐敌人探针确认两个瞄准点被同一个 `Torus/Range` BoxCollider 包围，世界尺寸约 151.38×0.52×183.54 m。两名敌人到达路径均完整；正式及独立扩大取样都找不到能穿过该实体碰撞盒射击的位置。场景 YAML 确认 Range 只有 Transform 和 BoxCollider，未被其他序列化对象引用；父 Torus 已有真实 MeshCollider 和渲染网格。

修复归属改为场景精确配置：只关闭场景 BoxCollider `fileID:959548491` 的 enabled，保留对象、Transform、群、Prefab、父模型真实碰撞体。不需要改候选扫描、交战规则或失败重规划。新增 MapCommandEnemyProcessingTests 用同一实际场景对照开/关该盒，正式 Resolver 应只在关闭后恢复；先在旧保存态验证失败，再应用一字段修正。

### G8b 实现和定向回归

旧实现反例：`20260913-224939-069` 场景用例证明启用盒时拒绝、关闭即恢复，但保存配置仍启用，失败；`20260913-225009-599` 相同 NavMeshData 交换注册顺序后指纹从 22096c… 变为 15d52a…，失败。

场景已仅改上述 enabled 一字段。MapGraphNavigationGeometrySignature 规范顶点和三角形枚举顺序，三角形只循环旋转、不逆转，保留全部精确坐标、区域、绕序、重复几何；正负零统一，非法数据返回空签名。Fingerprint 更新版本、组合原场景 Surface/Link 签名；原缓存服务、活动链接补验和 profile/锚点约束不变。复杂度 O(V log V + T log T)，仅在原启动/导航变更采集点执行，无逐帧扫描。

定点场景最终用实际缩放后的 baseOffset 复现角色高度。`20260913-225357-350` 1/1、`20260913-225434-992` 缓存 8/8 通过，包括注册顺序、精确几何/区域/绕序变化、链接及保存重读。RuntimeCacheTests 仅扩展真实场景签名耗时/规模证据，零补算断言保持。下一步仅以 ValidateOnly 刷新已保存缓存，独立对照布局及连接完全相等，再跑实际场景。

缓存维护调整：隔离 ValidateOnly `20260913-225642-761` 通过，但导入保护发现它会重新选择一个群锚点，该产物未导入主项目。Extend MapCommandSceneRepairTests.cs 增加显式 `RefreshGraphBakeFromSavedBindingsWithoutChangingLayout`，复用原 CostService 对当前 26 条保存绑定补算，原样传回全部图配置，仅在隔离副本保存 SO，断言场景字节不变。该职责属于已有隔离维护，不改生产作者事务；导入时再次逐字段比较布局、绑定，仅允许地图版本和导航证据变化。

### G8c 实场验证和审查调整

首次固定绑定维护 `20260913-230101-262` 通过并导入。RuntimeCache `230233-051` 零补算、发布图 `230302-245` 全部路线、视觉 `230329-921` 通过。SC02 `230233-280` 1 号撤离结算，2 号战死，路线/终局合同均通过，零运行时 Error，未触发处理失败探针。

代码审查发现仅按三角形坐标排序还需保留原始顶点共享关系。最终签名使用坐标排序后的顶点索引，三角形仍写索引；同坐标的独立顶点不合并，重编号有歧义时保守失效。新增共享顶点/独立重合顶点反例，避免坐标相同却拓扑不同的输入被视作同一份几何。该调整仍归既定纯签名文件，需重新跑缓存定向回归、刷新缓存并复验最终场景。

### G8d 最终结果

单纯按旧索引区分同坐标顶点的中间实现仍受注册顺序影响，`230615-636` 的 7/8 保留为审查失败证据，后续维护产物未导入。最终以有向邻接面内容稳定区分顶点，CSR 连续缓冲保留全部共享关系；存在不可区分的重合顶点时仍保守失效。`230914-684` 8/8 通过，`230947-325` 固定绑定维护通过，导入 revision 7，逐字段验证全部布局不变。

最终零补算 `231201-245`、全路线 `231231-300`、实际群处理 `231259-520`、真实截图 `231330-997` 均通过，合计 12 项定向测试，另 1 项维护。源码、场景和资产输入已核对，场景仅一字段修改。

SC02 `231201-495` 两名 Agent 撤离结算，路线/终局合同 PASS；原始报告有一次 LostSight 子指令失败，随后同一群被清除、同一根路线继续并完成，保留 ISSUES_OBSERVED 和原计数，不宣称零告警。SC10/MR02 `231338-815` 近/远移动、中间群、无效指令保留和 5 次反击恢复覆盖通过，最终正常战死，脚本/路线/终局合同 PASS。两轮无运行时 Error、无处理群 Unreachable 探针事件。

两张最终实际截图已查看，地图样式正常。运行时帧率仅 4× Editor 诊断；本轮完整 Capture 56.83 ms 为初始化/导航变化开销，不声称每帧开销或整体启动性能获益。两项原问题修复完成，完整证据和保留诊断见 [验收报告](../../outputs/map-grid-authoring/route-and-cache-fixes/validation_report.md)，架构审查见第 9 节。

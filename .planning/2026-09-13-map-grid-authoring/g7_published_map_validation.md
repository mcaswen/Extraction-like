# G7 新发布地图功能验证

2026-09-13。用户已发布 revision 5，磁盘资产为 7 区域、27 群、26 连接。本轮验证这份实际发布图，不重新生成或覆盖布局。已读地图大规划/P6、G6、SceneRaid Runner 和相邻测试。沿用自主验证、修复、文档和中文提交授权。

## 范围、文件归属和验收

1. Create `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandPublishedGraphTests.cs` 及 `.meta`：只读实际场景和发布图，检查严格几何、每条保存边的双向真实导航、缓存成本，独立 Floyd 全对照正式最短路线，记录连通和撤离可达性。作为发布资产验收独立文件，不往玩法或地图编辑器添加诊断状态。
2. Extend `Assets/Scripts/Editor/AgentReproduction/Tests/MapCommandVisualTests.cs`、`MapCommandPresentationTests.cs`：删除旧 28 群硬编码，按当前发布资产的完整节点/区域/边身份验收。真实 Play Mode 导出紧凑图和 M 图截图；现有焦点点击、缩放平移、失败保留及 Agent 投影构造继续复用。
3. Extend `tools/agent-repro/cases.json`：登记发布图只读用例。Reuse `MapGraphSceneSynchronizationTests.cs` 的保存身份用例、`MapCommandRuntimeCacheTests.cs` 的正式 Play Mode 缓存用例，分别窄筛选，不启动维护/场景修复组。
4. Reuse `Invoke-SceneRaid.ps1`、`SceneRaidRouteCommandDriver.cs`、`SceneRaidRouteEvidence.cs` 和独立合同：隔离场景工作区执行 SC10/MR02 近群、远跨区群、无效指令保持，再执行 SC02 4× 自主流程。背包由原测试驱动代操作，战死按预期终态处理。记录根路线、实际经过群、反击恢复、结算及错误，不以指令接受代替到达。

运行时源码和既有模块边界暂不改变；若发现实际缺陷，先记录证据和具体文件归属再修复。优先验证功能；4× Editor 的帧时仅诊断，不据此宣称 1× Player 平均 FPS 达标。常驻场景验证使用新的 `AnomalySearchMapValidation`，Regression 继续独立，用户使用的 Final 副本不覆盖。

## 结果

初步发现发布图缓存的导航指纹为 6947f5…，重新加载的 EditMode/Play Mode 为 ce55d6…，实际 profile 完全一致。零补算的 RuntimeCache 专用用例失败；几何和全部已发布边的真实双向导航正常。功能审计改为记录缓存警告、通过原有预算补算后再验证实际最短路线，缓存专用失败独立保留，避免把一个缓存问题派生成数百条不可达报告。当前不修改发布资产或生产策略，继续收集实际场景功能证据。

验证完成，功能整体未通过。最终 NUnit 12 通过、1 个缓存用例失败。7 区域、27 群、26 连接，52 个真实导航方向和 702 组起终点路线对照通过；场景身份一致，两张真实紧凑/M 地图截图已检查。

SC10/MR02 和 SC02 均由测试驱动在独立 Editor 以 4× 执行。指令脚本覆盖近目标、远跨区移动、无效请求保留和 4 次反击恢复，但容量撤离提前结束远目标路线，不宣称最终玩家目标到达。两轮均发现 1 号在龙骨礁 EnemySourceCluster_A 附近以 66–68 生命停住，群内仍有 2 敌人；到达锚点后处理报 Unreachable，重规划/换终点重复进入同一群，最终整局无法完成。SC10 的 2 号撤离结算；SC02 的 2 号正常战死，不作为缺陷。

保存导航指纹未命中产生 26 边、52 方向额外补算，原保存边长与重新实测最大差异约 0.00003132 m，暂未证明具体指纹变化根因。两项问题记录为未修复。本轮没有证据支持直接改动世界配置或运行时规则，结束测试阶段，不把生产修复混入本次交付。

最终源资产和测试代码哈希与对应运行一致，用户发布的场景和地图内容原样纳入提交。完整报告、原始失败、成功结果、截图和运行事件归档 [published-map-validation](../../outputs/map-grid-authoring/published-map-validation/validation_report.md)。架构审查见同目录 architecture_review.md 第 8 节。

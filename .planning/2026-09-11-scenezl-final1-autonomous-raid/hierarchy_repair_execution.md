# 场景层级接线修复小规划与执行记录

## 1. 本轮授权和边界

用户要求先修当前场景：从 Cluster 下检测对应 point / LootBox，修复 LineRenderer，再按 Zone 下的 Cluster 自动补列表和绑定。本轮按此已明确的层级规则实施，不等待之前第 3 项的泛化 Review。其他自主流程、RaidFlow 所有者和性能优化留在原规划阶段。

最新磁盘场景已经出现 ExtractionCluster Prefab 实例，地形、NavMesh 也有用户修改。之前的 `serialization_audit.json` 是旧快照，不能覆盖当前资产。此次使用当前文件建立独立副本，修复后只在源场景哈希仍匹配时回写，保留修复前场景；不修改地形、NavMesh 或 Prefab 源。

## 2. 规则和职责

- 编辑器场景修复，不增加逐帧扫描。后续可从菜单或命令行重复执行，结果写入场景序列化字段。
- ResourceCluster 收集最近所属 Cluster 下的 LootBoxEntity，ExtractionCluster 收集 ExtractionPointController，EnemySourceCluster 收集 EnemySpawnPoint。包含禁用子物体；嵌套 Cluster 是归属边界。对应列表按层级修正空项、重复和错归属，已有有效成员保留 ID、状态和相对顺序，新增成员按层级顺序补入。
- Cluster 绑定最近祖先 Zone；Zone 列表补入实际所属 Cluster，移除空项、重复和已归属其他 Zone 的项。没有祖先 Zone 的合法显式绑定保留并记录；没有任何归属的 Cluster 报告出来，不按空间距离猜测或创建新区域。
- ActiveEnemyCluster 的运行时敌人注册不改。场景已有的 ActiveEnemyCluster 参与 Zone 和轮廓修复。
- LineRenderer 必须独立归属于对应 Authoring，修复空引用/错误引用，缺组件时创建，缺材质时使用统一 URP 粒子 Unlit 材质（支持顶点色）；保留已有可用材质和 Authoring 的颜色、宽度、轮廓参数。复用正式 RefreshRangeShape，先刷新 Cluster，后刷新 Zone，不重新实现轮廓算法。
- 撤离群的范围 Collider 引用失效或误指其他群时，从所属撤离点恢复；不改变撤离点坐标、Collider、时长和检测系数。

## 3. 具体文件归属

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Create | `Assets/Scripts/Editor/GameplayTargets/TargetHierarchyRepair.cs` | 层级归属、序列化成员列表、Zone 双向绑定，返回修复/审计结果 |
| Create | `Assets/Scripts/Editor/GameplayTargets/TargetRangeRendererRepair.cs` | 范围 Renderer 和共享材质接线，复用业务轮廓刷新 |
| Create | `Assets/Scripts/Editor/GameplayTargets/TargetHierarchyRepairEntry.cs` | 菜单和批处理入口，加载/保存/重载、报告、幂等性和非目标字段验证 |
| Create | `Assets/Scripts/Editor/AgentReproduction/Tests/TargetHierarchyRepairTests.cs` | 嵌套层级、缺成员、旧绑定、状态保留、Renderer 误绑和重复执行的 EditMode 构造用例 |
| Create | `Assets/Art/Materials/Raid/M_TargetRangeLine.mat` | 多个轮廓共用的可持久化材质，复用 URP 包的 Particles/Unlit shader，不创建自定义 shader |
| Create | `tools/agent-repro/Invoke-TargetHierarchyRepair.ps1` | 复用隔离副本，执行定向测试和修复，备份/哈希检查后回写场景和新增材质 |
| Reuse | `tools/agent-repro/AgentRepro.Workspace.psm1` | Unity 版本匹配、独占锁和源输入复制 |
| Reuse | `Assets/Scripts/Gameplay/Targets/Authoring/GameplayTargetClusterAuthoringBase.cs`、`TargetZoneAuthoring.cs` | 原有公开 RefreshRangeShape 和数据定义；本轮不改运行时刷新频率 |
| Extend | `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity` | 只保存本次修复产生的成员、绑定和轮廓差异，保留用户最新布局 |

依赖保持 Editor → Gameplay/UnityEditor，Gameplay 不引用修复工具。SerializedObject 负责已有私有序列化字段，不新增反射写入的运行时测试后门。

## 4. 验收

1. 构造并执行嵌套 Cluster/Zone、禁用成员、重复/空/错归属、成员状态保留、Renderer 缺失和误绑用例。
2. 在当前完整场景副本中运行，记录修复前后数量、对象路径、成员引用、Zone 关系、Renderer 材质和顶点。
3. 保存、重新加载后独立核对成员和双向绑定，检查每个轮廓具有有效引用、材质、闭合/world-space 和有限坐标；再次执行不再改变关系或新增 Renderer。
4. 验证原有 Transform、LootBox/出生点/撤离点配置未改变，地形和 NavMesh 文件保持原哈希。构造测试为 EditMode，不将其当成搜打撤或 120 FPS 验收。
5. 失败后修复工具并复跑；成功后回写场景，更新本文和架构审查。

## 5. 实施结果

**已完成场景回写和本阶段验收。** 最终运行 `20260911-185640-719`，输入代码基线为用户提交 `7e7c1e8` 加本次改动。结果见 [hierarchy_repair_result.json](hierarchy_repair_result.json)，原始 XML、图像、日志、修复前场景和原子替换备份位于 `Logs/TargetHierarchyRepair/20260911-185640-719/`。

- 第一轮 6 个构造测试通过 5 个；失败来自测试先添加要求 Collider 的撤离组件，修正搭建顺序后 6/6 通过，真实 URP 顶点颜色渲染也通过。
- 完整场景保存/重载检查发现三个 Zone 的线被加载回调重新缩小：渔村 `24→248`、雨林 `40→216`、龙骨礁 `32→176`，并非浮点舍入。`TargetZoneAuthoring.CollectSourcePoints` 在子群轮廓缓存尚未建立时退回群中心，导致加载顺序影响范围。
- 小规划调整：**Extend `Assets/Scripts/Gameplay/Targets/Authoring/TargetZoneAuthoring.cs`**，读到空的子群轮廓时先复用 `cluster.RefreshRangeShape()` 初始化；不新增公共接口、不改变轮廓算法或更新频率。构造测试追加冷缓存场景，对比初始化前后的完整轮廓。这属于本轮范围线修复，覆盖保存/重载失败后再回写源场景。
- 中途工具问题也已修正：Windows PowerShell 的中文脚本改为 UTF-8 BOM；原子替换采用明确备份路径，临时文件移出 Assets，避免编辑器将其当成资产导入；报告 List 去掉 Unity 不序列化的 readonly 修饰，并由外层检查关键字段是否实际写出。

最终结果：

| 项目 | 结果 |
| --- | --- |
| 定向 NUnit EditMode 测试 | **7/7 通过**，包含层级边界、旧 Zone 清理、成员身份/状态、出生点去重、Renderer 独立性、真实 URP 顶点色、冷缓存轮廓 |
| 最新场景展开 | 8 个 Zone、28 个 Cluster、32 个 LootBox、2 个撤离点、30 个出生点 |
| 显式修复记录 | 62 条：5 个资源成员列表、5 个 Zone 列表、8 个新增 Renderer、8 个 Renderer 引用、36 个轮廓/材质刷新 |
| Zone 绑定 | 全部有有效归属，审计提示为 0；场景差异还保存了加载时自动解析出的两条原为空的显式 Zone 引用 |
| 撤离群 | **2 个**，分别位于 `Zone-雨林/ExtractionCluster` 和 `Zone-龙骨礁/ExtractionCluster`，各包含 1 个撤离点 |
| 范围线 | 36 个目标全部通过本地 Renderer、材质、闭合、世界坐标、有限顶点校验；128×128 图像像素断言通过，已查看生成的绿色范围线图像 |
| 保存/重载/幂等 | 保存后重新加载通过，再次修复没有新增修改或 Renderer |
| 非目标资产 | 原有 Transform 和撤离点配置不变；用户地形和 NavMesh 的哈希保持不变，没有写入本阶段提交 |
| 源文件保护 | 回写前完整输入清单比对通过，原子替换后场景 SHA256 核对通过 |

场景修复前 SHA256：`594cd2d51cb7f02271f300c2299e319b98a10bf6649ad31c8d9f6650546aca82`。

场景修复后 SHA256：`bbd30981ff7ba01d73def8476894e0c139a71b3af1c7db575a72109b2c2b3b04`。

实际场景差异复核：没有删除任何序列化对象，没有改动已有 Transform 文档；变化集中在目标组件、Prefab 接线覆盖、Renderer 及为引用它们补出的 stripped 记录。Unity 输出的空 `value:` 等行自带尾空格，保留原序列化格式，没有为通过文本空白检查重写整个场景。

本阶段未验证自主搜打撤或 120 FPS，也未处理 RaidFlow 单例、多角色读条或三个性能热点。后续以当前修复场景重新建立 P0 基线。

## 6. 后续逻辑测试加速

保留用户上一条要求：逻辑性 Play Mode 测试默认先验证 2 倍速，稳定后允许 4 倍速，保留 1 倍速关键回归；性能验收为 1 倍速。背包搜索使用 unscaled time，本身不会随 Time.timeScale 加速。具体时钟/超时配置在主规划实施时补齐，本轮场景编辑测试不需要模拟倍速。

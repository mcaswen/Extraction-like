# P4h：小地图无效范围扫描和隐藏图标更新

**已取消。用户明确“小地图就不用扣了，只要平均帧大于60帧就行”。下方仅保留草案和取消原因，不继续实施。**

## 启动条件和证据

P4f 记录 `RaidMinimapController.Update` 单次峰值约 5.27 ms。源码每 0.75 实际秒扫描区域或所有 Renderer，计算仅供全屏地图使用的范围，但全屏地图关闭时仍扫描、逐帧更新隐藏 FullIcon。当前场景及 Prefab 未找到 RaidRegionMarker 对应 GUID，因此实际走 Renderer 回退。P4g 的正常速度数据返回后，若尾部仍不达标，则执行本小阶段；不能仅因文件存在就继续扩大优化范围。

## 文件和职责

- **Extend `Assets/Scripts/Gameplay/Raid/RaidMinimapController.cs`**：全屏地图隐藏时不计算其范围、不写隐藏图标坐标；打开时先更新范围和图标再显示，打开后继续按原 0.75 秒刷新动态范围。小地图标记发现、世界位置跟随和可见性保持。复用 aliveIds/remove 缓冲；查询无需实例 ID 排序时使用 Unity 2022.3 的无排序查询，保留原活跃/关闭筛选语义。新增局部 ProfilerMarker 只用于证明范围查询次数。
- **Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidMinimapPerformanceTests.cs`**：实际 Canvas、Renderer、敌人标记构造；隐藏时范围扫描为零、首次打开同步当前范围、打开后移动/生成对象保持刷新、小地图仍跟随位置和标记增删。不复制正式坐标算法。
- **Reuse `ReproductionTestFixture.cs`、`World/TestNavMeshBuilder.cs`、`World/AgentFactory.cs`、`World/EnemyFactory.cs`、`RuntimeFixtureAccess.cs`**：实际运行环境和只读/构造适配。计数器先用已知调用校准，测量期间不手动写结果。
- **Extend `tools/agent-repro/cases.json`、`README.md`、本目录结果/审查**：定向用例和新旧正常速度数据。

此阶段仍由同一个地图展示组件拥有 UI 和范围，不新增全局注册表，不向敌人/箱子/出口注入地图订阅，不缓存全场景 Renderer 到永久失效。保留打开状态下动态地形的原刷新语义，不调整地图大小、范围算法或游戏目标决策。隐藏时“范围值暂时旧”不外露，打开前同步保证玩家看到的结果一致。

## 验收和风险

先加局部观测 Marker 和真实隐藏扫描反例，取得红灯后修改。检查打开当天新增/移动的远端 Renderer，验证第一帧使用最新范围；保持区域优先、Renderer 回退和过滤规则。开启地图时不减少动态刷新率。测试后跑 1 倍速原场景，固定预热后保留所有慢帧，比较 GC、P99、1% Low、最大帧；不因平均 FPS 已够就忽略尾部。

如果首次打开范围扫描造成超过门槛的峰值，保留失败，下一步再评估分帧范围生成或场景明确边界，需要另列职责和数据一致性方案，不在本阶段静默引入永久范围缓存。

## 实施结果

仅完成观测 Marker 和首轮构造，`20260911-234959-560` 为 3 项、2 个红灯，没有实施优化。收到用户停止要求后等待该轮退出，撤回两个 Marker 行，删除未提交的新测试及 meta。小地图恢复到阶段前源码，日志保留，不再追绿；当前性能口径见 [P4i](p4i_average_fps_scope.md)。

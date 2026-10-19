# P0 基线与审计：执行记录

基线：`48b6209`（大规划修订后的 dev）。目标见 [大规划](task_plan.md) 第 8 节 P0。

## 1. 范围

1. 时长批跑工具转正进仓库：`tools/agent-repro/duration/`。
2. D1 方案 b：现有调试视图和 I 键打印只在编辑器和开发构建中运行；周期性调试视图只用安全读入口。
3. D2：审计并处理"读取在场角色带出价值"的副作用。
4. 固定遥测禁用调用清单的第一版，从调试视图开始覆盖。
5. 用 10-18 基线的 16 个种子重跑，形成重构前的新基线，并与旧基线对比。

## 2. D2 审计结论

调用链：决策控制器每 0.5 秒调用 `TryGetCarriedLootSummary` → 在场角色走 `TryGetCurrentInventoryRevenue` → `CreateCharacterInventorySnapshot` → `SyncCharacterContainerRuntimeState`。这一步对每个装备槽调用 `SyncEquippedItemRuntimeDataFromGrid`，产生两处写入：

- 用网格的新快照**替换**已装备容器的运行时内部物品列表（`SyncInternalContainerState`）；
- 网格的 `ExtractCellStateData` 会把特殊格状态写回网格自己的运行时状态（`SetCellStates`）。

两处都是"界面 → 模型"的同步，内容与界面一致，但会提前刷新其他读者看到的缓存；同时每次都要克隆整套背包数据。结论：**有副作用**，按 D2 由实现方处理。

处理：新增 `EquipmentSlotUI.PeekInternalItems()`，它返回同步后会写入的同一份数据（有关联网格时直接读网格，否则读运行时状态），本身不写入任何状态。在场角色的带出价值改为逐槽按原快照口径累加：背包取内部物品，没有背包时取松散网格；头、身、脸、耳机、图腾两槽计本身（容器除外）和内部物品；战术背心与原口径一样不计入。不在场角色的快照路径不变。

验证：`DebugReadOnly` 组断言汇总前后装备内部物品列表是同一引用，数量和价值正确，并与同步快照的结算口径 `GetCurrentRunRevenueValue` 相等。

## 3. D1 处理

- **调试视图**：组件和序列化字段保持不变，避免正式构建出现序列化布局不一致；`Awake`、`OnEnable`、`Update` 的逻辑只在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 下编译，正式构建 `Awake` 中自行禁用。资源群的"当前具体目标"改为按直线距离取最近的未处理成员（本轮搜索记录或全局已搜登记，都是纯查询），不再调用会刷新群状态并逐成员算路径的可达查询；不再显示导航停靠点。
- **I 键打印**：自动创建只在编辑器和开发构建中发生；读注册表改用 `ActiveInstance`，没有注册表时直接返回，不再新建。它是按键触发的诊断，成员级可达性明细仍保留（只在按键时发生）。
- **未处理**：背包自己的 I 键调试打印（`DumpBackpackCapacitySyncDebug`）不在 D1 范围内，保持原样。

## 4. 禁用调用清单（第一版）

周期运行的调试和遥测代码不得出现：`TryGetNearestReachableIncompleteResource`、`HasPendingResources`、`AppendNavigationDebugSnapshot`、`GetOrCreate(`、`TryGetFacts`、`TryEstimateRoute`、`TryCopyRouteNodeEnemies`、`.CanShootAt(`、`TryCollectExtractableItemsForAgent`、`TryGetExtractionInventorySummary`、`RefreshTarget(`、`SyncEquippedItemRuntimeDataFromGrid`。`DebugReadOnly` 组用源码扫描固定调试视图；P7 扩展到遥测、面板、叠加层和导演目录，并补充对黑板写入、指令生命周期修改、导航代理赋值和物理同步的检查。

## 5. 验证记录

全部在验证克隆的隔离副本中、Windows session 0 下以 batchmode 运行。

**定向回归**（验证克隆 `baebb79`，p0-a）：Decision 6/6、DecisionRoutePacing 5/5、SceneInventory 9/9、ClusterCommandInventory 11/11、MapCommandEntry 12/12、ResourceSearchRevisit 4/4 全部通过；DebugReadOnly 首次 1/2，失败原因是夹具默认没有装备背包，补上夹具背包后（`6cf18c3`，p0-b）2/2 通过。

**大脑推进基准**（`1dad853`，p0-d；5000 次推进求平均耗时，单帧额外推进 500 次与空闲帧比较 GC 分配）：

| 场景 | 状态 | 耗时（微秒/次） | 托管分配（字节/次） |
| --- | --- | --- | --- |
| 探索空闲 | Explore | 4.19 | 200 |
| 搜索接近 | SearchResource | 18.78 | 3230 |
| 战斗冷却 | Combat | 16.07 | 676 |

测量方式两次修正：Unity 的 Mono 不实现 `GC.GetAllocatedBytesForCurrentThread`（恒为 0）；Mono 已用堆增量看不到小块分配（探索、战斗也读成 0）。最终改用每帧的 `GC Allocated In Frame` 计数器。

**时长基线**（`bbd2b68`，P0 改动已生效；批次 `dur-20261019-133801-p0-baseline`，4 倍速、观察 480 游戏秒、10-18 基线的同一组 16 个种子）：

| 指标 | 10-18 基线 | P0 新基线 |
| --- | --- | --- |
| 有效时长中位数 | 178.0 秒 | 163.4 秒 |
| 有效时长平均 | 180.7 秒 | 180.0 秒 |
| 最短 / 最长 | 153.1 / 232.3 秒 | 103.0 / 261.2 秒 |
| 双人撤离 | 16/16 | 15/16 |
| 阵亡 | 0 | 1（种子 13731，角色 1 在 110 秒） |
| 平均带出价值 | 6898 | 6897 |

同一种子两次运行的结果差别很大（例如种子 5731：180.9 秒对 221 秒；种子 7731：232.3 秒对 163.7 秒），说明实时对局在 4 倍速下没有逐局确定性，只能按分布比较。平均时长和带出价值几乎相同；中位数下降和一次阵亡可能来自 D1 去掉调试视图每 0.1 秒刷新资源群状态后的时机差异，也可能是噪声，单批无法区分。之后重构的对比以本批为基线；若 P6 的结果偏离超过阈值，再各补一批区分噪声。

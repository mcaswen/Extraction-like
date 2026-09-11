# 三个热点的复杂度和剩余耗时

分析基于提交 a4d6cd1 后的源码及 SC01 `20260911-203250-166`，本次没有再运行场景，也没有修改 Gameplay。复杂度描述代码规模增长，实测耗时描述本轮特定输入，二者不能互相替代。

## 为什么仍然只有约 41 FPS

10 秒预热后，连续帧平均间隔 24.528 ms；Discovery 0.1218 ms，两个 Pawn 合计 0.2923 ms，八个 Zone 合计 0.9218 ms，三段累计约 1.336 ms。三段平均 CPU 耗时已经不能解释整帧约 24.5 ms。当前没有完整拆开其他脚本、物理/动画、渲染、编辑器和同步等待，不能断言剩余都是 GPU 耗时，也不能把 CPU Marker 与墙钟/GPU 时间简单相减对账。

上轮主要降低昂贵操作的执行次数，复用缓冲，未改变所有扫描的最坏复杂度。Discovery 预热后 p99 2.4505 ms；Pawn.Facts 仍出现 12.4643 ms 单帧尖峰，Zone.Shape 出现 6.6371 ms 尖峰。平均改善不代表最大帧耗时达标。Facts 平均仅 0.0595 ms，不能仅凭一次尖峰就断定其属性算法有高复杂度或一定发生了 GC。

## 记号和范围

- A：Agent 数；C：扫描到的群数；R：资源成员总数；r：范围内资源数；m：当前资源群成员数。
- K：一个资源的停靠候选数，通常是 Collider 候选加 16 个外圈点及中心/配置点；D：相关对象层级/Collider 遍历规模。
- N：收集后候选数；e_i：第 i 个敌人群的成员数。
- P：一次导航采样和路径求解的成本；L：路径角点数；Q：一次射线及命中对象过滤的成本。
- c：某 Zone 的子群数；p：子群输入轮廓点总数；q：生成轮廓点数。

P/Q 不是 O(1) 承诺。Unity 的 NavMesh.CalculatePath 是同步查询，长路径可能影响帧率，官方建议限制每帧路径求解数量。Unity 导航采用 A*；使用优先队列、每个节点按标准有限图搜索处理时，可以用 O((V+E) log V) 理解图搜索规模，但它不是 Unity API 给出的实际最坏复杂度保证，导航采样、过滤和实现细节仍另有成本。

来源：[Unity 2022.3 CalculatePath](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html)、[导航内部原理](https://docs.unity3d.com/cn/2022.3/Manual/nav-InnerWorkings.html)。

## 源码逐段判断

| 位置 | 复杂度/频率 | 解释 |
| --- | --- | --- |
| `Gameplay/Agent/Runtime/AgentTargetDiscoveryController.cs:93` | 调度 O(A)，外加本帧实际扫描 Agent 的成本 | 每帧复制/检查 Agent；最多扫描 4 个 Agent 不是最多 4 次寻路，单个 Agent 仍可同步查询大量候选 |
| `Gameplay/Agent/Targeting/AgentTargetCandidateCollector.cs:44`、`Gameplay/Targets/Authoring/ResourceClusterAuthoring.cs:238` | 资源主路径约 O(C + R + r(D + K² + KP) + N log N) | 范围外仍遍历/刷新成员状态，但不寻路；范围内每成员尝试候选，候选生成中 AddUniqueCandidate 线性去重，累计最坏 K²。此式按已初始化的成员状态计算，不包含首次生成库存等冷启动成本 |
| 同 Collector 的敌人收集、`Gameplay/Targets/Authoring/ActiveEnemyClusterAuthoring.cs:38` | 群内去重最坏 O(Σ e_i²)，另加瞄准点、可见性/路径查询及候选排序 | AppendAliveEnemies 的循环内 List.Contains 是线性查找。不是全场必然 E²，而是各群平方之和。外层 HashSet 没有消除内层这部分成本 |
| `Gameplay/Agent/Core/AgentPawnRoot.cs:197` | 没有单一统一复杂度 | 身体固定字段同步接近 O(1)，但 Brain 的活动节点、导航、资源扫描、战斗检查都计算在其 Update 中，不能看入口函数短就称整个 Pawn 为 O(1) |
| `Gameplay/Agent/Navigation/AgentNavigationMotor.cs:33`、`AgentNavigationQuery.cs:24` | 不重算时监视近似 O(1)；重算 O(P + L) | 平稳目标通常每 0.1 游戏秒重算；目标/参数变化、路径丢失/陈旧、接近到达等会提前重算。因此是普通节奏约 10 次/游戏秒，不是任何输入下的严格上限 |
| `Gameplay/Agent/Navigation/AgentResourceNavigationResolver.cs:17` | 命中缓存仍 O(m)；到期重新做群查询 | 为验证缓存成员未完成，仍 foreach 全群。最多 0.1 游戏秒后重新查询；正常游戏时间流动的等待背包阶段仍可能周期性扫描整个群，没有做到纯状态变更驱动 |
| `Gameplay/Targets/Authoring/TargetZoneAuthoring.cs:78`、`Runtime/GameplayTargetRangeCache.cs:39` | 普通静态 Zone 每帧 O(c+p)；BoxCollider 覆盖一般 O(c) | 仍逐帧聚合子群，复制并逐点比较源轮廓。缓存省掉重建和投射，没有把输入检查变为 O(1)；Collider 从子层级解析时还需计入层级查找 |
| `Gameplay/Targets/Runtime/GameplayTargetShapeUtility.cs:37` | 轮廓重建 O(p log p + q)，投射 O(qQ) | 排序构建凸包，再平滑采样；每个输出点射线投射，并过滤命中对象。静态轮廓仍每 0.25–0.5 游戏秒重投射；动态敌人移动使群及 Zone 输入变化，可每帧重建 |

以上位置均以 `Assets/Scripts/` 为根。固定层级/小集合有利于平均耗时，但不应据此把组件层级遍历、物理查询或路径计算视为免费。

## 这次检查确认的剩余重复工作

1. **重复自动目标先验证、后去重。** `AgentDirectiveLifecycleController.cs:24` 先调用 Validate，到第 36 行才判断 SameTarget。资源校验又经 `AgentDirectiveValidationService.cs:45` 调用无范围全群查询，随后再做 Navigation.Check。已选中同一资源群也会重复支付这条成本。这部分包含在 Discovery 的提交调用中。
2. **短期缓存没有等同于具体成员持有。** ResourceNavigationResolver 命中还扫描成员，到期又选整个群；等待状态没有完全停止无意义查询。需要保持完成/移动/失活/位移失效语义，不能简单永久缓存。
3. **Zone 没有输入版本快路径。** 当前每帧收集全部已细分的子轮廓点，任何实际点变化都会触发构形；子群构形后 Zone 再构形。轮廓较大或敌人持续移动时，复制/排序/投射仍叠加。
4. **局部平方去重和空间查询分配仍存在。** 敌人群 List.Contains、资源候选 AddUniqueCandidate 各有局部平方成本；`CombatAimPointResolver.Resolve` 的 GetComponentsInChildren 数组、`TargetVisibilityQuery.ClearSegment` 的 RaycastAll/OverlapSphere 返回数组仍会分配。当前全帧约 90 KiB GC 不能全部归因于这些调用，需分配调用栈确认。

后续需要一面减少这些确定的重复工作，一面把整帧 CPU/GPU/编辑器/同步等待拆开。只继续压这三个函数的均值，不能据现有证据保证从 41 FPS 达到 120 FPS。以上是分析结果，不将新优化方向视为已经实现或测试通过。

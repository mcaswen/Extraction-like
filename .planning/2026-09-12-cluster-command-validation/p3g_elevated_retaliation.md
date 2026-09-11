# P3g：无导航锚点的高处敌人接近和无效反击循环

## 事实和规则

第三版最终矩阵槽位 4 `Logs/SceneRaid/20260912-061650-234`：Actor1 在 `(-226.6226,3.0318,-72.8233)` 受到高处 AnchorSentinel 连续伤害。敌人在 `(-210.8137,7.7654,-76.5035)`，没有就绪的 NavMeshAgent，双方平面距离约 16.23，角色射程 8。185.20 游戏秒起产生 50 次 Accepted → Failed:Unreachable，原搜刮被不断取消；最终战死与此执行故障分开记录。

接近查询目前只支持目标自身完整路径或其 partial path 终点。敌人身体/脚下无法在小采样半径内找到导航锚点时，没有 partial path，直接返回不可达，尚未考虑附近可达的地面射击点。另一方面 Lifecycle 将伤害来源的所有 Unreachable 都豁免成 Accepted，本意是等待导航重建，却也接受了真实无解目标。

遵循已确认规则：远程可跨高低差射击，必须满足实际身体/枪口射线、射程和自身完整路径；无法形成有效反击时保留当前有效任务。不能通过放宽空间约束、改生命或无限提交同一无效反击解决。

## 文件与边界

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ElevatedCombatApproachTests.cs` + meta：构造无 NavMeshAgent 的高处敌人，手动/受击指令在 1×/4×实际接近并造成伤害；真正过高目标的连续伤害不得替换原撤离，目标重新进入可行范围后可反击并恢复原撤离。墙体、枪口射程、过高目标提供阴性对照。复用现有 World/Agent/Enemy/Target 工厂，不向生产注入测试回调。
- Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentCombatApproachQuery.cs`：保留原直接完整路径和 partial 候选优先顺序；它们无法提供位置时，在角色当前导航地表高度、敌人水平位置附近查询有界的地面候选。中心和两个半径（射程 0.5、0.85）的八方向，共最多 17 个；按朝向角色方向优先。每个候选需要 NavMesh 类型/areaMask 采样、角色完整路径、按路径起点换算的实际身体/枪口位置、正确转向后的射线和射程验证。失败不能返回原敌人身体位置。候选校验提取为本文件私有方法，Buffer 继续由调用者持有，不引入全场注册/扫描。
- Extend `Assets/Scripts/Gameplay/Agent/Core/AgentPawnRoot.cs` 的原 `RecordCombatDamageInterrupt`：记录真实受击事实后，已有有效反击保持原目标；新来源先走既有 Validation，只有可执行或 NavigationNotReady 才向 Lifecycle 提交。不可行来源没有成为正式任务，不取消/挂起当前任务。此处是已有伤害来源转命令入口，不把筛选搬到 UI 或日志层。
- Extend `Assets/Scripts/Gameplay/Agent/Commands/AgentDirectiveLifecycleController.cs`：仅对伤害的 NavigationNotReady 保留既有有限等待豁免，不再豁免已经确定的 Unreachable。直接调用者仍收到正式拒绝，玩家请求和反馈契约保持。
- Extend `tools/agent-repro/cases.json` 注册精确构造；必要的真实记录点构造归同一测试文件，独立标为场景夹具，不改变正式 MC02 脚本。

依赖仍为 Pawn 伤害入口 → Validation / Lifecycle，Validation 和 Engage → CombatApproachQuery → NavMesh / 只读射线。没有新接口、候选缓存或状态所有者。候选只在原接近查询失败时尝试，空间搜索最多 17 个；每个候选路径成本取决于导航网格，不能写成整体 O(1)。已有反击保持时避免重复候选查询和生成新命令身份。

## 阶段和验收

先构造红灯，再实施上述两个已明确的缺口。相关回归包含旧七项战斗接近、反击保持/恢复、远敌/群后备、导航失败和无进展。所有阴性仍失败；真正不可反击的连续受击仍扣血，旧撤离继续实际移动，不能用零伤害制造通过。按记录点核对真实场景的可行射击位置，再复跑原 MC02。

完成后审查实际查询次数和 1× 性能；若实际没有可行射击点，只能保持旧有效任务，不能把不可达目标伪造为可达。第三版四局原始失败保留，修复后重新冻结同一 11 槽位及共享导航的自主回归。死亡仍不驱动数值修复。

## 结果

构造红绿、原场景记录点验证完成，真实 MC02 复跑进行中。

红灯 `Logs/AgentReproduction/20260912-062649-156`：9 项中 3 通过、6 失败。手动高处目标被拒绝，伤害高处目标 Accepted 后 Failed，真正过高的攻击者立即替换原撤离；墙体/枪口/过高阴性原本通过。按上述边界实现候选和伤害前置筛选，同时保留 NavigationNotReady 有限等待。

`Logs/AgentReproduction/20260912-062855-095` 51/51 相关回归通过。追加同文件三项：NavigationNotReady 的 1×/4×等待后恢复同一反击、原场景记录坐标的 Sentinel 通过真实 Motor 移动和真实子弹造成伤害。场景夹具只在准备时定位双方、关闭无关角色/AI，不注入命中或扣血，不将此构造冒充自然整局。场景用例需要图形上下文，分组登记开启。

`Logs/AgentReproduction/20260912-063236-222` 3/3：两种倍速的导航恢复继续同一反击；原场景 Sentinel 记录点正常走到 `(-216.79,3.04,-75.11)`，一发真实子弹令生命比例降为 0.875。没有绕过实际 Collider 或直接伤害目标。两份 XML 合并 54 个不同 Passed 用例，ElevatedCombatApproach 全部 12 个登记参数零缺失。前一轮阶段中途看到 50 个 case 目录，最终 XML 实际是 51，文档按最终值校正。

原 MC02 / 731 整局 `Logs/SceneRaid/20260912-063359-173`：PASS，7/7 步骤 COMPLETE，双人撤离，原始失败/运行错误/停滞均为 0，库存和仓库契约通过。4× 整局约 99.93 FPS 仅作诊断；最终 1× 性能仍待 P4。没有更换脚本、种子、目标阈值或战斗数值。

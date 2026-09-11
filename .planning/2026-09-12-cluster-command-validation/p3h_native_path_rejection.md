# P3h：实验室原生路径交付持续拒绝

## 已确认的边界

第四版最终矩阵 `Logs/SceneRaid/20260912-063855-616` 重现 P3d 的实验室问题。130.772–132.796 游戏秒连续 SetPathRetry，最终 SetPathDeadline；Actor2 从 x=-430.492 移到 -427.276 后停止，z 保持 197.984。2 秒恢复窗口阻止了无限等待，但没有修复现场问题，不能继续将 P3d 描述成根因已解决。

真实前置路径是 Box[0] 在 `(-423.689,0.008,214.317)` 搜完后，转向 Box[2]，后者接近点随角色位置从 z=210.079 持续变到 z=197.921。原动态构造从 (-444,3.008,197.984) 出发，缺少上述入射方向/路径历史。新日志需明确原生拒绝时被查询的路径是否有角点、起点与角色导航地表是否一致，不能先推断引擎内部原因。

## 文件和实施

- Extend `Assets/Scripts/Gameplay/Agent/Navigation/AgentNavigationQuery.cs` 的既有调用者 Buffer：保存最近角点数量，仅诊断；Check 原规则不变。
- Extend `AgentNavigationMotor.cs` 的现有条件编译失败日志：记录查询角点数量/首点/末点、路径状态、当前 hasPath/destination/remainingDistance。数组角点来自刚完成的 Check，避免用失败后重新计算的路径冒充原始输入。仅失败时构造日志，不增加正常 Player 输出或日志驱动逻辑。
- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidResourceNavigationTests.cs` 的动态资源接近用例：原起点、上一个箱子出发点、首次失败点三种；原自然步长和固定 captureDeltaTime=0.01 两种。只在构造里设置固定时间并 finally 恢复，4×时每帧游戏步长 0.04 秒，逼近原场景约 100 FPS 的移动步幅。真实场景和性能运行禁止此设置。
- Extend `tools/agent-repro/cases.json` 登记六种参数；原固定终点构造和 P3d 失败预算测试保留。

[Unity 2022.3 captureDeltaTime 文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Time-captureDeltaTime.html) 说明固定间隔仍受 timeScale 缩放，且不修改 unscaledTime。此构造没有背包计时，只用于导航重现，不将固定间隔产物作为 FPS 成绩。

先拿到原生失败的具体路径输入，之后按证明追加修复归属。不得通过 Warp 恢复、强制 SetDestination、放宽完整路径或提高期限规避故障。第四版两局继续保留，最终矩阵尚未完成。

## 结果

`064713-626` 六项均到达，其中两个固定步长构造各触发一次真实 SetPathRetry 后恢复。失败路径非空且 Complete，首角点等于角色导航地面位置，交付失败后 hasPath=false。

`065230-593` 原 MC01-E 正常死亡，零执行失败，此次没有到达持续拒绝现场，不作为问题已修复的证据。继续在现有动态构造中复用 `AgentResourceNavigationResolver.cs`，覆盖正式动作层的接近点缓存，替代原每帧直接重选，避免构造绕过相关生产路径；没有修改生产缓存规则。

缓存链 `065449-128` 六项通过，均无拒绝。进一步固定自然、0.008、0.01、0.0125、0.01666667 五种帧步长，与三个已知起点组成 15 项有限边界，检查经过导航边缘的离散位置；构造仍要求真实到达，不把固定时间当性能成绩。

`065641-179` 15 项中 14 通过，origin=1 / captureStep=0.01666667 稳定产生连续拒绝至 Deadline，末点 x=-426.525452、z=197.984161。增加一次仅在夹具失败后的诊断：新 NavMeshPath 计算/交付，再 ResetPath 后计算/交付，保留原 Failed 断言使诊断不能转绿。这是定位原生状态差异的受控实验，不进入真实场景驱动或生产恢复策略。

## 基于复现实验的修复设计

`065823-801` 过滤器未转义括号导致 0 用例，按基础设施失败保留。转义后 `065920-200` 再次红灯：新路径对象和 ResetPath 均无效。`070022-684` 静态完整路径、四种终点微调仍失败；`070121-586` 第一、二、三个拐点的短路径也失败。均保留原失败断言，没有把诊断操作算作修复通过。

`070243-498` 同一失败状态下，SetDestination 原终点，12 帧后 pathPending=false、PathComplete、hasPath=true，角色实际移动 5.96 米；随后同一终点 CalculatePath/SetPath 成功。已证明存在能够实际恢复的原生请求路径，未证明 Unity C++ 内部拒绝机制。

- Create `Assets/Scripts/Gameplay/Agent/Navigation/AgentDestinationRecovery.cs` 和 meta：仅包装一次已校验目的地的异步请求状态，核对 pending、完整路径、过期状态和实际路径终点。独立原因是原生请求协议与 Motor 的查询缓存、进展计时不同；不选择业务目标，不设置 Transform、Warp、速度或最终任务状态。
- Extend `AgentNavigationMotor.cs`：完整 Check 后 SetPath 拒绝才开始上述恢复；沿用原 `AgentPathAssignmentBudget.cs` 的固定 2 秒窗口。请求接受不等于路径恢复，只有完整且终点匹配才恢复 Moving。挂起时不连续覆盖异步请求；任务改令/Stop/导航失效清请求，暂停不消耗游戏时间预算。拒绝、无完整路径或到期仍失败。恢复后回到原查询与 SetPath 链，进展超时不放宽。
- Extend `NavigationExecutionTests.cs`：真实 NavMesh 上的异步完整路径/断开岛阴性、暂停和取消复位。场景原 15 个边界去除临时诊断干预，严格靠 Motor 走到原目标；登记全部参数到 `cases.json`。

这是基于反例调整先前“不盲目 SetDestination”的方案：入口仍必须经过完整空间校验，接受请求后还要验证真实完整路径，期限不变。依赖为 Motor → 查询 / 期限 / 原生恢复包装 → Unity，Gameplay 不引用测试。没有修改玩家命令公共接口、目标选择策略或场景。先跑原红灯，再完整导航相关构造、真实 MC01-E，完成审查提交后重新冻结最终矩阵。

`070609-329` 原确定性红灯 1/1 转绿，已移除所有夹具恢复干预：SetPathRetry 的下一帧 DestinationRecovered，随后原目标真实到达。新增六项原生请求完整/部分/错误终点（1×/4×）和两项原场景恢复期间暂停/改令构造；与全部资源导航、旧导航执行、远敌/群后备及高差战斗一起回归。首次命令用了未登记 Group=NavigationRecovery，被入口拒绝；随后使用现有 Navigation 分组和明确 TestFilter，没有新增虚假测试组。

`070843-109` 65 项中 61 通过，15 项原场景帧步长全部实际到达，暂停恢复通过。三项新夹具断言失败来自未经 NavMesh 采样的理想坐标：原生包装要求已采样目的地，平面和实验室地面与理想 y 有偏移；修正测试准备使用实际 NavMesh 命中，保留严格终点核对，未放宽生产容差。另一个旧高差导航恢复 1× 在 8 秒墙钟内未击中，补充该测试终态位置/导航/命令/生命探针，按原期限复跑，不直接扩大等待或修改伤害。

`071259-423` 10/10 通过，包含六种目的地协议反例、原生恢复中暂停/改令两项和旧高差恢复两种倍速。旧 1× 在 1.76 游戏秒实际击中、保持原反击身份，未再超时；首次超时机制未确认，不将单次复跑声称为已修复另一生产 bug。两轮 XML 合并 65 个不同 Passed 名称，四组全部登记参数零遗漏：17 导航、26 资源导航、10 群可达、12 高差交战。

原 MC01-E `071400-478`：证据 PASS、EXPECTED_DEATH，原始 Failed=1 为完整证据证明的已见后 LostSight，额外失败/运行错误/停滞均为 0，幸存者结算通过。本局未观察到原生恢复分支，不声称它覆盖了该分支；分支恢复由真实地形上的确定性构造证明。第四版两局已归档 `p4_runs_superseded_9d52840.json`，包含原始连续拒绝失败。阶段代码和架构审查完成，提交后冻结第五版原 11 槽位及 SC02 自主回归。

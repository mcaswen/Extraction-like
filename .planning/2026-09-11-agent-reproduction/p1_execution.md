# P1 小规划：指令生命周期、导航与反馈

- 状态：完成阶段实现和 Review；图形截图及全量重复回归在 P4。
- 目标：F1 反击后恢复撤离；F3 受击可打断资源；F5／R1 导航校验、失败释放和顶部反馈；R5 位移后重新验证交互。
- 验收：先保存修复前真实失败；修复后测试恢复、覆盖／取消、无效旧回调、断路、零距离、动态失败、位移与 UI。用户无需操作 Play Mode。

## 文件归属与复用

- Create：大方案列出的 `Agent/Commands/` 四文件、`Agent/Navigation/` 三文件、`Targets/Presentation/` 三文件及提示预制体。
- Extend：`AgentPawnRoot.cs`、`IAgentCommandReceiver.cs`、`AgentCommandRouter.cs`、`AgentPawnConfig.cs`、`AgentManualDirectiveLock.cs`、`AgentBrainTransitionRules.cs` 和 Move／Search／Extract／Engage／Action 基类；Dispatcher 与 PlayerInputManager；Discovery／Decision 提交调用点。
- Reuse：Intervention 底层存取、资源群真实成员／导航点、Registry、Raid 撤离、Inventory 公共入口。
- Create 测试：`World/TestNavMeshBuilder.cs`、`World/AgentFactory.cs`、`World/EnemyFactory.cs`、`World/TargetFactory.cs`、F1／F3／F5／导航风险／指令生命周期／反馈用例。按需要实现，不生成空文件。

## 依赖与控制流

提交 → 无副作用校验 → 接受后同步任务事实 → 动作执行 → 按 CommandId 报告终态。生命周期唯一保存活动任务与一份挂起撤离；新人工命令及死亡取消恢复。UI 只订阅结构化结果。导航查询不决定任务，Motor 不持有 UI 或选择策略。

## 实施时调整

1. 将已批准的 Perception 基础空间查询提前到 P1，以供远程指令接受前校验；P2 再接入所有敌人／弹体。避免先写一套临时射线算法。
2. 增补 `Assets/Scripts/Gameplay/Agent/AI/Factories/AgentBrainStateFactory.cs` 的 Extend：Combat 改由 Engage 节点统一处理有效性、靠近与开火，避免前置 Move 节点绕过“原地可射击，无需走到敌人脚下”的规则。归属仍为既有行为树组装层。
3. 指令 ID 必须唯一，终态只消费匹配 ID；自动重复提交同一有效任务应保持原身份，避免重置无进展时间。
4. 基础攻击间隔在本阶段一并移入 CombatController；技能集合与属性分离仍在 P3 完成。

## 实际结果

- 修复前证据：`Logs/AgentReproduction/20260911-010520-229`，F1 在 20 帧内切换宏状态 20 次，反击后仍保留 CombatDamageInterrupt 指令；F5 不可达路径被接受。未受伤撤离控制组通过。
- 运行器调整：跨 Domain Reload 的 UnitySetUp 在多帧测试中出现提前结束；改为隔离副本内关闭 Domain Reload（仍逐例进出 Play Mode、重建场景），增加最终契约检查点。`20260911-010147-691` 的 F1 通过缺少完整轨迹，不作为有效证据。P0 的单帧导航/物理证据仍成立，但多帧能力以修正后运行证据为准。
- 实现：已接入 Commands 四文件、Navigation 三文件、Perception 基础查询、任务事实原子写入、挂起/恢复撤离、ID 终态校验、动作失败上报、R5 位移重验、普通攻击节奏持久化和 HUD 提示预制体。自动选择调用点移除提交前事实写入。
- 首轮回归：`20260911-011339-769`，Smoke 1/1、F1 2/2、F5 1/1，Regression exit 0；后续补边界测试和界面校验。
- 文件补充：`Assets/Scripts/Editor/AgentReproduction/Tests/DirectiveLifecycleTests.cs`（生命周期/F3）、`NavigationExecutionTests.cs`（R1 运行期失败）、`ResourceDisplacementTests.cs`（R5 真背包开关与位移）。每个文件单独覆盖一类契约，未创建业务测试分支。
- 边界回归：`20260911-011652-805` 中 Lifecycle 3/3 通过；Navigation 的零距离到达和 R5 返回后交互失败，定位到正式 Agent.prefab 的 baseOffset=1。改用路径表面起点后，`20260911-012201-214` Navigation 2/2、`20260911-012315-921` R5 1/1 通过。
- 提示回归：`20260911-012353-962` Feedback 1/1，正式 Resources prefab 实例、成功/不可达失败文字、暂停时 alpha 淡入淡出及不挡射线均通过。尚未声称 nographics 验证了实际渲染。
- 运行器防挂：一轮 F5 已输出 XML 并进入 Unity shutdown 后进程不退出；核对隔离工程命令行后仅结束拥有的 PID 44784，继续下组。随后给 CLI 增加 XML 完成后 60 秒退出看门狗，异常另写 shutdown-timeout.json 并计基础设施失败，不掩盖退出异常。
- Review：Commands 持有任务，Navigation 不依赖 UI/策略，Presentation 单向订阅；Action 基类删除原导航实现和直线移动兜底。原自动选择事实写入已合并到接受入口。新命令切换会清除攻击视野计时与资源等待身份，旧 CommandId 完成被拒绝。通过。
- 提交：本阶段提交见 git log；后续继续 P2。

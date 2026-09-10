# P1 小规划：指令生命周期、导航与反馈

- 状态：待 P0 实际接入验证后执行；架构已获用户确认。
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

- 修复前证据：待运行。
- 实现：待开始。
- 测试／Review：待执行。
- 提交：待完成。

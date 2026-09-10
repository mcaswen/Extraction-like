# 阶段架构审查

## P0：自动测试入口

- 状态：通过。已实际运行 Unity 2022.3.62f2c1，Editor 预定义程序集发现成功，无程序集迁移。
- 边界：PowerShell 处理快照／进程／结果完整性；Editor Fixture 处理 Play Mode 与场景；Writer 处理证据；Smoke 验证导航／碰撞。未改 Gameplay。
- 风险处理：Domain Reload 重入已复现并修正；进程硬超时只终止启动器拥有的 Unity 进程树，下一轮实际继续成功；测试副本使用独立存档身份。
- 证据：详见 [P0 执行记录](p0_execution.md)。原始 NUnit／日志在对应 run-id 下，故意失败和超时没有被当作业务通过。
- 后续：各阶段补充共享逻辑、依赖、状态所有权与误报检查；P5 作全量审查。

## P1：指令生命周期、导航与反馈

- 状态：通过阶段审查。F1/F3/F5/R1/R5 的生产实现、正反控制和边界用例已完成，实际运行见 [P1](p1_execution.md)。
- 所有权：生命周期唯一持有活动任务和一份挂起撤离，使用 CommandId 防止旧回调；低层存储沿用 Intervention。Pawn 仅装配/桥接；导航查询和 Motor 不持有黑板/UI。
- 依赖：Commands → Navigation/只读接口/既有 Registry 目标；Presentation → 不可变结果事件；Perception 基础只依赖 Unity。未引入反向 UI 依赖和程序集迁移。
- 删除重复：Action 基类移除导航实现与直线兜底；Dispatcher/Discovery/Decision 不再在验证前改任务事实。普通攻击间隔归 CombatController，状态重入不重置。
- 测试审查：发现并纠正多帧假通过，加入末尾检查点；资源位移用真实 Inventory 开关、NavMesh Warp 与 searched 状态验证。baseOffset 到达缺陷由失败用例定位，非修改夹具绕过。
- 剩余阶段边界：P2 接入双方真实可见事实/三维弹体与所有敌人，P3 处理技能集合与属性分离，P4 渲染截图/重复回归。以上未标成已完成。

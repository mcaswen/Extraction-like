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

## P2：目标绑定与空间交战

- 状态：通过。七种正式敌人预制体、失效分支、巡逻候选、双向高低差及范围技能共 24 例通过，详见 [P2](p2_execution.md)。
- 所有权：EnemyCombatTargetBinding 原子解析身份及接收器，具体敌人控制器撤销自己持有的旧攻击阶段；Selector 负责候选扫描。旧 Player 回退不会重新拾起已离场/死亡 Agent。
- 依赖：Perception 只引用 Unity；范围伤害对身体的特殊排除由 Combat 层提供谓词。CandidateCollector 通过 Authoring 公开复制接口读取全部初始/运行时成员，没有反射业务私有集合。
- 空间链路：观察范围与已知反击任务分离；实际枪口再次验证；三维方向与每个物理步扫掠；失败不退回直接扣血。AOE/DOT 保留技能形状并按作用原点检查墙体。HunterBoss 原有咆哮掩体减伤规则保留，不把既有范围招式重设计为普通弹体。
- 附带修复：正式 VFX 创建先停止 ParticleSystem 再设 duration；不是忽略日志。范围查询、身份断言和 NavMesh 夹具中的误报也通过正反对照定位。
- 下一阶段：可执行候选与风险候选分开使用，资源/撤离成员距离和冷却保留交 P3；全部阶段一起重复回归交 P4。

## P3：共同候选与冷却

- 状态：通过阶段审查，Decision 4/4、Cooldown 5/5 实测通过，见 [P3](p3_execution.md)。
- CandidateCollector 统一产出成员级事实；风险保留全体感知合法敌人，执行选择要求可射击或可达；资源新选/保持不再混用群中心。资源 Cluster 继续持有成员完成状态。
- Decision 读取 Pawn.Defense，评分服务仍只消费快照；范围外可达撤离作为后备，仍走既有风险阈值。删除了两个控制器的重复距离/成员筛选实现。
- CombatController 保留相同配置的运行时技能，仅集合变化才调整实例；同类型同 SkillId 替换受控迁移截止时间。SkillBase 继续持有冷却，Controller 持有普攻锁；未出现测试代码对业务状态的后门写入。
- 限制：路径风险仍沿用直线邻近估算；没有宣称改为逐 NavMesh 路段风险积分。真实图腾 UI 链路与组合矩阵由 P4 完成。

## P4：综合回归与 Review 增补

- 状态：**通过**。最终 `20260911-030638-823` 的 75 例三轮 225/225 通过，0 失败/缺失/超时；原 71 例三轮 213/213 证据保留。基线与所有修正详见 [P4](p4_execution.md)。
- 组合用例经实际 Raid 计时销毁 A，再观察敌人绑定 B 并产生真实伤害；装备槽调用实际 TryEquip，等待 Pawn 刷新后验证冷却；Canvas 使用正式 prefab 并输出原始 PNG。
- Review 发现并补齐自动失败任务立即重选和重复 SkillId 双冷却。失败记忆独立在 AgentTargetFailureMemory，Collector 只组合缓存；按 Agent/目标、位置和游戏时间限制作用域，启停订阅与生命周期对称。敌人仍计入风险，手动命令不查询此缓存。
- 重复技能定义在 CombatController 配置协调阶段按 SkillId 去重，现存冷却仍归 SkillBase。不会将截止时间写入 SO 或全局静态表。
- 缺失攻击配置、禁用目标、完成群以及 Tidal 开场感知漏检均在既有生产责任文件修复；粒子 Assert 在四个 VFX 创建入口修复，没有忽略异常。
- 场景根与角色根、NavMesh baseOffset、Sentinel prefab 身体偏移、船锚刚体初始化、固定巡逻姿态的夹具错误与生产 Bug 分开记录。测试监听器清理守卫不改变行为断言。
- 7 个报告层构造通过，包含全绿 XML 后进程异常退出；NUnit 最终结论、用例清单和进程完整性共同决定门禁。新增 44 个 Unity 文件的 .meta 完整、GUID 无冲突。
- 最终批次 4,958 个源输入文件 SHA256 一致，实际生产代码与测试执行快照相符；diff whitespace 检查和全 Gameplay/VFX 的测试反向依赖扫描通过。

## P5：交付审查（进行中）

- 已核对 15 份本次相关 Markdown 的本地文件链接，无缺失目标。
- README 保留资产盘点、正式入口、制作流程及尚未接通的产品链；框架文档已替换旧直线移动/直接扣血兜底、旧优先级和单主角撤离描述。
- 大规划中的草案文件明确收敛为实际文件，Reuse/Extend/Wrap/Create 均指向具体路径。最终统计和提交完成后更新本节状态。

# P1b：改令、路由和背包交接

状态：构造、验证和审查完成。P1a 已提交 `3339c0d`，10 项新构造和 23 项相邻回归通过。

## 文件归属与实现边界

- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandTransitionTests.cs` + meta：正式 Dispatcher 的三种任务互相替换、重复、有限连发、焦点/显式路由、拒绝保持和共享敌人收尾。夹具只在构造时设置位置、速度和被动敌人；旧 CommandId 的 Finish 调用是明确的迟到回调故障注入，绝不用于完成最新任务。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandInventoryTests.cs` + meta：从原定 Transition 文件分出真实 UI 暂停、资源会话归属和箱内守恒测试。独立原因是它拥有 Canvas/InventoryDriver/物品夹具，而普通指令转换不依赖背包 UI；仍在原 Editor 测试目录和依赖方向内。
- Extend `Assets/Scripts/Editor/AgentReproduction/World/TargetFactory.cs`：增加由真实 LootBox 构造资源群的能力，成员配置仍归目标夹具，不修改游戏 Authoring。
- Extend `Assets/Scripts/Editor/AgentReproduction/World/EnemyFactory.cs`：被动敌人增加可选初始生命参数，在激活前配置，旧用例默认 10000 不变；共享敌人用例以短时间内真实击杀验证收尾。
- Extend `tools/agent-repro/cases.json`：登记两个精确 NUnit 分组。
- Reuse `AgentFactory.cs`、`EnemyFactory.cs`、`InventoryFactory.cs`、`TestNavMeshBuilder.cs`、`RuntimeWait.cs`、`SceneRaidInventoryDriver.cs` 和 `SceneRaidInventoryLedger.cs`，不新建生命周期、库存算法或指令接收器。

测试先运行现有生产实现。只有红灯定位证明生产问题时，才在本记录追加具体修复归属、再修复复跑；不预先扩大业务修改。上述拆文件是原计划内测试职责细分，不改变 Gameplay 公共接口和重要架构。

## 构造与验收

1. Search/Engage/Extract 的 3×3 转换在 1×/4×执行：旧任务先有真实移动，新任务必须移动向新位置，旧身份回调被拒绝。相同群 A→A 和 A→B→A、同帧八次命令分别验证最后任务进展和有界提示队列。
2. 默认焦点先 1 后 2，焦点 2 时显式指定 1，另一名角色的任务不变；拒绝 null/禁用/完成/不可达群和无效角色时保留旧任务。正常死亡通过正式伤害构造，检查显式死者不会转投生者。
3. 双人同一敌人使用真实射击直至死亡，两人的指令均正确收尾，不人工结束最新任务。反击改令沿用上一阶段已通过的敌人/新撤离对照，补资源目标覆盖旧挂起撤离。
4. 真正打开箱 A 后向 B/敌人/撤离下令，覆盖 1×/4×；正式关闭旧会话恢复原倍速，A 余物不消失，新任务不被旧关闭回调完成，恢复后有进展。
5. 两人同箱由现有 Driver 处理，验证实际物品总量、会话归属和结束解锁。近/远资源、未开箱撤离的最终结算继续由 P2–P4 场景脚本及已有 SceneTerminal/SceneStorage 定向覆盖，不把仅有移动的本阶段测试报告为完整结算。

记录 XML 精确参数数量。旧回归已覆盖的有限反击、合法高低差和 HUD 淡出不重复实现。P1b 没有新增逐帧生产工作，因此不拿无图形构造 FPS 冒充性能验收；真实场景性能仍在 P4。

## 结果

登记 31 项指令转换、7 项真实库存构造。首次运行 `Logs/AgentReproduction/20260912-035725-042`：XML 38 项、37 通过，耗时 65.52 秒。唯一失败是共享敌人被真实击杀销毁后，测试等待谓词继续调用 EnemyHealthController.IsAlive，产生 MissingReferenceException；堆栈落在测试第 174 行，没有生产错误证据。

为该谓词增加 Unity null 判断，保留真实射击和双指令各一次 Completed 的断言。受影响单例在 `Logs/AgentReproduction/20260912-035934-322` 1/1 通过。没有重跑未变的 37 项；两份 XML 合计覆盖清单中的 38 个不同参数用例。使用 TestFilter 合并分组后另核对 XML 名称和登记清单，没有把分组标签当作测试数。

本阶段未修改生产文件。实际通过了 18 项三种任务切换、1×/4×重复/连发、焦点和显式路由、五种拒绝、死者拒绝不转投、资源覆盖反击、真实共享敌人死亡、六种开包改令和双人同箱物品只转移一次。

P1 后续仍需独立检查未开箱直接撤离的携带和终态，不能用无 InventoryScreen 的旧 SceneTerminal 白盒代替。P2 调查发现非焦点角色在首次切换前可能尚无角色库存快照，这会影响直接撤离和只读证据；下一小阶段先构造确认，不让测试切焦点掩盖此边界。

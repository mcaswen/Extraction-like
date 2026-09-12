# P1 玩家任务恢复

文件和职责按 task_plan：只扩展原生命周期的单份挂起，不新增任务栈或从反击动作直接重发玩家命令。`SuspendedDirective` 是通用只读视图，原 `SuspendedExtraction` 保持仅返回撤离任务，旧调用者语义不变。有效新令/取消/死亡对挂起任务补发 Cancelled，避免只清引用却留下悬空生命周期事件。

## 复现

- `Logs/AgentReproduction/20260912-155310-557` 是新夹具的编译错误（AgentId 类型传给 string 参数），不是产品红灯；修正为已有 AgentIdValue。
- `Logs/AgentReproduction/20260912-155353-433`：13 项、5 通过、8 失败。Search/Engage 在 1×/4×均没有 Suspended；拒绝新令后、反击失败后、双角色恢复丢原任务，失效挂起目标缺少终态。原 Extract 两项通过，对照成立。
- `Logs/AgentReproduction/20260912-155525-170`：修复后同一组 13/13 通过，13.53 秒。实际移动构造经过正式 Dispatcher、伤害、敌人实际死亡和行为树恢复，验证原 CommandId/TargetId、导航终点及恢复后 X 方向继续移动；没有在此用例直接 Finish 反击。

边界用例显式注入 LostSight 完成回调来验证失败恢复策略；不能当作自然失去视线的覆盖。Cancel/Death/NewOrder 不恢复旧任务，RejectedOrder 保留，InvalidTarget 发布失败，双角色互不覆盖。

`Logs/AgentReproduction/20260912-155705-735` 定向回归原 Lifecycle、F1 撤离中断、SceneRetaliation、ClusterCommandTransition，45/45 通过，57.62 秒。XML 的完整参数名与登记清单严格相等，不将自定义 TestFilter 所借用的分组名称误认为测试范围。测试进程均正常退出，保留原独立 Editor。P1 的 13 项新测试和 45 项相邻回归通过，审查完成。

## P1b 终态边界补充

P2 收尾审查发现：挂起的 Search 若被别人搜完，或手动 Engage 的目标本身就是被反击击杀的对象，恢复校验会报 TargetCompleted，不能作为失败展示。沿用原 Tick 的目标终态规则，将目标完成的判断抽成同文件私有函数，恢复先判断目标终态，再进行导航校验；不把路径失败当作任务完成。禁用撤离点仍然失败。新增三种目标对照，先运行红灯。

`20260912-161556-201`：3 项中 Search/Engage 2 项失败（预期 Completed，实际 Failed），禁用撤离点对照通过。修复后 `20260912-161647-077`：完整 ManualDirectiveResume 16 项 + Lifecycle 6 项 + SceneRetaliation 6 项，28/28 通过，29.24 秒，XML 参数名与清单相等。

## P1c 只读探针对齐

最终审查发现 `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs` 的通用 suspendedCommand 字段仍读取旧 SuspendedExtraction 视图。Extend 此文件改读通用 SuspendedDirective，不改变 DTO 或生产生命周期；在六种实际移动恢复构造中增加正式 ReadModel 断言，先红后绿，并回归原 SceneRaidCommandHarness。此前场景的 Suspended/Resumed 事件仍完整，这项修正补齐状态快照中的挂起身份，不重写旧日志。

`20260912-165620-268`：6 项中搜索/交战四项读出 null，撤离两项通过。修正这一行后，`20260912-165734-388`：六项实际移动/快照验证和 19 项原 CommandHarness，25/25 通过，23.35 秒。没有改动玩法或帧采样，因此不重复运行已经完成的整局。

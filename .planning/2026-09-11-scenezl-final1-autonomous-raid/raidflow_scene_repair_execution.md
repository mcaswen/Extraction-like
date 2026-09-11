# RaidFlow 场景配置修复

日期：2026-09-11。状态：场景修复完成，保存/重载/幂等和两轮实际运行所有者检查通过；P1 运行器退出故障另行处理。用户明确授权遇到 RaidFlow 这类场景 bug 直接修场景。本小阶段从 P3 提前，不等待性能优化。

## 证据和决定

P0 两轮相同场景分别由 Canvas/GameManager 和根 RaidFlowController 的 Awake 获得 Singleton，任务名称随启动顺序改变。场景实际有三处组件。

保留根对象 `RaidFlowController`（Prefab 来源 `Assets/Prefabs/RaidFlowController.prefab`）作为唯一流程所有者，使用它现有的 `Large Island Wall Layout` 配置。移除 `Canvas/GameManager`、`IslandWallLayoutRoot/RaidFlowController` 上的重复组件，保留对应 GameObject 和其他组件。Canvas 的删除记录作为本场景 Prefab override 保存，不修改共享 Canvas prefab。若发现直接引用被移除组件，重绑到保留实例并记录。

## 文件归属

| 决策 | 文件 | 职责 |
| --- | --- | --- |
| Extend | `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity` | 唯一 RaidFlow 配置，不改布局、地形、NavMesh、出口和资源规则 |
| Create | `Assets/Scripts/Editor/AgentReproduction/SceneRaid/SceneRaidSceneRepairEntry.cs` | 显式一次性场景配置迁移、引用保护、保存/重载/幂等验证；与只读 Audit/Observe 分开 |
| Create | `tools/agent-repro/Invoke-SceneRaidRepair.ps1` | 调用隔离迁移，检查源输入哈希、备份和原子回写 |
| Reuse | `tools/agent-repro/AgentRepro.Workspace.psm1` | 副本所有权、锁、Unity 版本和输入快照 |
| Reuse | `SceneRaidSceneAudit.cs`、`SceneRaidReadModel.cs`、`Invoke-SceneRaid.ps1` | 修复后的真实展开及 Play Mode 所有者验证 |

没有 Gameplay 或 Singleton API 变化。修复入口只能显式调用，正常运行器不偷偷修正被测内容。

## 验收

保存/重载后整个场景只有一个 RaidFlow，所有原 Transform 和非 RaidFlow 组件数量保留，保留实例配置不变；重复执行移除数为 0。源全输入快照一致才回写，保留回写前备份。随后两轮原场景 Play Mode 均检查唯一根所有者和要求撤离集合 `{1,2}`，继续记录已知 NoProgress/性能问题。

## 结果和 Review

修复运行 `20260911-193039-332`：3 → 1，移除两处重复组件，无需重绑其他引用，重载后二次修改 0。场景 SHA256 从 `bbd30981ff7ba01d73def8476894e0c139a71b3af1c7db575a72109b2c2b3b04` 变为 `78213ed1bdf3b32ebdb122df769c14c507b56f08465bc53d72c0b4a779c0aae5`。输入哈希验证通过，原子替换前保存完整备份。

场景差异为 2 行新增、17 行移除：Canvas 实例增加一个 removed-component override；场景内另一处重复 MonoBehaviour 及组件引用删除。没有删除 GameObject、Transform 或修改共享 Prefab、地形、NavMesh；保留实例参数、其他组件数量、所有 Transform 和出口配置通过程序断言。

实际 Play Mode `20260911-193222-820`、`20260911-193800-568` 均完整观察 60 秒，运行时仅剩根 `RaidFlowController[4]`，任务名固定为 `Large Island Wall Layout`，要求撤离集合为 `{1,2}`。这两轮的 Unity 进程在原生退出阶段挂起，已记录并按失败终止，不能作为整局/性能验收通过；不影响已写出的唯一所有者事实。结构化结果见 [raidflow_scene_repair_result.json](raidflow_scene_repair_result.json)。

架构审查：迁移入口与只读 Audit/Observe 分离；仅场景持有唯一流程配置，不增加新的全局管理器。移除 Prefab 组件通过 Unity 实例覆盖保存，保存/重载测试验证引用没有重新出现。运行器退出问题归 P1，不通过修改业务结算来掩盖。

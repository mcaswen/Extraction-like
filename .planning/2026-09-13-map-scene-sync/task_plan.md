# 地图重新生成的场景身份报错

状态：G1 诊断完成，G2 场景同步已实现并通过 42 项定向验证。用户最新说明已确认群删除、换区属于有意的场景修改，原歧义已解除；当前结果见 [G2 小规划](g2_apply_scene_changes.md) 和 [验收报告](../../outputs/map-grid-authoring/scene-sync/validation_report.md)。下文保留 G1 当时的定位依据和待确认状态，不代表当前仍需确认。G1 基线 `678770f`，沿用已确认的地图指挥分层和自主闭环授权。

## 问题与验收

点击地图生成出现 `NodeSynchronizationRequired:cluster_065e3bcfbc8e922239efcb8b0a222710`、`SceneSynchronizationRequired`。当前校验将群缺失、来源身份变化、Zone 变化和类型变化合成同一个信息，没有给出旧/新值；必须先证明实际差异。磁盘场景中该对象是 `Zone-实验室/Env/实验室/EnemySourceCluster`，组件 fileID `6431816093595122837`，Zone fileID `1279994158`，与已保存 SO 的来源和区域一致，不能凭错误文字宣称它已经被删除。

本步验收：通过 Unity 实际采集复现并输出身份差异；按实证修复对应归属，保留手工布局/连接和安全约束；Agent 运行定向测试，记录结果与架构审查后中文提交。若磁盘场景通过而窗口缓存不同，修窗口工作副本/诊断，不修改正确场景。

## 小规划与文件边界

1. Extend `Assets/Scripts/Editor/AgentReproduction/Tests/MapGraphSceneSynchronizationTests.cs`：在隔离副本用正式 `MapGraphSceneCollector.Capture` 对照场景绑定的 SO，导出全部旧/新节点身份、Zone、类型、层级、缺失/新增。仅只读采集，不保存源场景；用真实 Unity 事实区分序列化引用与实际运行对象身份。
2. Extend `tools/agent-repro/cases.json`：登记上述定向诊断；Reuse `Invoke-AgentRepro.ps1` 和已有 Regression 副本，非 Play Mode 检查无需完整搜打撤。
3. Reuse `MapGraphGenerationController.cs`、`MapGraphLayoutGenerator.cs`、`MapGraphEditorDocument.cs`、`MapGraphEditorWindow.cs` 的既有生成/作者事务边界。先检查真实差异，再补具体修复小规划；不先移除 ValidateSynchronization，也不将重要场景身份删除静默合并。

已读依据：地图大规划、P2 生成/作者保存记录、框架文档、既有场景同步与生成控制器测试。生成器拥有采集冻结和验证，Document 拥有工作副本/Undo，Window 拥有交互，场景与 SO 保存归 AuthoringTransaction；诊断和构造仍归 Editor/AgentReproduction。

## 结果

`Logs/AgentReproduction/20260913-170437-603` 主项目已保存场景实际加载检查 1/1 PASS，节点身份、归属和类型均与正式 SO 一致。

进程核对确认当前唯一的 Unity PID 38472 打开的是 `D:/Unity-Projects/.agent-repro/AnomalySearchFinal/Project`，不是主项目。对该副本的已保存场景逐 YAML 对象比较，确有 12 个块删除、16 个块变化、0 个新块：内层 `EnemySourceCluster`（GameObject 6977126096491438660、组件 6431816093595122837）及两个 `Pfb_EnemySpawn` 被移除；地图节点仍在，Binding 的目标已变为 fileID 0。因此不是单纯坐标变动，也不是主项目里的来源 ID 丢失。

已只读备份两份场景和精确对象差异到 `Logs/MapSceneSynchronization/20260913-170651/`；不再向这个正在被用户编辑的 Final 副本同步源工程，以免覆盖用户改动。已询问删除是否有意，决定后续同步旧图还是恢复实体；等待时不执行任何依赖该选择的场景或地图改写。

独立诊断改进小规划：Extend `MapGraphGenerationController.cs` 的既有校验信息，区分对象缺失、来源 ID 变化、Zone 归属变化和类型变化，带可读群名/层级及旧新值；不改变阻断条件，不自动恢复实体或删除图节点。Extend `MapGraphGenerationControllerTests.cs` 的现有同步反例断言可读原因，运行该组；此改进不依赖用户最终选择保留删除还是恢复。

诊断改进已实现，`Logs/AgentReproduction/20260913-170905-595` 定向同步反例 1/1 PASS，0 基础设施问题、源输入未变。主项目文件已包含具体缺失/来源/归属/类型说明，旧对象删除保护未放宽。当前打开的 Final 副本没有被同步覆盖，仍保留用户删除内容；后续恢复或同步动作等待用户确认删除意图，不能将本次诊断提交当作场景问题已经修复。

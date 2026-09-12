# P6 真实场景、独立契约和交付

状态：小规划已记录，等待 P5 最终截图和提交后实施。沿用已确认的大规划和用户“直接做完”的授权；逻辑 4×，真实渲染 1× 平均 FPS >60，战死是正常终态。

## P6a：根路线证据接入和自主场景

已重读 SceneRaidRunController/Observer/ReadModel、InventoryDriver、现有 CommandScenario/ClusterCommandDriver、脚本配置和 Runner、RouteSnapshot/Result/Step、Installer/共享 Environment。现有 MC 驱动按具体子指令 ID 判定完成，不能代表多群根路线。本步先补只读证据，保留 SC02/SC03 的自主行为和背包代理。

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidRouteEvidence.cs`：订阅真实 Pawn 根结果和注册事件，保存根/版本/序列/游标、步骤及反击变化；低频记录地图投影、原锚点距离、世界位置和共享查询次数。通过现有只读快照读取，不产生导航查询、目标指令或取物。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidReadModel.cs`：在每名角色的既有 Transform/导航/敌人血量快照旁附上根路线记录，不复制原导航和战斗探针。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidRunController.cs`：在场景加载前安装证据订阅，LateUpdate 调度，终态写完后释放；复用现有库存、帧、渲染和结算设施。
- Create `tools/agent-repro/SceneRaid.Routes.Contracts.psm1`、`Test-SceneRaidRoutes.ps1`：从原始路线事件/快照独立检查已接受根、合法相邻边、实际游标顺序、反击身份保持、显示序列/位置和正式终态。缺证据不能记 PASS，接受通知不能代替执行完成；保留拒绝/失败原因供诊断。
- Extend `tools/agent-repro/SceneRaid.Report.psm1`：路线契约独立字段，不拿旧单敌人契约冒充新规则；保留现有库存守恒和死亡判定。
- Reuse `Invoke-SceneRaid.ps1`、`SceneRaidInventoryDriver.cs`、`SceneRaidFrameSampler.cs`：首先执行 MR01（SC02），根据根/子/世界联合证据定位问题，逐项写修复归属后复跑。

## P6b：远近指挥、编辑生效和导航断连

新路线脚本使用独立版本和目录 `tools/agent-repro/map-command-scenarios.json`；继承已有请求文件、哈希和进程隔离机制。MR02 按真实入图/行进/背包关闭前提，向正式 Router/地图 Handler 有限次下令，另一角色保持自主。旧 MC 脚本继续记录历史子指令含义，不静默改判定。具体驱动文件在 P6a 证据验证后补小规划，仍归 Automation/Commands，不能放入 Gameplay。

MR03 在测试拥有的图副本删除/添加合法边，比较实际规划和经过序列，结合现有纯图、根重规划和 Editor 保存测试补证。当前龙骨礁撤离锚点可采样但跨区路径 Partial：先导出真实路径角点、附近几何/层级/导航面和画面，判断是锚点、通路还是烘焙配置问题；不得删节点、伪造可达或瞬移。用户已授权有证据的场景修复，只精确提交本次修改，隔离用户场景/NavMesh 增量。

正式图的运行时导航指纹从 P5c 顺延到此步：先关闭真实导航问题，再按最终场景/导航采集签名，避免保存马上失效的缓存；不因此跳过最终缓存复用验证。

## P6c：渲染、截图和最终审查

复用 SC07 构建和已授权的可见 Player，MR04 1×、4K/High Fidelity、不限帧，正常处理资源/交战/撤离和预期战死。小/大地图切换只影响显示，不改变任务。截图开销单列，原始帧数据完整保留，平均 >60 为门槛。持续捕获实际行进、等待、反击、多人及撤离画面，打开查看后修正，未自然出现的状态由确定性构造补证。

更新 `README.md`、`Assets/Docs/GameplayAgentFrameworkDesign.md`、`outputs/map_command_validation_report.md`、本规划及 `architecture_review.md`。记录每轮输入/结果、真实覆盖缺口和剩余问题；各小步通过后中文提交，不推送，不提前宣称整项完成。

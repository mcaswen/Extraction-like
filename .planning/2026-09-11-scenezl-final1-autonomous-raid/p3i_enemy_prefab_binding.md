# P3i：敌人来源群误填出生点 Prefab

## 证据和目标

2731 的死亡轨迹出现有效敌人却没有群 TargetId。展开实际场景和 Prefab 后找到具体配置错误：渔村、实验楼两处、雨林两处共 5 个 EnemySourceCluster 的 `_enemyPrefabs` 中，11 项填的是 `Assets/Prefabs/Enemy/SpawnPoint/Common/Pfb_EnemySpawn_*.prefab`，而非对应 Pawn。

`EnemySpawnPoint.Spawn()` 按来源群配置实例化该对象，第一次得到的是另一个出生点，没有 EnemyHealth，无法注册；其 Start 再生成真正敌人，但根出生点已脱离来源群层级，注册失败，群等级规则也没有正确传递。开局 30 个配置出生点与仅 19 个活跃群成员相符。不能将前面的绿色整局解释为所有敌人接线正确，2731 死亡也不能只归为数值。

目标：把这 11 项直接改为各出生点 Prefab 中已有的真实 `_enemyPrefab` 引用，保持敌人种类、槽位顺序、场景 Transform、出生点数量和既有等级配置。用户已明确允许直接修复此类场景配置 bug。

## 文件归属与边界

- **Extend `Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`**：只修改 11 个 `_enemyPrefabs` 引用，包括 7 个 Prefab 实例数组覆盖和 4 个直接序列化列表项。真实 GUID/fileID 从对应 SpawnPoint Prefab 读取，不按名字猜测；保留用户 FFT 的三处 inactive 差异，提交时排除它们。
- **Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidEnemyPrefabConfigurationTests.cs` + meta**：只读场景配置断言及真实加载后的注册链断言。独立文件因为验证正式场景装配，不属于反击行为或通用层级修复算法。
- **Reuse `ReproductionTestFixture.cs`、`TestRunContext.cs`、`CaseArtifactWriter.cs`**：隔离存档、有限 Play Mode 生命周期、原始证据；没有测试目标指令或健康/位置写入。
- **Extend `tools/agent-repro/cases.json`**：登记两项场景装配构造；不让原 Smoke 自动跑所有图形场景。

本阶段不引入运行时自动拆包装器，不在 Collector 扫全场兜底，也不改变反击和评分策略。错误来自已确认的具体场景引用，修复归场景。

## 实施与验收

1. 当前 Player 源输入保持冻结，完成这轮后再改测试代码；该轮属于修复前配置证据。
2. 两个只读用例先红：每个有效敌人配置必须实际含 EnemyHealth，整场生成后不得出现脱离来源群的新出生点，每个活敌都应能反查所属 ActiveEnemyCluster/SourceCluster。
3. 逐项替换上述引用，重新加载场景，两项用例转绿；数量应为 30 个原始出生点、30 个实际敌人，全部注册。
4. P5 重新冻结修复后的场景，先复跑失败种子 2731 的 4×，再按结果决定完整矩阵。旧种子结果不混入新场景的最终通过次数；1× Editor、可见 Player 必须使用新场景。
5. 回写结果与架构审查，按阶段提交，保留全部失败证据。

## 实施结果

首次夹具 `20260912-005235-032` 使用了 EditMode UnityTest 不接受的 AsyncOperation yield，改为轮询 isDone + yield null；未据此判定游戏问题。

第二次 `20260912-005603-780` 两项断言均明确红灯：11 个敌人槽位误填 SpawnPoint，运行时 41 个出生点、30 个活敌、11 个未注册。夹具收尾又暴露通用微场景先移除 NavMesh、真实场景角色仍可 Update 的错误，增加本夹具 TearDown 提前停用角色，不吞错误或改玩法。脚本替换先验证数量，首次仅匹配 7 项覆盖时主动中止、没有写场景；随后补识别 4 项直接序列化数组。

`20260912-005727-852` 确认夹具收尾无误，两个业务断言仍红。场景精确替换 11 项后，`20260912-005822-606` **2/2 通过**：30 个槽位无错误、30 个出生点、30 个活敌、0 个未注册，正常退出、源哈希不变。场景除这 11 项引用之外所有字节保持原样，未调整怪物数值、等级或世界坐标。随后先对 2731 运行完整 4×，不以两个装配断言代替整局验收。

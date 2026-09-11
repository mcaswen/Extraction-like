# P1c：未开箱、未聚焦角色直接撤离

状态：修复、红绿验证和审查完成。P1b 已提交 `beab272`。

## 问题和文件归属

`InventoryScreenController.SwitchActiveInventoryAgent` 首次只保存当前角色快照，其他角色在首次聚焦前可能没有 `_inventorySnapshotsByAgentId` 条目。`RaidFlowController.TrySettleExtractedAgentInventory` 在有背包组件但没有角色快照时明确失败。旧 SceneTerminal 测试不创建背包，不能覆盖此边界。

- Extend `Assets/Scripts/Editor/AgentReproduction/Tests/ClusterCommandInventoryTests.cs`：增加两人从未开箱直接下达不同撤离群，分别构造焦点角色/非焦点角色先到达。位置和撤离耗时只在初始夹具配置，实际到点、presence、计时和结算都走正式运行，测试不切焦点、不写 presence、不调用手动结算。
- Extend `tools/agent-repro/cases.json`：登记两项。先运行当前实现，保留红灯。
- 复用 `TargetFactory.Extraction`、`InventoryFactory.Create`、`SceneRaidReadModel` 和正式 `RaidFlowController`。如果确认库存初始化缺陷，修复归 `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs` 的角色库存生命周期；不在 RaidFlow 将缺快照当空，不在 Automation 创造库存。具体设计由红灯和注册/初始化顺序确认后补入本文件。

## 验收

两个角色从真实移动到点到正式 MissionCompleted，extracted=settled={1,2}，无 Missing inventory snapshot 错误；无开包和测试焦点切换。已有物品/预置装备的完整数量核对由只读携带证据在 P2 继续，本例不把空夹具成功冒充预置物品入库验证。正常死亡仍是预期玩法，本构造使用无敌人的空间消除无关随机性。

## 结果

红灯 `Logs/AgentReproduction/20260912-040312-739`：2 项中焦点先撤离通过，非焦点先撤离失败，正式 RaidFlow 报 `Missing extraction inventory snapshot for agent '2'`。没有开箱、没有测试切焦点，正常计时触发错误，确认为生产初始化缺陷。

## 红灯后的修复设计

Extend `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs`：在既有绑定焦点流程保存当前初始 UI 后，为已注册存活角色一次性建立独立默认 CharacterInventorySnapshot。默认背包沿用 DefaultBackpackItem/InventoryItemRuntimeState.Create，不复制焦点角色装备或物品，不生成 UI、不切焦点/关会话。初始和后注册角色均覆盖；每帧只遍历小型注册列表并查一次初始化集合，已存在角色不分配库存，复杂度 O(角色数)，不扫描物品或全场对象。

保存快照时记录该身份已初始化，独立初始化集合阻止“曾有快照后来丢失”被重新当空背包。现有结算失败路径不变，未注册身份仍拒绝。库存所有权、私有数据结构和公共接口均不变，不新增 Registry 事件或 Gameplay → Automation 依赖。

追加两个对照：后注册角色初始化不改变当前打开会话、焦点、暂停和物品；已初始化的非焦点快照被故障注入删除后不能重新伪造空库存。完成后复跑本组九项已有库存构造、两个新对照和 SceneInventory/SceneStorage/SceneTerminal 相关回归。

## 回归调整

第一次修复后 11 项中 10 项通过，唯一失败来自新夹具假设 Canvas 默认配置了 DefaultBackpackItem，而基础 Canvas 实际为空；对照现显式构造默认包配置，生产仍支持没有默认包的旧 Canvas。索引遍历注册列表，避免 IReadOnlyList 枚举器逐帧分配。

`20260912-040730-205`：32 项中 31 项通过。唯一失败是 `SceneRaidStorageTests.FailedSettlementPreservesAgentInventoryAndEndsAutomationPromptly` 仍期待旧 `BEHAVIOR_BLOCKED`；核对历史提交 `7dc233a` 已将 RunController 的所有观察到的 MissionFailure 统一输出为 `RAID_OBSERVED_FAILURE`，独立契约再检查是否正常死亡。本次库存修复没有改变该输出。

Extend `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidStorageTests.cs` 的过期状态断言为现有观测协议，追加 result.errors>0；保留角色未销毁、物品未丢、未结算、及时收尾断言。原契约 alive/settlement_missing 等反例继续证明结算失败不能按 EXPECTED_DEATH 通过。不为迁就旧断言改写已经确认的死亡语义。

最终受影响单例 `Logs/AgentReproduction/20260912-041011-202` 1/1 通过；合并前轮，11 项本组、9 项 SceneInventory、8 项 SceneStorage、4 项 SceneTerminal 共 32 项通过。`Logs/SceneRaidContractProbes/20260912-041011-218` 的 44 项独立契约反例通过。前后源指纹一致，没有漏测或基础设施错误。真实场景 SC02 在 P2 驱动基础落地后补跑，当前没有新 FPS 结论。

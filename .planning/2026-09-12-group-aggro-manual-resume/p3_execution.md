# P3 真实场景和补充定位

SC08 / MC02 / seed 731 / 4×：`Logs/SceneRaid/20260912-161817-808`，PASS，全部撤离、仓库一致，6/7 步骤自然覆盖。原 Editor PID 11628 保留。

SC09 / MC01-E / seed 731 / 1×：`Logs/SceneRaid/20260912-162153-639`，601.78 秒到达预设期限，BEHAVIOR_BLOCKED，不算通过。三个手动敌人步骤均已下达，尾段发现 Boss NoProgress 和自主搜索/交战反复切换。正常战死不列入修复。

## P3a Boss 物理所有权小规划

探针显示 Boss 根位置 Y=19.675，身体/瞄准点从 18.65 下降到 8.32、5.57、4.90；角色已在根位置 4.4m 内，却因真实身体距离超出 8m 射程而无法开火。实际场景引用 `Assets/Prefabs/Enemy/Pawn/Boss/Pfb_Enemy_HunterBoss.prefab`（GUID a75329ef5f0ca7a42a88b4f3db576821），没有覆盖 Rigidbody 的这两项设置：Prefab 同时含 NavMeshAgent、脚本位移和启用重力的动态 Rigidbody，属于重复移动所有权。

- Extend 上述 Boss Prefab：让受脚本/导航驱动的 Rigidbody 使用 kinematic，关闭重力；不删物理碰撞、NavMeshAgent，不改生命、射程、技能或平衡。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/EnemyPhysicsOwnershipTests.cs` + meta：复用正式 Boss Prefab，在只有导航数据、无物理地面支撑的隔离布局，验证物理身体和导航根不分离；仍通过正式受击驱动群体追击。选择这个文件是为了独立拥有物理/导航布局，避免把资产配置验证塞进表现或指令测试。
- Extend `tools/agent-repro/cases.json`：登记确定性参数。先跑旧 Prefab 复现，再修正、复跑；原 1×失败日志保留。自主任务反复切换先不改选择策略，修正身体漂移后用同一 MC01-E/seed 复查，避免同时改两个因果链。

旧场景已于 16:32 退出 Play Mode、记录 sourceUnchanged 的 process.json，剩余工作是对已保存文件的离线报告；后续独立 NUnit 在 Regression 工作区运行，不改变原始日志或场景结果。

### 复现和修复结果

- `Logs/AgentReproduction/20260912-163821-482` 是夹具使用 WaitForFixedUpdate 导致 EditMode 测试枚举器报错，不是产品红灯；使用原运行器支持的 yield null，在导航/行为更新后采样。
- `20260912-164026-303`：两种倍速均复现实体分离，1×最大 1.201593m、4×最大 1.185833m，2/2 失败。根对象实际追击移动了 21m，物理身体约在根对象下方 1.2m。
- Boss Prefab 改为 kinematic / gravity=false 后，`20260912-164205-776` 的两项身体验证、两项 Boss 群体追击和四项目标绑定回归，8/8 通过；实体与根的最大差距降为 0.071514m / 0.097717m，保留正常的单帧位置同步间隔。场景没有覆写这两个 Prefab 字段。
- 同场景、同 seed 的 4× MC01-E：`Logs/SceneRaid/20260912-164422-101`，证据 PASS、EXPECTED_DEATH、3/3 步骤 COMPLETE，结算 PASS；66.13 秒墙钟、206.69 秒游戏，8 次背包会话，0 运行错误、0 指令执行失败、0 停滞。日志实际出现两名角色手动交战任务的 Suspended/Resumed。无需同时修改自主选择策略。
- 最后一次同场景同 seed 的 1× MC01-E：`Logs/SceneRaid/20260912-164639-422`，证据 PASS、EXPECTED_DEATH、结算 PASS；216.83 秒墙钟、199.06 秒游戏，10 次背包会话，0 运行错误、0 非预期执行失败、0 停滞。一个 LostSight 有完整的先见后失去视线超时证据，按既有契约认定为预期结束；2/3 步骤被观察，第三步因角色不可用缺失。
- 本机 4K High Fidelity Editor、1×采样平均 133.655022 FPS、p99 14.7194ms、最大 465.5355ms；满足用户的平均 >60 FPS 要求，仍保留慢帧诊断。Editor 原报告的 performanceAcceptance 标志固定 false，本轮没有重建 Player，不能把它称为新的独立 Player 性能验收。
- 最终玩法配置再跑 4× MC02：`Logs/SceneRaid/20260912-165307-641`，证据/游戏/结算 PASS，两名角色全部撤离，64.98 秒墙钟、181.08 秒游戏，10 次背包会话，0 错误、0 执行失败、0 停滞。4/7 步骤观察到；反击改令因角色已不可用，两个重复步骤因整局提前完成未自然触发，保留 PARTIAL，不延长局面来制造覆盖。相应规则已有独立构造通过。

### 原失败样本的独立报告

旧 1×样本 `20260912-162153-639` 最终报告保留 FAIL：一个符合既有契约的 LostSight、三个非预期 NoProgress、整局未完成；另有 frame_duration_mismatch。因此其 CSV 诊断平均 112.02 FPS 不作为正式性能验收。离线处理约 11 分钟才结束，主要数据包含约 50MB counter CSV；本轮没有改报告器，也没有删除这个证据问题或把超时改成通过。

## 收尾

P1–P3 的玩法、配置和相关诊断修复完成。整局后仅对只读 suspendedCommand 映射补齐，25 项定向测试通过，详见 P1c。不同阶段成功项的精确参数名并集为 124 项（44 新增、80 旧回归），与登记清单完全相等，索引见 `outputs/group_aggro_manual_resume_tests.json`。所有自动测试进程已结束，原 Editor 保留，未推送远端。

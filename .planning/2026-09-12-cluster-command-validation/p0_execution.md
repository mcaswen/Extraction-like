# P0：真实 Cluster 清单与路径调查

状态：实现、构造、真实场景观察和自审完成。用户已确认 R-C1/R-C2 行为基准。

## 范围和文件归属

- Create `Assets/Scripts/Automation/SceneRaid/Commands/SceneRaidClusterCatalog.cs`、目录和文件 meta：只读收集三个可点击群类型、成员身份、来源、两名角色当前位置/范围、成员距离、视线和路径证据。读取 ActiveInstance，禁止 GetOrCreate 创建业务对象，不发命令或写 NavMeshAgent 路径。
- Create `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidCommandHarnessTests.cs` + meta：先实现清单只读性、不可达/距离分类两项构造，后阶段再追加驱动自检。复用正式 Agent/Target Factory 与 NavMesh 夹具。
- Extend `Assets/Scripts/Automation/SceneRaid/SceneRaidScenarioConfig.cs`、`SceneRaidRunController.cs`、`tools/agent-repro/Invoke-SceneRaid.ps1`：新增显式 `captureCommandCatalog`/`-CaptureCommandCatalog`，仅请求时在角色导航就绪后捕获一次，成本单独记录；旧运行默认不执行。本阶段先不添加 ManualCluster 模式。
- Extend `tools/agent-repro/cases.json`：登记 `SceneCommandHarness` 两项清单构造。复用 SC01 15 秒观察、同一常驻 Editor，生成 `command-catalog.json`，不以观察通过冒充指令执行通过。
- 回写本文件、主规划和 `architecture_review.md`，另保存 `p0_target_matrix.json` 作为场景身份/近远缺口和脚本拆局依据。

Catalog 不调用 ResourceCluster 会刷新成员状态和缓存的可达解析。资源只记录成员原位置附近的 NavMesh 采样路径，明确 `SampledPivotOnly`，不能据此宣布箱边可达或不可达；正式资源接近点在后续 Dispatcher 构造/执行中验证。敌人使用现有地表位置解析，另外记录真实三维视线；跨高差可射击性不靠地表路径阴性否定。所有路径均是独立缓冲，只代表采样时的导航证据。

## 测试和验收

构造比较 Capture 前后角色 Transform、NavMesh 目的地/路径、当前指令、焦点、资源状态和注册数量；验证来源群不入可点击清单、同群所有成员入清单、稳定排序和重复捕获一致。另构造断开导航面、近/远成员，验证距离分级与路径阴性不被写成可执行通过。

先跑上述两项，再用 SC01 正常 Domain Reload 的 15 秒观察收集当前场景：实际两个 Agent、三个目标群类型、成员来源和每个 Agent 的近/远覆盖。检查零人工/测试指令、源码输入未变，观察期错误如实记录，不修复正常战死。清单完成后固定后续分局/选择规则和缺口；不强行移动角色制造“开局附近”。

## 实施结果

清单两项自检在 `Logs/AgentReproduction/20260912-032558-792` **2/2 通过**。首次 `032336-189` 一项失败来自夹具预期：EnemySource 启用后自动生成配套活跃群，合法空活跃群也应保留；改为核对排除来源群、保留所有配套活跃群，未修改清单实现以凑数量。

真实 SC01 `Logs/SceneRaid/20260912-032620-897`：15 秒观察证据 PASS，零程序错误、零失败/拒绝指令、零停滞，没有手动命令；源输入未变。原 PID 60224 已由外部退出，运行器按协议启动 PID 11628，完成后保留。不是复用了已退出的 Editor，也不把这轮观察作为完整搜打撤验收。

清单包含 12 个资源群/32 个成员、14 个活跃敌人群/30 个敌人、2 个撤离群/2 个点。两名角色 R 均为 200，移动速度分别为 8/12；近远按原阈值不变。开局两人的附近资源为 6/9 个、远处资源 11/10 个，附近敌人为 8/10 个、远处敌人 11/10 个；两人都没有附近撤离点，各有一个中距和一个远处撤离点。资源数是成员数，不冒充群数。

捕获发生在第 3 帧，额外清单成本 38.16 ms，单次诊断不用于 FPS 成绩。原始 JSON 保留代理起点、各成员位置、路径状态和角色距离；机器清单和 SHA256 已归档为 [p0_target_matrix.json](p0_target_matrix.json)。资源 SampledPivotOnly 证据不能代替实际停靠验证，敌人地表路径阴性不能否定跨高差远程开火。

后续脚本冻结为 MC01-R（资源近远）、MC01-E（敌人近远）、MC01-X（未开箱直接远处撤离，进入近范围后再下令）、MC02（改令/反击）、MC03（焦点/双人/会话）、MC04-N（真实单成员敌人群完成后拒绝）。基础六轮 4×，MC02/03 各重复一轮，MC01-R 增加 1731、4×，MC02 增加 1× Editor，MC01-R 增加 1× Player，共 11 个最终运行槽位，具体触发规则随机器清单冻结。自然死亡和前提缺失仍独立报告，不能按 11 槽位填满就算覆盖完成。

近处撤离通过 MC01-X 的正常行走后复投同一群覆盖，保持 200 米门槛；同群多出口后备采用构造，因为真实场景每群只有一个点。P1/P2 若发现某脚本触发条件无法合法满足，先记录证据并调整规划，不在运行中自动换规则。

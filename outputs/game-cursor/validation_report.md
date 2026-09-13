# 游戏内鼠标指针显示验收

2026-09-13。已完成实现和架构审查，2 项相关定向回归通过。

原问题是 InventoryScreenController 关闭背包时执行 CursorLockMode.Locked 和 Cursor.visible=false，导致依赖鼠标点击群、地图和 UI 的正常玩法失去指针。现改为关闭后保持自由且可见；PlayerInputManager 在启用、切回应用窗口时同样恢复。使用系统指针，不新增场景组件或贴图。

正式场景已通过 TargetInputManager prefab 引用该输入组件。静态核对 Assets 中 Cursor 的全部写入，只有两个文件中的 3 处设置，均为 None/true；没有每帧写入，也没有隐藏/锁定残留。

| 现有用例（窄筛选） | 结果 | 运行 |
| --- | --- | --- |
| SceneRaidInventoryTests.QuickTransferWaitsForSearchUsesRotationAndPreservesCloseData | 1/1，通过真实搜索、旋转取物、关闭保存及暂停恢复 | 20260913-201113-293 |
| MapCommandPresentationTests.CompactExpandedZoomAndDragShareTheSameViewsAndExecutedRoute | 1/1，小图/放大图、缩放拖动和路线保持 | 20260913-201145-289 |

Agent 从 Ubuntu 启动 Regression 隔离图形 Editor，协程自动运行 Play Mode，用户无需操作。首次调用因 Ubuntu 临时 WSLInterop 注册项缺失而未启动 Unity，恢复互操作注册后两项运行均成功。没有改动用户 Unity 进程或持久 WSL 配置。

原报告、NUnit XML、Cursor 静态扫描和两个修改源文件的测试快照哈希在 [evidence/manifest.json](evidence/manifest.json) 及相邻目录。两项回归证明相关既有 UI 行为未被破坏；没有新增仅复述赋值语句的测试，未执行操作系统切窗自动化，不将 Camera/Canvas 图片当作系统指针像素证据，也不声称完整游戏/FPS 矩阵已重跑。

文件职责及审查见 [小规划](../../.planning/2026-09-13-game-cursor/task_plan.md)、[架构审查](../../.planning/2026-09-13-game-cursor/architecture_review.md)。

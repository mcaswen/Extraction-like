# 游戏内鼠标指针显示

2026-09-13。状态：实现、定向回归和架构审查完成。沿用用户自主实现、验证和中文提交的授权。

## 问题与范围

当前只有 InventoryScreenController 写入 Unity Cursor 状态：打开背包时自由且可见，关闭背包时却锁定并隐藏。正常游戏依靠点击群、小地图和 UI 下令，这段旧关闭逻辑与现有玩法不符。目标为使用系统鼠标指针，在正常游玩、开关背包、进入游戏和切回游戏窗口时可见且自由移动。本次不增加指针贴图、悬停变色或新的输入系统。

已读依据：GameplayAgentFrameworkDesign 的输入/展示分层、地图指挥规划及现有作者审查；扫描 PlayerInputManager、InventoryScreenController、地图展示和背包验证入口，全文核对 Assets 中 Cursor 的写入点。现有运行时组件足够承载这次事件状态修正，不调整模块依赖或场景配置。

## 文件职责

- **Extend `Assets/Scripts/Gameplay/Targets/Input/PlayerInputManager.cs`**：在 OnEnable 和重新获得应用焦点时设置可见且不锁定，归属现有玩家鼠标输入入口。使用一个私有方法共用两个事件的设置，不在 Update 中重复写入。
- **Extend `Assets/Scripts/Gameplay/Backpack/InventoryScreenController.cs`**：关闭背包后保持可见且不锁定。沿用现有开关 UI 的事件处理，拖拽回收、容器保存、暂停恢复和会话回调不变；不让背包依赖目标输入模块。
- **Reuse `Assets/Scripts/Editor/AgentReproduction/Tests/SceneRaidInventoryTests.cs`、`MapCommandPresentationTests.cs` 和 `tools/agent-repro/Invoke-AgentRepro.ps1`**：选择已有背包开关/暂停恢复和地图展示相关用例回归。此次为局部可逆的指针设置，不新建测试框架或只复述赋值语句的测试。

## 验证和验收

静态核对游戏代码中不再存在隐藏或锁定指针的写入，确认指针设置只在生命周期和 UI 开关事件发生，不新增每帧扫描。使用 Regression 隔离项目运行相关已有用例，用户无需手动进入 Play Mode。用例证明原 UI 会话、库存和展示流程未被破坏，不能将 Camera/Canvas 截图当作操作系统指针的像素证据。最终记录实际运行范围、结果及架构审查，中文提交。

## 实现结果

已实现两个既有文件的事件调整。正式场景通过 TargetInputManager prefab（10753347698b44abcb008b9637af6b41）引用已启用的 PlayerInputManager，因此无需新增或修改场景组件。Assets 中全部 Cursor 写入均保持 None/true，只有 3 处事件设置，没有 Update 写入或隐藏/锁定的残留。

Agent 从 Ubuntu 使用现有 Regression 隔离运行器完成 2 项窄筛选回归：20260913-201113-293 背包搜索、旋转取物、关闭保存和暂停恢复 1/1；20260913-201145-289 小图/放大图切换、缩放拖动和执行路线保持 1/1。二者均启用图形设备，原 XML、报告和源码哈希见 [验证报告](../../outputs/game-cursor/validation_report.md)。没有修改现有测试，也未将两项窄筛选记为完整测试组通过。

环境记录：本轮 Ubuntu 的 WSLInterop 临时注册项缺失，首次 Unity 启动前报 Exec format error；恢复运行时 MZ → /init 注册后，沿用 Ubuntu 执行成功。未修改持久 WSL 配置或操作用户 Unity 进程。

审查结论见 [architecture_review.md](architecture_review.md)：文件归属、依赖和现有 UI 会话语义均保持；设置为常数时间的事件操作，本次不新增 FPS 验收结论。

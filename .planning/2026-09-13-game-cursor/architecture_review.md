# 游戏内鼠标指针审查

日期：2026-09-13。结论：通过。

依据：[task_plan.md](task_plan.md)。新增职责保持在现有鼠标输入入口和背包 UI 生命周期内：PlayerInputManager 在启用、重新获得应用焦点时显示并释放指针，InventoryScreenController 在关闭背包时保持同一状态。两者不相互依赖，不引入新单例、全局服务、场景组件、运行时 schema 或输入框架。

共享的两句 Unity Cursor 设置不足以构成独立模块，PlayerInputManager 用私有方法复用自己的两个生命周期事件；背包仍拥有自身开关时的 UI 状态处理。没有在现有大文件增加无关算法，也没有每帧写入或场景扫描。拖拽结束、容器保存、搜索会话和暂停恢复的执行顺序不变。

全 Assets 静态核对不再存在 Cursor.visible=false 或锁定鼠标的写入。正式场景已引用启用的 TargetInputManager prefab，不需额外配置。两项相关已有 Play Mode 协程在隔离图形运行中通过，结果和覆盖边界见 [验证报告](../../outputs/game-cursor/validation_report.md)。系统指针不是 Camera/Canvas 渲染内容，本次不以游戏截图宣称验证了其像素形状，也未程序切换用户桌面焦点。

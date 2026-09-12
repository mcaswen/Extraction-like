# 地图编辑窗口视觉证据

来源：`Logs/MapGraphEditor/20260913-004834-668`，Unity 2022.3.62f2c1 的实际 EditorWindow，当前 `Scenezl_Final 1.unity`。窗口 1400 × 900 点，DPI 1.75，输出 2450 × 1575 像素。截图直接读取本窗口 GUIView 的渲染表面，D3D 读回方向在采集时修正，不依赖桌面前台遮挡；不是静态算法示意图，也不是正式游戏 HUD。

- [全图](01-editor-overview.png)：7 个区域、28 个规范群、28 条横竖单段边。深色底、中央区名、金色资源、红色敌人、青色撤离图标均可读；区域留白较多，位置关系保持上一轮已验收布局。龙骨礁撤离仍是已记录的真实导航孤岛，没有画假连接。
- [局部选择](02-editor-detail.png)：群的四向端口、选中边框、位置/行列锁和对齐控件可见。缩放固定选中点，画布内容裁剪在属性面板之外。

编码 Agent 已实际打开两张图检查。布局独立验收通过，错误的“28 个作者图分量”中间诊断已移除；保留真实的 2 个物理分量及可选补边预算提示。`evidence.json` 含实际尺寸、图内容指纹和绘制次数。

复现（从 Ubuntu 调用既有 Windows Unity）：

```bash
/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -File D:/Unity-Projects/Extraction-like/tools/agent-repro/Invoke-MapGraphEditorPreview.ps1 -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchRegression
```

首轮桌面屏幕读取被其他前台窗口遮挡，未作为项目图形证据归档；第二轮 GUIView 读回上下颠倒，已修正后复拍。当前正式游戏小图/大图尚未替换，P5 再检验完整 HUD 的协调度和真实路线状态。

# Unity 天空盒更换与昼夜循环教程

适用项目：Extraction-like；Unity **2022.3.62f2c1**，**URP 14**。

正式地图：`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`。

## 1. 先认识这次加入的两个版本

**版本 1：原来的明亮蓝色天空。** 原材质、原 Shader 均保留，没有被改写。切回时也会恢复安装版本 2 前的主方向光与环境光。

**版本 2：网上日空 / 星空素材的平滑昼夜循环。** 白天云层、傍晚暖色、夜晚银河星空，再回到清晨。夜晚有蓝色环境补光与柔和方向光，刻意比真实夜色明亮，方便看清道路和航空航天模型。

这是适合目前风格化场景的艺术化过渡，不是天文学级太阳、月亮和云层模拟。照片里的云不会自己飘动，太阳在照片中的位置也不会随脚本真正移动；脚本调整的是两张天空的混合与场景灯光。傍晚与清晨采用同一组暖色过渡，但灯光变化方向相反。

| 内容 | 在 Project 面板中的位置 |
| --- | --- |
| 天空盒 1 材质 | `Assets/Art/Environment/Skyboxes/Skybox_OrbitalTwilight.mat` |
| 天空盒 2 材质 | `Assets/Art/Environment/Skyboxes/Skybox_AerospaceDayNight_V2.mat` |
| 天空盒 2 Shader | 同文件夹内的 `AerospaceDayNightBlend.shader` |
| 网上下载的日空、夜空 HDR | `Assets/Art/External/Skyboxes_PolyHaven_CC0/Textures` |
| 素材来源与授权说明 | `Assets/Art/External/Skyboxes_PolyHaven_CC0/README.md` |
| 昼夜控制脚本 | `Assets/Scripts/Environment/AerospaceDayNightCycle.cs` |
| 切换菜单 / Inspector 按钮 | `Assets/Scripts/Editor/AerospaceSkyboxBuilder.cs` |
| 地图中的控制节点 | Hierarchy 中的 `Aerospace_DayNight_Skybox_V2` |

日空来源：[Poly Haven / Kloppenheim 06 Pure Sky](https://polyhaven.com/a/kloppenheim_06_puresky)。夜空来源：[Poly Haven / Rogland Clear Night](https://polyhaven.com/a/rogland_clear_night)。两张素材为 CC0，可商用与再分发，作者信息仍保留用于比赛素材说明。[授权说明](https://polyhaven.com/license)

## 2. 最简单的用法：在 1 和 2 之间切换

1. 打开 Unity，等待右下角导入 / 编译完成。
2. 在 Project 面板找到正式地图，双击 `Scenezl_Final 1.unity`。
3. **先退出 Play 模式**，确保顶部播放按钮没有亮起。
4. 顶部菜单选择 `Tools > Extraction-like > Skybox`。
5. 选择需要的版本：
   - `Use 1 - Original Bright Sky`：原天空盒 + 原灯光；不再自动循环。
   - `Use 2 - Day Night Cycle`：昼夜版本；以控制器的 `Start Phase` 为起点。
6. 按 **Cmd+S（Mac）/ Ctrl+S（Windows）** 保存场景。
7. 使用版本 2 时，点击顶部 Play，天空与灯光才会随时间变化。

当前正式地图默认使用 **版本 2、白天开始、完整周期 160 秒**。编辑模式下保持静态，避免你摆模型时天空不断变化。

如果菜单说“没有 V2 控制器”：检查打开的是否为正式地图；如果是在新地图中需要这套功能，选择 `Install V2 In Current Scene`，再保存。此菜单只设置当前打开的场景，记录该场景原天空与灯光作为它的版本 1。

不要为了切回版本 1 直接删除控制节点：先用 `Use 1` 恢复光照；确认已恢复后，再删除节点才安全。直接删除节点或仅取消勾选组件只会停止循环，**不等于完整切回版本 1**。

## 3. 不等完整周期：直接预览白天 / 夜晚

方法 A：菜单 `Tools > Extraction-like > Skybox > Preview 2`，选择 `Day / Sunset / Night / Sunrise`。

方法 B：Hierarchy 搜索 `Aerospace_DayNight_Skybox_V2`，选中节点，在 Inspector 底部点击 **白天 / 傍晚 / 夜晚 / 清晨**。

这些按钮会同时改变天空和灯光，并把 `Start Phase` 改为对应阶段。点击夜晚后进入 Play，会从夜晚开始循环。如果想从白天开始，点击 **白天** 后再保存。

如果你只想临时对比，不希望改变起点，比较完成后点回 **白天** 即可。不要在多个场景同时开启多个昼夜控制器：同一个场景的全局 `RenderSettings` 应由一个控制器管理。

## 4. 调整周期、夜晚亮度和开始时间

选中 `Aerospace_DayNight_Skybox_V2`，修改 `Aerospace Day Night Cycle` 组件。修改完成后点击底部 **应用当前参数 / 预览 Start Phase**，再保存。

| Inspector 参数 | 当前值 | 用途 / 调整示例 |
| --- | --- | --- |
| `Cycle Seconds` | `160` | **白天 → 夜晚 → 白天完整一轮**的秒数。不是白天 160 秒再加夜晚 160 秒。想慢一点填 `240`。最小有效值 10。 |
| `Start Phase` | `0` | 0=白天，0.25=傍晚，0.5=夜晚，0.75=清晨，1 会回到 0。 |
| `Animate` | 勾选 | Play 时自动循环。取消后可保持固定阶段；设置 Start Phase 后重新进入 Play。 |
| `Use Unscaled Time` | 勾选 | 使用现实秒数；游戏调整 Time Scale 时仍维持 160 秒。如果希望游戏暂停也暂停昼夜，取消勾选。 |
| `Day Exposure` | `0.55` | 日空图片的亮度倍数；不是场景灯光亮度。 |
| `Night Exposure` | `0.08` | 夜空照片 / 银河亮度倍数；想更显眼可逐步加到 0.10–0.15。网上 HDR 的曝光基准不同，这张夜空填 1 会过亮。 |
| `Panorama Rotation` | `130` | 水平旋转全景，改变云与银河朝向。 |
| `Night Tint` | 蓝色 | 给网上夜空轻微蓝色调，与场景科技灯光配合；可以改为白色以保留照片原色。 |
| `Night Visibility Floor` | 蓝色 | 夜空最低亮度底色。抬高蓝色亮度，夜空更清晰，但太高会冲淡星星。 |
| `Night Horizon Glow` | 深蓝色 | 夜间地平线渐变；同时遮住夜空原照片的沙漠地面。 |
| `Day Light Intensity` | `1.1` | 白天照到模型的主方向光强度。 |
| `Night Light Intensity` | `0.6` | 夜间蓝色方向补光。地图太暗优先小幅提高到 0.7–0.8。 |
| `Night Ambient Multiplier` | `1.25` | 夜间天空 / 水平 / 地面环境补光倍数。阴影或背面太黑时提高到 1.35–1.5。 |
| `Light Yaw` | `0` | 主方向光昼夜轨迹的水平朝向。与 HDR 照片的太阳并非物理绑定。 |
| `Refresh Environment Reflections` | 不勾选 | 可选定期更新天空环境反射；当前场景使用固定的三色环境补光，默认不做昂贵的每帧反射重算。 |
| `Reflection Refresh Seconds` | `5` | 开启上一项时的更新间隔。金属反射明显滞后才考虑启用，并观察帧率。 |
| `Secondary Directional Lights` | 地图中的另一盏全局方向光 | 已发现原地图中名为 `Point Light` 的对象实际是橙色 Directional Light，也会照亮全图。V2 控制它，避免夜晚仍一片橙色；不是路边局部灯。 |
| `Secondary Day Multiplier` | `0.3` | 其他全局方向补光在白天相对原强度的倍数，避免暖色补光过曝。 |
| `Secondary Night Multiplier` | `0.05` | 其他全局方向补光夜晚的原强度倍数，并转为蓝色。切回 V1 时恢复原值。 |

从白天开始，时间与阶段关系如下：

| 现实时间 | 阶段 |
| --- | --- |
| 0 秒 | 白天 |
| 约 40 秒 | 傍晚 |
| 约 80 秒 | 夜晚 |
| 约 120 秒 | 清晨 |
| 160 秒 | 回到白天，继续下一轮 |

变化是连续的，并非每 30 秒突然换一张图片。默认采用夜间偏蓝、昼间自然明亮、过渡偏暖的配色。

**调整建议：先看人物和路面，再看天空。** 如果模型暗而天空亮，调 `Night Light Intensity / Night Ambient Multiplier`；如果模型清楚但天空几乎全黑，再调 `Night Exposure / Night Visibility Floor`。

## 5. 把版本 2 的日空 / 夜空换成其他网络图片

这一部分是更换昼夜系统用的两张 HDR，不是替换整个静态天空盒。

1. 优先从有明确授权的网站下载 **360° 等距柱状全景 HDR / EXR**。推荐同为无地面 Pure Sky 的日空；普通横向风景照片不能直接当全景，可能出现拉伸、接缝。
2. 新建独立目录，例如 `Assets/Art/External/MySkyHDR/Textures`。把下载的 `.hdr` 或 `.exr` 拖进 Project 面板这个文件夹，保留来源 / 作者 / 授权说明。
3. 选中新纹理，在 Inspector 设置：
   - `Texture Type`：`Default`。
   - `Texture Shape`：**2D**，不能是 Cube，因为版本 2 的 Shader 使用两张 2D 全景。
   - `sRGB (Color Texture)`：关闭，HDR 按线性数据读取。
   - `Max Size`：建议 `2048`；清晰度不够再考虑 4096，内存开销会增加。
   - `Compression`：建议先 `None`，避免星空压缩损伤。
   - `Generate Mip Maps`：开启。
   - `Wrap Mode`：在各轴设置中 **U=Repeat、V=Clamp**（如显示统一 Wrap Mode，展开轴选项 / Per-axis）。
   - 点 `Apply`。
4. 找到并选中 `Skybox_AerospaceDayNight_V2.mat`，Shader 应为 `ExtractionLike > Skybox > Aerospace Day Night Blend`。
5. 将日空拖到 **Day HDR (2D Panorama)**，夜空拖到 **Night HDR (2D Panorama)**。
6. 不要只拖到控制器的 `Skybox Version 2` 槽位：该槽位需要的是 **Material 材质**，不是 HDR 图片。
7. 选中控制节点，点击夜晚 / 白天预览，调整亮度和旋转；最后点白天并保存。

也可以先复制 V2 材质（Cmd/Ctrl+D）再替换纹理，命名如 `Skybox_MyDayNight_V3.mat`，把复制品拖入控制器的 `Skybox Version 2`，这样随时可以拖回原来的 V2。

`Install V2 In Current Scene` 会重新连接本次默认的两张网上 HDR。已经手动换好纹理后，不需要再次安装；日常使用预览按钮或 `Use 1 / Use 2` 即可。

运行时脚本使用 V2 材质的临时复制品，退出 Play 不会把动画阶段写进原材质。**Play 期间调的参数通常也不会保存**；记下数值，退出 Play 后再填写、应用、保存。

## 6. 手动更换普通的静态天空盒

如果后续不用昼夜循环，或希望试一个其他静态天空盒：

1. 先使用 `Tools > Extraction-like > Skybox > Use 1 - Original Bright Sky`，确保控制器停止且原光照已恢复。
2. 将新天空 HDR 导入 Assets 的独立文件夹。
3. 在 Project 空白处右键 `Create > Material`，给材质起名如 `Skybox_MyStaticSky`。
4. 在材质 Inspector 中把 Shader 改为 **Skybox > Panoramic**。
5. 把 HDR 图片拖到材质的全景纹理槽中（通常名为 `Spherical (HDR)`），调整 `Exposure / Rotation / Tint`。
6. 顶部菜单 `Window > Rendering > Lighting`，进入 **Environment** 页。
7. 找到 **Skybox Material**，拖入刚创建的 **Material**。
8. 如果希望天空色参与场景照明，在 **Environment Lighting** 中设置 **Source = Skybox**；若只替换背景而保留手动环境光，就不要随意改这一项。
9. 按需要调整场景的 `Directional Light` 强度 / 颜色。单改天空盒不一定让地图本体变亮。
10. 保存场景；检查 Game 视图效果。

Unity 的全局天空盒是在 Lighting 的 Environment 中指定的材质；URP 支持材质天空盒。相机上的独立 Skybox 组件可能覆盖全局设置。[Unity 2022.3 官方天空盒说明](https://docs.unity3d.com/2022.3/Documentation/Manual/skyboxes-using.html)

此后再点 `Use 2`，脚本会重新接管全局天空与主方向光。如果希望刚换的静态天空作为新的版本 1，只换控制器的 `Skybox Version 1` 材质引用即可改变返回的天空背景，**原灯光记录仍是安装时的那组**。需要改变整个 V1 灯光基线时，先备份场景、切回并调整光照，再移除旧控制节点、重新安装（重新安装会重新连接默认 V2 HDR），不要在 Play 中改基线。

## 7. 已经指定天空，但看不到 / 一片黑 / 变粉色怎么办

### Scene 视图没显示天空

检查 Scene 视图上方的 **Effects / FX** 下拉项，打开 **Skybox**。不同窗口布局会把该选项折叠到不同位置。

还要打开 Scene 视图的 **Scene Lighting**（灯泡图标）才能用真实场景光照比较白天 / 夜晚；关闭时编辑器使用自己的预览灯，不等于最终游戏效果。

### Game 视图没有天空

选中实际用于游戏的相机。在 URP Camera 的 **Environment** 区域将 **Background Type** 设为 **Skybox**。如果显示的是内置管线的字段，则对应 **Clear Flags = Skybox**。

检查相机是否有独立 **Skybox** 组件：它的材质会覆盖全局天空；不需要该覆盖时停用 / 移除该组件。先备份再改，不要删除 Camera 本身。

俯视角主要看到地面时，天空不明显是正常的；可在 Scene 视图降低观察角度对比，但不要为了预览改掉正式游戏相机的构图。

### 夜空亮，但物体仍很暗

检查控制器是否启用、`Sun Light` 是否引用正式地图的 `Directional Light`。只拖入天空材质，没有控制器，不会自动得到本次夜间补光。提高夜间灯光 / 环境补光参数，而不是无限增加天空 Exposure。

如果项目后续增加固定曝光、Tone Mapping 或后期 Volume，最终亮度还会受到这些设置影响；用 Game 视图复核，不只看材质预览小球。

### 材质是粉色

看 Console 是否有 Shader 编译错误。V2 Shader 针对本项目 URP 编写，不能直接搬到 HDRP。检查 `Assets/Settings` 中的 URP 管线资产以及 Project Settings 的管线设置；不要批量转换原项目所有材质。

### 纹理槽无法接收 HDR

检查导入形状是否为 `2D`，不是 `Cube`。控制器材质槽只能放 Material；材质中的 Day / Night 纹理槽才放 Texture。

### 不循环

只有 Play 才自动循环。检查节点与组件都启用、`Animate` 勾选、`Cycle Seconds=160`。只在 Lighting 中替换材质而没有控制脚本，不会产生昼夜变化。

### 金属反射没有随着天空马上变化

这次默认不每帧更新天空反射，避免卡顿；三色环境光和方向光会持续变化。先确保反射不是某个固定的 Baked Reflection Probe 决定的，再尝试勾选 `Refresh Environment Reflections`，保持较长更新间隔。静态烘焙的反射探针不会由这个开关自动变成动态探针。

## 8. 如何撤回这次昼夜设置

最方便：菜单 `Use 1 - Original Bright Sky`，保存。V2 文件保留，不影响原版本。

想从地图中完全移除：**先 Use 1、保存**，再删除 `Aerospace_DayNight_Skybox_V2` 节点、保存。只删除节点可能留下最后一次预览的 V2 光照。

完整场景备份位于项目目录：

`UserSettings/SceneBackups/Scenezl_Final 1_Before_DayNight_Skybox_V2.unity`

该目录被 Git 忽略，仅本机保留。恢复完整备份会同时丢掉安装 V2 之后对该场景的其他修改，所以一般用菜单切回即可；不要在新摆放已经改很多之后直接覆盖整个地图。

确实要整图恢复时：保存当前新版本为另一份备份，关闭 Unity，把上述备份**复制**回正式场景 `.unity` 文件的位置，保留正式场景原来的 `.meta`；再重新打开 Unity。不要移动或删除仅有的一份备份。

## 9. 给组员共享时要提交什么

需要一起提交：V2 材质、Shader、两个 HDR、Runtime / Editor 脚本、这些 Assets 对应的 **`.meta`**、正式场景改动、素材授权说明和本教程。

不提交：`Library / Temp / Logs / UserSettings`。本机备份与预览图因此不会自动出现在组员电脑。

项目 `.gitattributes` 已将 `.hdr` 纳入 **Git LFS**。组员需要安装 Git LFS，正常 clone / pull 后执行 `git lfs pull`，不能只保留百来字节的 LFS 指针文件。不要删除已有 `.meta` 后重新导入，否则 GUID 改变可能导致材质 / 场景丢引用。

本次修改保存在本地工作分支，**并未自动推送**。之前原仓库对账号 `316sandon12` 的写权限曾返回 403；天空盒设置与仓库写权限是两件事，正式推送前仍需要确认协作者写权限或采用团队认可的分支 / PR 流程。

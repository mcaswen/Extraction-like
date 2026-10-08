# 火箭坠毁残骸 V1

正式场景：`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`

独立场景根节点：`Aerospace_RocketCrash_V1`

## 内容与来源

这是组合搭建的试摆版本，不是从网上下载的一整套“坠毁火箭”。

- 主箭体、断裂上级、头锥、侧助推器、撕裂蒙皮、断翼及贴地烧痕：本项目编辑器脚本生成的低多边形网格，存于 `Meshes/`。
- 尾部发动机与部分散落接头：Kenney Space Kit 的 `rocket_baseA`、`rocket_fuelA`、`rocket_finsA`、`rocket_topA`；原始 FBX 在 `Assets/Art/External/AerospaceKit_CC0/Models/Kenney_SpaceKit/`。
- Kenney 官方来源：https://kenney.nl/assets/space-kit
- Kenney 原始授权：CC0，文本在 `Assets/Art/External/AerospaceKit_CC0/Licenses/Kenney_SpaceKit_CC0.txt`。本文件不变更第三方素材授权。
- `Materials/`：本版本使用的 URP/Lit 白色外壳、橙色标识、烧蚀金属、暗色内壁和烧痕材质。
- `Prefabs/PFB_RocketCrash_And_Debris_V1.prefab`：完整残骸与散落零件组合。

## 布置

主残骸位于食堂外侧、林间道路旁，保留火箭尾翼、长箭体和尖头锥的轮廓，箭体断成几段并带露出的内壁和锯齿断口。另有一根脱落的侧助推器、坠毁点周围 7 件较大的零件，以及四组地图零件（食堂路肩、宿舍方向、龙骨研究区方向、渔村东侧），每组 2 件。

这是视觉试摆：未添加碰撞体、未重烘焙或覆盖 NavMesh，未修改地形数据、植被、原宝箱或敌人。确认摆放后再增加必要的简化碰撞体与导航配置。烧痕以贴地网格实现，原有高草可能遮住部分烧痕。

## 对比与撤回

- 在 Hierarchy 搜索 `Aerospace_RocketCrash_V1`，关闭根节点的勾选即可暂时隐藏本次全部内容。
- 只删除该根节点，不会删除原地图、路边 V1 或龙骨 V1。
- 完整本机恢复点：`UserSettings/SceneBackups/Scenezl_Final 1_Before_RocketCrash_V1.unity`，保留用户开始本次任务前刚保存的场景；该目录被 Git 忽略。
- 重新生成：`Tools > Extraction-like > Aerospace > Build Rocket Crash V1`。此操作会重新生成该版本的 Prefab 和网格，覆盖对这些新增资源的手动调整；请先另存版本再使用。
- 仅移除场景中的残骸与零件：`Tools > Extraction-like > Aerospace > Remove Rocket Crash V1`。

编辑器生成脚本：`Assets/Scripts/Editor/AerospaceRocketCrashBuilder.cs`。所有网格、材质和 Prefab 均为已保存的 Unity 资源，不依赖游戏运行时执行生成代码。

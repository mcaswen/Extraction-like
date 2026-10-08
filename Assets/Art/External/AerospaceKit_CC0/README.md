# Aerospace Kit CC0

这是为“龙骨礁”和林间道路航空航天化改造筛选并导入的轻量模型库。所有第三方模型均来自 Kenney 官方资源页，并按原始名称保留，方便核对来源和授权。

## 来源与授权

- Kenney Space Kit: https://kenney.nl/assets/space-kit
- Kenney Modular Space Kit: https://kenney.nl/assets/modular-space-kit
- 授权：Creative Commons Zero (CC0 1.0)
- 访问与导入日期：2026-09-16

两份原始授权文本位于 `Licenses/`。CC0 允许在个人、教育和商业项目中使用、修改与再分发；署名 Kenney 不是强制要求，但建议在最终作品鸣谢中保留。

## 文件结构

- `Models/Kenney_SpaceKit/`：卫星天线、通信设备、发电机、管线、平台、支架、货运飞行器、探测车和火箭模块。路边 V1 另补充 `rail`、`rail_corner`、`rail_end`、`rail_middle`、`supports_low`、`platform_small`、`pipe_supportLow` 七个原始 FBX。
- `Models/Kenney_ModularSpaceKit/`：电缆、舱门、激光门、模块化地板、墙体和楼梯。
- `Textures/Kenney_ModularSpaceKit/`：模块化套件使用的原始色彩图。
- `Materials/`：Unity 自动生成的 URP/Lit 材质。
- `Editor/AerospaceAssetSetup.cs`：自动建立材质并映射到 FBX；也可从 Unity 菜单手动刷新。

## 龙骨礁推荐组合

1. 中央晶体：`machine_generatorLarge` + `pipe_ringHigh` + `pipe_ringSupport` + `cables`，组合成跃迁核心/零点反应堆。
2. 龙头区域：`satelliteDish_large` 或 `satelliteDish_detailed` + `machine_wireless`，组合成生物星舰传感阵列。
3. 肋骨区域：`supports_high` + `structure_detailed` + `template-wall-detail-a`，表现暴露的舰体框架和维修夹具。
4. 地面祭坛：`platform_large`、`platform_long`、`template-floor-big`、`stairs-wide`，改造成回收/维护平台。
5. 外围叙事：`craft_cargoB`、`rover`、火箭四件套与油桶，用作考察队营地和残骸回收设备。

## Unity 使用

模型可直接从 Project 窗口拖进场景。第一次导入后，编辑器脚本会把原始材质转换为 URP/Lit 并写入 `Materials/`。

如果模型出现粉色、纯白或材质没有刷新，请执行：

`Tools > Extraction-like > Aerospace > Refresh URP Materials`

建议先用少量模型完成一处视觉焦点，再复制到其他区域。大面积铺设前请制作 Prefab，并为重复物体启用静态批处理或 GPU Instancing。

## 龙骨礁摆放版本 V1

- 场景父节点：`Zone-龙骨礁/Aerospace_SetDress_V1`
- 独立 Prefab：`Prefabs/PFB_Dragonbone_Aerospace_SetDress_V1.prefab`
- 本机摆放前备份：`UserSettings/SceneBackups/Scenezl_Final 1_Before_Aerospace_SetDress_V1.unity`（该目录被 Git 忽略，不会重复提交整份场景）
- 重新生成：`Tools > Extraction-like > Aerospace > Build Dragonbone Layout V1`
- 只移除新增摆件：`Tools > Extraction-like > Aerospace > Remove Dragonbone Layout V1`

V1 共使用 28 个模型实例，分为能源核心、传感器阵列、肋骨维修结构、南侧入口和考察设备五组。FBX 本身未添加碰撞体，因此不会改变现有 NavMesh；如果后续需要玩家站上新增平台，应单独补充简化碰撞体并重新烘焙导航。

## 食堂外林间路边摆放版本 V1

- 正式场景：`Assets/Scenes/Scene_DB/Scenezl_Final 1.unity`
- 独立场景根节点：`Aerospace_Roadside_SetDress_V1`（与龙骨 V1 分开）
- 独立 Prefab：`Prefabs/PFB_Aerospace_Roadside_SetDress_V1.prefab`
- 摆放前完整备份：`UserSettings/SceneBackups/Scenezl_Final 1_Before_Aerospace_Roadside_V1.unity`（保留原场景和龙骨 V1，被 Git 忽略）
- 重新生成：`Tools > Extraction-like > Aerospace > Build Roadside Layout V1`
- 仅移除本次路边内容：`Tools > Extraction-like > Aerospace > Remove Roadside Layout V1`

布置分为食堂外通信站、林间能源节点、研究区方向中继点和道路引导四组，共 22 个嵌套模型实例、9 个导航/状态信标和 3 盏局部工作灯。设备站采用按地形起伏计算的水平底座，不修改地形。保留原有树木、石堆、宝箱、敌人及出生点；新增模型不添加碰撞体，不重新烘焙或覆盖 NavMesh。V1 是视觉试摆，后续确认位置后再补充必要碰撞体。

可在 Hierarchy 选中根节点，取消勾选以对比前后效果；删除该根节点只会移除路边 V1。完整备份属于本机恢复点，团队共享仍应提交正式场景、Prefab、资源与全部 `.meta`。源 FBX 已匹配仓库 Git LFS 规则。

## 新增火箭坠毁残骸

完整残骸组合在 `Assets/Art/Environment/AerospaceRocketCrash/`，而不是本第三方源素材目录。它使用此处的四个 `rocket_*A.fbx` 作为发动机和散落接头，并结合本项目生成的破损箭体、头锥、断翼和蒙皮网格。

场景根节点：`Aerospace_RocketCrash_V1`；完整 Prefab：`Assets/Art/Environment/AerospaceRocketCrash/Prefabs/PFB_RocketCrash_And_Debris_V1.prefab`。组合来源、备份和撤回方法见该目录的 `README.md`。

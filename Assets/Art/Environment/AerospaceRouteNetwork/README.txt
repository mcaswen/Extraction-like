全路线航天调查设施 / 2026-10-18

正式场景：Assets/Scenes/Scene_DB/Scenezl_Final 1.unity
范围：7 个区域、26 条发布路线、75 组设备；32 个可搜索容器的外观替换。

美术内容
- 遥测太阳能站、回收发动机运输架、光学调查探头：本项目使用 Blender 制作。
- 航天回收箱：本项目使用 Blender 制作，顶部火箭标识、装甲包角、密封盖、锁扣、接口和状态灯。
- 通信天线：复用项目现有 Kenney Space Kit 素材。
- 回收运输载具和增压服务设备：本次从 Kenney 官方 Space Kit 下载 craft_cargoA.fbx、machine_barrelLarge.fbx，统一调整实例材质。
- 未加入光墙、道路中心灯带、栅栏、全屏特效或额外实时灯。

资源位置
本目录 Models / Materials / Prefabs 为 Unity 正式资源，4 个 Blender 自制模型各有 3 级 LOD。
项目根目录 SourceArt/AerospaceRouteNetwork/AerospaceRouteEquipment.blend 为可编辑 Blender 源文件。
项目根目录 tools/art/build_route_art.py 为可复现建模脚本。
外部素材：Assets/Art/External/AerospaceKit_CC0/Models/Kenney_SpaceKit

场景组织与对比
新增沿路设备集中在 Aerospace_AllRoutes_SetDress 节点，按 Route_01 ... Route_26 分类。
每个原容器下新增 Aerospace_RecoveryCase_Visual；原模型保留为不可见导航源，没有删除。
不可见专用材质不输出颜色、深度或阴影，旧 Renderer 保持原启用状态，从而保留将来导航烘焙时的几何输入。
Tools > Extraction-like > Aerospace > All Routes > Compare Original Appearance
可同时关闭新装饰并恢复原宝箱外观；Show Aerospace Appearance 恢复新外观。
不要删除 LootBoxEntity、原容器根节点或它的 Collider，否则会影响搜索。
完整场景备份位于 UserSettings/SceneBackups/Before_Aerospace_AllRoutes.unity。

安全与验证
原容器的标识、层级、搜索时间、物品表、网格、保存状态、碰撞体均保持不变。
新装饰全部 Ignore Raycast，无 Collider / Rigidbody / NavMeshObstacle / Agent / GameplayTarget。
NavMeshModifier 对新模型递归排除导航烘焙；未重烘焙或替换导航数据。
安装前后 1,512 次寻路结果及拐点、导航几何哈希一致；原有 297 个 Collider 不变。
coverage 报告每 12 米采样到最近航天物体的平面距离；不是光线遮挡测试。
另行输出每条路线与各区域昼夜游戏相机预览；原大型龙骨在部分镜头中的既有遮挡未修改。
测试入口：AgentReproduction.Tests.AerospaceRouteArtTests。
搜索/背包/五种零件和首次科普过渡沿用原系统；本功能不写入运行时保存，也不新增玩法。

回收箱开盖反馈
RecoveryCaseOpeningFeedback 在 Agent 到达或 F 交互后播放 1.08 秒开盖，再进入原搜索流程。
箱盖和左右锁扣分别活动，内舱具备托盘与状态灯；原搜索时长、保底投放规则及拾取音效不变。
同一局重访已开启的箱子不重复播放；两个 Agent 共用同一箱子的开盖状态。
新开箱音效：Assets/Resources/GameAudio/SFX/recovery_case_open.wav。
独立测试与说明：Docs/Validation/RecoveryCaseOpening_20261018。

在线素材来源与授权
作者：Kenney
原始页面：https://kenney.nl/assets/space-kit
下载：https://kenney.nl/media/pages/assets/space-kit/20874c75ac-1677698978/kenney_space-kit.zip
下载包 SHA256：d5d7cdf2635ed5a43a9187deaf409b6f47484e402321128341d3c3698e9ef4d9
授权：CC0。保留原授权文件 Assets/Art/External/AerospaceKit_CC0/Licenses/Kenney_SpaceKit_CC0.txt。

以后继续调整
安装菜单不会覆盖已存在的布局，以保护人工摆放。
手工移动设备后请保持通行中心和搜索点留白，再跑安全测试及游戏视角验收。

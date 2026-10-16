文昌一号 · 迫降景观

正式场景：Assets/Scenes/Scene_DB/Scenezl_Final 1.unity
场景根节点：Aerospace_Wenchang_EmergencyLanding_V1
预制体：Prefabs/PFB_Wenchang_EmergencyLanding.prefab
Blender 源文件：ArtSource/WenchangLanding/Wenchang01_EmergencyLanding.blend（仓库根目录下，不参与 Unity 自动导入）

沿用完整文昌一号的尖头整流罩、主箭体、双助推器、橙色识别环、中文及国旗。
新增四个着陆支撑、轻微下部变形、翘起护板、局部烟熏色、缓慢灰烟和脚底接触土堆。
完整写实母版未覆盖；游戏版使用独立几何、顶点色材质及三级 LOD。
LOD0 / LOD1 / LOD2：143431 / 74624 / 45956 三角形，每级 10 个网格 Renderer。
保留中文和国旗几何；远距离优先保留识别轮廓，删除大量亚像素螺钉和隐藏细节。
灰烟：两组有上限的粒子 + 两个各 6 三角形的柔和交叉片；不投影，不参与深度轮廓描边。

只改变景观及旧残骸配套视觉的显隐：旧横倒箭体、旧破口、长拖痕、旧烟雾和横向灯带隐藏。
保留调查设备、远景雷达、周边扫描灯，以及地图上的其他散落零件。
不添加 Collider、NavMeshObstacle、NavMeshAgent、GameplayTarget 或交互逻辑。
根节点 NavMeshModifier 对全部 Agent 类型递归排除，全部子对象使用 Ignore Raycast。
不修改 TerrainData、寻路烘焙、地图节点/路线、摄像机配置、搜集系统、科普 UI 或 160 秒昼夜周期。

对比/恢复（先退出 Play 模式）：
Tools > Extraction-like > Aerospace > Wenchang Landing > Compare Previous Horizontal Wreck
会隐藏新版本并恢复本次记录的旧景观显隐状态。
Show Upright Landing 会切回新版本。确认后自行保存场景。
不要只删新节点：这样不会自动恢复那 18 个旧视觉节点。使用上述菜单可完整对比。

制作时的完整场景备份在本机 UserSettings/SceneBackups/Scenezl_Final 1_Before_WenchangLanding.unity。
跨机器恢复请使用 Git 历史或切换菜单，不依赖本机备份路径。
Build Scene Replacement 只安装到尚未有新节点的场景；重复执行会拒绝，避免误叠加。
Render Actual Game Camera 输出白天/夜晚及道路靠近视角到 UserSettings/ScenePreviews/WenchangLanding。

资源由本项目已有文昌一号模型派生，无新增付费素材、外部下载模型或插件依赖。
PNG / FBX / Blender 源文件通过 Git LFS 跟踪。拉取后如资产只有指针文本，请执行 git lfs pull。

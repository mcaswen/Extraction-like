# P2b 布局视觉证据

各图均从隔离 Unity Edit Mode 采集的真实场景布局 JSON 渲染，1600×1000、项目 `Assets/Font/text-c.ttf` 字体、深色主题。它们是算法审阅图，尚不是正式小地图或 HUD 实拍。生成后均由 Agent 打开检查。

| 图片 | 运行 | 结果 |
| --- | --- | --- |
| [01-fixed-seed.png](01-fixed-seed.png) | `20260912-214518-209` | 旧场景 8 区域、28 群；错误空“渔村2”浮到北侧。用户随后修复，旧图仅留历史 |
| [02-corrected-scene-seed.png](02-corrected-scene-seed.png) | `20260912-220928-484` | 修正场景 7 区域、28 群；中央名称无遮挡、26 条单段横竖线、0 交叉，渔村位置仍偏南，需联合生成改善 |
| [03-joint-geographic.png](03-joint-geographic.png) | `20260912-223226-432` | 使用用户新导航烘焙，联合比较 4 个骨架；渔村位于奇点塔西侧略偏北，方位计数改善，中央名称无遮挡、26 条横竖边、0 交叉 |
| [04-with-shortcuts.png](04-with-shortcuts.png) | `20260912-230841-197` | 同一场景补入 2 条有实测绕行收益的连接，28 条横竖边、0 交叉；形成两个可读环路，名称留白保持，区域留白仍待实际窗口/HUD 检查 |

修正场景指纹 `c216c62c522bba24b146d1ad3f359938`，导航烘焙指纹 `dd2d6ed39b1e8aadb7aec3785561311f`。龙骨礁撤离旁的 `!` 表示本轮测量所得物理断连，未通过假边掩盖。

源码基线 `5043ac7` 加本阶段诊断测试及渲染脚本；原始全对导航矩阵、布局、搜索预算和校验结果在各运行目录的 `map-seed-layout.json`，实际采集结果另见 `20260912-220831-959`。

复现（仓库根目录，Python 需安装 Pillow）：

```powershell
powershell -NoProfile -File tools/agent-repro/Invoke-AgentRepro.ps1 -Mode Regression -Group MapGraphSceneLayout -WorkspaceRoot D:/Unity-Projects/.agent-repro/AnomalySearchRegression -TimeoutSeconds 300
python tools/agent-repro/Render-MapGraphPreview.py Logs/AgentReproduction/<run-id>/map-seed-layout.json outputs/map-command/visual/P2b/<iteration>.png
```

固定 MST 仅用于诊断。正式生成、编辑器画布、小地图/放大图和真实 Agent 路线尚需后续阶段实现及截图。

第三张使用 `ea073ad` 加 P2b4 联合生成实现，输入指纹更新为场景 `436c7a5c609ed5fe76a489060eb18e84` / 导航 `17e80a2593b3808e59919818228226fd`，龙骨礁撤离断连仍存在。数据源改为本轮 `map-joint-layout.json`，渲染命令相同。Geographic 胜出，工作 2.45 秒；强次轴策略耗尽自身坐标预算，结果不表示全局最优。图中区域留白和实际 HUD 的可读性留给后续真实视图迭代。

第四张使用 `ef9cd4f` 加 P2b5b 实现，指纹同第三张。新增边的原绕行比分别 2.6153、2.4766；方位反转计数 49 → 56，视觉评分 4.3939 → 4.8906，在预设上限内换取路线收益。联合工作约 4.33 秒、每批最长 22.81 ms。已实际打开检查，未把静态图当作正式 UI 完成证据。

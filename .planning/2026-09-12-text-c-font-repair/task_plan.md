# text-c「指」字形修复小规划

## 问题和验收

用户已授权直接修复字体文件。`Assets/Font/text-c.ttf` 的 Unicode U+6307（指）和 U+5740（址）映射到不同 glyph，但两个 glyph 的轮廓、hint 指令和栅格结果相同；问题在字体资产，不在指令字符串或 UI 赋值。仅修复已确认的「指」，不扩大为整套字体审校。

验收：指/址可辨，指保留字体原有风格；其余 glyph 的轮廓、指令、度量和全部 cmap 不变；字体及 TMP 缓存 GUID、材质引用保持；正式成功/失败提示渲染、淡入淡出通过。无运行时算法修改，无新增逐帧成本，不重复整场搜打撤测试。

## 文件归属和边界

沿用既有 `2026-09-11-agent-reproduction/repair_design.md` 的资产/Presentation 分工，字体数据不在 Gameplay 内修补。此次没有模块划分、公共接口或运行时依赖变化，按已授权的小规划闭环实施。

| 选择 | 具体文件 | 职责 |
| --- | --- | --- |
| Extend | `Assets/Font/text-c.ttf` | 原地重建 U+6307：取本字体「拍」的扌轮廓及「脂」的旨轮廓，保持原坐标、字宽；清除旧字形专属 hint 程序 |
| Extend | `Assets/Font/text-c SDF.asset` | 从修复的 TTF 刷新已缓存 TMP 字符，避免 TMP 界面继续使用旧轮廓 |
| Create | `tools/font-repair/repair_text_c.py` | 离线资产修复、非目标 glyph/cmap/度量差异校验、32/64/120 像素对照图；不进入运行时 |
| Create | `Assets/Scripts/Editor/FontMaintenance/TextCFontAssetRepair.cs`（及 meta） | 固定资产路径的缓存维护入口；保留资产/材质身份和字符集合，支持 batchmode，不涉及业务状态 |
| Extend | `Assets/Scripts/Editor/AgentReproduction/Tests/CommandFeedbackGraphicsTests.cs` | 正式 Text 实际栅格的指/址差异断言，延用已有截图和淡出用例 |
| Reuse | `Assets/Scripts/Editor/AgentReproduction/Reporting/CaseArtifactWriter.cs`、`tools/agent-repro/Invoke-AgentRepro.ps1` | 隔离 Unity 自动运行、截图和结果证据；不修改职责 |
| Reuse | `Assets/Resources/HUD/Pfb_AgentCommandFeedback.prefab`、`Assets/Scripts/Gameplay/Targets/Presentation/AgentCommandFeedbackText.cs` | 保留正式字体引用、布局、字符串和显示链路 |

## 步骤与风险

1. 保存坏字形证据；新增正式栅格差异断言，确认旧字体失败。
2. 离线修复 TTF，在独立输出位置预览、验证后覆盖源文件。保持 `.meta` 不变。
3. 在隔离 Unity 工程重建现有 TMP 缓存，检查所有原字符仍存在、无丢失字形，回传资产；不改变用户打开的 Editor 状态。
4. 运行 Graphics 定向用例，读取截图，记录审查后提交，正文使用自然中文。

重建 TMP 会改变图集像素和打包位置，但不能改变外部材质/字体引用或丢失字符。如果全量重建无法容纳原字符，改为只更新目标 glyph 的缓存并记录原因，不擅自缩小字号、图集或删字符。拼合采用本字体原有部件，是局部字形重建，并非声称找回作者的原始设计；不引入其他字体授权或风格。

## 实施结果

已实现原地 TTF 修复、TMP 定向更新、正式 Text/TMP 栅格差异断言。TTF 保存时避免 fonttools 对无关 cmap/post/hhea 的自动规范化；保留这些表的原始字节和原 hmtx 布局。

TMP 整表重建实际无法容纳所有字符，按规划备选改为仅更新 glyph 2907。审查时发现缓存维护会打开图集可读性，已通过 SerializedObject 恢复原来的 false，避免额外持有纹理 CPU 副本。保留所有 GUID、材质、图集尺寸和其余字符数据。

红灯和首次绿灯、资产校验均已记录在 [修复报告](../../outputs/font_repair_report.md)。最终 `20260912-175651-635` Graphics 1/1 通过，含 Text、TMP 的实际字形差异和提示淡出；5 张原始截图已逐一读取。实现完成，[架构审查](architecture_review.md)通过。

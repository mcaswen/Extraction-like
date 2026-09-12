# text-c「指」字形修复

2026-09-12。修复资产为 `Assets/Font/text-c.ttf`、`Assets/Font/text-c SDF.asset`。原字体中的「指」（U+6307）和「址」（U+5740）虽有不同 glyph index，却具有相同轮廓和 hint 程序，所以正确的字符串也会显示成「址令下达成功」。

## 修复内容

- 从同字体「拍」取扌，从「脂」取旨，保持部件原坐标、轮廓方向和字宽，重建「指」。没有混入另一种字体。移除仅适用于旧错误轮廓的 hint 指令。
- 保留全部 cmap、glyph 顺序和其他字形；TTF 只有 `glyf`、`loca`、`hmtx` 和 `head` 数据表改变，其中 `loca` 为字形偏移，`head` 含字体校验值。没有改变 family 名称或 `.meta` GUID。
- TMP 使用现有图集空白位置重新缓存「指」，保留其他字符 UV、字形、材质和图集身份。原来的旧格位保留为已占用，避免错误修改现有打包记录。维护工具重复执行时检测该次修复后的度量，直接返回，不持续占用空间。
- 保留图集 4096×4096、字号 77、原本不可读的纹理配置；没有新增图集或保留额外可读纹理副本。Prefab、场景、运行时文案和指令逻辑不变。

## 验证证据

1. [字体对照图](feedback/font-repair-comparison.png)：32、64、120 像素下，「指」正确可辨，「址」保持原样。
2. [TTF 差异校验](feedback/font-repair-verification.json)：7,782 个 glyph 中仅 `uni6307` 改变；其他 7,781 个 glyph 的轮廓和 hint、度量保持；全部字符映射保持。提示中的其余字符也做了三个字号的栅格一致性检查。
3. [TMP 差异校验](feedback/font-repair-sdf-verification.json)：3,631 条字符记录和顺序不变，仅 glyph 2907 更新；3,630 个其他字形记录完全相同。像素变化仅位于新格位 `(3693,3866)–(3771,3924)`，原位置全部为空白，没有覆盖任何已有内容。
4. Unity 红灯：`Logs/AgentReproduction/20260912-174711-280`，正式 Text 渲染「指」「址」的差异为 **0 像素**，新增断言正确失败。失败还暴露原夹具没有在断言失败时解绑 Camera.targetTexture，已用 finally 修正清理。
5. Unity 首次绿灯：`Logs/AgentReproduction/20260912-175306-825`，正式字体差异 **374 像素**，成功/失败/暂停下淡出用例通过（5.11 秒）。最终补充 TMP 实际渲染验证的结果见下方。

维护日志：`Logs/FontRepair/rebuild-target.log` 定向更新成功，`rebuild-idempotent.log` 重复执行不修改；`rebuild-preserve-readability.log` 验证保留原纹理可读性。缓存重建曾尝试整表重新打包，但无法容纳现有全部字符，未回传该失败结果；最终改为定向更新。一次维护工具编译因调用 Unity 内部 API 失败，已改为公开 SerializedObject API，并实际运行通过。

## 维护入口与边界

- `tools/font-repair/repair_text_c.py`：依赖 Python 的 fonttools、Pillow。输入修复前 TTF，输出候选字体、对照 PNG 和 JSON；不直接覆盖输入，检测到已修复字体会拒绝重复拼合。
- `Assets/Scripts/Editor/FontMaintenance/TextCFontAssetRepair.cs`：固定资产路径的 Editor 维护入口，`-executeMethod TextCFontAssetRepair.Rebuild -quit`。先修 TTF，再显式更新缓存；不在导入、启动或 Update 中自动执行。
- `CommandFeedbackGraphicsTests`：在原有正式反馈用例中比较实际渲染，不再仅以 HasCharacter 判断字形正确。

此次仅修复用户指出的「指」字，不声称完成整套字体的语义审校。原字形是用本字体已有部件重建，未声称恢复作者原稿。

最终 Unity 验证 `Logs/AgentReproduction/20260912-175651-635`：**1/1 通过**，正式 Graphics 用例耗时 4.69 秒，包含 Text **374 像素**差异、TMP **448 像素**差异、成功/失败原因、暂停下淡入淡出。源文件指纹门禁通过。此次扩展现有测试，未新增用例数，也未运行无关全项目测试。

Agent 已读取最终原始截图：[成功](feedback/font-repaired-success.png)、[失败](feedback/font-repaired-failure.png)、[消退](feedback/font-repaired-fading.png)、[隐藏](feedback/font-repaired-hidden.png)、[TMP 缓存](feedback/font-repaired-tmp-corrected.png)。成功和失败首字均为正确的「指」。审查结果见 [architecture_review.md](../.planning/2026-09-12-text-c-font-repair/architecture_review.md)。

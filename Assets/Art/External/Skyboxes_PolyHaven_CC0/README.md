# Poly Haven 天空素材（CC0）

用于天空盒版本 2 的网络 HDRI；版本 1 不在此文件夹，不会被替换。

| 文件 | 原始页面 | 作者 | 下载尺寸 / 校验 |
| --- | --- | --- | --- |
| `Textures/kloppenheim_06_puresky_2k.hdr` | https://polyhaven.com/a/kloppenheim_06_puresky | Greg Zaal（原始摄影）、Jarod Guest（天空编辑） | 2K HDR，4,434,394 bytes；MD5 `590a829b3cf71216451655e601d542c6` |
| `Textures/rogland_clear_night_2k.hdr` | https://polyhaven.com/a/rogland_clear_night | Greg Zaal | 2K HDR，6,838,156 bytes；MD5 `bd652268098521c5fd23e9fd1a4368b0` |

授权：CC0 1.0，可商用、修改、随项目再分发；无需强制署名，仍保留作者信息方便比赛素材说明。

- Poly Haven 授权说明：https://polyhaven.com/license
- CC0 法律文本：https://creativecommons.org/publicdomain/zero/1.0/legalcode
- 素材尺寸及下载地址来自官方公开 API：`https://api.polyhaven.com/files/<asset-id>`。
- 下载日期：2026-09-17。

下载文件保持原始内容；Unity 导入设置为线性 HDR、2D 全景、2048 尺寸。夜空原照片的地面在自定义 Shader 中通过地平线渐变遮蔽，没有修改源文件。

渐变 Shader、两分钟循环控制脚本、夜间可读性补光为本项目新增代码，不是网站提供的昼夜系统。
天空材质：`Assets/Art/Environment/Skyboxes/Skybox_AerospaceDayNight_V2.mat`。
详细教程：`Docs/Unity天空盒更换与昼夜循环教程.md`。

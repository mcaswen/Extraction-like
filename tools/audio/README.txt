ASTRA 稀有零件拾取音效

当前音效：用户于 2026-10-08 选定的「厚实锁定 · 加重版」（第 3 款微加重试听）。
发布与检测工具：build_discovery.py（Python 3 + numpy）。它原样复制已确认母版，不再合成旧版共鸣。
母版：sources/approved/precision_lock_weighted.wav，与游戏资源使用相同 Git LFS 对象。
refine_discovery.py 是试听被否定的上一版加重尝试，仅保留历史，不用于当前资源。

从仓库根目录发布并检测（不会改动 .meta）：
python3 tools/audio/build_discovery.py --output Assets/Resources/Aerospace/RareDiscovery.wav --report Docs/Validation/AerospaceSelectedAudio_20261008/audio-analysis.json

运行波形检查：
python3 tools/audio/test_build_discovery.py -v

声音结构：清脆机械触点 → 短促低中频重量 → 快速收尾，不带固定音高的乐器长鸣。
时长 0.84 秒，48 kHz，16-bit stereo PCM。约 108 毫秒内完成 99% 的声音能量。
选定文件 SHA-256：f6d684435787282a18c3dc3cb4bd669026a824bb9592297bba1cec6699ee2ba6。
沿用原 RareDiscovery.wav 路径和 .meta GUID，不需要重新挂载到场景。
Unity 使用 2D AudioSource、音量 0.85；ignoreListenerPause 保证科普暂停时继续播放。
不修改 AudioListener.volume，不让普通拾取、已收集零件重复转移或满包失败误触发。

物理质感层来自 Kenney Impact Sounds（CC0）；sources/kenney-impact 内保留两个
输入 WAV、原许可和来源记录。选定母版将这些物理触点与原创短噪声层混合，并补充短促低中频。
没有使用三角洲行动的音轨、截取录音或游戏资产，也不声称与其音效逐一匹配。

检查范围包括确定性、峰值及四倍过采样峰值、无削波、首尾收束、及时起音、
左右声道与单声道合并兼容。技术检查不能代替用户对具体听感的评价。

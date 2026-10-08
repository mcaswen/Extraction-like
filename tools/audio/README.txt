ASTRA 稀有零件拾取音效

当前生成器：build_discovery.py（Python 3 + numpy）。
refine_discovery.py 是试听被否定的上一版加重尝试，仅保留历史，不用于当前资源。

从仓库根目录生成：
python3 tools/audio/build_discovery.py --output Assets/Resources/Aerospace/RareDiscovery.wav --report Docs/Validation/AerospaceFinalPolish_20261004/audio-analysis.json --preview Docs/Validation/AerospaceFinalPolish_20261004/Discovery_Preview.wav

运行波形检查：
python3 tools/audio/test_build_discovery.py -v

声音结构：短金属触点 → 约 70 毫秒处展开的主体共鸣 → 渐弱的空间尾音。
时长 1.95 秒，48 kHz，16-bit stereo PCM。主体居中，只有少量尾音形成左右差异。
沿用原 RareDiscovery.wav 路径和 .meta GUID，不需要重新挂载到场景。
Unity 使用 2D AudioSource、音量 0.85；ignoreListenerPause 保证科普暂停时继续播放。
不修改 AudioListener.volume，不让普通拾取、已收集零件重复转移或满包失败误触发。

物理质感层来自 Kenney Impact Sounds（CC0）；sources/kenney-impact 内保留两个
输入 WAV、原许可和来源记录。其余起音、共鸣与空间层由本项目生成器合成。
没有使用三角洲行动的音轨、截取录音或游戏资产，也不声称与其音效逐一匹配。

检查范围包括确定性、峰值及四倍过采样峰值、无削波、首尾收束、及时起音、
左右声道与单声道合并兼容。技术检查不能代替用户对具体听感的评价。

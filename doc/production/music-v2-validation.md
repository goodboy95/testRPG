# 第二室音乐教室 v2 · 实际验收记录

日期：2026-09-09。`[已实现]` 环境与表现接入完成；以下是自动化与实际截图检查，人工视觉终验和玩家盲测尚未完成。

[本机对照页](../../outputs/music-v2/review.html) · [俯视布局](../../outputs/music-v2/floor-plan.svg) · [实现与来源](../engineering/music-remodel-v2.md)

## 采用资产

| 项目 | 实际值 | 上限 |
| --- | ---: | ---: |
| 房间 | 12×10×4 m | 已确认尺寸 |
| 东北器材间 | 3.2×3.2 m，门洞 1.4 m | 门洞 ≥1.2 m |
| 三角面 | 58,544 | 180,000 |
| 可见网格保守上限 | 68 | 220 |
| 材质 | 15 | 24 |
| GLB | 30,024,224 bytes / 28.63 MiB | 60 MiB |
| 纹理估计 | 192 MiB，9 张 2048² 图片，含 mipmap | 记录值，非 GPU 实测 |

纹理复用已采用 Poly Haven CC0 的木材、抹灰墙和水磨石三套资源。未生成图片、采购或引入新依赖。模型不含角色、动画、相机或灯光。固定引擎 4.6.3 Mono、Compatibility，Core/net8.0 与内容版本 6 不变。

## 验证结果

| 验证 | 结果与范围 |
| --- | --- |
| Core / 内容 / 移动 | **135/135 通过，0 跳过**。134 基线上新增 1 项实际音乐室 GLB、布局哈希、家具、门轴、状态根、预算和源/产物一致性检查；未新增或修改本轮 Core 规则。 |
| Godot Debug 编译 | **0 警告、0 错误**。 |
| Blender 只读复导 | `MUSIC_V2_CHECK_PASS room=12x10x4 equipment=3.2x3.2 readOnly=true`；源/GLB 哈希、实际结构、几何和纹理估计一致。 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。包含全部房间、角色移动、13 段故事、合成 UI 输入与取消。 |
| 反复往返 | `MANOR_V3_ROUNDTRIPS_PASS rooms=4 repeats=2 transitions=16 camera=true`，含音乐室两轮出入与相对镜头角度检查。 |
| 音乐室专项 | `MUSIC_V2_PROBE_PASS targets=18 routes=20 barriers=4 restores=38 companion=true graybox=False`。灰模先通过相同布局检查，再制作细模。 |
| UI 专项 | 1280×720、1600×900、1920×1080 × 100%、125%、150%，**9 组全部 PASS**；每组 9 图，合计 **81 图**。 |
| 场景截图 | 三档分辨率各 24 图，合计 **72 图**；加 UI 共 **153 图**，PNG 实际尺寸审核通过。 |
| 文件检查 | 本轮脚本可解析；相关文档链接与本机对照页资源检查，`git diff --check` 通过。 |

音乐室专项使用场景中的真实 `CharacterBody3D` 胶囊和 `TestMove` sweep 检查 20 条双向通路：门内外、中央三个横坐标、座椅西侧、椅后横路、钢琴前与北侧工作台。另检查未解锁门、隔墙、打开门扇、西侧出口的四条阻挡。31 个碰撞定义中开关门扇互斥；16 件家具的实际 GLB AABB 与碰撞占地匹配。

38 次实例恢复覆盖 16 件家具中心、门口、隔墙和器材间内点的关闭/打开两种状态；修正坐标写回旧二维坐标字段，有效器材间内点保持不动。18 个标记在相应可交互阶段均可聚焦，三组资料与对应机关距离均 ≥2 米，入口不自动聚焦。

晓雨专项逐步调用实际同行逻辑，检查介绍模态不移动、门扇动画完成后出门、四段绕行、校准期间走向侧面观察位、关闭后归队、中途取消后被其他模态暂停并继续，以及异常越界恢复。正常路径逐步断言无障碍侵入、无瞬移。必要合作观察在界面立即可读。

截图包含未加入、刚开门、工具拾取、部分安装、谱面拓显、校准、完成、纸带、合影回访与同行。23 号板面特写隐藏人物；24 号为美术检查，临时把门叶摆回关闭角度以查看内侧编号，不能当成玩家可在锁闭器材间自由行走的证明。其余为真实第三人称镜头，保留人物遮挡。UI 图包含隔门必要观察、校准及合作图、纸带、合影、声学、三级求助和独立记录；没有恢复机关关联资料。

## 帧时与运行边界

设备：NVIDIA GeForce RTX 2080 Ti，OpenGL Compatibility。主室与器材间各环绕 180 帧，三分辨率各两组；启用当前帧率同步。最新精确数据在 [metrics.json](../../outputs/music-v2/probe/metrics.json)。最终结果均值约 16.69 ms，P95 约 16.77–16.83 ms；这受 60 Hz 同步限制，不代表显卡极限性能。资源读取约 0.59 秒、场景 Ready 含角色和五帧约 0.42 秒是热缓存观测，不能当作冷启动承诺。

首次并行执行的九场景冒烟在礼堂左侧拉杆合成输入断言处失败（`outputs/music-v2-smoke.log`）；没有修改礼堂逻辑，单独复跑与最终采用资产复跑均通过。原因未单独确证，保留日志作为时序偶发记录。早期图形 UI 试跑有退出资源未释放日志，专项加入图像释放和退出清理后九组正式日志均干净。灰模和早期预览位于 `graybox/`、`preview/`；未计入当前 153 图。

本轮图形检查使用 `--audio-driver Dummy`，仅验证四个原有离线 WAV 的引用和状态播放接线，未验收真实听感。未进行三名玩家完整盲测、真实物理 raw delta / Xbox、GTX 1660 性能、Windows 导出或本机角色分发。目标 60–90 分钟和最终视觉质量仍须人工验收。

## 复现与本机产物

根目录 PowerShell，依次运行；不要并行开启多个图形捕获实例：

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& 'C:/Program Files/dotnet/dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python scripts/tools/remodel_music_v2.py -- check --root .
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --audio-driver Dummy --scene res://scenes/testing/MusicV2Probe.tscn
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --audio-driver Dummy --scene res://scenes/testing/DreamThoughtProbe.tscn -- --music-only --output=outputs/music-v2/ui/1280-1 --width=1280 --scale=1
& C:/Python314/python.exe scripts/tools/review_music_v2.py --root .
```

UI 替换 `width` 为 1280、1600、1920，`scale` 为 1、1.25、1.5，使用不同输出目录。无图形音乐室行为检查可加 `--headless` 与 `-- --music-behavior-only`。Blender `check` 不覆写采用资产。

主要日志：`outputs/music-v2-core-tests.log`、`music-v2-build.log`、`music-v2-model-check.log`、`music-v2-smoke-final.log`、`music-v2-final-probe.log` 和九份 `music-v2-ui-{width}-{scale}.log`。截图尺寸清单为 `outputs/music-v2/capture-audit.json`。

本机角色与含角色截图受原有禁止提交、推送、再分发和导出约束，`outputs/music-v2/` 已加入本地 Git 排除；未进行 Git 提交、推送或 Windows 导出。旧 v1 程序源快照与截图明确标为历史。

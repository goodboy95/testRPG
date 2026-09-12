# 档案与录音修复室 v2

截至 2026-09-10：`[已实现]` 第三室采用 14×12×4.2 米独立环境。13 个交互 ID、检索答案、保险丝装配、回访奖励、录音机修复和时间线结果保持不变，内容版本仍为 6。验证证据与未完成人工项见[两室验收](../production/late-rooms-v2-validation.md)。

## 布局与实物

东墙入口宽 1.6 米；西侧、北侧为文件架，南侧为分类柜、索引纸和时间线台。中央检索桌与抽屉柜分开；东侧分别布置保险丝夹具与录音机工作台。中央净通路 2.8 米，主要次通路 1.5 米；入口安全点 `(4.5,0,4.2)`，录音机合作位置 `(4.2,0,2.2)`。各资料均有纸张、夹板、桌面或柜面承托，标题使用贴附纸面的本地化文字。

模型包含抽屉、拉手、文件盒、可取索引纸、保险管、线材、夹具、摇柄和卷盘。检索拉开实体抽屉；拾取移除索引纸；部分安装只显示对应录音机部件，启动后卷盘转动；时间线整理切换纸面实物。移除了旧悬浮标签和无关发光机关。南侧真实窗洞、窗框、玻璃、墙裙、天花板与工作灯组成封闭房间。采用 Compatibility 支持的 Color 环境光，无 SSAO；玻璃不参与投影以保留窗光。

## 共享布局与状态边界

`scripts/presentation/ArchiveRoomLayout3D.cs` 是 Blender 生成、20 组碰撞、13 标记、门轴、旧档恢复和晓雨路径的共同来源。抽屉伸出区有保守碰撞，避免打开后穿入。`LateRoomLayout` 提供 0.32 米胶囊边距、路点与安全恢复；`GroundWaypointNavigator3D` 缓存可见连接并连续绕过家具。`DreamArchiveRoomController3D` 在基类初始化前应用标记，普通模态暂停跟随，录音机合作不引入额外等待。

存档仍使用 `(16,54,608,272)` 旧二维坐标矩形，映射至新房间内缩范围。有效位置保持；落入柜体、墙体或抽屉阻挡的位置就近恢复。临时路径属于表现层；Core、存档结构与剧情事件未改变。

## 源与复验

- 源：`art/blender/dream_late_rooms/dream-archive-room-v2.blend`。
- 运行时：`assets/generated/dream_escape/3d/dream-archive-room-v2.glb`，同目录 `-manifest.json`。
- 生成器：`scripts/tools/remodel_archive_v2.py`；v1 文件保留历史，不能用 v1 构建器覆盖 v2。
- 来源记录：`assets/generated/provenance.json` 的 `dream_archive_room_runtime_v2`；复用已采用的 CC0 木材、抹灰与水磨石。Blender 版本、制作指令、布局/源/GLB SHA-256、后处理与参数在 manifest 中。

```powershell
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python scripts/tools/remodel_archive_v2.py -- check --root .
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --audio-driver Dummy --scene res://scenes/testing/ArchiveV2Probe.tscn
```

`check` 只读复验当前采用文件；`build`/`graybox` 是有意重做资产的命令，不用于日常验收。实际为 90,240 三角面、53 网格对象、13 材质、32,433,952 字节，低于 180,000／220／24／60 MiB 上限。源和 GLB 不含本机角色；运行时截图中出现的本机角色仍禁止提交、分发与导出。

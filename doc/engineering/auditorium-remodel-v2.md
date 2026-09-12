# 旧学校礼堂 v2

截至 2026-09-10：`[已实现]` 第四室采用 22×16×7.5 米独立环境。保留 15 个交互 ID、双视角资料、舞台答案、磁带顺序、四步交接和 `ExitOpened` 完成页，内容版本 6 不变。实际验收与人工边界见[两室验收](../production/late-rooms-v2-validation.md)。

## 布局与实物

南侧入口，北侧为宽 14 米、深 4 米、高 1.1 米景观舞台。台下、两侧和后部工作区都在 Y=0；实体碰撞隔离舞台，无可攀登入口。中部两组观众席，每组四排四座，共 32 座，Blender、GLB 和 Godot 中均复用同一座椅网格。中央净通路 3.38 米，主要次通路至少 1.5 米，南侧入口安全点 `(0,0,5.2)`。

西侧为放映机与侧台观察位，东侧为吊杆配重、剪带台及母带设备；后部控制台避开入场轴线。模型包含台口、褶皱幕布、吊杆、滑轮、四只活动配重、三只影盘、镜头、卷盘、两侧手柄和小型实体指示灯。纸面线索有实物承托，完整内容仍通过独立资料界面阅读。

舞台预览调整四配重高度和三影盘角度；关闭即恢复持久状态。幕布从固定顶部收起，已安装配重跟随机构；镜头、剪接卷盘、母带和出口分别显示实际安装/完成状态。母带沿用离线白噪声与文字说明。持杆、返听、收光只影响当前实例；打开其他模态、离房或重载后重新协作。景观门与原有完成页同时对应现有出口终点。

## 共享布局与移动

`scripts/presentation/AuditoriumLayout3D.cs` 驱动建模、56 组碰撞、15 个标记、门轴、映射及晓雨路径。座椅、设备和舞台碰撞参与 `LateRoomLayout` 安全点判定；`GroundWaypointNavigator3D` 在当前房间连续绕行。侧台观察及左侧操作位沿真实通路可达，不把到达延迟加入解谜条件。存档保持原二维位置格式，有效坐标保留，障碍内坐标就近恢复。

沿用既定 Orbit 相机 FOV 60°、俯角 -21°、臂长 3.4 米、跟随高度 1.3 米。普通近距离画面可能裁去高台口，不因此修改全项目相机基线。房间使用 Color 环境光、窗光与台口/后部灯，不启用 SSAO。

## 源与复验

- 源：`art/blender/dream_late_rooms/dream-auditorium-room-v2.blend`。
- 运行时：`assets/generated/dream_escape/3d/dream-auditorium-room-v2.glb`，同目录 `-manifest.json`。
- 生成器：`scripts/tools/remodel_auditorium_v2.py`，复用档案室生产辅助函数；v1 保留历史，禁止旧构建器覆盖 v2。
- 来源：`assets/generated/provenance.json` 的 `dream_auditorium_runtime_v2`。复用已采用 CC0 木材、抹灰、水磨石与 `fabric_pattern_07`；制作指令、版本、参数、后处理和 SHA-256 在 manifest 内。

```powershell
& 'D:/ProgramFiles/Blender/blender.exe' --background --factory-startup --python scripts/tools/remodel_auditorium_v2.py -- check --root .
& 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe' --path . --audio-driver Dummy --scene res://scenes/testing/AuditoriumV2Probe.tscn
```

实际 86,004 三角面、97 网格对象／66 唯一网格、15 材质、32,833,880 字节，低于 260,000／320／32／80 MiB 上限。`check` 为只读；`build`/`graybox` 会重建候选，仅用于有意制作。角色资产不在环境 GLB 内，本机角色及其截图的提交、分发与导出限制不变。

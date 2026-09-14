# 村庄地形正式采用验收

2026-09-14。负责人查看三处地形样板后确认“看着不错，可以覆盖”，授权将当前数据用于正式村庄与默认 v2 预览。视觉方向已确认；此确认不代表真实手柄、目标硬件或全村精修验收。

## 交付范围

- `[已实现]` 正式醒后出门与继续村庄存档加载 Terrain3D 1.0.2；默认 `VillagePreview.tscn` 使用同一地形。`VillageTerrainPreview.tscn` 保留地形专用标题及独立走查入口。
- 独立预览追加 `-- --village-original-terrain` 回归原 v2 地面；`-- --village-v1` 保留原 v1，灰模仍采用原地面。`--terrain-baseline` 保留测试别名；正式模式忽略回归开关。
- 旧档 X/Z 在安全范围内保持，Y 与当前地面相差不超过 0.5 米时贴合地面再校验碰撞；严重高度错误、越界和建筑内落点回当前区域安全点。进入住宅停用缓存地形碰撞，出门恢复。
- 本轮复用全部已保存地形、纹理、草石、插件与模型，不改资产内容。只有家门外道路、广场边缘、南桥西岸是精修；其余为基础地形迁入。
- 没有 Core、存档字段、地点标识、内容版本 6、剧情、移动速度或镜头参数变更。独立预览不读写正式存档。

## 本轮验证

实际日志位于 [outputs/village-terrain-adoption](../../outputs/village-terrain-adoption)。本轮复用既有规则和内容测试，新增正式引擎恢复断言与默认入口／回归入口断言，不新增 Core 测试。

| 检查 | 本轮实际结果 |
| --- | --- |
| Core／内容／表现层规则 | 182 项通过，0 失败、0 跳过；测试数量不变。见 [日志](../../outputs/village-terrain-adoption/tests.log) |
| Godot Debug 构建 | 0 警告、0 错误。见 [日志](../../outputs/village-terrain-adoption/build.log) |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，退出码 0；见 [日志](../../outputs/village-terrain-adoption/smoke.log) |
| 正式恢复与住宅往返 | 102 项通过、退出码 0；三分辨率字幕／卧室／暂停／家门／村庄共 15 图。三处实际改变高度的旧档保留安全 X/Z、重复存读档、建筑重叠回退、门灯与地形碰撞切换、ExitOpened 收尾、旧完成档、Legacy 及正式档不受测试写入均通过。见 [日志](../../outputs/village-terrain-adoption/formal.log) |
| 默认 v2 入口 | 399 项通过、退出码 0；25 条路线记录、三分辨率共 18 图，含三条原村庄路线、工作净空、桥头、住宅往返与 billboard 回退。见 [报告](../../outputs/village-terrain-adoption/20260914-191508-default/results.json) |
| 原 v2 地形回归 | 使用 `--terrain-baseline --village-batch-only`，132 项通过、退出码 0；包含原地面选择、工作区域及住宅往返，未重复整村三条长路线。见 [日志](../../outputs/village-terrain-adoption/original.log) |
| v1 回归 | 使用 `--village-v1 --village-batch-only`，48 项通过、退出码 0；原地形、3.1 米/秒、暂停／镜头／住宅往返／回退保留，未重复整村三条长路线。见 [日志](../../outputs/village-terrain-adoption/v1.log) |

正式与默认地形日志各保留一条上游 `instance_reset_physics_interpolation()` 弃用警告，无运行错误；没有修改上游二进制掩盖警告。抽查本轮正式家门与默认广场截图，场景与 HUD 正常；原道路的硬质几何边缘仍属于既有表现，不新增全村美术完成声明。

默认入口本轮记录加载约 4494.6 ms；这次不是冷启动或同机性能对比实验，不与回归入口的偶发加载耗时推导性能结论。资源与预算未改变，沿用已保存数据；上一轮性能与视频见历史验收。

本轮文档本地链接检查通过，AGENTS 到全部文档不超过两次点击；`git diff --check` 通过，仅保留既有换行转换提示。开工快照对比为 15 个既有文件修改、1 个新文档、无删除；资产、插件、Core、数据和本地化无变化。审查结果保存于本轮输出目录的 `links.json` 与 `increment.json`。

## 复现与边界

```powershell
# 正式入口：继续已有现实档，或梦醒后从住宅出门。
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path .
# 默认独立预览
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --path . --scene res://scenes/testing/VillagePreview.tscn
# 默认入口路线与地形专项（输出本轮独立目录）
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --terrain-adoption --terrain-check --village-capture
# 正式存档专项，使用隔离存档
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageWakeProbe.tscn -- --prologue-e2e --terrain-adoption
```

本轮不重新绘制／生成地形，不重跑完整序章输入通关或录像矩阵，不运行 Blender、不导出游戏。样板原有预算、来源和视频保留为上一轮事实，见[制作说明](../engineering/village-terrain-sample.md)及[样板历史验收](village-terrain-validation.md)。本机角色与画面继续仅限本机非商业，不提交、推送或分发。

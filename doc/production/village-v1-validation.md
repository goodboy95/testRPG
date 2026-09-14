# 村庄 3D v1 验收记录

日期：2026-09-12。`[已实现]` 以下为最终工作副本的自动检查与本机图形记录；负责人视觉确认仍为 `[计划]`。场景范围、状态边界及制作命令见[实现说明](../engineering/village-v1.md)。

## 资产与构建

- Core 构建成功，完整测试 **162/162**、0 失败/跳过：原住宅 155 基线＋6 项村庄布局/地形规则＋1 项实际 GLB 契约。
- Godot 4.6.3 .NET Debug 构建 **0 警告、0 错误**；资源重新导入完成。
- 原九场景回归通过 `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`，日志 `outputs/village-v1-final-nine-scene-smoke.log`；同时出现动作、洋房往返、序章 v6 和 UI 专项通过标记。
- 原独立住宅图形回归通过 `HOME_V1_PROBE_PASS checks=179 graybox=False loadMs=687.4`，含三分辨率 84 张图；日志 `outputs/village-v1-final-home-regression.log`。当前 `outputs/home-v1/validation/` 更新为这次回归，早期较慢加载数据仍见住宅历史日志。
- `build_village_v1.py check` 通过：源/布局/GLB/原住宅源哈希、实际地形顶点、单位法线、UV、24 栋建筑位置、共享民居与树木、无角色/动画载荷、打包纹理及临时目录确定性复导。
- 最终资产：634,421 三角面，207 网格对象／139 独立源网格，34 材质，GLB 39.47 MiB，纹理估算 172 MiB，均低于本版预算。
- 村庄仅从住宅 v1 派生外观；原住宅 Blend 哈希继续匹配 manifest。所有角色资源与角色画面保留本机限制。

## 灰模与连续行走

`[已实现]` 先完成共享总图与灰模输入验证，记录在 `outputs/village-v1/graybox/results.json`（229 项检查）。灰模源只在 `work/village-v1-graybox/`；其加载 22.45 秒是早期过程数据。最终材质与山体调整后重新执行正式村庄完整专项。

`[已实现]` 最终 `VILLAGE_V1_PROBE_PASS checks=405 graybox=False loadMs=1164.0`。路线使用引擎动作输入驱动真实角色碰撞与重力，连续走完整条路线，未在路线段之间传送；各独立用例开始前设置测试落点。路线测试使用 3 倍仿真速度，下表是累计物理时间，不能当作人工计时。

| 路线 | 实测仿真时间 | 经过高度 |
|---|---:|---:|
| home_shop | 30.05 秒 | -0.68–1.94 米 |
| creek_loop | 124.20 秒 | -0.81–3.27 米 |
| rail_loop | 88.60 秒 | 1.60–5.90 米 |

家到杂货铺约 90.5 米，理想匀速慢跑约 29.2 秒，实际计入起停/转向为 30.05 秒。溪岸 124.2 秒为完整回环而非村庄两端单程；旧站回环 88.6 秒。两座桥在中心及左右 0.65 米偏移分别双向走过，共 12 次，每次约 10.8–10.85 秒；桥头无阻塞。另检查慢走/慢跑、地面跟随、桥栏、水岸及外围碰撞、贴墙镜头压缩/恢复和高度隔离交互。

## 住宅往返与状态

- F 从村庄进屋，A/F 从住宅出门；往返反复操作同一灯开关验证订阅没有叠加。
- 转场拒绝重复请求，活动树中始终一个区域；户外开启地形移动，室内保留平地移动。
- Esc 暂停、B 恢复、暂停禁移动/释放鼠标、模拟失焦清零、鼠标环绕输入通过；这些是注入输入与通知检查。
- 住宅灯态往返保持，重置释放缓存住宅；缺失模型安全回退 billboard。
- 正式会话序列化和正式存档文件前后完全一致，没有接入正式剧情和保存路径。

最终村庄初次实例加载 **1.164 秒**，首次进屋转场 **1.004 秒**，后续转场 **0.265–0.416 秒**。转场配置为淡出/淡入各 0.2 秒；表中为实际计时。首次实例数据来自已经导入资源的工作机，未清系统磁盘/显卡缓存，不能标为目标硬件冷启动。

## 图形与性能

`[已实现]` NVIDIA GeForce RTX 2080 Ti，Compatibility/OpenGL。1280×720、1600×900、1920×1080 各 24 张，共 **72 张**：16 个玩家视角、4 个鸟瞰视角、4 个住宅室内视角。逐组查看九张联系表，并单独检查重点区域；未观察到修正前的院地闪烁、路面大片互穿或标牌字号异常。角色靠近镜头时的隐藏/透明沿用既有镜头行为。

| 分辨率 | 漫游均值 | 漫游 P95 | 环绕 P95 |
|---|---:|---:|---:|
| 1280x720 | 16.70 ms | 16.75 ms | 16.74 ms |
| 1600x900 | 16.70 ms | 16.76 ms | 16.75 ms |
| 1920x1080 | 16.70 ms | 16.75 ms | 16.77 ms |

每档采集 360 个实际漫游渲染帧及 180 个环绕帧。数据包含帧率节拍/VSync等待，约 60 Hz；不是纯 GPU 耗时，也不能证明低配余量。依据图形复核修正了光照过曝、草地条纹、标牌字形尺度、道路裁切、院地精度和远山形状；重复贴图已按字节/色彩空间合并。保留最早外观与结果在 `outputs/village-v1/before-first-visual/`，最终图位于 `validation/`。

- [带比例总平面图](../../outputs/village-v1/village-v1-site-plan.png)
- [村庄鸟瞰](../../outputs/village-v1/validation/1920x1080/17-overview.png)
- [主角家周边](../../outputs/village-v1/validation/1920x1080/01-home-facade.png)
- [乔姨杂货铺](../../outputs/village-v1/validation/1920x1080/05-shop.png)
- [旧站](../../outputs/village-v1/validation/1920x1080/14-station.png)
- [机器结果](../../outputs/village-v1/validation/results.json)

## 日志与复现

根目录 PowerShell：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build TownRpg.csproj --configuration Debug --no-restore
& 'D:\ProgramFiles\Blender\blender.exe' --background --python-exit-code 1 --python scripts/tools/build_village_v1.py -- check --root .
& 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe' --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-capture
```

对应日志：`outputs/village-v1-final-core-build.log`、`village-v1-final-core-tests.log`、`village-v1-final-game-build.log`、`village-v1-final-import.log`、`village-v1-final-asset-check.log`、`village-v1-final-rendered-probe.log`。快速 `--village-lookdev` 只拍摄五个画面，不执行完整专项，不得使用它声称路线/状态验证通过。

## 尚未验收

`[计划]` 负责人现场视觉确认、人工自然行走/镜头舒适度、真实 Xbox/物理 raw delta、目标 GTX 1660 级设备与冷启动、正式导出。图形进程报告音频驱动不可用并回退 dummy，本版未新增声音，也没有进行听音验收。自动注入输入和图形检查不替代人工游玩。居民/日程/购物/求助信与醒后剧情属于后续范围，不能描述为本版已实现。

`[固定约束]` 本机角色及含角色截图仅供本机非商业使用，禁止提交、推送、分发或导出；本轮未执行提交、推送或游戏导出。

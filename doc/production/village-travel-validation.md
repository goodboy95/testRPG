# 村庄赶路优化验收

> 本轮日期：2026-09-14。设计及后续工作见[分批优化计划](../design/village-optimization.md)。本记录独立于此前建模、醒来接入验收。

## 实现与边界

村庄 v2 创建玩家时设置 4.2 米/秒，正式与独立预览共用；显式 v1、住宅和梦境沿用 3.1 米/秒，Shift 慢走 1.4 米/秒。只修改区域速度配置，不修改全局默认值、步态参考速度或动画资产；无 Core、存档或内容版本变更。三维布局、碰撞、镜头和模型保持原状。

本轮测试输出集中在 `outputs/village-travel/`，正式接入专项沿用其独立时间戳目录 `outputs/village-wake/integration/`。历史固定位置的村庄 v2 结果不覆盖。3.1 对照速度只在测试过程中临时设置，同一 v2 外景、同一路点、同一输入驱动、TimeScale=1；生产入口没有速度覆盖参数。

## 规则与构建

- 180 项 Core／内容测试通过，0 失败、0 跳过。此前 174 项，新增两组各三档帧率的表现层移动规则，共 6 项；原 3.1 米/秒用例保留。
- Godot Debug 构建 0 警告、0 错误。
- 日志：`outputs/village-travel/tests.log`、`outputs/village-travel/build.log`。

## 引擎与录像

### 同场景输入对照

| 路线 | 原速实际输入时间 | 新速实际输入时间 | 缩短 |
| --- | --- | --- | --- |
| 家门外到杂货铺 | 30.72 秒 | 22.80 秒 | 25.8% |
| 溪岸回环 | 125.02 秒 | 92.68 秒 | 25.9% |
| 旧站回环 | 89.22 秒 | 66.15 秒 | 25.9% |

记录为实际输入驱动走查累计的游戏物理时间，包含路点接近减速及转向，TimeScale=1；不是单纯长度除速度，也不作为 GPU 或输入设备性能认证。原始对照：`outputs/village-travel/20260914-164311-v2/comparison.json`。

首轮三条对照、全部路线与桥头检查均已完成，随后新增推车碰撞测试失败，整轮 exit 1，日志 `outputs/village-travel/v2-probe.log` 保留。原因是测试选择了推车南侧、紧邻广场大树的接近点，不能断言角色一定抵达推车本体。修正为北侧通路，并在断言中记录实际位置和接触间隙；游戏模型和碰撞未改动。成功的速度对照继续有效，后续用 `--village-travel-verify` 复跑全部新速度路线与专项，跳过已经完成的两档计时对照。

### 最终引擎回归

- v2 最终专项通过 384 项，exit 0，无错误；报告 `outputs/village-travel/20260914-165230-v2/results.json`，日志 `outputs/village-travel/v2-verify.log`。覆盖三条原路、五条工作接近路线、五处净空、双桥两向、实际家具阻挡、退出、急停、慢走、暂停／失焦、住宅往返、重置和 billboard 回退。六处实跑接触时角色中心到碰撞面的间隙约 0.297–0.302 米，与胶囊半径一致。
- v1 显式回归通过 343 项，exit 0，无错误；报告 `outputs/village-travel/20260914-165446-v1/results.json`，日志 `outputs/village-travel/v1-probe.log`。验证原资源和 3.1 米/秒默认值、三条路线、桥头、住宅往返与输入恢复。检查数量包含沿路采样，随行走速度变化，不与以前的检查计数直接比较覆盖率。
- 正式醒后专项通过 83 项，exit 0，无错误；此前 74 项基础上增加每档分辨率三项区域速度断言。报告 `outputs/village-wake/integration/20260914-165708/report.json`，日志 `outputs/village-travel/wake-probe.log`。覆盖家中 3.1、出门 4.2、室外读档 4.2、门灯与位置恢复、重复转场、安全点、旧完成档迁入、ExitOpened 收尾及 Legacy 路由；正式存档、备份和临时文件哈希未变化。
- 九场景冒烟通过，exit 0，无错误：`GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。日志 `outputs/village-travel/smoke.log`，包含角色动作、大厅往返、序章对白及 UI 检查。

### 动作录像与尺寸

输出目录为 `outputs/village-travel/20260914-170001-video/`；三个文件分别为 `motion-1280x720.mp4`、`motion-1600x900.mp4`、`motion-1920x1080.mp4`。各 240 帧、30 FPS、8 秒；三档实际尺寸全部验证，并逐帧解码检查编码结果。1080p 使用无边框全屏，避免窗口被可用桌面区域降档。原始逐帧图、动作记录、四格联系图、`encoded-videos.json` 与 `motion-summary.json` 保留在同一目录。

录像按固定 30 FPS 仿真步长采集，覆盖连续跑动、转向、急停、慢走和反向起步；不代表实机实时帧性能。稳定跑动片段三档速度均为 4.2、状态为 jog、步态相位持续推进；停稳片段速度为 0，慢走片段为 1.4。脚本没有改写动画周期或钳制新速度。日志 `outputs/village-travel/video.log` 与 `outputs/village-travel/encode.log` 均完成。

代理抽查了三档分辨率的跑动、转向、停稳和慢走画面，角色、镜头与提示可见，没有发现新出现的主要遮挡。联系图与状态采样不能证明所有混合帧都没有滑步；动作自然度仍留给负责人实际体验。

专项可复现命令（从项目根目录运行）：

```powershell
dotnet test tests/TownRpg.Core.Tests/TownRpg.Core.Tests.csproj --no-restore
dotnet build TownRpg.csproj --no-restore
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-travel-check
# 只复验新速路线和碰撞，跳过已完成的两档对照：在上一条命令后追加 --village-travel-verify
# 原版速度与资源回归，同样输出到本轮独立目录
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-travel-check --village-v1
# 每档 240 帧、30 FPS、8 秒；固定步长用于动作观察，不用于实机性能认证
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --fixed-fps 30 --path . --scene res://scenes/testing/VillageV1Probe.tscn -- --village-travel-video
# 重新录制后改为该次实际输出目录
C:/Python314/python.exe scripts/tools/build_village_travel_review.py outputs/village-travel/20260914-170001-video
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --audio-driver Dummy --path . --scene res://scenes/testing/VillageWakeProbe.tscn -- --prologue-e2e
& D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe --headless --audio-driver Dummy --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
```

## 工作副本审查

开工前对 Git 可见工作副本按实际 UTF-8 路径记录 SHA256（包括当时未跟踪文件），保存于本机临时目录 `village-travel-start.json`。本轮增量为 11 个已有文件修改、4 个新文件，无删除；模型、资产、布局、Core、剧情／本地化、存档实现、共享玩家控制器及动画驱动均未改动。结果见 `outputs/village-travel/increment.json`。此前用户已有修改保留，没有重建源资产。

本地文档 548 个链接有效，52 份文档全部从 AGENTS 可达，最多两次点击；新优化计划和验收均一次点击可达。`git diff --check` 通过，仅原有换行转换提醒。检查结果见 `outputs/village-travel/links.json` 和 `outputs/village-travel/diff-check.log`。将生产文件中的新速度配置与注释逆向还原后，其 SHA256 与开工前完全一致，确认该文件没有附带其他改动。

## 人工验收与未执行项

负责人操作手感、动作自然度：待确认。合成输入和固定步长录像不等于真实鼠标 raw delta、实体 Xbox 或目标硬件验收。含本机角色的录像、截图只限本机使用，不提交、分发或导出。

本轮不重复完整序章输入通关，不运行 Blender 重建、模型导出或游戏导出；没有安装地形插件、生成贴图、提交或推送。地形与材质仅写入计划。

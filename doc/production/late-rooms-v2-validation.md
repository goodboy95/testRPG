# 后两室 v2 与四室序章验收

日期：2026-09-10。`[已实现]` 档案室、礼堂扩大重制与自动化完整流程验证。`[计划]` 负责人视觉终验、真实设备与玩家盲测。两类结果分开记录。

实现见[档案室](../engineering/archive-remodel-v2.md)、[礼堂](../engineering/auditorium-remodel-v2.md)。新旧实机对照与两份俯视布局见[本机复核页](../../outputs/late-rooms-v2/review.md)。输出目录及含本机角色的截图禁止提交、分发与导出；环境源/GLB 不含该角色。

## 资产与空间

按“档案室灰模→细模→礼堂灰模→细模→全流程”执行。每室灰模先通过真实胶囊路线检查，随后采用细模、材质与灯光。

| 项目 | 档案室 v2 | 礼堂 v2 |
| --- | --- | --- |
| 室内尺寸 | 14×12×4.2 m | 22×16×7.5 m |
| 中央／主要次通路 | 2.8／1.5 m | 3.38／1.5 m |
| 交互点／碰撞定义 | 13／20 | 15／56 |
| 实际三角面 | 90,240（上限 180,000） | 86,004（上限 260,000） |
| 网格对象／唯一网格 | 53／53（对象上限 220） | 97／66（对象上限 320） |
| 材质 | 13（上限 24） | 15（上限 32） |
| GLB 字节 | 32,433,952（上限 60 MiB） | 32,833,880（上限 80 MiB） |
| 本机只读复验 | `ARCHIVE_V2_CHECK_PASS` | `AUDITORIUM_V2_CHECK_PASS seats=32 sharedMesh=true` |

两份 manifest 的源、GLB 和布局 SHA-256 与当前文件一致，实际 GLB accessor、节点和材质检查通过。礼堂 32 座在 Blender、GLB 与 Godot 均共享一个网格。舞台保持景观，全部交互位于同一地面；墙、家具与舞台碰撞均在场景中生效。

资产日志：`outputs/late-rooms-v2/archive-final-check.log`、`auditorium-final-check.log`；灰模与细模构建/导入日志同目录。v1 文件保留历史。

## 自动化结果

| 验证 | 实际结果 |
| --- | --- |
| Core／内容 | 145/145，0 失败、0 跳过。143 基线新增两室实际 GLB／共享布局检查各 1 项。`core-tests.log`。 |
| Godot Debug C# | 编译 0 警告、0 错误。引擎固定 4.6.3 Mono，Compatibility；未升级依赖。 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。`godot-smoke.log`。 |
| 序章对白与机关 | `PROLOGUE_V6_PASS groups=12 stories=13 imported=21 cancellation paper photo stage gamepad_undo`。 |
| UI 回归 | `UI_REMAKE_PASS scales=3 native_enter native_a mouse_drag no_references focus drag_payload cancel_drag sequence_undo explicit_submit`，另断言原生控件焦点下 Esc 关闭舞台及顺序面板。 |
| Legacy／移动 | 既有 Core 回归及 `CHARACTER_MOTION_PASS ... legacy fallback` 通过；大厅 16 次换场检查通过。 |
| 两室场景专项 | 档案室 13 标记／14 段胶囊路径／12 个旧档恢复位置；礼堂 15 标记／16 段路径／48 个旧档恢复位置。灰模、细模均通过。 |
| 字号与字幕 | `LATE_ROOMS_UI_PASS captures=144 resolutions=3 menu_scales=4 independent_subtitles workspace_confirm_bounds`。 |

两室场景专项使用真实 `PlayerController3D` 胶囊和碰撞，但以独立状态夹具检查安装、预览、开柜、门、相机缩臂、家具阻挡及晓雨连续绕行；它不代替下面的完整通关。有效旧位置保持，障碍内位置恢复。专项还验证普通模态暂停、协作位置可达、舞台/抽屉阻挡与状态重新实例化。

完整输入测试发现舞台原生按钮有焦点时 Esc 未到达 `_UnhandledInput`。机关与顺序面板改在 `_Input` 处理取消，子控件拖动取消仍先处理；新增焦点下 Esc 回归，原生鼠标拖动、拖动取消、手柄 B 与撤销旧检查继续通过。此次仅修影响操作的问题，其余 UI 统一仍等待样板视觉确认。

## 从标题开始的实际输入路线

最终通过运行：`outputs/late-rooms-v2/prologue-e2e/20260910-090625/`；总日志 [e2e-run.log](../../outputs/late-rooms-v2/e2e-run.log)，结构化 [report.json](../../outputs/late-rooms-v2/prologue-e2e/20260910-090625/report.json) 与逐步 [trace.json](../../outputs/late-rooms-v2/prologue-e2e/20260910-090625/trace.json)。

```text
PROLOGUE_E2E_PASS walks=76 meters=571.42 clicks=170 reloads=6 pins=4 events=13 keyboard_mouse joypad isolated_save
```

`clicks` 是历史计数名称，此次实际记录 170 次界面按钮激活，主要使用获得控件焦点后发送 Enter；不等同 170 次物理鼠标点击。世界中通过合成捕获鼠标移动转向、W 行走、F 交互；手柄通过真实输入事件覆盖方向导航、A 调整/确认、B 返回、X 背包及最终四步合作。角色使用正常物理处理、加减速、`MoveAndSlide` 与场景碰撞，路径规划只决定输入方向；主路径没有设置角色位置，也没有填充物品、已解谜题或事件集合。

路线为标题新游戏→第一室→大厅→音乐室相识→档案检索→一次集中回访教室地图柜、音乐室灯箱→档案修复与时间线→礼堂双视角、母带、交接→现有完成页→返回标题。另为中断检查从礼堂离房一次后返回，不产生第二次主谜题集中回访。

检查包括缺少钟表材料、错误窗扣答案、取消并重试开场对白、关闭机关、重开预览复位、背包打断协作、离房取消协作、合成失焦清零移动、重载清除持杆。已完成录音机不再提供安装目标，在原地再次按 F 不消耗、不领奖、不提交；终点重载不重复事件。最终 13 个序章事件齐全，Facts／Evidence／Leads 未被梦境记录污染，状态仍为 `ExitOpened`，没有扩写醒后内容。

## 存读档与大头针

主路径使用 `GameSession.UseTestSavePath`，真实自动保存至本次输出目录；没有启用写盘抑制。所有继续、写入、`.tmp`、`.bak` 和恢复使用同一隔离路径。正式 `user://save-slot-1.json` 及其备份、临时文件前后哈希／存在性一致；越出项目输出目录的测试路径被拒绝。

| 检查点文件 | 验证 |
| --- | --- |
| `music-open.json` | 第二室开门相识后，移除主档、留下实际备份与中断 `.tmp`，标题“继续”恢复原进度。 |
| `concentrated-backtrack.json` | 回访教室地图柜后返回标题并继续。 |
| `recorder-partial.json` | 卷盘、摇柄已实际安装，保险丝尚未装入；继续后保留部分装配。 |
| `handoff-rehearsed.json` | 四步排演持久完成，重载后尚需当前实例操作。 |
| `cooperation-held.json` | 持杆时返回标题再继续；持久进度相同，临时持杆清除。 |
| `exit-opened.json` | 出口完成页返回标题再继续，恢复同一完成页且事件集合不变。 |

四室分别从实际获得的检查点执行大头针取消、确认和终态重载；确认后物品清空、`CompletedPin`、`old_house` 与既有完成页均正确。测试随后仅把分支前真实保存的文件字节写回隔离路径，通过标题“继续”恢复主线，不构造进度。四份 `pin-dream.*.json` 与六份检查点保留在本次目录中。

## 画面与帧时

旧版 24 张对照、灰模 39 张、最终两室三分辨率 117 张，均在 `outputs/late-rooms-v2/`。档案室每档 17 图，礼堂每档 22 图，覆盖入口、主要路线、机关前后态、资料承托、近景、合作及碰撞。`ui/` 另有 144 张：1280×720、1600×900、1920×1080，菜单 100%／125%／150%／200%，每档再检查独立字幕倍率。包含检索、时间线、舞台正/侧面、磁带、排演、独立资料和字幕。

字号专项使用明确隔离的显示夹具，不冒充输入通关；工作区及确认按钮不超屏，字幕不超过两行和安全区域。200% 时舞台反馈允许在现有滚动区阅读。抽查大字号舞台、时间线与独立 200% 字幕实图，未见覆盖确认键或字幕截断。其余 UI 延用既有样式。

GPU：NVIDIA GeForce RTX 2080 Ti。每个视点采样 180 帧；档案室一个环绕点，礼堂两个点。数据分别在 `archive/final/metrics.json`、`auditorium/final/metrics.json`。

| 房间 | 平均帧时（各分辨率范围） | P95 范围 | 最大单帧 |
| --- | --- | --- | --- |
| 档案室 | 16.694–16.696 ms | 16.776–16.796 ms | 16.929 ms |
| 礼堂 | 16.692–16.696 ms | 16.768–16.792 ms | 17.315 ms |

该结果受约 60 Hz 帧节奏限制，不是无上限 GPU 基准。截图专项记录的实例加载加首帧等待为档案室 **16,097 ms**、礼堂 **1,190 ms**。为核对档案室等待，独立重跑 `ArchiveV2Probe -- --behavior-only`，通路/状态再次通过，加载为 **991 ms**，见 `archive-load-recheck.log` 与 `archive/final/behavior.json`。原始慢记录未覆盖；一次复测不能证明冷启动稳定达标。需要在目标硬件上区分首次导入/着色器、冷暖缓存与实际发行加载再验收。

## 复现

在根目录 PowerShell，先完成 AGENTS.md 中 Core 测试、Godot 恢复与构建；构建失败时不得继续使用旧程序集。

```powershell
$GodotConsole = 'D:/ProgramFiles/Godot/Godot_v4.6.3-stable_mono_win64_console.exe'
& $GodotConsole --headless --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& $GodotConsole --path . --audio-driver Dummy --scene res://scenes/testing/ArchiveV2Probe.tscn
& $GodotConsole --path . --audio-driver Dummy --scene res://scenes/testing/AuditoriumV2Probe.tscn
& $GodotConsole --path . --audio-driver Dummy --scene res://scenes/testing/LateRoomsUiProbe.tscn
& $GodotConsole --path . --disable-vsync --audio-driver Dummy --fixed-fps 60 --scene res://scenes/testing/PrologueE2EProbe.tscn -- --prologue-e2e
python scripts/tools/write_late_room_layout.py archive
python scripts/tools/write_late_room_layout.py auditorium
```

完整输入通关需要图形窗口以验证捕获鼠标转向，不使用 headless；固定模拟帧长只提高自动运行效率，仍由物理行走。测试音频使用 Dummy，本机 WASAPI 无可用输出设备，因此没有宣称真实听音通过。每次完整流程生成独立时间戳目录，不修改玩家正式存档。

`[计划]` 负责人确认模型、材质、灯光与纸面可读性；至少 3 名玩家不看攻略完成 60–90 分钟节奏/提示盲测；真实 Xbox、物理 raw delta、GTX 1660／目标硬件、真实音频和 Windows x64 导出验收。上述自动化结果不代表这些项目已完成。

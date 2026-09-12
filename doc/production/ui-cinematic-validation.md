# 电影感 UI 第一阶段：实际验收

> 2026-09-09；Windows，Godot 4.6.3 Mono / Compatibility，RTX 2080 Ti。

`[已实现]` 四类引擎样板、独立字幕设置与自动化验证完成。`[计划]` 视觉确认后再推广至其余页面；当前结果不等同于阅读舒适度、真实 Xbox 或完整人工游玩验收。

## 构建与行为

| 检查 | 实际结果 |
| --- | --- |
| Core build | 通过，0 警告 / 0 错误。 |
| Core / 内容测试 | 143/143，0 跳过；135 基线新增 5 项字幕设置/旧档兼容、3 项分页测试。TRX：`outputs/ui-cinematic/tests/cinematic-ui.trx`。 |
| Godot 本地包 restore / Debug build | 通过，0 警告 / 0 错误。未升级引擎、SDK 或插件。 |
| 九场景冒烟 | `GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=21 items=31`。 |
| 序章专项（冒烟内） | `PROLOGUE_V6_PASS groups=12 stories=13 imported=21 cancellation paper photo stage gamepad_undo`，包含原子提交与取消重试。 |
| 原 UI 机关专项 | `UI_REMAKE_PASS scales=3 native_enter native_a mouse_drag no_references focus drag_payload cancel_drag sequence_undo explicit_submit`，独立运行和最终冒烟内均通过。 |
| 样板输入 | `CINEMATIC_INPUT_PASS mouse_history H Y B Enter A pagination unread retry auto tools focus fade settings`；最终 1080p / 100% 图形运行完成输入与布局检查。 |

字幕专项使用真实 Godot 控件与合成 InputEvent，检查鼠标打开历史、H/Y 工具、B 逐层返回、Enter/A 阅读、已读跳过、未读等待、取消后重试、长题干分页后再出现选择、方向键选项、焦点恢复、模态阻断移动、自动暂停与恢复、淡入、八字姓名、实时字号重排与设置预览一致性。背景和菜单倍率变化保持当前页，关闭工具保留此前选中的选项。

初次冒烟发现两帧延迟恢复导致 Tab 打开背包后暂时没有焦点。已修复为立即聚焦、两帧后只校正滚动，且不夺取期间改变的焦点；重新执行九场景冒烟和全部样板矩阵通过。最终 `outputs/ui-cinematic/logs/` 没有 `ERROR`、`WARNING`、`FAIL` 或 `NOT_RUN`。

最终检查显式使用 `--audio-driver Dummy`，不代表音频验收。早期图形运行遇到 WASAPI 无输出设备而回退 Dummy；本轮没有修改音频代码。

## 截图与人工边界

`[已实现]` **17 状态 × 14 配置 = 238 张矩阵图**；四类同场景前后各一张，另有 8 张对照图。每张 PNG 真实宽高和全部 17 状态的布局 JSON 均通过审核，见 `outputs/ui-cinematic/audit.json`。图像由 Godot 原样保存，未缩放或修改像素。

| 配置 | 范围 |
| --- | --- |
| 常用字号 | 1280×720、1600×900、1920×1080，各 100% / 125% / 150%，153 张。 |
| 放大压力 | 三档分辨率各 200%，51 张。 |
| 独立倍率 | 1080p 菜单 100% / 字幕 200%，以及菜单 200% / 字幕 100%，34 张。 |

状态包括探索、真实线索提示、空/满背包与末行焦点、空/长记录、明亮窗前字幕、工具、历史、两行对白、选择、无背景、高对比、音乐教室、设置预览和临时低光场景。长记录、序章字幕及 Legacy 长句/选择采用现有游戏文本；前后对照沿用旧 `UiUxCapture` 的相同测试文本和进度，不作为新增剧情。低光图仅在测试实例降低环境与灯光，未改资产。

逐图断言字幕不超过两行，真实字体测量宽高不溢出、字幕安全区有效、管理面板不超屏、首行焦点不被顶端裁切。抽查默认、放大及两种混合倍率的主要页面、末行焦点、工具、选择和背景。阅读窗口外的内容通过滚动到达，未要求所有内容同时显示；自动布局检查不能证明所有设备上的阅读舒适度。

本机打开[前后对照与矩阵](../../outputs/ui-cinematic/review.html)，可切页面、字号、状态或原始像素视图。`before/` 为修改前捕获，`after/` 为最终源码捕获；`first/`、`second/`、`probe/` 属于迭代过程，不作为最终结果。含本机限定角色的截图由 `outputs/` 排除规则保护，未提交、上传 Figma 或导出。

评审页生成与 PNG 路径/尺寸审核通过；内置浏览器策略拦截 `file://`，未完成 HTML 页内按钮的浏览器验证。原始游戏截图已直接查看，HTML 可由负责人在本机打开；没有启动服务或使用其他方式绕过该限制。

## 复现

先执行根 AGENTS.md 的 Core 测试、本地包还原和 Godot Debug 构建，再运行：

```powershell
$CinematicGodot = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'
& $CinematicGodot --headless --audio-driver Dummy --path . --scene res://scenes/testing/GodotSmokeRunner.tscn
& $CinematicGodot --path . --audio-driver Dummy --scene res://scenes/testing/CinematicUiProbe.tscn -- --width=1920 --scale=1 --output=outputs/ui-cinematic/matrix/1080-100
# 布局矩阵：1280/1600/1920 与 1/1.25/1.5/2，附加 --capture-only，按顺序运行。
# 混合示例：
& $CinematicGodot --path . --audio-driver Dummy --scene res://scenes/testing/CinematicUiProbe.tscn -- --width=1920 --scale=1 --subtitle-scale=2 --capture-only --output=outputs/ui-cinematic/matrix/1080-menu100-sub200
& $CinematicGodot --path . --audio-driver Dummy --scene res://scenes/testing/UiUxCapture.tscn -- --width=1920 --scale=1 --only=exploration,inventory,records,dialogue --output=outputs/ui-cinematic/after/1080-100
python scripts/tools/build_cinematic_ui_review.py
```

输入专项需要窗口获得焦点；无焦点会明确报告自动恢复检查未执行。本次最终日志没有该跳过。所有夹具使用 `SuppressPersistence()`，不会修改玩家存档。1920 档在当前 1080p 桌面使用无边框全屏，保存前断言真实分辨率。

`[计划]` 负责人确认字号、位置和透明程度；真实输入设备、完整盲测、目标硬件与 Windows 导出仍待验收。其余 UI 外观统一属于下一阶段。

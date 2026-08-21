# 测试与 Windows 发布

## 环境基线

项目只支持以下开发基线：

- Windows x64；
- Godot 4.6.3 stable Mono/.NET；
- Compatibility/OpenGL；
- .NET SDK 10.0.400 用于命令行，项目目标框架 `net8.0`；
- Dialogue Manager v3.10.4（当前未 vendored）；
- 640×360 内部视口和整数缩放。

当前工作机路径：

```text
Godot: D:\ProgramFiles\Godot
dotnet: C:\Program Files\dotnet\dotnet.exe
```

路径不是项目契约，版本才是。换机时设置新的 PowerShell 变量，不要把本机绝对路径写进 Godot 资源或业务代码。

## 用编辑器运行

最简单方式是启动 Godot 4.6.3 Mono，导入或打开根目录 `project.godot`。等待资源导入和 C# 编译结束：

- `F5`：运行 `res://scenes/app/Main.tscn`；
- `F6`：运行当前打开场景；
- 出现 C# 构建错误时先查看 Godot 底部 Build/Debugger，而不是更换引擎版本。

也可从 PowerShell 打开编辑器：

```powershell
$GodotRoot = 'D:\ProgramFiles\Godot'
$GodotEditor = Join-Path $GodotRoot 'Godot_v4.6.3-stable_mono_win64.exe'
& $GodotEditor --editor --path .
```

## Core 构建和测试

Core 不依赖 Godot，应先独立验证：

```powershell
$DotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
& $DotnetExe build .\src\TownRpg.Core\TownRpg.Core.csproj --configuration Release
& $DotnetExe test .\tests\TownRpg.Core.Tests\TownRpg.Core.Tests.csproj --configuration Release
```

截至 2026-08-17 的基线是 **84/84 通过，0 失败，0 跳过**。相对旧 71 项新增 13 项，覆盖梦境第一室出生、分散观察链、固定答案、顺序开放、跨房间回访、错误无副作用、晓雨加入前提示门槛、三级提示、刺醒、存档往返、Legacy 兼容和非法梦境状态拒绝。既有测试继续覆盖：

- 关系阶段、有方向关系、截断、礼物和救援同行资格；
- 承诺状态机、不随日期过期、时间推进和模态引用计数；
- 暴露、路线、救援奖励、三档结局；
- 情境战零关系可胜、联携、战败和撤退；
- 存档往返、坏 JSON、版本拒绝和非法状态；
- 事件候选、日程边界、下一可见时段和重叠校验；
- 叙事白名单、原子回滚、结果幂等和纯查询；
- JSON schema、角色/事件引用、对白跳转、静态 ID、本地化和 Godot 资源路径。

测试数量以后可以变化，但交付报告必须解释新增、删除或跳过项，不能机械要求永远等于 71。

## Godot C# 构建

只使用已安装 Godot 4.6.3 随附的离线 NuGet 包源：

```powershell
$GodotRoot = 'D:\ProgramFiles\Godot'
$DotnetExe = 'C:\Program Files\dotnet\dotnet.exe'
$GodotPackages = Join-Path $GodotRoot 'GodotSharp\Tools\nupkgs'

& $DotnetExe restore .\TownRpg.csproj --source $GodotPackages --ignore-failed-sources
& $DotnetExe build .\TownRpg.csproj --configuration Debug --no-restore
& $DotnetExe build .\TownRpg.csproj --configuration Release --no-restore
```

不要增加网络 NuGet 源，不要通过升级 Godot SDK 解决编译错误。`.godot/`、`bin/`、`obj/` 是生成目录，不应手工编辑。

## 引擎冒烟

```powershell
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'

& $GodotConsole --headless --path . --quit-after 30
& $GodotConsole --headless --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn'
```

第二条命令必须输出：

```text
GODOT_SMOKE_PASS scenes=9 characters=5 schedules=21 events=8 items=8
```

冒烟验证 Autoload、九场景和物品目录，并对 3D 第一室检查 GLB/着色器加载、四个相机角位、旋转锁、普通/减少闪烁裁墙、二维/三维坐标往返、旧坐标安全落点、Fixed-Y billboard、九个唯一且可达交互标记、四类视觉状态、19 组静态碰撞与物理通阻探针。它不能证明谜题理解、手柄手感、存档 I/O 或完整流程正确。

第一室 2D 基线仍可用原有 Pillow 命令复核；当前 3D MVP 另执行 Blender 只读校验和九张截图采集：

```powershell
$PythonExe = 'C:\Python314\python.exe'
& $PythonExe .\scripts\tools\build_dream_pixel_art.py check
& $PythonExe .\scripts\tools\prepare_original_classroom.py check

$BlenderExe = 'D:\ProgramFiles\Blender\blender.exe'
& $BlenderExe --background --factory-startup --python .\scripts\tools\build_classroom_3d.py -- check --root (Get-Location).Path

$CaptureDir = Join-Path (Get-Location) 'outputs\classroom-3d-mvp\3d'
& $GodotConsole --path . --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- "--capture-classroom-3d=$CaptureDir"

& $GodotConsole --path . --resolution 1280x720 --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- --profile-classroom-3d
& $GodotConsole --path . --resolution 1920x1080 --scene 'res://scenes/testing/GodotSmokeRunner.tscn' -- --profile-classroom-3d
```

预期出现 `DREAM_PIXEL_ART_CHECK_PASS files=5 paletteMax=32`、`ORIGINAL_CLASSROOM_CHECK_PASS source=1672x941 occluders=4`、`CLASSROOM_3D_CHECK_PASS meshes=130 desks=10` 与 `CLASSROOM_3D_CAPTURE_PASS files=9`。截图覆盖东南/西南/西北/东北四个方向、初始/黑板/教师桌/地图柜四类状态及碰撞调试；截图采集是视觉复核证据，不代替完整人工操作。

## 四室梦境人工验收

1. 新游戏确认姓名后直接出生在第一室，能从内侧前往走廊；进入前及 HUD 不出现“梦境”“醒来”“跳过”。
2. 用键盘和 Xbox 分别完成：粉笔显序→窗/钟/值日表观察→`372`→钥匙开启第二室→`1-4-2-3`→三处辨听→社团档案→第一室合影→第二室点名柜→第三室录音机→第四室三板与双侧机关。
3. 每次开新房后返回此前房间；在走廊和四室分别保存/继续，确认区域、坐标、物品、线索和提示等级恢复。
4. 对每个目标尝试至少一个错误道具和错误答案，确认不消耗、不解锁、不自动保存伪进度。
5. 救出晓雨前确认没有提示页且 Core 拒绝领取；加入后逐级领取 0–3，确认无资源或关系惩罚。
6. 关闭声音或只看屏幕完成辨听；确认文字与波形可用。不得只用颜色区分滤色和控制板状态。
7. 在走廊和四室分别取消一次大头针确认；再于五个区域分别确认刺醒，验证清空梦境物品、无现实纸条、重载不能重进或领奖。
8. 完成出口后确认只显示第一阶段开发完成页，不揭示正式信息、不生成纸条、不标记 `CompletedEscape`。
9. 以 100%/125%/150% 字号检查物品/记录/晓雨分页、数字直输、逐项撤销、确认框的焦点链与取消行为。
10. 第一室默认无悬浮物件标签；接近粉笔、黑板、窗、时钟、值日表、教师桌和地图柜时出现地面光环且底部提示与目标一致。检查普通/高对比光环，确认角色不能穿过墙、柜和桌椅但可通过主要过道与东侧门洞。
11. 在第一室用 `Q/E` 和 `LB/RB` 依次转过四个角位；确认严格每次 90°、约 0.3 秒、快速连按不排队，旋转期间角色/焦点/交互暂停，离开到 2D 走廊后旋转键无效，返回时重置为东南镜头。
12. 在四个镜头方向分别检查 WASD/左摇杆始终按屏幕方向移动，主角下/左/右/上动画由实际位移选择且停止时保留朝向；后续房间的晓雨仍使用新版 4×4 2D 素材和既定同行位置。
13. 普通模式确认墙体与镜头同步淡变；“减少闪烁”下旋转期间只出现完全显示/隐藏状态，不出现半透明墙。检查近墙不投射异常阴影或遮挡交互。

目标游玩时长 30–45 分钟必须通过真实首次游玩记录确认，自动测试和熟练开发者速通不能替代。

## Legacy 垂直切片人工验收

### 主路径

1. 从标题开始新游戏，输入中文姓名和称谓，也测试空值默认。
2. 确认站立不走时间、实际移动走时间、顶墙不走时间。
3. 完成醒来、四人重逢，并和乔姨交谈。
4. 分三次存档或新游戏覆盖河岸、档案、怀表三项活动。
5. 建立承诺，在手册确认参与者、描述和待履行状态。
6. 睡到第二天，确认 NPC 换位和手册下一可见时段。
7. 阅读工程告示，分别验证线路证据和公司证据；确认承诺变为已履行。
8. 首次异象分别验证【隐瞒】【冒险】和撤离；只有冒险提高暴露。
9. 战斗覆盖基础破坏获胜、观察+安抚联携、战败重试、主动撤退后再挑战。
10. 完成后存档、返回标题、继续游戏，确认世界、关系、承诺、手册和设置一致。

### 软锁与异常

- 第一天立即睡觉、连续睡 3 天后仍可找到主线入口。
- NPC 不在场时能从手册理解下一时段；关键线索仍有世界入口。
- 23:00 后可移动、换图和回旧宅，不强制传送。
- 对话中退出或关闭场景，不应永久持有 ModalGate；事件未提交可重试。
- 连续快速确认不能重复发关系、经验或事件结果。
- 旧/坏存档明确报错；只有 `.bak` 时可恢复；失败不覆盖可用旧档。
- 暂停菜单“脱离卡住”只移动到安全点，不改变剧情事实。

### 输入、显示与辅助

| 项目 | 验收标准 |
| --- | --- |
| 键盘 | WASD/方向键、F/空格、J、Esc 均正确；3D 第一室额外检查 Q/E；UI 确认不重复触发世界。 |
| Xbox 手柄 | 左摇杆、A、Y、Menu 正确；提示随最后输入设备切换。 |
| 1280×720 | 2× 整数缩放、像素边缘稳定、文字可读。 |
| 1920×1080 | 3× 整数缩放、无非整数模糊或错误裁切。 |
| 字号 | 100%/125%/150% 不遮挡关键按钮和选项。 |
| 高对比 | 交互提示有不透明深色背景。 |
| 闪烁 | 关闭闪烁后无快速明暗变化；当前切片默认无闪烁。 |
| 故事模式 | 战斗不因来袭失去意志，但仍需完成情境目标。 |
| 中文输入 | Windows IME 可输入；失败时默认姓名仍可继续。 |

### 性能

目标是在 1080p 显示环境和中低端集显上保持 60 FPS，地图切换约 2 秒以内。第一室使用 4× 3D MSAA；记录机器、分辨率、窗口模式、平均/最低 FPS、旋转帧时间和 2D/3D 换场耗时，不要只写“感觉流畅”。

2026-08-21 自动窗口采样（RTX 2080 Ti、Compatibility/OpenGL、60 FPS 上限、每档 240 帧）：

| 请求/实际窗口 | 平均 FPS | 最低瞬时 FPS | 最大旋转帧时 | 第一室场景加载 |
| --- | ---: | ---: | ---: | ---: |
| 1280×720 / 1280×720 | 60.0 | 52.2 | 17.02 ms | 89.70 ms |
| 1920×1080 / 1920×1061（桌面工作区限制） | 60.0 | 54.4 | 18.38 ms | 76.92 ms |

该记录证明当前独显和内部 640×360 Viewport 下达到目标，不代表中低端集显、完整四室长时游玩或 2D/3D 换场已经验收。

## Dialogue Manager 验收

插件接入后至少验证：

- 八个事件都从 `dialoguePath` 和 `startTitle` 实际加载，不再运行硬编码副本；
- 所有 `Narrative.Evaluate`/`Execute` 固定参数重载可被反射调用；
- 自动播放、已读跳过、历史、取消和选项都工作；
- 未知命令、错误参数、缺失 title 和缺失本地化在开始新游戏前失败；
- 取消不完成事件，完成只提交一次；存读档后对白条件与同一 `GameState` 一致。

## Windows x64 导出

`export_presets.cfg` 已有 `Windows x64` preset，输出到 `exports/town-rpg/`，排除文档、测试、工作区和 outputs。当前工作机缺少 Godot 4.6.3 Mono 导出模板，因此截至 2026-08-15 尚未执行发布导出。

模板已由环境负责人提供且版本完全匹配后，执行：

```powershell
$GodotConsole = 'D:\ProgramFiles\Godot\Godot_v4.6.3-stable_mono_win64_console.exe'
& $GodotConsole --headless --path . --export-release 'Windows x64' 'exports/town-rpg/town-rpg.exe'
```

导出成功后不要只验证 exe 存在：

1. 在不打开 Godot 编辑器的环境运行导出版本。
2. 完成标题→新游戏→旧宅→存档→退出→继续的最小路径。
3. 验证 JSON、对白、本地化、标题图和 C# 程序集均被包含。
4. 检查用户目录能创建和恢复存档。
5. 将整个 `exports/town-rpg/` 目录打包为免安装 ZIP，不只单独复制 exe。
6. 解压到包含中文或空格的路径再次运行。

## 盲测通过标准

邀请 2–3 名熟人，不口头解释目标。每人应能完成切片，并能：

- 找到目标 NPC 或从手册判断下一时段；
- 解释【承诺】和暴露征兆；
- 指出至少两项自己的行为改变了后续反馈；
- 在战败或撤退后继续；
- 保存、退出并恢复。

若多数测试者做不到，暂停 Day 3 以后内容，优先修改因果提示、手册、承诺和风险反馈。

## 验收记录原则

每次交付记录：提交/源码状态、日期、机器环境、实际命令、通过数、人工覆盖路径、截图或日志位置、未执行项和阻断原因。不得把“代码存在”“自动测试通过”“场景冒烟通过”和“人工完整验收通过”混写成同一状态。
